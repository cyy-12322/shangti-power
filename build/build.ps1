#Requires -Version 5.1
# 一键构建：先编主程序，再把它内嵌进安装包
# 用法： powershell -ExecutionPolicy Bypass -File .\build\build.ps1
param(
  [switch]$DebugBuild,
  [switch]$Sign,
  [string]$PfxPath = '',
  [string]$PfxPassword = ''
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
# 兼容不同 Windows 版本/CI 环境：依次尝试 Framework64 / Framework
$cscCandidates = @(
  (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'),
  (Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe')
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
  Write-Host ("已尝试: " + ($cscCandidates -join ' ; '))
  throw '找不到 .NET Framework 编译器 csc.exe（请确认系统安装了 .NET Framework 4.x）'
}
Write-Host ("使用编译器: " + $csc)

$outDir = Join-Path $root 'dist'
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
$icon = Join-Path $root 'assets\query.ico'

function Compile($outName, $sources, $extraArgs) {
  # 注意：不要把参数包在引号里。PowerShell 7 调用原生程序时按参数逐一传递，
  # 数组元素里多余的引号会被当成文件名的一部分（CS2021: file name is too long or invalid）。
  $cscArgs = @(
    '/nologo',
    '/target:winexe',
    ('/out:' + (Join-Path $outDir $outName)),
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Drawing.dll',
    '/r:System.Web.Extensions.dll',
        '/r:System.Security.dll',
    '/platform:anycpu'
  )
  if (-not $DebugBuild) { $cscArgs += '/optimize+' }
  if (Test-Path $icon) { $cscArgs += ('/win32icon:' + $icon) }
  if ($null -ne $extraArgs -and @($extraArgs).Count -gt 0) { $cscArgs += @($extraArgs) }
  $cscArgs += @($sources)
  Write-Host ("  csc " + (($cscArgs) -join " "))
  & $csc @cscArgs
  if ($LASTEXITCODE -ne 0) { throw ('编译失败：' + $outName + '（退出码 ' + $LASTEXITCODE + '）') }
  $info = Get-Item (Join-Path $outDir $outName)

  Write-Host ('OK  ' + $info.Name + '  ' + [math]::Round($info.Length / 1KB, 1) + ' KB')
}

# 1) 主程序
$appSources = @(Get-ChildItem (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
Write-Host ('[1/2] 编译主程序（' + $appSources.Count + ' 个源文件）')
$donateRes = Join-Path $root 'assets\donate.jpg'
$appResources = @()
if (Test-Path $donateRes) { $appResources += ('/resource:' + $donateRes + ',DonateImage.jpg'); Write-Host '内嵌赞赏码: donate.jpg' }
$logoRes = Join-Path $root 'assets\logo-96.png'
if (Test-Path $logoRes) { $appResources += ('/resource:' + $logoRes + ',LogoImage.png'); Write-Host '内嵌应用图标: logo-96.png' }
Compile '宿电监测.exe' $appSources $appResources
if ($Sign) {
  Write-Host '[sign] 签名主程序'
  if ($PfxPath) { & (Join-Path $PSScriptRoot 'sign.ps1') -PfxPath $PfxPath -PfxPassword $PfxPassword -File '宿电监测.exe' }
  else { & (Join-Path $PSScriptRoot 'sign.ps1') -SelfSign -TrustLocal -File '宿电监测.exe' }
}

# 2) 安装包（内嵌主程序）
$setupSources = @()
$setupSources += (Join-Path $root 'src\Theme.cs')
$setupSources += @(Get-ChildItem (Join-Path $root 'installer') -Filter '*.cs' | ForEach-Object { $_.FullName })
Write-Host ('[2/2] 编译安装包（' + $setupSources.Count + ' 个源文件，内嵌主程序）')
$resource = '/resource:' + (Join-Path $outDir '宿电监测.exe') + ',SudiApp.exe'
Compile '宿电监测安装包.exe' $setupSources @($resource)
if ($Sign) {
  Write-Host '[sign] 签名安装包'
  if ($PfxPath) { & (Join-Path $PSScriptRoot 'sign.ps1') -PfxPath $PfxPath -PfxPassword $PfxPassword -File '宿电监测安装包.exe' }
  else { & (Join-Path $PSScriptRoot 'sign.ps1') -SelfSign -TrustLocal -File '宿电监测安装包.exe' }
}

# 3) 附带使用说明 + 一键解除锁定工具
$unblock = Join-Path $root 'installer\Install-Unblocked.cmd'
if (Test-Path $unblock) {
  Copy-Item $unblock (Join-Path $outDir 'Install-Unblocked.cmd') -Force
  Write-Host 'OK  Install-Unblocked.cmd'
}
$readmeSrc = Join-Path $root 'installer\安装说明.txt'
if (Test-Path $readmeSrc) {
  Copy-Item $readmeSrc (Join-Path $outDir '安装说明.txt') -Force
  Write-Host 'OK  安装说明.txt'
}

# 4) 解除「网络来源」标记（下载来的文件会带，导致 SmartScreen 拦截）
Get-ChildItem $outDir -Filter '*.exe' | ForEach-Object {
  try { Unblock-File -Path $_.FullName } catch { }
}

Write-Host ''
Write-Host '产物：'
Write-Host '  dist\宿电监测.exe           主程序（可直接运行）'
Write-Host '  dist\宿电监测安装包.exe     安装包（发给别人用这个）'
Write-Host '  dist\安装说明.txt            出现 SmartScreen 提示时看这个'

