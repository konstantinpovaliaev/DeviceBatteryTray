using System.Reflection;
using System.Runtime.InteropServices;

namespace DeviceBatteryTray.Tests;

/// <summary>
/// Security guard: the app must not be able to talk to the network or start other programs.
/// Fails the build's test run if a networking/process assembly or a networking DLL import appears.
/// </summary>
[TestClass]
public sealed class NoNetworkGuardTests
{
    private static readonly Assembly App = typeof(DeviceBatteryTray.Battery.BatteryPoller).Assembly;

    private static readonly string[] ForbiddenAssemblyPrefixes =
    [
        "System.Net", // Http, Sockets, WebClient, NetworkInformation, ...
        "System.Diagnostics.Process",
        "System.ServiceModel",
    ];

    private static readonly HashSet<string> AllowedNativeLibraries = new(StringComparer.OrdinalIgnoreCase)
    {
        "cfgmgr32.dll", "hid.dll", "kernel32.dll", "user32.dll", "shell32.dll",
    };

    [TestMethod]
    public void ReferencesNoNetworkOrProcessAssemblies()
    {
        string[] offending = App.GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .Where(name => ForbiddenAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        Assert.AreEqual(0, offending.Length, "forbidden references: " + string.Join(", ", offending));
    }

    [TestMethod]
    public void ImportsOnlyAllowedNativeLibraries()
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static |
                                 BindingFlags.Instance | BindingFlags.DeclaredOnly;
        var imports = App.GetTypes()
            .SelectMany(t => t.GetMethods(all))
            .Where(m => (m.Attributes & MethodAttributes.PinvokeImpl) != 0)
            .Select(m => (Method: m, Import: m.GetCustomAttribute<DllImportAttribute>()))
            .ToList();

        Assert.IsTrue(imports.Count > 0, "the scan should see the app's P/Invokes");
        foreach (var (method, import) in imports)
        {
            Assert.IsNotNull(import, method.Name);
            string library = import.Value.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? import.Value : import.Value + ".dll";
            Assert.IsTrue(AllowedNativeLibraries.Contains(library), $"{method.DeclaringType?.Name}.{method.Name} imports {library}");
        }
    }

    [TestMethod]
    public void NativeLibrariesLoadFromSystem32Only()
    {
        var attribute = App.GetCustomAttribute<DefaultDllImportSearchPathsAttribute>();
        Assert.IsNotNull(attribute);
        Assert.AreEqual(DllImportSearchPath.System32, attribute.Paths);
    }
}
