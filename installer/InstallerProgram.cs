using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShangtiPower.Setup
{
    /// <summary>安装程序入口：支持 /S 静默安装（用于自动化测试）。</summary>
    static class InstallerProgram
    {
        public const string AppName = "宿电监测";
        public const string AppId = "SudianMonitor";
        public const string AppVersion = "1.6.0";
        public const string AppExeName = "宿电监测.exe";

        public static string LogPath
        {
            get
            {
                string dir = Path.Combine(Path.GetTempPath(), "ShangtiPowerSetup");
                try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
                return Path.Combine(dir, "install.log");
            }
        }

        public static void Log(string msg)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg;
            try { Console.WriteLine(line); } catch { }
            try { File.AppendAllText(LogPath, line + Environment.NewLine); } catch { }
        }

        public static string DefaultInstallDir
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("SHANGTI_INSTALL_DIR");
                if (!string.IsNullOrEmpty(env)) return env;
                string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(Path.Combine(baseDir, "Programs"), AppId);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        static int Main(string[] args)
        {
            try { SetProcessDPIAware(); } catch { }
            ServicePointManagerSecurityWorkaround();
            bool silent = HasArg(args, "--silent") || HasArg(args, "/S");
            string dir = ArgValue(args, "--dir");
            if (string.IsNullOrEmpty(dir)) dir = DefaultInstallDir;
            bool desktop = !HasArg(args, "--no-desktop");
            bool autostart = !HasArg(args, "--no-autostart");
            string d = ArgValue(args, "--desktop");
            if (d == "0") desktop = false;
            string a = ArgValue(args, "--autostart");
            if (a == "0") autostart = false;
            bool noRun = HasArg(args, "--no-run");

            if (silent)
            {
                try
                {
                    InstallEngine engine = new InstallEngine(dir, desktop, autostart);
                    engine.Run(null);
                    for (int i = 0; i < engine.Warnings.Count; i++) Log("[warn] " + engine.Warnings[i]);
                    Log("SILENT INSTALL OK -> " + engine.TargetDir);
                    return 0;
                }
                catch (Exception ex)
                {
                    Log("SILENT INSTALL FAIL: " + ex.GetType().Name + " -> " + ex.Message);
                    return 1;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
            {
                Log("UI 异常: " + (e.Exception == null ? "?" : e.Exception.ToString()));
                try
                {
                    MessageBox.Show("安装程序遇到问题：\n" + (e.Exception == null ? "未知错误" : e.Exception.Message),
                        AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch { }
            };

            int previewPage = 0;
            string pv = ArgValue(args, "--preview-page");
            if (!string.IsNullOrEmpty(pv)) int.TryParse(pv, out previewPage);
            using (InstallerForm f = new InstallerForm(dir, desktop, autostart, !noRun, previewPage))
            {
                if (ArgValue(args, "--preview-done") == "1") f.PreviewFinished(dir);
                Application.Run(f);
            }
            return 0;
        }

        private static void ServicePointManagerSecurityWorkaround()
        {
            try
            {
                System.Net.ServicePointManager.SecurityProtocol =
                    (System.Net.SecurityProtocolType)3072 | (System.Net.SecurityProtocolType)12288;
            }
            catch { }
        }

        private static bool HasArg(string[] args, string name)
        {
            if (args == null) return false;
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string ArgValue(string[] args, string name)
        {
            if (args == null) return null;
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }

    /// <summary>实际安装动作（GUI 与静默共用）。</summary>
    public class InstallEngine
    {
        public string TargetDir;
        public bool CreateDesktopShortcut;
        public bool EnableAutoStart;
        public readonly List<string> Warnings = new List<string>();
        public string InstalledExe { get { return Path.Combine(TargetDir, InstallerProgram.AppExeName); } }

        public InstallEngine(string dir, bool desktop, bool autostart)
        {
            TargetDir = dir;
            CreateDesktopShortcut = desktop;
            EnableAutoStart = autostart;
        }

        public void Run(Action<string> progress)
        {
            Report(progress, "准备安装目录…");
            string err = null;
            if (!TryPrepare(TargetDir, out err))
            {
                // 用户可能选了 Program Files / 系统盘根目录等需要管理员权限的位置
                string fallback = InstallerProgram.DefaultInstallDir;
                Warnings.Add("所选目录无法写入（" + err + "），已自动改为安装到：" + fallback);
                InstallerProgram.Log("[warn] target not writable: " + TargetDir + " -> " + err + "，fallback " + fallback);
                TargetDir = fallback;
                if (!TryPrepare(TargetDir, out err))
                    throw new Exception("安装失败：无法创建安装目录。\n" + TargetDir + "\n" + err);
            }

            Report(progress, "释放程序文件…");
            ExtractApp();

            Report(progress, "创建开始菜单快捷方式…");
            string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
            CreateShortcut(Path.Combine(startMenu, "宿电监测.lnk"), "宿电监测");

            if (CreateDesktopShortcut)
            {
                Report(progress, "创建桌面快捷方式…");
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                CreateShortcut(Path.Combine(desktop, "宿电监测.lnk"), "宿电监测（桌面）");
            }

            Report(progress, "配置开机自启…");
            string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string startupLnk = Path.Combine(startup, "宿电监测.lnk");
            if (EnableAutoStart)
            {
                CreateShortcut(startupLnk, "宿电监测（开机自启）");
            }
            else
            {
                try { if (File.Exists(startupLnk)) File.Delete(startupLnk); } catch { }
            }

            Report(progress, "写入卸载信息…");
            WriteUninstallEntry();

            Report(progress, "完成");
        }

        private void Report(Action<string> progress, string text)
        {
            InstallerProgram.Log("[step] " + text);
            if (progress != null) progress(text);
        }

        private string ExePath { get { return Path.Combine(TargetDir, InstallerProgram.AppExeName); } }

        private void ExtractApp()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            string resourceName = "SudiApp.exe";
            using (Stream s = asm.GetManifestResourceStream(resourceName))
            {
                if (s == null)
                    throw new Exception("安装包损坏：找不到内嵌程序（" + resourceName + "）");
                using (FileStream fs = new FileStream(ExePath, FileMode.Create, FileAccess.Write))
                {
                    byte[] buf = new byte[81920];
                    int n;
                    while ((n = s.Read(buf, 0, buf.Length)) > 0) fs.Write(buf, 0, n);
                }
            }
        }

        /// <summary>尝试创建目录并验证可写；失败时给出原因。</summary>
        private bool TryPrepare(string dir, out string error)
        {
            error = null;
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write-probe");
                File.WriteAllText(probe, "1");
                File.Delete(probe);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + " -> " + ex.Message;
                return false;
            }
        }

        private void CreateShortcut(string lnkPath, string description)
        {
            object shell = null;
            object sc = null;
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) { Warnings.Add("系统缺少 WScript.Shell，无法创建快捷方式"); return; }
                shell = Activator.CreateInstance(t);
                sc = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                Type sct = sc.GetType();
                sct.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { ExePath });
                sct.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { TargetDir });
                sct.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { description });
                sct.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { ExePath + ",0" });
                sct.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
                InstallerProgram.Log("[lnk] " + lnkPath);
            }
            catch (Exception ex)
            {
                Warnings.Add("创建快捷方式失败（" + Path.GetFileName(lnkPath) + "）：" + ex.Message +
                             "（可能被安全软件拦截）");
                InstallerProgram.Log("[lnk] FAIL " + lnkPath + " : " + ex.Message);
            }
            finally
            {
                try { if (sc != null && System.Runtime.InteropServices.Marshal.IsComObject(sc)) System.Runtime.InteropServices.Marshal.ReleaseComObject(sc); } catch { }
                try { if (shell != null && System.Runtime.InteropServices.Marshal.IsComObject(shell)) System.Runtime.InteropServices.Marshal.ReleaseComObject(shell); } catch { }
            }
        }

        private void WriteUninstallEntry()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + InstallerProgram.AppId))
                {
                    if (k == null) return;
                    k.SetValue("DisplayName", InstallerProgram.AppName);
                    k.SetValue("DisplayVersion", InstallerProgram.AppVersion);
                    k.SetValue("Publisher", "ShangtiPower");
                    k.SetValue("InstallLocation", TargetDir);
                    k.SetValue("DisplayIcon", ExePath);
                    k.SetValue("UninstallString", "\"" + ExePath + "\" --uninstall");
                    k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                Warnings.Add("写入卸载信息失败：" + ex.Message);
            }
        }
    }
}
