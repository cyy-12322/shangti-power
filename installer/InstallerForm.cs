using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace ShangtiPower.Setup
{
    /// <summary>安装向导：许可协议 → 安装位置 → 安装选项 → 完成。</summary>
    public class InstallerForm : RoundedForm
    {
        [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

        private const string LicenseText =
@"宿电监测（SudianMonitor） v1.0.0

【开源协议】MIT License
Copyright (c) 2026 ShangtiPower contributors

特此免费授予任何获得本软件及相关文档文件副本的人不受限制地处置本软件的权利，
包括但不限于使用、复制、修改、合并、发布、分发、再许可和/或销售本软件副本，
并允许获得本软件的人这样做，但须满足以下条件：

上述版权声明和本许可声明应包含在本软件的所有副本或主要部分中。

本软件按「原样」提供，不附带任何明示或暗示的担保，包括但不限于适销性、
特定用途适用性和非侵权性的担保。在任何情况下，作者或版权持有人均不对任何
索赔、损害或其他责任负责，无论是在合同诉讼、侵权行为还是其他方面，均由本
软件或本软件的使用或其他交易引起、产生或与之相关。

【免责声明】
· 本项目为个人学习作品，非上海体育大学官方工具。
· 数据来自学校公开的电费查询页面，作者不对数据的准确性、及时性、可用性负责。
· 请仅查询本人宿舍，勿用于批量采集或任何商业用途。
· 使用本软件产生的任何后果由使用者自行承担。

【隐私说明】
· 本软件不需要账号密码，不采集、不上传任何个人信息。
· 仅在查询时访问学校电费接口；宿舍编号保存在本机配置文件中。
· 除查询外不进行任何网络通信。

点击下方开关表示你已阅读并同意上述全部内容。";

        private enum Page { License = 0, Folder = 1, Options = 2, Progress = 3 }

        private Page _page = Page.License;
        private readonly string _defaultDir;
        private string _installDir;
        private bool _desktop;
        private bool _autostart;
        private bool _launchAfter;
        private bool _installing;
        private bool _installed;

        private readonly List<Control>[] _pageControls = new List<Control>[4];
        private RichTextBox _license;
        private ToggleSwitch _agree;
        private TextBox _dirBox;
        private ToggleSwitch _desktopToggle;
        private ToggleSwitch _deleteToggle;
        private bool _deleteInstaller = true;
        private ToggleSwitch _launchToggle;
        private Label _progressLabel;
        private Label _warnLabel;
        private AccentButton _btnBack;
        private AccentButton _btnNext;
        private Rectangle _closeRect;

        public InstallerForm(string defaultDir, bool desktop, bool autostart, bool launchAfter) : this(defaultDir, desktop, autostart, launchAfter, 0) { }

        public InstallerForm(string defaultDir, bool desktop, bool autostart, bool launchAfter, int previewPage)
        {
            _defaultDir = defaultDir;
            _installDir = defaultDir;
            _desktop = desktop;
            _autostart = false; // 安装器不再负责开机自启，改由应用内设置控制
            _launchAfter = launchAfter;

            Text = "安装 宿电监测";
            ClientSize = new Size(640, 600);
            CornerRadius = 20f;
            for (int i = 0; i < 4; i++) _pageControls[i] = new List<Control>();

            BuildLicensePage();
            BuildFolderPage();
            BuildOptionsPage();
            BuildProgressPage();
            BuildFooter();

            int page = previewPage < 0 ? 0 : (previewPage > 3 ? 3 : previewPage);
            ShowPage((Page)page);
        }

        // ---------------------------------------------------------------- 页面构建
        private void Register(Page page, Control c)
        {
            Controls.Add(c);
            _pageControls[(int)page].Add(c);
            c.Visible = false;
        }

        private void BuildLicensePage()
        {
            _license = new RichTextBox();
            _license.ReadOnly = true;
            _license.BorderStyle = BorderStyle.None;
            _license.BackColor = Theme.Card;
            _license.ForeColor = Theme.TextSecondary;
            _license.Font = Theme.F(11.5f, FontStyle.Regular);
            _license.ScrollBars = RichTextBoxScrollBars.Vertical;
            _license.SetBounds(40, 108, 560, 300);
            _license.Text = LicenseText.Replace("v1.0.0", "v" + InstallerProgram.AppVersion);
            Register(Page.License, _license);

            _agree = new ToggleSwitch();
            _agree.Checked = false;
            _agree.SetBounds(40, 424, 46, 26);
            _agree.CheckedChanged += delegate { UpdateButtons(); };
            Register(Page.License, _agree);
        }

        private void BuildFolderPage()
        {
            _dirBox = new TextBox();
            _dirBox.BackColor = Theme.Card;
            _dirBox.ForeColor = Theme.TextPrimary;
            _dirBox.BorderStyle = BorderStyle.FixedSingle;
            _dirBox.Font = Theme.F(12.5f, FontStyle.Regular);
            _dirBox.SetBounds(40, 168, 430, 32);
            _dirBox.ReadOnly = true;
            _dirBox.TabStop = false;
            _dirBox.Text = _installDir;
            _dirBox.TextChanged += delegate { _installDir = _dirBox.Text.Trim(); };
            Register(Page.Folder, _dirBox);

            AccentButton browse = new AccentButton();
            browse.Text = "浏览…";
            browse.ColorTop = Theme.SecondaryTop;
            browse.ColorBottom = Theme.SecondaryBottom;
            browse.ForeColor = Theme.SecondaryText;
            browse.BorderColor = Theme.CardBorder;
            browse.Font = Theme.F(12.5f, FontStyle.Bold);
            browse.SetBounds(482, 166, 118, 36);
            browse.Click += delegate { BrowseFolder(); };
            Register(Page.Folder, browse);
        }

        private void BuildOptionsPage()
        {
            _desktopToggle = new ToggleSwitch();
            _desktopToggle.Checked = _desktop;
            _desktopToggle.SetBounds(40, 168, 46, 26);
            _desktopToggle.CheckedChanged += delegate { _desktop = _desktopToggle.Checked; };
            Register(Page.Options, _desktopToggle);

            _deleteToggle = new ToggleSwitch();
            _deleteToggle.Checked = _deleteInstaller;
            _deleteToggle.SetBounds(40, 256, 46, 26);
            _deleteToggle.CheckedChanged += delegate { _deleteInstaller = _deleteToggle.Checked; };
            Register(Page.Options, _deleteToggle);
        }

        private void BuildProgressPage()
        {
            _progressLabel = new Label();
            _progressLabel.BackColor = Color.Transparent;
            _progressLabel.ForeColor = Theme.TextPrimary;
            _progressLabel.Font = Theme.F(13f, FontStyle.Bold);
            _progressLabel.Text = "正在准备…";
            _progressLabel.SetBounds(40, 180, 560, 26);
            Register(Page.Progress, _progressLabel);



            _warnLabel = new Label();
            _warnLabel.BackColor = Color.Transparent;
            _warnLabel.ForeColor = Theme.Amber;
            _warnLabel.Font = Theme.F(10.5f, FontStyle.Regular);
            _warnLabel.SetBounds(40, 360, 560, 110);
            _warnLabel.Visible = false;
            Register(Page.Progress, _warnLabel);
        }

        private void BuildFooter()
        {
            _btnBack = new AccentButton();
            _btnBack.Text = "上一步";
            _btnBack.ColorTop = Theme.SecondaryTop;
            _btnBack.ColorBottom = Theme.SecondaryBottom;
            _btnBack.ForeColor = Theme.SecondaryText;
            _btnBack.BorderColor = Theme.CardBorder;
            _btnBack.Font = Theme.F(13f, FontStyle.Bold);
            _btnBack.SetBounds(40, 524, 130, 46);
            _btnBack.Click += delegate { GoBack(); };
            Controls.Add(_btnBack);

            _btnNext = new AccentButton();
            _btnNext.Text = "下一步";
            _btnNext.SetBounds(470, 524, 130, 46);
            _btnNext.Click += delegate { GoNext(); };
            Controls.Add(_btnNext);

            _closeRect = new Rectangle(ClientSize.Width - 44, 10, 30, 30);
        }

        // ---------------------------------------------------------------- 页面切换
        private void ShowPage(Page page)
        {
            _page = page;
            for (int i = 0; i < 4; i++)
                foreach (Control c in _pageControls[i]) c.Visible = (i == (int)page);
            _btnBack.Visible = (page == Page.Folder || page == Page.Options);
            _btnNext.Visible = (page != Page.Progress) || _installed;
            UpdateButtons();
            Invalidate();
        }

        private void UpdateButtons()
        {
            if (_page == Page.License)
            {
                _btnNext.Text = "下一步";
                _btnNext.Enabled = _agree.Checked;
            }
            else if (_page == Page.Folder)
            {
                _btnNext.Text = "下一步";
                _btnNext.Enabled = _installDir.Length > 3;
            }
            else if (_page == Page.Options)
            {
                _btnNext.Text = "开始安装";
                _btnNext.Enabled = !_installing;
            }
            else
            {
                _btnNext.Text = "完成";
                _btnNext.Enabled = _installed;
            }
        }

        private void GoBack()
        {
            if (_installing) return;
            if (_page == Page.Folder) ShowPage(Page.License);
            else if (_page == Page.Options) ShowPage(Page.Folder);
        }

        private void GoNext()
        {
            if (_page == Page.License) { ShowPage(Page.Folder); return; }
            if (_page == Page.Folder) { ShowPage(Page.Options); return; }
            if (_page == Page.Options) { StartInstall(); return; }
            if (_page == Page.Progress && _installed) { Finish(); }
        }

        private void BrowseFolder()
        {
            using (FolderBrowserDialog dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择安装位置（建议放在非系统盘）";
                dlg.ShowNewFolderButton = true;
                dlg.SelectedPath = _installDir;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    // 用户可能选到磁盘根目录，自动补一层目录名
                    string sel = dlg.SelectedPath;
                    if (Path.GetFileName(sel) != InstallerProgram.AppId) sel = Path.Combine(sel, InstallerProgram.AppId);
                    _installDir = sel;
                    _dirBox.Text = sel;
                }
            }
        }

        // ---------------------------------------------------------------- 安装
        private void StartInstall()
        {
            if (_installing) return;
            _installing = true;
            ShowPage(Page.Progress);
            _progressLabel.Text = "正在安装…";

            InstallEngine engine = new InstallEngine(_installDir, _desktop, _autostart);
            ThreadPool.QueueUserWorkItem(delegate(object st)
            {
                try
                {
                    engine.Run(delegate(string text) { SafeInvoke(delegate { _progressLabel.Text = text; }); });
                    SafeInvoke(delegate { OnInstalled(engine); });
                }
                catch (Exception ex)
                {
                    InstallerProgram.Log("安装失败: " + ex);
                    SafeInvoke(delegate
                    {
                        _installing = false;
                        _progressLabel.Text = "安装失败：" + ex.Message + "\n\n日志：" + InstallerProgram.LogPath;
                        _progressLabel.ForeColor = Theme.Red;
                        _btnNext.Visible = false;
                        _btnBack.Visible = true;
                    });
                }
            });
        }

        private void OnInstalled(InstallEngine engine)
        {
            _installing = false;
            _installed = true;
            _installDir = engine.TargetDir;
            _btnNext.Text = "完成";
            _btnNext.Glyph = "check";
            _btnNext.Visible = true;
            _btnNext.Enabled = true;
            _progressLabel.ForeColor = Theme.Green;
            _progressLabel.Text = "安装完成 ✓";
            if (engine.Warnings.Count > 0)
            {
                _warnLabel.Text = "提示：\n· " + string.Join("\n· ", engine.Warnings.ToArray());
                _warnLabel.Visible = true;
            }
            UpdateButtons();
            Invalidate();
        }

        /// <summary>仅用于截图/预览「安装完成」状态。</summary>
        public void PreviewFinished(string dir)
        {
            _installing = false;
            _installed = true;
            _installDir = dir;
            ShowPage(Page.Progress);
            _progressLabel.ForeColor = Theme.Green;
            _progressLabel.Text = "安装完成 ✓";
            _btnNext.Text = "完成";
            _btnNext.Glyph = "check";
            _btnNext.Visible = true;
            _btnNext.Enabled = true;
            Invalidate();
        }

        private void Finish()
        {

            // 删除安装包（可选）：延迟到本进程退出后再删，避免文件被占用
            if (_deleteInstaller)
            {
                try
                {
                    string self = Application.ExecutablePath;
                    string cmd = "/c ping -n 3 127.0.0.1 >nul & del /f /q \"" + self + "\"";
                    Process.Start(new ProcessStartInfo("cmd.exe", cmd)
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true
                    });
                }
                catch { }
            }
            Close();
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

        // ---------------------------------------------------------------- 绘制
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            string title = "安装 宿电监测";
            string subtitle = "一个查宿舍剩余电量的小工具 · v" + InstallerProgram.AppVersion;
            switch (_page)
            {
                case Page.License: title = "许可协议"; subtitle = "请阅读并同意以下协议后继续"; break;
                case Page.Folder: title = "选择安装位置"; subtitle = "程序约 0.1 MB，默认装到当前用户目录，无需管理员权限"; break;
                case Page.Options: title = "安装选项"; subtitle = "选择你需要的快捷方式与安装包清理方式"; break;
                case Page.Progress: title = _installed ? "安装完成" : "正在安装"; subtitle = _installed ? "感谢使用，欢迎到项目主页提建议" : "请稍候…"; break;
            }
            Theme.Text(g, title, Theme.F(19f, FontStyle.Bold), Theme.TextPrimary,
                new RectangleF(40, 20, 400, 30), StringAlignment.Near);
            Theme.Text(g, subtitle, Theme.F(11.5f, FontStyle.Regular), Theme.TextSecondary,
                new RectangleF(40, 52, 560, 22), StringAlignment.Near);

            // 协议页开关文字
            if (_page == Page.License)
            {
                Theme.Text(g, "我已阅读并同意上述全部内容", Theme.F(12.5f, FontStyle.Bold), Theme.TextPrimary,
                    new RectangleF(96, 424, 400, 26), StringAlignment.Near);
                Theme.Text(g, "不同意将无法继续安装", Theme.F(10.5f, FontStyle.Regular), Theme.TextDim,
                    new RectangleF(96, 444, 400, 18), StringAlignment.Near);
            }

            if (_page == Page.Folder)
            {
                Theme.Text(g, "安装目录", Theme.F(11.5f, FontStyle.Bold), Theme.TextDim,
                    new RectangleF(40, 138, 200, 20), StringAlignment.Near);
                Theme.Text(g, "提示：装到 D 盘等非系统盘也可以，重装系统不会丢。", Theme.F(10.5f, FontStyle.Regular),
                    Theme.TextDim, new RectangleF(40, 214, 560, 20), StringAlignment.Near);
            }

            if (_page == Page.Options)
            {
                Theme.Text(g, "创建桌面快捷方式", Theme.F(12.5f, FontStyle.Bold), Theme.TextPrimary,
                    new RectangleF(96, 168, 400, 26), StringAlignment.Near);
                Theme.Text(g, "在桌面放一个图标，双击就能查电量", Theme.F(10.5f, FontStyle.Regular), Theme.TextDim,
                    new RectangleF(96, 188, 460, 18), StringAlignment.Near);
                Theme.Text(g, "安装完成后删除安装包", Theme.F(12.5f, FontStyle.Bold), Theme.TextPrimary,
                    new RectangleF(96, 256, 400, 26), StringAlignment.Near);
                Theme.Text(g, "安装成功后自动删除下载的安装包文件，不占空间", Theme.F(10.5f, FontStyle.Regular),
                    Theme.TextDim, new RectangleF(96, 276, 500, 18), StringAlignment.Near);
            }

            if (_page == Page.Progress && _installed)
            {
                Theme.Text(g, "安装目录：" + _installDir, Theme.F(11f, FontStyle.Regular), Theme.TextDim,
                    new RectangleF(40, 214, 560, 20), StringAlignment.Near);
                Theme.TextCenter(g, "点右下角「完成」即可退出安装程序",
                    Theme.F(11f, FontStyle.Regular), Theme.TextSecondary,
                    new RectangleF(40, 452, 560, 22));

            }

            // 关闭按钮
            bool hover = _closeRect.Contains(PointToClient(Cursor.Position));
            if (hover) Theme.FillRound(g, _closeRect, 8f, Color.FromArgb(200, 198, 48, 62));
            Theme.TextCenter(g, "✕", Theme.F(13f, FontStyle.Bold),
                hover ? Theme.RedDark : Theme.TextSecondary, _closeRect);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            bool hover = _closeRect.Contains(e.Location);
            Cursor = hover ? Cursors.Hand : Cursors.Default;
            Invalidate(_closeRect);
            base.OnMouseMove(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            if (_closeRect.Contains(e.Location))
            {
                if (_installing) return;
                Close();
                return;
            }
            base.OnMouseClick(e);
        }
    }
}
