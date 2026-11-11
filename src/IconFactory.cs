using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace ThinkPadKbdLight
{
    // one icon per level, drawn once and cached
    class IconFactory : IDisposable
    {
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr hIcon);

        readonly int _maxLevel;
        readonly Dictionary<int, Icon> _cache = new Dictionary<int, Icon>();

        public IconFactory(int maxLevel)
        {
            _maxLevel = Math.Max(1, maxLevel);
        }

        public Icon Get(int level)
        {
            if (level < 0) level = -1;   // all errors share one icon
            Icon icon;
            if (!_cache.TryGetValue(level, out icon))
                _cache[level] = icon = Draw(level);
            return icon;
        }

        public void Dispose()
        {
            // FromHandle doesn't own the handle
            foreach (Icon icon in _cache.Values)
            {
                IntPtr h = icon.Handle;
                icon.Dispose();
                DestroyIcon(h);
            }
            _cache.Clear();
        }

        Icon Draw(int level)
        {
            const int S = 32;
            using (Bitmap bmp = new Bitmap(S, S))
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                Color fill = Color.FromArgb(90, 90, 90);
                if (level > 0)
                {
                    float t = Math.Min(1f, (float)level / _maxLevel);                    fill = Color.FromArgb((int)(150 + 105 * t), (int)(110 + 100 * t), (int)(20 + 30 * t));
                }

                using (GraphicsPath box = RoundedRect(new Rectangle(1, 1, S - 2, S - 2), 7))
                using (Brush fillBrush = new SolidBrush(fill))
                using (Pen outline = new Pen(Color.FromArgb(40, 40, 40), 2f))
                {
                    g.FillPath(fillBrush, box);
                    g.DrawPath(outline, box);
                }

                using (Brush keys = new SolidBrush(Color.FromArgb(level <= 0 ? 170 : 60, 30, 30, 30)))
                    for (int row = 0; row < 2; row++)
                        for (int col = 0; col < 4; col++)
                            g.FillRectangle(keys, 6 + col * 6, 19 + row * 5, 4, 3);

                string text = level < 0 ? "?" : level.ToString();
                using (Font font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (Brush textBrush = new SolidBrush(level <= 0 ? Color.White : Color.FromArgb(30, 30, 30)))
                {
                    float w = g.MeasureString(text, font).Width;
                    g.DrawString(text, font, textBrush, (S - w) / 2f, 3f);
                }

                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
