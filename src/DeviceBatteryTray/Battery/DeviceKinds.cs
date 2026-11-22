namespace DeviceBatteryTray.Battery;

internal enum DeviceKind
{
    Other,
    Keyboard,
    Mouse,
    Headphones,
    Speaker,
    Gamepad,
}

/// <summary>Maps what Windows knows about a device to the pictogram shown in its tray icon.</summary>
internal static class DeviceKinds
{
    /// <summary>From the container category Windows shows in Settings, e.g. "Input.Keyboard", "Audio.Speakers".</summary>
    public static DeviceKind FromCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return DeviceKind.Other;
        }

        bool Is(string prefix) => category.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        if (Is("Input.Keyboard"))
        {
            return DeviceKind.Keyboard;
        }

        if (Is("Input.Mouse") || Is("Input.Touchpad") || Is("Input.Trackball"))
        {
            return DeviceKind.Mouse;
        }

        if (Is("Input.Gaming") || Is("Input.Gamepad") || Is("Input.Joystick") || Is("Gaming"))
        {
            return DeviceKind.Gamepad;
        }

        if (Is("Audio.Headphone") || Is("Audio.Headset") || Is("Multimedia.Headset"))
        {
            return DeviceKind.Headphones;
        }

        return Is("Audio.Speaker") ? DeviceKind.Speaker : DeviceKind.Other;
    }

    /// <summary>From a Bluetooth Class of Device (classic Bluetooth only).</summary>
    public static DeviceKind FromClassOfDevice(uint classOfDevice)
    {
        uint major = (classOfDevice >> 8) & 0x1F;
        if (major == 0x05) // peripheral
        {
            uint input = (classOfDevice >> 6) & 0x3; // 1 keyboard, 2 pointing, 3 combo
            uint subtype = (classOfDevice >> 2) & 0xF; // 1 joystick, 2 gamepad
            return input switch
            {
                1 or 3 => DeviceKind.Keyboard,
                2 => DeviceKind.Mouse,
                _ => subtype is 1 or 2 ? DeviceKind.Gamepad : DeviceKind.Other,
            };
        }

        if (major == 0x04) // audio/video
        {
            uint minor = (classOfDevice >> 2) & 0x3F;
            return minor switch
            {
                0x01 or 0x02 or 0x06 => DeviceKind.Headphones, // headset, hands-free, headphones
                0x05 or 0x07 => DeviceKind.Speaker,             // loudspeaker, portable audio
                _ => DeviceKind.Other,
            };
        }

        return DeviceKind.Other;
    }
}
