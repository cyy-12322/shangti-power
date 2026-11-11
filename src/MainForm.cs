using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace ShangtiPower
{
    /// <summary>主窗口：主页面（电量）+ 设置页面（同一窗口内切换）。</summary>
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
        private AccentButton _btnPay;

        private AccentButton _btnRepick;
        private DarkDropdown _threshold;
        private ToggleSwitch _autoStart;
        private AccentButton _btnSave;

        private System.Windows.Forms.Timer _animTimer;
        private Rectangle _closeRect;
        private Rectangle _minRect;
        private Rectangle _gearRect;
        private Rectangle _donateRect;
        private Rectangle _backRect;
        private Rectangle _linkRect;
        private bool _hoverClose, _hoverMin, _hoverGear, _hoverDonate, _hoverBack;

        // 页面切换动画（滑动 + 缓出，约 210ms，跟手且有质感）
        private const float PageAnimSeconds = 0.21f;
        private bool _animating;
        private Bitmap _animFrom;
        private Bitmap _animTo;
        private float _animT;
        private int _animDir;

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

            _pageTimer = new System.Windows.Forms.Timer();
            _pageTimer.Interval = 15;
            _pageTimer.Tick += delegate { OnPageAnimTick(); };

            if (Program.IsAutoStartEnabled() != _cfg.AutoStart) Program.SetAutoStart(_cfg.AutoStart);

            Shown += delegate { RefreshPower(); };
            FormClosed += delegate { Application.Exit(); };
            ShowPage(PageMain);
        }

        // ------------------------------------------------------------ 主页面控件
        private void BuildMainPage()
        {
            _gauge = new ArcGauge();
            _gauge.SetBounds(72, 76, 276, 276);
            Controls.Add(_gauge);

            // 底部双按钮：刷新（主）+ 去缴费（次）
            _btnRefresh = new AccentButton();
            _btnRefresh.Text = "刷新电量";
            _btnRefresh.Glyph = "refresh";
            _btnRefresh.Radius = 25f;
            _btnRefresh.Font = Theme.F(13f, FontStyle.Bold);
            _btnRefresh.SetBounds(20, 502, 186, 50);
            _btnRefresh.Click += delegate { RefreshPower(); };
            Controls.Add(_btnRefresh);

            _btnPay = new AccentButton();
            _btnPay.Text = "去缴费";
            _btnPay.Glyph = "pay";
            _btnPay.Radius = 25f;
            _btnPay.Font = Theme.F(13f, FontStyle.Bold);
            _btnPay.SetBounds(214, 502, 186, 50);
            _btnPay.Click += delegate { OpenPayPage(); };
            Controls.Add(_btnPay);
            ApplyPayStyle();
        }

        // ------------------------------------------------------------ 设置页控件
        private void BuildSettingsPage()
        {
            _btnRepick = new AccentButton();
            _btnRepick.Text = "重新选择房间";
            _btnRepick.ColorTop = Color.FromArgb(255, 246, 250, 255);
            _btnRepick.ColorBottom = Color.FromArgb(255, 230, 241, 255);
            _btnRepick.BorderColor = Color.FromArgb(200, 214, 243);
            _btnRepick.ForeColor = Theme.BlueDark;
            _btnRepick.Radius = 12f;
            _btnRepick.Font = Theme.F(12.5f, FontStyle.Bold);
            _btnRepick.SetBounds(40, 158, 180, 40);
            _btnRepick.Click += delegate { Repick(); };
            Controls.Add(_btnRepick);

            _threshold = new DarkDropdown();
            _threshold.Placeholder = "选择阈值";
            _threshold.Font = Theme.F(13f, FontStyle.Regular);
            _threshold.SetBounds(40, 262, 160, 40);
            double[] options = new double[] { 10, 15, 20, 30, 50 };
            for (int i = 0; i < options.Length; i++)
                _threshold.AddItem(options[i].ToString("0") + " 度", options[i]);
            Controls.Add(_threshold);

            _autoStart = new ToggleSwitch();
            _autoStart.SetBounds(40, 340, 46, 26);
            Controls.Add(_autoStart);

            _btnSave = new AccentButton();
            _btnSave.Text = "保存";
            _btnSave.SetBounds(24, 506, 372, 46);
            _btnSave.Click += delegate { SaveSettings(); };
            Controls.Add(_btnSave);
        }

        private void ShowPage(int page)
        {
            if (_animating) return;
            if (_page == page) { ApplyPage(page); Invalidate(); return; }

            if (!IsHandleCreated) { ApplyPage(page); Invalidate(); return; }
            // 1) 先离屏截取当前页面
            int t0 = Environment.TickCount;
            Bitmap from = CaptureClient();
            // 2) 切换到目标页并离屏渲染（不触发屏幕重绘，避免先闪一下）
            ApplyPage(page);
            Bitmap to = CaptureClient();
            Program.Log("页面切换耗时: capture from/to = " + (Environment.TickCount - t0) + "ms");
            if (from == null || to == null)
            {
                if (from != null) from.Dispose();
                if (to != null) to.Dispose();
                Invalidate();
                return;
            }
            _animFrom = from;
            _animTo = to;
            _animDir = page == PageSettings ? 1 : -1;
            _animT = 0f;
            _animating = true;
            foreach (Control ctl in Controls) ctl.Visible = false;
            Invalidate();
            _pageTimer.Start();
        }

        private System.Windows.Forms.Timer _pageTimer;

        private void ApplyPage(int page)
        {
            _page = page;
            bool main = page == PageMain;
            Theme.SetVisible(_gauge, main);
            Theme.SetVisible(_btnRefresh, main);
            Theme.SetVisible(_btnPay, main);
            Theme.SetVisible(_btnRepick, !main);
            Theme.SetVisible(_threshold, !main);
            Theme.SetVisible(_autoStart, !main);
            Theme.SetVisible(_btnSave, !main);
            if (!main) LoadSettingsIntoControls();
            Invalidate(true);
        }

        private void LoadSettingsIntoControls()
        {
            _threshold.SelectByTag(_cfg.LowThreshold);
            _autoStart.Checked = _cfg.AutoStart;
        }

        // ------------------------------------------------------------ 缴费入口
        private void ApplyPayStyle()
        {
            if (_btnPay == null) return;
            bool low = _result != null && _error == null
                && _result.Power <= _cfg.LowThreshold;
            if (low)
            {
                _btnPay.ColorTop = Color.FromArgb(255, 255, 248, 232);
                _btnPay.ColorBottom = Color.FromArgb(255, 255, 236, 200);
                _btnPay.BorderColor = Color.FromArgb(210, 176, 110);
                _btnPay.ForeColor = Color.FromArgb(190, 118, 12);
            }
            else
            {
                _btnPay.ColorTop = Color.FromArgb(255, 255, 255, 255);
                _btnPay.ColorBottom = Color.FromArgb(255, 243, 248, 255);
                _btnPay.BorderColor = Color.FromArgb(198, 214, 240);
                _btnPay.ForeColor = Theme.BlueDark;
            }
            _btnPay.Invalidate();
        }

        private void OpenPayPage()
        {
            try
            {
                if (_cfg != null && !_cfg.PayHintShown)
                {
                    string tip = "接下来会打开浏览器，进入学校官方的「校园安心付」缴费页面。\n\n"
                        + "· 在页面里选择你的校区 / 楼栋 / 楼层 / 房间，填上姓名即可缴费\n"
                        + "· 本软件只负责打开这个官方页面，不参与支付，也不会保存你的姓名或支付信息";
                    MessageBox.Show(this, tip, "去缴费", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    _cfg.PayHintShown = true;
                    try { _cfg.Save(); } catch { }
                }
                Process.Start(new ProcessStartInfo(Program.PayUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Program.Log("打开缴费页失败: " + ex.Message);
                MessageBox.Show(this, "打开浏览器失败，你可以手动访问：\n" + Program.PayUrl,
                    "去缴费", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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
            if (Program.DemoMode)
            {
                PowerResult demo = new PowerResult();
                demo.Power = 37.66;
                demo.Unit = "度";
                demo.ElapsedMs = 120;
                _result = demo;
                _queriedAt = DateTime.Now;
                _targetRatio = Math.Max(0.02, Math.Min(1.0, demo.Power / 100.0));
                _gauge.Accent = Theme.StatusColor(demo.Power, demo.Unit, _cfg.LowThreshold);
                _gauge.Accent2 = AccentButton.Lighten(_gauge.Accent, 45);
                _gauge.ValueText = demo.Power.ToString("0.00");
                _gauge.UnitText = demo.Unit;
                ApplyPayStyle();
                Invalidate();
                return;
            }
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
                        _gauge.Accent2 = AccentButton.Lighten(_gauge.Accent, 45);
                        _gauge.ValueText = r.Power.ToString("0.00");
                        _gauge.UnitText = r.Unit;
                        _gauge.Caption = null;
                        ApplyPayStyle();
                        Program.Log("查询成功: " + r.Power + " " + r.Unit + " (阈值 " + _cfg.LowThreshold + " 度)");
                        Invalidate();
                    });
                }
                catch (Exception ex)
                {
                    SafeInvoke(delegate
                    {
                        _loading = false;
                        _error = FriendlyError(ex);
                        ApplyPayStyle();
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

        // ------------------------------------------------------------ 页面切换动画
        private Bitmap CaptureClient()
        {
            try
            {
                Bitmap bmp = new Bitmap(ClientSize.Width, ClientSize.Height);
                DrawToBitmap(bmp, new Rectangle(0, 0, ClientSize.Width, ClientSize.Height));
                return bmp;
            }
            catch { return null; }
        }

        private static float EaseOutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        private void OnPageAnimTick()
        {
            if (!_animating) { _pageTimer.Stop(); return; }
            _animT += 0.015f / PageAnimSeconds;
            if (_animT >= 1f)
            {
                _animT = 1f;
                _pageTimer.Stop();
                _animating = false;
                if (_animFrom != null) { _animFrom.Dispose(); _animFrom = null; }
                if (_animTo != null) { _animTo.Dispose(); _animTo = null; }
                // 恢复控件可见性
                ApplyPage(_page);
                Cursor = Cursors.Default;
                Invalidate(true);
                return;
            }
            Invalidate();
        }

        private void DrawPageAnim(Graphics g)
        {
            if (_animFrom == null || _animTo == null) return;
            float e = EaseOutCubic(Math.Min(1f, _animT));
            float w = ClientSize.Width, h = ClientSize.Height;
            float offset = (1f - e) * w;
            int dir = _animDir;
            using (SolidBrush bg = new SolidBrush(Theme.BgBottom))
                g.FillRectangle(bg, 0, 0, w, h);
            // t=0 时旧页在原位、新页在屏幕外；t=1 时旧页移出、新页归位
            g.DrawImage(_animFrom, new RectangleF(-dir * (w - offset), 0, w, h));
            g.DrawImage(_animTo, new RectangleF(dir * offset, 0, w, h));
        }

        // ------------------------------------------------------------ 动画
        private void OnAnimTick()
        {
            if (_animating) return;
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
                _gauge.Accent2 = Theme.Cyan;
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

            if (_animating) { DrawPageAnim(g); return; }

            DrawTitleBar(g);

            if (_page == PageMain) PaintMainPage(g);
            else PaintSettingsPage(g);
        }

        private void DrawTitleBar(Graphics g)
        {
            // 右上：✕ / —；设置图标在 ✕ 正下方；打赏图标在左侧，与设置对称
            _closeRect = new Rectangle(ClientSize.Width - 48, 10, 34, 34);
            _gearRect = new Rectangle(ClientSize.Width - 48, 48, 34, 34);
            _minRect = new Rectangle(ClientSize.Width - 86, 10, 34, 34);
            _donateRect = new Rectangle(24, 48, 34, 34);

            if (_page == PageMain)
            {
                System.Drawing.Drawing2D.InterpolationMode oldMode = g.InterpolationMode;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                RectangleF logoRect = new RectangleF(22, 10, 24, 24);
                if (Theme.Logo != null)
                {
                    using (GraphicsPath clip = Theme.Rounded(logoRect, 7f))
                    {
                        Region oldClip = g.Clip;
                        g.SetClip(clip);
                        g.DrawImage(Theme.Logo, logoRect);
                        g.ResetClip();
                    }
                }
                else
                {
                    Theme.Bolt(g, new RectangleF(26, 12, 14, 22), Theme.Blue);
                }
                g.InterpolationMode = oldMode;
                Theme.Text(g, "宿电监测", Theme.F(15f, FontStyle.Bold), Theme.TextPrimary,
                    new RectangleF(52, 12, 180, 26), StringAlignment.Near);

                // 打赏（左上角，与设置对称；金币 + ¥ 一眼看懂）
                DrawIconTile(g, _donateRect, _hoverDonate,
                    Color.FromArgb(255, 224, 150, 56), Color.FromArgb(255, 255, 176, 88),
                    delegate(Graphics gg, RectangleF r)
                    {
                        Theme.DonateCoin(gg, new RectangleF(r.X, r.Y, r.Width, r.Height),
                            Color.FromArgb(255, 226, 132, 40), Color.FromArgb(255, 255, 214, 110),
                            Color.FromArgb(160, 78, 36));
                    });
                // 设置（✕ 正下方，冷色底 + 实心齿轮）
                DrawIconTile(g, _gearRect, _hoverGear,
                    Color.FromArgb(200, 150, 170, 210), Color.FromArgb(255, 210, 220, 240),
                    delegate(Graphics gg, RectangleF r)
                    {
                        Theme.GearSolid(gg, new RectangleF(r.X, r.Y, r.Width, r.Height),
                            _hoverGear ? Theme.Blue : Theme.TextSecondary);
                    });
            }
            else
            {
                // 标题在左上角；返回箭头下移一行，与主页面的设置图标同一水平线
                Theme.Text(g, "设置", Theme.F(16f, FontStyle.Bold), Theme.TextPrimary,
                    new RectangleF(24, 12, 200, 26), StringAlignment.Near);
                _backRect = new Rectangle(24, 48, 34, 34);
                if (_hoverBack) Theme.FillRound(g, _backRect, 11f, Color.FromArgb(40, 255, 255, 255));
                Theme.BackArrow(g, new RectangleF(_backRect.X + 7, _backRect.Y + 7, _backRect.Width - 14, _backRect.Height - 14),
                    _hoverBack ? Theme.TextPrimary : Theme.TextSecondary);
            }

            // 最小化 / 关闭
            if (_hoverMin) Theme.FillRound(g, _minRect, 10f, Color.FromArgb(235, 242, 255));
            if (_hoverClose) Theme.FillRound(g, _closeRect, 10f, Color.FromArgb(255, 232, 236));
            Theme.TextCenter(g, "—", Theme.F(14f, FontStyle.Bold),
                _hoverMin ? Theme.TextPrimary : Theme.TextSecondary, _minRect);
            Theme.TextCenter(g, "✕", Theme.F(13f, FontStyle.Bold),
                _hoverClose ? Theme.RedDark : Theme.TextSecondary, _closeRect);
        }

        /// <summary>小米风格图标底座：圆角方块 + 细边框，悬停变亮。</summary>
        private void DrawIconTile(Graphics g, Rectangle r, bool hover, Color tintTop, Color tintBottom, Action<Graphics, RectangleF> glyph)
        {
            RectangleF box = new RectangleF(r.X, r.Y, r.Width, r.Height);
            Theme.Shadow(g, box, 11f, hover ? 30 : 16);
            Theme.FillRound(g, box, 11f, Color.White);
            if (hover)
                using (GraphicsPath path = Theme.Rounded(box, 11f))
                using (LinearGradientBrush bg = new LinearGradientBrush(box,
                    Color.FromArgb(255, 244, 249, 255), Color.FromArgb(255, 233, 242, 255), LinearGradientMode.Vertical))
                    g.FillPath(bg, path);
            Theme.StrokeRound(g, box, 11f, hover ? Color.FromArgb(180, 205, 240) : Theme.CardBorder, 1f);
            float pad = 8f;
            glyph(g, new RectangleF(r.X + pad, r.Y + pad, r.Width - pad * 2, r.Height - pad * 2));
        }

        private void PaintMainPage(Graphics g)
        {
            string room = _cfg == null ? "" : TrimRoom(_cfg.BuildDisplayName());
            Theme.TextCenter(g, room, Theme.F(12.5f, FontStyle.Regular), Theme.TextSecondary,
                new RectangleF(20, 42, ClientSize.Width - 40, 22));

            // 状态胶囊（y=400，与黑色版一致；白底 + 彩色描边）
            if (_result != null && _error == null)
            {
                string status = Theme.StatusText(_result.Power, _result.Unit, _cfg.LowThreshold);
                Color accent = Theme.StatusColor(_result.Power, _result.Unit, _cfg.LowThreshold);
                SizeF textSize = g.MeasureString(status, Theme.F(12f, FontStyle.Bold));
                float w = textSize.Width + 46f;
                RectangleF chip = new RectangleF((ClientSize.Width - w) / 2f, 370, w, 32);
                Theme.Shadow(g, chip, 15f, 16);
                Theme.FillRound(g, chip, 15f, Color.White);
                Theme.StrokeRound(g, chip, 15f, Color.FromArgb(110, accent), 1.2f);
                using (SolidBrush dot = new SolidBrush(accent))
                    g.FillEllipse(dot, chip.X + 14, chip.Y + 11.5f, 7, 7);
                Theme.Text(g, status, Theme.F(12f, FontStyle.Bold), accent,
                    new RectangleF(chip.X + 28, chip.Y, chip.Width - 34, chip.Height), StringAlignment.Near);
            }

            // 信息卡（y=440，与黑色版一致）
            RectangleF card = new RectangleF(60, 420, ClientSize.Width - 120, 64);
            Theme.CardBox(g, card, 18f);

            if (_error == null)
            {
                string updated = _queriedAt == DateTime.MinValue ? "--" : _queriedAt.ToString("HH:mm:ss");
                string elapsed = _result == null ? "" : ("· " + (_result.ElapsedMs / 1000.0).ToString("0.00") + "s");
                Theme.Text(g, "更新时间", Theme.F(11f, FontStyle.Regular), Theme.TextDim,
                    new RectangleF(card.X + 20, card.Y + 9, 120, 18), StringAlignment.Near);
                Theme.Text(g, updated + " " + elapsed, Theme.F(12f, FontStyle.Bold), Theme.TextPrimary,
                    new RectangleF(card.X + 20, card.Y + 31, 200, 20), StringAlignment.Near);
                Theme.Text(g, "数据来源", Theme.F(11f, FontStyle.Regular), Theme.TextDim,
                    new RectangleF(card.Right - 150, card.Y + 9, 130, 18), StringAlignment.Far);
                Theme.Text(g, "校园安心付", Theme.F(12f, FontStyle.Bold), Theme.TextSecondary,
                    new RectangleF(card.Right - 150, card.Y + 31, 130, 20), StringAlignment.Far);
            }
            else
            {
                Theme.Text(g, "⚠ " + _error, Theme.F(11.5f, FontStyle.Regular), Theme.Red,
                    new RectangleF(card.X + 14, card.Y, card.Width - 28, card.Height), StringAlignment.Near);
            }
        }
        private void PaintSettingsPage(Graphics g)
        {
            // 卡片一：当前房间
            RectangleF card1 = new RectangleF(20, 88, ClientSize.Width - 40, 128);
            Theme.CardBox(g, card1, 22f);
            Theme.Text(g, "当前房间", Theme.F(11f, FontStyle.Bold), Theme.TextDim,
                new RectangleF(40, 104, 200, 18), StringAlignment.Near);
            Theme.Text(g, TrimRoom(_cfg.BuildDisplayName()), Theme.F(13f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(40, 124, 340, 24), StringAlignment.Near);

            // 卡片二：阈值 + 自启
            RectangleF card2 = new RectangleF(20, 226, ClientSize.Width - 40, 176);
            Theme.CardBox(g, card2, 22f);
            Theme.Text(g, "低电量提醒阈值", Theme.F(11f, FontStyle.Bold), Theme.TextDim,
                new RectangleF(40, 242, 220, 18), StringAlignment.Near);
            Theme.Text(g, "低于这个度数时，主界面会变成红色提醒", Theme.F(10f, FontStyle.Regular), Theme.TextDim,
                new RectangleF(40, 306, 340, 18), StringAlignment.Near);

            Theme.Text(g, "开机自动运行", Theme.F(12.5f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(98, 340, 300, 26), StringAlignment.Near);
            Theme.Text(g, "开机后自动弹窗显示电费", Theme.F(10f, FontStyle.Regular), Theme.TextDim,
                new RectangleF(98, 360, 300, 18), StringAlignment.Near);

            // 底部版本与链接
            Theme.Text(g, "v" + Program.Version + "  ·  开源项目 · 谢谢支持",
                Theme.F(10.5f, FontStyle.Regular), Theme.TextDim,
                new RectangleF(24, 424, 372, 20), StringAlignment.Near);
            _linkRect = new Rectangle(24, 446, 372, 20);
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
            bool hg = _page == PageMain && _gearRect.Contains(e.Location);
            bool hd = _page == PageMain && _donateRect.Contains(e.Location);
            bool hb = _page == PageSettings && _backRect.Contains(e.Location);
            if (hc != _hoverClose || hm != _hoverMin || hg != _hoverGear || hd != _hoverDonate || hb != _hoverBack)
            {
                _hoverClose = hc; _hoverMin = hm; _hoverGear = hg; _hoverDonate = hd; _hoverBack = hb;
                Cursor = (hc || hm || hg || hd || hb) ? Cursors.Hand : Cursors.Default;
                Invalidate(true);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hoverClose = _hoverMin = _hoverGear = _hoverDonate = _hoverBack = false;
            Cursor = Cursors.Default;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (_animating) return;
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
            if (_page == PageMain && _donateRect.Contains(e.Location))
            {
                using (DonateForm f = new DonateForm()) f.ShowDialog(this);
                return;
            }
            if (_page == PageMain && _gearRect.Contains(e.Location))
            {
                ShowPage(PageSettings);
                return;
            }
            if (_page == PageSettings)
            {
                if (_backRect.Contains(e.Location)) { ShowPage(PageMain); return; }
                if (_linkRect.Contains(e.Location))
                {
                    try { Process.Start(new ProcessStartInfo(Program.RepoUrl) { UseShellExecute = true }); } catch { }
                    return;
                }
            }
            base.OnMouseClick(e);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
        }
    }
}
