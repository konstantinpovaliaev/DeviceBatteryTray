using DeviceBatteryTray.Battery;

namespace DeviceBatteryTray.Tests;

[TestClass]
public sealed class HidValueMathTests
{
    [TestMethod]
    [DataRow(100u, 0, 100, 100)]
    [DataRow(50u, 0, 100, 50)]
    [DataRow(0u, 0, 100, 0)]
    [DataRow(128u, 0, 255, 50)]
    [DataRow(255u, 0, 255, 100)]
    public void ToPercent_ScalesByLogicalRange(uint raw, int min, int max, int expected) =>
        Assert.AreEqual(expected, HidValueMath.ToPercent(raw, min, max));

    [TestMethod]
    public void ToPercent_RejectsValueOutsideRange() => Assert.IsNull(HidValueMath.ToPercent(101, 0, 100));

    [TestMethod]
    public void ToPercent_WithoutRange_AcceptsOnlyPercentLikeValues()
    {
        Assert.AreEqual(42, HidValueMath.ToPercent(42, 0, 0));
        Assert.IsNull(HidValueMath.ToPercent(150, 0, 0));
    }

    [TestMethod]
    public void IsBatteryUsage_KnowsStandardUsages()
    {
        Assert.IsTrue(HidValueMath.IsBatteryUsage(0x06, 0x20));
        Assert.IsTrue(HidValueMath.IsBatteryUsage(0x85, 0x65));
        Assert.IsFalse(HidValueMath.IsBatteryUsage(0x06, 0x24));
        Assert.IsFalse(HidValueMath.IsBatteryUsage(0x0C, 0x20));
    }

    [TestMethod]
    public void IsExpectedReply_AcceptsOnlyRequestedReportId()
    {
        // Replies observed from the Keychron K2 in rotation: only report 3 carries the battery level.
        Assert.IsTrue(HidValueMath.IsExpectedReply([0x03, 0x64], 0x03));
        Assert.IsFalse(HidValueMath.IsExpectedReply([0x2C, 0x26], 0x03));
        Assert.IsFalse(HidValueMath.IsExpectedReply([0x51, 0x66], 0x03));
        Assert.IsFalse(HidValueMath.IsExpectedReply([0x03], 0x03));
    }
}
