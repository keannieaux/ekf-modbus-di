namespace ShnoSetting.Core.Schedule;

/// <summary>
/// Упаковка слова графика: биты 0–11 — время в формате ЧЧММ (12:00 → 1200),
/// биты 12–15 — маска пускателей (бит 12 = КМ1).
/// Пропуск — явно 0x0FFF (12 единиц времени + нулевая маска).
/// </summary>
public static class ScheduleWord
{
    public const ushort Empty = 0x0FFF;
    public const int TimeBits = 0x0FFF;

    public static ushort Pack(int? hhmm, int mask)
        => hhmm is null
            ? Empty
            : (ushort)((hhmm.Value & TimeBits) | ((mask & 0xF) << 12));

    public static void Unpack(int word, out int? hhmm, out int mask)
    {
        int time = word & TimeBits;
        hhmm = time == TimeBits ? null : time;
        mask = (word >> 12) & 0xF;
    }

    public static bool IsEmpty(int word) => (word & TimeBits) == TimeBits;

    /// <summary>"8:48" → 848. Возвращает null для "--:--"/пустого.</summary>
    public static int? ParseTime(string text)
    {
        text = text.Trim();
        if (text.Length == 0 || text == "--:--") return null;
        var parts = text.Split(':');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out int h)
            || !int.TryParse(parts[1], out int m)
            || h is < 0 or > 23 || m is < 0 or > 59)
            throw new FormatException($"Некорректное время: \"{text}\"");
        return h * 100 + m;
    }

    /// <summary>848 → "8:48", null → "--:--".</summary>
    public static string FormatTime(int? hhmm)
        => hhmm is null ? "--:--" : $"{hhmm.Value / 100}:{hhmm.Value % 100:D2}";
}
