[CmdletBinding()]
param([string]$AssetsDirectory)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($AssetsDirectory)) {
    $AssetsDirectory = Join-Path $PSScriptRoot '..\src\SpatialViewer.App\Assets'
}

Add-Type -AssemblyName System.Drawing
$assets = [IO.Path]::GetFullPath($AssetsDirectory)

function Assert-Png([string]$Name, [int]$Width, [int]$Height) {
    $path = Join-Path $assets $Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing branding asset: $Name" }
    $image = [Drawing.Image]::FromFile($path)
    try {
        if ($image.Width -ne $Width -or $image.Height -ne $Height) {
            throw "Unexpected dimensions for $Name: $($image.Width)x$($image.Height), expected ${Width}x${Height}."
        }
    }
    finally { $image.Dispose() }
}

Assert-Png 'Square44x44Logo.png' 44 44
Assert-Png 'Square150x150Logo.png' 150 150
Assert-Png 'StoreLogo.png' 50 50
Assert-Png 'SplashScreen.png' 620 300

foreach ($size in @(16, 20, 24, 30, 32, 36, 40, 44, 48, 60, 64, 72, 80, 96, 128, 256)) {
    Assert-Png ("Square44x44Logo.targetsize-{0}.png" -f $size) $size $size
    Assert-Png ("Square44x44Logo.targetsize-{0}_altform-unplated.png" -f $size) $size $size
}

$scaleExpectations = @{
    'Square44x44Logo.scale-100.png' = @(44, 44)
    'Square44x44Logo.scale-125.png' = @(55, 55)
    'Square44x44Logo.scale-150.png' = @(66, 66)
    'Square44x44Logo.scale-200.png' = @(88, 88)
    'Square44x44Logo.scale-400.png' = @(176, 176)
    'Square150x150Logo.scale-100.png' = @(150, 150)
    'Square150x150Logo.scale-200.png' = @(300, 300)
    'Square150x150Logo.scale-400.png' = @(600, 600)
    'StoreLogo.scale-100.png' = @(50, 50)
    'StoreLogo.scale-200.png' = @(100, 100)
    'StoreLogo.scale-400.png' = @(200, 200)
}
foreach ($entry in $scaleExpectations.GetEnumerator()) {
    Assert-Png $entry.Key ([int]$entry.Value[0]) ([int]$entry.Value[1])
}

$icoPath = Join-Path $assets 'AppIcon.ico'
if (-not (Test-Path -LiteralPath $icoPath -PathType Leaf)) { throw 'Missing AppIcon.ico.' }
$stream = [IO.File]::OpenRead($icoPath)
$reader = [IO.BinaryReader]::new($stream)
try {
    $reserved = $reader.ReadUInt16()
    $type = $reader.ReadUInt16()
    $count = $reader.ReadUInt16()
    if ($reserved -ne 0 -or $type -ne 1 -or $count -lt 9) { throw "Invalid multi-resolution ICO header: reserved=$reserved type=$type count=$count" }
    $sizes = [Collections.Generic.HashSet[int]]::new()
    for ($i = 0; $i -lt $count; $i++) {
        $widthByte = $reader.ReadByte()
        $heightByte = $reader.ReadByte()
        $reader.ReadByte() | Out-Null
        $reader.ReadByte() | Out-Null
        $reader.ReadUInt16() | Out-Null
        $reader.ReadUInt16() | Out-Null
        $reader.ReadUInt32() | Out-Null
        $reader.ReadUInt32() | Out-Null
        $width = if ($widthByte -eq 0) { 256 } else { [int]$widthByte }
        $height = if ($heightByte -eq 0) { 256 } else { [int]$heightByte }
        if ($width -ne $height) { throw "Non-square ICO frame: ${width}x${height}." }
        $sizes.Add($width) | Out-Null
    }
    foreach ($required in @(16, 20, 24, 32, 40, 48, 64, 128, 256)) {
        if (-not $sizes.Contains($required)) { throw "AppIcon.ico is missing ${required}x${required} frame." }
    }
}
finally {
    $reader.Dispose()
    $stream.Dispose()
}

$mainWindow = Get-Content (Join-Path $PSScriptRoot '..\src\SpatialViewer.App\MainWindow.xaml') -Raw
if ($mainWindow -match 'ms-appx:///Assets/Square44x44Logo\.png') {
    throw 'Custom title bar regression: MainWindow.xaml must not render the product icon.'
}

$appCode = Get-Content (Join-Path $PSScriptRoot '..\src\SpatialViewer.App\App.xaml.cs') -Raw
if ($appCode -notmatch 'AppWindow\.SetIcon\(iconPath\)') {
    throw 'Native window icon contract missing: AppWindow.SetIcon(iconPath) was not found.'
}

Write-Host 'SpatialViewer branding contract PASS: pixel-specific shell assets, multi-resolution ICO, native window icon, and untouched custom title bar.'
