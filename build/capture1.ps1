$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$exe = Join-Path $root 'dist\ShangtiPower.exe'
$data = Join-Path $root 'build\ui-data'
$shots = Join-Path $root 'build'
if (-not (Test-Path $data)) { New-Item -ItemType Directory -Path $data -Force | Out-Null }
if (-not (Test-Path (Join-Path $data 'config.json'))) {
  '{"CampusId":"1","CampusName":"杨浦校区","BuildingId":"169","BuildingName":"05号楼","FloorId":"5-7","FloorName":"5-7楼","RoomId":"1170","RoomName":"5-718","DisplayName":"杨浦校区 · 05号楼 · 5-7楼 · 5-718","LowThreshold":20,"AutoStart":true}' | Set-Content (Join-Path $data 'config.json') -Encoding UTF8
}
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -ReferencedAssemblies System.Drawing,System.Windows.Forms -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public class U8 {
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int w, int ht, uint fl);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr h, int a, out RECT r, int s);
  public struct RECT { public int Left, Top, Right, Bottom; }
  public static void Shot(IntPtr h, string path) { RECT r; DwmGetWindowAttribute(h, 9, out r, 16); int w=r.Right-r.Left, ht=r.Bottom-r.Top; if(w<=0||ht<=0) return; using(System.Drawing.Bitmap b=new System.Drawing.Bitmap(w,ht)) using(System.Drawing.Graphics g=System.Drawing.Graphics.FromImage(b)){ g.CopyFromScreen(r.Left,r.Top,0,0,b.Size); b.Save(path,System.Drawing.Imaging.ImageFormat.Png);} }
  public static void ShotScreen(string path) { var b2 = System.Windows.Forms.Screen.PrimaryScreen.Bounds; using(System.Drawing.Bitmap b=new System.Drawing.Bitmap(b2.Width,b2.Height)) using(System.Drawing.Graphics g=System.Drawing.Graphics.FromImage(b)){ g.CopyFromScreen(0,0,0,0,b.Size); b.Save(path,System.Drawing.Imaging.ImageFormat.Png);} }
}
"@
Get-Process ShangtiPower -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 400
$env:SHANGTI_DATA_DIR = $data
$p = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 5
$h = $p.MainWindowHandle
if ($h -eq [IntPtr]::Zero) { Write-Host 'no window'; exit 1 }
[void][U8]::SetWindowPos($h, [IntPtr]::Zero, 900, 260, 0, 0, 0x0001 -bor 0x0004)
Start-Sleep -Milliseconds 700
[U8]::Shot($h, (Join-Path $shots 'v-main.png'))
[U8]::ShotScreen((Join-Path $shots 'v-screen.png'))
Write-Host 'captured'
Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
