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
    /// <summary>Адрес счётчика; 0 = слот отключён.</summary>
    public int Address { get; set; }
    public bool IsActive => Address != 0;
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
        int[] addresses = await transport.ReadHoldingRegistersAsync(
            P.AddressRegsBase, P.SlotCount, ModbusPriority.High, ct);

        var config = new MeterSlotConfig[P.SlotCount];
        for (int i = 0; i < config.Length; i++)
            config[i] = new MeterSlotConfig { Type = (MeterType)types[i], Address = addresses[i] };
        return config;
    }

    public async Task WriteConfigAsync(int slot, MeterSlotConfig config, CancellationToken ct = default)
    {
        await transport.WriteSingleRegisterAsync(P.TypeRegsBase + slot, (int)config.Type, ct);
        await transport.WriteSingleRegisterAsync(P.AddressRegsBase + slot, config.Address, ct);
    }

    /// <summary>
    /// Чтение данных счётчиков (циклический опрос).
    /// Возвращает массив по слотам; null — слот отключён (адрес = 0).
    /// </summary>
    public async Task<MeterData?[]> ReadDataAsync(CancellationToken ct = default)
    {
        // Конфиг в опросе читаем низким приоритетом — адреса редко меняются.
        int[] types = await transport.ReadHoldingRegistersAsync(
            P.TypeRegsBase, P.SlotCount, ModbusPriority.Low, ct);
        int[] addresses = await transport.ReadHoldingRegistersAsync(
            P.AddressRegsBase, P.SlotCount, ModbusPriority.Low, ct);
        bool[] commStatus = await transport.ReadCoilsAsync(
            P.CommStatusCoilsBase, P.SlotCount, ModbusPriority.Low, ct);

        var result = new MeterData?[P.SlotCount];
        for (int slot = 0; slot < P.SlotCount; slot++)
        {
            if (addresses[slot] == 0) continue; // слот отключён — не опрашиваем

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
        return result;
    }
}
