using DeviceBatteryTray.Battery;

namespace DeviceBatteryTray.Services;

/// <summary>
/// Decides when to warn about a low battery: once when a device drops to the threshold, and again
/// only after it has been charged noticeably above it.
/// </summary>
internal sealed class LowBatteryNotifier
{
    public const int Hysteresis = 5;

    private readonly HashSet<string> _warned = new(StringComparer.Ordinal);

    public IReadOnlyList<DeviceView> Check(IEnumerable<DeviceView> devices, int lowPercent)
    {
        var toWarn = new List<DeviceView>();
        foreach (DeviceView device in devices)
        {
            if (device.Percent is not int percent)
            {
                continue;
            }

            if (percent <= lowPercent)
            {
                if (_warned.Add(device.Key))
                {
                    toWarn.Add(device);
                }
            }
            else if (percent > lowPercent + Hysteresis)
            {
                _warned.Remove(device.Key);
            }
        }

        return toWarn;
    }
}
