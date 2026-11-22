using System.Resources;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// English text lives in the main assembly (Strings.resx); other languages in satellite resources.
[assembly: NeutralResourcesLanguage("en")]

// Native DLLs are loaded from System32 only, never from the application folder or the current
// directory, so a planted hid.dll / cfgmgr32.dll next to the exe cannot be picked up.
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
[assembly: InternalsVisibleTo("DeviceBatteryTray.Tests")]
