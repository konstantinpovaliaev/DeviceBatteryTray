using System.Globalization;
using DeviceBatteryTray.Battery;
using DeviceBatteryTray.Native;
using DeviceBatteryTray.Services;

namespace DeviceBatteryTray.Ui;

/// <summary>Keeps one tray icon per visible device, plus a placeholder when none is visible.</summary>
internal sealed class TrayIconManager : IDisposable
{
    private const uint PlaceholderId = 0xFFFF;

    private readonly IntPtr _window;
    private readonly string _executable;
    private readonly Dictionary<string, TrayIcon> _icons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _shown = new(StringComparer.Ordinal); // key -> rendered signature
    private TrayIcon? _placeholder;
    private string? _placeholderTip;
    private uint _nextId = 1;

    public TrayIconManager(IntPtr window, string executable)
    {
        _window = window;
        _executable = executable;
    }

    public void Sync(IReadOnlyList<DeviceView> devices, AppSettings settings)
    {
        List<DeviceView> visible = devices.Where(d => !settings.HiddenDevices.Contains(d.Key)).ToList();
        var visibleKeys = visible.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);

        foreach (string key in _icons.Keys.Where(k => !visibleKeys.Contains(k)).ToList())
        {
            _icons[key].Dispose();
            _icons.Remove(key);
            _shown.Remove(key);
        }

        int size = SystemInformation.SmallIconSize.Width;
        bool light = TaskbarTheme.IsLight();
        foreach (DeviceView device in visible)
        {
            BatteryLevel level = device.Level(settings.LowPercent, settings.CriticalPercent);
            DeviceKind kind = KindFor(device, settings);
            string tip = Tooltip(device);
            string signature = $"{level}|{device.Connected}|{size}|{tip}|{kind}|{light}";
            if (_shown.TryGetValue(device.Key, out string? previous) && previous == signature)
            {
                continue;
            }

            if (!_icons.TryGetValue(device.Key, out TrayIcon? icon))
            {
                icon = new TrayIcon(_window, _nextId++, StableGuid.For(_executable, device.Key));
                _icons[device.Key] = icon;
            }

            icon.Update(IconRenderer.Render(kind, level, device.Connected, light, size), tip);
            _shown[device.Key] = signature;
        }

        if (visible.Count == 0)
        {
            string tip = Strings.AppName + "\n" + (devices.Count == 0 ? Strings.NoDevices : Strings.AllHidden);
            if (_placeholder is null || _placeholderTip != tip)
            {
                _placeholder ??= new TrayIcon(_window, PlaceholderId, StableGuid.For(_executable, "placeholder"));
                _placeholder.Update(IconRenderer.Render(DeviceKind.Other, BatteryLevel.Unknown, false, light, size), tip);
                _placeholderTip = tip;
            }
        }
        else if (_placeholder is not null)
        {
            _placeholder.Dispose();
            _placeholder = null;
            _placeholderTip = null;
        }
    }

    public void RecreateAll()
    {
        foreach (TrayIcon icon in _icons.Values)
        {
            icon.Recreate();
        }

        _placeholder?.Recreate();
    }

    public void ShowBalloon(string deviceKey, string title, string text)
    {
        TrayIcon? icon = _icons.GetValueOrDefault(deviceKey) ?? _icons.Values.FirstOrDefault() ?? _placeholder;
        icon?.ShowBalloon(title, text);
    }

    public void Dispose()
    {
        foreach (TrayIcon icon in _icons.Values)
        {
            icon.Dispose();
        }

        _icons.Clear();
        _placeholder?.Dispose();
        _placeholder = null;
    }

    /// <summary>The user's choice for this device if any, otherwise what Windows reports.</summary>
    internal static DeviceKind KindFor(DeviceView device, AppSettings settings) =>
        settings.DeviceKinds.TryGetValue(device.Key, out DeviceKind chosen) ? chosen : device.Kind;

    /// <summary>Forces every icon to be redrawn on the next Sync (theme or DPI change).</summary>
    public void Invalidate()
    {
        _shown.Clear();
        _placeholderTip = null;
    }

    internal static string Tooltip(DeviceView device)
    {
        string level = device.Percent is int p ? p.ToString(CultureInfo.InvariantCulture) + "%" : Strings.NoData;
        string line = $"{device.Name}: {level}";
        if (device.Source is BatterySource source && device.ReadAt is DateTimeOffset at)
        {
            line += $"\n{(source == BatterySource.Hid ? Strings.SourceHid : Strings.SourceWindows)}, {at:HH:mm}";
        }

        if (!device.Connected)
        {
            line += "\n" + Strings.NotConnected;
        }

        return line;
    }
}
