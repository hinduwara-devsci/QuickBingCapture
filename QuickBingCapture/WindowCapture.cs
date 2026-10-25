using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace QuickBingCapture;

internal sealed class WindowInfo
{
    public WindowInfo(IntPtr handle, Rectangle bounds, string title)
    {
        Handle = handle;
        Bounds = bounds;
        Title = title;
    }

    public IntPtr Handle { get; }
    public Rectangle Bounds { get; }
    public string Title { get; }
}

/// <summary>Finds top-level application windows and captures a single window on its own.</summary>
internal static class WindowCapture
{
    private const int DwmwaExtendedFrameBounds = 9;
    private const int DwmwaCloaked = 14;
    private const uint PwRenderFullContent = 0x00000002;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>Visible, non-minimized app windows, front-most first.</summary>
    public static List<WindowInfo> GetCapturableWindows()
    {
        var result = new List<WindowInfo>();
        EnumWindows((hWnd, _) =>
        {
            try
            {
                WindowInfo? info = Describe(hWnd);
                if (info != null) result.Add(info);
            }
            catch
            {
                // ignore windows we cannot inspect
            }
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static WindowInfo? Describe(IntPtr hWnd)
    {
        if (!IsWindowVisible(hWnd) || IsIconic(hWnd)) return null;

        // Skip invisible "cloaked" windows (other virtual desktops, hidden UWP frames).
        if (DwmGetWindowAttribute(hWnd, DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return null;

        int exStyle = GetWindowLong(hWnd, GwlExStyle);
        if ((exStyle & (WsExToolWindow | WsExNoActivate)) != 0) return null;

        var cls = new StringBuilder(256);
        GetClassName(hWnd, cls, cls.Capacity);
        string className = cls.ToString();
        if (className == "Progman" || className == "WorkerW" ||
            className == "Shell_TrayWnd" || className == "Shell_SecondaryTrayWnd") return null;

        int len = GetWindowTextLength(hWnd);
        if (len <= 0) return null;
        var text = new StringBuilder(len + 1);
        GetWindowText(hWnd, text, text.Capacity);
        string title = text.ToString();
        if (string.IsNullOrWhiteSpace(title)) return null;

        // The "extended frame" is the visible window without the invisible resize border/shadow.
        if (DwmGetWindowAttribute(hWnd, DwmwaExtendedFrameBounds, out Rect r, Marshal.SizeOf(typeof(Rect))) != 0 &&
            !GetWindowRect(hWnd, out r)) return null;

        Rectangle bounds = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
        if (bounds.Width < 20 || bounds.Height < 20) return null;
        return new WindowInfo(hWnd, bounds, title);
    }

    /// <summary>
    /// Captures one window. Uses PrintWindow so the result contains only that window (even if other
    /// windows overlap it); falls back to a normal screen copy if the app refuses to render that way.
    /// </summary>
    public static Bitmap CaptureWindow(IntPtr hWnd, Rectangle frameBounds)
    {
        Bitmap? printed = TryPrintWindow(hWnd, frameBounds);
        if (printed != null) return printed;

        SetForegroundWindow(hWnd);
        Thread.Sleep(250);
        var bmp = new Bitmap(frameBounds.Width, frameBounds.Height, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(frameBounds.Location, Point.Empty, frameBounds.Size, CopyPixelOperation.SourceCopy);
        }
        return bmp;
    }

    private static Bitmap? TryPrintWindow(IntPtr hWnd, Rectangle frameBounds)
    {
        try
        {
            if (!GetWindowRect(hWnd, out Rect wr)) return null;
            int w = wr.Right - wr.Left;
            int h = wr.Bottom - wr.Top;
            if (w <= 0 || h <= 0) return null;

            using var full = new Bitmap(w, h, PixelFormat.Format24bppRgb);
            bool ok;
            using (Graphics g = Graphics.FromImage(full))
            {
                g.Clear(Color.White);
                IntPtr hdc = g.GetHdc();
                try { ok = PrintWindow(hWnd, hdc, PwRenderFullContent); }
                finally { g.ReleaseHdc(hdc); }
            }

            if (!ok || LooksBlack(full)) return null;

            var wanted = new Rectangle(frameBounds.X - wr.Left, frameBounds.Y - wr.Top, frameBounds.Width, frameBounds.Height);
            Rectangle crop = Rectangle.Intersect(wanted, new Rectangle(0, 0, w, h));
            if (crop.Width < 2 || crop.Height < 2) crop = new Rectangle(0, 0, w, h);
            return full.Clone(crop, PixelFormat.Format24bppRgb);
        }
        catch
        {
            return null;
        }
    }

    // GPU-rendered apps sometimes return a completely black bitmap through PrintWindow.
    private static bool LooksBlack(Bitmap bmp)
    {
        for (int yi = 1; yi <= 8; yi++)
        {
            for (int xi = 1; xi <= 8; xi++)
            {
                Color c = bmp.GetPixel(bmp.Width * xi / 9, bmp.Height * yi / 9);
                if (c.R != 0 || c.G != 0 || c.B != 0) return false;
            }
        }
        return true;
    }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out Rect pvAttribute, int cbAttribute);
}
