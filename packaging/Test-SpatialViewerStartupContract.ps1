[CmdletBinding()]
param([string]$AssetsDirectory)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($AssetsDirectory)) {
    $AssetsDirectory = Join-Path $PSScriptRoot '..\src\SpatialViewer.App\Assets'
}

Add-Type -AssemblyName System.Drawing
$assets = [IO.Path]::GetFullPath($AssetsDirectory)

function Assert-StartupPng([string]$Name, [int]$Width, [int]$Height) {
    $path = Join-Path $assets $Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Missing startup asset: $Name"
    }

    $bitmap = New-Object Drawing.Bitmap($path)
    try {
        if ($bitmap.Width -ne $Width -or $bitmap.Height -ne $Height) {
            throw "Unexpected startup dimensions for ${Name}: $($bitmap.Width)x$($bitmap.Height), expected ${Width}x${Height}."
        }

        $corner = $bitmap.GetPixel(0, 0)
        if ($corner.R -ne 32 -or $corner.G -ne 32 -or $corner.B -ne 32 -or $corner.A -ne 255) {
            throw "Startup background mismatch for ${Name}: expected opaque #202020, found A=$($corner.A) R=$($corner.R) G=$($corner.G) B=$($corner.B)."
        }

        $center = $bitmap.GetPixel([int]($bitmap.Width / 2), [int]($bitmap.Height / 2))
        if ($center.R -eq 32 -and $center.G -eq 32 -and $center.B -eq 32) {
            throw "Startup product mark is not visible at the center of ${Name}."
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

Assert-StartupPng 'SplashScreen.png' 620 300
$expectations = @{
    'SplashScreen.scale-100.png' = @(620, 300)
    'SplashScreen.scale-125.png' = @(775, 375)
    'SplashScreen.scale-150.png' = @(930, 450)
    'SplashScreen.scale-200.png' = @(1240, 600)
    'SplashScreen.scale-400.png' = @(2480, 1200)
}
foreach ($entry in $expectations.GetEnumerator()) {
    Assert-StartupPng $entry.Key ([int]$entry.Value[0]) ([int]$entry.Value[1])
}

$manifestPath = Join-Path $PSScriptRoot '..\src\SpatialViewer.App\Package.appxmanifest'
$manifest = Get-Content -LiteralPath $manifestPath -Raw
if ($manifest -notmatch 'xmlns:uap5="http://schemas\.microsoft\.com/appx/manifest/uap/windows10/5"') {
    throw 'Startup manifest contract missing uap5 namespace.'
}
if ($manifest -notmatch '<uap:SplashScreen\s+Image="Assets\\SplashScreen\.png"\s+BackgroundColor="#202020"\s+uap5:Optional="true"\s*/>') {
    throw 'Startup manifest contract must use the native optional #202020 splash resource.'
}

$projectPath = Join-Path $PSScriptRoot '..\src\SpatialViewer.App\SpatialViewer.App.csproj'
$project = Get-Content -LiteralPath $projectPath -Raw
if ($project -notmatch '<AppxBundleAutoResourcePackageQualifiers>Scale\|DXFeatureLevel</AppxBundleAutoResourcePackageQualifiers>') {
    throw 'Startup DPI contract missing Scale resource-package qualifier.'
}
if ($project -notmatch 'New-SpatialViewerStartupScreen\.ps1' -or $project -notmatch 'Test-SpatialViewerStartupContract\.ps1') {
    throw 'Startup generation/validation hooks are not wired into the app build.'
}

Write-Host 'SpatialViewer startup contract PASS: native optional splash, #202020 surface, centered mark, and 100/125/150/200/400 DPI resources.'
