using System.Threading.Channels;
using EasyModbus;

namespace ShnoSetting.Core.Modbus;

/// <summary>
/// Единственный владелец <see cref="ModbusClient"/>.
/// Все операции сериализуются через очередь с двумя приоритетами:
/// записи/команды пользователя (High) вытесняют циклический опрос (Low).
/// При обрыве связи выполняет автоматическое переподключение с задержкой.
/// </summary>
public sealed class ModbusDispatcher : IModbusTransport
{
    // Ограничения протокола Modbus на один запрос (с запасом).
    private const int MaxRegistersPerRequest = 120;
    private const int MaxCoilsPerRequest = 1968;
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(3);

    private abstract class Request
    {
        public abstract void Execute(ModbusClient client);
        public abstract void Fail(Exception ex);
    }

    private sealed class Request<T>(Func<ModbusClient, T> action, CancellationToken ct) : Request
    {
        private readonly TaskCompletionSource<T> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T> Task => _tcs.Task;

        public override void Execute(ModbusClient client)
        {
            try
            {
                if (ct.IsCancellationRequested) { _tcs.TrySetCanceled(ct); return; }
                _tcs.TrySetResult(action(client));
            }
            catch (Exception ex)
            {
                _tcs.TrySetException(ex);
                throw; // сообщаем циклу о сбое для обработки обрыва связи
            }
        }

        public override void Fail(Exception ex) => _tcs.TrySetException(ex);
    }

    private readonly Channel<Request> _high = Channel.CreateUnbounded<Request>();
    private readonly Channel<Request> _low = Channel.CreateUnbounded<Request>();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    private ModbusClient? _client;
    private string _ip = "";
    private int _port = 502;
    private byte _unitId = 1;
    private volatile bool _isConnected;
    private DateTime _nextReconnectAttempt = DateTime.MinValue;

    public ModbusDispatcher()
    {
        _loop = Task.Run(LoopAsync);
    }

    public bool IsConnected => _isConnected;
    public event Action<bool>? ConnectionChanged;

    public Task ConnectAsync(string ip, int port, byte unitId = 1, CancellationToken ct = default)
    {
        // Параметры пишем до постановки запроса в очередь — цикл прочитает их при подключении.
        _ip = ip;
        _port = port;
        _unitId = unitId;
        _nextReconnectAttempt = DateTime.MinValue;
        return Enqueue(_ =>
        {
            // Фактическое подключение выполняет EnsureConnected перед запросом;
            // здесь просто подтверждаем успех.
            return true;
        }, ModbusPriority.High, ct);
    }

    public Task DisconnectAsync()
    {
        return Enqueue<object?>(client =>
        {
            try { client.Disconnect(); } catch { /* соединение и так мёртвое */ }
            return null;
        }, ModbusPriority.High, CancellationToken.None);
    }

    public Task<bool[]> ReadCoilsAsync(int start, int count, ModbusPriority priority = ModbusPriority.Low, CancellationToken ct = default)
        => Enqueue(c => Chunked(count, (off, n) => c.ReadCoils(start + off, n), MaxCoilsPerRequest), priority, ct);

    public Task<bool[]> ReadDiscreteInputsAsync(int start, int count, ModbusPriority priority = ModbusPriority.Low, CancellationToken ct = default)
        => Enqueue(c => Chunked(count, (off, n) => c.ReadDiscreteInputs(start + off, n), MaxCoilsPerRequest), priority, ct);

    public Task<int[]> ReadHoldingRegistersAsync(int start, int count, ModbusPriority priority = ModbusPriority.Low, CancellationToken ct = default)
        => Enqueue(c => Chunked(count, (off, n) => c.ReadHoldingRegisters(start + off, n), MaxRegistersPerRequest), priority, ct);

    public Task<int[]> ReadInputRegistersAsync(int start, int count, ModbusPriority priority = ModbusPriority.Low, CancellationToken ct = default)
        => Enqueue(c => Chunked(count, (off, n) => c.ReadInputRegisters(start + off, n), MaxRegistersPerRequest), priority, ct);

    public Task WriteSingleCoilAsync(int address, bool value, CancellationToken ct = default)
        => Enqueue<object?>(c => { c.WriteSingleCoil(address, value); return null; }, ModbusPriority.High, ct);

