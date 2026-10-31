using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;

namespace ShangtiPower
{
    /// <summary>首次运行向导：选校区 → 楼栋 → 楼层 → 房间（数据来自学校接口级联）。</summary>
    public class SetupForm : RoundedForm
    {
        private readonly ApiClient _api = new ApiClient();
        private List<LevelDef> _levels;
        private readonly AppConfig _prefill;
        private AppConfig _result;
        private readonly DarkDropdown[] _combos = new DarkDropdown[4];
        private readonly string[] _labels = { "校区", "楼栋", "楼层", "房间" };
        private readonly string[] _types = { "area", "architecture", "floor", "room" };
        private Label _status;
        private AccentButton _btnOk;
        private AccentButton _btnCancel;
        private ToggleSwitch _autoStart;
        private bool _busy;
        private AppConfig _working = new AppConfig();

        public AppConfig Result { get { return _result; } }

        public SetupForm(AppConfig prefill) : this(prefill, false) { }

        public SetupForm(AppConfig prefill, bool skipLoad)
        {
            _prefill = prefill;
            Text = "初次设置";
            ClientSize = new Size(480, 596);
            CornerRadius = 20f;
            AllowDrag = true;

            for (int i = 0; i < 4; i++)
            {
                DarkDropdown cb = new DarkDropdown();
                cb.Font = Theme.F(13f, FontStyle.Regular);
                cb.SetBounds(40, 148 + i * 74, 400, 40);
                cb.Enabled = false;
                int index = i;
                cb.SelectionChanged += delegate { OnComboChanged(index); };
                Controls.Add(cb);
                _combos[i] = cb;
            }

            _status = new Label();
            _status.Text = "正在加载学校数据…";
            _status.ForeColor = Theme.TextSecondary;
            _status.Font = Theme.F(11.5f, FontStyle.Regular);
            _status.BackColor = Color.Transparent;
            _status.SetBounds(40, 452, 400, 22);
            Controls.Add(_status);

            _autoStart = new ToggleSwitch();
            _autoStart.Checked = _prefill == null || _prefill.AutoStart;
            _autoStart.SetBounds(40, 484, 46, 26);
            Controls.Add(_autoStart);

            _btnOk = new AccentButton();
            _btnOk.Text = "保存并开始";
            _btnOk.Enabled = false;
            _btnOk.SetBounds(40, 528, 300, 46);
            _btnOk.Click += delegate { Save(); };
            Controls.Add(_btnOk);

            _btnCancel = new AccentButton();
            _btnCancel.Text = "取消";
            _btnCancel.ColorTop = Color.FromArgb(58, 64, 84);
            _btnCancel.ColorBottom = Color.FromArgb(42, 47, 63);
            _btnCancel.ForeColor = Theme.TextSecondary;
            _btnCancel.Font = Theme.F(13f, FontStyle.Bold);
            _btnCancel.SetBounds(352, 528, 88, 46);
            _btnCancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            Controls.Add(_btnCancel);

            if (!skipLoad) Shown += delegate { LoadLevels(); };
        }


        private void SetStatus(string text, bool error)
        {
            _status.Text = text;
            _status.ForeColor = error ? Theme.Red : Theme.TextSecondary;
        }

        private void LoadLevels()
        {
            if (_busy) return;
            _busy = true;
            SetStatus("正在加载学校数据…", false);
            Program.Log("LoadLevels: 开始请求学校层级");
            ThreadPool.QueueUserWorkItem(delegate(object st)
            {
                try
                {
                    Program.Log("LoadLevels: 请求层级中…");
                    List<LevelDef> levels = _api.GetLevels(15000);
                    Program.Log("LoadLevels: 层级 OK, count=" + levels.Count);
                    if (levels.Count < 4)
                        throw new Exception("学校接口返回的层级不足（" + levels.Count + "）");
                    List<RoomOption> first = _api.GetOptions(levels[0], null, 15000);
                    Program.Log("LoadLevels: 校区选项 OK, count=" + first.Count);
                    SafeInvoke(delegate
                    {
                        _levels = levels;
                        if (_labels.Length == 4)
                        {
                            _labels[0] = string.IsNullOrEmpty(levels[0].Remark) ? "校区" : levels[0].Remark;
                            _labels[1] = string.IsNullOrEmpty(levels[1].Remark) ? "楼栋" : levels[1].Remark;
                            _labels[2] = string.IsNullOrEmpty(levels[2].Remark) ? "楼层" : levels[2].Remark;
                            _labels[3] = string.IsNullOrEmpty(levels[3].Remark) ? "房间" : levels[3].Remark;
                        }
                        Program.Log("LoadLevels: UI 回调开始");
                        FillCombo(0, first);
                        Program.Log("LoadLevels: FillCombo(0) 完成");
                        _combos[0].Enabled = first.Count > 0;
                        _busy = false;
                        // 预填（重新选择房间）时，自动继续级联加载下一级
                        if (_combos[0].SelectedIndex >= 0)
                        {
                            Program.Log("LoadLevels: 预填校区 -> 继续级联");
                            OnComboChanged(0);
                        }
                        SetStatus("请依次选择；选好后点「保存并开始」", false);
                        Invalidate();
                    });
                }
                catch (Exception ex)
                {
                    SafeInvoke(delegate
                    {
                        _busy = false;
                        SetStatus("加载失败：" + ex.Message, true);
                        Program.Log("向导加载失败: " + ex.Message);
                    });
                }
            });
        }

