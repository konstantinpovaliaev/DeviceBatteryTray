using System.Text;

namespace DeviceBatteryTray.Native;

/// <summary>Well-known device property keys.</summary>
internal static class DevKeys
{
    public static readonly DevPropKey FriendlyName = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 14);
    public static readonly DevPropKey DeviceDesc = new(new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), 2);
    public static readonly DevPropKey ContainerId = new(new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), 2);
    public static readonly DevPropKey InstanceId = new(new Guid("78c34fc8-104a-4aca-9ea4-524d52996e57"), 256);

    /// <summary>Battery level Windows keeps for Bluetooth devices (Settings > Bluetooth &amp; devices).</summary>
    public static readonly DevPropKey BluetoothBattery = new(new Guid("104ea319-6ee2-4701-bd47-8ddbf425bbe5"), 2);

    /// <summary>Bluetooth "connected" flag on the device's root node (BTHENUM\DEV_* / BTHLE\DEV_*).</summary>
    public static readonly DevPropKey BluetoothConnected = new(new Guid("83da6326-97a6-4088-9453-a1923f573b29"), 15);

    /// <summary>DEVPKEY_DeviceContainer_Category, e.g. "Input.Keyboard", "Audio.Speakers".</summary>
    public static readonly DevPropKey ContainerCategory = new(new Guid("78c34fc8-104a-4aca-9ea4-524d52996e57"), 90);

    /// <summary>DEVPKEY_Bluetooth_ClassOfDevice (classic Bluetooth).</summary>
    public static readonly DevPropKey BluetoothClassOfDevice = new(new Guid("2bd67d8b-8beb-48d5-87e0-6cda3428040a"), 10);
}

/// <summary>
/// Read-only access to the Windows device tree through cfgmgr32. Only reads values Windows already
/// holds; nothing here sends a request to a device.
/// </summary>
internal static class DeviceProperties
{
    public static IReadOnlyList<string> PresentDeviceIds()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (Win32.CM_Get_Device_ID_List_SizeW(out uint length, null, Win32.CM_GETIDLIST_FILTER_PRESENT) != Win32.CR_SUCCESS)
            {
                return [];
            }

            var buffer = new char[length];
            int result = Win32.CM_Get_Device_ID_ListW(null, buffer, length, Win32.CM_GETIDLIST_FILTER_PRESENT);
            if (result == Win32.CR_SUCCESS)
            {
                return SplitMultiString(buffer);
            }

            if (result != Win32.CR_BUFFER_SMALL)
            {
                return [];
            }
        }

        return [];
    }

    public static IReadOnlyList<string> PresentInterfaces(Guid interfaceClass)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (Win32.CM_Get_Device_Interface_List_SizeW(out uint length, in interfaceClass, null,
                    Win32.CM_GET_DEVICE_INTERFACE_LIST_PRESENT) != Win32.CR_SUCCESS)
            {
                return [];
            }

            var buffer = new char[length];
            int result = Win32.CM_Get_Device_Interface_ListW(in interfaceClass, null, buffer, length,
                Win32.CM_GET_DEVICE_INTERFACE_LIST_PRESENT);
            if (result == Win32.CR_SUCCESS)
            {
                return SplitMultiString(buffer);
            }

            if (result != Win32.CR_BUFFER_SMALL)
            {
                return [];
            }
        }

        return [];
    }

    public static string? InterfaceInstanceId(string deviceInterface)
    {
        uint size = 0;
        int result = Win32.CM_Get_Device_Interface_PropertyW(deviceInterface, in DevKeys.InstanceId, out _, null, ref size, 0);
        if (result != Win32.CR_BUFFER_SMALL || size == 0)
        {
            return null;
        }

        var buffer = new byte[size];
        result = Win32.CM_Get_Device_Interface_PropertyW(deviceInterface, in DevKeys.InstanceId, out uint type, buffer, ref size, 0);
        return result == Win32.CR_SUCCESS && type == Win32.DEVPROP_TYPE_STRING ? DecodeString(buffer) : null;
    }

    public static bool TryLocate(string instanceId, out uint devInst) =>
        Win32.CM_Locate_DevNodeW(out devInst, instanceId, 0) == Win32.CR_SUCCESS;

    public static string? GetString(uint devInst, DevPropKey key) =>
        TryGet(devInst, key, out uint type, out byte[] data) && type == Win32.DEVPROP_TYPE_STRING ? DecodeString(data) : null;

    public static Guid? GetGuid(uint devInst, DevPropKey key) =>
        TryGet(devInst, key, out uint type, out byte[] data) && type == Win32.DEVPROP_TYPE_GUID && data.Length == 16
            ? new Guid(data)
            : null;

    public static byte? GetByte(uint devInst, DevPropKey key) =>
        TryGet(devInst, key, out uint type, out byte[] data) && type == Win32.DEVPROP_TYPE_BYTE && data.Length >= 1
            ? data[0]
            : null;

    public static uint? GetUInt32(uint devInst, DevPropKey key) =>
        TryGet(devInst, key, out uint type, out byte[] data) && type == Win32.DEVPROP_TYPE_UINT32 && data.Length >= 4
            ? BitConverter.ToUInt32(data, 0)
            : null;

    /// <summary>First entry of a string (or string list) property.</summary>
    public static string? GetFirstString(uint devInst, DevPropKey key)
    {
        if (!TryGet(devInst, key, out uint type, out byte[] data) ||
            (type != Win32.DEVPROP_TYPE_STRING && type != Win32.DEVPROP_TYPE_STRING_LIST))
        {
            return null;
        }

        return DecodeString(data).Split('\0', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    }

    public static bool? GetBool(uint devInst, DevPropKey key) =>
        TryGet(devInst, key, out uint type, out byte[] data) && type == Win32.DEVPROP_TYPE_BOOLEAN && data.Length >= 1
            ? data[0] != 0
            : null;

    private static bool TryGet(uint devInst, DevPropKey key, out uint type, out byte[] data)
    {
        data = [];
        uint size = 0;
        int result = Win32.CM_Get_DevNode_PropertyW(devInst, in key, out type, null, ref size, 0);
        if (result != Win32.CR_BUFFER_SMALL || size == 0)
        {
            return false;
        }

        data = new byte[size];
        return Win32.CM_Get_DevNode_PropertyW(devInst, in key, out type, data, ref size, 0) == Win32.CR_SUCCESS;
    }

    private static string DecodeString(byte[] data) => Encoding.Unicode.GetString(data).TrimEnd('\0');

    internal static IReadOnlyList<string> SplitMultiString(char[] buffer) =>
        new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries);
}
