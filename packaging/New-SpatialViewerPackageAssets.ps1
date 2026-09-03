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

function New-RoundedPath([float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius) {
    $path = New-Object Drawing.Drawing2D.GraphicsPath
    $diameter = [Math]::Max(1.0, $Radius * 2.0)
    [void]$path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    [void]$path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    [void]$path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    [void]$path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    [void]$path.CloseFigure()
    return $path
}

function Fill-RoundedRect($Graphics, $Brush, [float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius) {
    $path = New-RoundedPath $X $Y $Width $Height $Radius
    try { [void]$Graphics.FillPath($Brush, $path) }
    finally { $path.Dispose() }
}

function Draw-RoundedRect($Graphics, $Pen, [float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius) {
    $path = New-RoundedPath $X $Y $Width $Height $Radius
    try { [void]$Graphics.DrawPath($Pen, $path) }
    finally { $path.Dispose() }
}

function New-IconBitmap([int]$Size) {
    $bitmap = New-Object Drawing.Bitmap($Size, $Size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)

    $whiteBrush = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(250, 249, 247))
    $cadBrush = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(126, 169, 202))
    $gisBrush = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(169, 204, 151))
    $rhinoBrush = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(135, 197, 209))
    $bimBrush = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(170, 190, 203))
    $buildingBrush = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(244, 248, 249))
    $shadowBrush = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(145, 168, 181))
    $lensBrush = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(45, 239, 249, 244))

    $outlinePen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(210, 219, 214), [Math]::Max(1.0, $Size * 0.018))
    $whitePen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(245, 250, 250), [Math]::Max(1.0, $Size * 0.032))
    $riverPen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(91, 176, 199), [Math]::Max(1.5, $Size * 0.095))
    $routePen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(229, 155, 133), [Math]::Max(1.0, $Size * 0.028))
    $ringPen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(126, 184, 145), [Math]::Max(1.5, $Size * 0.058))
    $handlePen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(94, 157, 121), [Math]::Max(2.0, $Size * 0.105))
    $collarPen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(229, 155, 133), [Math]::Max(1.0, $Size * 0.032))
    $focusPen = New-Object Drawing.Pen([Drawing.Color]::FromArgb(238, 250, 250, 250), [Math]::Max(1.0, $Size * 0.022))

    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([Drawing.Color]::Transparent)

        foreach ($pen in @($whitePen, $riverPen, $routePen, $ringPen, $handlePen, $collarPen, $focusPen)) {
            $pen.StartCap = [Drawing.Drawing2D.LineCap]::Round
            $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
            $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
        }

        $outerMargin = [float][Math]::Max(1.0, $Size * 0.045)
        $outerRadius = [float][Math]::Max(2.0, $Size * 0.19)
        $outerExtent = [float]($Size - 2.0 * $outerMargin)
        Fill-RoundedRect $graphics $whiteBrush $outerMargin $outerMargin $outerExtent $outerExtent $outerRadius
        Draw-RoundedRect $graphics $outlinePen $outerMargin $outerMargin $outerExtent $outerExtent $outerRadius

        $innerMargin = [float]($Size * 0.105)
        $gap = [float][Math]::Max(1.0, $Size * 0.028)
        $cell = [float](($Size - 2.0 * $innerMargin - $gap) / 2.0)
        $cellRadius = [float][Math]::Max(1.5, $Size * 0.075)
        $rightX = [float]($innerMargin + $cell + $gap)
        $bottomY = [float]($innerMargin + $cell + $gap)

        Fill-RoundedRect $graphics $cadBrush $innerMargin $innerMargin $cell $cell $cellRadius
        Fill-RoundedRect $graphics $gisBrush $rightX $innerMargin $cell $cell $cellRadius
        Fill-RoundedRect $graphics $rhinoBrush $innerMargin $bottomY $cell $cell $cellRadius
        Fill-RoundedRect $graphics $bimBrush $rightX $bottomY $cell $cell $cellRadius

        # CAD: thick plan outline with only the strokes that survive 16–24 px.
        $x0 = [float]($innerMargin + $cell * 0.18)
        $y0 = [float]($innerMargin + $cell * 0.22)
        $x1 = [float]($innerMargin + $cell * 0.78)
        $y1 = [float]($innerMargin + $cell * 0.78)
        $cadPoints = New-Object 'Drawing.PointF[]' 7
        $cadPoints[0] = New-Object Drawing.PointF($x0, $y1)
        $cadPoints[1] = New-Object Drawing.PointF($x0, $y0)
        $cadPoints[2] = New-Object Drawing.PointF([float]($x0 + $cell * 0.28), $y0)
        $cadPoints[3] = New-Object Drawing.PointF([float]($x0 + $cell * 0.28), [float]($y0 + $cell * 0.18))
        $cadPoints[4] = New-Object Drawing.PointF($x1, [float]($y0 + $cell * 0.18))
        $cadPoints[5] = New-Object Drawing.PointF($x1, $y1)
        $cadPoints[6] = New-Object Drawing.PointF($x0, $y1)
        [void]$graphics.DrawLines($whitePen, $cadPoints)
        if ($Size -ge 32) {
            [void]$graphics.DrawLine($whitePen, [float]($x0 + $cell * 0.30), [float]($y0 + $cell * 0.18), [float]($x0 + $cell * 0.30), $y1)
            [void]$graphics.DrawLine($whitePen, [float]($x0 + $cell * 0.30), [float]($y0 + $cell * 0.50), $x1, [float]($y0 + $cell * 0.50))
        }

        # GIS: broad river + one coral route.
        [void]$graphics.DrawBezier($riverPen,
            (New-Object Drawing.PointF([float]($rightX + $cell * 0.70), [float]($innerMargin + $cell * 0.05))),
            (New-Object Drawing.PointF([float]($rightX + $cell * 0.42), [float]($innerMargin + $cell * 0.28))),
            (New-Object Drawing.PointF([float]($rightX + $cell * 0.85), [float]($innerMargin + $cell * 0.60))),
            (New-Object Drawing.PointF([float]($rightX + $cell * 0.55), [float]($innerMargin + $cell * 0.95))))
        if ($Size -ge 28) {
            [void]$graphics.DrawBezier($routePen,
                (New-Object Drawing.PointF([float]($rightX + $cell * 0.12), [float]($innerMargin + $cell * 0.78))),
                (New-Object Drawing.PointF([float]($rightX + $cell * 0.36), [float]($innerMargin + $cell * 0.58))),
                (New-Object Drawing.PointF([float]($rightX + $cell * 0.62), [float]($innerMargin + $cell * 0.74))),
                (New-Object Drawing.PointF([float]($rightX + $cell * 0.92), [float]($innerMargin + $cell * 0.50))))
        }

        # Rhino/NURBS: one unmistakable surface wave.
        [void]$graphics.DrawBezier($whitePen,
            (New-Object Drawing.PointF([float]($innerMargin + $cell * 0.05), [float]($bottomY + $cell * 0.70))),
            (New-Object Drawing.PointF([float]($innerMargin + $cell * 0.28), [float]($bottomY + $cell * 0.05))),
            (New-Object Drawing.PointF([float]($innerMargin + $cell * 0.72), [float]($bottomY + $cell * 0.95))),
            (New-Object Drawing.PointF([float]($innerMargin + $cell * 0.95), [float]($bottomY + $cell * 0.30))))

        # BIM: high-contrast massing blocks.
        [void]$graphics.FillRectangle($shadowBrush, [float]($rightX + $cell * 0.57), [float]($bottomY + $cell * 0.40), [float]($cell * 0.20), [float]($cell * 0.40))
        [void]$graphics.FillRectangle($buildingBrush, [float]($rightX + $cell * 0.52), [float]($bottomY + $cell * 0.22), [float]($cell * 0.20), [float]($cell * 0.50))
        if ($Size -ge 24) {
            [void]$graphics.FillRectangle($buildingBrush, [float]($rightX + $cell * 0.20), [float]($bottomY + $cell * 0.52), [float]($cell * 0.26), [float]($cell * 0.22))
        }

        # Central viewer magnifier.
        $lensX = [float]($Size * 0.32)
        $lensY = [float]($Size * 0.32)
        $lensSize = [float]($Size * 0.38)
        [void]$graphics.FillEllipse($lensBrush, $lensX, $lensY, $lensSize, $lensSize)
        [void]$graphics.DrawEllipse($ringPen, $lensX, $lensY, $lensSize, $lensSize)
        [void]$graphics.DrawLine($handlePen, [float]($Size * 0.62), [float]($Size * 0.62), [float]($Size * 0.82), [float]($Size * 0.82))
        [void]$graphics.DrawLine($collarPen, [float]($Size * 0.675), [float]($Size * 0.675), [float]($Size * 0.705), [float]($Size * 0.705))

        if ($Size -ge 32) {
            $cx = [float]($Size * 0.51)
            $cy = [float]($Size * 0.51)
            [void]$graphics.DrawLine($focusPen, [float]($cx - $Size * 0.045), $cy, [float]($cx + $Size * 0.045), $cy)
            [void]$graphics.DrawLine($focusPen, $cx, [float]($cy - $Size * 0.045), $cx, [float]($cy + $Size * 0.045))
        }
    }
    finally {
        foreach ($resource in @($whiteBrush, $cadBrush, $gisBrush, $rhinoBrush, $bimBrush, $buildingBrush, $shadowBrush, $lensBrush,
                                 $outlinePen, $whitePen, $riverPen, $routePen, $ringPen, $handlePen, $collarPen, $focusPen)) {
            if ($null -ne $resource) { $resource.Dispose() }
        }
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
    $bitmap = New-Object Drawing.Bitmap($Width, $Height, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([Drawing.Color]::FromArgb(244, 247, 244))
        $iconSize = [Math]::Min([int]($Height * 0.54), [int]($Width * 0.26))
        $icon = New-IconBitmap $iconSize
        try {
            $x = [int](($Width - $iconSize) / 2)
            $y = [int](($Height - $iconSize) / 2)
            [void]$graphics.DrawImage($icon, $x, $y, $iconSize, $iconSize)
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
    $images = New-Object 'System.Collections.Generic.List[byte[]]'
    foreach ($size in $Sizes) {
        $pngPath = Join-Path $output ("Square44x44Logo.targetsize-{0}_altform-unplated.png" -f $size)
        [void]$images.Add([IO.File]::ReadAllBytes($pngPath))
    }

    $stream = [IO.File]::Open($Path, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $writer = New-Object IO.BinaryWriter($stream)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$Sizes.Count)
        $offset = 6 + (16 * $Sizes.Count)
        for ($i = 0; $i -lt $Sizes.Count; $i++) {
            $size = $Sizes[$i]
            $dimensionByte = if ($size -ge 256) { [byte]0 } else { [byte]$size }
            $writer.Write($dimensionByte)
            $writer.Write($dimensionByte)
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

Save-IconPng (Join-Path $output 'Square44x44Logo.png') 44
Save-IconPng (Join-Path $output 'Square150x150Logo.png') 150
Save-IconPng (Join-Path $output 'StoreLogo.png') 50
Save-Splash (Join-Path $output 'SplashScreen.png') 620 300

$scaleValues = @(100, 125, 150, 200, 400)
foreach ($scale in $scaleValues) {
    $factor = $scale / 100.0
    Save-IconPng (Join-Path $output ("Square44x44Logo.scale-{0}.png" -f $scale)) ([int][Math]::Round(44 * $factor))
    Save-IconPng (Join-Path $output ("Square150x150Logo.scale-{0}.png" -f $scale)) ([int][Math]::Round(150 * $factor))
    Save-IconPng (Join-Path $output ("StoreLogo.scale-{0}.png" -f $scale)) ([int][Math]::Round(50 * $factor))
    Save-Splash (Join-Path $output ("SplashScreen.scale-{0}.png" -f $scale)) ([int][Math]::Round(620 * $factor)) ([int][Math]::Round(300 * $factor))
}

$targetSizes = @(16, 20, 24, 30, 32, 36, 40, 44, 48, 60, 64, 72, 80, 96, 128, 256)
foreach ($size in $targetSizes) {
    Save-IconPng (Join-Path $output ("Square44x44Logo.targetsize-{0}.png" -f $size)) $size
    Save-IconPng (Join-Path $output ("Square44x44Logo.targetsize-{0}_altform-unplated.png" -f $size)) $size
}

Save-Ico (Join-Path $output 'AppIcon.ico') @(16, 20, 24, 32, 40, 48, 64, 128, 256)
Write-Host "Generated crisp multi-scale SpatialViewer icon assets in $output"
