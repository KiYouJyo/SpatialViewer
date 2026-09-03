[CmdletBinding()]
param([string]$OutputDirectory)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot '..\src\SpatialViewer.App\Assets'
}

Add-Type -AssemblyName System.Drawing
$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $output | Out-Null

# Keep the startup surface deliberately simple. UrbanPlanToolbox and PageArc both
# let the native MSIX splash own the cold-start interval instead of opening a
# second authored window. SpatialViewer follows that model: one centered product
# mark on a stable #202020 field, with a scale-qualified bitmap for every Windows
# desktop DPI bucket used by the package.
function Save-StartupPng([string]$Path, [int]$Width, [int]$Height, [int]$Scale) {
    $bitmap = New-Object Drawing.Bitmap($Width, $Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([Drawing.Color]::FromArgb(32, 32, 32))

        $iconPath = Join-Path $output ("Square150x150Logo.scale-{0}.png" -f $Scale)
        if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
            $iconPath = Join-Path $output 'Square150x150Logo.png'
        }
        if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
            throw "SpatialViewer startup icon source was not found: $iconPath"
        }

        $factor = $Scale / 100.0
        $iconSize = [int][Math]::Round(150 * $factor)
        $icon = [Drawing.Image]::FromFile($iconPath)
        try {
            $x = [int][Math]::Round(($Width - $iconSize) / 2.0)
            $y = [int][Math]::Round(($Height - $iconSize) / 2.0)
            [void]$graphics.DrawImage($icon, $x, $y, $iconSize, $iconSize)
        }
        finally {
            $icon.Dispose()
        }

        $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

$scaleValues = @(100, 125, 150, 200, 400)
foreach ($scale in $scaleValues) {
    $factor = $scale / 100.0
    $width = [int][Math]::Round(620 * $factor)
    $height = [int][Math]::Round(300 * $factor)
    Save-StartupPng (Join-Path $output ("SplashScreen.scale-{0}.png" -f $scale)) $width $height $scale
}

# Keep an unqualified 100% fallback as PageArc does. Packaged Windows launches
# resolve the scale-qualified resource first; the fallback also keeps direct
# package inspection and older tooling deterministic.
Copy-Item -LiteralPath (Join-Path $output 'SplashScreen.scale-100.png') -Destination (Join-Path $output 'SplashScreen.png') -Force

Write-Host "Generated SpatialViewer native startup screen: 620x300 logical canvas, #202020 background, scale 100/125/150/200/400."
