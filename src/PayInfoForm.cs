using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShangtiPower
{
    /// <summary>编辑缴费用的姓名 / 学号（加密保存在本机，仅用于自动填入学校缴费页）。</summary>
    public class PayInfoForm : RoundedForm
    {
        private readonly InputField _name;
        private readonly InputField _no;
        private readonly AccentButton _save;
        private readonly AccentButton _cancel;
        private Rectangle _closeRect;

        public string PayName { get { return _name.Text.Trim(); } }
        public string PayStudentNo { get { return _no.Text.Trim(); } }

        public PayInfoForm(string name, string studentNo)
        {
            Text = "缴费信息";
            ClientSize = new Size(380, 330);
            CornerRadius = 20f;
            AllowDrag = true;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            KeyPreview = true;
            _closeRect = new Rectangle(ClientSize.Width - 44, 10, 30, 30);

            _name = new InputField();
            _name.Placeholder = "例如：张三";
            _name.Text = name ?? "";
            _name.Font = Theme.F(12.5f, FontStyle.Regular);
            _name.SetBounds(24, 104, 332, 42);
            Controls.Add(_name);

            _no = new InputField();
            _no.Placeholder = "选填，学号或缴费账号";
            _no.Text = studentNo ?? "";
            _no.SetBounds(24, 174, 332, 42);
            Controls.Add(_no);

            _save = new AccentButton();
            _save.Text = "保存";
            _save.Radius = 22f;
            _save.SetBounds(24, 266, 160, 44);
            _save.Click += delegate { OnSave(); };
            Controls.Add(_save);

            _cancel = new AccentButton();
            _cancel.Text = "取消";
            _cancel.Radius = 22f;
            _cancel.ColorTop = Color.FromArgb(255, 246, 250, 255);
            _cancel.ColorBottom = Color.FromArgb(255, 232, 242, 255);
            _cancel.BorderColor = Color.FromArgb(200, 214, 243);
            _cancel.ForeColor = Theme.BlueDark;
            _cancel.SetBounds(196, 266, 160, 44);
            _cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_cancel);
        }

        private void OnSave()
        {
            if (PayName.Length == 0)
            {
                MessageBox.Show(this, "请先填写姓名——学校缴费页需要它来匹配你的账户。",
                    "缴费信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _name.Focus();
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Theme.Text(g, "缴费信息", Theme.F(16f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(24, 12, 240, 26), StringAlignment.Near);
            Theme.Text(g, "只保存在这台电脑（加密），点「去缴费」时自动填入学校页面",
                Theme.F(10.5f, FontStyle.Regular), Theme.TextSecondary,
                new RectangleF(24, 44, ClientSize.Width - 48, 20), StringAlignment.Near);

            Theme.Text(g, "姓名", Theme.F(11f, FontStyle.Bold), Theme.TextDim,
                new RectangleF(24, 82, 200, 18), StringAlignment.Near);
            Theme.Text(g, "学号 / 缴费账号（选填）", Theme.F(11f, FontStyle.Bold), Theme.TextDim,
                new RectangleF(24, 152, 260, 18), StringAlignment.Near);

            Theme.Text(g, "不会上传到任何第三方服务器，只在打开学校缴费页时使用",
                Theme.F(9.5f, FontStyle.Regular), Theme.TextDim,
                new RectangleF(24, 228, ClientSize.Width - 48, 20), StringAlignment.Near);

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
            if (_closeRect.Contains(e.Location)) { DialogResult = DialogResult.Cancel; Close(); return; }
            base.OnMouseClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); return; }
            base.OnKeyDown(e);
        }
    }
}
