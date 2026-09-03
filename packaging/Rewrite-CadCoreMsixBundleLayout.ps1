param(
    [Parameter(Mandatory = $true)]
    [string]$BundlePath,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$BundledVersion,

    [string]$PayloadDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$bundle = [IO.Path]::GetFullPath($BundlePath)
if (-not (Test-Path -LiteralPath $bundle -PathType Leaf)) {
    throw "MSIXBundle was not found: $bundle"
}

$payloadRoot = $null
if (-not [string]::IsNullOrWhiteSpace($PayloadDirectory)) {
    $payloadRoot = [IO.Path]::GetFullPath($PayloadDirectory)
    if (-not (Test-Path -LiteralPath $payloadRoot -PathType Container)) {
        throw "Cad Core payload directory was not found: $payloadRoot"
    }
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

if ($payloadRoot) {
    foreach ($name in $cadCoreNames) {
        $source = Join-Path $payloadRoot $name
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Resolved Cad Core payload is missing: $source"
        }
        $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($source).FileVersion
        if ([string]::IsNullOrWhiteSpace($fileVersion) -or -not $fileVersion.StartsWith("$BundledVersion.")) {
            throw "Resolved Cad Core payload version mismatch for ${name}: $fileVersion does not match $BundledVersion"
        }
    }
}

$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("SpatialViewer-CadCore-Rewrite-" + [Guid]::NewGuid().ToString('N'))
$unbundle = Join-Path $tempRoot 'bundle'
$unpack = Join-Path $tempRoot 'package'
$bundleInputs = Join-Path $tempRoot 'bundle-inputs'
$verifyBundle = Join-Path $tempRoot 'verify-bundle'
$repackedInner = Join-Path $tempRoot 'repacked.msix'
$repackedBundle = Join-Path $tempRoot 'repacked.msixbundle'

try {
    New-Item -ItemType Directory -Force -Path $unbundle, $unpack, $bundleInputs | Out-Null

    & $makeappx.FullName unbundle /p $bundle /d $unbundle /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makeappx unbundle failed: $LASTEXITCODE" }

    $bundleManifestPath = Join-Path $unbundle 'AppxMetadata\AppxBundleManifest.xml'
    if (-not (Test-Path -LiteralPath $bundleManifestPath -PathType Leaf)) {
        throw 'AppxBundleManifest.xml was not found after unbundling.'
    }
    [xml]$bundleManifest = Get-Content -LiteralPath $bundleManifestPath -Raw
    $bundleIdentity = $bundleManifest.SelectSingleNode("/*[local-name()='Bundle']/*[local-name()='Identity']")
    $bundleVersion = if ($bundleIdentity) { [string]$bundleIdentity.Version } else { '' }
    if ($bundleVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "Original bundle version is invalid: $bundleVersion"
    }

    # A proper high-DPI bundle contains one architecture package plus zero or more
    # resource packs (for example scale-100/125/150/400). Rewrite only the x64
    # architecture package and copy resource packs back byte-for-byte.
    $allInnerPackages = @(Get-ChildItem -LiteralPath $unbundle -Filter '*.msix' -File)
    $architecturePackages = @($allInnerPackages | Where-Object { $_.Name -match '_x64\.msix$' })
    if ($architecturePackages.Count -ne 1) {
        throw "Expected exactly one inner x64 architecture MSIX, found $($architecturePackages.Count) among $($allInnerPackages.Count) bundle packages."
    }
    $inner = $architecturePackages[0]

    & $makeappx.FullName unpack /p $inner.FullName /d $unpack /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makeappx unpack failed: $LASTEXITCODE" }

    $packageManifestPath = Join-Path $unpack 'AppxManifest.xml'
    if (-not (Test-Path -LiteralPath $packageManifestPath -PathType Leaf)) {
        throw 'AppxManifest.xml was not found after unpacking the inner MSIX.'
    }
    [xml]$packageManifest = Get-Content -LiteralPath $packageManifestPath -Raw
    $packageIdentity = $packageManifest.SelectSingleNode("/*[local-name()='Package']/*[local-name()='Identity']")
    $packageVersion = if ($packageIdentity) { [string]$packageIdentity.Version } else { '' }
    if ($packageVersion -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "Inner package version is invalid: $packageVersion"
    }
    if ($packageVersion -ne $bundleVersion) {
        throw "Bundle/package version mismatch before rewrite: bundle=$bundleVersion package=$packageVersion"
    }

    $fallback = Join-Path $unpack "Kernels\Bundled\$BundledVersion"
    New-Item -ItemType Directory -Force -Path $fallback | Out-Null

    foreach ($name in $cadCoreNames) {
        $rootCopy = Join-Path $unpack $name
        $destination = Join-Path $fallback $name
        if ($payloadRoot) {
            Remove-Item -LiteralPath $rootCopy -Force -ErrorAction SilentlyContinue
            Copy-Item -LiteralPath (Join-Path $payloadRoot $name) -Destination $destination -Force
        }
        else {
            if (-not (Test-Path -LiteralPath $rootCopy -PathType Leaf)) {
                throw "Cad Core root payload was not found before rewrite: $name"
            }
            Move-Item -LiteralPath $rootCopy -Destination $destination -Force
        }
    }

    foreach ($footprint in @('AppxBlockMap.xml', 'AppxSignature.p7x', '[Content_Types].xml')) {
        Remove-Item -LiteralPath (Join-Path $unpack $footprint) -Force -ErrorAction SilentlyContinue
    }

    & $makeappx.FullName pack /d $unpack /p $repackedInner /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed: $LASTEXITCODE" }

    foreach ($package in $allInnerPackages) {
        $destination = Join-Path $bundleInputs $package.Name
        if ($package.FullName -eq $inner.FullName) {
            Copy-Item -LiteralPath $repackedInner -Destination $destination -Force
        }
        else {
            Copy-Item -LiteralPath $package.FullName -Destination $destination -Force
        }
    }

    & $makeappx.FullName bundle /d $bundleInputs /p $repackedBundle /bv $bundleVersion /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makeappx bundle failed: $LASTEXITCODE" }

    New-Item -ItemType Directory -Force -Path $verifyBundle | Out-Null
    & $makeappx.FullName unbundle /p $repackedBundle /d $verifyBundle /o | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "makeappx verification unbundle failed: $LASTEXITCODE" }
    $verifyManifestPath = Join-Path $verifyBundle 'AppxMetadata\AppxBundleManifest.xml'
    [xml]$verifyManifest = Get-Content -LiteralPath $verifyManifestPath -Raw
    $verifyIdentity = $verifyManifest.SelectSingleNode("/*[local-name()='Bundle']/*[local-name()='Identity']")
    $rewrittenVersion = if ($verifyIdentity) { [string]$verifyIdentity.Version } else { '' }
    if ($rewrittenVersion -ne $bundleVersion -or $rewrittenVersion -ne $packageVersion) {
        throw "Rewritten bundle version mismatch: rewritten=$rewrittenVersion original=$bundleVersion package=$packageVersion"
    }

    $verifiedPackages = @(Get-ChildItem -LiteralPath $verifyBundle -Filter '*.msix' -File)
    if ($verifiedPackages.Count -ne $allInnerPackages.Count) {
        throw "Resource-pack preservation failed: before=$($allInnerPackages.Count) after=$($verifiedPackages.Count)."
    }

    Move-Item -LiteralPath $repackedBundle -Destination $bundle -Force
    $sourceLabel = if ($payloadRoot) { 'resolved latest integrated release' } else { 'build output' }
    Write-Host "Rewrote MSIX Cad Core layout: root payload removed; bundled fallback=$BundledVersion; source=$sourceLabel; package=$bundleVersion; preserved resource packs=$($allInnerPackages.Count - 1)."
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
