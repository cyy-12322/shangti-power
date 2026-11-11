using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShangtiPower
{
    /// <summary>去缴费时的操作指引（手动填写模式：官方页面正常登录，程序只负责复制信息）。</summary>
    public class PaymentGuideForm : RoundedForm
    {
        private readonly AppConfig _cfg;
        private readonly AccentButton _copyName;
        private readonly AccentButton _copyNo;
        private readonly AccentButton _ok;
        private Rectangle _closeRect;
        private Timer _resetTimer;

        public PaymentGuideForm(AppConfig cfg)
        {
            _cfg = cfg;
            Text = "去缴费";
            ClientSize = new Size(380, 330);
            CornerRadius = 20f;
            AllowDrag = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            _closeRect = new Rectangle(ClientSize.Width - 44, 10, 30, 30);

            bool hasName = cfg != null && !string.IsNullOrEmpty(cfg.PayName);

            _copyName = MakeButton("复制姓名", 24, 262, 104, 40, false);
            _copyName.Enabled = hasName;
            _copyName.Click += delegate { Copy(cfg == null ? "" : cfg.PayName, _copyName); };
            Controls.Add(_copyName);

            _copyNo = MakeButton("复制学号", 136, 262, 104, 40, false);
            _copyNo.Enabled = cfg != null && !string.IsNullOrEmpty(cfg.PayStudentNo);
            _copyNo.Click += delegate { Copy(cfg == null ? "" : cfg.PayStudentNo, _copyNo); };
            Controls.Add(_copyNo);

            _ok = MakeButton("知道了", 248, 262, 108, 40, true);
            _ok.Click += delegate { Close(); };
            Controls.Add(_ok);

            _resetTimer = new Timer();
            _resetTimer.Interval = 1200;
            _resetTimer.Tick += delegate { _resetTimer.Stop(); _copyName.Text = "复制姓名"; _copyNo.Text = "复制学号"; };
        }

        private static AccentButton MakeButton(string text, int x, int y, int w, int h, bool primary)
        {
            AccentButton b = new AccentButton();
            b.Text = text;
            b.Radius = 20f;
            b.Font = Theme.F(12f, FontStyle.Bold);
            if (!primary)
            {
                b.ColorTop = Color.FromArgb(255, 246, 250, 255);
                b.ColorBottom = Color.FromArgb(255, 232, 242, 255);
                b.BorderColor = Color.FromArgb(200, 214, 243);
                b.ForeColor = Theme.BlueDark;
            }
            b.SetBounds(x, y, w, h);
            return b;
        }

        private void Copy(string text, AccentButton button)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                Clipboard.SetText(text);
                button.Text = "已复制 ✓";
                _resetTimer.Stop();
                _resetTimer.Start();
            }
            catch { }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Theme.Text(g, "去缴费", Theme.F(16f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(24, 12, 240, 26), StringAlignment.Near);
            Theme.Text(g, "浏览器里已打开学校官方缴费页面，按下面步骤填一下就行",
                Theme.F(10.5f, FontStyle.Regular), Theme.TextSecondary,
                new RectangleF(24, 46, ClientSize.Width - 48, 20), StringAlignment.Near);

            string[] steps = new string[]
            {
                "1. 姓名已复制好：点页面里的「姓名」框，按 Ctrl + V",
                "2. 有学号框的话，点下面的「复制学号」再粘贴",
                "3. 依次选择：校区 → 楼栋 → 楼层 → 房间",
                "4. 点「查询」→ 选金额 → 微信/支付宝付款"
            };
            for (int i = 0; i < steps.Length; i++)
            {
                Theme.Text(g, steps[i], Theme.F(10.5f, FontStyle.Regular), Theme.TextPrimary,
                    new RectangleF(24, 82 + i * 25, ClientSize.Width - 48, 20), StringAlignment.Near);
            }

            string room = _cfg == null ? "" : _cfg.BuildDisplayName();
            Theme.FillRound(g, new RectangleF(20, 186, ClientSize.Width - 40, 52), 14f,
                Color.FromArgb(255, 246, 250, 255));
            Theme.Text(g, "你的房间", Theme.F(10f, FontStyle.Regular), Theme.TextDim,
                new RectangleF(34, 193, 200, 16), StringAlignment.Near);
            Theme.Text(g, room, Theme.F(11.5f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(34, 210, ClientSize.Width - 60, 20), StringAlignment.Near);

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
