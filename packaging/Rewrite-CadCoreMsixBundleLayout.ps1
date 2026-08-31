param(
    [Parameter(Mandatory = $true)]
    [string]$BundlePath,

    [string]$BundledVersion = '0.3.0'
)

$ErrorActionPreference = 'Stop'

$bundle = [IO.Path]::GetFullPath($BundlePath)
if (-not (Test-Path -LiteralPath $bundle -PathType Leaf)) {
    throw "MSIXBundle was not found: $bundle"
}

$makeappx = Get-ChildItem (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin') -Recurse -Filter makeappx.exe |
    Where-Object FullName -match '\\x64\\makeappx.exe$' |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if (-not $makeappx) {
    throw 'x64 makeappx.exe was not found.'
}

$cadCoreNames = @(
    'SpatialViewer.Core.dll',
    'SpatialViewer.Rendering.dll',
    'SpatialViewer.Rendering.Windows.dll',
    'SpatialViewer.Formats.Cad.dll',
    'SpatialViewer.Formats.Cad.ACadSharp.dll'
)

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("SpatialViewer-CadCore-Rewrite-" + [Guid]::NewGuid().ToString('N'))
$unbundle = Join-Path $tempRoot 'bundle'
$unpack = Join-Path $tempRoot 'package'
$bundleInputs = Join-Path $tempRoot 'bundle-inputs'
$repackedInner = Join-Path $tempRoot 'repacked.msix'
$repackedBundle = Join-Path $tempRoot 'repacked.msixbundle'

try {
    New-Item -ItemType Directory -Force -Path $unbundle, $unpack, $bundleInputs | Out-Null

    & $makeappx.FullName unbundle /p $bundle /d $unbundle /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makeappx unbundle failed: $LASTEXITCODE" }

    $innerPackages = @(Get-ChildItem -LiteralPath $unbundle -Filter '*.msix' -File)
    if ($innerPackages.Count -ne 1) {
        throw "Expected exactly one inner x64 MSIX, found $($innerPackages.Count)."
    }
    $inner = $innerPackages[0]

    & $makeappx.FullName unpack /p $inner.FullName /d $unpack /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makeappx unpack failed: $LASTEXITCODE" }

    $fallback = Join-Path $unpack "Kernels\Bundled\$BundledVersion"
    New-Item -ItemType Directory -Force -Path $fallback | Out-Null

    foreach ($name in $cadCoreNames) {
        $rootCopy = Join-Path $unpack $name
        if (-not (Test-Path -LiteralPath $rootCopy -PathType Leaf)) {
            throw "Cad Core root payload was not found before rewrite: $name"
        }
        $destination = Join-Path $fallback $name
        Move-Item -LiteralPath $rootCopy -Destination $destination -Force
    }

    # MakeAppx regenerates package footprint files. They must not be fed back
    # into the pack operation from the unpacked source directory.
    foreach ($footprint in @('AppxBlockMap.xml', 'AppxSignature.p7x', '[Content_Types].xml')) {
        Remove-Item -LiteralPath (Join-Path $unpack $footprint) -Force -ErrorAction SilentlyContinue
    }

    & $makeappx.FullName pack /d $unpack /p $repackedInner /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed: $LASTEXITCODE" }

    $innerInput = Join-Path $bundleInputs $inner.Name
    Copy-Item -LiteralPath $repackedInner -Destination $innerInput -Force

    & $makeappx.FullName bundle /d $bundleInputs /p $repackedBundle /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed: $LASTEXITCODE" }

    Move-Item -LiteralPath $repackedBundle -Destination $bundle -Force

    Write-Host "Rewrote MSIX Cad Core layout: root payload removed; bundled fallback=$BundledVersion."
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
