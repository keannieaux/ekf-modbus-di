namespace ShnoSetting.Core.Modbus;

public enum ModbusPriority
{
    /// <summary>Циклический опрос — выполняется, когда нет записей.</summary>
    Low,
    /// <summary>Команды пользователя (запись, чтение по кнопке) — приоритет над опросом.</summary>
    High
}

/// <summary>
/// Потокобезопасный асинхронный доступ к Modbus TCP.
/// Реализация сериализует все операции через одно соединение.
/// </summary>
public interface IModbusTransport : IDisposable
{
    bool IsConnected { get; }

    /// <summary>Срабатывает при установке/потере соединения (из фонового потока).</summary>
    event Action<bool>? ConnectionChanged;

    /// <summary>Задаёт параметры подключения и выполняет первое подключение.</summary>
    Task ConnectAsync(string ip, int port, byte unitId = 1, CancellationToken ct = default);

    Task DisconnectAsync();

    Task<bool[]> ReadCoilsAsync(int start, int count, ModbusPriority priority = ModbusPriority.Low, CancellationToken ct = default);
    Task<bool[]> ReadDiscreteInputsAsync(int start, int count, ModbusPriority priority = ModbusPriority.Low, CancellationToken ct = default);
    Task<int[]> ReadHoldingRegistersAsync(int start, int count, ModbusPriority priority = ModbusPriority.Low, CancellationToken ct = default);
    Task<int[]> ReadInputRegistersAsync(int start, int count, ModbusPriority priority = ModbusPriority.Low, CancellationToken ct = default);

    Task WriteSingleCoilAsync(int address, bool value, CancellationToken ct = default);
    Task WriteMultipleCoilsAsync(int start, bool[] values, CancellationToken ct = default);
    Task WriteSingleRegisterAsync(int address, int value, CancellationToken ct = default);
    Task WriteMultipleRegistersAsync(int start, int[] values, CancellationToken ct = default);
}
