using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShangtiPower
{
    /// <summary>应用入口：单实例、首次配置、命令行自检、开机自启。</summary>
    static class Program
    {
        public const string AppName = "上体电量";
        public const string AppId = "ShangtiPower";
        public const string Version = "1.0.3";
        public const string RepoUrl = "https://github.com/cyy-12322/shangti-power";

        private static string _dataDir;

        /// <summary>配置目录：优先 %APPDATA%\ShangtiPower；不可写时退回到 exe 同级目录（便携模式）。</summary>
        public static string DataDir
        {
            get
            {
                if (_dataDir != null) return _dataDir;

                string env = Environment.GetEnvironmentVariable("SHANGTI_DATA_DIR");
                if (!string.IsNullOrEmpty(env))
                {
                    if (!Directory.Exists(env)) Directory.CreateDirectory(env);
                    _dataDir = env;
                    return _dataDir;
                }

                string roaming = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppId);
                if (IsWritableDir(roaming)) { _dataDir = roaming; return _dataDir; }

                // 便携模式：放在 exe 旁边
                string side = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath) ?? ".", "config");
                if (IsWritableDir(side)) { _dataDir = side; return _dataDir; }

                _dataDir = roaming; // 都写不了，退回默认（日志会记录失败）
                return _dataDir;
            }
        }

        /// <summary>测试目录是否可写（有些安全软件会拦截未签名程序写用户目录）。</summary>
        private static bool IsWritableDir(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write-probe");
                File.WriteAllText(probe, "1");
                File.Delete(probe);
                return true;
            }
            catch { return false; }
        }

        public static string ConfigPath { get { return Path.Combine(DataDir, "config.json"); } }
        public static string LogPath { get { return Path.Combine(DataDir, "app.log"); } }

        /// <summary>命令行输出：winexe 没有控制台，所以同时写文件，便于自动化验证。</summary>
        public static void Emit(string msg)
        {
            try { Console.WriteLine(msg); } catch { }
            try
            {
                File.AppendAllText(Path.Combine(DataDir, "cli.txt"),
                    DateTime.Now.ToString("HH:mm:ss") + "  " + msg + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        public static void Log(string msg)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg;
            try { Console.WriteLine("[log] " + msg); } catch { }
            try
            {
                File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
            try
            {
                // 兜底：即使 %APPDATA% 写不了，也留下痕迹
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "ShangtiPower.fallback.log"),
                    line + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        /// <summary>学校接口只支持 TLS1.2+/1.3，老 .NET 默认协议会握手失败。</summary>
        public static void ConfigureTls()
        {
            try
            {
                ServicePointManager.SecurityProtocol =
                    (SecurityProtocolType)3072   // Tls12
                    | (SecurityProtocolType)12288; // Tls13
            }
            catch { }
            try { ServicePointManager.Expect100Continue = false; } catch { }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

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

        // ------------------------------------------------------------ 开机自启
        // 优先用「启动文件夹快捷方式」：很多安全软件会拦截未签名程序写注册表 Run 键，
        // 但允许往 Start Menu\Startup 放 .lnk，兼容性更好。注册表作为备用。
        public static string StartupLnkPath
        {
            get
            {
                try
                {
                    string dir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                    if (!string.IsNullOrEmpty(dir)) return Path.Combine(dir, "上体电量.lnk");
                }
                catch { }
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\Startup\上体电量.lnk");
            }
        }

        public static string StartupVbsPath
        {
            get
            {
                try
                {
                    string dir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                    if (!string.IsNullOrEmpty(dir)) return Path.Combine(dir, "上体电量.vbs");
                }
                catch { }
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\Startup\上体电量.vbs");
            }
        }

        public static bool IsAutoStartEnabled()
        {
            try { if (File.Exists(StartupLnkPath)) return true; } catch { }
            try { if (File.Exists(StartupVbsPath)) return true; } catch { }
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (k == null) return false;
                    return k.GetValue(AppId) != null;
                }
            }
            catch { return false; }
        }

        /// <summary>备用启动项：写一个隐藏控制台的 .vbs（纯文件写入，比 COM/注册表更容易被放行）。</summary>
        private static bool CreateStartupVbs()
        {
            try
            {
                string exe = Application.ExecutablePath;
                string content =
                    "' 上体电量 开机自启（由程序自动生成，可删除）" + Environment.NewLine +
                    "CreateObject(\"WScript.Shell\").Run \"\"\"" + exe + "\"\"\", 1, False" + Environment.NewLine;
                File.WriteAllText(StartupVbsPath, content, Encoding.UTF8);
                bool ok = File.Exists(StartupVbsPath);
                Log("创建启动项 vbs: " + (ok ? "OK" : "失败") + " -> " + StartupVbsPath);
                return ok;
            }
            catch (Exception ex)
            {
                Log("创建 vbs 异常: " + ex.GetType().Name + " -> " + ex.Message);
                return false;
            }
        }

        /// <summary>用 WScript.Shell COM 在启动文件夹创建快捷方式（不依赖注册表）。</summary>
        private static bool CreateStartupShortcut()
        {
            object shell = null;
            object sc = null;
            try
            {
                Type t = Type.GetTypeFromProgID("WScript.Shell");
                if (t == null) { Log("创建启动项失败：找不到 WScript.Shell"); return false; }
                shell = Activator.CreateInstance(t);
                sc = t.InvokeMember("CreateShortcut",
                    BindingFlags.InvokeMethod, null, shell, new object[] { StartupLnkPath });
                Type sct = sc.GetType();
                string exe = Application.ExecutablePath;
                sct.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { exe });
                sct.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { Path.GetDirectoryName(exe) });
                sct.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { AppName });
                sct.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { exe + ",0" });
                sct.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
                bool ok = File.Exists(StartupLnkPath);
                Log("创建启动项快捷方式: " + (ok ? "OK" : "失败") + " -> " + StartupLnkPath);
                return ok;
            }
            catch (Exception ex)
            {
                Log("创建启动项异常: " + ex.GetType().Name + " -> " + ex.Message);
                return false;
            }
            finally
            {
                try { if (sc != null && Marshal.IsComObject(sc)) Marshal.ReleaseComObject(sc); } catch { }
                try { if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell); } catch { }
            }
        }

        public static void SetAutoStart(bool enabled)
        {
            try
            {
                if (enabled)
                {
                    bool ok = CreateStartupShortcut();
                    if (!ok) ok = CreateStartupVbs();
                    if (!ok)
                    {
                        // 最后备用：注册表 Run 键（可能被安全软件拦截）
                        try
                        {
                            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(
                                @"Software\Microsoft\Windows\CurrentVersion\Run"))
                            {
                                if (k != null)
                                {
                                    k.SetValue(AppId, "\"" + Application.ExecutablePath + "\"", RegistryValueKind.String);
                                    Log("已写入注册表 Run 键（备用方案）");
                                }
                            }
                        }
                        catch (Exception ex) { Log("注册表自启也失败: " + ex.GetType().Name + " -> " + ex.Message); }
                    }
                }
                else
                {
                    try { if (File.Exists(StartupLnkPath)) File.Delete(StartupLnkPath); } catch (Exception ex) { Log("删除启动项失败: " + ex.Message); }
                    try { if (File.Exists(StartupVbsPath)) File.Delete(StartupVbsPath); } catch { }
                    try
                    {
                        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                            @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                        {
                            if (k != null) k.DeleteValue(AppId, false);
                        }
                    }
                    catch { }
                    Log("已关闭开机自启");
                }
                Log("自启状态校验: " + (IsAutoStartEnabled() ? "已开启" : "未开启"));
            }
            catch (Exception ex) { Log("设置自启失败: " + ex.GetType().Name + " -> " + ex.Message); }
        }

        // ------------------------------------------------------------ 卸载
        public static void RunUninstall(bool silent)
        {
            try
            {
                if (!silent)
                {
                    DialogResult r = MessageBox.Show(
                        "确定要卸载「上体电量」吗？\n\n将删除：程序文件、桌面/开始菜单快捷方式、开机自启和本地配置。",
                        "卸载上体电量", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r != DialogResult.Yes) return;
                }

                SetAutoStart(false);

                // 删除快捷方式
                try { File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "上体电量.lnk")); } catch { }
                string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                try { File.Delete(Path.Combine(startMenu, "上体电量.lnk")); } catch { }

                // 删除卸载信息
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(
                        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\ShangtiPower", false);
                }
                catch { }

                // 删除配置
                try { if (Directory.Exists(DataDir)) Directory.Delete(DataDir, true); } catch { }

                string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
                string exe = Application.ExecutablePath;

                if (!silent)
                    MessageBox.Show("已卸载。点击确定后程序将退出，并自动删除程序文件。", "上体电量",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                // 延迟删除自身（程序运行中无法删自己）
                try
                {
                    string cmd = "/c ping -n 3 127.0.0.1 >nul & del /f /q \"" + exe + "\" & rmdir /s /q \"" + exeDir + "\"";
                    Process.Start(new ProcessStartInfo("cmd.exe", cmd) { WindowStyle = ProcessWindowStyle.Hidden, CreateNoWindow = true });
                }
                catch { }
            }
            catch (Exception ex) { Log("卸载失败: " + ex.GetType().Name + " -> " + ex.Message); }
        }
        // ------------------------------------------------------------ 入口
        [STAThread]
        static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                Exception ex = e.ExceptionObject as Exception;
                Log("!!! 未捕获异常: " + (ex == null ? e.ExceptionObject.ToString() : ex.ToString()));
                Emit("UNHANDLED: " + (ex == null ? "?" : ex.GetType().Name + " " + ex.Message));
            };
            try { SetProcessDPIAware(); } catch { }
            Diag.Sink = delegate(string m) { Log(m); };
            ConfigureTls();

            if (HasArg(args, "--install-autostart")) { SetAutoStart(true); Emit(IsAutoStartEnabled() ? "autostart=on" : "autostart=FAILED"); return 0; }
            if (HasArg(args, "--remove-autostart")) { SetAutoStart(false); Emit("autostart=off"); return 0; }
            if (HasArg(args, "--uninstall"))
            {
                RunUninstall(HasArg(args, "--silent"));
                return 0;
            }
            if (HasArg(args, "--version")) { Emit("ShangtiPower " + Version); return 0; }

            if (HasArg(args, "--preview-setup"))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Emit("PREVIEW-SETUP start");
                Application.Run(new SetupForm(AppConfig.Load(), true));
                Emit("PREVIEW-SETUP end");
                return 0;
            }

            if (HasArg(args, "--selftest-setup"))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
                {
                    Log("UI 异常(selftest-setup): " + (e.Exception == null ? "?" : e.Exception.ToString()));
                    Emit("SELFTEST-SETUP UI-EXCEPTION: " + (e.Exception == null ? "?" : e.Exception.Message));
                };
                AppConfig pre = AppConfig.Load();
                Emit("SELFTEST-SETUP start prefill=" + (pre == null ? "(none)" : pre.DisplayName));
                SetupForm f = new SetupForm(pre);
                System.Windows.Forms.Timer t = new System.Windows.Forms.Timer();
                t.Interval = 400;
                t.Tick += delegate { f.DebugTick(t); };
                f.Shown += delegate { t.Start(); };
                Application.Run(f);
                Emit("SELFTEST-SETUP end");
                return 0;
            }

            if (HasArg(args, "--preview-dropdown"))
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new PreviewForm());
                return 0;
            }

            if (HasArg(args, "--diag"))
            {
                Console.WriteLine("user=" + Environment.UserName);
                Console.WriteLine("dataDir=" + DataDir);
                Console.WriteLine("configPath=" + ConfigPath);
                Console.WriteLine("startupLnk=" + StartupLnkPath);
                try
                {
                    string probe = Path.Combine(DataDir, "diag.txt");
                    File.WriteAllText(probe, "ok " + DateTime.Now.ToString("HH:mm:ss"), Encoding.UTF8);
                    Console.WriteLine("write dataDir OK -> " + probe);
                }
                catch (Exception ex) { Console.WriteLine("write dataDir FAIL: " + ex.GetType().Name + " -> " + ex.Message); }
                try
                {
                    string probe2 = Path.Combine(Path.GetTempPath(), "ShangtiPower.diag.txt");
                    File.WriteAllText(probe2, "ok");
                    Console.WriteLine("write temp OK -> " + probe2);
                }
                catch (Exception ex) { Console.WriteLine("write temp FAIL: " + ex.GetType().Name + " -> " + ex.Message); }
                try
                {
                    string probe3 = @"E:\dsh工作区\上体电费自动查询\build\cli\write-test.txt";
                    File.WriteAllText(probe3, "ok");
                    Console.WriteLine("write workspace OK -> " + probe3);
                }
                catch (Exception ex) { Console.WriteLine("write workspace FAIL: " + ex.GetType().Name + " -> " + ex.Message); }
                try
                {
                    System.Security.Principal.WindowsIdentity id = System.Security.Principal.WindowsIdentity.GetCurrent();
                    Console.WriteLine("identity=" + id.Name + " isSystem=" + id.IsSystem);
                }
                catch { }
                return 0;
            }

            if (HasArg(args, "--reset"))
            {
                try { if (File.Exists(ConfigPath)) File.Delete(ConfigPath); } catch { }
                Emit("config removed");
                return 0;
            }

            AppConfig cfg = AppConfig.Load();

            if (HasArg(args, "--selftest"))
            {
                try
                {
                    if (cfg == null || !cfg.IsComplete())
                    {
                        Emit("SELFTEST FAIL: 还没配置房间（先运行 GUI 完成首次设置）");
                        return 3;
                    }
                    ApiClient api = new ApiClient();
                    PowerResult r = api.QueryPower(cfg, 15000);
                    Emit(string.Format(CultureInfo.InvariantCulture,
                        "SELFTEST OK {0} {1:0.00} {2} (查询耗时 {3}ms)",
                        cfg.DisplayName, r.Power, r.Unit, r.ElapsedMs));
                    return 0;
                }
                catch (Exception ex)
                {
                    Emit("SELFTEST FAIL: " + ex.Message);
                    return 1;
                }
            }

            if (HasArg(args, "--dump-levels"))
            {
                try
                {
                    ApiClient api = new ApiClient();
                    List<LevelDef> lv = api.GetLevels(15000);
                    for (int i = 0; i < lv.Count; i++)
                        Emit(lv[i].Rank + " " + lv[i].Type + " " + lv[i].Url + " " + lv[i].Remark);
                    return 0;
                }
                catch (Exception ex) { Emit("FAIL: " + ex.Message); return 1; }
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "Global\\ShangtiPower_SingleInstance", out createdNew))
            {
                if (!createdNew) return 0; // 已经有一个在跑，直接退出

                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                // 任何未处理的 UI 异常都记录 + 提示，避免「闪退」
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
                {
                    Log("UI 异常: " + (e.Exception == null ? "?" : e.Exception.ToString()));
                    try
                    {
                        MessageBox.Show("程序遇到一个问题：\n" + (e.Exception == null ? "未知错误" : e.Exception.Message) +
                            "\n\n详细信息已记录到日志。", "上体电量",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    catch { }
                };

                if (cfg == null || !cfg.IsComplete())
                {
                    using (SetupForm setup = new SetupForm(null))
                    {
                        if (setup.ShowDialog() != DialogResult.OK) return 0;
                        cfg = setup.Result;
                        cfg.Save();
                    }
                }

                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
                {
                    Log("未捕获异常: " + (e.ExceptionObject == null ? "?" : e.ExceptionObject.ToString()));
                };

                Application.Run(new MainForm(cfg));
            }
            return 0;
        }
    }
}
