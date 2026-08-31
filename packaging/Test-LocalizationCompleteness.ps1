param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

$languages = @('zh-CN', 'ja-JP', 'en-US')
$resourceMaps = @{}
foreach ($language in $languages) {
    $path = Join-Path $ProjectRoot "src/SpatialViewer.App/Strings/$language/Resources.resw"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing resource file: $path" }
    [xml]$xml = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    $map = @{}
    foreach ($node in $xml.root.data) {
        $name = [string]$node.name
        $value = [string]$node.value
        if ([string]::IsNullOrWhiteSpace($name)) { continue }
        if ($map.ContainsKey($name)) { throw "Duplicate resource key in ${language}: $name" }
        if ([string]::IsNullOrWhiteSpace($value)) { throw "Empty resource value in ${language}: $name" }
        $map[$name] = $value
    }
    $resourceMaps[$language] = $map
}

$referenceKeys = @($resourceMaps['zh-CN'].Keys | Sort-Object)
foreach ($language in @('ja-JP', 'en-US')) {
    $keys = @($resourceMaps[$language].Keys | Sort-Object)
    $missing = @($referenceKeys | Where-Object { -not $resourceMaps[$language].ContainsKey($_) })
    $extra = @($keys | Where-Object { -not $resourceMaps['zh-CN'].ContainsKey($_) })
    if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
        throw "Resource key mismatch for $language. Missing: $($missing -join ', '); Extra: $($extra -join ', ')"
    }
}

function Assert-Key([string]$key, [string]$origin) {
    foreach ($language in $languages) {
        if (-not $resourceMaps[$language].ContainsKey($key)) {
            throw "Missing localization key '$key' for $language (referenced by $origin)."
        }
    }
}

$xamlFiles = @(
    'src/SpatialViewer.App/MainWindow.xaml',
    'src/SpatialViewer.App/Views/HomeView.xaml',
    'src/SpatialViewer.App/Views/CadViewerView.xaml'
)
foreach ($relative in $xamlFiles) {
    $path = Join-Path $ProjectRoot $relative
    $text = Get-Content -LiteralPath $path -Raw -Encoding UTF8
    foreach ($match in [regex]::Matches($text, 'x:Uid="([^"]+)"')) {
        $uid = $match.Groups[1].Value
        $hasResource = $referenceKeys | Where-Object { $_ -eq $uid -or $_.StartsWith("$uid.", [StringComparison]::Ordinal) } | Select-Object -First 1
        if (-not $hasResource) { throw "x:Uid '$uid' in $relative has no resource entry." }
    }
}

$csFiles = Get-ChildItem (Join-Path $ProjectRoot 'src/SpatialViewer.App') -Recurse -Filter '*.cs'
foreach ($file in $csFiles) {
    $text = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    foreach ($match in [regex]::Matches($text, '(?:\bT|GetString)\("([A-Za-z0-9_.]+)"\)')) {
        Assert-Key $match.Groups[1].Value $file.FullName
    }
}

$mainWindowPath = Join-Path $ProjectRoot 'src/SpatialViewer.App/MainWindow.xaml.cs'
$mainWindow = Get-Content -LiteralPath $mainWindowPath -Raw -Encoding UTF8
$forbiddenShellFallbacks = @(
    'CreateTabVisual(id, "主页"',
    'ShowPlaceholder("项目"',
    'ShowPlaceholder("收藏"',
    'ShowPlaceholder("导入文件夹"',
    'CloseButtonText = "关闭"'
)
foreach ($fragment in $forbiddenShellFallbacks) {
    if ($mainWindow.Contains($fragment, [StringComparison]::Ordinal)) {
        throw "Hard-coded shell localization fallback returned: $fragment"
    }
}

$aboutPath = Join-Path $ProjectRoot 'src/SpatialViewer.App/Views/AboutView.xaml.cs'
$about = Get-Content -LiteralPath $aboutPath -Raw -Encoding UTF8
if (-not $about.Contains('ProductNameText.Text = T("AppName")', [StringComparison]::Ordinal)) {
    throw 'About product name is not bound to the localized AppName resource.'
}

Write-Host "Localization contract PASS: $($referenceKeys.Count) synchronized keys across zh-CN / ja-JP / en-US; XAML and runtime key references resolved."
