using System;
using System.Drawing;
using System.IO;
using System.Reflection;
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
        public static void SetVisible(Control c, bool visible)
        {
            if (c == null) return;
            // 注意：窗口句柄创建前 Control.Visible 读出来总是 false，
            // 所以不能比较，必须无条件赋值，否则控件会在切换后仍然显示。
            c.Visible = visible;
        }

        public static readonly Color BgTop = Color.FromArgb(249, 251, 255);
        public static readonly Color BgBottom = Color.FromArgb(226, 236, 253);
        public static readonly Color Card = Color.FromArgb(255, 255, 255);
        public static readonly Color CardBorder = Color.FromArgb(222, 232, 247);
        public static readonly Color TextPrimary = Color.FromArgb(28, 40, 66);
        public static readonly Color TextSecondary = Color.FromArgb(88, 105, 138);
        public static readonly Color TextDim = Color.FromArgb(144, 158, 184);
        public static readonly Color Green = Color.FromArgb(30, 190, 110);
        public static readonly Color GreenDark = Color.FromArgb(18, 155, 88);
        public static readonly Color Blue = Color.FromArgb(74, 140, 255);
        public static readonly Color BlueDark = Color.FromArgb(43, 104, 236);
        public static readonly Color Cyan = Color.FromArgb(96, 205, 255);
        public static readonly Color Amber = Color.FromArgb(245, 158, 11);
        public static readonly Color Red = Color.FromArgb(235, 76, 82);
        public static readonly Color RedDark = Color.FromArgb(210, 46, 56);

        // 次级按钮（白底浅边框）
        public static readonly Color SecondaryTop = Color.FromArgb(255, 255, 255);
        public static readonly Color SecondaryBottom = Color.FromArgb(238, 244, 255);
        public static readonly Color SecondaryText = Color.FromArgb(74, 92, 124);

        private static Image _logo;
        private static bool _logoLoaded;

        /// <summary>界面用的应用图标（内嵌 logo-96.png，无则返回 null）。</summary>
        public static Image Logo
        {
            get
            {
                if (_logoLoaded) return _logo;
                _logoLoaded = true;
                try
                {
                    using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("LogoImage.png"))
                    {
                        if (s != null)
                        {
                            using (Image img = Image.FromStream(s)) _logo = new Bitmap(img);
                        }
                    }
                }
                catch { _logo = null; }
                return _logo;
            }
        }

        /// <summary>柔和投影（白卡片用），strength 越小越淡。</summary>
        public static void Shadow(Graphics g, RectangleF r, float radius, int strength)
        {
            for (int i = 3; i >= 1; i--)
            {
                Color c = Color.FromArgb(Math.Max(2, strength / (i + 2)), 84, 120, 178);
                FillRound(g, new RectangleF(r.X - i, r.Y - i + 3, r.Width + i * 2, r.Height + i * 2), radius + i, c);
            }
        }

        /// <summary>白色卡片 + 柔和投影 + 浅边框。</summary>
        public static void CardBox(Graphics g, RectangleF r, float radius)
        {
            Shadow(g, r, radius, 24);
            FillRound(g, r, radius, Color.White);
            StrokeRound(g, r, radius, CardBorder, 1f);
        }

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
        /// <summary>手绘齿轮（设置图标）。</summary>
        public static void Gear(Graphics g, RectangleF r, Color color)
        {
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            float outer = r.Width * 0.30f;
            float inner = r.Width * 0.13f;
            using (Pen pen = new Pen(color, Math.Max(1.6f, r.Width * 0.075f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawEllipse(pen, cx - outer * 0.72f, cy - outer * 0.72f, outer * 1.44f, outer * 1.44f);
                // 8 个齿
                for (int i = 0; i < 8; i++)
                {
                    double a = Math.PI * 2 * i / 8.0;
                    float x1 = cx + (float)(Math.Cos(a) * outer * 0.98f);
                    float y1 = cy + (float)(Math.Sin(a) * outer * 0.98f);
                    float x2 = cx + (float)(Math.Cos(a) * outer * 1.38f);
                    float y2 = cy + (float)(Math.Sin(a) * outer * 1.38f);
                    g.DrawLine(pen, x1, y1, x2, y2);
                }
                g.DrawEllipse(pen, cx - inner, cy - inner, inner * 2, inner * 2);
            }
        }

        /// <summary>手绘心形（打赏图标）。</summary>
        public static void Heart(Graphics g, RectangleF r, Color color)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                float w = r.Width, h = r.Height, x = r.X, y = r.Y;
                p.AddBezier(x + w * 0.5f, y + h * 0.30f, x + w * 0.08f, y - h * 0.12f, x - w * 0.06f, y + h * 0.52f, x + w * 0.5f, y + h);
                p.AddBezier(x + w * 0.5f, y + h * 0.30f, x + w * 0.92f, y - h * 0.12f, x + w * 1.06f, y + h * 0.52f, x + w * 0.5f, y + h);
                p.CloseFigure();
                using (SolidBrush b = new SolidBrush(color)) g.FillPath(b, p);
            }
        }

        /// <summary>手绘返回箭头（‹）。</summary>
        public static void BackArrow(Graphics g, RectangleF r, Color color)
        {
            float cx = r.X + r.Width * 0.58f, cy = r.Y + r.Height * 0.5f;
            using (Pen pen = new Pen(color, Math.Max(1.8f, r.Width * 0.13f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx + r.Width * 0.22f, cy - r.Height * 0.30f, cx - r.Width * 0.18f, cy);
                g.DrawLine(pen, cx - r.Width * 0.18f, cy, cx + r.Width * 0.22f, cy + r.Height * 0.30f);
            }
        }

        /// <summary>实心齿轮（设置图标）：8 齿 + 中间圆孔，小米风格。</summary>
        public static void GearSolid(Graphics g, RectangleF r, Color color)
        {
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            float rad = Math.Min(r.Width, r.Height) / 2f;
            float outer = rad * 0.98f;
            float inner = rad * 0.76f;
            float hole = rad * 0.34f;
            int teeth = 8;
            double step = Math.PI * 2 / teeth;

            using (GraphicsPath path = new GraphicsPath(FillMode.Alternate))
            {
                PointF[] pts = new PointF[teeth * 4];
                for (int i = 0; i < teeth; i++)
                {
                    double a0 = step * i;
                    pts[i * 4 + 0] = Polar(cx, cy, outer, a0 - step * 0.20);
                    pts[i * 4 + 1] = Polar(cx, cy, outer, a0 + step * 0.20);
                    pts[i * 4 + 2] = Polar(cx, cy, inner, a0 + step * 0.28);
                    pts[i * 4 + 3] = Polar(cx, cy, inner, a0 + step * 0.72);
                }
                path.AddPolygon(pts);
                // 中间圆孔：Alternate 填充下与外形叠加形成镂空
                path.AddEllipse(cx - hole, cy - hole, hole * 2f, hole * 2f);
                using (SolidBrush b = new SolidBrush(color)) g.FillPath(b, path);
            }
        }

        private static PointF Polar(float cx, float cy, float rad, double angle)
        {
            return new PointF(cx + (float)Math.Cos(angle) * rad, cy + (float)Math.Sin(angle) * rad);
        }

        /// <summary>打赏金币（一眼看懂）：金色圆币 + ¥ + 闪光。</summary>
        public static void DonateCoin(Graphics g, RectangleF r, Color goldTop, Color goldBottom, Color symbol)
        {
            float cx = r.X + r.Width / 2f;
            float cy = r.Y + r.Height / 2f;
            float rad = Math.Min(r.Width, r.Height) / 2f;
            RectangleF circle = new RectangleF(cx - rad, cy - rad, rad * 2f, rad * 2f);
            using (LinearGradientBrush b = new LinearGradientBrush(circle, goldTop, goldBottom, LinearGradientMode.Vertical))
                g.FillEllipse(b, circle);
            // 内圈
            using (Pen pen = new Pen(Color.FromArgb(150, symbol), Math.Max(1.1f, rad * 0.12f)))
                g.DrawEllipse(pen, cx - rad * 0.78f, cy - rad * 0.78f, rad * 1.56f, rad * 1.56f);
            // ¥ 符号
            Theme.TextCenter(g, "¥", Theme.F(Math.Max(8f, rad * 1.02f), FontStyle.Bold), symbol,
                new RectangleF(cx - rad, cy - rad, rad * 2f, rad * 2f));
            // 闪光（右上 + 左下）
            using (Pen pen = new Pen(Color.FromArgb(220, 255, 255, 255), Math.Max(1f, rad * 0.11f)))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                float sx = r.X + r.Width * 0.86f, sy = r.Y + r.Height * 0.14f;
                g.DrawLine(pen, sx - rad * 0.16f, sy, sx + rad * 0.16f, sy);
                g.DrawLine(pen, sx, sy - rad * 0.16f, sx, sy + rad * 0.16f);
            }
        }
        /// <summary>可爱咖啡杯（打赏图标）：杯身 + 把手 + 热气 + 小心心。</summary>
        public static void CoffeeCup(Graphics g, RectangleF r, Color cup, Color steam, Color heart)
        {
            float w = r.Width, h = r.Height, x = r.X, y = r.Y;
            // 杯身（上宽下窄的圆角梯形）
            float top = y + h * 0.40f, bottom = y + h * 0.90f;
            PointF[] body =
            {
                new PointF(x + w * 0.12f, top),
                new PointF(x + w * 0.72f, top),
                new PointF(x + w * 0.64f, bottom),
                new PointF(x + w * 0.20f, bottom)
            };
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddPolygon(body);
                using (SolidBrush b = new SolidBrush(cup)) g.FillPath(b, path);
                // 底部圆角修饰
                using (SolidBrush b = new SolidBrush(cup)) g.FillEllipse(b, x + w * 0.18f, bottom - h * 0.08f, w * 0.48f, h * 0.16f);
            }
            // 把手
            using (Pen pen = new Pen(cup, Math.Max(1.5f, w * 0.10f)))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawArc(pen, x + w * 0.62f, top + h * 0.04f, w * 0.28f, h * 0.30f, -80f, 190f);
            }
            // 热气（两条小波浪）
            using (Pen pen = new Pen(Color.FromArgb(200, steam), Math.Max(1.2f, w * 0.07f)))
            {
                pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                g.DrawBezier(pen,
                    x + w * 0.30f, y + h * 0.30f,
                    x + w * 0.22f, y + h * 0.20f,
                    x + w * 0.40f, y + h * 0.16f,
                    x + w * 0.32f, y + h * 0.06f);
                g.DrawBezier(pen,
                    x + w * 0.52f, y + h * 0.30f,
                    x + w * 0.44f, y + h * 0.20f,
                    x + w * 0.62f, y + h * 0.16f,
                    x + w * 0.54f, y + h * 0.06f);
            }
            // 杯子中间的可爱小心心
            Heart(g, new RectangleF(x + w * 0.28f, top + h * 0.10f, w * 0.22f, h * 0.20f), heart);
        }
        /// <summary>渐变心形（打赏图标），带高光。</summary>
        public static void HeartGradient(Graphics g, RectangleF r, Color top, Color bottom)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                float w = r.Width, h = r.Height, x = r.X, y = r.Y;
                p.AddBezier(x + w * 0.5f, y + h * 0.30f, x + w * 0.08f, y - h * 0.12f, x - w * 0.06f, y + h * 0.52f, x + w * 0.5f, y + h);
                p.AddBezier(x + w * 0.5f, y + h * 0.30f, x + w * 0.92f, y - h * 0.12f, x + w * 1.06f, y + h * 0.52f, x + w * 0.5f, y + h);
                p.CloseFigure();
                using (LinearGradientBrush b = new LinearGradientBrush(
                    new RectangleF(r.X, r.Y, r.Width, r.Height + 1f), top, bottom, LinearGradientMode.Vertical))
                    g.FillPath(b, p);
                // 左上高光
                using (SolidBrush hi = new SolidBrush(Color.FromArgb(70, 255, 255, 255)))
                    g.FillEllipse(hi, r.X + r.Width * 0.22f, r.Y + r.Height * 0.20f, r.Width * 0.22f, r.Height * 0.16f);
            }
        }

        public static void Text(Graphics g, string text, Font font, Color color, RectangleF rect, StringAlignment align)
        {
            // 小字用 ClearType（清晰），大字用灰度抗锯齿（避免彩色描边/锯齿）
            g.TextRenderingHint = font.Size >= 18f
                ? System.Drawing.Text.TextRenderingHint.AntiAliasGridFit
                : System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
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
            if (power <= low) return Red;            // 低于阈值：红
            if (power <= low * 1.25) return Amber;   // 略高于阈值：黄（只留 25% 缓冲）
            return Blue;
        }

        public static string StatusText(double power, string unit, double low)
        {
            if (unit != "度") return "余额";
            if (power <= low) return "电量偏低 · 建议充值";
            if (power <= low * 1.25) return "注意 · 电量不多了";
            return "电量充足";
        }
    }

    /// <summary>无边框圆角按钮（渐变 + 悬浮高亮）。</summary>
    public class AccentButton : Control
    {
        public Color ColorTop { get; set; }
        public Color ColorBottom { get; set; }
        public Color BorderColor { get; set; }
        public float Radius { get; set; }
        public string Glyph { get; set; }

        private bool _hover;
        private bool _down;

        public AccentButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            ColorTop = Theme.Blue;
            ColorBottom = Theme.Cyan;
            BorderColor = Color.Transparent;
            Radius = 14f;
            Cursor = Cursors.Hand;
            BackColor = Color.Transparent;
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
                top = Color.FromArgb(240, 244, 251);
                bottom = Color.FromArgb(230, 237, 249);
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
            if (Enabled && BorderColor.A > 0)
                using (GraphicsPath p2 = Theme.Rounded(r, Radius))
                using (Pen pen = new Pen(BorderColor, 1f))
                    g.DrawPath(pen, p2);
            if (!string.IsNullOrEmpty(Glyph) && Glyph == "check")
            {
                float gx = 18f, gy = Height / 2f;
                using (Pen pen = new Pen(Color.FromArgb(240, 255, 255, 255), 2.4f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, new PointF[] {
                        new PointF(gx, gy),
                        new PointF(gx + 5f, gy + 5f),
                        new PointF(gx + 13f, gy - 6f)
                    });
                }
            }
            else if (!string.IsNullOrEmpty(Glyph) && Glyph == "pay")
            {
                using (Font f = Theme.F(16f, FontStyle.Bold))
                using (SolidBrush br = new SolidBrush(Enabled ? ForeColor : Color.FromArgb(172, 182, 200)))
                {
                    SizeF sz = g.MeasureString("¥", f);
                    g.DrawString("¥", f, br, new PointF(Width / 2f - 48f - sz.Width / 2f, (Height - sz.Height) / 2f));
                }
            }
            else if (!string.IsNullOrEmpty(Glyph) && Glyph == "refresh")
            {
                Theme.RefreshArrow(g, new RectangleF(Width / 2f - 58, Height / 2f - 8, 16, 16), Color.FromArgb(240, 255, 255, 255), 2.2f);
            }
            else if (!string.IsNullOrEmpty(Glyph))
            {
                Rectangle glyphRect = new Rectangle(8, 0, 22, Height);
                Theme.TextCenter(g, Glyph, Theme.F(15f, FontStyle.Regular), Color.FromArgb(235, 255, 255, 255), glyphRect);
            }
            RectangleF tr = new RectangleF(string.IsNullOrEmpty(Glyph) ? 0 : 18, 0, Width - (string.IsNullOrEmpty(Glyph) ? 0 : 18), Height);
            Color textColor = Enabled ? ForeColor : Color.FromArgb(172, 182, 200);
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
        /// <summary>渐变终点色（A=0 时用纯色）。</summary>
        public Color Accent2 { get; set; }
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

            // 关键：把画笔粗细和光晕都算进边距，否则圆弧会超出控件边界，
            // 在上下左右被裁出“平口”（之前主界面圆环被切掉就是这个原因）。
            float box = Math.Min(Width, Height);
            float thick = Math.Max(10f, box * 0.072f);
            float glowExtra = 10f;
            float margin = (thick + glowExtra) / 2f + 2f;
            float size = box - margin * 2f;

            RectangleF ring = new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size);

            // 背景环
            using (Pen bg = new Pen(Color.FromArgb(255, 219, 229, 245), thick))
            {
                bg.StartCap = LineCap.Round;
                bg.EndCap = LineCap.Round;
                g.DrawArc(bg, ring, 135f, 270f);
            }

            // 进度环（带光晕）
            float sweep = (float)(270.0 * Math.Max(0.0, Math.Min(1.0, Ratio)));
            if (sweep > 0.5f)
            {
                using (Pen glow = new Pen(Color.FromArgb(46, Accent), thick + glowExtra))
                {
                    glow.StartCap = LineCap.Round;
                    glow.EndCap = LineCap.Round;
                    g.DrawArc(glow, ring, 135f, sweep);
                }
                Color c2 = Accent2.A > 0 ? Accent2 : Accent;
                using (LinearGradientBrush lb = new LinearGradientBrush(ring, Accent, c2, 45f))
                using (Pen pen = new Pen(lb, thick))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    g.DrawArc(pen, ring, 135f, sweep);
                }
            }

            // 中心文字（字号按整个控件尺寸算，保持视觉大小不变）
            RectangleF valRect = new RectangleF(0, Height / 2f - box * 0.19f, Width, box * 0.30f);
            Theme.TextCenter(g, ValueText, Theme.F(Math.Max(30f, box * 0.245f), FontStyle.Bold), Theme.TextPrimary, valRect);

            RectangleF unitRect = new RectangleF(0, Height / 2f + box * 0.075f, Width, box * 0.16f);
            Theme.TextCenter(g, UnitText, Theme.F(Math.Max(12f, box * 0.08f), FontStyle.Regular), Theme.TextSecondary, unitRect);

            if (!string.IsNullOrEmpty(Caption))
            {
                RectangleF capRect = new RectangleF(0, Height / 2f + box * 0.22f, Width, box * 0.16f);
                Theme.TextCenter(g, Caption, Theme.F(Math.Max(11f, box * 0.072f), FontStyle.Regular), Theme.TextDim, capRect);
            }
        }
    }

    /// <summary>可拖动、无边框、圆角的窗体基类。</summary>
    public class RoundedForm : Form
    {
        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;
        private const int WS_MINIMIZEBOX = 0x00020000;
        private const int WS_SYSMENU = 0x00080000;
        // Windows 11 原生圆角（DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2）
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public float CornerRadius { get; set; }
        public bool AllowDrag { get; set; }

        /// <summary>系统是否接管了圆角（Win11）。接管后不再用 Region 裁剪，圆角更平滑、窗口动画也正常。</summary>
        private bool _useDwmCorners;

        public RoundedForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            CornerRadius = 18f;
            AllowDrag = true;
            BackColor = Theme.BgBottom;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            ApplyAppIcon();
        }

        private void ApplyAppIcon()
        {
            try
            {
                Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null) Icon = ico;
                Diag.Log("窗口图标: " + (Icon != null ? "已加载" : "缺失"));
            }
            catch { }
        }

        /// <summary>加上最小化/系统菜单样式，任务栏行为与普通窗口一致（含最小化动画）。</summary>
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= WS_MINIMIZEBOX | WS_SYSMENU;
                return cp;
            }
        }

        private const int WM_SETICON = 0x80;
        private Icon _smallIcon;
        private Icon _bigIcon;

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                Icon ico = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (ico != null)
                {
                    Icon = ico;
                    _smallIcon = new Icon(ico, 16, 16);
                    _bigIcon = new Icon(ico, 32, 32);
                    SendMessage(Handle, WM_SETICON, (IntPtr)0, _smallIcon.Handle);
                    SendMessage(Handle, WM_SETICON, (IntPtr)1, _bigIcon.Handle);
                    Diag.Log("任务栏图标已设置");
                }
            }
            catch (Exception ex) { Diag.Log("设置窗口图标失败: " + ex.Message); }
            try
            {
                int pref = 2; // DWMWCP_ROUND
                int hr = DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref pref, sizeof(int));
                _useDwmCorners = (hr == 0);
            }
            catch { _useDwmCorners = false; }

            Diag.Log("窗口圆角: " + (_useDwmCorners ? "系统原生(DWM)" : "Region 回退(Win10)"));
            if (!_useDwmCorners) ApplyRoundedRegion(); // Win10 回退：用 Region 裁剪
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (!_useDwmCorners) ApplyRoundedRegion();
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

        // 手动拖动：不要用 WM_NCLBUTTONDOWN/DefWindowProc 的移动循环——
        // 那会把鼠标按下“吃掉”，导致窗体上自绘的 ✕ / — 按钮点不动。
        private bool _dragging;
        private Point _dragCursorStart;
        private Point _dragWindowStart;
        private bool _dragMoved;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!AllowDrag || e.Button != MouseButtons.Left) return;
            _dragging = true;
            _dragMoved = false;
            _dragCursorStart = Cursor.Position;
            _dragWindowStart = Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;
            if ((e.Button & MouseButtons.Left) == 0) { _dragging = false; return; }
            Point now = Cursor.Position;
            int dx = now.X - _dragCursorStart.X;
            int dy = now.Y - _dragCursorStart.Y;
            if (!_dragMoved && (Math.Abs(dx) > 3 || Math.Abs(dy) > 3)) _dragMoved = true;
            if (_dragMoved) Location = new Point(_dragWindowStart.X + dx, _dragWindowStart.Y + dy);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
            _dragMoved = false;
            base.OnMouseUp(e);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            PaintWindowFrame(e.Graphics);
        }

        protected void PaintWindowFrame(Graphics g)
        {
            Theme.FillGradient(g, new Rectangle(0, 0, Width, Height), Theme.BgTop, Theme.BgBottom);
            using (GraphicsPath p = Theme.Rounded(new RectangleF(0, 0, Width, Height), CornerRadius))
            {
                g.SetClip(p);
                // 左上蓝色柔光
                using (GraphicsPath glow = new GraphicsPath())
                {
                    glow.AddEllipse(new RectangleF(-Width * 0.55f, -Height * 0.55f, Width * 1.7f, Height * 1.05f));
                    using (PathGradientBrush b = new PathGradientBrush(glow))
                    {
                        b.CenterColor = Color.FromArgb(34, 110, 170, 255);
                        b.SurroundColors = new Color[] { Color.Transparent };
                        g.FillPath(b, glow);
                    }
                }
                // 右下青色柔光
                using (GraphicsPath glow2 = new GraphicsPath())
                {
                    glow2.AddEllipse(new RectangleF(Width * 0.35f, Height * 0.55f, Width * 0.9f, Height * 0.7f));
                    using (PathGradientBrush b2 = new PathGradientBrush(glow2))
                    {
                        b2.CenterColor = Color.FromArgb(28, 120, 214, 255);
                        b2.SurroundColors = new Color[] { Color.Transparent };
                        g.FillPath(b2, glow2);
                    }
                }
                g.ResetClip();
                using (Pen border = new Pen(Color.FromArgb(210, 255, 255, 255), 1.2f))
                    g.DrawPath(border, p);
                using (Pen border2 = new Pen(Color.FromArgb(70, 160, 190, 228), 1f))
                    g.DrawPath(border2, p);
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
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
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
            Color fill = Checked ? Theme.Blue : Color.FromArgb(198, 212, 234);
            if (_hover) fill = AccentButton.Lighten(fill, 10);
            Theme.FillRound(g, track, 13f, fill);
            float knob = 20f;
            float x = Checked ? track.Right - knob - 3f : track.X + 3f;
            using (SolidBrush sh = new SolidBrush(Color.FromArgb(28, 60, 90, 140)))
                g.FillEllipse(sh, x, track.Y + 4f, knob, knob);
            using (SolidBrush b = new SolidBrush(Color.White))
                g.FillEllipse(b, x, track.Y + 3f, knob, knob);
        }
    }
}