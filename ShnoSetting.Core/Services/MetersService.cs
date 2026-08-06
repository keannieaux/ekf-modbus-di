using ShnoSetting.Core.Modbus;
using ShnoSetting.Core.Profiles;

namespace ShnoSetting.Core.Services;

public enum MeterType
{
    /// <summary>CE318 (CE208)</summary>
    Ce318 = 0,
    /// <summary>CC301 (CC101)</summary>
    Cc301 = 1
}

/// <summary>Настройки одного слота счётчика.</summary>
public sealed class MeterSlotConfig
{
    public MeterType Type { get; set; }
    /// <summary>Адрес счётчика (DINT, 2 регистра); 0 = слот отключён.
    /// Для CE318 адрес совпадает с серийным номером.</summary>
    public int Address { get; set; }
    public bool IsActive => Address != 0;
}

/// <summary>Снимок счётчиков из циклического опроса: настройки слотов + данные.</summary>
public sealed class MetersSnapshot
{
    /// <summary>Настройки слотов (тип + адрес), читаются каждый тик.</summary>
    public required MeterSlotConfig[] Config { get; init; }
    /// <summary>Данные по слотам; null — слот отключён (адрес = 0).</summary>
    public required MeterData?[] Data { get; init; }
}

/// <summary>Данные, прочитанные из активного слота счётчика.</summary>
public sealed class MeterData
{
    public float[] Voltages { get; set; } = new float[3];   // по фазам
    public float[] Currents { get; set; } = new float[3];   // по фазам
    public float[] Powers { get; set; } = new float[4];     // 3 фазы + общая
    public float Energy { get; set; }                       // накопленная энергия
    public int Serial { get; set; }                         // DINT
    public bool CommOk { get; set; }                        // статус связи
}

/// <summary>Счётчики: до 6 слотов, по ТЗ п. 4.3.</summary>
public sealed class MetersService(IModbusTransport transport, ControllerProfile profile)
{
    private MetersProfile P => profile.Meters;

    public async Task<MeterSlotConfig[]> ReadConfigAsync(CancellationToken ct = default)
    {
        int[] types = await transport.ReadHoldingRegistersAsync(
            P.TypeRegsBase, P.SlotCount, ModbusPriority.High, ct);
        // Адрес — DINT: 2 регистра на слот, младшим словом вперёд.
        int[] addrRegs = await transport.ReadHoldingRegistersAsync(
            P.AddressRegsBase, P.SlotCount * 2, ModbusPriority.High, ct);

        var config = new MeterSlotConfig[P.SlotCount];
        for (int i = 0; i < config.Length; i++)
            config[i] = new MeterSlotConfig
            {
                Type = (MeterType)types[i],
                Address = RegisterConverter.ToDInt(addrRegs[i * 2], addrRegs[i * 2 + 1])
            };
        return config;
    }

    public async Task WriteConfigAsync(int slot, MeterSlotConfig config, CancellationToken ct = default)
    {
        await transport.WriteSingleRegisterAsync(P.TypeRegsBase + slot, (int)config.Type, ct);
        // Адрес — DINT: 2 регистра на слот, младшим словом вперёд.
        var (low, high) = RegisterConverter.FromDInt(config.Address);
        await transport.WriteMultipleRegistersAsync(
            P.AddressRegsBase + slot * 2, new[] { low, high }, ct);
    }

    /// <summary>
    /// Чтение настроек и данных счётчиков (циклический опрос).
    /// Data по слотам: null — слот отключён (адрес = 0).
    /// </summary>
    public async Task<MetersSnapshot> ReadDataAsync(CancellationToken ct = default)
    {
        // Конфиг в опросе читаем низким приоритетом — адреса редко меняются.
        int[] types = await transport.ReadHoldingRegistersAsync(
            P.TypeRegsBase, P.SlotCount, ModbusPriority.Low, ct);
        // Адрес — DINT: 2 регистра на слот, младшим словом вперёд.
        int[] addrRegs = await transport.ReadHoldingRegistersAsync(
            P.AddressRegsBase, P.SlotCount * 2, ModbusPriority.Low, ct);
        bool[] commStatus = await transport.ReadCoilsAsync(
            P.CommStatusCoilsBase, P.SlotCount, ModbusPriority.Low, ct);

        var config = new MeterSlotConfig[P.SlotCount];
        var result = new MeterData?[P.SlotCount];
        for (int slot = 0; slot < P.SlotCount; slot++)
        {
            int address = RegisterConverter.ToDInt(addrRegs[slot * 2], addrRegs[slot * 2 + 1]);
            config[slot] = new MeterSlotConfig { Type = (MeterType)types[slot], Address = address };
            if (address == 0) continue; // слот отключён — не опрашиваем

            int blockBase = P.DataBlocksBase + slot * P.DataBlockStride;
            int[] block = await transport.ReadHoldingRegistersAsync(
                blockBase, P.DataBlockStride, ModbusPriority.Low, ct);

            result[slot] = new MeterData
            {
                Voltages = RegisterConverter.ToFloats(block, P.VoltageOffset, 3),
                Currents = RegisterConverter.ToFloats(block, P.CurrentOffset, 3),
                Powers = RegisterConverter.ToFloats(block, P.PowerOffset, 4),
                Energy = RegisterConverter.ToFloat(block[P.EnergyOffset], block[P.EnergyOffset + 1]),
                Serial = RegisterConverter.ToDInt(block[P.SerialOffset], block[P.SerialOffset + 1]),
                CommOk = commStatus[slot]
            };
        }
        return new MetersSnapshot { Config = config, Data = result };
    }
}
