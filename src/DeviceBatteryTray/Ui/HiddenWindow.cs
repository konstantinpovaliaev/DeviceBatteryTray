using DeviceBatteryTray.Native;

namespace DeviceBatteryTray.Ui;

/// <summary>
/// Invisible top-level window: receives tray icon callbacks, device-change broadcasts and the
/// TaskbarCreated message (message-only windows do not get broadcasts, so this one is top-level).
/// </summary>
internal sealed class HiddenWindow : NativeWindow, IDisposable
{
    private const int WM_DEVICECHANGE = 0x0219;
    private const int WM_SETTINGCHANGE = 0x001A;
    private const int WM_DISPLAYCHANGE = 0x007E;
    private const int WS_EX_TOOLWINDOW = 0x80;

    private readonly int _taskbarCreated = (int)Win32.RegisterWindowMessageW("TaskbarCreated");

    public HiddenWindow()
    {
        CreateHandle(new CreateParams { Caption = Strings.AppName, ExStyle = WS_EX_TOOLWINDOW });
    }

    public event Action? DevicesChanged;

    public event Action? TaskbarCreated;

    public event Action? AppearanceChanged;

    /// <summary>Icon id and notification event (low word of lParam with NOTIFYICON_VERSION_4).</summary>
    public event Action<uint, int>? TrayEvent;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == TrayIcon.CallbackMessage)
        {
            long lParam = m.LParam.ToInt64();
            TrayEvent?.Invoke((uint)((lParam >> 16) & 0xFFFF), (int)(lParam & 0xFFFF));
            return;
        }

        if (m.Msg == WM_DEVICECHANGE)
        {
            DevicesChanged?.Invoke();
        }
        else if (m.Msg is WM_SETTINGCHANGE or WM_DISPLAYCHANGE)
        {
            AppearanceChanged?.Invoke(); // light/dark theme or scaling may have changed
        }
        else if (_taskbarCreated != 0 && m.Msg == _taskbarCreated)
        {
            TaskbarCreated?.Invoke();
        }

        base.WndProc(ref m);
    }

    public void Dispose() => DestroyHandle();
}
