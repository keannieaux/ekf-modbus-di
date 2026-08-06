using ShnoSetting.Core.Modbus;
using ShnoSetting.Core.Profiles;

namespace ShnoSetting.Core.Schedule;

/// <summary>
/// Запись графика в ПЛК и чтение графика из ПЛК.
/// График не участвует в циклическом опросе — только по явной команде.
/// </summary>
public sealed class ScheduleService(IModbusTransport transport, ControllerProfile profile)
{
    /// <summary>Читает CSV-файл и записывает график в ПЛК (помесячно, только реальные дни).</summary>
    public async Task ImportFromCsvAsync(string csvPath, CancellationToken ct = default)
    {
        string text = await File.ReadAllTextAsync(csvPath, ct);
        var days = ScheduleCsv.Parse(text);
        await WriteToPlcAsync(days, ct);
    }

    public async Task WriteToPlcAsync(IReadOnlyList<ScheduleDay> days, CancellationToken ct = default)
    {
        var byDate = days.ToDictionary(d => (d.Month, d.Day));
        var schedule = profile.Schedule;

        for (int month = 1; month <= 12; month++)
        {
            ct.ThrowIfCancellationRequested();
            int dayCount = ScheduleCalendar.DaysInMonth(month);
            int[] registers = new int[dayCount * schedule.IntervalsPerDay];

            for (int day = 1; day <= dayCount; day++)
            {
                if (!byDate.TryGetValue((month, day), out var scheduleDay))
                    throw new InvalidOperationException($"В данных нет дня {day:D2}.{month:D2}");

                for (int slot = 0; slot < schedule.IntervalsPerDay; slot++)
                {
                    var interval = scheduleDay.Intervals[slot];
                    registers[(day - 1) * schedule.IntervalsPerDay + slot] =
                        ScheduleWord.Pack(interval.Time, ScheduleModeMap.ToMask(interval.Mode));
                }
            }

            // Хвосты месяца (до 31 дня) не трогаем — пишем только реальные дни.
            await transport.WriteMultipleRegistersAsync(
                ScheduleCalendar.MonthBase(schedule, month), registers, ct);
        }
    }

    public async Task<IReadOnlyList<ScheduleDay>> ReadFromPlcAsync(CancellationToken ct = default)
    {
        var schedule = profile.Schedule;
        var days = new List<ScheduleDay>(ScheduleCalendar.TotalDays);

        for (int month = 1; month <= 12; month++)
        {
            ct.ThrowIfCancellationRequested();
            int[] registers = await transport.ReadHoldingRegistersAsync(
                ScheduleCalendar.MonthBase(schedule, month),
                ScheduleCalendar.MonthRegisterCount(schedule, month),
                ModbusPriority.High, ct);

            int dayCount = ScheduleCalendar.DaysInMonth(month);
            for (int day = 1; day <= dayCount; day++)
            {
                var scheduleDay = new ScheduleDay(month, day);
                for (int slot = 0; slot < schedule.IntervalsPerDay; slot++)
                {
                    int word = registers[(day - 1) * schedule.IntervalsPerDay + slot];
                    ScheduleWord.Unpack(word, out int? time, out int mask);
                    scheduleDay.Intervals[slot].Time = time;
                    scheduleDay.Intervals[slot].Mode = ScheduleModeMap.ToMode(mask);
                }
                days.Add(scheduleDay);
            }
        }
        return days;
    }
}
