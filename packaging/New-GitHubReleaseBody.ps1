[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$tag = "v$Version"
$files = @(
    "docs/RELEASE-NOTES-v$Version.md",
    "docs/RELEASE-NOTES-v$Version.ja.md",
    "docs/RELEASE-NOTES-v$Version.en.md"
)
foreach ($file in $files) {
    if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot $file) -PathType Leaf)) {
        throw "Missing release notes: $file"
    }
}

$lines = [IO.File]::ReadAllLines((Join-Path $repositoryRoot $files[0]), [Text.Encoding]::UTF8)
if ($lines.Count -lt 2) { throw 'Chinese release notes must include a language switcher and body.' }

# The standalone release-notes document keeps its own language switcher so it is
# navigable when opened directly in the repository. The GitHub Release body has
# a separate absolute-link switcher, so strip the document-local switcher (and
# the H1 above it) before appending the actual notes body.
$switcherIndex = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i].TrimStart().StartsWith('简体中文 |')) {
        $switcherIndex = $i
        break
    }
}
if ($switcherIndex -lt 0) { throw 'Chinese release notes must include the language switcher line.' }

$bodyStart = $switcherIndex + 1
while ($bodyStart -lt $lines.Count -and [string]::IsNullOrWhiteSpace($lines[$bodyStart])) {
    $bodyStart++
}
if ($bodyStart -ge $lines.Count) { throw 'Chinese release notes must include body content after the language switcher.' }

$repositoryUrl = 'https://github.com/KiYouJyo/SpatialViewer'
$publishedHeader = "简体中文 | [日本語]($repositoryUrl/blob/$tag/$($files[1])) | [English]($repositoryUrl/blob/$tag/$($files[2]))"
$bodyLines = $lines[$bodyStart..($lines.Count - 1)]
$body = $publishedHeader + [Environment]::NewLine + [Environment]::NewLine + ($bodyLines -join [Environment]::NewLine)

$directory = Split-Path -Parent $OutputPath
if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
[IO.File]::WriteAllText($OutputPath, $body, [Text.UTF8Encoding]::new($false))
Write-Output "Generated GitHub release body: $OutputPath"
