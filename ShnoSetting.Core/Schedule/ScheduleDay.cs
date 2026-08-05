namespace ShnoSetting.Core.Schedule;

/// <summary>Один интервал смены режима.</summary>
public sealed class ScheduleInterval
{
    /// <summary>Время в формате ЧЧММ (848 = 8:48); null = пропуск ("--:--").</summary>
    public int? Time { get; set; }

    /// <summary>Режим 0..16 по таблице ТЗ.</summary>
    public int Mode { get; set; } = 5;
}

/// <summary>График одного дня (8 интервалов).</summary>
public sealed class ScheduleDay
{
    public const int IntervalCount = 8;

    public int Month { get; }
    public int Day { get; }
    public ScheduleInterval[] Intervals { get; } = new ScheduleInterval[IntervalCount];

    public ScheduleDay(int month, int day)
    {
        Month = month;
        Day = day;
        for (int i = 0; i < IntervalCount; i++)
            Intervals[i] = new ScheduleInterval();
    }
}
