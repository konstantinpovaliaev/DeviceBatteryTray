using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DeviceBatteryTray.Native;

/// <summary>Device property key (DEVPROPKEY).</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct DevPropKey(Guid fmtId, uint pid)
{
    public readonly Guid FmtId = fmtId;
    public readonly uint Pid = pid;
}

/// <summary>HIDP_CAPS; only the fields this app uses are mapped.</summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
internal struct HidpCaps
{
    [FieldOffset(0)] public ushort Usage;
    [FieldOffset(2)] public ushort UsagePage;
    [FieldOffset(4)] public ushort InputReportByteLength;
    [FieldOffset(6)] public ushort OutputReportByteLength;
    [FieldOffset(8)] public ushort FeatureReportByteLength;
    [FieldOffset(48)] public ushort NumberInputValueCaps;
    [FieldOffset(60)] public ushort NumberFeatureValueCaps;
}

/// <summary>HIDP_VALUE_CAPS; only the fields this app uses are mapped.</summary>
[StructLayout(LayoutKind.Explicit, Size = 72)]
internal struct HidpValueCaps
{
    [FieldOffset(0)] public ushort UsagePage;
    [FieldOffset(2)] public byte ReportId;
    [FieldOffset(6)] public ushort LinkCollection;
    [FieldOffset(12)] public byte IsRange;
    [FieldOffset(40)] public int LogicalMin;
    [FieldOffset(44)] public int LogicalMax;
    [FieldOffset(56)] public ushort UsageMin; // NotRange.Usage when IsRange == 0
    [FieldOffset(58)] public ushort UsageMax;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HiddAttributes
{
    public int Size;
    public ushort VendorId;
    public ushort ProductId;
    public ushort VersionNumber;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct NotifyIconData
{
    public int cbSize;
    public IntPtr hWnd;
    public uint uID;
    public uint uFlags;
    public uint uCallbackMessage;
    public IntPtr hIcon;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
    public uint dwState;
    public uint dwStateMask;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
    public uint uVersion;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
    public uint dwInfoFlags;
    public Guid guidItem;
    public IntPtr hBalloonIcon;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LastInputInfo
{
    public uint cbSize;
    public uint dwTime;
}

/// <summary>
/// All native calls of the app. Only local system DLLs: device configuration, HID, the shell
/// notification area and window helpers. No networking APIs (enforced by NoNetworkGuardTests).
/// </summary>
internal static class Win32
{
    // cfgmgr32
    public const int CR_SUCCESS = 0x00;
    public const int CR_BUFFER_SMALL = 0x1A;
    public const uint CM_GETIDLIST_FILTER_PRESENT = 0x100;
    public const uint CM_GET_DEVICE_INTERFACE_LIST_PRESENT = 0x0;
    public const uint DEVPROP_TYPE_BYTE = 0x03;
    public const uint DEVPROP_TYPE_UINT32 = 0x07;
    public const uint DEVPROP_TYPE_STRING_LIST = 0x2012;
    public const uint DEVPROP_TYPE_GUID = 0x0D;
    public const uint DEVPROP_TYPE_BOOLEAN = 0x11;
    public const uint DEVPROP_TYPE_STRING = 0x12;

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_ID_List_SizeW(out uint length, string? filter, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_ID_ListW(string? filter, [Out] char[] buffer, uint length, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Locate_DevNodeW(out uint devInst, string deviceId, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_DevNode_PropertyW(uint devInst, in DevPropKey key, out uint type,
        [Out] byte[]? buffer, ref uint size, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_Interface_List_SizeW(out uint length, in Guid interfaceClass, string? deviceId, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_Interface_ListW(in Guid interfaceClass, string? deviceId,
        [Out] char[] buffer, uint length, uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    public static extern int CM_Get_Device_Interface_PropertyW(string deviceInterface, in DevPropKey key, out uint type,
        [Out] byte[]? buffer, ref uint size, uint flags);

    // hid
    public const int HIDP_STATUS_SUCCESS = 0x00110000;

    [DllImport("hid.dll")]
    public static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_GetAttributes(SafeFileHandle device, ref HiddAttributes attributes);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr preparsedData);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_GetInputReport(SafeFileHandle device, [In, Out] byte[] buffer, int length);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool HidD_GetFeature(SafeFileHandle device, [In, Out] byte[] buffer, int length);

    [DllImport("hid.dll")]
    public static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

    [DllImport("hid.dll")]
    public static extern int HidP_GetValueCaps(int reportType, [Out] HidpValueCaps[] valueCaps, ref ushort length, IntPtr preparsedData);

    [DllImport("hid.dll")]
    public static extern int HidP_GetUsageValue(int reportType, ushort usagePage, ushort linkCollection, ushort usage,
        out uint usageValue, IntPtr preparsedData, [In] byte[] report, uint reportLength);

    // kernel32
    public const uint FILE_SHARE_READ_WRITE = 0x3;
    public const uint OPEN_EXISTING = 3;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    // user32
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetLastInputInfo(ref LastInputInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessageW(string message);

    // shell32
    public const uint NIM_ADD = 0x0;
    public const uint NIM_MODIFY = 0x1;
    public const uint NIM_DELETE = 0x2;
    public const uint NIM_SETVERSION = 0x4;
    public const uint NIF_MESSAGE = 0x01;
    public const uint NIF_ICON = 0x02;
    public const uint NIF_TIP = 0x04;
    public const uint NIF_INFO = 0x10;
    public const uint NIF_GUID = 0x20;
    public const uint NIF_SHOWTIP = 0x80;
    public const uint NIIF_WARNING = 0x02;
    public const uint NIIF_RESPECT_QUIET_TIME = 0x80;
    public const uint NOTIFYICON_VERSION_4 = 4;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);
}
