using System.Globalization;

namespace DeviceBatteryTray.Ui;

/// <summary>Supported UI languages and how the active one is chosen.</summary>
internal static class Languages
{
    public const string Default = "en";

    /// <summary>Language codes with their names written in that language (for the menu).</summary>
    public static readonly IReadOnlyList<(string Code, string NativeName)> Supported =
    [
        ("en", "English"),
        ("fr", "Français"),
        ("es", "Español"),
        ("de", "Deutsch"),
        ("ru", "Русский"),
        ("uk", "Українська"),
        ("be", "Беларуская"),
    ];

    public static bool IsSupported(string? code) => code is not null && Supported.Any(l => l.Code == code);

    /// <summary>
    /// The user's choice if it is supported; otherwise the Windows display language if supported;
    /// otherwise English.
    /// </summary>
    public static string Resolve(string? chosen, CultureInfo system)
    {
        if (IsSupported(chosen))
        {
            return chosen!;
        }

        string systemCode = system.TwoLetterISOLanguageName;
        return IsSupported(systemCode) ? systemCode : Default;
    }
}
