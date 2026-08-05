using System.Text.Json;

namespace ShnoSetting.Core.Profiles;

/// <summary>
/// Загружает JSON-профили контроллеров из каталога (по умолчанию "profiles" рядом с exe).
/// </summary>
public sealed class ProfileProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    private readonly string _directory;

    public ProfileProvider(string directory) => _directory = directory;

    public IReadOnlyList<ControllerProfile> LoadAll()
    {
        var profiles = new List<ControllerProfile>();
        if (!Directory.Exists(_directory)) return profiles;

        foreach (var file in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                var profile = JsonSerializer.Deserialize<ControllerProfile>(
                    File.ReadAllText(file), JsonOptions);
                if (profile is not null)
                    profiles.Add(profile);
            }
            catch (Exception ex)
            {
                // Битый профиль не должен ронять приложение.
                System.Diagnostics.Debug.WriteLine($"Профиль {file} пропущен: {ex.Message}");
            }
        }
        return profiles;
    }

    public void Save(ControllerProfile profile)
    {
        Directory.CreateDirectory(_directory);
        var fileName = string.Concat(profile.Name.Split(Path.GetInvalidFileNameChars()));
        var path = Path.Combine(_directory, fileName + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(profile, JsonOptions));
    }
}
