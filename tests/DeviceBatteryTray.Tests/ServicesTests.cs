using DeviceBatteryTray.Battery;
using DeviceBatteryTray.Services;
using DeviceBatteryTray.Ui;

namespace DeviceBatteryTray.Tests;

[TestClass]
public sealed class LowBatteryNotifierTests
{
    private static DeviceView Device(int? percent) => new("k2", "Keychron K2", percent, BatterySource.Hid, null, true);

    [TestMethod]
    public void WarnsOncePerDischarge()
    {
        var notifier = new LowBatteryNotifier();
        Assert.AreEqual(0, notifier.Check([Device(50)], 20).Count);
        Assert.AreEqual(1, notifier.Check([Device(20)], 20).Count);
        Assert.AreEqual(0, notifier.Check([Device(15)], 20).Count);
        Assert.AreEqual(0, notifier.Check([Device(24)], 20).Count, "within hysteresis: no reset");
        Assert.AreEqual(0, notifier.Check([Device(19)], 20).Count);
        Assert.AreEqual(0, notifier.Check([Device(26)], 20).Count, "charged above threshold + hysteresis: reset");
        Assert.AreEqual(1, notifier.Check([Device(18)], 20).Count);
    }

    [TestMethod]
    public void IgnoresUnknownLevels() => Assert.AreEqual(0, new LowBatteryNotifier().Check([Device(null)], 20).Count);
}

[TestClass]
public sealed class SettingsStoreTests
{
    [TestMethod]
    public void RoundTripsAndNormalizes()
    {
        string dir = Path.Combine(Path.GetTempPath(), "DeviceBatteryTray.Tests", Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(Path.Combine(dir, "settings.json"));
        store.Save(new AppSettings { PollMinutes = 1, LowPercent = 25, HiddenDevices = ["a", "a", " "] });

        AppSettings loaded = store.Load();
        Assert.AreEqual(5, loaded.PollMinutes, "clamped to the minimum");
        Assert.AreEqual(25, loaded.LowPercent);
        CollectionAssert.AreEqual(new[] { "a" }, loaded.HiddenDevices);
    }

    [TestMethod]
    public void DamagedFileGivesDefaults()
    {
        string dir = Path.Combine(Path.GetTempPath(), "DeviceBatteryTray.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "settings.json");
        File.WriteAllText(path, "{ not json");

        AppSettings loaded = new SettingsStore(path).Load();
        Assert.AreEqual(30, loaded.PollMinutes);
    }

    [TestMethod]
    public void MissingFileGivesDefaults() =>
        Assert.AreEqual(30, new SettingsStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "x.json")).Load().PollMinutes);
}

[TestClass]
public sealed class StableGuidTests
{
    [TestMethod]
    public void IsDeterministicAndDistinct()
    {
        Guid a = StableGuid.For(@"C:\Apps\DeviceBatteryTray.exe", "{K2}");
        Assert.AreEqual(a, StableGuid.For(@"c:\apps\devicebatterytray.exe", "{K2}"), "path is case-insensitive");
        Assert.AreNotEqual(a, StableGuid.For(@"C:\Apps\DeviceBatteryTray.exe", "{JBUDS}"));
        Assert.AreNotEqual(a, StableGuid.For(@"D:\Other\DeviceBatteryTray.exe", "{K2}"));
    }
}

[TestClass]
public sealed class TooltipTests
{
    [TestMethod]
    public void ShowsLevelSourceAndConnection()
    {
        var view = new DeviceView("k", "Keychron K2", 87, BatterySource.Hid, new DateTimeOffset(2026, 11, 22, 14, 5, 0, TimeSpan.Zero), false);
        string tip = TrayIconManager.Tooltip(view);
        StringAssert.StartsWith(tip, "Keychron K2: 87%");
        StringAssert.Contains(tip, "HID, 14:05");
        StringAssert.Contains(tip, Strings.NotConnected);
        Assert.IsTrue(tip.Length <= 127);
    }

    [TestMethod]
    public void LevelThresholds()
    {
        DeviceView At(int? p) => new("k", "n", p, null, null, true);
        Assert.AreEqual(BatteryLevel.Unknown, At(null).Level(20, 10));
        Assert.AreEqual(BatteryLevel.Critical, At(10).Level(20, 10));
        Assert.AreEqual(BatteryLevel.Low, At(20).Level(20, 10));
        Assert.AreEqual(BatteryLevel.Normal, At(21).Level(20, 10));
    }
}
