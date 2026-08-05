using System.Text.Json;

namespace ShnoSetting.Core.Settings;

/// <summary>Локальные настройки приложения (не хранятся в ПЛК).</summary>
public sealed class AppSettings
{
    public const int InputCount = 64;

    public string Ip { get; set; } = "192.168.1.245";
    public int Port { get; set; } = 502;
    public int PollPeriodMs { get; set; } = 1000;
    public string? ProfileName { get; set; }

    /// <summary>Наименования 64 дискретных входов.</summary>
    public string[] InputNames { get; set; } = CreateDefaultNames();

    private static string[] CreateDefaultNames()
    {
        var names = new string[InputCount];
        for (int i = 0; i < names.Length; i++)
            names[i] = $"Вход {i}";
        return names;
    }
}

/// <summary>Загрузка/сохранение настроек в JSON-файл.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public SettingsStore(string path) => _path = path;

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions);
                if (settings is not null)
                {
                    // Защита от обрезанного массива имён в старых файлах.
                    if (settings.InputNames.Length != AppSettings.InputCount)
                    {
                        var names = settings.InputNames;
                        Array.Resize(ref names, AppSettings.InputCount);
                        settings.InputNames = names;
                    }
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Настройки не загружены: {ex.Message}");
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Настройки не сохранены: {ex.Message}");
        }
    }
}
