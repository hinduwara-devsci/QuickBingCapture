using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace QuickBingCapture;

internal enum CaptureMode
{
    Region,
    Window
}

internal sealed class SelectionOverlay : Form
{
    private readonly Bitmap _screenImage;
    private readonly Rectangle _virtualBounds;
    private readonly List<WindowInfo> _windows;
    private CaptureMode _mode;
    private WindowInfo? _hover;
    private Point _start;
    private Point _current;
    private bool _dragging;

    /// <summary>Screen-coordinate rectangle chosen by the user.</summary>
    public Rectangle SelectedBounds { get; private set; }

    /// <summary>Non-zero when the user picked a whole window instead of dragging an area.</summary>
    public IntPtr SelectedWindow { get; private set; }

    public SelectionOverlay(CaptureMode mode)
    {
        _mode = mode;

        // Must happen before the overlay is shown so the overlay itself is not in the list.
        _windows = WindowCapture.GetCapturableWindows();

        _virtualBounds = SystemInformation.VirtualScreen;
        _screenImage = new Bitmap(_virtualBounds.Width, _virtualBounds.Height);
        using (var g = Graphics.FromImage(_screenImage))
        {
            g.CopyFromScreen(_virtualBounds.Location, Point.Empty, _virtualBounds.Size,
                CopyPixelOperation.SourceCopy);
        }

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        Bounds = _virtualBounds;
        TopMost = true;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        Cursor = Cursors.Cross;
        KeyPreview = true;
        BackColor = Color.Black;
        Opacity = 1.0;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Activate();
        Focus();
        UpdateHover(Cursor.Position);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.DrawImageUnscaled(_screenImage, 0, 0);

        using var shade = new SolidBrush(Color.FromArgb(105, 0, 0, 0));
        e.Graphics.FillRectangle(shade, ClientRectangle);

        if (_mode == CaptureMode.Region && _dragging)
        {
            Rectangle r = Normalize(_start, _current);
            if (r.Width > 0 && r.Height > 0)
            {
                e.Graphics.DrawImage(_screenImage, r, r, GraphicsUnit.Pixel);
                using var pen = new Pen(Color.DeepSkyBlue, 2);
                e.Graphics.DrawRectangle(pen, r);
                DrawLabel(e.Graphics, $"{r.Width} × {r.Height} px", r);
            }
            return;
        }

        if (_mode == CaptureMode.Window && _hover != null)
        {
            Rectangle r = Rectangle.Intersect(ToLocal(_hover.Bounds), ClientRectangle);
            if (r.Width > 0 && r.Height > 0)
            {
                e.Graphics.DrawImage(_screenImage, r, r, GraphicsUnit.Pixel);
                using var pen = new Pen(Color.DeepSkyBlue, 3);
                e.Graphics.DrawRectangle(pen, r);
                string title = _hover.Title.Length > 60 ? _hover.Title.Substring(0, 60) + "…" : _hover.Title;
                DrawLabel(e.Graphics, $"{title}  •  {_hover.Bounds.Width} × {_hover.Bounds.Height} px", r);
            }
        }

        string instruction = _mode == CaptureMode.Region
            ? "Drag to select an area  •  W: capture a whole window  •  Esc to cancel"
            : "Click a window to capture it  •  W: select an area instead  •  Esc to cancel";
        SizeF size = e.Graphics.MeasureString(instruction, Font);
        e.Graphics.FillRectangle(Brushes.Black, 16, 16, size.Width + 20, size.Height + 12);
        e.Graphics.DrawString(instruction, Font, Brushes.White, 26, 22);
    }

    private void DrawLabel(Graphics g, string text, Rectangle target)
    {
        using var labelBrush = new SolidBrush(Color.FromArgb(220, 20, 20, 20));
        SizeF size = g.MeasureString(text, Font);
        var labelRect = new RectangleF(target.Left, Math.Max(0, target.Top - size.Height - 8), size.Width + 12, size.Height + 6);
        g.FillRectangle(labelBrush, labelRect);
        g.DrawString(text, Font, Brushes.White, labelRect.Left + 6, labelRect.Top + 3);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _mode != CaptureMode.Region) return;
        _start = e.Location;
        _current = e.Location;
        _dragging = true;
        Capture = true;
        Invalidate();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_mode == CaptureMode.Window)
        {
            UpdateHover(new Point(e.X + _virtualBounds.X, e.Y + _virtualBounds.Y));
            return;
        }
        if (!_dragging) return;
        _current = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Right)
        {
            Cancel();
            return;
        }
        if (e.Button != MouseButtons.Left) return;

        if (_mode == CaptureMode.Window)
        {
            if (_hover == null) return;
            SelectedWindow = _hover.Handle;
            SelectedBounds = _hover.Bounds;
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        if (!_dragging) return;
        _current = e.Location;
        _dragging = false;
        Capture = false;
        Rectangle local = Normalize(_start, _current);
        SelectedBounds = new Rectangle(local.X + _virtualBounds.X, local.Y + _virtualBounds.Y, local.Width, local.Height);
        DialogResult = local.Width >= 2 && local.Height >= 2 ? DialogResult.OK : DialogResult.Cancel;
        Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Cancel();
            e.Handled = true;
            return;
        }
        if (e.KeyCode == Keys.W)
        {
            _mode = _mode == CaptureMode.Region ? CaptureMode.Window : CaptureMode.Region;
            _dragging = false;
            Capture = false;
            _hover = null;
            UpdateHover(Cursor.Position);
            Invalidate();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    private void Cancel()
    {
        Capture = false;
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void UpdateHover(Point screenPoint)
    {
        WindowInfo? found = null;
        if (_mode == CaptureMode.Window)
        {
            foreach (WindowInfo w in _windows)
            {
                if (w.Bounds.Contains(screenPoint))
                {
                    found = w;
                    break;
                }
            }
        }
        if (!ReferenceEquals(found, _hover))
        {
            _hover = found;
            Invalidate();
        }
    }

    private Rectangle ToLocal(Rectangle screenRect)
        => new Rectangle(screenRect.X - _virtualBounds.X, screenRect.Y - _virtualBounds.Y, screenRect.Width, screenRect.Height);

    private static Rectangle Normalize(Point a, Point b)
        => Rectangle.FromLTRB(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));

    protected override void Dispose(bool disposing)
    {
        if (disposing) _screenImage.Dispose();
        base.Dispose(disposing);
    }
}
