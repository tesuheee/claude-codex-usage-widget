using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Headroom
{
    // Starts Headroom at sign-in via the per-user Run key.
    //
    // Deliberately NOT a Windows service and NOT the HKLM Run key: both need
    // elevation to install, survive uninstall as orphans, and a service cannot
    // draw to the user's desktop anyway. HKCU\...\Run needs no admin rights,
    // is removed with the user profile, and is what Task Manager's Startup tab
    // shows -- so a user who wants it gone can remove it without this app.
    static class StartupRegistration
    {
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "Headroom";

        // Quoting matters: the executable commonly sits under a path containing
        // spaces, and an unquoted Run value is parsed at the first space.
        public static string CommandFor(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath)) return null;
            return "\"" + exePath.Trim('"') + "\"";
        }

        public static bool IsEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    return key.GetValue(ValueName) != null;
                }
            }
            catch (Exception ex)
            {
                DebugLog.Write("startup-error.txt", "read: " + ex);
                return false;
            }
        }

        // Returns the state actually achieved, so the caller can reconcile the
        // setting with reality rather than assuming the write succeeded.
        public static bool Apply(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return false;
                    if (enabled)
                    {
                        string command = CommandFor(Application.ExecutablePath);
                        if (command == null) return false;
                        key.SetValue(ValueName, command, RegistryValueKind.String);
                    }
                    else
                    {
                        if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
                    }
                }
                return IsEnabled() == enabled;
            }
            catch (Exception ex)
            {
                DebugLog.Write("startup-error.txt", (enabled ? "enable: " : "disable: ") + ex);
                return IsEnabled();
            }
        }

        // The registered path goes stale when the portable exe is moved. Called
        // at startup so the entry keeps pointing at wherever the exe now lives.
        public static void RefreshPathIfRegistered()
        {
            try
            {
                if (!IsEnabled()) return;
                string expected = CommandFor(Application.ExecutablePath);
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;
                    string current = key.GetValue(ValueName) as string;
                    if (!string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
                        key.SetValue(ValueName, expected, RegistryValueKind.String);
                }
            }
            catch (Exception ex)
            {
                DebugLog.Write("startup-error.txt", "refresh: " + ex);
            }
        }
    }
}