        private void FillCombo(int index, List<RoomOption> options)
        {
            DarkDropdown cb = _combos[index];
            cb.Clear();
            for (int i = 0; i < options.Count; i++) cb.AddItem(options[i].Name, options[i].Key);
            // 只有「重新选择」时才自动选中旧值；首次设置必须由用户明确选择
            string want = PrefillKey(index);
            if (!string.IsNullOrEmpty(want)) cb.SelectByTag(want);
            else cb.SelectedIndex = -1;
        }

        private string PrefillKey(int index)
        {
            if (_prefill == null) return null;
            switch (index)
            {
                case 0: return _prefill.CampusId;
                case 1: return _prefill.BuildingId;
                case 2: return _prefill.FloorId;
                case 3: return _prefill.RoomId;
            }
            return null;
        }

        private void OnComboChanged(int index)
        {
            // 任何 UI 异常都不能让程序闪退，只提示并记录
            try { OnComboChangedCore(index); }
            catch (Exception ex)
            {
                Program.Log("选择变化异常: " + ex.GetType().Name + " -> " + ex.Message);
                SetStatus("出错了，请重新选择：" + ex.Message, true);
            }
        }

        private void OnComboChangedCore(int index)
        {
            if (_levels == null) return;
            if (index < 0 || index > 3) return;
            if (index == 0 && _combos[0].SelectedIndex < 0) return;
            // 必须先选上一级，再选下一级（防止空列表/越界导致闪退）
            if (index > 0 && _combos[index - 1].SelectedIndex < 0) return;
            if (_combos[index].SelectedIndex < 0) return;

            // 记录当前选择
            if (index == 0) { _working.CampusId = SelectedKey(0); _working.CampusName = SelectedName(0); }
            if (index == 1) { _working.BuildingId = SelectedKey(1); _working.BuildingName = SelectedName(1); }
            if (index == 2) { _working.FloorId = SelectedKey(2); _working.FloorName = SelectedName(2); }
            if (index == 3) { _working.RoomId = SelectedKey(3); _working.RoomName = SelectedName(3); }

            // 清空后续
            for (int i = index + 1; i < 4; i++)
            {
                _combos[i].Clear();
                _combos[i].Enabled = false;
            }
            _btnOk.Enabled = false;

            if (index == 3)
            {
                _btnOk.Enabled = !string.IsNullOrEmpty(_working.RoomId);
                SetStatus("房间已选好，点「保存并开始」", false);
                return;
            }

            if (index + 1 >= _levels.Count) return;
            LevelDef next = _levels[index + 1];
            AppConfig snapshot = CloneWorking();
            SetStatus("加载" + _labels[index + 1] + "…", false);
            _busy = true;
            ThreadPool.QueueUserWorkItem(delegate(object st)
            {
                try
                {
                    Program.Log("级联: 请求 " + _labels[index + 1] + " 选项");
                    List<RoomOption> opts = _api.GetOptions(next, snapshot, 15000);
                    Program.Log("级联: " + _labels[index + 1] + " 选项 OK, count=" + opts.Count);
                    SafeInvoke(delegate
                    {
                        _busy = false;
                        FillCombo(index + 1, opts);
                        _combos[index + 1].Enabled = opts.Count > 0;
                        bool prefilled = _combos[index + 1].SelectedIndex >= 0;
                        if (!prefilled)
                            SetStatus(opts.Count > 0 ? ("请选择" + _labels[index + 1]) : ("没有可选的" + _labels[index + 1]), opts.Count == 0);
                        // 预填命中：继续级联下一级（最后一级会自己把状态/按钮设置好）
                        if (prefilled)
                        {
                            Program.Log("级联: 预填 " + _labels[index + 1] + " -> 继续级联");
                            OnComboChanged(index + 1);
                        }
                    });
                }
                catch (Exception ex)
                {
                    SafeInvoke(delegate
                    {
                        _busy = false;
                        SetStatus("加载失败：" + ex.Message, true);
                    });
                }
            });
        }

