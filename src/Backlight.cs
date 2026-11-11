using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ThinkPadKbdLight
{
    // wraps Keyboard_Core.dll, only get, set and max level are used
    class Backlight
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool SetDllDirectory(string path);

        readonly object _ctl;
        readonly MethodInfo _get, _set;

        public string DllPath { get; private set; }
        public int MaxLevel { get; private set; }

        Backlight(string dllPath)
        {
            DllPath = dllPath;
            SetDllDirectory(Path.GetDirectoryName(dllPath));   // so mfc140u & co. get found

            Type t = Assembly.LoadFile(dllPath).GetType("Keyboard_Core.KeyboardControl", true);
            _ctl = Activator.CreateInstance(t);
            _get = t.GetMethod("GetKeyboardBackLightStatus");
            _set = t.GetMethod("SetKeyboardBackLightStatus");
            if (_get == null || _set == null)
                throw new MissingMethodException("DLL looks different than expected, the known methods are missing");

            MaxLevel = 2;
            MethodInfo getLevel = t.GetMethod("GetKeyboardBackLightLevel");
            if (getLevel != null)
            {
                object[] a = { 0 };
                if ((uint)getLevel.Invoke(_ctl, a) == 0 && (int)a[0] >= 1) MaxLevel = (int)a[0];
            }
        }

        // 0 = off, -1 = error
        public int Get()
        {
            try
            {
                object[] a = { 0, null };
                return (uint)_get.Invoke(_ctl, a) == 0 ? (int)a[0] : -1;
            }
            catch { return -1; }
        }

        public bool Set(int level)
        {
            try { return (uint)_set.Invoke(_ctl, new object[] { level, null }) == 0; }
            catch { return false; }
        }

        // messages are for the log, the dialog text comes from Strings
        public static Backlight Open(string configuredPath)
        {
            string dll = FindDll(configuredPath);
            if (dll == null) throw new FileNotFoundException("Keyboard_Core.dll not found");
            try
            {
                return new Backlight(dll);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Could not load " + dll + ": " + ex.Message, ex);
            }
        }

        // config, next to the exe, Lenovo folders (newest version wins)
        static string FindDll(string configured)
        {
            if (!string.IsNullOrEmpty(configured) && File.Exists(configured)) return configured;

            string local = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "Keyboard_Core.dll");
            if (File.Exists(local)) return local;

            string[] roots =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Lenovo\Vantage\Addins\ThinkKeyboardAddin"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Lenovo\VantageService"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Lenovo"),
            };

            string best = null;
            Version bestVersion = null;
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                string[] found;
                try { found = Directory.GetFiles(root, "Keyboard_Core.dll", SearchOption.AllDirectories); }
                catch { continue; }

                foreach (string file in found)
                {
                    Version v = VersionFromPath(file);
                    if (best == null || (v != null && (bestVersion == null || v > bestVersion)))
                    {
                        best = file;
                        bestVersion = v;
                    }
                }
            }
            return best;
        }

        static Version VersionFromPath(string path)
        {
            for (string dir = Path.GetDirectoryName(path); !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
            {
                Version v;
                if (Version.TryParse(Path.GetFileName(dir), out v)) return v;
            }
            return null;
        }
    }
}
