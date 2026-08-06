namespace ShnoSetting.Core.Modbus;

/// <summary>
/// Преобразование многорегистровых значений (float, DINT).
/// ПЛК передаёт 2 регистра младшим словом вперёд (первый регистр = младшее слово),
/// значение собирается старшим словом вперёд (big endian).
/// </summary>
public static class RegisterConverter
{
    /// <param name="low">Первый регистр (младшее слово).</param>
    /// <param name="high">Второй регистр (старшее слово).</param>
    public static float ToFloat(int low, int high)
    {
        byte[] bytes =
        [
            (byte)(high >> 8), (byte)(high & 0xFF),
            (byte)(low >> 8), (byte)(low & 0xFF)
        ];
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    /// <param name="low">Первый регистр (младшее слово).</param>
    /// <param name="high">Второй регистр (старшее слово).</param>
    public static int ToDInt(int low, int high)
    {
        byte[] bytes =
        [
            (byte)(high >> 8), (byte)(high & 0xFF),
            (byte)(low >> 8), (byte)(low & 0xFF)
        ];
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    /// <summary>Возвращает (первый регистр = младшее слово, второй регистр = старшее слово).</summary>
    public static (int Low, int High) FromFloat(float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return ((bytes[2] << 8) | bytes[3], (bytes[0] << 8) | bytes[1]);
    }

    /// <summary>Возвращает (первый регистр = младшее слово, второй регистр = старшее слово).</summary>
    public static (int Low, int High) FromDInt(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return ((bytes[2] << 8) | bytes[3], (bytes[0] << 8) | bytes[1]);
    }

    /// <summary>Читает массив float из регистров, начиная со смещения (в регистрах).</summary>
    public static float[] ToFloats(int[] registers, int offset, int count)
    {
        var result = new float[count];
        for (int i = 0; i < count; i++)
            result[i] = ToFloat(registers[offset + i * 2], registers[offset + i * 2 + 1]);
        return result;
    }
}
