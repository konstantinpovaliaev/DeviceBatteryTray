using System.Globalization;
using System.Resources;
using DeviceBatteryTray.Battery;

namespace DeviceBatteryTray.Ui;

/// <summary>
/// User-visible text from Strings.resx (English) and its translations (Strings.&lt;lang&gt;.resx).
/// The UI culture is chosen by <see cref="Languages"/>; missing translations fall back to English.
/// </summary>
internal static class Strings
{
    public const string AppName = "DeviceBatteryTray";
    public const string SourceWindows = "Windows";
    public const string SourceHid = "HID";

    internal static readonly ResourceManager Resources = new("DeviceBatteryTray.Ui.Strings", typeof(Strings).Assembly);

    /// <summary>Culture used for all UI text; changed when the user picks a language.</summary>
    public static CultureInfo Culture { get; set; } = CultureInfo.GetCultureInfo(Languages.Default);

    public static string RefreshNow => Get(nameof(RefreshNow));
    public static string Devices => Get(nameof(Devices));
    public static string NoDevices => Get(nameof(NoDevices));
    public static string AllHidden => Get(nameof(AllHidden));
    public static string StartWithWindows => Get(nameof(StartWithWindows));
    public static string LowBatteryNotifications => Get(nameof(LowBatteryNotifications));
    public static string Exit => Get(nameof(Exit));
    public static string LowBatteryTitle => Get(nameof(LowBatteryTitle));
    public static string NotConnected => Get(nameof(NotConnected));
    public static string NoData => Get(nameof(NoData));
    public static string ShowIcon => Get(nameof(ShowIcon));
    public static string IconKind => Get(nameof(IconKind));
    public static string KindAuto => Get(nameof(KindAuto));
    public static string Language => Get(nameof(Language));
    public static string LanguageAuto => Get(nameof(LanguageAuto));

    public static string LogFile(string path) => string.Format(Culture, Get(nameof(LogFile)), path);

    public static string KindName(DeviceKind kind) => Get("Kind" + kind);

    private static string Get(string name) => Resources.GetString(name, Culture) ?? name;
}
