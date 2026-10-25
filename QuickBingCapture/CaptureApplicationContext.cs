using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickBingCapture;

internal sealed class CaptureApplicationContext : ApplicationContext
{
    private const int HotkeyRegionId = 0x5142;
    private const int HotkeyWindowId = 0x5143;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModShift = 0x0004;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkS = 0x53; // Alt+Shift+S = capture region
    private const uint VkW = 0x57; // Alt+Shift+W = capture one window

    private readonly HotkeyWindow _hotkeyWindow;
    private readonly NotifyIcon _tray;
    private readonly Icon _trayIcon;
    private readonly ToolStripMenuItem _startupItem;
    private bool _capturing;

    public CaptureApplicationContext(bool launchedAtStartup)
    {
        CleanOldUploadPages();
        StartupManager.RefreshIfEnabled();

        _hotkeyWindow = new HotkeyWindow(OnHotkey);
        bool regionOk = RegisterHotKey(_hotkeyWindow.Handle, HotkeyRegionId, ModAlt | ModShift | ModNoRepeat, VkS);
        bool windowOk = RegisterHotKey(_hotkeyWindow.Handle, HotkeyWindowId, ModAlt | ModShift | ModNoRepeat, VkW);

        _startupItem = new ToolStripMenuItem("Start with Windows") { Checked = StartupManager.IsEnabled() };
        _startupItem.Click += (_, _) => ToggleStartup();

        var menu = new ContextMenuStrip();
        menu.Items.Add("Capture region (Alt+Shift+S)", null, (_, _) => StartCapture(CaptureMode.Region));
        menu.Items.Add("Capture window (Alt+Shift+W)", null, (_, _) => StartCapture(CaptureMode.Window));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_startupItem);
        menu.Items.Add("About / help", null, (_, _) => ShowAbout());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        menu.Opening += (_, _) => _startupItem.Checked = StartupManager.IsEnabled();

