using System.Globalization;
using DeviceBatteryTray.Battery;
using DeviceBatteryTray.Native;
using DeviceBatteryTray.Services;

namespace DeviceBatteryTray.Ui;

/// <summary>
/// The application: no main window, only tray icons and their context menu. A one-minute tick
/// re-reads cached Windows state; actual device requests are rare and gated by PollPolicy.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private const int WM_CONTEXTMENU = 0x007B;
    private const int NIN_SELECT = 0x0400;
    private const int NIN_KEYSELECT = 0x0401;

    private readonly SettingsStore _store = SettingsStore.Default;
    private readonly AppSettings _settings;
    private readonly HiddenWindow _window = new();
    private readonly TrayIconManager _tray;
    private readonly BatteryPoller _poller = new();
    private readonly LowBatteryNotifier _notifier = new();
    private readonly System.Windows.Forms.Timer _tick = new() { Interval = 60_000 };
    private readonly System.Windows.Forms.Timer _deviceChangeDelay = new() { Interval = 5_000 };
    private readonly ContextMenuStrip _menu = new();
    private IReadOnlyList<DeviceView> _devices = [];
    private bool _polling;
    private bool _hasPolled;
    private bool _pendingManual;

    public TrayApplicationContext()
    {
        _settings = _store.Load();
        ApplyLanguage();
        _tray = new TrayIconManager(_window.Handle, Environment.ProcessPath ?? Strings.AppName);

        _window.TrayEvent += OnTrayEvent;
        _window.TaskbarCreated += () => _tray.RecreateAll();
        _window.DevicesChanged += () =>
        {
            // Wait for the burst of change messages to settle; a device that just woke up needs a moment.
            _poller.MarkDevicesChanged();
            _deviceChangeDelay.Stop();
            _deviceChangeDelay.Start();
        };
        _deviceChangeDelay.Tick += (_, _) =>
        {
            _deviceChangeDelay.Stop();
            _ = PollAsync(manual: false);
        };
        _tick.Tick += (_, _) => _ = PollAsync(manual: false);
        _window.AppearanceChanged += () =>
        {
            _tray.Invalidate();
            if (_hasPolled)
            {
                _tray.Sync(_devices, _settings);
            }
        };

        // No icon before the first scan: a short-lived placeholder would get its own entry in
        // Windows' "Other system tray icons" list and confuse the per-device visibility settings.
        _tick.Start();
        Log.Write($"started {AppVersion.Display}, language {Strings.Culture.Name}, poll interval {_settings.PollMinutes} min");
        _ = PollAsync(manual: false);
    }

    protected override void ExitThreadCore()
    {
        _tick.Stop();
        _deviceChangeDelay.Stop();
        _tray.Dispose();
        _menu.Dispose();
        _window.Dispose();
        Log.Write("stopped");
        base.ExitThreadCore();
    }

    private async Task PollAsync(bool manual)
    {
        if (_polling)
        {
            _pendingManual |= manual;
            return;
        }

        _polling = true;
        try
        {
            TimeSpan interval = TimeSpan.FromMinutes(_settings.PollMinutes);
            _devices = await Task.Run(() => _poller.Poll(manual, interval));
            _hasPolled = true;
            _tray.Sync(_devices, _settings);

            if (_settings.LowBatteryNotifications)
            {
                foreach (DeviceView device in _notifier.Check(_devices, _settings.LowPercent))
                {
                    _tray.ShowBalloon(device.Key, Strings.LowBatteryTitle, $"{device.Name}: {device.Percent}%");
                }
            }
        }
        catch (Exception e)
        {
            Log.Write("poll failed: " + e);
        }
        finally
        {
            _polling = false;
        }

        if (_pendingManual)
        {
            _pendingManual = false;
            await PollAsync(manual: true);
        }
    }

    private void OnTrayEvent(uint iconId, int notification)
    {
        // With NOTIFYICON_VERSION_4 a click also produces WM_xBUTTONUP; react only to the semantic events.
        if (notification is WM_CONTEXTMENU or NIN_SELECT or NIN_KEYSELECT)
        {
            ShowMenu();
        }
    }

    private void ShowMenu()
    {
        foreach (ToolStripItem old in _menu.Items.Cast<ToolStripItem>().ToList())
        {
            old.Dispose();
        }

        _menu.Items.Clear();
        _menu.Items.Add(new ToolStripMenuItem($"{Strings.AppName} {AppVersion.Display}") { Enabled = false });
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Strings.RefreshNow, null, (_, _) => _ = PollAsync(manual: true));

        var devices = new ToolStripMenuItem(Strings.Devices);
        if (_devices.Count == 0)
        {
            devices.DropDownItems.Add(new ToolStripMenuItem(Strings.NoDevices) { Enabled = false });
        }

        foreach (DeviceView device in _devices)
        {
            devices.DropDownItems.Add(DeviceMenu(device));
        }

        _menu.Items.Add(devices);
        _menu.Items.Add(new ToolStripSeparator());

        var autostart = new ToolStripMenuItem(Strings.StartWithWindows) { Checked = Autostart.IsEnabled() };
        autostart.Click += (_, _) => Autostart.Set(!autostart.Checked);
        _menu.Items.Add(autostart);

        var notifications = new ToolStripMenuItem(Strings.LowBatteryNotifications) { Checked = _settings.LowBatteryNotifications };
        notifications.Click += (_, _) =>
        {
            _settings.LowBatteryNotifications = !_settings.LowBatteryNotifications;
            _store.Save(_settings);
        };
        _menu.Items.Add(notifications);
        _menu.Items.Add(LanguageMenu());

        _menu.Items.Add(new ToolStripMenuItem(Strings.LogFile(Log.FilePath)) { Enabled = false });
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Strings.Exit, null, (_, _) => ExitThread());

        Win32.SetForegroundWindow(_window.Handle); // lets the menu close when clicking elsewhere
        _menu.Show(Cursor.Position);
    }

    private ToolStripMenuItem DeviceMenu(DeviceView device)
    {
        string level = device.Percent is int p ? p + "%" : Strings.NoData;
        string key = device.Key;
        var menu = new ToolStripMenuItem($"{device.Name} — {level}");

        var show = new ToolStripMenuItem(Strings.ShowIcon) { Checked = !_settings.HiddenDevices.Contains(key) };
        show.Click += (_, _) => ToggleHidden(key);
        menu.DropDownItems.Add(show);

        var kindMenu = new ToolStripMenuItem(Strings.IconKind);
        bool overridden = _settings.DeviceKinds.TryGetValue(key, out DeviceKind chosen);
        var auto = new ToolStripMenuItem($"{Strings.KindAuto} ({Strings.KindName(device.Kind)})") { Checked = !overridden };
        auto.Click += (_, _) => SetKind(key, null);
        kindMenu.DropDownItems.Add(auto);
        foreach (DeviceKind kind in Enum.GetValues<DeviceKind>())
        {
            var item = new ToolStripMenuItem(Strings.KindName(kind)) { Checked = overridden && chosen == kind };
            item.Click += (_, _) => SetKind(key, kind);
            kindMenu.DropDownItems.Add(item);
        }

        menu.DropDownItems.Add(kindMenu);
        return menu;
    }

    private ToolStripMenuItem LanguageMenu()
    {
        var menu = new ToolStripMenuItem(Strings.Language);
        string systemCode = Languages.Resolve(null, CultureInfo.CurrentUICulture);
        string systemName = Languages.Supported.First(l => l.Code == systemCode).NativeName;

        var auto = new ToolStripMenuItem($"{Strings.LanguageAuto} ({systemName})") { Checked = _settings.Language is null };
        auto.Click += (_, _) => SetLanguage(null);
        menu.DropDownItems.Add(auto);
        menu.DropDownItems.Add(new ToolStripSeparator());

        foreach ((string code, string nativeName) in Languages.Supported)
        {
            var item = new ToolStripMenuItem(nativeName) { Checked = _settings.Language == code };
            item.Click += (_, _) => SetLanguage(code);
            menu.DropDownItems.Add(item);
        }

        return menu;
    }

    private void SetLanguage(string? code)
    {
        _settings.Language = code;
        _store.Save(_settings);
        ApplyLanguage();
        _tray.Invalidate(); // tooltips are rebuilt in the new language
        _tray.Sync(_devices, _settings);
    }

    private void ApplyLanguage() =>
        Strings.Culture = CultureInfo.GetCultureInfo(Languages.Resolve(_settings.Language, CultureInfo.CurrentUICulture));

    private void SetKind(string key, DeviceKind? kind)
    {
        if (kind is DeviceKind k)
        {
            _settings.DeviceKinds[key] = k;
        }
        else
        {
            _settings.DeviceKinds.Remove(key);
        }

        _store.Save(_settings);
        _tray.Sync(_devices, _settings);
    }

    private void ToggleHidden(string key)
    {
        if (!_settings.HiddenDevices.Remove(key))
        {
            _settings.HiddenDevices.Add(key);
        }

        _store.Save(_settings);
        _tray.Sync(_devices, _settings);
    }
}
