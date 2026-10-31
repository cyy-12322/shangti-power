using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace ShangtiPower
{
    /// <summary>主窗口：显示剩余电量，右上角关闭即退出。</summary>
    public class MainForm : RoundedForm
    {
        private AppConfig _cfg;
        private PowerResult _result;
        private string _error;
        private bool _loading;
        private DateTime _queriedAt;
        private double _animRatio;
        private double _targetRatio;
        private ArcGauge _gauge;
        private AccentButton _btnRefresh;
        private AccentButton _btnSettings;
        private System.Windows.Forms.Timer _animTimer;
        private int _pulse;
        private Rectangle _closeRect;
        private Rectangle _minRect;
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
            _btnSettings.Glyph = null;
            _btnSettings.ColorTop = Color.FromArgb(58, 64, 84);
            _btnSettings.ColorBottom = Color.FromArgb(42, 47, 63);
            _btnSettings.ForeColor = Theme.TextSecondary;
            _btnSettings.Font = Theme.F(13f, FontStyle.Bold);
            _btnSettings.SetBounds(300, 510, 92, 46);
            _btnSettings.Click += delegate { OpenSettings(); };
            Controls.Add(_btnSettings);

            _animTimer = new System.Windows.Forms.Timer();
            _animTimer.Interval = 16;
            _animTimer.Tick += delegate { OnAnimTick(); };
            _animTimer.Start();

            if (Program.IsAutoStartEnabled() != _cfg.AutoStart) Program.SetAutoStart(_cfg.AutoStart);

            Shown += delegate { RefreshPower(); };
            FormClosed += delegate { Application.Exit(); };
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

        // ------------------------------------------------------------ 设置
        private void OpenSettings()
        {
            using (SettingsForm f = new SettingsForm(_cfg))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    _cfg = f.Config;
                    RefreshPower();
                }
            }
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

            // 标题
            Theme.Bolt(g, new RectangleF(24, 14, 14, 22), Theme.Green);
            Theme.Text(g, "上体电量", Theme.F(15f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(46, 12, 200, 26), StringAlignment.Near);

            // 房间
            string room = _cfg == null ? "" : TrimRoom(_cfg.BuildDisplayName());
            Theme.TextCenter(g, room, Theme.F(12f, FontStyle.Regular), Theme.TextSecondary,
                new RectangleF(20, 52, ClientSize.Width - 40, 22));

            // 右上按钮
            int by = 10;
            _minRect = new Rectangle(ClientSize.Width - 78, by, 30, 30);
            _closeRect = new Rectangle(ClientSize.Width - 44, by, 30, 30);
            if (_hoverMin) Theme.FillRound(g, _minRect, 8f, Color.FromArgb(40, 255, 255, 255));
            if (_hoverClose) Theme.FillRound(g, _closeRect, 8f, Color.FromArgb(200, 198, 48, 62));
            Theme.TextCenter(g, "—", Theme.F(14f, FontStyle.Bold),
                _hoverMin ? Theme.TextPrimary : Theme.TextSecondary, _minRect);
            Theme.TextCenter(g, "✕", Theme.F(13f, FontStyle.Bold),
                _hoverClose ? Color.White : Theme.TextSecondary, _closeRect);

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
            base.OnMouseClick(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
        }
    }
}
