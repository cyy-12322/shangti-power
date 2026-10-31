using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShangtiPower
{
    /// <summary>调试用：展示下拉框展开后的滚动效果。</summary>
    public class PreviewForm : RoundedForm
    {
        private Timer _timer;

        public PreviewForm()
        {
            Text = "下拉预览";
            ClientSize = new Size(460, 260);
            CornerRadius = 18f;

            DarkDropdown d = new DarkDropdown();
            d.Placeholder = "请选择楼栋";
            d.Font = Theme.F(13f, FontStyle.Regular);
            d.SetBounds(40, 96, 380, 40);
            for (int i = 1; i <= 18; i++) d.AddItem("05号楼 / 测试楼栋 " + i, i);
            Controls.Add(d);

            Shown += delegate
            {
                Timer t = new Timer();
                t.Interval = 700;
                t.Tick += delegate
                {
                    t.Stop();
                    Program.Log("preview: open dropdown popup");
                    d.OpenPopupForTest(true);
                };
                t.Start();
                _timer = t; // 保持引用，避免被回收
            };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PaintWindowFrame(g);
            Theme.Text(g, "下拉滚动测试（18 项）", Theme.F(16f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(40, 34, 380, 30), StringAlignment.Near);
            Theme.Text(g, "滚轮可滚动，右侧有滚动条", Theme.F(11f, FontStyle.Regular), Theme.TextSecondary,
                new RectangleF(40, 64, 380, 20), StringAlignment.Near);
        }
    }
}
