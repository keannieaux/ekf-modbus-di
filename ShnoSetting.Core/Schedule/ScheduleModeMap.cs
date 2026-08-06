namespace ShnoSetting.Core.Schedule;

/// <summary>
/// Маппинг «Режим» (из CSV) ↔ битовая маска пускателей (бит 0 = КМ1 … бит 3 = КМ4,
/// в слове графика: бит 12 = КМ1 … бит 15 = КМ4).
/// Таблица по ТЗ: маска записана в порядке КМ1 КМ2 КМ3 КМ4 (КМ1 — младший бит),
/// т.е. режим 1 = КМ1, режим 6 = КМ1+КМ2 и т.д.
/// Режимы 0 и 5 — «все выключены», канонический режим для маски 0000 — 5.
/// </summary>
public static class ScheduleModeMap
{
    private static readonly Dictionary<int, int> ModeToMask = new()
    {
        [0] = 0b0000, [1] = 0b0001, [2] = 0b0010, [3] = 0b0100, [4] = 0b1000,
        [5] = 0b0000, [6] = 0b0011, [7] = 0b0101, [8] = 0b1001, [9] = 0b0110,
        [10] = 0b1010, [11] = 0b1100, [12] = 0b0111, [13] = 0b1011, [14] = 0b1110,
        [15] = 0b1111, [16] = 0b1101
    };

    private static readonly Dictionary<int, int> MaskToMode = BuildReverse();

    private static Dictionary<int, int> BuildReverse()
    {
        var reverse = new Dictionary<int, int>();
        foreach (var (mode, mask) in ModeToMask)
        {
            // Для маски 0000 есть два режима (0 и 5) — оставляем 5 как канонический.
            if (!reverse.TryGetValue(mask, out var existing) || mode > existing)
                reverse[mask] = mode;
        }
        return reverse;
    }

    public static bool IsValidMode(int mode) => ModeToMask.ContainsKey(mode);

    public static int ToMask(int mode)
        => ModeToMask.TryGetValue(mode, out var mask)
            ? mask
            : throw new ArgumentOutOfRangeException(nameof(mode), mode, "Допустимы режимы 0..16");

    public static int ToMode(int mask) => MaskToMode[mask & 0xF];
}
