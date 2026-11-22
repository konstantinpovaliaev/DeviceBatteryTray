using DeviceBatteryTray.Native;

namespace DeviceBatteryTray.Services;

/// <summary>Time since the last keyboard or mouse input in this session (no input content is seen).</summary>
internal static class IdleTime
{
    public static TimeSpan SinceLastInput()
    {
        var info = new LastInputInfo { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<LastInputInfo>() };
        if (!Win32.GetLastInputInfo(ref info))
        {
            return TimeSpan.MaxValue; // Unknown: treat as idle, so no device request is sent.
        }

        uint elapsed = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(elapsed);
    }
}
