namespace DeviceBatteryTray.Battery;

/// <summary>Pure helpers for interpreting HID battery values.</summary>
internal static class HidValueMath
{
    /// <summary>
    /// Generic Device Controls "Battery Strength" (0x06:0x20) and Battery System
    /// "Absolute State Of Charge" (0x85:0x65).
    /// </summary>
    public static bool IsBatteryUsage(ushort usagePage, ushort usage) =>
        (usagePage == 0x06 && usage == 0x20) || (usagePage == 0x85 && usage == 0x65);

    /// <summary>Scales a raw value to 0..100 using the descriptor's logical range; null if invalid.</summary>
    public static int? ToPercent(uint raw, int logicalMin, int logicalMax)
    {
        if (logicalMax > logicalMin)
        {
            if (raw < (long)logicalMin || raw > (long)logicalMax)
            {
                return null;
            }

            return (int)Math.Round((raw - (double)logicalMin) * 100.0 / (logicalMax - (double)logicalMin));
        }

        // No usable range in the descriptor: accept only values that already look like a percentage.
        return raw <= 100 ? (int)raw : null;
    }

    /// <summary>A GET_REPORT reply is only trusted when it carries the requested report ID.</summary>
    public static bool IsExpectedReply(ReadOnlySpan<byte> reply, byte reportId) =>
        reply.Length > 1 && reply[0] == reportId;
}
