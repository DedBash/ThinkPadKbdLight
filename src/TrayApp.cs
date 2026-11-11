using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ThinkPadKbdLight
{
    // never shown, only here for its handle (WM_POWERBROADCAST)
    class TrayApp : Form
    {
        const int WM_POWERBROADCAST = 0x0218;
        const int PBT_APMRESUMEAUTOMATIC = 0x0012;
        const int PBT_APMRESUMESUSPEND = 0x0007;
        const int PBT_POWERSETTINGCHANGE = 0x8013;
        static readonly Guid GUID_LIDSWITCH_STATE_CHANGE = new Guid("BA3E0F4D-B817-4094-A2D1-D56379E6A0F3");
        static readonly Guid GUID_CONSOLE_DISPLAY_STATE = new Guid("6FE69556-704A-47A0-8F24-C28D936FDA47");

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct POWERBROADCAST_SETTING { public Guid PowerSetting; public uint DataLength; public byte Data; }

        [DllImport("user32.dll")] static extern IntPtr RegisterPowerSettingNotification(IntPtr hwnd, ref Guid guid, int flags);
        [DllImport("user32.dll")] static extern bool UnregisterPowerSettingNotification(IntPtr handle);

        // EC isn't ready right after resume, so retry for ~30 s
        static readonly int[] RetryDelaysMs = { 400, 1500, 4000, 8000, 15000 };

        readonly Settings _cfg;
        readonly Backlight _bl;
        readonly IconFactory _icons;
        readonly NotifyIcon _tray = new NotifyIcon();
        readonly Timer _poll = new Timer();
        readonly Timer _retry = new Timer();
        readonly DateTime _startedAt = DateTime.Now;

        ToolStripMenuItem[] _levelItems, _langItems;   // [0] = auto
        ToolStripMenuItem _miTitle, _miRestore, _miEnforce, _miAdopt, _miAutostart, _miLanguage, _miOpenLog, _miExit;
        IntPtr _hLid, _hDisplay;
        int _shownLevel = -2;                        // -2 = nothing shown yet
        int _retryStep;
        DateTime _noAdoptUntil = DateTime.MinValue;  // ignore Fn+Space changes until then

        public TrayApp(Settings cfg, Backlight bl)
        {
            _cfg = cfg;
            _bl = bl;
            _icons = new IconFactory(bl.MaxLevel);

            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Opacity = 0;
            Text = "ThinkPadKbdLight";

            _tray.ContextMenuStrip = BuildMenu();
            _tray.MouseClick += delegate(object s, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left)
                    SetLevel((Math.Max(0, _bl.Get()) + 1) % (_bl.MaxLevel + 1), "click");
            };
            ShowLevel(_bl.Get());
            _tray.Visible = true;

            _poll.Interval = 2000;
            _poll.Tick += delegate { Poll(); };
            _poll.Start();
            _retry.Tick += delegate { RetryTick(); };

            SystemEvents.SessionSwitch += OnSessionSwitch;
            CreateHandle();   // never shown, so create the handle by hand
            Restore("startup");
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Guid lid = GUID_LIDSWITCH_STATE_CHANGE, display = GUID_CONSOLE_DISPLAY_STATE;
            _hLid = RegisterPowerSettingNotification(Handle, ref lid, 0);
            _hDisplay = RegisterPowerSettingNotification(Handle, ref display, 0);
        }

        // texts are set in Localize()
        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();

            _miTitle = new ToolStripMenuItem();
            _miTitle.Enabled = false;
            _miTitle.Font = new Font(menu.Font, FontStyle.Bold);
            menu.Items.Add(_miTitle);

            _levelItems = new ToolStripMenuItem[_bl.MaxLevel + 1];
            for (int i = 0; i <= _bl.MaxLevel; i++)
            {
                int level = i;
                _levelItems[i] = AddItem(menu, delegate { SetLevel(level, "menu"); });
            }

            menu.Items.Add(new ToolStripSeparator());
            _miRestore = AddCheckItem(menu, delegate { _cfg.RestoreOnWake = _miRestore.Checked; _cfg.Save(); });
            _miEnforce = AddCheckItem(menu, delegate { _cfg.Enforce = _miEnforce.Checked; _cfg.Save(); RefreshMenu(); ShowLevel(_shownLevel); });
            _miAdopt = AddCheckItem(menu, delegate { _cfg.AdoptFnSpace = _miAdopt.Checked; _cfg.Save(); });
            _miAutostart = AddCheckItem(menu, delegate
            {
                try { Autostart.Set(_miAutostart.Checked); }
                catch (Exception ex) { Log.Write("Could not change autostart: " + ex.Message); }
            });

            menu.Items.Add(new ToolStripSeparator());
            _miLanguage = new ToolStripMenuItem();
            _langItems = new ToolStripMenuItem[Strings.Codes.Length + 1];
            for (int i = 0; i < _langItems.Length; i++)
            {
                string setting = i == 0 ? "auto" : Strings.Codes[i - 1];
                _langItems[i] = new ToolStripMenuItem(i == 0 ? "" : Strings.Names[i - 1]);
                _langItems[i].Click += delegate { SetLanguage(setting); };
                _miLanguage.DropDownItems.Add(_langItems[i]);
            }
            menu.Items.Add(_miLanguage);

            _miOpenLog = AddItem(menu, delegate { try { Process.Start("explorer.exe", Settings.Dir); } catch { } });
            _miExit = AddItem(menu, delegate { Close(); });

            menu.Opening += delegate { RefreshMenu(); };
            Localize();
            return menu;
        }

        static ToolStripMenuItem AddItem(ContextMenuStrip menu, EventHandler onClick)
        {
            ToolStripMenuItem item = new ToolStripMenuItem();
            item.Click += onClick;
            menu.Items.Add(item);
            return item;
        }

        static ToolStripMenuItem AddCheckItem(ContextMenuStrip menu, EventHandler onClick)
        {
            ToolStripMenuItem item = AddItem(menu, onClick);
            item.CheckOnClick = true;
            return item;
        }

        void Localize()
        {
            _miTitle.Text = Strings.Get("menu.title");
            for (int i = 0; i < _levelItems.Length; i++) _levelItems[i].Text = LevelText(i);
            _miRestore.Text = Strings.Get("menu.restore");
            _miEnforce.Text = Strings.Get("menu.enforce");
            _miAdopt.Text = Strings.Get("menu.adopt");
            _miAutostart.Text = Strings.Get("menu.autostart");
            _miLanguage.Text = Strings.Get("menu.language");
            _langItems[0].Text = Strings.Get("lang.auto");   // rest are native names
            _miOpenLog.Text = Strings.Get("menu.openlog");
            _miExit.Text = Strings.Get("menu.exit");
        }

        string LevelText(int level)
        {
            if (level == 0) return Strings.Get("level.off");
            string text = Strings.Get("level.n", level);
            if (_bl.MaxLevel > 1 && level == 1) text += " " + Strings.Get("level.low");
            if (_bl.MaxLevel > 1 && level == _bl.MaxLevel) text += " " + Strings.Get("level.high");
            return text;
        }

        void SetLanguage(string setting)
        {
            _cfg.Language = setting;
            _cfg.Save();
            Strings.Use(setting);
            Localize();
            RefreshMenu();
            ShowLevel(_shownLevel);
            Log.Write("Language: " + setting);
        }

        void RefreshMenu()
        {
            for (int i = 0; i < _levelItems.Length; i++) _levelItems[i].Checked = i == _cfg.Level;
            _miRestore.Checked = _cfg.RestoreOnWake;
            _miEnforce.Checked = _cfg.Enforce;
            _miAdopt.Checked = _cfg.AdoptFnSpace;
            _miAdopt.Enabled = !_cfg.Enforce;
            try { _miAutostart.Checked = Autostart.IsEnabled(); } catch { }

            _langItems[0].Checked = Array.IndexOf(Strings.Codes, _cfg.Language) < 0;
            for (int i = 0; i < Strings.Codes.Length; i++) _langItems[i + 1].Checked = _cfg.Language == Strings.Codes[i];
        }

        void ShowLevel(int level)
        {
            _shownLevel = level;
            Icon icon = _icons.Get(level);
            if (!ReferenceEquals(_tray.Icon, icon)) _tray.Icon = icon;

            string text = level < 0 ? Strings.Get("tip.unknown") : level == 0 ? Strings.Get("tip.off") : Strings.Get("tip.level", level);
            if (_cfg.Enforce) text += " " + Strings.Get("tip.enforced");
            if (text.Length > 63) text = text.Substring(0, 63);   // NotifyIcon.Text: max 63
            if (_tray.Text != text) _tray.Text = text;
        }

        void SetLevel(int level, string source)
        {
            _cfg.Level = level;
            _cfg.Save();
            _noAdoptUntil = DateTime.Now.AddSeconds(5);
            bool ok = _bl.Set(level);
            Log.Write("Level " + level + " set (" + source + "): " + (ok ? "ok" : "failed"));
            ShowLevel(_bl.Get());
            RefreshMenu();
        }

        void Restore(string reason)
        {
            if (!_cfg.RestoreOnWake) return;
            if (_retry.Enabled && _retryStep == 0) return;   // resume events come in bursts
            _noAdoptUntil = DateTime.Now.AddSeconds(20);      // that's the firmware, not the user
            _retryStep = 0;
            _retry.Interval = RetryDelaysMs[0];
            _retry.Start();
            Log.Write("Restore: " + reason + " (target level " + _cfg.Level + ")");
        }

        void RetryTick()
        {
            _retry.Stop();
            int before = _bl.Get();
            int after = before;
            if (before != _cfg.Level)
            {
                _bl.Set(_cfg.Level);
                after = _bl.Get();
            }
            Log.Write("  attempt " + (_retryStep + 1) + ": " + (before == _cfg.Level ? "already ok" : before + " -> " + after));
            ShowLevel(after);

            _retryStep++;
            if (after != _cfg.Level && _retryStep < RetryDelaysMs.Length)
            {
                _retry.Interval = RetryDelaysMs[_retryStep];
                _retry.Start();
            }
        }

        void Poll()
        {
            int cur = _bl.Get();
            if (cur < 0) return;
            if (cur != _shownLevel) ShowLevel(cur);
            if (cur == _cfg.Level) return;

            if (_cfg.Enforce)
            {
                if (_bl.Set(_cfg.Level)) ShowLevel(_cfg.Level);
                Log.Write("Enforced: " + cur + " -> " + _cfg.Level);
            }
            else if (_cfg.AdoptFnSpace && cur > 0 && DateTime.Now >= _noAdoptUntil)
            {
                // Fn+Space. Off isn't adopted, else nothing to restore.
                _cfg.Level = cur;
                _cfg.Save();
                RefreshMenu();
                Log.Write("Level " + cur + " picked via Fn+Space, adopted as target");
            }
        }

        void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionUnlock
                || e.Reason == SessionSwitchReason.SessionLogon
                || e.Reason == SessionSwitchReason.ConsoleConnect)
                Restore("session " + e.Reason);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_POWERBROADCAST)
            {
                int evt = m.WParam.ToInt32();
                if (evt == PBT_APMRESUMEAUTOMATIC || evt == PBT_APMRESUMESUSPEND)
                {
                    Restore("resume");
                }
                else if (evt == PBT_POWERSETTINGCHANGE && m.LParam != IntPtr.Zero && (DateTime.Now - _startedAt).TotalSeconds > 3)
                {
                    // registering fires once with the current state, skip that
                    POWERBROADCAST_SETTING s = (POWERBROADCAST_SETTING)Marshal.PtrToStructure(m.LParam, typeof(POWERBROADCAST_SETTING));
                    if (s.PowerSetting == GUID_LIDSWITCH_STATE_CHANGE && s.Data == 1) Restore("lid opened");
                    else if (s.PowerSetting == GUID_CONSOLE_DISPLAY_STATE && s.Data == 1) Restore("display on");
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Log.Write("Exited");
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            UnregisterPowerSettingNotification(_hLid);
            UnregisterPowerSettingNotification(_hDisplay);
            _poll.Dispose();
            _retry.Dispose();
            _tray.Visible = false;
            _tray.Dispose();
            _icons.Dispose();
            base.OnFormClosing(e);
        }
    }
}
