using System;
using Microsoft.Win32;
using System.Windows.Forms;

namespace SnapMove
{
    internal static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "SnapMove";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    object value = key.GetValue(ValueName);
                    return value != null && string.Equals(value.ToString(), Command, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return;
                    if (enabled)
                        key.SetValue(ValueName, Command);
                    else if (key.GetValue(ValueName) != null)
                        key.DeleteValue(ValueName, false);
                }
            }
            catch
            {
            }
        }

        private static string Command
        {
            get { return "\"" + Application.ExecutablePath + "\""; }
        }
    }
}
