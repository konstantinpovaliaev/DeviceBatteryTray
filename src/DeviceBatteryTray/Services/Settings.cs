using System.Text.Json;
using System.Text.Json.Serialization;
using DeviceBatteryTray.Battery;

namespace DeviceBatteryTray.Services;

internal sealed class AppSettings
{
    /// <summary>UI language code chosen in the menu; null follows the Windows display language.</summary>
    public string? Language { get; set; }

    /// <summary>Per-device pictogram chosen by the user, overriding automatic detection.</summary>
    public Dictionary<string, DeviceKind> DeviceKinds { get; set; } = new(StringComparer.Ordinal);

    public int PollMinutes { get; set; } = 30;
    public int LowPercent { get; set; } = 20;
    public int CriticalPercent { get; set; } = 10;
    public bool LowBatteryNotifications { get; set; } = true;
    public List<string> HiddenDevices { get; set; } = [];

    /// <summary>Clamps values from a hand-edited or damaged file into safe ranges.</summary>
    public AppSettings Normalize()
    {
        PollMinutes = Math.Clamp(PollMinutes, 5, 240);
        LowPercent = Math.Clamp(LowPercent, 1, 99);
        CriticalPercent = Math.Clamp(CriticalPercent, 0, LowPercent);
        HiddenDevices = (HiddenDevices ?? []).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.Ordinal).ToList();
        if (!Ui.Languages.IsSupported(Language))
        {
            Language = null;
        }

        DeviceKinds = new Dictionary<string, DeviceKind>(
            (DeviceKinds ?? []).Where(p => !string.IsNullOrWhiteSpace(p.Key) && Enum.IsDefined(p.Value)), StringComparer.Ordinal);
        return this;
    }
}

/// <summary>Settings as JSON in %APPDATA%\DeviceBatteryTray\settings.json.</summary>
internal sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static SettingsStore Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeviceBatteryTray", "settings.json"));

    public string FilePath { get; } = path;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new AppSettings();
            }

            AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options);
            return (loaded ?? new AppSettings()).Normalize();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            Log.Write($"settings unreadable, using defaults: {e.Message}");
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings.Normalize(), Options));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Write($"settings not saved: {e.Message}");
        }
    }
}
