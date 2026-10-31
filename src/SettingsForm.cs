using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShangtiPower
{
    /// <summary>设置：换房间、低电量阈值、开机自启、关于/打赏。</summary>
    public class SettingsForm : RoundedForm
    {
        private AppConfig _cfg;
        private readonly DarkDropdown _threshold;
        private readonly ToggleSwitch _autoStart;
        private readonly Label _roomLabel;
        private Rectangle _linkRect;

        public AppConfig Config { get { return _cfg; } }

        public SettingsForm(AppConfig cfg)
        {
            _cfg = cfg;
            Text = "设置";
            ClientSize = new Size(440, 470);
            CornerRadius = 20f;

            _roomLabel = new Label();
            _roomLabel.Text = cfg == null ? "" : cfg.BuildDisplayName();
            _roomLabel.ForeColor = Theme.TextPrimary;
            _roomLabel.Font = Theme.F(13f, FontStyle.Bold);
            _roomLabel.BackColor = Color.Transparent;
            _roomLabel.SetBounds(40, 120, 360, 24);
            Controls.Add(_roomLabel);

            AccentButton btnRepick = new AccentButton();
            btnRepick.Text = "重新选择房间";
            btnRepick.ColorTop = Color.FromArgb(58, 64, 84);
            btnRepick.ColorBottom = Color.FromArgb(42, 47, 63);
            btnRepick.ForeColor = Theme.TextSecondary;
            btnRepick.Font = Theme.F(12.5f, FontStyle.Bold);
            btnRepick.SetBounds(40, 152, 200, 40);
            btnRepick.Click += delegate { Repick(); };
            Controls.Add(btnRepick);

            _threshold = new DarkDropdown();
            _threshold.Placeholder = "选择阈值";
            _threshold.Font = Theme.F(13f, FontStyle.Regular);
            _threshold.SetBounds(40, 248, 160, 40);
            double[] options = new double[] { 10, 15, 20, 30, 50 };
            for (int i = 0; i < options.Length; i++)
                _threshold.AddItem(options[i].ToString("0") + " 度", options[i]);
            double current = cfg == null ? 20 : cfg.LowThreshold;
            _threshold.SelectByTag(current);
            Controls.Add(_threshold);

            _autoStart = new ToggleSwitch();
            _autoStart.Checked = cfg != null && cfg.AutoStart;
            _autoStart.SetBounds(40, 318, 46, 26);
            Controls.Add(_autoStart);

            AccentButton btnSave = new AccentButton();
            btnSave.Text = "保存";
            btnSave.SetBounds(40, 392, 240, 46);
            btnSave.Click += delegate { Save(); };
            Controls.Add(btnSave);

            AccentButton btnCancel = new AccentButton();
            btnCancel.Text = "取消";
            btnCancel.ColorTop = Color.FromArgb(58, 64, 84);
            btnCancel.ColorBottom = Color.FromArgb(42, 47, 63);
            btnCancel.ForeColor = Theme.TextSecondary;
            btnCancel.Font = Theme.F(13f, FontStyle.Bold);
            btnCancel.SetBounds(292, 392, 108, 46);
            btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(btnCancel);

            Shown += delegate { };
        }

        private void Repick()
        {
            using (SetupForm f = new SetupForm(_cfg))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    _cfg = f.Result;
                    _roomLabel.Text = _cfg.BuildDisplayName();
                }
            }
        }

        private void Save()
        {
            if (_cfg == null) return;
            object tag = _threshold.SelectedTag;
            if (tag != null) _cfg.LowThreshold = Convert.ToDouble(tag);
            _cfg.AutoStart = _autoStart.Checked;
            _cfg.DisplayName = _cfg.BuildDisplayName();
            _cfg.Save();
            Program.SetAutoStart(_cfg.AutoStart);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PaintWindowFrame(g);

            Theme.Text(g, "设置", Theme.F(20f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(40, 22, 300, 30), StringAlignment.Near);

            Theme.Text(g, "当前房间", Theme.F(11.5f, FontStyle.Bold), Theme.TextDim,
                new RectangleF(40, 96, 200, 20), StringAlignment.Near);

            Theme.Text(g, "低电量提醒阈值", Theme.F(11.5f, FontStyle.Bold), Theme.TextDim,
                new RectangleF(40, 226, 200, 20), StringAlignment.Near);
            Theme.Text(g, "剩余电量低于这个值时，界面会变红提醒", Theme.F(10.5f, FontStyle.Regular),
                Theme.TextDim, new RectangleF(214, 256, 220, 20), StringAlignment.Near);

            Theme.Text(g, "开机自动运行", Theme.F(12.5f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(96, 318, 200, 26), StringAlignment.Near);
            Theme.Text(g, "开机后自动弹窗显示电费", Theme.F(10.5f, FontStyle.Regular), Theme.TextDim,
                new RectangleF(96, 338, 280, 18), StringAlignment.Near);

            Theme.Text(g, "v" + Program.Version + "  ·  开源项目 · 欢迎打赏 ☕",
                Theme.F(10.5f, FontStyle.Regular), Theme.TextDim,
                new RectangleF(40, 362, 360, 20), StringAlignment.Near);
            _linkRect = new Rectangle(40, 384, 360, 20);
            Theme.Text(g, "GitHub 项目主页（点击打开）", Theme.F(10.5f, FontStyle.Underline), Theme.Blue,
                new RectangleF(_linkRect.X, _linkRect.Y, _linkRect.Width, _linkRect.Height), StringAlignment.Near);

            Rectangle closeRect = new Rectangle(ClientSize.Width - 44, 10, 30, 30);
            Theme.TextCenter(g, "✕", Theme.F(13f, FontStyle.Bold), Theme.TextSecondary, closeRect);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Rectangle closeRect = new Rectangle(ClientSize.Width - 44, 10, 30, 30);
            if (closeRect.Contains(e.Location)) { DialogResult = DialogResult.Cancel; Close(); return; }
            if (_linkRect.Contains(e.Location))
            {
                try { Process.Start(new ProcessStartInfo(Program.RepoUrl) { UseShellExecute = true }); } catch { }
                return;
            }
            base.OnMouseClick(e);
        }
    }
}