        _trayIcon = AppIcon.Load(SystemInformation.SmallIconSize);
        _tray = new NotifyIcon
        {
            Icon = _trayIcon,
            Text = "Quick Bing Capture",
            ContextMenuStrip = menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => StartCapture(CaptureMode.Region);

        if (!regionOk || !windowOk)
        {
            string failed = !regionOk && !windowOk ? "Alt+Shift+S and Alt+Shift+W" : (!regionOk ? "Alt+Shift+S" : "Alt+Shift+W");
            MessageBox.Show(
                "Could not register " + failed + ". Another app may be using it.\n\nRight-click the tray icon and use the menu instead.",
                "Quick Bing Capture",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        else if (!launchedAtStartup)
        {
            // Stay silent when started automatically with Windows.
            _tray.ShowBalloonTip(2500, "Quick Bing Capture is ready",
                "Alt+Shift+S: select an area. Alt+Shift+W: capture a window.", ToolTipIcon.Info);
        }
    }

    private void OnHotkey(int id)
    {
        if (id == HotkeyRegionId) StartCapture(CaptureMode.Region);
        else if (id == HotkeyWindowId) StartCapture(CaptureMode.Window);
    }

    private void StartCapture(CaptureMode mode)
    {
        if (_capturing) return;
        _capturing = true;
        try
        {
            Bitmap image;
            using (var overlay = new SelectionOverlay(mode))
            {
                if (overlay.ShowDialog() != DialogResult.OK ||
                    overlay.SelectedBounds.Width < 2 || overlay.SelectedBounds.Height < 2)
                    return;

                image = overlay.SelectedWindow != IntPtr.Zero
                    ? WindowCapture.CaptureWindow(overlay.SelectedWindow, overlay.SelectedBounds)
                    : CaptureScreenRegion(overlay.SelectedBounds);
            }

            using (image)
            {
                // Put a real bitmap on the clipboard so it can also be pasted into other apps.
                Clipboard.SetDataObject((Image)image.Clone(), true, 5, 100);

                try
                {
                    OpenBingWithUpload(image);
                    _tray.ShowBalloonTip(3000, "Screenshot sent to Bing",
                        "Opening Bing Visual Search results.", ToolTipIcon.Info);
                }
                catch
                {
                    // Fallback: open Bing Images; the screenshot is on the clipboard, so click the page and press Ctrl+V.
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://www.bing.com/images",
                        UseShellExecute = true
                    });
                    _tray.ShowBalloonTip(5000, "Screenshot copied",
                        "Click the Bing page and press Ctrl+V.", ToolTipIcon.Info);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Capture failed:\n" + ex.Message, "Quick Bing Capture",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _capturing = false;
        }
    }

    private static Bitmap CaptureScreenRegion(Rectangle bounds)
    {
        var bmp = new Bitmap(bounds.Width, bounds.Height);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
        }
        return bmp;
    }

    private static string UploadFolder => Path.Combine(Path.GetTempPath(), "QuickBingCapture");

    private static void CleanOldUploadPages()
    {
        try
        {
            if (!Directory.Exists(UploadFolder)) return;
            foreach (string old in Directory.GetFiles(UploadFolder, "upload_*.html"))
            {
                try { if (File.GetCreationTimeUtc(old) < DateTime.UtcNow.AddMinutes(-2)) File.Delete(old); }
                catch { /* ignore */ }
            }
        }
        catch
        {
            // not critical
        }
    }

    // The old https://www.bing.com/visualsearch URL redirects to a marketing page that cannot accept
    // pastes. Instead the image is POSTed straight to Bing's upload endpoint through a tiny
    // auto-submitting local HTML form: no window focus, no timing guesses, no SendKeys.
    private static void OpenBingWithUpload(Bitmap image)
    {
        using var ms = new MemoryStream();
        image.Save(ms, ImageFormat.Png);
        string base64 = Convert.ToBase64String(ms.ToArray());

        Directory.CreateDirectory(UploadFolder);
        CleanOldUploadPages();

        string path = Path.Combine(UploadFolder, "upload_" + Guid.NewGuid().ToString("N") + ".html");
        string html =
            "<!doctype html><html><head><meta charset=\"utf-8\"><title>Sending to Bing...</title></head>" +
            "<body style=\"font-family:Segoe UI,sans-serif\">Uploading to Bing Visual Search..." +
            "<form id=\"f\" method=\"POST\" enctype=\"multipart/form-data\" " +
            "action=\"https://www.bing.com/images/search?view=detailv2&amp;iss=sbiupload&amp;FORM=SBIHMP&amp;sbisrc=ImgDropper\">" +
            "<input type=\"hidden\" name=\"imageBin\" value=\"" + base64 + "\"></form>" +
            "<script>document.getElementById('f').submit();</script></body></html>";
        File.WriteAllText(path, html);

        Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });

        // Delete the temporary page shortly after the browser has loaded it.
        var cleanup = new System.Windows.Forms.Timer { Interval = 60000 };
        cleanup.Tick += (_, _) =>
        {
            cleanup.Stop();
            cleanup.Dispose();
            try { File.Delete(path); } catch { /* ignore */ }
        };
        cleanup.Start();
    }

    private void ToggleStartup()
    {
        try
        {
            StartupManager.SetEnabled(!StartupManager.IsEnabled());
        }
        catch (Exception ex)
        {
            MessageBox.Show("Could not change the startup setting:\n" + ex.Message, "Quick Bing Capture",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        _startupItem.Checked = StartupManager.IsEnabled();
    }

    private void ShowAbout()
    {
        using var about = new AboutForm();
        about.ShowDialog();
    }

    protected override void ExitThreadCore()
    {
        UnregisterHotKey(_hotkeyWindow.Handle, HotkeyRegionId);
        UnregisterHotKey(_hotkeyWindow.Handle, HotkeyWindowId);
        _tray.Visible = false;
        _tray.Dispose();
        _trayIcon.Dispose();
        _hotkeyWindow.Dispose();
        base.ExitThreadCore();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private readonly Action<int> _callback;

        public HotkeyWindow(Action<int> callback)
        {
            _callback = callback;
            CreateHandle(new CreateParams { Caption = "QuickBingCaptureHotkeyWindow" });
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey) _callback(m.WParam.ToInt32());
            base.WndProc(ref m);
        }

        public void Dispose() => DestroyHandle();
    }
}
