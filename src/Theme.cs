using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ShangtiPower
{
    /// <summary>UI 诊断日志（Theme.cs 同时被安装包编译，不能直接依赖 Program）。</summary>
    public static class Diag
    {
        public static Action<string> Sink;
        public static void Log(string msg)
        {
            try { if (Sink != null) Sink(msg); } catch { }
        }
    }

    /// <summary>统一视觉：颜色、字体、圆角、渐变、按钮、弧形电量表。</summary>
    public static class Theme
    {
        public static readonly Color BgTop = Color.FromArgb(27, 30, 43);
        public static readonly Color BgBottom = Color.FromArgb(13, 15, 22);
        public static readonly Color Card = Color.FromArgb(35, 39, 54);
        public static readonly Color CardBorder = Color.FromArgb(26, 255, 255, 255);
        public static readonly Color TextPrimary = Color.FromArgb(240, 243, 250);
        public static readonly Color TextSecondary = Color.FromArgb(150, 158, 178);
        public static readonly Color TextDim = Color.FromArgb(105, 112, 132);
        public static readonly Color Green = Color.FromArgb(52, 211, 153);
        public static readonly Color GreenDark = Color.FromArgb(16, 138, 110);
        public static readonly Color Blue = Color.FromArgb(96, 165, 250);
        public static readonly Color Amber = Color.FromArgb(251, 191, 36);
        public static readonly Color Red = Color.FromArgb(248, 113, 113);
        public static readonly Color RedDark = Color.FromArgb(198, 48, 62);

        private static string _fontName;

        public static string FontName
        {
            get
            {
                if (_fontName == null)
                {
                    _fontName = "Microsoft YaHei UI";
                    try
                    {
                        using (InstalledFontCollection col = new InstalledFontCollection())
                        {
                            bool found = false;
                            for (int i = 0; i < col.Families.Length; i++)
                                if (col.Families[i].Name == _fontName) { found = true; break; }
                            if (!found) _fontName = "Segoe UI";
                        }
                    }
                    catch { _fontName = "Segoe UI"; }
                }
                return _fontName;
            }
        }

        public static Font F(float size, FontStyle style)
        {
            try { return new Font(FontName, size, style, GraphicsUnit.Pixel); }
            catch { return new Font("Segoe UI", size, style, GraphicsUnit.Pixel); }
        }

        public static GraphicsPath Rounded(RectangleF r, float radius)
        {
            GraphicsPath p = new GraphicsPath();
            if (radius <= 0.1f) { p.AddRectangle(r); return p; }
            float d = radius * 2f;
            p.AddArc(r.X, r.Y, d, d, 180f, 90f);
            p.AddArc(r.Right - d, r.Y, d, d, 270f, 90f);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0f, 90f);
            p.AddArc(r.X, r.Bottom - d, d, d, 90f, 90f);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, RectangleF r, float radius, Color color)
        {
            using (GraphicsPath p = Rounded(r, radius))
            using (SolidBrush b = new SolidBrush(color))
                g.FillPath(b, p);
        }

        public static void StrokeRound(Graphics g, RectangleF r, float radius, Color color, float width)
        {
            using (GraphicsPath p = Rounded(r, radius))
            using (Pen pen = new Pen(color, width))
                g.DrawPath(pen, p);
        }

        public static void FillGradient(Graphics g, Rectangle r, Color top, Color bottom)
        {
            using (LinearGradientBrush b = new LinearGradientBrush(
                new Rectangle(r.X, r.Y, Math.Max(1, r.Width), Math.Max(1, r.Height)),
                top, bottom, LinearGradientMode.Vertical))
                g.FillRectangle(b, r);
        }

        /// <summary>手绘闪电图标（不依赖字体 emoji）。</summary>
        public static void Bolt(Graphics g, RectangleF r, Color color)
        {
            PointF[] pts = new PointF[]
            {
                new PointF(r.X + r.Width * 0.58f, r.Y),
                new PointF(r.X + r.Width * 0.10f, r.Y + r.Height * 0.56f),
                new PointF(r.X + r.Width * 0.46f, r.Y + r.Height * 0.56f),
                new PointF(r.X + r.Width * 0.36f, r.Y + r.Height),
                new PointF(r.X + r.Width * 0.92f, r.Y + r.Height * 0.40f),
                new PointF(r.X + r.Width * 0.54f, r.Y + r.Height * 0.40f)
            };
            using (SolidBrush b = new SolidBrush(color))
                g.FillPolygon(b, pts);
        }

        /// <summary>手绘环形箭头（刷新图标）。</summary>
        public static void RefreshArrow(Graphics g, RectangleF r, Color color, float thick)
        {
            using (Pen pen = new Pen(color, thick))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawArc(pen, r, 40f, 290f);
                PointF tip = new PointF(r.X + r.Width * 0.92f, r.Y + r.Height * 0.25f);
                PointF[] tri = new PointF[]
                {
                    new PointF(tip.X + r.Width * 0.10f, tip.Y - r.Height * 0.02f),
                    new PointF(tip.X - r.Width * 0.06f, tip.Y - r.Height * 0.16f),
                    new PointF(tip.X - r.Width * 0.02f, tip.Y + r.Height * 0.12f)
                };
                using (SolidBrush b = new SolidBrush(color))
                    g.FillPolygon(b, tri);
            }
        }
        public static void Text(Graphics g, string text, Font font, Color color, RectangleF rect, StringAlignment align)
        {
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = align;
                sf.LineAlignment = StringAlignment.Center;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = StringFormatFlags.NoWrap;
                using (SolidBrush b = new SolidBrush(color))
                    g.DrawString(text, font, b, rect, sf);
            }
        }

        public static void TextCenter(Graphics g, string text, Font font, Color color, RectangleF rect)
        {
            Text(g, text, font, color, rect, StringAlignment.Center);
        }

        public static Color StatusColor(double power, string unit, double low)
        {
            if (unit != "度") return Blue;
            if (power <= low) return Red;
            if (power <= low * 2) return Amber;
            return Green;
        }

        public static string StatusText(double power, string unit, double low)
        {
            if (unit != "度") return "余额";
            if (power <= low) return "电量偏低 · 建议充值";
            if (power <= low * 2) return "注意 · 电量不多了";
            return "电量充足";
        }
    }

    /// <summary>无边框圆角按钮（渐变 + 悬浮高亮）。</summary>
    public class AccentButton : Control
    {
        public Color ColorTop { get; set; }
        public Color ColorBottom { get; set; }
        public float Radius { get; set; }
        public string Glyph { get; set; }

        private bool _hover;
        private bool _down;

        public AccentButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            ColorTop = Theme.Green;
            ColorBottom = Theme.GreenDark;
            Radius = 12f;
            Cursor = Cursors.Hand;
            Font = Theme.F(14f, FontStyle.Bold);
            ForeColor = Color.White;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(0, 0, Width - 1, Height - 1);
            Color top;
            Color bottom;
            if (!Enabled)
            {
                top = Color.FromArgb(48, 54, 70);
                bottom = Color.FromArgb(38, 43, 57);
            }
            else
            {
                top = _hover ? Lighten(ColorTop, 18) : ColorTop;
                bottom = _hover ? Lighten(ColorBottom, 10) : ColorBottom;
                if (_down) { top = Darken(top, 12); bottom = Darken(bottom, 12); }
            }
            using (GraphicsPath p = Theme.Rounded(r, Radius))
            using (LinearGradientBrush b = new LinearGradientBrush(
                new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)), top, bottom, 90f))
                g.FillPath(b, p);
            if (!string.IsNullOrEmpty(Glyph) && Glyph == "refresh")
            {
                Theme.RefreshArrow(g, new RectangleF(Width / 2f - 58, Height / 2f - 8, 16, 16), Color.FromArgb(240, 255, 255, 255), 2.2f);
            }
            else if (!string.IsNullOrEmpty(Glyph))
            {
                Rectangle glyphRect = new Rectangle(8, 0, 22, Height);
                Theme.TextCenter(g, Glyph, Theme.F(15f, FontStyle.Regular), Color.FromArgb(235, 255, 255, 255), glyphRect);
            }
            RectangleF tr = new RectangleF(string.IsNullOrEmpty(Glyph) ? 0 : 18, 0, Width - (string.IsNullOrEmpty(Glyph) ? 0 : 18), Height);
            Color textColor = Enabled ? ForeColor : Color.FromArgb(140, 148, 165);
            Theme.TextCenter(g, Text, Font, textColor, tr);
        }

        public static Color Lighten(Color c, int v)
        {
            return Color.FromArgb(c.A, Math.Min(255, c.R + v), Math.Min(255, c.G + v), Math.Min(255, c.B + v));
        }
        public static Color Darken(Color c, int v)
        {
            return Color.FromArgb(c.A, Math.Max(0, c.R - v), Math.Max(0, c.G - v), Math.Max(0, c.B - v));
        }
    }

    /// <summary>圆弧电量表：环 + 发光 + 中心数值。</summary>
    public class ArcGauge : Control
    {
        public double Ratio { get; set; }
        public Color Accent { get; set; }
        public string ValueText { get; set; }
        public string UnitText { get; set; }
        public string Caption { get; set; }

        public ArcGauge()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            Accent = Theme.Green;
            Ratio = 0;
            ValueText = "--";
            UnitText = "度";
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float size = Math.Min(Width, Height) - 18f;
            RectangleF ring = new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size);
            float thick = Math.Max(10f, size * 0.075f);

            // 背景环
            using (Pen bg = new Pen(Color.FromArgb(46, 255, 255, 255), thick))
            {
                bg.StartCap = LineCap.Round;
                bg.EndCap = LineCap.Round;
                g.DrawArc(bg, ring, 135f, 270f);
            }

            // 进度环（带光晕）
            float sweep = (float)(270.0 * Math.Max(0.0, Math.Min(1.0, Ratio)));
            if (sweep > 0.5f)
            {
                using (Pen glow = new Pen(Color.FromArgb(60, Accent), thick + 10f))
                {
                    glow.StartCap = LineCap.Round;
                    glow.EndCap = LineCap.Round;
                    g.DrawArc(glow, ring, 135f, sweep);
                }
                using (Pen pen = new Pen(Accent, thick))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, ring, 135f, sweep);
                }
            }

            // 中心文字
            RectangleF valRect = new RectangleF(0, Height / 2f - size * 0.19f, Width, size * 0.30f);
            Theme.TextCenter(g, ValueText, Theme.F(Math.Max(30f, size * 0.26f), FontStyle.Bold), Theme.TextPrimary, valRect);

            RectangleF unitRect = new RectangleF(0, Height / 2f + size * 0.075f, Width, size * 0.16f);
            Theme.TextCenter(g, UnitText, Theme.F(Math.Max(12f, size * 0.085f), FontStyle.Regular), Theme.TextSecondary, unitRect);

            if (!string.IsNullOrEmpty(Caption))
            {
                RectangleF capRect = new RectangleF(0, Height / 2f + size * 0.22f, Width, size * 0.16f);
                Theme.TextCenter(g, Caption, Theme.F(Math.Max(11f, size * 0.075f), FontStyle.Regular), Theme.TextDim, capRect);
            }
        }
    }

    /// <summary>可拖动、无边框、圆角的窗体基类。</summary>
    public class RoundedForm : Form
    {
        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        // 注意：必须用「非客户区」按下消息。若用 WM_LBUTTONDOWN，WinForms 会把它当成
        // 一次普通鼠标按下再次触发 OnMouseDown，形成无限递归 → StackOverflowException（闪退）。
        private const int WM_NCLBUTTONDOWN = 0xA1;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        public float CornerRadius { get; set; }
        public bool AllowDrag { get; set; }

        public RoundedForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            CornerRadius = 18f;
            AllowDrag = true;
            BackColor = Theme.BgBottom;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRoundedRegion();
        }

        private void ApplyRoundedRegion()
        {
            try
            {
                using (GraphicsPath p = Theme.Rounded(new RectangleF(0, 0, Width, Height), CornerRadius))
                    Region = new Region(p);
            }
            catch { }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!AllowDrag || e.Button != MouseButtons.Left) return;
            // 用非客户区消息启动窗口拖动（DefWindowProc 直接进入移动循环，不会回调 MouseDown）
            try
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, (IntPtr)HTCAPTION, IntPtr.Zero);
            }
            catch (Exception ex) { Diag.Log("拖动窗口失败: " + ex.Message); }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST && AllowDrag)
            {
                // 让无边框窗口在拖动区表现为标题栏
            }
        }

        protected void PaintWindowFrame(Graphics g)
        {
            Theme.FillGradient(g, new Rectangle(0, 0, Width, Height), Theme.BgTop, Theme.BgBottom);
            // 顶部柔光
            using (GraphicsPath p = Theme.Rounded(new RectangleF(0, 0, Width, Height), CornerRadius))
            {
                g.SetClip(p);
                using (GraphicsPath glow = new GraphicsPath())
                {
                    glow.AddEllipse(new RectangleF(-Width * 0.5f, -Height * 0.62f, Width * 2f, Height * 1.1f));
                    using (PathGradientBrush b = new PathGradientBrush(glow))
                    {
                        b.CenterColor = Color.FromArgb(28, 96, 165, 250);
                        b.SurroundColors = new Color[] { Color.Transparent };
                        g.FillPath(b, glow);
                    }
                }
                g.ResetClip();
                using (Pen border = new Pen(Color.FromArgb(30, 255, 255, 255), 1.2f))
                    g.DrawPath(border, p);
            }
        }
    }


    /// <summary>深色风格的开关控件。</summary>
    public class ToggleSwitch : Control
    {
        public bool Checked { get; set; }
        public event EventHandler CheckedChanged;

        private bool _hover;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Size = new Size(46, 26);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            Checked = !Checked;
            Invalidate();
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF track = new RectangleF(0, (Height - 26) / 2f, Width - 1, 26);
            Color fill = Checked ? Theme.Green : Color.FromArgb(64, 70, 88);
            if (_hover) fill = AccentButton.Lighten(fill, 10);
            Theme.FillRound(g, track, 13f, fill);
            float knob = 20f;
            float x = Checked ? track.Right - knob - 3f : track.X + 3f;
            using (SolidBrush b = new SolidBrush(Color.White))
                g.FillEllipse(b, x, track.Y + 3f, knob, knob);
        }
    }
}