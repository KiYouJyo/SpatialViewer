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
$repositoryUrl = 'https://github.com/KiYouJyo/SpatialViewer'
$publishedHeader = "简体中文 | [日本語]($repositoryUrl/blob/$tag/$($files[1])) | [English]($repositoryUrl/blob/$tag/$($files[2]))"
$body = $publishedHeader + [Environment]::NewLine + ($lines[1..($lines.Count - 1)] -join [Environment]::NewLine)

$directory = Split-Path -Parent $OutputPath
if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
[IO.File]::WriteAllText($OutputPath, $body, [Text.UTF8Encoding]::new($false))
Write-Output "Generated GitHub release body: $OutputPath"
