# 启动应用、截取窗口、退出（DPI 感知，用于 UI 验证）
# 用法: powershell -ExecutionPolicy Bypass -File .\build\shot.ps1 -Exe <exe> -DataDir <dir> -Out <png>
param(
  [string]$Exe,
  [string]$DataDir,
  [string]$Out,
  [string]$AppArgs = "",
  [int]$ExtraBottom = 0,
  [switch]$Full
)
$ErrorActionPreference = 'Stop'
$env:SHANGTI_DATA_DIR = $DataDir

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinCap2 {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", SetLastError=true)] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT rect, int size);
}
"@
[void][WinCap2]::SetProcessDPIAware()

if ([string]::IsNullOrEmpty($AppArgs)) { $p = Start-Process -FilePath $Exe -PassThru } else { $p = Start-Process -FilePath $Exe -ArgumentList $AppArgs -PassThru }
Start-Sleep -Milliseconds 3000
$p.Refresh()
$h = $p.MainWindowHandle
if ($h -eq [IntPtr]::Zero) {
  Start-Sleep -Milliseconds 2000
  $p.Refresh()
  $h = $p.MainWindowHandle
}
if ($h -eq [IntPtr]::Zero) {
  Get-Content (Join-Path $DataDir 'app.log') -Tail 20 -ErrorAction SilentlyContinue
  Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
  throw 'no window handle'
}

[void][WinCap2]::ShowWindow($h, 5)
[void][WinCap2]::SetWindowPos($h, [IntPtr]::Zero, 120, 120, 0, 0, 0x0001 -bor 0x0004)
[void][WinCap2]::SetForegroundWindow($h)
Start-Sleep -Milliseconds 800

# 优先用 DWM 扩展边框（物理像素，DPI 安全）
$r = New-Object -TypeName 'WinCap2+RECT'
$dwm = [WinCap2]::DwmGetWindowAttribute($h, 9, [ref]$r, 16)
if ($dwm -ne 0) {
  [void][WinCap2]::GetWindowRect($h, [ref]$r)
}
$w = $r.Right - $r.Left
$hh = $r.Bottom - $r.Top + $ExtraBottom
Write-Host ("handle=" + $h + " rect=" + $r.Left + "," + $r.Top + " " + $w + "x" + $hh)

if ($Full) {
  $r.Left = 0
  $r.Top = 0
  $w = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Width
  $hh = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds.Height
}
$bmp = New-Object System.Drawing.Bitmap($w, $hh)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$dir = Split-Path $Out -Parent
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose()
$bmp.Dispose()
Write-Host ("saved " + $Out)

Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Write-Host "app exited"
