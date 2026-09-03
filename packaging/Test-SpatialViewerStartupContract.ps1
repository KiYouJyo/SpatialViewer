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
            throw "Native bootstrap background mismatch for ${Name}: expected opaque #202020, found A=$($corner.A) R=$($corner.R) G=$($corner.G) B=$($corner.B)."
        }

        $center = $bitmap.GetPixel([int]($bitmap.Width / 2), [int]($bitmap.Height / 2))
        if ($center.R -eq 32 -and $center.G -eq 32 -and $center.B -eq 32) {
            throw "Native bootstrap product mark is not visible at the center of ${Name}."
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

# Stage 1: Windows/MSIX bootstrap resources remain DPI-qualified and optional.
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
    throw 'Stage-1 startup contract must keep the native optional #202020 bootstrap resource.'
}

# Stage 2: the real WinUI window owns a transparent overlay on the Mica surface.
$xamlPath = Join-Path $PSScriptRoot '..\src\SpatialViewer.App\MainWindow.xaml'
$xaml = Get-Content -LiteralPath $xamlPath -Raw
foreach ($required in @(
    '<MicaBackdrop />',
    'x:Name="ShellContent" Opacity="0" IsHitTestVisible="False"',
    'x:Name="StartupOverlay" Background="Transparent"',
    'Source="ms-appx:///Assets/Square150x150Logo.png"',
    'ImageOpened="OnStartupLogoImageOpened"',
    'ImageFailed="OnStartupLogoImageFailed"'
)) {
    if (-not $xaml.Contains($required)) { throw "Stage-2 startup XAML contract missing: $required" }
}

# Preserve the accepted title-bar geometry exactly while adding only an outer startup layer.
$titleBarContract = '<Grid x:Name="AppTitleBar" Grid.Row="0" Background="Transparent" ColumnDefinitions="104,*,132" Padding="16,0,12,0">'
if (-not $xaml.Contains($titleBarContract)) {
    throw 'Startup correction changed the accepted title-bar geometry.'
}
if (-not $xaml.Contains('<TextBlock Text="SpatialViewer" Style="{StaticResource BodyText}" FontWeight="SemiBold" VerticalAlignment="Center" />')) {
    throw 'Startup correction changed the accepted title-bar product text surface.'
}

$startupCodePath = Join-Path $PSScriptRoot '..\src\SpatialViewer.App\MainWindow.Startup.cs'
$startupCode = Get-Content -LiteralPath $startupCodePath -Raw
foreach ($required in @(
    'CompositionTarget.Rendering += OnStartupOverlayRendered;',
    'Task.Delay(TimeSpan.FromSeconds(1))',
    'Task.Delay(TimeSpan.FromSeconds(5))',
    'StartupSplashTiming.RemainingMinimumVisibleDuration',
    'ShellContent.Opacity = 1;',
    'new DoubleAnimation',
    'EasingMode = EasingMode.EaseOut',
    'StartupOverlay.Visibility = Visibility.Collapsed;',
    'ShellContent.IsHitTestVisible = true;'
)) {
    if (-not $startupCode.Contains($required)) { throw "Stage-2 startup runtime contract missing: $required" }
}

$timingPath = Join-Path $PSScriptRoot '..\src\SpatialViewer.App\StartupSplashTiming.cs'
$timing = Get-Content -LiteralPath $timingPath -Raw
foreach ($required in @(
    'MinimumVisibleDuration = TimeSpan.FromMilliseconds(500)',
    'FadeOutDuration = TimeSpan.FromMilliseconds(200)',
    'FadeOutFallbackDuration = TimeSpan.FromMilliseconds(300)'
)) {
    if (-not $timing.Contains($required)) { throw "Startup timing contract missing: $required" }
}

$projectPath = Join-Path $PSScriptRoot '..\src\SpatialViewer.App\SpatialViewer.App.csproj'
$project = Get-Content -LiteralPath $projectPath -Raw
if ($project -notmatch '<AppxBundleAutoResourcePackageQualifiers>Scale\|DXFeatureLevel</AppxBundleAutoResourcePackageQualifiers>') {
    throw 'Startup DPI contract missing Scale resource-package qualifier.'
}
if ($project -notmatch 'New-SpatialViewerStartupScreen\.ps1' -or $project -notmatch 'Test-SpatialViewerStartupContract\.ps1') {
    throw 'Startup generation/validation hooks are not wired into the app build.'
}

Write-Host 'SpatialViewer startup contract PASS: optional native bootstrap + Mica in-window overlay + 500 ms render-gated presentation + 200 ms fade + fail-open watchdog.'
