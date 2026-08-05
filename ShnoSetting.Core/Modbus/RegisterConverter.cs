namespace ShnoSetting.Core.Modbus;

/// <summary>
/// Преобразование многорегистровых значений (float, DINT).
/// Формат по ТЗ: 2 регистра, big endian — первый регистр содержит старшее слово.
/// </summary>
public static class RegisterConverter
{
    public static float ToFloat(int high, int low)
    {
        byte[] bytes =
        [
            (byte)(high >> 8), (byte)(high & 0xFF),
            (byte)(low >> 8), (byte)(low & 0xFF)
        ];
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToSingle(bytes, 0);
    }

    public static int ToDInt(int high, int low)
    {
        byte[] bytes =
        [
            (byte)(high >> 8), (byte)(high & 0xFF),
            (byte)(low >> 8), (byte)(low & 0xFF)
        ];
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return BitConverter.ToInt32(bytes, 0);
    }

    public static (int High, int Low) FromFloat(float value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return ((bytes[0] << 8) | bytes[1], (bytes[2] << 8) | bytes[3]);
    }

    public static (int High, int Low) FromDInt(int value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        return ((bytes[0] << 8) | bytes[1], (bytes[2] << 8) | bytes[3]);
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
