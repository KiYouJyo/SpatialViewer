param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'
$cadXamlPath = Join-Path $ProjectRoot 'src/SpatialViewer.App/Views/CadViewerView.xaml'
$rhinoXamlPath = Join-Path $ProjectRoot 'src/SpatialViewer.App/Views/ThreeDmViewerView.xaml'
$cadCodePath = Join-Path $ProjectRoot 'src/SpatialViewer.App/Views/CadViewerView.xaml.cs'
$rhinoCodePath = Join-Path $ProjectRoot 'src/SpatialViewer.App/Views/ThreeDmViewerView.xaml.cs'

$cadXaml = Get-Content -LiteralPath $cadXamlPath -Raw -Encoding UTF8
$rhinoXaml = Get-Content -LiteralPath $rhinoXamlPath -Raw -Encoding UTF8
$cadCode = Get-Content -LiteralPath $cadCodePath -Raw -Encoding UTF8
$rhinoCode = Get-Content -LiteralPath $rhinoCodePath -Raw -Encoding UTF8

function Assert-Contains([string]$text, [string]$fragment, [string]$message) {
    if (-not $text.Contains($fragment, [StringComparison]::Ordinal)) { throw $message }
}

function Get-ControlType([string]$text, [string]$name) {
    $pattern = '<(?<type>AppBar(?:Toggle)?Button)\b[^>]*\bx:Name="' + [regex]::Escape($name) + '"'
    $match = [regex]::Match($text, $pattern, [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) { throw "Control '$name' was not found." }
    return $match.Groups['type'].Value
}

Assert-Contains $cadXaml 'RowDefinitions="64,*,24"' 'CAD viewer chrome contract changed unexpectedly.'
Assert-Contains $rhinoXaml 'RowDefinitions="64,*,24"' 'Rhino viewer must use the same 64/*/24 row contract as CAD.'
Assert-Contains $cadXaml 'x:Name="ViewerToolbar"' 'CAD toolbar contract is missing.'
Assert-Contains $rhinoXaml 'x:Name="ViewerToolbar"' 'Rhino viewer toolbar must mirror the CAD toolbar surface.'
Assert-Contains $rhinoXaml 'x:Name="LeftPaneHost" Grid.Row="1" DisplayMode="Inline"' 'Rhino left pane must remain an inline SplitView.'
Assert-Contains $rhinoXaml 'x:Name="RightPaneHost" DisplayMode="Inline"' 'Rhino right pane must remain an inline SplitView.'
Assert-Contains $rhinoXaml 'OpenPaneLength="300"' 'Rhino default pane width must match CAD.'
Assert-Contains $rhinoXaml 'x:Name="PropertiesList"' 'Rhino properties must use the CAD-style property list.'
Assert-Contains $rhinoXaml 'x:Name="PropertiesEmpty"' 'Rhino properties must keep the CAD-style empty state.'
Assert-Contains $rhinoXaml 'x:Name="DiagnosticsBar"' 'Rhino properties must keep the CAD-style diagnostics surface.'
if ($rhinoXaml.Contains('<ComboBox', [StringComparison]::Ordinal)) {
    throw 'Rhino viewer must not introduce standalone ComboBox toolbar controls; use the CAD-native AppBar control language.'
}

foreach ($name in @('LayersButton', 'SelectTool', 'PanTool', 'PropertiesButton')) {
    $cadType = Get-ControlType $cadXaml $name
    $rhinoType = Get-ControlType $rhinoXaml $name
    if ($cadType -cne $rhinoType) {
        throw "Rhino control '$name' must use the same control type as CAD. CAD=$cadType Rhino=$rhinoType"
    }
}

foreach ($fragment in @(
    'e.NewSize.Width >= 1280',
    'e.NewSize.Width >= 800',
    'Large => 300d',
    'Medium => 240d',
    '_ => 220d',
    'SplitViewDisplayMode.Inline'
)) {
    Assert-Contains $cadCode $fragment "CAD responsive contract fragment missing: $fragment"
    Assert-Contains $rhinoCode $fragment "Rhino responsive behavior must match CAD: $fragment"
}

Write-Host 'Rhino/CAD viewer parity contract PASS: native toolbar controls, inline panes, responsive widths, properties surface, and status chrome remain aligned.'
