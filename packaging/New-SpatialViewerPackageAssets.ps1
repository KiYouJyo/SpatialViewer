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

function New-RoundedRectanglePath([float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius) {
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = [Math]::Max(1.0, $Radius * 2.0)
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-IconBitmap([int]$Size) {
    $bitmap = [Drawing.Bitmap]::new($Size, $Size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $resources = [Collections.Generic.List[IDisposable]]::new()
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([Drawing.Color]::Transparent)

        $white = [Drawing.Color]::FromArgb(250, 249, 247)
        $stroke = [Drawing.Color]::FromArgb(210, 219, 214)
        $cad = [Drawing.Color]::FromArgb(126, 169, 202)
        $gis = [Drawing.Color]::FromArgb(169, 204, 151)
        $rhino = [Drawing.Color]::FromArgb(135, 197, 209)
        $bim = [Drawing.Color]::FromArgb(170, 190, 203)
        $river = [Drawing.Color]::FromArgb(91, 176, 199)
        $green = [Drawing.Color]::FromArgb(126, 184, 145)
        $greenDark = [Drawing.Color]::FromArgb(94, 157, 121)
        $coral = [Drawing.Color]::FromArgb(229, 155, 133)
        $shadow = [Drawing.Color]::FromArgb(150, 170, 181)

        $outerMargin = [Math]::Max(1.0, $Size * 0.045)
        $outerRadius = [Math]::Max(2.0, $Size * 0.19)
        $outerPath = New-RoundedRectanglePath $outerMargin $outerMargin ($Size - 2 * $outerMargin) ($Size - 2 * $outerMargin) $outerRadius
        $outerBrush = [Drawing.SolidBrush]::new($white); $resources.Add($outerBrush)
        $outerPen = [Drawing.Pen]::new($stroke, [Math]::Max(1.0, $Size * 0.018)); $resources.Add($outerPen)
        $graphics.FillPath($outerBrush, $outerPath)
        $graphics.DrawPath($outerPen, $outerPath)
        $outerPath.Dispose()

        $innerMargin = $Size * 0.105
        $gap = [Math]::Max(1.0, $Size * 0.028)
        $cell = ($Size - 2 * $innerMargin - $gap) / 2.0
        $cellRadius = [Math]::Max(1.5, $Size * 0.075)

        $tileData = @(
            @($innerMargin, $innerMargin, $cad),
            @($innerMargin + $cell + $gap, $innerMargin, $gis),
            @($innerMargin, $innerMargin + $cell + $gap, $rhino),
            @($innerMargin + $cell + $gap, $innerMargin + $cell + $gap, $bim)
        )
        foreach ($tile in $tileData) {
            $path = New-RoundedRectanglePath ([float]$tile[0]) ([float]$tile[1]) ([float]$cell) ([float]$cell) ([float]$cellRadius)
            $brush = [Drawing.SolidBrush]::new([Drawing.Color]$tile[2]); $resources.Add($brush)
            $graphics.FillPath($brush, $path)
            $path.Dispose()
        }

        $lineWidth = [Math]::Max(1.0, $Size * 0.032)
        $whitePen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(244, 249, 249), $lineWidth); $resources.Add($whitePen)
        $whitePen.StartCap = [Drawing.Drawing2D.LineCap]::Round
        $whitePen.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $whitePen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round

        # CAD plan: broad, high-contrast geometry only; no micro-detail at taskbar sizes.
        $x0 = $innerMargin + $cell * 0.18
        $y0 = $innerMargin + $cell * 0.22
        $x1 = $innerMargin + $cell * 0.78
        $y1 = $innerMargin + $cell * 0.78
        $cadPoints = [Drawing.PointF[]]@(
            [Drawing.PointF]::new($x0, $y1),
            [Drawing.PointF]::new($x0, $y0),
            [Drawing.PointF]::new($x0 + $cell * 0.28, $y0),
            [Drawing.PointF]::new($x0 + $cell * 0.28, $y0 + $cell * 0.18),
            [Drawing.PointF]::new($x1, $y0 + $cell * 0.18),
            [Drawing.PointF]::new($x1, $y1),
            [Drawing.PointF]::new($x0, $y1)
        )
        $graphics.DrawLines($whitePen, $cadPoints)
        if ($Size -ge 32) {
            $graphics.DrawLine($whitePen, $x0 + $cell * 0.30, $y0 + $cell * 0.18, $x0 + $cell * 0.30, $y1)
            $graphics.DrawLine($whitePen, $x0 + $cell * 0.30, $y0 + $cell * 0.50, $x1, $y0 + $cell * 0.50)
        }

        # GIS: one river and one warm pedestrian/road trace.
        $riverPen = [Drawing.Pen]::new($river, [Math]::Max(1.5, $Size * 0.095)); $resources.Add($riverPen)
        $riverPen.StartCap = [Drawing.Drawing2D.LineCap]::Round
        $riverPen.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $gx = $innerMargin + $cell + $gap
        $gy = $innerMargin
        $graphics.DrawBezier($riverPen,
            [Drawing.PointF]::new($gx + $cell * 0.70, $gy + $cell * 0.05),
            [Drawing.PointF]::new($gx + $cell * 0.42, $gy + $cell * 0.28),
            [Drawing.PointF]::new($gx + $cell * 0.85, $gy + $cell * 0.60),
            [Drawing.PointF]::new($gx + $cell * 0.55, $gy + $cell * 0.95))
        if ($Size -ge 28) {
            $routePen = [Drawing.Pen]::new($coral, [Math]::Max(1.0, $Size * 0.028)); $resources.Add($routePen)
            $routePen.StartCap = [Drawing.Drawing2D.LineCap]::Round
            $routePen.EndCap = [Drawing.Drawing2D.LineCap]::Round
            $graphics.DrawBezier($routePen,
                [Drawing.PointF]::new($gx + $cell * 0.12, $gy + $cell * 0.78),
                [Drawing.PointF]::new($gx + $cell * 0.36, $gy + $cell * 0.58),
                [Drawing.PointF]::new($gx + $cell * 0.62, $gy + $cell * 0.74),
                [Drawing.PointF]::new($gx + $cell * 0.92, $gy + $cell * 0.50))
        }

        # Rhino/NURBS: a clean double wave rather than dense wireframe.
        $rx = $innerMargin
        $ry = $innerMargin + $cell + $gap
        $graphics.DrawBezier($whitePen,
            [Drawing.PointF]::new($rx + $cell * 0.05, $ry + $cell * 0.70),
            [Drawing.PointF]::new($rx + $cell * 0.28, $ry + $cell * 0.05),
            [Drawing.PointF]::new($rx + $cell * 0.72, $ry + $cell * 0.95),
            [Drawing.PointF]::new($rx + $cell * 0.95, $ry + $cell * 0.30))
        if ($Size -ge 40) {
            $thinPen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(210, 245, 249, 249), [Math]::Max(1.0, $Size * 0.014)); $resources.Add($thinPen)
            $graphics.DrawBezier($thinPen,
                [Drawing.PointF]::new($rx + $cell * 0.05, $ry + $cell * 0.82),
                [Drawing.PointF]::new($rx + $cell * 0.32, $ry + $cell * 0.25),
                [Drawing.PointF]::new($rx + $cell * 0.68, $ry + $cell * 0.82),
                [Drawing.PointF]::new($rx + $cell * 0.95, $ry + $cell * 0.42))
        }

        # BIM: strong massing blocks that survive 16/20/24 px rendering.
        $bx = $innerMargin + $cell + $gap
        $by = $innerMargin + $cell + $gap
        $buildingBrush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(242, 247, 248)); $resources.Add($buildingBrush)
        $shadowBrush = [Drawing.SolidBrush]::new($shadow); $resources.Add($shadowBrush)
        $graphics.FillRectangle($shadowBrush, [float]($bx + $cell * 0.57), [float]($by + $cell * 0.40), [float]($cell * 0.20), [float]($cell * 0.40))
        $graphics.FillRectangle($buildingBrush, [float]($bx + $cell * 0.52), [float]($by + $cell * 0.22), [float]($cell * 0.20), [float]($cell * 0.50))
        if ($Size -ge 24) {
            $graphics.FillRectangle($buildingBrush, [float]($bx + $cell * 0.20), [float]($by + $cell * 0.52), [float]($cell * 0.26), [float]($cell * 0.22))
        }

        # Viewing magnifier: the common cross-format viewer metaphor.
        $lensX = $Size * 0.32
        $lensY = $Size * 0.32
        $lensSize = $Size * 0.38
        $lensFill = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(28, 240, 250, 244)); $resources.Add($lensFill)
        $graphics.FillEllipse($lensFill, $lensX, $lensY, $lensSize, $lensSize)
        $ringPen = [Drawing.Pen]::new($green, [Math]::Max(1.5, $Size * 0.058)); $resources.Add($ringPen)
        $graphics.DrawEllipse($ringPen, $lensX, $lensY, $lensSize, $lensSize)

        $handlePen = [Drawing.Pen]::new($greenDark, [Math]::Max(2.0, $Size * 0.105)); $resources.Add($handlePen)
        $handlePen.StartCap = [Drawing.Drawing2D.LineCap]::Round
        $handlePen.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawLine($handlePen, [float]($Size * 0.62), [float]($Size * 0.62), [float]($Size * 0.82), [float]($Size * 0.82))
        $collarPen = [Drawing.Pen]::new($coral, [Math]::Max(1.0, $Size * 0.032)); $resources.Add($collarPen)
        $graphics.DrawLine($collarPen, [float]($Size * 0.675), [float]($Size * 0.675), [float]($Size * 0.705), [float]($Size * 0.705))

        if ($Size -ge 32) {
            $focusPen = [Drawing.Pen]::new([Drawing.Color]::FromArgb(238, 250, 250, 250), [Math]::Max(1.0, $Size * 0.022)); $resources.Add($focusPen)
            $focusPen.StartCap = [Drawing.Drawing2D.LineCap]::Round
            $focusPen.EndCap = [Drawing.Drawing2D.LineCap]::Round
            $cx = $Size * 0.51
            $cy = $Size * 0.51
            $graphics.DrawLine($focusPen, $cx - $Size * 0.045, $cy, $cx + $Size * 0.045, $cy)
            $graphics.DrawLine($focusPen, $cx, $cy - $Size * 0.045, $cx, $cy + $Size * 0.045)
        }
    }
    finally {
        foreach ($resource in $resources) { $resource.Dispose() }
        $graphics.Dispose()
    }
    return $bitmap
}

