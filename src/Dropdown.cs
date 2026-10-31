using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ShangtiPower
{
    /// <summary>深色自绘下拉选择控件（替代系统白色 ComboBox），支持滚动。</summary>
    public class DarkDropdown : Control
    {
        private class Item
        {
            public string Text;
            public object Tag;
            public Item(string text, object tag) { Text = text; Tag = tag; }
        }

        private readonly List<Item> _items = new List<Item>();
        private int _selected = -1;
        private bool _hover;
        private DropdownPopup _popup;

        public string Placeholder { get; set; }
        public event EventHandler SelectionChanged;

        public DarkDropdown()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                | ControlStyles.SupportsTransparentBackColor, true);
            Placeholder = "请选择";
            Font = Theme.F(13f, FontStyle.Regular);
            Cursor = Cursors.Hand;
            Height = 40;
            BackColor = Color.Transparent;
        }

        public int ItemCount { get { return _items.Count; } }

        public int SelectedIndex
        {
            get { return _selected; }
            set
            {
                int v = (value < -1 || value >= _items.Count) ? -1 : value;
                if (_selected == v) return;
                _selected = v;
                Invalidate();
            }
        }

        public object SelectedTag
        {
            get { return (_selected >= 0 && _selected < _items.Count) ? _items[_selected].Tag : null; }
        }

        public string SelectedText
        {
            get { return (_selected >= 0 && _selected < _items.Count) ? _items[_selected].Text : ""; }
        }

        public void Clear()
        {
            _items.Clear();
            _selected = -1;
            Invalidate();
        }

        public void AddItem(string text, object tag)
        {
            _items.Add(new Item(text, tag));
        }

        public bool SelectByTag(object tag)
        {
            for (int i = 0; i < _items.Count; i++)
                if (Equals(_items[i].Tag, tag)) { SelectedIndex = i; return true; }
            return false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (!Enabled) return;
            OpenPopup(false);
        }

        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        private void OpenPopup(bool keepOpen)
        {
            try
            {
                if (_popup != null && !_popup.IsDisposed) { _popup.Close(); return; }
                if (_items.Count == 0) return;
                _popup = new DropdownPopup(this, keepOpen);
                Point screen = PointToScreen(new Point(0, Height + 4));
                _popup.ShowAt(screen, Width, _selected);
            }
            catch (Exception ex) { Program.Log("打开下拉失败: " + ex.Message); }
        }

        internal void ClosePopup() { _popup = null; }

        /// <summary>仅用于自动化自测：模拟用户点选某一项。</summary>
        internal void DebugCommit(int index) { CommitSelection(index); }

        /// <summary>仅用于自动化截图/调试。</summary>
        internal void OpenPopupForTest(bool keepOpen) { OpenPopup(keepOpen); }

        internal void CommitSelection(int index)
        {
            SelectedIndex = index;
            if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
        }

        internal List<string> GetTexts()
        {
            List<string> list = new List<string>();
            for (int i = 0; i < _items.Count; i++) list.Add(_items[i].Text);
            return list;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);

            Color fill = Enabled ? Theme.Card : Color.FromArgb(28, 32, 44);
            if (_hover && Enabled) fill = Color.FromArgb(44, 49, 66);
            Theme.FillRound(g, r, 10f, fill);
            Theme.StrokeRound(g, r, 10f, _hover && Enabled ? Color.FromArgb(70, 120, 140, 200) : Theme.CardBorder, 1f);

            string text = SelectedIndex >= 0 ? SelectedText : Placeholder;
            Color color = SelectedIndex >= 0
                ? (Enabled ? Theme.TextPrimary : Theme.TextDim)
                : Theme.TextDim;
            Theme.Text(g, text, Font, color,
                new RectangleF(14, 0, Width - 44, Height), StringAlignment.Near);

            float cx = Width - 20f, cy = Height / 2f;
            using (Pen p = new Pen(Enabled ? Theme.TextSecondary : Theme.TextDim, 1.8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                g.DrawLines(p, new PointF[] {
                    new PointF(cx - 5, cy - 2.5f),
                    new PointF(cx, cy + 2.8f),
                    new PointF(cx + 5, cy - 2.5f)
                });
            }
        }
    }

    /// <summary>下拉弹出层（无边框、深色、悬停高亮、滚轮滚动）。</summary>
    internal class DropdownPopup : Form
    {
        private const int ItemHeight = 34;
        private const int PadY = 6;
        private const int MaxVisible = 10;

        private readonly DarkDropdown _owner;
        private readonly List<string> _texts;
        private int _hoverIndex = -1;
        private int _scroll;          // 第一个可见项
        private int _visible;         // 可见数量
        private readonly bool _keepOpen;

        public DropdownPopup(DarkDropdown owner, bool keepOpen)
        {
            _owner = owner;
            _keepOpen = keepOpen;
            _texts = owner.GetTexts();
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = Theme.Card;
            TopMost = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
        }

        public void ShowAt(Point screenPoint, int width, int selectedIndex)
        {
            _visible = Math.Min(_texts.Count, MaxVisible);
            int h = _visible * ItemHeight + PadY * 2;

            // 放不下就往上弹
            Rectangle screen = Screen.FromPoint(screenPoint).WorkingArea;
            if (screenPoint.Y + h > screen.Bottom)
                screenPoint.Y = Math.Max(screen.Top, screenPoint.Y - h - _owner.Height - 8);

            SetBounds(screenPoint.X, screenPoint.Y, width, h);
            ApplyRegion();
            Program.Log("popup.ShowAt " + screenPoint.X + "," + screenPoint.Y + " " + width + "x" + h + " items=" + _texts.Count);

            // 滚动到选中项可见
            if (selectedIndex >= 0)
            {
                if (selectedIndex >= _visible) _scroll = Math.Min(selectedIndex - _visible + 1, Math.Max(0, _texts.Count - _visible));
            }
            Show();
            Activate();
            Program.Log("popup shown visible=" + Visible + " bounds=" + Bounds);
            if (!_keepOpen) Deactivate += delegate { Close(); };
        }

        private void ApplyRegion()
        {
            try
            {
                using (GraphicsPath p = Theme.Rounded(new RectangleF(0, 0, Width, Height), 10f))
                    Region = new Region(p);
            }
            catch { }
        }

        private void ClampScroll()
        {
            int maxScroll = Math.Max(0, _texts.Count - _visible);
            if (_scroll < 0) _scroll = 0;
            if (_scroll > maxScroll) _scroll = maxScroll;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.FillRound(g, new RectangleF(0, 0, Width, Height), 10f, Theme.Card);
            Theme.StrokeRound(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 10f, Theme.CardBorder, 1f);

            ClampScroll();
            int end = Math.Min(_texts.Count, _scroll + _visible);
            for (int i = _scroll; i < end; i++)
            {
                float y = PadY + (i - _scroll) * ItemHeight;
                RectangleF item = new RectangleF(6, y, Width - 12, ItemHeight);
                if (i == _hoverIndex)
                    Theme.FillRound(g, item, 7f, Color.FromArgb(48, 54, 74));
                Theme.Text(g, _texts[i], Theme.F(13f, FontStyle.Regular), Theme.TextPrimary,
                    new RectangleF(item.X + 10, item.Y, item.Width - 14, item.Height), StringAlignment.Near);
            }

            // 滚动条
            if (_texts.Count > _visible)
            {
                float trackTop = PadY;
                float trackH = _visible * ItemHeight;
                float barH = Math.Max(18f, trackH * _visible / _texts.Count);
                float barY = trackTop + (trackH - barH) * _scroll / Math.Max(1, _texts.Count - _visible);
                Theme.FillRound(g, new RectangleF(Width - 9, barY, 5f, barH), 2.5f, Color.FromArgb(190, 150, 165, 195));
            }
        }

        private int IndexAt(int y)
        {
            int rel = (y - PadY) / ItemHeight;
            if (y < PadY || rel < 0 || rel >= _visible) return -1;
            int idx = _scroll + rel;
            return idx < _texts.Count ? idx : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int idx = IndexAt(e.Y);
            if (idx != _hoverIndex) { _hoverIndex = idx; Invalidate(); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (_texts.Count > _visible)
            {
                _scroll -= Math.Sign(e.Delta);
                ClampScroll();
                _hoverIndex = -1;
                Invalidate();
            }
            base.OnMouseWheel(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            int idx = IndexAt(e.Y);
            if (idx >= 0)
            {
                _owner.CommitSelection(idx);
                Close();
                return;
            }
            base.OnMouseClick(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _owner.ClosePopup();
            base.OnFormClosed(e);
        }
    }
}
