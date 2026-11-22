using DeviceBatteryTray.Battery;

namespace DeviceBatteryTray.Tests;

[TestClass]
public sealed class PollPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan Active = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(10);

    private static HidPollInput Input(
        bool connected = true,
        bool reconnected = false,
        DateTimeOffset? success = null,
        DateTimeOffset? attempt = null,
        TimeSpan? sinceInput = null,
        bool manual = false) =>
        new(Now, connected, reconnected, success, attempt, Interval, sinceInput ?? Active, manual);

    [TestMethod]
    public void NeverReadsDisconnectedDevice()
    {
        Assert.IsFalse(PollPolicy.ShouldReadHid(Input(connected: false)));
        Assert.IsFalse(PollPolicy.ShouldReadHid(Input(connected: false, manual: true)));
        Assert.IsFalse(PollPolicy.ShouldReadHid(Input(connected: false, reconnected: true)));
    }

    [TestMethod]
    public void ReadsRightAfterReconnect_EvenWhenRecentlyRead() =>
        Assert.IsTrue(PollPolicy.ShouldReadHid(Input(reconnected: true, success: Now.AddMinutes(-3), attempt: Now.AddMinutes(-3), sinceInput: Idle)));

    [TestMethod]
    public void DoesNotReadWhileUserIsIdle()
    {
        Assert.IsFalse(PollPolicy.ShouldReadHid(Input(sinceInput: Idle)));
        Assert.IsFalse(PollPolicy.ShouldReadHid(Input(sinceInput: Idle, manual: true)));
    }

    [TestMethod]
    public void FirstReadHappensWhenUserIsActive() => Assert.IsTrue(PollPolicy.ShouldReadHid(Input()));

    [TestMethod]
    public void RespectsInterval()
    {
        Assert.IsFalse(PollPolicy.ShouldReadHid(Input(success: Now.AddMinutes(-29), attempt: Now.AddMinutes(-29))));
        Assert.IsTrue(PollPolicy.ShouldReadHid(Input(success: Now.AddMinutes(-30), attempt: Now.AddMinutes(-30))));
    }

    [TestMethod]
    public void ManualRefreshIgnoresIntervalButNotMinimumGap()
    {
        Assert.IsTrue(PollPolicy.ShouldReadHid(Input(success: Now.AddMinutes(-5), attempt: Now.AddMinutes(-5), manual: true)));
        Assert.IsFalse(PollPolicy.ShouldReadHid(Input(success: Now.AddSeconds(-20), attempt: Now.AddSeconds(-20), manual: true)));
    }

    [TestMethod]
    public void BacksOffAfterFailedAttempt()
    {
        Assert.IsFalse(PollPolicy.ShouldReadHid(Input(attempt: Now.AddMinutes(-2))));
        Assert.IsFalse(PollPolicy.ShouldReadHid(Input(success: Now.AddMinutes(-60), attempt: Now.AddMinutes(-4), manual: true)));
        Assert.IsTrue(PollPolicy.ShouldReadHid(Input(attempt: Now.AddMinutes(-5))));
    }
}
