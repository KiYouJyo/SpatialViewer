[CmdletBinding()]
param(
    [string]$Repository = 'KiYouJyo/SpatialViewer.CadCore',
    [string]$MinimumVersion = '0.2.1',
    [string]$Compatibility = 'SpatialViewer 0.2.x'
)

$ErrorActionPreference = 'Stop'

function Normalize-Version([version]$Version) {
    return [version]::new($Version.Major, $Version.Minor, [Math]::Max(0, $Version.Build))
}

$headers = @{
    'User-Agent' = 'SpatialViewer-StaticBinding-Smoke/0.2.4'
    'Accept' = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
    $headers['Authorization'] = "Bearer $env:GITHUB_TOKEN"
}

$temp = Join-Path $env:RUNNER_TEMP "cadcore-static-binding-$([Guid]::NewGuid().ToString('N'))"
$archive = Join-Path $temp 'CadCore.zip'
$expanded = Join-Path $temp 'expanded'
$kernelRoot = Join-Path $temp 'LocalState/Kernels/CadCore'
$versionsRoot = Join-Path $kernelRoot 'versions'
New-Item -ItemType Directory -Force -Path $expanded, $versionsRoot | Out-Null

$previousRoot = $env:SPATIALVIEWER_CADCORE_ROOT
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers $headers
    if (-not $release -or [string]::IsNullOrWhiteSpace([string]$release.tag_name)) {
        throw 'Latest Cad Core Release metadata is unavailable.'
    }

    $availableText = ([string]$release.tag_name).TrimStart('v', 'V')
    $available = Normalize-Version ([version]::Parse($availableText))
    $minimum = Normalize-Version ([version]::Parse($MinimumVersion))
    if ($available -lt $minimum) { throw "Latest Cad Core version $available is older than required $minimum." }

    $assetName = "CadCore-v$availableText-x64.zip"
    $asset = @($release.assets) | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    if (-not $asset) { throw "Required release asset is missing: $assetName" }
    if ([string]::IsNullOrWhiteSpace([string]$asset.digest) -or -not ([string]$asset.digest).StartsWith('sha256:', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$assetName does not expose a GitHub SHA-256 digest."
    }

    Invoke-WebRequest -Uri ([string]$asset.browser_download_url) -OutFile $archive -Headers @{ 'User-Agent' = 'SpatialViewer/0.2.4' }
    $expectedSize = [long]$asset.size
    $actualSize = (Get-Item -LiteralPath $archive).Length
    if ($expectedSize -gt 0 -and $actualSize -ne $expectedSize) {
        throw "Downloaded size mismatch: expected=$expectedSize actual=$actualSize"
    }
    $expectedHash = ([string]$asset.digest).Substring(7).ToLowerInvariant()
    $actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -cne $expectedHash) {
        throw "Downloaded SHA-256 mismatch: expected=$expectedHash actual=$actualHash"
    }

    Expand-Archive -LiteralPath $archive -DestinationPath $expanded -Force
    $manifestPath = Join-Path $expanded 'cadcore-release.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'cadcore-release.json is missing.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.product -cne 'SpatialViewer.CadCore') { throw "Unexpected product: $($manifest.product)" }
    if ($manifest.version -cne $availableText) { throw "Manifest version mismatch: $($manifest.version) != $availableText" }
    if ($manifest.runtime -cne 'x64') { throw "Unsupported Cad Core runtime: $($manifest.runtime)" }
    if ($manifest.sourceRepository -cne $Repository) { throw "Unexpected source repository: $($manifest.sourceRepository)" }
    if ($manifest.compatibility -cne $Compatibility) { throw "Unexpected compatibility contract: $($manifest.compatibility)" }

    $finalDirectory = Join-Path $versionsRoot $availableText
    Move-Item -LiteralPath $expanded -Destination $finalDirectory
    @{ Version = $availableText } | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $kernelRoot 'pending.json') -Encoding utf8

    $env:SPATIALVIEWER_CADCORE_ROOT = $kernelRoot
    dotnet run --project packaging/CadCoreActivationProbe/CadCoreActivationProbe.csproj -c Release -- $availableText
    if ($LASTEXITCODE -ne 0) { throw "Cad Core static-binding activation probe failed: $LASTEXITCODE" }

    $pendingPath = Join-Path $kernelRoot 'pending.json'
    if (Test-Path -LiteralPath $pendingPath) { throw 'pending.json still exists after static-binding activation.' }
    $activePath = Join-Path $kernelRoot 'active.json'
    if (-not (Test-Path -LiteralPath $activePath -PathType Leaf)) { throw 'active.json was not created after static-binding activation.' }
    $active = Get-Content -LiteralPath $activePath -Raw | ConvertFrom-Json
    if ([string]$active.Version -cne $availableText) { throw "Active-state version mismatch: $($active.Version)" }

    Write-Host "Cad Core static-binding startup contract PASS: bundled project reference -> active $availableText"
}
finally {
    $env:SPATIALVIEWER_CADCORE_ROOT = $previousRoot
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
