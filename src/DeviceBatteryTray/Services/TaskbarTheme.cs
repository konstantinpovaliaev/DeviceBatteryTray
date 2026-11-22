using Microsoft.Win32;

namespace DeviceBatteryTray.Services;

/// <summary>Whether the taskbar uses the light theme (so pictograms must be dark to stay visible).</summary>
internal static class TaskbarTheme
{
    public static bool IsLight()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is int value && value != 0;
    }
}
