[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SignedBundlePath,
    [Parameter(Mandatory)][string]$PublicCertificatePath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$DisplayVersion,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$PackageVersion,
    [Parameter(Mandatory)][string]$SpatialViewerCommit,
    [Parameter(Mandatory)][string]$CadCoreCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $PackageVersion.StartsWith("$DisplayVersion.")) { throw 'Package version must extend the display version.' }
foreach ($path in @($SignedBundlePath, $PublicCertificatePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing input: $path" }
}

$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Resolve-Path -LiteralPath $PublicCertificatePath))
if ($certificate.HasPrivateKey -or $certificate.Subject -cne 'CN=AppPublisher') { throw 'Acceptance package requires the public AppPublisher certificate.' }

$output = [IO.Path]::GetFullPath($OutputDirectory)
$root = Join-Path $output "SpatialViewer-v$DisplayVersion-CadCore-Acceptance-x64"
if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
New-Item -ItemType Directory -Force -Path $root | Out-Null

$bundleName = "SpatialViewer_${PackageVersion}_CadCoreAcceptance_x64.msixbundle"
$certificateName = 'SpatialViewer-CadCore-Acceptance.cer'
Copy-Item -LiteralPath $SignedBundlePath -Destination (Join-Path $root $bundleName) -Force
Copy-Item -LiteralPath $PublicCertificatePath -Destination (Join-Path $root $certificateName) -Force

$install = @"
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
`$ErrorActionPreference = 'Stop'
function Test-IsAdministrator {
    return ([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}
if (-not (Test-IsAdministrator)) {
    `$arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -File ```"`$PSCommandPath```""
    `$child = Start-Process powershell.exe -ArgumentList `$arguments -Verb RunAs -Wait -PassThru
    exit `$child.ExitCode
}
`$bundlePath = Join-Path `$PSScriptRoot '$bundleName'
`$certificatePath = Join-Path `$PSScriptRoot '$certificateName'
`$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(`$certificatePath)
if (`$certificate.HasPrivateKey -or `$certificate.Subject -cne 'CN=AppPublisher') { throw 'Acceptance certificate is invalid.' }
if (-not (Get-ChildItem Cert:\LocalMachine\TrustedPeople -ErrorAction SilentlyContinue | Where-Object Thumbprint -eq `$certificate.Thumbprint)) {
    Import-Certificate -FilePath `$certificatePath -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
}
`$signature = Get-AuthenticodeSignature -FilePath `$bundlePath
if (-not `$signature.SignerCertificate -or `$signature.SignerCertificate.Thumbprint -cne `$certificate.Thumbprint) { throw 'MSIXBundle signature does not match the included certificate.' }
Add-AppxPackage -Path `$bundlePath -ErrorAction Stop
`$installed = @(Get-AppxPackage -Name SpatialViewer | Where-Object { `$_.Publisher -ceq 'CN=AppPublisher' })
if (`$installed.Count -lt 1) { throw 'SpatialViewer package was not found after installation.' }
if ([string]`$installed[0].Version -ne '$PackageVersion') { throw "Installed version mismatch: `$(`$installed[0].Version)" }
Write-Output 'SpatialViewer v$DisplayVersion CadCore acceptance installation completed.'
"@

$uninstall = @'
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$package = Get-AppxPackage -Name SpatialViewer -ErrorAction SilentlyContinue | Where-Object { $_.Publisher -ceq 'CN=AppPublisher' } | Sort-Object Version -Descending | Select-Object -First 1
if (-not $package) { Write-Output 'SpatialViewer is not installed.'; exit 0 }
Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
Write-Output "Removed $($package.PackageFullName)."
'@

$installCmd = @'
@echo off
chcp 65001 >nul
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install.ps1"
set "EXITCODE=%ERRORLEVEL%"
if not "%EXITCODE%"=="0" (
  echo Installation failed. Exit code: %EXITCODE%
  pause
  exit /b %EXITCODE%
)
echo Installation completed.
pause
'@

$uninstallCmd = @'
@echo off
chcp 65001 >nul
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Uninstall.ps1"
set "EXITCODE=%ERRORLEVEL%"
if not "%EXITCODE%"=="0" (
  echo Uninstall failed. Exit code: %EXITCODE%
  pause
  exit /b %EXITCODE%
)
echo Uninstall completed.
pause
'@

$readme = @"
Spatial Viewer v$DisplayVersion — CadCore 独立化验收包
====================================================

用途
----
此包专门用于验收 CAD 内核从 SpatialViewer UI 仓库剥离后的产品构建。
它包含本次 CI 直接构建并签名的 x64 MSIXBundle，不会在线下载旧 GitHub Release。

版本
----
SpatialViewer: $DisplayVersion
MSIX package: $PackageVersion
SpatialViewer commit: $SpatialViewerCommit
CadCore commit: $CadCoreCommit
Publisher: CN=AppPublisher

安装
----
1. 完整解压 ZIP。
2. 双击“① 安装验收版.cmd”。
3. 首次安装会请求管理员权限，将随包公钥证书加入 LocalMachine\\TrustedPeople。
4. 安装脚本会再次校验 MSIXBundle 签名与版本号。

卸载
----
双击“② 卸载验收版.cmd”。

安全边界
--------
- 包内只有公钥证书，不包含 PFX、私钥或证书密码。
- 证书仅加入 TrustedPeople，不加入 Trusted Root。
- 不修改系统级 PowerShell 执行策略。
"@

[IO.File]::WriteAllText((Join-Path $root 'Install.ps1'), $install, [Text.UTF8Encoding]::new($true))
[IO.File]::WriteAllText((Join-Path $root 'Uninstall.ps1'), $uninstall, [Text.UTF8Encoding]::new($true))
[IO.File]::WriteAllText((Join-Path $root '① 安装验收版.cmd'), $installCmd, [Text.ASCIIEncoding]::new())
[IO.File]::WriteAllText((Join-Path $root '② 卸载验收版.cmd'), $uninstallCmd, [Text.ASCIIEncoding]::new())
[IO.File]::WriteAllText((Join-Path $root '请先阅读.txt'), $readme, [Text.UTF8Encoding]::new($true))

$hashLines = Get-ChildItem -LiteralPath $root -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name | ForEach-Object {
    "{0} *{1}" -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant(), $_.Name
}
Set-Content -LiteralPath (Join-Path $root 'SHA256SUMS.txt') -Value $hashLines -Encoding ASCII

$zip = Join-Path $output "SpatialViewer-v$DisplayVersion-CadCore-Acceptance-x64.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -LiteralPath $root -DestinationPath $zip -CompressionLevel Optimal

$verify = Join-Path $output 'verify-extracted'
if (Test-Path -LiteralPath $verify) { Remove-Item -LiteralPath $verify -Recurse -Force }
Expand-Archive -LiteralPath $zip -DestinationPath $verify -Force
$verifiedRoot = Join-Path $verify (Split-Path -Leaf $root)
if (-not (Test-Path -LiteralPath (Join-Path $verifiedRoot $bundleName))) { throw 'Acceptance ZIP verification failed: bundle missing.' }
if (-not (Test-Path -LiteralPath (Join-Path $verifiedRoot '① 安装验收版.cmd'))) { throw 'Acceptance ZIP verification failed: installer missing.' }
Remove-Item -LiteralPath $verify -Recurse -Force

Write-Output $zip
