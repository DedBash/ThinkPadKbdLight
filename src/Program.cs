// Keeps the ThinkPad keyboard backlight on, via Lenovo's Keyboard_Core.dll
// (Vantage, 32 bit, hence x86). csc from .NET 4 only does C# 5.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ThinkPadKbdLight
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);

        [STAThread]
        static int Main(string[] args)
        {
            bool cli = args.Length > 0;
            Log.Init();
            Settings cfg = Settings.Load();
            Strings.Use(cfg.Language);

            Backlight bl;
            try
            {
                bl = Backlight.Open(cfg.DllPath);
            }
            catch (Exception ex)
            {
                Log.Write("Error: " + ex.Message);
                if (!cli)
                {
                    string text = ex is FileNotFoundException
                        ? Strings.Get("err.dllnotfound", Settings.FilePath)
                        : Strings.Get("err.dllload", (ex.InnerException ?? ex).Message);
                    MessageBox.Show(text, "ThinkPadKbdLight", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return 2;
            }

            cfg.Level = Math.Max(0, Math.Min(bl.MaxLevel, cfg.Level));

            if (cli) return RunCli(args, cfg, bl);

            bool firstInstance;
            using (new Mutex(true, "Local\\ThinkPadKbdLight", out firstInstance))
            {
                if (!firstInstance) return 0;

                SetProcessDPIAware();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                cfg.Save();   // create settings.ini if it's missing
                Log.Write("Started. DLL: " + bl.DllPath + ", max level: " + bl.MaxLevel);
                Application.Run(new TrayApp(cfg, bl));
            }
            return 0;
        }

        static int RunCli(string[] args, Settings cfg, Backlight bl)
        {
            AttachConsole(-1);   // a winexe has no console otherwise
            string cmd = args[0].ToLowerInvariant();

            if (cmd == "--get")
            {
                int cur = bl.Get();
                Console.WriteLine(cur);
                return cur < 0 ? 1 : 0;
            }

            int target = -1;
            if (cmd == "--on") target = cfg.Level > 0 ? cfg.Level : bl.MaxLevel;
            else if (cmd == "--off") target = 0;
            else if (cmd == "--set" && args.Length > 1) int.TryParse(args[1], out target);

            if (target < 0 || target > bl.MaxLevel)
            {
                Console.WriteLine(Strings.Get("cli.usage", bl.MaxLevel));
                return 1;
            }

            bool ok = bl.Set(target);
            Console.WriteLine(Strings.Get(ok ? "cli.set.ok" : "cli.set.fail", target));
            return ok ? 0 : 1;
        }
    }
}
