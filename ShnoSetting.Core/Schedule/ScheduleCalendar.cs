using ShnoSetting.Core.Profiles;

namespace ShnoSetting.Core.Schedule;

/// <summary>
/// Календарь графика: 366 дней (29 февраля — всегда), месяцы с шагом MonthStride,
/// записываются только реальные дни месяца, «хвосты» не трогаем.
/// </summary>
public static class ScheduleCalendar
{
    public const int TotalDays = 366;

    /// <summary>Реальное число дней месяца; февраль — всегда 29.</summary>
    public static int DaysInMonth(int month)
    {
        if (month is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(month));
        return month == 2 ? 29 : DateTime.DaysInMonth(2023, month); // 2023 — невисокосный
    }

    /// <summary>Адрес интервала: month 1..12, day 1..31, slot 0..7.</summary>
    public static int Address(ScheduleProfile profile, int month, int day, int slot)
    {
        if (day < 1 || day > DaysInMonth(month))
            throw new ArgumentOutOfRangeException(nameof(day));
        if (slot < 0 || slot >= profile.IntervalsPerDay)
            throw new ArgumentOutOfRangeException(nameof(slot));
        return profile.MonthsBase
            + profile.MonthStride * (month - 1)
            + (day - 1) * profile.IntervalsPerDay
            + slot;
    }

    /// <summary>Базовый адрес месяца (для блочной записи/чтения реальных дней).</summary>
    public static int MonthBase(ScheduleProfile profile, int month)
        => profile.MonthsBase + profile.MonthStride * (month - 1);

    /// <summary>Число регистров реальных дней месяца.</summary>
    public static int MonthRegisterCount(ScheduleProfile profile, int month)
        => DaysInMonth(month) * profile.IntervalsPerDay;

    /// <summary>Все 366 дней года по порядку.</summary>
    public static IEnumerable<(int Month, int Day)> AllDays()
    {
        for (int month = 1; month <= 12; month++)
            for (int day = 1; day <= DaysInMonth(month); day++)
                yield return (month, day);
    }
}
