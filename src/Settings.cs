using System;
using System.IO;
using System.Text;

namespace ThinkPadKbdLight
{
    class Settings
    {
        public int Level = 2;             // 0 = off
        public bool RestoreOnWake = true;
        public bool Enforce = false;      // also against Fn+Space
        public bool AdoptFnSpace = true;
        public string DllPath = "";       // empty = auto-detect
        public string Language = "auto";  // auto or a file name from lang/

        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ThinkPadKbdLight");
        public static readonly string FilePath = Path.Combine(Dir, "settings.ini");

        public static Settings Load()
        {
            Settings s = new Settings();
            if (!File.Exists(FilePath)) return s;
            try
            {
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    int eq = line.IndexOf('=');
                    if (eq < 1 || line.StartsWith("#")) continue;

                    string key = line.Substring(0, eq).Trim();
                    string val = line.Substring(eq + 1).Trim();
                    int n;
                    switch (key)
                    {
                        case "Level": if (int.TryParse(val, out n)) s.Level = n; break;
                        case "RestoreOnWake": s.RestoreOnWake = IsTrue(val); break;
                        case "Enforce": s.Enforce = IsTrue(val); break;
                        case "AdoptFnSpace": s.AdoptFnSpace = IsTrue(val); break;
                        case "DllPath": s.DllPath = val; break;
                        case "Language": s.Language = val.ToLowerInvariant(); break;
                    }
                }
            }
            catch { }   // broken ini -> defaults
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllLines(FilePath, new[]
                {
                    "# 1 = on, 0 = off",
                    "Level=" + Level,
                    "RestoreOnWake=" + (RestoreOnWake ? 1 : 0),
                    "Enforce=" + (Enforce ? 1 : 0),
                    "AdoptFnSpace=" + (AdoptFnSpace ? 1 : 0),
                    "DllPath=" + DllPath,
                    "Language=" + Language,
                }, Encoding.UTF8);
            }
            catch { }
        }

        static bool IsTrue(string v)
        {
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
