using System;
using System.IO;
using System.Linq;
using System.Text;

namespace ThinkPadKbdLight
{
    static class Log
    {
        const long MaxBytes = 256 * 1024;
        static readonly string FilePath = Path.Combine(Settings.Dir, "log.txt");

        public static void Init()
        {
            try
            {
                Directory.CreateDirectory(Settings.Dir);
                // too big: keep the second half
                if (new FileInfo(FilePath).Length > MaxBytes)
                {
                    string[] lines = File.ReadAllLines(FilePath);
                    File.WriteAllLines(FilePath, lines.Skip(lines.Length / 2).ToArray(), Encoding.UTF8);
                }
            }
            catch { }   // file missing or locked, ignore
        }

        public static void Write(string msg)
        {
            try { File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }
    }
}
