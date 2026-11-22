using System.Runtime.InteropServices;
using DeviceBatteryTray.Native;
using DeviceBatteryTray.Services;

namespace DeviceBatteryTray.Ui;

/// <summary>
/// One notification-area icon through Shell_NotifyIconW, identified by a stable GUID so Windows
/// remembers its visibility per device. Falls back to a plain id if the GUID cannot be registered.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    public const int CallbackMessage = 0x8000 + 1; // WM_APP + 1

    private readonly IntPtr _window;
    private readonly Guid _guid;
    private bool _useGuid = true;
    private bool _added;
    private IntPtr _icon;
    private string _tip = "";

    public TrayIcon(IntPtr window, uint id, Guid guid)
    {
        _window = window;
        Id = id;
        _guid = guid;
    }

    public uint Id { get; }

    /// <summary>Takes ownership of <paramref name="icon"/>.</summary>
    public void Update(IntPtr icon, string tip)
    {
        IntPtr previous = _icon;
        _icon = icon;
        _tip = Truncate(tip, 127);

        if (!_added || !Send(Win32.NIM_MODIFY, Win32.NIF_ICON | Win32.NIF_TIP | Win32.NIF_SHOWTIP))
        {
            Add();
        }

        if (previous != IntPtr.Zero)
        {
            Win32.DestroyIcon(previous);
        }
    }

    /// <summary>Re-creates the icon after Explorer restarted (TaskbarCreated).</summary>
    public void Recreate()
    {
        _added = false;
        Add();
    }

    public void ShowBalloon(string title, string text)
    {
        if (!_added)
        {
            return;
        }

        NotifyIconData data = Data(Win32.NIF_INFO);
        data.szInfoTitle = Truncate(title, 63);
        data.szInfo = Truncate(text, 255);
        data.dwInfoFlags = Win32.NIIF_WARNING | Win32.NIIF_RESPECT_QUIET_TIME;
        Win32.Shell_NotifyIconW(Win32.NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_added)
        {
            Send(Win32.NIM_DELETE, 0);
            _added = false;
        }

        if (_icon != IntPtr.Zero)
        {
            Win32.DestroyIcon(_icon);
            _icon = IntPtr.Zero;
        }
    }

    private void Add()
    {
        const uint flags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP | Win32.NIF_SHOWTIP;
        if (_useGuid)
        {
            Send(Win32.NIM_DELETE, 0); // clears a leftover icon with the same GUID (e.g. after a crash)
            if (Send(Win32.NIM_ADD, flags))
            {
                _added = true;
                SetVersion();
                return;
            }

            Log.Write($"tray icon GUID registration failed for id {Id}; using plain id");
            _useGuid = false;
        }

        _added = Send(Win32.NIM_ADD, flags);
        if (_added)
        {
            SetVersion();
        }
    }

    private void SetVersion()
    {
        NotifyIconData data = Data(0);
        data.uVersion = Win32.NOTIFYICON_VERSION_4;
        Win32.Shell_NotifyIconW(Win32.NIM_SETVERSION, ref data);
    }

    private bool Send(uint message, uint flags)
    {
        NotifyIconData data = Data(flags);
        return Win32.Shell_NotifyIconW(message, ref data);
    }

    private NotifyIconData Data(uint flags) => new()
    {
        cbSize = Marshal.SizeOf<NotifyIconData>(),
        hWnd = _window,
        uID = Id,
        uFlags = flags | (_useGuid ? Win32.NIF_GUID : 0),
        uCallbackMessage = CallbackMessage,
        hIcon = _icon,
        szTip = _tip,
        szInfo = "",
        szInfoTitle = "",
        guidItem = _useGuid ? _guid : Guid.Empty,
    };

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
