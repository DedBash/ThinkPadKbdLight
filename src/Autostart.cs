using System.Windows.Forms;
using Microsoft.Win32;

namespace ThinkPadKbdLight
{
    // HKCU\...\Run, no admin needed
    static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "ThinkPadKbdLight";

        public static bool IsEnabled()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(Name) != null;
        }

        public static void Set(bool enable)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enable) k.SetValue(Name, "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(Name, false);
            }
        }
    }
}
