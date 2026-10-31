#Requires -Version 5.1
<#
  代码签名工具（上体电量）
  ============================================================
  用途：给 dist 里的 exe 签名，让 Windows 识别「发布者」，减少 SmartScreen 拦截。

  三种用法：

  1) 临时方案：自签名 + 本机信任（只对本机有效，适合开发者自己）
     powershell -ExecutionPolicy Bypass -File .\build\sign.ps1 -SelfSign -TrustLocal

  2) 正式方案：用 CA 购买/申请的代码签名证书（.pfx）
     powershell -ExecutionPolicy Bypass -File .\build\sign.ps1 -PfxPath "D:\cert\my.pfx" -PfxPassword "密码"

  3) 只签名不信任（已有证书在系统证书库时）
     powershell -ExecutionPolicy Bypass -File .\build\sign.ps1 -Thumbprint "证书指纹"

  卸载本机自签名信任：
     powershell -ExecutionPolicy Bypass -File .\build\sign.ps1 -RemoveSelfSign
#>
param(
  [string]$PfxPath,
  [string]$PfxPassword,
  [string]$Thumbprint,
  [switch]$SelfSign,
  [switch]$TrustLocal,
  [switch]$RemoveSelfSign,
  [string]$DistDir = '',
  [string]$TimestampUrl = 'http://timestamp.digicert.com',
  [string]$File = ''
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if ([string]::IsNullOrEmpty($DistDir)) { $DistDir = Join-Path $root 'dist' }
$stateDir = Join-Path $root 'build\signing'
if (-not (Test-Path $stateDir)) { New-Item -ItemType Directory -Path $stateDir | Out-Null }
$thumbFile = Join-Path $stateDir 'selftrust-thumbprint.txt'

function Log($m) { Write-Host ('[sign] ' + $m) }

function Get-ExeFiles {
  if (-not (Test-Path $DistDir)) { throw ('找不到 dist 目录：' + $DistDir) }
  if (-not [string]::IsNullOrEmpty($File)) {
    $one = Join-Path $DistDir $File
    if (-not (Test-Path $one)) { throw ('找不到文件：' + $one) }
    return @(Get-Item $one)
  }
  $files = @(Get-ChildItem $DistDir -Filter '*.exe' | Where-Object { $_.Name -notlike '*unsigned*' })
  if ($files.Count -eq 0) { throw 'dist 里没有 exe' }
  return $files
}

# ---------------------------------------------------------------- 移除本机自签名信任
if ($RemoveSelfSign) {
  if (Test-Path $thumbFile) {
    $tp = (Get-Content $thumbFile -Raw).Trim()
    foreach ($storeName in @('Root', 'TrustedPublisher', 'My')) {
      try {
        $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($storeName, 'CurrentUser')
        $store.Open('ReadWrite')
        foreach ($c in @($store.Certificates)) {
          if ($c.Thumbprint -eq $tp) { $store.Remove($c); Log ('已从 CurrentUser\' + $storeName + ' 移除证书') }
        }
        $store.Close()
      } catch { Log ('移除 ' + $storeName + ' 失败: ' + $_.Exception.Message) }
    }
    Remove-Item $thumbFile -Force -ErrorAction SilentlyContinue
    Log '本机自签名信任已移除'
  } else {
    Log '没有找到自签名记录，无需清理'
  }
  return
}

# ---------------------------------------------------------------- 取证书
$cert = $null

if ($PfxPath) {
  if (-not (Test-Path $PfxPath)) { throw ('找不到证书文件：' + $PfxPath) }
  $sec = ConvertTo-SecureString $PfxPassword -AsPlainText -Force
  $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($PfxPath, $sec, 'Exportable,PersistKeySet')
  Log ('已加载证书：' + $cert.Subject + '  有效期至 ' + $cert.NotAfter.ToString('yyyy-MM-dd'))
}
elseif ($Thumbprint) {
  $cert = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
          Where-Object { $_.Thumbprint -eq $Thumbprint } | Select-Object -First 1
  if (-not $cert) { throw ('找不到指纹为 ' + $Thumbprint + ' 的证书') }
}
elseif ($SelfSign) {
  Log '创建自签名代码签名证书（仅用于本机测试，别人电脑不信任）'
  $existing = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
              Where-Object { $_.Subject -like '*ShangtiPower*' -and $_.NotAfter -gt (Get-Date) } |
              Select-Object -First 1
  if ($existing) {
    $cert = $existing
    Log ('复用已有自签名证书：' + $cert.Thumbprint)
  } else {
    $cert = New-SelfSignedCertificate `
      -Type CodeSigningCert `
      -Subject 'CN=ShangtiPower Local Dev, O=ShangtiPower, C=CN' `
      -KeyUsage DigitalSignature `
      -KeyExportPolicy Exportable `
      -NotAfter (Get-Date).AddYears(3) `
      -CertStoreLocation 'Cert:\CurrentUser\My'
    Log ('已创建自签名证书：' + $cert.Thumbprint)
  }

  if ($TrustLocal) {
    # 导入到「当前用户」的根证书 + 受信任发布者（不需要管理员权限）
    $cerPath = Join-Path $stateDir 'ShangtiPowerLocal.cer'
    [System.IO.File]::WriteAllBytes($cerPath, $cert.Export('Cert'))
    foreach ($storeName in @('Root', 'TrustedPublisher')) {
      try {
        $store = New-Object System.Security.Cryptography.X509Certificates.X509Store($storeName, 'CurrentUser')
        $store.Open('ReadWrite')
        $store.Add((New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($cerPath)))
        $store.Close()
        Log ('已导入 CurrentUser\' + $storeName + '（本机信任）')
      } catch {
        Log ('导入 ' + $storeName + ' 失败（若提示无权限，可用管理员运行）: ' + $_.Exception.Message)
      }
    }
    Set-Content -Path $thumbFile -Value $cert.Thumbprint -Encoding ASCII
  }
}
else {
  throw '请指定证书来源：-PfxPath（正式证书） 或 -SelfSign（本机测试自签名）'
}

if (-not $cert) { throw '没有可用证书' }

# ---------------------------------------------------------------- 签名
$exes = Get-ExeFiles
$failed = 0
foreach ($exe in $exes) {
  Log ('签名 ' + $exe.Name + ' …')
  try {
    $result = $null
    try {
      $result = Set-AuthenticodeSignature -FilePath $exe.FullName -Certificate $cert `
        -TimestampServer $TimestampUrl -HashAlgorithm SHA256
    } catch {
      Log ('带时间戳签名失败，改为不带时间戳：' + $_.Exception.Message)
      $result = Set-AuthenticodeSignature -FilePath $exe.FullName -Certificate $cert -HashAlgorithm SHA256
    }
    Log ('  -> ' + $result.Status + ' ' + $result.StatusMessage)
    if ($result.Status -ne 'Valid') { $failed++ }
  } catch {
    Log ('  签名失败：' + $_.Exception.Message)
    $failed++
  }
}

Log ''
Log '--- 校验结果 ---'
foreach ($exe in $exes) {
  $sig = Get-AuthenticodeSignature -FilePath $exe.FullName
  Write-Host ('  ' + $exe.Name + '  ' + $sig.Status + '  发布者: ' + ($(if ($sig.SignerCertificate) { $sig.SignerCertificate.Subject } else { '（无）' })))
}

if ($failed -gt 0) { Log ('完成，但有 ' + $failed + ' 个签名未生效'); exit 1 }
Log '全部签名成功'
if ($SelfSign) {
  Log '提示：这是自签名证书，只有本机（已导入信任）认可；分发给别人需要在对方电脑导入证书，'
  Log '     或购买 CA 代码签名证书后用 -PfxPath 重新签名。'
}
