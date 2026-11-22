using DeviceBatteryTray.Services;

namespace DeviceBatteryTray.Battery;

/// <summary>
/// Collects battery levels from all sources and keeps the last good value per device.
/// Not thread-safe: the caller runs one Poll at a time (on a background thread).
/// </summary>
internal sealed class BatteryPoller
{
    private sealed class State
    {
        public string Name = "";
        public DeviceKind Kind;
        public int? Percent;
        public BatterySource? Source;
        public DateTimeOffset? ReadAt;
        public bool Connected;
        public bool? WasConnected;
        public bool HasHid;
        public DateTimeOffset? LastHidAttempt;
        public DateTimeOffset? LastHidSuccess;
    }

    private readonly Dictionary<string, State> _states = new(StringComparer.Ordinal);
    private IReadOnlyList<HidBatteryEndpoint> _endpoints = [];
    private volatile bool _rediscover = true;

    /// <summary>Called when Windows reports a device change; HID collections are rescanned on the next poll.</summary>
    public void MarkDevicesChanged() => _rediscover = true;

    public IReadOnlyList<DeviceView> Poll(bool manual, TimeSpan interval)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        DeviceTopology topology = DeviceTopology.Capture();

        bool hidDeviceWithoutEndpoint = _states.Any(s =>
            s.Value.HasHid && topology.Containers.TryGetValue(s.Key, out ContainerInfo? c) && c.Connected &&
            !_endpoints.Any(e => e.ContainerKey == s.Key));
        if (_rediscover || hidDeviceWithoutEndpoint)
        {
            _rediscover = false;
            _endpoints = HidBatterySource.Discover(topology);
        }

        var keys = new HashSet<string>(topology.WindowsBattery.Keys, StringComparer.Ordinal);
        keys.UnionWith(_endpoints.Select(e => e.ContainerKey));
        // A sleeping device may drop its HID collections; keep showing it while Windows still knows it.
        keys.UnionWith(_states.Where(s => s.Value.HasHid && topology.Containers.ContainsKey(s.Key)).Select(s => s.Key));

        foreach (string gone in _states.Keys.Where(k => !keys.Contains(k)).ToList())
        {
            _states.Remove(gone);
        }

        TimeSpan sinceInput = IdleTime.SinceLastInput();
        foreach (string key in keys)
        {
            if (!_states.TryGetValue(key, out State? state))
            {
                state = new State();
                _states[key] = state;
            }

            ContainerInfo? container = topology.Containers.GetValueOrDefault(key);
            state.Name = container?.Name ?? (state.Name.Length > 0 ? state.Name : key);
            state.Kind = container?.Kind ?? state.Kind;
            bool connected = container?.Connected ?? false;
            bool justReconnected = connected && state.WasConnected == false;
            state.WasConnected = connected;
            state.Connected = connected;

            List<HidBatteryEndpoint> endpoints = _endpoints.Where(e => e.ContainerKey == key).ToList();
            state.HasHid |= endpoints.Count > 0;

            // Windows' own value is cached by the Bluetooth stack: reading it costs the device nothing.
            // For devices with a HID battery usage, the HID value wins once it has been read.
            if (topology.WindowsBattery.TryGetValue(key, out int windowsPercent) && state.LastHidSuccess is null)
            {
                state.Percent = windowsPercent;
                state.Source = BatterySource.Windows;
                state.ReadAt = now;
            }

            if (endpoints.Count == 0)
            {
                continue;
            }

            var input = new HidPollInput(now, connected, justReconnected, state.LastHidSuccess, state.LastHidAttempt,
                interval, sinceInput, manual);
            if (!PollPolicy.ShouldReadHid(input))
            {
                continue;
            }

            state.LastHidAttempt = now;
            int? percent = null;
            foreach (HidBatteryEndpoint endpoint in endpoints)
            {
                percent = HidBatterySource.Read(endpoint);
                if (percent is not null)
                {
                    break;
                }
            }

            Log.Write($"HID read {state.Name}: {(percent is int p ? p + "%" : "no valid reply")}" +
                      (justReconnected ? " (after reconnect)" : manual ? " (manual)" : ""));
            if (percent is not null)
            {
                state.Percent = percent;
                state.Source = BatterySource.Hid;
                state.ReadAt = now;
                state.LastHidSuccess = now;
            }
        }

        List<DeviceView> views = _states
            .Select(s => new DeviceView(s.Key, s.Value.Name, s.Value.Percent, s.Value.Source, s.Value.ReadAt, s.Value.Connected, s.Value.Kind))
            .OrderBy(v => v.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        string summary = string.Join("; ", views.Select(v =>
            $"{v.Name} [{v.Kind}] {(v.Percent is int p ? p + "%" : "?")} {v.Source?.ToString() ?? "-"}{(v.Connected ? "" : " disconnected")}"));
        if (summary != _lastSummary)
        {
            _lastSummary = summary;
            Log.Write("devices: " + (summary.Length > 0 ? summary : "none") + $" | HID endpoints: {_endpoints.Count}");
        }

        return views;
    }

    private string? _lastSummary;
}
