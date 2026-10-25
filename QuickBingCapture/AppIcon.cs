using System;
using System.Drawing;

namespace QuickBingCapture;

internal static class AppIcon
{
    /// <summary>Loads the embedded multi-size icon at the requested size (falls back to a system icon).</summary>
    public static Icon Load(Size size)
    {
        try
        {
            using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("app.ico");
            if (stream != null) return new Icon(stream, size);
        }
        catch
        {
            // fall through to the default icon
        }
        return SystemIcons.Application;
    }
}