    public Task WriteMultipleCoilsAsync(int start, bool[] values, CancellationToken ct = default)
        => Enqueue<object?>(c => { ChunkedWrite(values.Length, (off, n) => c.WriteMultipleCoils(start + off, Slice(values, off, n)), MaxCoilsPerRequest); return null; }, ModbusPriority.High, ct);

    public Task WriteSingleRegisterAsync(int address, int value, CancellationToken ct = default)
        => Enqueue<object?>(c => { c.WriteSingleRegister(address, value); return null; }, ModbusPriority.High, ct);

    public Task WriteMultipleRegistersAsync(int start, int[] values, CancellationToken ct = default)
        => Enqueue<object?>(c => { ChunkedWrite(values.Length, (off, n) => c.WriteMultipleRegisters(start + off, Slice(values, off, n)), MaxRegistersPerRequest); return null; }, ModbusPriority.High, ct);

    // ------------------------------------------------------------------

    private async Task LoopAsync()
    {
        var ct = _stop.Token;
        while (!ct.IsCancellationRequested)
        {
            Request? request = await WaitNextAsync(ct);
            if (request is null) continue;

            if (!EnsureConnected(out var connectError))
            {
                request.Fail(connectError!);
                continue;
            }

            try
            {
                request.Execute(_client!);
            }
            catch
            {
                HandleConnectionError();
            }
        }
    }

    private async Task<Request?> WaitNextAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_high.Reader.TryRead(out var high)) return high;
            if (_low.Reader.TryRead(out var low)) return low;

            var highWait = _high.Reader.WaitToReadAsync(ct).AsTask();
            var lowWait = _low.Reader.WaitToReadAsync(ct).AsTask();
            await Task.WhenAny(highWait, lowWait);
        }
        return null;
    }

    private bool EnsureConnected(out Exception? error)
    {
        error = null;
        if (_client is { Connected: true })
        {
            SetConnected(true);
            return true;
        }

        SetConnected(false);
        if (DateTime.UtcNow < _nextReconnectAttempt)
        {
            error = new IOException("Нет соединения с ПЛК");
            return false;
        }

        try
        {
            try { _client?.Disconnect(); } catch { /* игнорируем */ }
            _client = new ModbusClient(_ip, _port)
            {
                UnitIdentifier = _unitId,
                ConnectionTimeout = 2000
            };
            _client.Connect();
            SetConnected(true);
            return true;
        }
        catch (Exception ex)
        {
            _nextReconnectAttempt = DateTime.UtcNow + ReconnectDelay;
            error = ex;
            return false;
        }
    }

    private void HandleConnectionError()
    {
        try { _client?.Disconnect(); } catch { /* игнорируем */ }
        _nextReconnectAttempt = DateTime.UtcNow + ReconnectDelay;
        SetConnected(false);
    }

    private void SetConnected(bool value)
    {
        if (_isConnected == value) return;
        _isConnected = value;
        ConnectionChanged?.Invoke(value);
    }

    private Task<T> Enqueue<T>(Func<ModbusClient, T> action, ModbusPriority priority, CancellationToken ct)
    {
        var request = new Request<T>(action, ct);
        var channel = priority == ModbusPriority.High ? _high : _low;
        if (!channel.Writer.TryWrite(request))
            throw new InvalidOperationException("Modbus-диспетчер остановлен");
        return request.Task;
    }

    private static T[] Chunked<T>(int count, Func<int, int, T[]> read, int maxChunk)
    {
        var result = new T[count];
        int offset = 0;
        while (offset < count)
        {
            int n = Math.Min(maxChunk, count - offset);
            var part = read(offset, n);
            Array.Copy(part, 0, result, offset, n);
            offset += n;
        }
        return result;
    }

    private static void ChunkedWrite(int count, Action<int, int> write, int maxChunk)
    {
        int offset = 0;
        while (offset < count)
        {
            int n = Math.Min(maxChunk, count - offset);
            write(offset, n);
            offset += n;
        }
    }

    private static T[] Slice<T>(T[] source, int offset, int count)
    {
        var result = new T[count];
        Array.Copy(source, offset, result, 0, count);
        return result;
    }

    public void Dispose()
    {
        _stop.Cancel();
        _high.Writer.TryComplete();
        _low.Writer.TryComplete();
        try { _client?.Disconnect(); } catch { /* игнорируем */ }
        try { _loop.Wait(TimeSpan.FromSeconds(2)); } catch { /* игнорируем */ }
        _stop.Dispose();
    }
}
