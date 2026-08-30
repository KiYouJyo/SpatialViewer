[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$SignedBundlePath,
    [Parameter(Mandatory)][string]$PublicCertificatePath,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$DisplayVersion,
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+\.\d+$')][string]$PackageVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $PackageVersion.StartsWith("$DisplayVersion.")) { throw 'Package version must extend the display version.' }
foreach ($path in @($SignedBundlePath, $PublicCertificatePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing input: $path" }
}

$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new((Resolve-Path -LiteralPath $PublicCertificatePath))
if ($certificate.HasPrivateKey -or $certificate.Subject -cne 'CN=AppPublisher') { throw 'The installer needs the matching public AppPublisher certificate.' }
$output = [IO.Path]::GetFullPath($OutputDirectory)
$root = Join-Path $output "SpatialViewer-v$DisplayVersion-x64-one-click"
if (Test-Path -LiteralPath $root) { throw "Installer output already exists: $root" }
$payload = Join-Path $root 'payload'
New-Item -ItemType Directory -Force -Path $payload | Out-Null

$metadata = [ordered]@{
    schemaVersion = 1
    displayVersion = $DisplayVersion
    packageVersion = $PackageVersion
    releaseTag = "v$DisplayVersion"
    packageIdentityName = 'SpatialViewer'
    publisher = 'CN=AppPublisher'
    architecture = 'x64'
    remoteBundleFileName = (Get-Item -LiteralPath $SignedBundlePath).Name
    certificateFileName = "SpatialViewer-v$DisplayVersion-Framework-Dependent.cer"
    releaseApiUri = "https://api.github.com/repos/KiYouJyo/SpatialViewer/releases/tags/v$DisplayVersion"
    checksumFileName = 'SHA256SUMS.txt'
}
$metadata | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $payload 'InstallerMetadata.json') -Encoding UTF8
Copy-Item -LiteralPath $PublicCertificatePath -Destination (Join-Path $payload $metadata.certificateFileName)

$install = @'
[CmdletBinding()]
param([switch]$ImportCertificateOnly)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$payload = $PSScriptRoot
$metadata = Get-Content -Raw -LiteralPath (Join-Path $payload 'InstallerMetadata.json') | ConvertFrom-Json
function Test-IsAdministrator { ([Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator) }
function Ensure-Certificate([string]$CertificatePath, [string]$Thumbprint) {
    $trusted = Get-ChildItem 'Cert:\LocalMachine\TrustedPeople' -ErrorAction SilentlyContinue | Where-Object Thumbprint -eq $Thumbprint
    if ($trusted) { return }
    if (Test-IsAdministrator) { Import-Certificate -FilePath $CertificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null; return }
    $arguments = @('-NoLogo','-NoProfile','-ExecutionPolicy','Bypass','-File',('"{0}"' -f $PSCommandPath),'-ImportCertificateOnly') -join ' '
    $child = Start-Process -FilePath powershell.exe -ArgumentList $arguments -Verb RunAs -Wait -PassThru
    if ($child.ExitCode -ne 0) { throw 'Certificate trust setup was cancelled or failed.' }
}
if ($ImportCertificateOnly) {
    $certificatePath = Join-Path $payload $metadata.certificateFileName
    if (-not (Test-IsAdministrator)) { throw 'Certificate trust setup requires elevation.' }
    Import-Certificate -FilePath $certificatePath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
    exit 0
}
$certificatePath = Join-Path $payload $metadata.certificateFileName
$certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new($certificatePath)
if ($certificate.HasPrivateKey -or $certificate.Subject -cne $metadata.publisher) { throw 'Installer certificate does not match the package publisher.' }
Ensure-Certificate $certificatePath $certificate.Thumbprint
$release = Invoke-RestMethod -Uri $metadata.releaseApiUri -Headers @{ Accept = 'application/vnd.github+json'; 'User-Agent' = "SpatialViewer/$($metadata.displayVersion)" }
if ($release.tag_name -ne $metadata.releaseTag -or $release.draft -or $release.prerelease) { throw 'The requested stable release is unavailable.' }
$bundle = @($release.assets | Where-Object name -eq $metadata.remoteBundleFileName)
$checksum = @($release.assets | Where-Object name -eq $metadata.checksumFileName)
if ($bundle.Count -ne 1 -or $checksum.Count -ne 1) { throw 'The release asset set is incomplete.' }
$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ("SpatialViewer-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $temporaryDirectory | Out-Null
try {
    $checksumPath = Join-Path $temporaryDirectory $metadata.checksumFileName
    $bundlePath = Join-Path $temporaryDirectory $metadata.remoteBundleFileName
    Invoke-WebRequest -Uri $checksum[0].browser_download_url -UseBasicParsing -OutFile $checksumPath
    $expected = $null
    foreach ($line in Get-Content -LiteralPath $checksumPath) {
        if ($line -match '^(?<hash>[A-Fa-f0-9]{64})\s+(?:\*)?(?<name>.+)$' -and $matches.name -eq $metadata.remoteBundleFileName) { $expected = $matches.hash.ToUpperInvariant(); break }
    }
    if (-not $expected) { throw 'The checksum manifest does not contain the expected MSIXBundle.' }
    Invoke-WebRequest -Uri $bundle[0].browser_download_url -UseBasicParsing -OutFile $bundlePath
    if ((Get-Item -LiteralPath $bundlePath).Length -ne [long]$bundle[0].size) { throw 'Downloaded MSIXBundle size does not match the release metadata.' }
    if ((Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256).Hash.ToUpperInvariant() -ne $expected) { throw 'Downloaded MSIXBundle checksum verification failed.' }
    $signature = Get-AuthenticodeSignature -FilePath $bundlePath
    if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Subject -cne $metadata.publisher -or $signature.SignerCertificate.Thumbprint -cne $certificate.Thumbprint) { throw 'Downloaded MSIXBundle signature verification failed.' }
    Add-AppxPackage -Path $bundlePath -ErrorAction Stop
    $installed = @(Get-AppxPackage -Name $metadata.packageIdentityName | Where-Object { $_.Publisher -ceq $metadata.publisher })
    if ($installed.Count -ne 1 -or [string]$installed[0].Version -ne $metadata.packageVersion -or [string]$installed[0].Architecture -ne 'X64' -or [string]$installed[0].Status -ne 'Ok') { throw 'Installed package verification failed.' }
    Write-Output "Spatial Viewer v$($metadata.displayVersion) installation completed."
}
finally { if (Test-Path -LiteralPath $temporaryDirectory) { Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force } }
'@

$uninstall = @'
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$payload = $PSScriptRoot
$metadata = Get-Content -Raw -LiteralPath (Join-Path $payload 'InstallerMetadata.json') | ConvertFrom-Json
$package = Get-AppxPackage -Name $metadata.packageIdentityName -ErrorAction SilentlyContinue | Where-Object { $_.Publisher -ceq $metadata.publisher } | Sort-Object Version -Descending | Select-Object -First 1
if (-not $package) { Write-Output 'Spatial Viewer is not installed.'; exit 0 }
Remove-AppxPackage -Package $package.PackageFullName -ErrorAction Stop
Write-Output "Removed $($package.PackageFullName)."
'@

$installCommand = @'
@echo off
chcp 65001 >nul
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0payload\Install.ps1"
set "EXITCODE=%ERRORLEVEL%"
if not "%EXITCODE%"=="0" (
  echo Installation failed. Exit code: %EXITCODE%
  pause
  exit /b %EXITCODE%
)
echo Installation completed.
pause
'@
$uninstallCommand = @'
@echo off
chcp 65001 >nul
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0payload\Uninstall.ps1"
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
Spatial Viewer v$DisplayVersion self-signed preview package
===========================================================

This x64 package installs the signed MSIX $PackageVersion from the matching GitHub Release. It is a preview certificate, not a commercially trusted code-signing certificate.

Installation
------------
1. Fully extract this ZIP before starting; do not run it from the archive preview.
2. Run “① 安装图览.cmd” and accept the Windows UAC prompt when asked to trust the public preview certificate.
3. The installer downloads the release MSIXBundle, verifies SHA-256 and its signature, and verifies the installed package identity.

Security boundary
-----------------
- The package contains only the public certificate. It never contains a PFX, P12, private key, or certificate password.
- The certificate is added only to LocalMachine TrustedPeople; it is never added to Trusted Root.
- The bootstrap does not change the global PowerShell execution policy.

Uninstall
---------
Run “② 卸载图览.cmd”. It only removes the Spatial Viewer GitHub package.
"@

[IO.File]::WriteAllText((Join-Path $payload 'Install.ps1'), $install, [Text.UTF8Encoding]::new($true))
[IO.File]::WriteAllText((Join-Path $payload 'Uninstall.ps1'), $uninstall, [Text.UTF8Encoding]::new($true))
[IO.File]::WriteAllText((Join-Path $root '① 安装图览.cmd'), $installCommand, [Text.ASCIIEncoding]::new())
[IO.File]::WriteAllText((Join-Path $root '② 卸载图览.cmd'), $uninstallCommand, [Text.ASCIIEncoding]::new())
[IO.File]::WriteAllText((Join-Path $root '请先阅读.txt'), $readme, [Text.UTF8Encoding]::new($true))

$hashLines = Get-ChildItem -LiteralPath $payload -Recurse -File | Sort-Object FullName | ForEach-Object {
    $relative = $_.FullName.Substring($payload.Length).TrimStart('\').Replace('\','/')
    "$((Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant()) *$relative"
}
Set-Content -LiteralPath (Join-Path $payload 'SHA256SUMS.txt') -Value $hashLines -Encoding UTF8
Write-Output $root
