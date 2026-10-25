using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace QuickBingCapture;

/// <summary>"Start with Windows" through the per-user Run key (no admin rights needed).</summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuickBingCapture";

    private static string Command => "\"" + Application.ExecutablePath + "\" --autostart";

    public static bool IsEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(ValueName) != null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
        else key.DeleteValue(ValueName, false);
    }

    /// <summary>If the app was moved, point the existing startup entry at the new location.</summary>
    public static void RefreshIfEnabled()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key?.GetValue(ValueName) is string current && !string.Equals(current, Command, StringComparison.OrdinalIgnoreCase))
                key.SetValue(ValueName, Command, RegistryValueKind.String);
        }
        catch
        {
            // not critical
        }
    }
}
