namespace DeviceBatteryTray.Battery;

internal enum BatterySource
{
    /// <summary>Value Windows keeps for Bluetooth devices (HFP / GATT Battery Service).</summary>
    Windows,

    /// <summary>Standard HID battery usage read from the device.</summary>
    Hid,
}

internal enum BatteryLevel
{
    Unknown,
    Normal,
    Low,
    Critical,
}

/// <summary>What the UI shows for one physical device.</summary>
internal sealed record DeviceView(
    string Key,
    string Name,
    int? Percent,
    BatterySource? Source,
    DateTimeOffset? ReadAt,
    bool Connected,
    DeviceKind Kind = DeviceKind.Other)
{
    public BatteryLevel Level(int lowPercent, int criticalPercent) => Percent switch
    {
        null => BatteryLevel.Unknown,
        int p when p <= criticalPercent => BatteryLevel.Critical,
        int p when p <= lowPercent => BatteryLevel.Low,
        _ => BatteryLevel.Normal,
    };
}

/// <summary>One physical device as Windows groups it (a device container).</summary>
internal sealed record ContainerInfo(string Key, string Name, bool IsBluetooth, bool Connected, DeviceKind Kind);
