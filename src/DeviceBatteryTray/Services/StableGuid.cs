using System.Security.Cryptography;
using System.Text;

namespace DeviceBatteryTray.Services;

/// <summary>
/// Deterministic GUID per (executable path, device) for tray icons, so Windows keeps each device's
/// "always show" choice across restarts. Windows binds an icon GUID to the exe path, so the path is
/// part of the input: a moved exe gets new GUIDs instead of a registration conflict.
/// </summary>
internal static class StableGuid
{
    public static Guid For(string executablePath, string deviceKey)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            "DeviceBatteryTray|" + executablePath.ToUpperInvariant() + "|" + deviceKey));
        byte[] bytes = hash.AsSpan(0, 16).ToArray();
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50); // version 5 style (name based)
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(bytes);
    }
}
