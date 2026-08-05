using ShnoSetting.Core.Modbus;
using ShnoSetting.Core.Profiles;

namespace ShnoSetting.Core.Services;

/// <summary>Пускатели: 4 шт., общий регистр маски + общий регистр длительности, по ТЗ п. 4.2.</summary>
public sealed class StartersService(IModbusTransport transport, ControllerProfile profile)
{
    private StartersProfile P => profile.Starters;

    /// <summary>Обратная связь пускателей (циклический опрос).</summary>
    public Task<bool[]> ReadFeedbackAsync(CancellationToken ct = default)
        => transport.ReadCoilsAsync(P.FeedbackCoilsBase, P.Count, ModbusPriority.Low, ct);

    /// <summary>Запись битовой маски включения (бит 0 = КМ1 … бит 3 = КМ4).</summary>
    public Task WriteMaskAsync(int mask, CancellationToken ct = default)
        => transport.WriteSingleRegisterAsync(P.ControlReg, mask & 0xF, ct);

    /// <summary>Запись длительности ручного режима, секунды (0 = выключить ручной режим).</summary>
    public Task WriteDurationAsync(int seconds, CancellationToken ct = default)
        => transport.WriteSingleRegisterAsync(P.DurationReg, seconds, ct);
}
