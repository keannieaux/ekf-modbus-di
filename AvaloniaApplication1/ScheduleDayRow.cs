using System.Linq;
using ShnoSetting.Core.Schedule;

namespace AvaloniaApplication1;

/// <summary>
/// Строка таблицы предпросмотра графика: дата + 16 текстовых ячеек
/// (Время 1, Режим 1, …, Время 8, Режим 8). Только для отображения.
/// </summary>
public sealed class ScheduleDayRow
{
    public required string Date { get; init; }

    /// <summary>16 ячеек: время и режим по каждому из 8 интервалов.</summary>
    public required string[] Cells { get; init; }

    public static ScheduleDayRow FromDay(ScheduleDay day) => new()
    {
        Date = $"{day.Day:D2}.{day.Month:D2}",
        Cells = day.Intervals
            .SelectMany(i => new[] { ScheduleWord.FormatTime(i.Time), i.Mode.ToString() })
            .ToArray()
    };
}
