using ShnoSetting.Core.Modbus;
using ShnoSetting.Core.Profiles;

namespace ShnoSetting.Core.Services;

/// <summary>Мгновенные состояния дискретных входов.</summary>
public sealed class InputStates(bool[] raw, bool[] output)
{
    public bool[] Raw { get; } = raw;
    public bool[] Output { get; } = output;
}

/// <summary>Конфигурация дискретных входов (селекторы + НО/НЗ).</summary>
public sealed class InputConfig(int[] selectors, bool[] noNc)
{
    public int[] Selectors { get; } = selectors;
    public bool[] NoNc { get; } = noNc;
}

/// <summary>Дискретные входы: до 64 штук, по ТЗ п. 4.1.</summary>
public sealed class DiscreteInputsService(IModbusTransport transport, ControllerProfile profile)
{
    private DiscreteInputsProfile P => profile.DiscreteInputs;

    /// <summary>Чтение мгновенных значений (циклический опрос).</summary>
    public async Task<InputStates> ReadStatesAsync(CancellationToken ct = default)
    {
        var raw = await transport.ReadCoilsAsync(P.RawCoilsBase, P.Count, ModbusPriority.Low, ct);
        var output = await transport.ReadCoilsAsync(P.OutputCoilsBase, P.Count, ModbusPriority.Low, ct);
        return new InputStates(raw, output);
    }

    /// <summary>Чтение конфигурации (по кнопке).</summary>
    public async Task<InputConfig> ReadConfigAsync(CancellationToken ct = default)
    {
        var selectors = await transport.ReadHoldingRegistersAsync(
            P.SelectorRegsBase, P.Count, ModbusPriority.High, ct);
        var noNc = await transport.ReadCoilsAsync(
            P.NoNcCoilsBase, P.Count, ModbusPriority.High, ct);
        return new InputConfig(selectors, noNc);
    }

    /// <summary>Запись конфигурации (по кнопке).</summary>
    public async Task WriteConfigAsync(InputConfig config, CancellationToken ct = default)
    {
        await transport.WriteMultipleRegistersAsync(P.SelectorRegsBase, config.Selectors, ct);
        await transport.WriteMultipleCoilsAsync(P.NoNcCoilsBase, config.NoNc, ct);
    }
}
