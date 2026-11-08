using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace ShangtiPower
{
    /// <summary>打赏窗口：显示内嵌的赞赏码图片。</summary>
    public class DonateForm : RoundedForm
    {
        private readonly Image _qr;
        private Rectangle _closeRect;

        public DonateForm()
        {
            Text = "支持一下";
            ClientSize = new Size(380, 520);
            CornerRadius = 20f;
            AllowDrag = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            _qr = LoadResourceImage("DonateImage.jpg");
            _closeRect = new Rectangle(ClientSize.Width - 44, 10, 30, 30);
            KeyPreview = true;
        }

        private static Image LoadResourceImage(string name)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                using (Stream s = asm.GetManifestResourceStream(name))
                {
                    if (s == null) return null;
                    using (Image img = Image.FromStream(s)) return new Bitmap(img);
                }
            }
            catch { return null; }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            // 顶部 icon 与标题
            RectangleF logoRect = new RectangleF(22, 10, 24, 24);
            if (Theme.Logo != null)
            {
                using (GraphicsPath clip = Theme.Rounded(logoRect, 7f))
                {
                    g.SetClip(clip);
                    g.DrawImage(Theme.Logo, logoRect);
                    g.ResetClip();
                }
            }
            else
            {
                Theme.Heart(g, new RectangleF(26, 13, 15, 14), Theme.Amber);
            }
            Theme.Text(g, "支持一下", Theme.F(16f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(52, 12, 240, 26), StringAlignment.Near);
            Theme.TextCenter(g, "省了跑充值机的功夫？欢迎请作者喝杯咖啡",
                Theme.F(10.5f, FontStyle.Regular), Theme.TextSecondary,
                new RectangleF(22, 44, ClientSize.Width - 44, 20));

            // 赞赏码卡片
            RectangleF box = new RectangleF(40, 82, ClientSize.Width - 80, ClientSize.Width - 80);
            Theme.Shadow(g, new RectangleF(box.X - 10, box.Y - 10, box.Width + 20, box.Height + 20), 18f, 26);
            Theme.FillRound(g, new RectangleF(box.X - 10, box.Y - 10, box.Width + 20, box.Height + 20), 18f, Color.White);
            Theme.StrokeRound(g, new RectangleF(box.X - 10, box.Y - 10, box.Width + 20, box.Height + 20), 18f, Theme.CardBorder, 1f);
            if (_qr != null)
            {
                g.DrawImage(_qr, box);
            }
            else
            {
                Theme.TextCenter(g, "赞赏码图片缺失", Theme.F(12f, FontStyle.Regular), Theme.TextDim, box);
            }

            Theme.TextCenter(g, "微信扫一扫 · 金额随意 · 感谢支持",
                Theme.F(11f, FontStyle.Regular), Theme.TextDim,
                new RectangleF(0, box.Bottom + 20, ClientSize.Width, 22));

            Theme.TextCenter(g, "点右上角 ✕ 或按 Esc 关闭",
                Theme.F(10f, FontStyle.Regular), Theme.TextDim,
                new RectangleF(0, box.Bottom + 46, ClientSize.Width, 20));

            bool hover = _closeRect.Contains(PointToClient(Cursor.Position));
            if (hover) Theme.FillRound(g, _closeRect, 10f, Color.FromArgb(255, 232, 236));
            Theme.TextCenter(g, "✕", Theme.F(13f, FontStyle.Bold),
                hover ? Theme.RedDark : Theme.TextSecondary, _closeRect);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Cursor = _closeRect.Contains(e.Location) ? Cursors.Hand : Cursors.Default;
            Invalidate(_closeRect);
            base.OnMouseMove(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (_closeRect.Contains(e.Location)) { Close(); return; }
            base.OnMouseClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { Close(); return; }
            base.OnKeyDown(e);
        }
    }
}
