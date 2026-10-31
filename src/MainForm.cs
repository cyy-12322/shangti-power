using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace ShangtiPower
{
    /// <summary>主窗口：主页面（电量）+ 设置页面（同一窗口内切换，不弹第二个窗口）。</summary>
    public class MainForm : RoundedForm
    {
        private const int PageMain = 0;
        private const int PageSettings = 1;

        private AppConfig _cfg;
        private PowerResult _result;
        private string _error;
        private bool _loading;
        private DateTime _queriedAt;
        private double _animRatio;
        private double _targetRatio;
        private int _pulse;
        private int _page = PageMain;

        private ArcGauge _gauge;
        private AccentButton _btnRefresh;
        private AccentButton _btnSettings;

        private AccentButton _btnRepick;
        private DarkDropdown _threshold;
        private ToggleSwitch _autoStart;
        private AccentButton _btnSave;
        private AccentButton _btnBack;

        private System.Windows.Forms.Timer _animTimer;
        private Rectangle _closeRect;
        private Rectangle _minRect;
        private Rectangle _linkRect;
        private bool _hoverClose, _hoverMin;

        public MainForm(AppConfig cfg)
        {
            _cfg = cfg;
            Text = Program.AppName;
            ClientSize = new Size(420, 580);
            CornerRadius = 20f;
            BackColor = Theme.BgBottom;
            ShowInTaskbar = true;
            KeyPreview = true;

            BuildMainPage();
            BuildSettingsPage();

            _animTimer = new System.Windows.Forms.Timer();
            _animTimer.Interval = 16;
            _animTimer.Tick += delegate { OnAnimTick(); };
            _animTimer.Start();

            if (Program.IsAutoStartEnabled() != _cfg.AutoStart) Program.SetAutoStart(_cfg.AutoStart);

            Shown += delegate { RefreshPower(); };
            FormClosed += delegate { Application.Exit(); };
            ShowPage(PageMain);
        }

        // ------------------------------------------------------------ 主页面
        private void BuildMainPage()
        {
            _gauge = new ArcGauge();
            _gauge.SetBounds(60, 92, 300, 300);
            Controls.Add(_gauge);

            _btnRefresh = new AccentButton();
            _btnRefresh.Text = "刷新电量";
            _btnRefresh.SetBounds(28, 510, 264, 46);
            _btnRefresh.Click += delegate { RefreshPower(); };
            Controls.Add(_btnRefresh);

            _btnSettings = new AccentButton();
            _btnSettings.Text = "设置";
            _btnSettings.ColorTop = Color.FromArgb(58, 64, 84);
            _btnSettings.ColorBottom = Color.FromArgb(42, 47, 63);
            _btnSettings.ForeColor = Theme.TextSecondary;
            _btnSettings.Font = Theme.F(13f, FontStyle.Bold);
            _btnSettings.SetBounds(300, 510, 92, 46);
            _btnSettings.Click += delegate { ShowPage(PageSettings); };
            Controls.Add(_btnSettings);
        }

        // ------------------------------------------------------------ 设置页面
        private void BuildSettingsPage()
        {
            _btnRepick = new AccentButton();
            _btnRepick.Text = "重新选择房间";
            _btnRepick.ColorTop = Color.FromArgb(58, 64, 84);
            _btnRepick.ColorBottom = Color.FromArgb(42, 47, 63);
            _btnRepick.ForeColor = Theme.TextSecondary;
            _btnRepick.Font = Theme.F(12.5f, FontStyle.Bold);
            _btnRepick.SetBounds(24, 152, 180, 40);
            _btnRepick.Click += delegate { Repick(); };
            Controls.Add(_btnRepick);

            _threshold = new DarkDropdown();
            _threshold.Placeholder = "选择阈值";
            _threshold.Font = Theme.F(13f, FontStyle.Regular);
            _threshold.SetBounds(24, 248, 160, 40);
            double[] options = new double[] { 10, 15, 20, 30, 50 };
            for (int i = 0; i < options.Length; i++)
                _threshold.AddItem(options[i].ToString("0") + " 度", options[i]);
            Controls.Add(_threshold);

            _autoStart = new ToggleSwitch();
            _autoStart.SetBounds(24, 340, 46, 26);
            Controls.Add(_autoStart);

            _btnSave = new AccentButton();
            _btnSave.Text = "保存";
            _btnSave.SetBounds(24, 510, 240, 46);
            _btnSave.Click += delegate { SaveSettings(); };
            Controls.Add(_btnSave);

            _btnBack = new AccentButton();
            _btnBack.Text = "返回";
            _btnBack.ColorTop = Color.FromArgb(58, 64, 84);
            _btnBack.ColorBottom = Color.FromArgb(42, 47, 63);
            _btnBack.ForeColor = Theme.TextSecondary;
            _btnBack.Font = Theme.F(13f, FontStyle.Bold);
            _btnBack.SetBounds(276, 510, 120, 46);
            _btnBack.Click += delegate { ShowPage(PageMain); };
            Controls.Add(_btnBack);
        }

        private void ShowPage(int page)
        {
            _page = page;
            bool main = page == PageMain;
            Theme.SetVisible(_gauge, main);
            Theme.SetVisible(_btnRefresh, main);
            Theme.SetVisible(_btnSettings, main);
            Theme.SetVisible(_btnRepick, !main);
            Theme.SetVisible(_threshold, !main);
            Theme.SetVisible(_autoStart, !main);
            Theme.SetVisible(_btnSave, !main);
            Theme.SetVisible(_btnBack, !main);
            if (!main) LoadSettingsIntoControls();
            Invalidate(true);
        }

        private void LoadSettingsIntoControls()
        {
            if (_threshold.SelectedIndex < 0 || _threshold.ItemCount == 0) { }
            _threshold.SelectByTag(_cfg.LowThreshold);
            _autoStart.Checked = _cfg.AutoStart;
        }

        private void Repick()
        {
            using (SetupForm f = new SetupForm(_cfg))
            {
                if (f.ShowDialog(this) == DialogResult.OK && f.Result != null)
                {
                    _cfg = f.Result;
                    RefreshPower();
                }
            }
            Invalidate();
        }

        private void SaveSettings()
        {
            object tag = _threshold.SelectedTag;
            if (tag != null) _cfg.LowThreshold = Convert.ToDouble(tag);
            _cfg.AutoStart = _autoStart.Checked;
            _cfg.DisplayName = _cfg.BuildDisplayName();
            try { _cfg.Save(); } catch (Exception ex) { Program.Log("保存设置失败: " + ex.Message); }
            try { Program.SetAutoStart(_cfg.AutoStart); } catch (Exception ex) { Program.Log("设置自启失败: " + ex.Message); }
            ShowPage(PageMain);
            RefreshPower();
        }

        // ------------------------------------------------------------ 查询
        private void RefreshPower()
        {
            if (_loading) return;
            _loading = true;
            _error = null;
            _btnRefresh.Text = "查询中…";
            _btnRefresh.Enabled = false;
            Invalidate();
            ThreadPool.QueueUserWorkItem(delegate(object state)
            {
                try
                {
                    ApiClient api = new ApiClient();
                    PowerResult r = api.QueryPower(_cfg, 15000);
                    SafeInvoke(delegate
                    {
                        _result = r;
                        _loading = false;
                        _queriedAt = DateTime.Now;
                        _btnRefresh.Text = "刷新电量";
                        _btnRefresh.Enabled = true;
                        _targetRatio = Math.Max(0.02, Math.Min(1.0, r.Power / 100.0));
                        _gauge.Accent = Theme.StatusColor(r.Power, r.Unit, _cfg.LowThreshold);
                        _gauge.ValueText = r.Power.ToString("0.00");
                        _gauge.UnitText = r.Unit;
                        _gauge.Caption = null;
                        Program.Log("查询成功: " + r.Power + " " + r.Unit + " (" + r.ElapsedMs + "ms)");
                        Invalidate();
                    });
                }
                catch (Exception ex)
                {
                    SafeInvoke(delegate
                    {
                        _loading = false;
                        _error = FriendlyError(ex);
                        _btnRefresh.Text = "重试";
                        _btnRefresh.Enabled = true;
                        Program.Log("查询失败: " + ex.Message);
                        Invalidate();
                    });
                }
            });
        }

        private static string FriendlyError(Exception ex)
        {
            string m = ex.Message;
            if (m.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("超时", StringComparison.OrdinalIgnoreCase) >= 0)
                return "网络超时，学校服务器可能有点慢";
            if (m.IndexOf("基础连接", StringComparison.OrdinalIgnoreCase) >= 0
                || m.IndexOf("连接", StringComparison.OrdinalIgnoreCase) >= 0)
                return "连不上学校服务器，检查网络后重试";
            return m;
        }

        private void SafeInvoke(MethodInvoker action)
        {
            try
            {
                if (IsDisposed) return;
                if (InvokeRequired) BeginInvoke(action);
                else action();
            }
            catch { }
        }

        // ------------------------------------------------------------ 动画
        private void OnAnimTick()
        {
            bool changed = false;
            if (Math.Abs(_targetRatio - _animRatio) > 0.002)
            {
                _animRatio += (_targetRatio - _animRatio) * 0.16;
                _gauge.Ratio = _animRatio;
                changed = true;
            }
            else if (_gauge.Ratio != _targetRatio)
            {
                _animRatio = _targetRatio;
                _gauge.Ratio = _targetRatio;
                changed = true;
            }
            if (_loading)
            {
                _pulse = (_pulse + 4) % 360;
                _gauge.Accent = Theme.Blue;
                changed = true;
            }
            if (changed) Invalidate(true);
        }

        // ------------------------------------------------------------ 绘制
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            PaintWindowFrame(g);
            DrawTitleBar(g);

            if (_page == PageMain) PaintMainPage(g);
            else PaintSettingsPage(g);
        }

        private void DrawTitleBar(Graphics g)
        {
            Theme.Bolt(g, new RectangleF(24, 14, 14, 22), Theme.Green);
            Theme.Text(g, _page == PageMain ? "上体电量" : "设置", Theme.F(15f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(46, 12, 200, 26), StringAlignment.Near);

            int by = 10;
            _minRect = new Rectangle(ClientSize.Width - 78, by, 30, 30);
            _closeRect = new Rectangle(ClientSize.Width - 44, by, 30, 30);
            if (_hoverMin) Theme.FillRound(g, _minRect, 8f, Color.FromArgb(40, 255, 255, 255));
            if (_hoverClose) Theme.FillRound(g, _closeRect, 8f, Color.FromArgb(200, 198, 48, 62));
            Theme.TextCenter(g, "—", Theme.F(14f, FontStyle.Bold),
                _hoverMin ? Theme.TextPrimary : Theme.TextSecondary, _minRect);
            Theme.TextCenter(g, "✕", Theme.F(13f, FontStyle.Bold),
                _hoverClose ? Color.White : Theme.TextSecondary, _closeRect);
        }

        private void PaintMainPage(Graphics g)
        {
            // 房间
            string room = _cfg == null ? "" : TrimRoom(_cfg.BuildDisplayName());
            Theme.TextCenter(g, room, Theme.F(12f, FontStyle.Regular), Theme.TextSecondary,
                new RectangleF(20, 52, ClientSize.Width - 40, 22));

            // 状态胶囊
            if (_result != null && _error == null)
            {
                string status = Theme.StatusText(_result.Power, _result.Unit, _cfg.LowThreshold);
                Color accent = Theme.StatusColor(_result.Power, _result.Unit, _cfg.LowThreshold);
                SizeF textSize = g.MeasureString(status, Theme.F(12f, FontStyle.Bold));
                float w = textSize.Width + 46f;
                RectangleF chip = new RectangleF((ClientSize.Width - w) / 2f, 400, w, 30);
                Theme.FillRound(g, chip, 15f, Color.FromArgb(30, accent));
                Theme.StrokeRound(g, chip, 15f, Color.FromArgb(90, accent), 1f);
                using (SolidBrush dot = new SolidBrush(accent))
                    g.FillEllipse(dot, chip.X + 14, chip.Y + 11.5f, 7, 7);
                Theme.Text(g, status, Theme.F(12f, FontStyle.Bold), accent,
                    new RectangleF(chip.X + 28, chip.Y, chip.Width - 34, chip.Height), StringAlignment.Near);
            }

            // 信息卡
            RectangleF card = new RectangleF(24, 440, ClientSize.Width - 48, 52);
            Theme.FillRound(g, card, 14f, Theme.Card);
            Theme.StrokeRound(g, card, 14f, Theme.CardBorder, 1f);

            if (_error == null)
            {
                string updated = _queriedAt == DateTime.MinValue ? "--" : _queriedAt.ToString("HH:mm:ss");
                string elapsed = _result == null ? "" : ("· " + (_result.ElapsedMs / 1000.0).ToString("0.00") + "s");
                Theme.Text(g, "更新时间", Theme.F(11f, FontStyle.Regular), Theme.TextDim,
                    new RectangleF(card.X + 16, card.Y + 6, 120, 18), StringAlignment.Near);
                Theme.Text(g, updated + " " + elapsed, Theme.F(12f, FontStyle.Bold), Theme.TextPrimary,
                    new RectangleF(card.X + 16, card.Y + 24, 200, 20), StringAlignment.Near);
                Theme.Text(g, "数据来源", Theme.F(11f, FontStyle.Regular), Theme.TextDim,
                    new RectangleF(card.Right - 170, card.Y + 6, 150, 18), StringAlignment.Far);
                Theme.Text(g, "校园安心付", Theme.F(12f, FontStyle.Bold), Theme.TextSecondary,
                    new RectangleF(card.Right - 170, card.Y + 24, 150, 20), StringAlignment.Far);
            }
            else
            {
                Theme.Text(g, "⚠ " + _error, Theme.F(11.5f, FontStyle.Regular), Theme.Red,
                    new RectangleF(card.X + 14, card.Y, card.Width - 28, card.Height), StringAlignment.Near);
            }
        }

        private void PaintSettingsPage(Graphics g)
        {
            Theme.Text(g, "当前房间", Theme.F(11.5f, FontStyle.Bold), Theme.TextDim,
                new RectangleF(24, 96, 200, 20), StringAlignment.Near);
            Theme.Text(g, TrimRoom(_cfg.BuildDisplayName()), Theme.F(12.5f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(24, 118, 372, 24), StringAlignment.Near);

            Theme.Text(g, "低电量提醒阈值", Theme.F(11.5f, FontStyle.Bold), Theme.TextDim,
                new RectangleF(24, 222, 200, 20), StringAlignment.Near);
            Theme.Text(g, "剩余电量低于这个值时，界面会变红提醒", Theme.F(10.5f, FontStyle.Regular), Theme.TextDim,
                new RectangleF(196, 260, 200, 20), StringAlignment.Near);

            Theme.Text(g, "开机自动运行", Theme.F(12.5f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(82, 340, 300, 26), StringAlignment.Near);
            Theme.Text(g, "开机后自动弹窗显示电费", Theme.F(10.5f, FontStyle.Regular), Theme.TextDim,
                new RectangleF(82, 360, 300, 18), StringAlignment.Near);

            Theme.Text(g, "v" + Program.Version + "  ·  开源项目 · 欢迎打赏 ☕",
                Theme.F(10.5f, FontStyle.Regular), Theme.TextDim,
                new RectangleF(24, 430, 372, 20), StringAlignment.Near);
            _linkRect = new Rectangle(24, 452, 372, 20);
            Theme.Text(g, "GitHub 项目主页（点击打开）", Theme.F(10.5f, FontStyle.Underline), Theme.Blue,
                new RectangleF(_linkRect.X, _linkRect.Y, _linkRect.Width, _linkRect.Height), StringAlignment.Near);
        }

        private static string TrimRoom(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("  ", " ");
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hc = _closeRect.Contains(e.Location);
            bool hm = _minRect.Contains(e.Location);
            if (hc != _hoverClose || hm != _hoverMin)
            {
                _hoverClose = hc; _hoverMin = hm;
                Cursor = (hc || hm) ? Cursors.Hand : Cursors.Default;
                Invalidate(new Rectangle(ClientSize.Width - 90, 0, 90, 50));
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverClose = false; _hoverMin = false; Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (_closeRect.Contains(e.Location))
            {
                Program.Log("用户点击关闭，退出程序");
                Application.Exit();
                return;
            }
            if (_minRect.Contains(e.Location))
            {
                WindowState = FormWindowState.Minimized;
                return;
            }
            if (_page == PageSettings && _linkRect.Contains(e.Location))
            {
                try { Process.Start(new ProcessStartInfo(Program.RepoUrl) { UseShellExecute = true }); } catch { }
                return;
            }
            base.OnMouseClick(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
        }
    }
}
