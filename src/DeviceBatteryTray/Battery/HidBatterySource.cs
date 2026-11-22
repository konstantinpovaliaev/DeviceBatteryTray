using System.Diagnostics;
using DeviceBatteryTray.Native;
using DeviceBatteryTray.Services;
using Microsoft.Win32.SafeHandles;

namespace DeviceBatteryTray.Battery;

internal enum HidReportKind
{
    Input = 0,
    Feature = 2,
}

/// <summary>A HID collection whose descriptor declares a battery value.</summary>
internal sealed record HidBatteryEndpoint(
    string Path,
    string ContainerKey,
    HidReportKind Kind,
    byte ReportId,
    ushort ReportLength,
    ushort UsagePage,
    ushort Usage,
    ushort LinkCollection,
    int LogicalMin,
    int LogicalMax);

/// <summary>
/// Finds and reads standard HID battery usages. Discovery only parses descriptors Windows already
/// holds (no device traffic). Reading sends HID GET_REPORT requests and is gated by PollPolicy.
/// Devices are opened with zero access rights and nothing is ever written to them.
/// </summary>
internal static class HidBatterySource
{
    private const int MaxAttempts = 6;
    private static readonly TimeSpan SlowReply = TimeSpan.FromSeconds(3);

    public static IReadOnlyList<HidBatteryEndpoint> Discover(DeviceTopology topology)
    {
        Win32.HidD_GetHidGuid(out Guid hidGuid);
        var endpoints = new List<HidBatteryEndpoint>();

        foreach (string path in DeviceProperties.PresentInterfaces(hidGuid))
        {
            using SafeFileHandle handle = Open(path);
            if (handle.IsInvalid || !Win32.HidD_GetPreparsedData(handle, out IntPtr preparsed))
            {
                continue;
            }

            try
            {
                if (Win32.HidP_GetCaps(preparsed, out HidpCaps caps) != Win32.HIDP_STATUS_SUCCESS)
                {
                    continue;
                }

                string? instanceId = DeviceProperties.InterfaceInstanceId(path);
                string containerKey = instanceId is null ? "path:" + path.ToUpperInvariant() : topology.ContainerKeyFor(instanceId);

                Collect(endpoints, path, containerKey, preparsed, HidReportKind.Input, caps.NumberInputValueCaps, caps.InputReportByteLength);
                Collect(endpoints, path, containerKey, preparsed, HidReportKind.Feature, caps.NumberFeatureValueCaps, caps.FeatureReportByteLength);
            }
            finally
            {
                Win32.HidD_FreePreparsedData(preparsed);
            }
        }

        return endpoints;
    }

    /// <summary>Returns the battery percentage, or null when the device gave no valid reply.</summary>
    public static int? Read(HidBatteryEndpoint endpoint)
    {
        using SafeFileHandle handle = Open(endpoint.Path);
        if (handle.IsInvalid || !Win32.HidD_GetPreparsedData(handle, out IntPtr preparsed))
        {
            return null;
        }

        try
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var report = new byte[endpoint.ReportLength];
                report[0] = endpoint.ReportId;
                var timer = Stopwatch.StartNew();
                bool ok = endpoint.Kind == HidReportKind.Input
                    ? Win32.HidD_GetInputReport(handle, report, report.Length)
                    : Win32.HidD_GetFeature(handle, report, report.Length);

                if (ok && HidValueMath.IsExpectedReply(report, endpoint.ReportId) &&
                    Win32.HidP_GetUsageValue((int)endpoint.Kind, endpoint.UsagePage, endpoint.LinkCollection, endpoint.Usage,
                        out uint value, preparsed, report, (uint)report.Length) == Win32.HIDP_STATUS_SUCCESS)
                {
                    return HidValueMath.ToPercent(value, endpoint.LogicalMin, endpoint.LogicalMax);
                }

                // A failed or slow request means the device is not answering: do not insist.
                if (!ok || timer.Elapsed > SlowReply)
                {
                    Log.Write($"HID read stopped after attempt {attempt + 1} (ok={ok}, {timer.ElapsedMilliseconds} ms)");
                    break;
                }

                // ok but a different report ID: some keyboards answer with unrelated reports in rotation.
            }
        }
        finally
        {
            Win32.HidD_FreePreparsedData(preparsed);
        }

        return null;
    }

    private static void Collect(List<HidBatteryEndpoint> endpoints, string path, string containerKey, IntPtr preparsed,
        HidReportKind kind, ushort count, ushort reportLength)
    {
        if (count == 0 || reportLength < 2)
        {
            return;
        }

        var valueCaps = new HidpValueCaps[count];
        ushort length = count;
        if (Win32.HidP_GetValueCaps((int)kind, valueCaps, ref length, preparsed) != Win32.HIDP_STATUS_SUCCESS)
        {
            return;
        }

        for (int i = 0; i < length; i++)
        {
            HidpValueCaps cap = valueCaps[i];
            ushort last = cap.IsRange != 0 ? cap.UsageMax : cap.UsageMin;
            for (int usage = cap.UsageMin; usage <= last; usage++)
            {
                if (HidValueMath.IsBatteryUsage(cap.UsagePage, (ushort)usage))
                {
                    endpoints.Add(new HidBatteryEndpoint(path, containerKey, kind, cap.ReportId, reportLength,
                        cap.UsagePage, (ushort)usage, cap.LinkCollection, cap.LogicalMin, cap.LogicalMax));
                    break;
                }
            }
        }
    }

    private static SafeFileHandle Open(string path) =>
        Win32.CreateFileW(path, 0, Win32.FILE_SHARE_READ_WRITE, IntPtr.Zero, Win32.OPEN_EXISTING, 0, IntPtr.Zero);
}
