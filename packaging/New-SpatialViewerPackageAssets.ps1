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

function New-Logo([string]$Path, [int]$Size) {
    $bitmap = [Drawing.Bitmap]::new($Size, $Size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([Drawing.Color]::FromArgb(32, 41, 41))
        $cyan = [Drawing.Color]::FromArgb(48, 190, 232)
        $light = [Drawing.Color]::FromArgb(235, 245, 246)
        $pen = [Drawing.Pen]::new($cyan, [Math]::Max(2, [int]($Size / 18)))
        $pen.StartCap = [Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $margin = [int]($Size * 0.18)
        $cell = ($Size - 2 * $margin) / 4
        for ($index = 0; $index -le 4; $index++) {
            $position = $margin + $index * $cell
            $graphics.DrawLine($pen, $position, $margin, $position, $Size - $margin)
            $graphics.DrawLine($pen, $margin, $position, $Size - $margin, $position)
        }
        $graphics.FillEllipse([Drawing.SolidBrush]::new($light), [int]($Size * 0.36), [int]($Size * 0.36), [int]($Size * 0.28), [int]($Size * 0.28))
        $graphics.DrawEllipse($pen, [int]($Size * 0.22), [int]($Size * 0.36), [int]($Size * 0.56), [int]($Size * 0.28))
        $pen.Dispose()
    }
    finally { $graphics.Dispose() }
    try { $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png) }
    finally { $bitmap.Dispose() }
}

New-Logo (Join-Path $output 'Square44x44Logo.png') 176
New-Logo (Join-Path $output 'Square150x150Logo.png') 600
New-Logo (Join-Path $output 'StoreLogo.png') 200
New-Logo (Join-Path $output 'SplashScreen.png') 1240
Write-Output "Generated SpatialViewer package assets in $output"
