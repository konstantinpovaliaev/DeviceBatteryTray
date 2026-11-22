[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace DeviceBatteryTray.Tests;

[TestClass]
public static class TestSetup
{
    /// <summary>Keeps tests from writing into the user's real log.</summary>
    [AssemblyInitialize]
    public static void Initialize(TestContext _) => DeviceBatteryTray.Services.Log.Enabled = false;
}
