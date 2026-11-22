using System.Reflection;

namespace DeviceBatteryTray.Services;

/// <summary>
/// Version and source commit baked into the exe at build time (the .NET SDK appends "+&lt;commit&gt;"
/// to the informational version when building from a git checkout), so a binary can be traced
/// back to the exact source it was built from.
/// </summary>
internal static class AppVersion
{
    public static string Informational { get; } =
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

    /// <summary>e.g. "1.0.0 (3f2a9c1)".</summary>
    public static string Display => Format(Informational);

    internal static string Format(string informational)
    {
        int plus = informational.IndexOf('+');
        if (plus < 0)
        {
            return informational;
        }

        string version = informational[..plus];
        string commit = informational[(plus + 1)..];
        return $"{version} ({(commit.Length > 7 ? commit[..7] : commit)})";
    }
}
