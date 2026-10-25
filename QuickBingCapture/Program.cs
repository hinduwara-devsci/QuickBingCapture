using System;
using System.Threading;
using System.Windows.Forms;

namespace QuickBingCapture;

internal static class Program
{
    // Also used by the installer (AppMutex) so it can detect a running copy.
    internal const string MutexName = "QuickBingCaptureSingleInstanceMutex";

    [STAThread]
    private static void Main(string[] args)
    {
        // Only one tray instance at a time (important now that it also starts with Windows).
        using var mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew) return;

        bool autostart = false;
        foreach (string a in args)
        {
            if (string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase)) autostart = true;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var app = new CaptureApplicationContext(autostart);
        Application.Run(app);
    }
}
