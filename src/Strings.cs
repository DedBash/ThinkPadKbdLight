using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ThinkPadKbdLight
{
    // UI texts. lang/*.ini is embedded, lang\*.ini next to the exe is read on top.
    static class Strings
    {
        const string Fallback = "en";

        internal static readonly Dictionary<string, Dictionary<string, string>> Langs = new Dictionary<string, Dictionary<string, string>>();
        static readonly Dictionary<string, string> En;
        static Dictionary<string, string> _cur;

        public static string[] Codes { get; private set; }   // en first
        public static string[] Names { get; private set; }

        static Strings()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string res in asm.GetManifestResourceNames())
            {
                if (!res.StartsWith("lang.") || !res.EndsWith(".ini")) continue;
                using (StreamReader r = new StreamReader(asm.GetManifestResourceStream(res), Encoding.UTF8))
                    Add(res.Substring(5, res.Length - 9), r.ReadToEnd());
            }

            string dir = Path.Combine(Path.GetDirectoryName(asm.Location), "lang");
            if (Directory.Exists(dir))
                foreach (string file in Directory.GetFiles(dir, "*.ini"))
                {
                    try { Add(Path.GetFileNameWithoutExtension(file), File.ReadAllText(file, Encoding.UTF8)); }
                    catch (Exception ex) { Log.Write("Could not read " + file + ": " + ex.Message); }
                }

            if (!Langs.ContainsKey(Fallback)) Langs[Fallback] = new Dictionary<string, string>();
            En = _cur = Langs[Fallback];
            Codes = Langs.Keys.OrderBy(c => c == Fallback ? "" : c).ToArray();
            Names = Codes.Select(c => Langs[c].ContainsKey("name") ? Langs[c]["name"] : c).ToArray();
        }

        static void Add(string code, string text)
        {
            code = code.ToLowerInvariant();
            Dictionary<string, string> t;
            if (!Langs.TryGetValue(code, out t)) Langs[code] = t = new Dictionary<string, string>();

            foreach (string raw in text.Split('\n'))
            {
                string line = raw.Trim();
                int eq = line.IndexOf('=');
                if (eq < 1 || line.StartsWith("#")) continue;
                t[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
            }
        }

        // auto = Windows display language
        public static void Use(string setting)
        {
            string code = Langs.ContainsKey(setting) ? setting : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            _cur = Langs.ContainsKey(code) ? Langs[code] : En;
        }

        // missing -> English -> key
        public static string Get(string key, params object[] args)
        {
            string s;
            if (!_cur.TryGetValue(key, out s) && !En.TryGetValue(key, out s)) return key;
            return args.Length == 0 ? s : string.Format(s, args);
        }
    }
}
