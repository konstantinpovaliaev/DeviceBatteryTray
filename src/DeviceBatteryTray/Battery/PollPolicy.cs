namespace DeviceBatteryTray.Battery;

/// <summary>Inputs for deciding whether a HID battery request may be sent to a device now.</summary>
internal readonly record struct HidPollInput(
    DateTimeOffset Now,
    bool Connected,
    bool JustReconnected,
    DateTimeOffset? LastSuccess,
    DateTimeOffset? LastAttempt,
    TimeSpan Interval,
    TimeSpan SinceLastInput,
    bool Manual);

/// <summary>
/// Power-safe rules for HID requests (see docs/TECHNICAL.md, "Power-safe polling"): never page a disconnected
/// (sleeping) device, and otherwise only ask while the user is active, so the device is awake anyway.
/// </summary>
internal static class PollPolicy
{
    /// <summary>User input this recent means the user is at the computer and the devices are awake.</summary>
    public static readonly TimeSpan RecentInput = TimeSpan.FromSeconds(60);

    /// <summary>Minimum gap between any two requests to the same device.</summary>
    public static readonly TimeSpan MinAttemptGap = TimeSpan.FromMinutes(1);

    /// <summary>Wait after a failed request before trying again.</summary>
    public static readonly TimeSpan FailureBackoff = TimeSpan.FromMinutes(5);

    public static bool ShouldReadHid(in HidPollInput input)
    {
        if (!input.Connected)
        {
            return false;
        }

        if (input.LastAttempt is DateTimeOffset attempt)
        {
            TimeSpan sinceAttempt = input.Now - attempt;
            if (sinceAttempt < MinAttemptGap)
            {
                return false;
            }

            bool lastFailed = input.LastSuccess is null || input.LastSuccess < attempt;
            if (lastFailed && sinceAttempt < FailureBackoff && !input.JustReconnected)
            {
                return false;
            }
        }

        if (input.JustReconnected)
        {
            return true; // The device has just woken up because the user used it.
        }

        if (input.SinceLastInput > RecentInput)
        {
            return false;
        }

        if (input.Manual)
        {
            return true;
        }

        return input.LastSuccess is not DateTimeOffset success || input.Now - success >= input.Interval;
    }
}
