using DeviceBatteryTray.Battery;
using DeviceBatteryTray.Services;
using DeviceBatteryTray.Ui;

namespace DeviceBatteryTray.Tests;

[TestClass]
public sealed class DeviceKindsTests
{
    [TestMethod]
    // Expected kinds are passed by name: DeviceKind is internal and cannot appear in a public test signature.
    [DataRow("Input.Keyboard", "Keyboard")]
    [DataRow("Input.Mouse", "Mouse")]
    [DataRow("Audio.Speakers", "Speaker")]
    [DataRow("Audio.Headphone", "Headphones")]
    [DataRow("Audio.Headset", "Headphones")]
    [DataRow("Input.Gaming", "Gamepad")]
    [DataRow("Display.Monitor", "Other")]
    [DataRow("", "Other")]
    public void FromCategory(string category, string expected) =>
        Assert.AreEqual(Enum.Parse<DeviceKind>(expected), DeviceKinds.FromCategory(category));

    [TestMethod]
    [DataRow(0x000540u, "Keyboard")]   // Keychron K2 (observed)
    [DataRow(0x240414u, "Speaker")]    // JLab JBuds Party (observed)
    [DataRow(0x002580u, "Mouse")]      // peripheral, pointing
    [DataRow(0x002508u, "Gamepad")]    // peripheral, gamepad
    [DataRow(0x240404u, "Headphones")] // audio, wearable headset
    [DataRow(0x5A020Cu, "Other")]      // phone
    public void FromClassOfDevice(uint cod, string expected) =>
        Assert.AreEqual(Enum.Parse<DeviceKind>(expected), DeviceKinds.FromClassOfDevice(cod));

    [TestMethod]
    public void UserChoiceOverridesDetection()
    {
        var device = new DeviceView("k", "SMA225", 50, BatterySource.Windows, null, true, DeviceKind.Mouse);
        var settings = new AppSettings();
        Assert.AreEqual(DeviceKind.Mouse, TrayIconManager.KindFor(device, settings));
        settings.DeviceKinds["k"] = DeviceKind.Keyboard;
        Assert.AreEqual(DeviceKind.Keyboard, TrayIconManager.KindFor(device, settings));
    }

    [TestMethod]
    public void EveryKindHasItsOwnGlyph() =>
        Assert.AreEqual(Enum.GetValues<DeviceKind>().Length,
            Enum.GetValues<DeviceKind>().Select(IconRenderer.Glyph).Distinct().Count());

    [TestMethod]
    public void SettingsKeepDeviceKinds()
    {
        string path = Path.Combine(Path.GetTempPath(), "DeviceBatteryTray.Tests", Guid.NewGuid().ToString("N"), "settings.json");
        var store = new SettingsStore(path);
        var settings = new AppSettings();
        settings.DeviceKinds["{K2}"] = DeviceKind.Keyboard;
        store.Save(settings);

        Assert.AreEqual(DeviceKind.Keyboard, store.Load().DeviceKinds["{K2}"]);
        StringAssert.Contains(File.ReadAllText(path), "\"Keyboard\"");
    }

    [TestMethod]
    public void RendersEveryKindAndLevel()
    {
        foreach (DeviceKind kind in Enum.GetValues<DeviceKind>())
        {
            foreach (BatteryLevel level in Enum.GetValues<BatteryLevel>())
            {
                IntPtr icon = IconRenderer.Render(kind, level, connected: false, lightTaskbar: true, size: 20);
                Assert.AreNotEqual(IntPtr.Zero, icon);
                DeviceBatteryTray.Native.Win32.DestroyIcon(icon);
            }
        }
    }
}

[TestClass]
public sealed class IconInkTests
{
    [TestMethod]
    public void NormalAndUnknownUseTaskbarInkOnly()
    {
        Assert.AreEqual(Color.White.ToArgb(), IconRenderer.Ink(BatteryLevel.Normal, true, lightTaskbar: false).ToArgb());
        Assert.AreEqual(Color.White.ToArgb(), IconRenderer.Ink(BatteryLevel.Unknown, true, lightTaskbar: false).ToArgb());
        Assert.AreEqual(Color.FromArgb(0x1F, 0x1F, 0x1F).ToArgb(), IconRenderer.Ink(BatteryLevel.Normal, true, lightTaskbar: true).ToArgb());
    }

    [TestMethod]
    public void LowAndCriticalAreColouredButMuted()
    {
        foreach (bool light in new[] { false, true })
        {
            Color low = IconRenderer.Ink(BatteryLevel.Low, true, light);
            Color critical = IconRenderer.Ink(BatteryLevel.Critical, true, light);
            Assert.AreNotEqual(low.ToArgb(), critical.ToArgb());
            Assert.IsTrue(low.GetSaturation() < 0.75f && critical.GetSaturation() < 0.75f, "muted, not neon");
            Assert.IsTrue(low.GetSaturation() > 0.3f && critical.GetSaturation() > 0.3f, "still clearly coloured");
        }
    }

    [TestMethod]
    public void AsleepFadesOnlyWhenNoWarning()
    {
        Assert.AreEqual(IconRenderer.AsleepAlpha, IconRenderer.Ink(BatteryLevel.Normal, connected: false, lightTaskbar: false).A);
        Assert.AreEqual(255, IconRenderer.Ink(BatteryLevel.Critical, connected: false, lightTaskbar: false).A);
    }
}
