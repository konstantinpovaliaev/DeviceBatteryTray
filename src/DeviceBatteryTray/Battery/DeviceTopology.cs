using DeviceBatteryTray.Native;

namespace DeviceBatteryTray.Battery;

/// <summary>
/// Snapshot of the present devices, grouped by physical device (container), with the Bluetooth
/// battery levels and connection flags Windows already holds. Capturing it sends nothing to any device.
/// </summary>
internal sealed class DeviceTopology
{
    private static readonly Guid LocalMachineContainer = new("00000000-0000-0000-ffff-ffffffffffff");

    public required IReadOnlyDictionary<string, ContainerInfo> Containers { get; init; }

    /// <summary>Device instance id (upper case) to container key.</summary>
    public required IReadOnlyDictionary<string, string> InstanceToContainer { get; init; }

    /// <summary>Container key to the Windows Bluetooth battery percentage.</summary>
    public required IReadOnlyDictionary<string, int> WindowsBattery { get; init; }

    public string ContainerKeyFor(string instanceId) =>
        InstanceToContainer.TryGetValue(instanceId.ToUpperInvariant(), out string? key) ? key : InstanceKey(instanceId);

    public static DeviceTopology Capture()
    {
        var instanceToContainer = new Dictionary<string, string>(StringComparer.Ordinal);
        var battery = new Dictionary<string, int>(StringComparer.Ordinal);
        var rootName = new Dictionary<string, string>(StringComparer.Ordinal);
        var rootConnected = new Dictionary<string, bool>(StringComparer.Ordinal);
        var anyName = new Dictionary<string, string>(StringComparer.Ordinal);
        var kinds = new Dictionary<string, DeviceKind>(StringComparer.Ordinal);

        foreach (string id in DeviceProperties.PresentDeviceIds())
        {
            if (!DeviceProperties.TryLocate(id, out uint node))
            {
                continue;
            }

            Guid? container = DeviceProperties.GetGuid(node, DevKeys.ContainerId);
            string key = container is Guid g && g != LocalMachineContainer && g != Guid.Empty
                ? g.ToString("B").ToUpperInvariant()
                : InstanceKey(id);
            instanceToContainer[id.ToUpperInvariant()] = key;

            string? name = DeviceProperties.GetString(node, DevKeys.FriendlyName);
            if (name is not null)
            {
                anyName.TryAdd(key, name);
            }

            if (IsBluetoothRoot(id))
            {
                if (name is not null)
                {
                    rootName[key] = name;
                }

                rootConnected[key] = DeviceProperties.GetBool(node, DevKeys.BluetoothConnected) ?? true;

                DeviceKind kind = DeviceKinds.FromCategory(DeviceProperties.GetFirstString(node, DevKeys.ContainerCategory));
                if (kind == DeviceKind.Other && DeviceProperties.GetUInt32(node, DevKeys.BluetoothClassOfDevice) is uint cod)
                {
                    kind = DeviceKinds.FromClassOfDevice(cod);
                }

                kinds[key] = kind;
            }
            else if (!kinds.ContainsKey(key) &&
                     DeviceKinds.FromCategory(DeviceProperties.GetFirstString(node, DevKeys.ContainerCategory)) is var other &&
                     other != DeviceKind.Other)
            {
                kinds[key] = other; // non-Bluetooth devices (e.g. USB receivers) may carry the category too
            }

            if (DeviceProperties.GetByte(node, DevKeys.BluetoothBattery) is byte level && level <= 100)
            {
                battery[key] = battery.TryGetValue(key, out int existing) ? Math.Max(existing, level) : level;
            }
        }

        var containers = new Dictionary<string, ContainerInfo>(StringComparer.Ordinal);
        foreach (string key in instanceToContainer.Values.Distinct())
        {
            bool isBluetooth = rootConnected.ContainsKey(key);
            string name = rootName.GetValueOrDefault(key) ?? anyName.GetValueOrDefault(key) ?? key;
            containers[key] = new ContainerInfo(key, name, isBluetooth, !isBluetooth || rootConnected[key],
                kinds.GetValueOrDefault(key, DeviceKind.Other));
        }

        return new DeviceTopology
        {
            Containers = containers,
            InstanceToContainer = instanceToContainer,
            WindowsBattery = battery,
        };
    }

    internal static bool IsBluetoothRoot(string instanceId) =>
        instanceId.StartsWith(@"BTHENUM\DEV_", StringComparison.OrdinalIgnoreCase) ||
        instanceId.StartsWith(@"BTHLE\DEV_", StringComparison.OrdinalIgnoreCase);

    private static string InstanceKey(string instanceId) => "dev:" + instanceId.ToUpperInvariant();
}
