using ShnoSetting.Core.Modbus;

namespace ShnoSetting.Core.Services;

/// <summary>Результат одного цикла опроса.</summary>
public sealed class PollResult
{
    public required InputStates Inputs { get; init; }
    public required bool[] StarterFeedback { get; init; }
    public required MeterData?[] Meters { get; init; }
    public DateTime? PlcTime { get; init; }
}

/// <summary>
/// Циклический опрос ПЛК с настраиваемым периодом (по умолчанию 1 с).
/// Все чтения идут с низким приоритетом — запись их вытесняет.
/// Если цикл не успел завершиться за период — тик пропускается.
/// График в опрос не входит (по ТЗ).
/// </summary>
public sealed class PollingService : IDisposable
{
    private readonly IModbusTransport _transport;
    private readonly DiscreteInputsService _inputs;
    private readonly StartersService _starters;
    private readonly MetersService _meters;
    private readonly ClockService _clock;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _loop;

    private volatile bool _enabled;

    public PollingService(
        IModbusTransport transport,
        DiscreteInputsService inputs,
        StartersService starters,
        MetersService meters,
        ClockService clock)
    {
        _transport = transport;
        _inputs = inputs;
        _starters = starters;
        _meters = meters;
        _clock = clock;
        _loop = Task.Run(LoopAsync);
    }

    /// <summary>Период опроса в миллисекундах (по умолчанию 1000).</summary>
    public int PeriodMs { get; set; } = 1000;

    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    /// <summary>Успешный цикл опроса (из фонового потока — маршаллить на UI).</summary>
    public event Action<PollResult>? Polled;

    /// <summary>Ошибка цикла опроса (из фонового потока — маршаллить на UI).</summary>
    public event Action<string>? PollError;

    private async Task LoopAsync()
    {
        var ct = _stop.Token;
        while (!ct.IsCancellationRequested)
        {
            if (_enabled && _transport.IsConnected)
            {
                try
                {
                    Polled?.Invoke(await PollOnceAsync(ct));
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    PollError?.Invoke(ex.Message);
                }
            }

            try { await Task.Delay(PeriodMs, ct); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task<PollResult> PollOnceAsync(CancellationToken ct)
    {
        return new PollResult
        {
            Inputs = await _inputs.ReadStatesAsync(ct),
            StarterFeedback = await _starters.ReadFeedbackAsync(ct),
            Meters = await _meters.ReadDataAsync(ct),
            PlcTime = await _clock.ReadTimeAsync(ct)
        };
    }

    public void Dispose()
    {
        _stop.Cancel();
        try { _loop.Wait(TimeSpan.FromSeconds(2)); } catch { /* игнорируем */ }
        _stop.Dispose();
    }
}
