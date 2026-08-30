[CmdletBinding()]
param([Parameter(Mandatory)][string]$ReleaseDirectory, [string]$ZipPath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $ReleaseDirectory).Path
$payload = Join-Path $root 'payload'
foreach ($file in @('① 安装图览.cmd', '② 卸载图览.cmd', '请先阅读.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $root $file) -PathType Leaf)) { throw "Missing root installer file: $file" }
}
foreach ($file in @('InstallerMetadata.json', 'Install.ps1', 'Uninstall.ps1', 'SHA256SUMS.txt')) {
    if (-not (Test-Path -LiteralPath (Join-Path $payload $file) -PathType Leaf)) { throw "Missing installer payload file: $file" }
}
$metadata = Get-Content -Raw -LiteralPath (Join-Path $payload 'InstallerMetadata.json') | ConvertFrom-Json
if ($metadata.schemaVersion -ne 1 -or $metadata.packageIdentityName -cne 'SpatialViewer' -or $metadata.publisher -cne 'CN=AppPublisher' -or $metadata.architecture -cne 'x64') { throw 'Installer metadata identity mismatch.' }
if ($metadata.displayVersion -notmatch '^\d+\.\d+\.\d+$' -or $metadata.packageVersion -ne "$($metadata.displayVersion).0" -or $metadata.releaseTag -ne "v$($metadata.displayVersion)") { throw 'Installer metadata version mismatch.' }
if ($metadata.releaseApiUri -ne "https://api.github.com/repos/KiYouJyo/SpatialViewer/releases/tags/v$($metadata.displayVersion)") { throw 'Installer release URI mismatch.' }
if (-not (Test-Path -LiteralPath (Join-Path $payload $metadata.certificateFileName) -PathType Leaf)) { throw 'Public certificate is missing.' }
$forbidden = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.Extension -in @('.msix', '.msixbundle', '.appinstaller', '.pfx', '.p12') })
if ($forbidden.Count -gt 0) { throw "Bootstrap contains forbidden package/private-key files: $($forbidden.Name -join ', ')" }
$hashes = @{}
Get-Content -LiteralPath (Join-Path $payload 'SHA256SUMS.txt') | ForEach-Object { if ($_ -match '^(?<hash>[A-Fa-f0-9]{64}) \*(?<name>.+)$') { $hashes[$matches.name] = $matches.hash.ToUpperInvariant() } }
Get-ChildItem -LiteralPath $payload -Recurse -File | Where-Object Name -ne 'SHA256SUMS.txt' | ForEach-Object {
    $relative = $_.FullName.Substring($payload.Length).TrimStart('\').Replace('\','/')
    if (-not $hashes.ContainsKey($relative) -or $hashes[$relative] -ne (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToUpperInvariant()) { throw "Payload checksum mismatch: $relative" }
}
if ($ZipPath -and (Get-Item -LiteralPath $ZipPath).Length -gt 5MB) { throw 'One-click bootstrap is unexpectedly large.' }
Write-Output 'GitHub one-click installer package validation passed.'
