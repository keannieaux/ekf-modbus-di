using System.Globalization;
using System.Text;

namespace ShnoSetting.Core.Schedule;

/// <summary>
/// Парсер/писатель CSV графика. Формат: разделитель ';', строка = день,
/// колонки: Дата; Время 1; Режим 1; …; Время 8; Режим 8.
/// Пропуск времени — "--:--". Дата без года (dd.MM); если год в файле есть — игнорируется.
/// Файл должен содержать все 366 дней по порядку.
/// </summary>
public static class ScheduleCsv
{
    private const char Separator = ';';
    private const int ColumnsPerDay = 1 + ScheduleDay.IntervalCount * 2;

    public static IReadOnlyList<ScheduleDay> Parse(string text)
    {
        var days = new List<ScheduleDay>(ScheduleCalendar.TotalDays);
        var expected = ScheduleCalendar.AllDays().GetEnumerator();
        expected.MoveNext();

        string[] lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex].Trim('\r', ' ', ';');
            if (line.Length == 0) continue;

            // Пропускаем строку заголовка (не начинается с даты).
            if (!char.IsDigit(line[0])) continue;

            var columns = line.Split(Separator);
            if (columns.Length < ColumnsPerDay)
                throw new FormatException(
                    $"Строка {lineIndex + 1}: ожидалось {ColumnsPerDay} колонок, найдено {columns.Length}");

            var (month, day) = expected.Current;
            ValidateDate(columns[0], month, day, lineIndex + 1);

            var scheduleDay = new ScheduleDay(month, day);
            for (int slot = 0; slot < ScheduleDay.IntervalCount; slot++)
            {
                string timeText = columns[1 + slot * 2];
                string modeText = columns[2 + slot * 2];

                int? time;
                try { time = ScheduleWord.ParseTime(timeText); }
                catch (FormatException ex)
                { throw new FormatException($"Строка {lineIndex + 1}, интервал {slot + 1}: {ex.Message}"); }

                if (!int.TryParse(modeText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int mode)
                    || !ScheduleModeMap.IsValidMode(mode))
                    throw new FormatException(
                        $"Строка {lineIndex + 1}, интервал {slot + 1}: некорректный режим \"{modeText.Trim()}\"");

                scheduleDay.Intervals[slot].Time = time;
                scheduleDay.Intervals[slot].Mode = mode;
            }

            days.Add(scheduleDay);
            if (!expected.MoveNext() && lineIndex < lines.Length - 1)
                throw new FormatException($"Строка {lineIndex + 1}: в файле больше {ScheduleCalendar.TotalDays} дней");
        }

        if (days.Count != ScheduleCalendar.TotalDays)
            throw new FormatException(
                $"Файл должен содержать все {ScheduleCalendar.TotalDays} дней, найдено {days.Count}");

        return days;
    }

    private static void ValidateDate(string text, int month, int day, int lineNumber)
    {
        // Формат dd.MM или dd.MM.yyyy; год игнорируем по ТЗ.
        var parts = text.Trim().Split('.');
        if (parts.Length is not (2 or 3)
            || !int.TryParse(parts[0], out int d)
            || !int.TryParse(parts[1], out int m)
            || d != day || m != month)
            throw new FormatException(
                $"Строка {lineNumber}: ожидалась дата {day:D2}.{month:D2}, найдено \"{text.Trim()}\"");
    }

    public static string Write(IReadOnlyList<ScheduleDay> days)
    {
        var sb = new StringBuilder();
        sb.Append("Дата");
        for (int slot = 1; slot <= ScheduleDay.IntervalCount; slot++)
            sb.Append(Separator).Append("Время ").Append(slot)
              .Append(Separator).Append("Режим ").Append(slot);
        sb.AppendLine();

        foreach (var day in days)
        {
            // Дата без года.
            sb.Append(day.Day.ToString("D2")).Append('.')
              .Append(day.Month.ToString("D2"));

            foreach (var interval in day.Intervals)
            {
                sb.Append(Separator).Append(ScheduleWord.FormatTime(interval.Time));
                sb.Append(Separator).Append(interval.Mode);
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
