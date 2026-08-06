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

    /// <summary>Чтение конфигурации (циклический опрос — Low, по кнопке — High).</summary>
    public async Task<InputConfig> ReadConfigAsync(
        ModbusPriority priority = ModbusPriority.High, CancellationToken ct = default)
    {
        var selectors = await transport.ReadHoldingRegistersAsync(
            P.SelectorRegsBase, P.Count, priority, ct);
        var noNc = await transport.ReadCoilsAsync(
            P.NoNcCoilsBase, P.Count, priority, ct);
        return new InputConfig(selectors, noNc);
    }

    /// <summary>Запись назначения (селектора) одного входа — сразу при изменении в UI.</summary>
    public Task WriteSelectorAsync(int index, int value, CancellationToken ct = default)
        => transport.WriteSingleRegisterAsync(P.SelectorRegsBase + index, value, ct);

    /// <summary>Запись флага НО/НЗ одного входа — сразу при изменении в UI.</summary>
    public Task WriteNoNcAsync(int index, bool isNc, CancellationToken ct = default)
        => transport.WriteSingleCoilAsync(P.NoNcCoilsBase + index, isNc, ct);

    /// <summary>Сброс дискретных входов: импульс бита в слове управления (read-modify-write).</summary>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        int mask = 1 << P.ResetTriggerBit;
        int word = (await transport.ReadHoldingRegistersAsync(
            P.ResetTriggerReg, 1, ModbusPriority.High, ct))[0];
        await transport.WriteSingleRegisterAsync(P.ResetTriggerReg, word | mask, ct);
        await Task.Delay(300, ct);
        await transport.WriteSingleRegisterAsync(P.ResetTriggerReg, word & ~mask, ct);
    }
}