function Save-IconPng([string]$Path, [int]$Size) {
    $bitmap = New-IconBitmap $Size
    try { $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png) }
    finally { $bitmap.Dispose() }
}

function Save-Splash([string]$Path, [int]$Width, [int]$Height) {
    $bitmap = [Drawing.Bitmap]::new($Width, $Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([Drawing.Color]::FromArgb(244, 247, 244))
        $iconSize = [Math]::Min([int]($Height * 0.54), [int]($Width * 0.26))
        $icon = New-IconBitmap $iconSize
        try {
            $x = [int](($Width - $iconSize) / 2)
            $y = [int](($Height - $iconSize) / 2)
            $graphics.DrawImage($icon, $x, $y, $iconSize, $iconSize)
        }
        finally { $icon.Dispose() }
        $bitmap.Save($Path, [Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}

function Save-Ico([string]$Path, [int[]]$Sizes) {
    $images = @()
    foreach ($size in $Sizes) {
        $pngPath = Join-Path $output ("Square44x44Logo.targetsize-{0}_altform-unplated.png" -f $size)
        $images += ,([IO.File]::ReadAllBytes($pngPath))
    }
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $writer = [IO.BinaryWriter]::new($stream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$Sizes.Count)
        $offset = 6 + (16 * $Sizes.Count)
        for ($i = 0; $i -lt $Sizes.Count; $i++) {
            $size = $Sizes[$i]
            $writer.Write([byte](if ($size -ge 256) { 0 } else { $size }))
            $writer.Write([byte](if ($size -ge 256) { 0 } else { $size }))
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$images[$i].Length)
            $writer.Write([uint32]$offset)
            $offset += $images[$i].Length
        }
        foreach ($image in $images) { $writer.Write([byte[]]$image) }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

# Base logical assets.
Save-IconPng (Join-Path $output 'Square44x44Logo.png') 44
Save-IconPng (Join-Path $output 'Square150x150Logo.png') 150
Save-IconPng (Join-Path $output 'StoreLogo.png') 50
Save-Splash (Join-Path $output 'SplashScreen.png') 620 300

# Explicit scale-qualified assets prevent Windows from up/down-sampling a single source.
$scales = @{ 100 = 1.0; 125 = 1.25; 150 = 1.5; 200 = 2.0; 400 = 4.0 }
foreach ($entry in $scales.GetEnumerator()) {
    $scale = [int]$entry.Key
    $factor = [double]$entry.Value
    Save-IconPng (Join-Path $output ("Square44x44Logo.scale-{0}.png" -f $scale)) ([int][Math]::Round(44 * $factor))
    Save-IconPng (Join-Path $output ("Square150x150Logo.scale-{0}.png" -f $scale)) ([int][Math]::Round(150 * $factor))
    Save-IconPng (Join-Path $output ("StoreLogo.scale-{0}.png" -f $scale)) ([int][Math]::Round(50 * $factor))
    Save-Splash (Join-Path $output ("SplashScreen.scale-{0}.png" -f $scale)) ([int][Math]::Round(620 * $factor)) ([int][Math]::Round(300 * $factor))
}

# Pixel-snapped shell variants used by Start, taskbar, Open With and recent-app surfaces.
$targetSizes = @(16, 20, 24, 30, 32, 36, 40, 44, 48, 60, 64, 72, 80, 96, 128, 256)
foreach ($size in $targetSizes) {
    Save-IconPng (Join-Path $output ("Square44x44Logo.targetsize-{0}.png" -f $size)) $size
    Save-IconPng (Join-Path $output ("Square44x44Logo.targetsize-{0}_altform-unplated.png" -f $size)) $size
}

Save-Ico (Join-Path $output 'AppIcon.ico') @(16, 20, 24, 32, 40, 48, 64, 128, 256)
Write-Output "Generated crisp multi-scale SpatialViewer icon assets in $output"
