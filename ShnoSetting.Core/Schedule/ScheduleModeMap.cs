namespace ShnoSetting.Core.Schedule;

/// <summary>
/// Маппинг «Режим» (из CSV) ↔ битовая маска пускателей (бит 0 = КМ1 … бит 3 = КМ4).
/// Таблица по ТЗ; режимы 0 и 5 — «все выключены», канонический режим для маски 0000 — 5.
/// </summary>
public static class ScheduleModeMap
{
    private static readonly Dictionary<int, int> ModeToMask = new()
    {
        [0] = 0b0000, [1] = 0b1000, [2] = 0b0100, [3] = 0b0010, [4] = 0b0001,
        [5] = 0b0000, [6] = 0b1100, [7] = 0b1010, [8] = 0b1001, [9] = 0b0110,
        [10] = 0b0101, [11] = 0b0011, [12] = 0b1110, [13] = 0b1101, [14] = 0b0111,
        [15] = 0b1111, [16] = 0b1011
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
