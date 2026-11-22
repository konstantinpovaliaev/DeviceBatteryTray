using DeviceBatteryTray.Ui;

namespace DeviceBatteryTray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\DeviceBatteryTray.SingleInstance", out bool created);
        if (!created)
        {
            return; // Another instance already owns the tray icons.
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