        /// <summary>自测：按顺序自动选完四级，用于验证级联逻辑不崩溃。</summary>
        internal void DebugTick(System.Windows.Forms.Timer timer)
        {
            try
            {
                if (_levels == null) { Program.Log("selftest: levels 未加载"); return; }
                int sel = -1;
                for (int i = 0; i < 4; i++) if (_combos[i].SelectedIndex >= 0) sel = i;
                if (sel >= 3)
                {
                    timer.Stop();
                    Program.Log("selftest: 四级全部选好 -> " + _combos[0].SelectedText + " / " + _combos[1].SelectedText + " / " + _combos[2].SelectedText + " / " + _combos[3].SelectedText);
                    Program.Emit("SELFTEST-SETUP OK");
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }
                int next = sel + 1;
                DarkDropdown cb = _combos[next];
                Program.Log("selftest tick: sel=" + sel + " next=" + next + " enabled=" + cb.Enabled + " items=" + cb.ItemCount + " sel0='" + _combos[0].SelectedText + "'");
                if (cb.SelectedIndex < 0 && cb.Enabled && cb.ItemCount > 0)
                {
                    Program.Log("selftest: 选中第 " + next + " 级 -> " + cb.GetTexts()[0]);
                    cb.DebugCommit(0);
                    return;
                }
                // 预填时会自动选中第一级但不触发级联，这里手动触发一次
                if (sel >= 0 && sel < 3 && !cb.Enabled && cb.ItemCount == 0)
                {
                    Program.Log("selftest: 手动触发第 " + sel + " 级的级联加载");
                    OnComboChanged(sel);
                }
            }
            catch (Exception ex)
            {
                Program.Log("selftest 异常: " + ex.GetType().Name + " -> " + ex.Message);
                Program.Emit("SELFTEST-SETUP FAIL: " + ex.Message);
                timer.Stop();
                Close();
            }
        }

        private AppConfig CloneWorking()
        {
            AppConfig c = new AppConfig();
            c.CampusId = _working.CampusId;
            c.BuildingId = _working.BuildingId;
            c.FloorId = _working.FloorId;
            c.RoomId = _working.RoomId;
            return c;
        }

        private string SelectedKey(int i)
        {
            object tag = _combos[i].SelectedTag;
            return tag == null ? "" : Convert.ToString(tag);
        }

        private string SelectedName(int i)
        {
            return _combos[i].SelectedText;
        }

        private void SafeInvoke(MethodInvoker action)
        {
            try
            {
                if (IsDisposed) return;
                if (InvokeRequired) BeginInvoke(action); else action();
            }
            catch { }
        }

        private void Save()
        {
            for (int i = 0; i < 4; i++)
            {
                if (SelectedKey(i).Length == 0)
                {
                    SetStatus("请先选好" + _labels[i], true);
                    return;
                }
            }
            AppConfig cfg = new AppConfig();
            cfg.CampusId = SelectedKey(0); cfg.CampusName = SelectedName(0);
            cfg.BuildingId = SelectedKey(1); cfg.BuildingName = SelectedName(1);
            cfg.FloorId = SelectedKey(2); cfg.FloorName = SelectedName(2);
            cfg.RoomId = SelectedKey(3); cfg.RoomName = SelectedName(3);
            cfg.DisplayName = cfg.BuildDisplayName();
            cfg.LowThreshold = _prefill == null ? 20 : _prefill.LowThreshold;
            cfg.AutoStart = _autoStart.Checked;
            cfg.Save();
            Program.SetAutoStart(cfg.AutoStart);
            _result = cfg;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            PaintWindowFrame(g);

            Theme.Text(g, "初次设置", Theme.F(20f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(40, 22, 300, 30), StringAlignment.Near);
            Theme.Text(g, "选择你的宿舍位置，只需设置一次，之后自动记住", Theme.F(12f, FontStyle.Regular),
                Theme.TextSecondary, new RectangleF(40, 58, 400, 22), StringAlignment.Near);

            for (int i = 0; i < 4; i++)
            {
                Theme.Text(g, _labels[i], Theme.F(11.5f, FontStyle.Bold), Theme.TextDim,
                    new RectangleF(40, 124 + i * 74, 200, 20), StringAlignment.Near);
            }

            Theme.Text(g, "开机自动运行", Theme.F(12.5f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(96, 484, 200, 26), StringAlignment.Near);
            Theme.Text(g, "每次开机自动查一次电费并弹窗提醒", Theme.F(10.5f, FontStyle.Regular), Theme.TextDim,
                new RectangleF(96, 504, 300, 18), StringAlignment.Near);

            // 关闭按钮
            Rectangle closeRect = new Rectangle(ClientSize.Width - 44, 10, 30, 30);
            Theme.TextCenter(g, "✕", Theme.F(13f, FontStyle.Bold), Theme.TextSecondary, closeRect);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            Rectangle closeRect = new Rectangle(ClientSize.Width - 44, 10, 30, 30);
            if (closeRect.Contains(e.Location)) { DialogResult = DialogResult.Cancel; Close(); return; }
            base.OnMouseClick(e);
        }
    }
}
