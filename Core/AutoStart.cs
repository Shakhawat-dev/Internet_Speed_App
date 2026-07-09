using Microsoft.Win32;

namespace InternetSpeedApp.Core;

/// <summary>Manages the HKCU Run-key entry that starts the app with Windows.</summary>
internal static class AutoStart
{
    private const string AppName = "InternetSpeedApp";
    private const string RunKey  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    internal static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(AppName) is not null;
        }
    }

    internal static void SetEnabled(bool on, string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;
        if (on) key.SetValue(AppName, executablePath);
        else    key.DeleteValue(AppName, throwOnMissingValue: false);
    }
}
