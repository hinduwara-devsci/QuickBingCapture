using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace QuickBingCapture;

internal sealed class AboutForm : Form
{
    public const string DeveloperName = "Heshan Induwara";
    public const string GitHubUrl = "https://github.com/hinduwara-devsci";

    public AboutForm()
    {
        Text = "About Quick Bing Capture";
        Icon = AppIcon.Load(SystemInformation.SmallIconSize);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(500, 470);
        Font = new Font("Segoe UI", 9F);

        string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
        const int textWidth = 410;

        var logo = new PictureBox
        {
            Image = AppIcon.Load(new Size(64, 64)).ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(20, 20),
            Size = new Size(64, 64)
        };

        var title = new Label
        {
            Text = "Quick Bing Capture",
            Font = new Font("Segoe UI Semibold", 15F),
            AutoSize = true,
            Location = new Point(100, 22)
        };
        var versionLabel = new Label
        {
            Text = "Version " + version,
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Location = new Point(102, 58)
        };

        var flow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Location = new Point(20, 100),
            Size = new Size(460, 300)
        };

        flow.Controls.Add(MakeLabel(
            "Shortcuts\r\n" +
            "  Alt+Shift+S  –  select an area (press W to switch to window mode)\r\n" +
            "  Alt+Shift+W  –  click a window to capture that app only\r\n" +
            "  Esc or right-click  –  cancel",
            textWidth, 0));
        flow.Controls.Add(MakeLabel(
            "The capture is copied to your clipboard and sent to Bing Visual Search in your default browser.",
            textWidth, 10));
        flow.Controls.Add(MakeLabel("Developed by " + DeveloperName, textWidth, 10, true));

        var link = new LinkLabel
        {
            Text = "GitHub: github.com/hinduwara-devsci",
            AutoSize = true,
            Margin = new Padding(3, 2, 3, 0)
        };
        link.LinkClicked += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true }); }
            catch { /* no browser available */ }
        };
        flow.Controls.Add(link);

        flow.Controls.Add(MakeLabel(
            "Free and open source (MIT License) – free to use, modify and share.",
            textWidth, 14));
        flow.Controls.Add(MakeLabel(
            "Privacy: this app collects and stores nothing and has no accounts, ads or telemetry. " +
            "When you capture, the image goes only to Microsoft Bing, directly from your browser.",
            textWidth, 6));

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(90, 30),
            Location = new Point(390, 424)
        };

        Controls.Add(logo);
        Controls.Add(title);
        Controls.Add(versionLabel);
        Controls.Add(flow);
        Controls.Add(ok);
        AcceptButton = ok;
        CancelButton = ok;
    }

    private static Label MakeLabel(string text, int width, int topMargin, bool bold = false)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            MaximumSize = new Size(width, 0),
            Margin = new Padding(3, topMargin, 3, 0),
            Font = bold ? new Font("Segoe UI Semibold", 9.5F) : null!
        };
    }
}
