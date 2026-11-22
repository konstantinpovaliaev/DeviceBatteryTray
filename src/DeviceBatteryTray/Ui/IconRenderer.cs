using System.Drawing.Imaging;
using System.Drawing.Text;
using DeviceBatteryTray.Battery;

namespace DeviceBatteryTray.Ui;

/// <summary>
/// Draws a tray icon in the style of Windows' own system icons: a device pictogram from the
/// Segoe Fluent Icons / MDL2 font, filling the icon, in the taskbar's ink colour. Colour is used
/// only for low and critical charge (muted tones); the percentage itself is shown in the tooltip.
/// </summary>
internal static class IconRenderer
{
    private static readonly string[] GlyphFonts = ["Segoe Fluent Icons", "Segoe MDL2 Assets"];

    /// <summary>Opacity of a device that is asleep or off (its last known level is kept).</summary>
    internal const int AsleepAlpha = 110;

    /// <summary>Returns an HICON the caller must release with DestroyIcon.</summary>
    public static IntPtr Render(DeviceKind kind, BatteryLevel level, bool connected, bool lightTaskbar, int size)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        using (Font font = GlyphFont(size))
        using (var brush = new SolidBrush(Ink(level, connected, lightTaskbar)))
        using (var format = new StringFormat(StringFormat.GenericTypographic))
        {
            g.Clear(Color.Transparent);
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            format.FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
            g.DrawString(Glyph(kind), font, brush, new RectangleF(0, 0, size, size), format);
        }

        return bitmap.GetHicon();
    }

    internal static string Glyph(DeviceKind kind) => kind switch
    {
        DeviceKind.Keyboard => "",
        DeviceKind.Mouse => "",
        DeviceKind.Headphones => "",
        DeviceKind.Speaker => "",
        DeviceKind.Gamepad => "",
        _ => "", // generic devices
    };

    /// <summary>Taskbar ink for normal/unknown levels; muted amber/red for low/critical; faded when asleep.</summary>
    internal static Color Ink(BatteryLevel level, bool connected, bool lightTaskbar)
    {
        Color ink = level switch
        {
            BatteryLevel.Low => lightTaskbar ? Color.FromArgb(0xA8, 0x7A, 0x1E) : Color.FromArgb(0xD6, 0xA8, 0x4E),
            BatteryLevel.Critical => lightTaskbar ? Color.FromArgb(0xB4, 0x43, 0x3A) : Color.FromArgb(0xD9, 0x67, 0x5E),
            _ => lightTaskbar ? Color.FromArgb(0x1F, 0x1F, 0x1F) : Color.White,
        };

        // Low/critical stay fully visible even while the device sleeps: that warning matters most.
        bool warning = level is BatteryLevel.Low or BatteryLevel.Critical;
        return connected || warning ? ink : Color.FromArgb(AsleepAlpha, ink);
    }

    private static Font GlyphFont(float pixels)
    {
        foreach (string name in GlyphFonts)
        {
            var font = new Font(name, pixels, FontStyle.Regular, GraphicsUnit.Pixel);
            if (font.Name == name)
            {
                return font;
            }

            font.Dispose();
        }

        return new Font("Segoe UI Symbol", pixels, FontStyle.Regular, GraphicsUnit.Pixel);
    }
}
