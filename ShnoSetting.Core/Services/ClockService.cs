using ShnoSetting.Core.Modbus;
using ShnoSetting.Core.Profiles;

namespace ShnoSetting.Core.Services;

/// <summary>Время контроллера: чтение + синхронизация с ПК, по ТЗ п. 4.4.</summary>
public sealed class ClockService(IModbusTransport transport, ControllerProfile profile)
{
    private ClockProfile P => profile.Clock;

    /// <summary>Чтение времени ПЛК (циклический опрос). null — некорректные данные.</summary>
    public async Task<DateTime?> ReadTimeAsync(CancellationToken ct = default)
    {
        int[] r = await transport.ReadHoldingRegistersAsync(P.ReadRegsBase, 6, ModbusPriority.Low, ct);
        try
        {
            return new DateTime(r[0], r[1], r[2], r[3], r[4], r[5]);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Запись времени ПК в регистры ПЛК + импульс на триггер «применить».</summary>
    public async Task SyncToPcAsync(CancellationToken ct = default)
    {
        var now = DateTime.Now;
        int[] values = [now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second];

        await transport.WriteMultipleRegistersAsync(P.WriteRegsBase, values, ct);
        await transport.WriteSingleCoilAsync(P.SyncTriggerCoil, true, ct);
        await Task.Delay(300, ct);
        await transport.WriteSingleCoilAsync(P.SyncTriggerCoil, false, ct);
    }
}
