namespace DeviceBatteryTray.Services;

/// <summary>Small local diagnostic log in %LOCALAPPDATA%\DeviceBatteryTray (never sent anywhere).</summary>
internal static class Log
{
    private const long MaxBytes = 256 * 1024;
    private static readonly object Gate = new();

    public static string Directory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeviceBatteryTray");

    public static string FilePath { get; } = Path.Combine(Directory, "log.txt");

    /// <summary>Tests switch logging off so they do not write into the user's real log.</summary>
    internal static bool Enabled { get; set; } = true;

    public static void Write(string message)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(Directory);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                {
                    File.Move(FilePath, FilePath + ".old", overwrite: true);
                }

                File.AppendAllText(FilePath, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Logging must never break the app.
        }
    }
}
