[CmdletBinding()]
param(
    [string]$Repository = 'KiYouJyo/SpatialViewer.CadCore',
    [string]$MinimumVersion = '0.9.0',
    [string]$RequiredAbiVersion = '1.0.0.0',
    [string]$HostContractName = 'SpatialViewer.CadHost',
    [string]$HostContractVersion = '1.0.0'
)

$ErrorActionPreference = 'Stop'

function Normalize-Version([version]$Version) {
    return [version]::new($Version.Major, $Version.Minor, [Math]::Max(0, $Version.Build))
}

function Assert-HostContract([object]$Manifest) {
    if ([int]$Manifest.schemaVersion -ne 2) { throw "Unexpected CadCore manifest schema: $($Manifest.schemaVersion)" }
    if (-not $Manifest.hostContract) { throw 'Manifest hostContract is missing.' }
    if ([string]$Manifest.hostContract.name -cne $HostContractName) { throw "Unexpected host contract name: $($Manifest.hostContract.name)" }
    $host = [version]$HostContractVersion
    $min = [version][string]$Manifest.hostContract.minVersion
    $max = [version][string]$Manifest.hostContract.maxVersionExclusive
    if ($min -ge $max) { throw "Invalid host contract range: $min..<${max}" }
    if ($host -lt $min -or $host -ge $max) { throw "Host contract is incompatible: host=$host package=$min..<${max}" }
}

$headers = @{
    'User-Agent' = 'SpatialViewer-Acceptance/0.3.1'
    'Accept' = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
    $headers['Authorization'] = "Bearer $env:GITHUB_TOKEN"
}

$temp = Join-Path $env:RUNNER_TEMP "cadcore-update-contract-$([Guid]::NewGuid().ToString('N'))"
$archive = Join-Path $temp 'CadCore.zip'
$manifestDownload = Join-Path $temp 'cadcore-release.json'
$expanded = Join-Path $temp 'expanded'
New-Item -ItemType Directory -Force -Path $expanded | Out-Null

try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers $headers
    if (-not $release -or [string]::IsNullOrWhiteSpace([string]$release.tag_name)) { throw 'Latest CadCore Release metadata is unavailable.' }

    $availableText = ([string]$release.tag_name).TrimStart('v', 'V')
    $available = Normalize-Version ([version]::Parse($availableText))
    $minimum = Normalize-Version ([version]::Parse($MinimumVersion))
    if ($available -lt $minimum) { throw "Latest CadCore version $available is older than required $minimum." }

    $assetName = "CadCore-v$availableText-x64.zip"
    $asset = @($release.assets) | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    if (-not $asset) { throw "Required release asset is missing: $assetName" }
    $manifestAsset = @($release.assets) | Where-Object { $_.name -eq 'cadcore-release.json' } | Select-Object -First 1
    if (-not $manifestAsset) { throw 'Standalone cadcore-release.json release asset is missing.' }

    foreach ($pair in @(@($manifestAsset, $manifestDownload), @($asset, $archive))) {
        $releaseAsset = $pair[0]
        $path = [string]$pair[1]
        if ([string]::IsNullOrWhiteSpace([string]$releaseAsset.digest) -or -not ([string]$releaseAsset.digest).StartsWith('sha256:', [StringComparison]::OrdinalIgnoreCase)) {
            throw "$($releaseAsset.name) does not expose a GitHub SHA-256 digest."
        }
        Invoke-WebRequest -Uri ([string]$releaseAsset.browser_download_url) -Headers @{ 'User-Agent' = 'SpatialViewer-Acceptance/0.3.1' } -OutFile $path
        $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        $expectedHash = ([string]$releaseAsset.digest).Substring(7).ToLowerInvariant()
        if ($actualHash -cne $expectedHash) { throw "Release digest mismatch for $($releaseAsset.name): expected=$expectedHash actual=$actualHash" }
    }

    $preflight = Get-Content -LiteralPath $manifestDownload -Raw | ConvertFrom-Json
    if ($preflight.product -cne 'SpatialViewer.CadCore') { throw "Unexpected product: $($preflight.product)" }
    if ($preflight.version -cne $availableText) { throw "Manifest version mismatch: $($preflight.version) != $availableText" }
    if ($preflight.tag -cne "v$availableText") { throw "Manifest tag mismatch: $($preflight.tag)" }
    if ($preflight.runtime -cne 'x64') { throw "Unsupported CadCore runtime: $($preflight.runtime)" }
    if ($preflight.sourceRepository -cne $Repository) { throw "Unexpected source repository: $($preflight.sourceRepository)" }
    if ([string]$preflight.abiVersion -cne $RequiredAbiVersion) { throw "Unexpected ABI: $($preflight.abiVersion) != $RequiredAbiVersion" }
    Assert-HostContract $preflight

    Expand-Archive -LiteralPath $archive -DestinationPath $expanded -Force
    $manifestPath = Join-Path $expanded 'cadcore-release.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'cadcore-release.json is missing from archive.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ([string]$manifest.commit -cne [string]$preflight.commit -or [string]$manifest.version -cne [string]$preflight.version -or [string]$manifest.abiVersion -cne [string]$preflight.abiVersion) {
        throw 'Archive manifest does not match standalone preflight manifest.'
    }
    Assert-HostContract $manifest
    $abiVersion = [version]::Parse([string]$manifest.abiVersion)

    $projects = @(
        'SpatialViewer.Core',
        'SpatialViewer.Formats.Cad',
        'SpatialViewer.Formats.Cad.ACadSharp',
        'SpatialViewer.Rendering',
        'SpatialViewer.Rendering.Windows'
    )
    foreach ($project in $projects) {
        $projectRoot = Join-Path (Join-Path $expanded 'bin') $project
        if (-not (Test-Path -LiteralPath $projectRoot -PathType Container)) { throw "Missing project payload: $project" }
        $dll = Get-ChildItem -LiteralPath $projectRoot -Recurse -Filter "$project.dll" | Select-Object -First 1
        if (-not $dll) { throw "Missing required assembly: $project.dll" }
        $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dll.FullName).Version
        if ($assemblyVersion -ne $abiVersion) { throw "ABI mismatch for $($project).dll: $assemblyVersion != $abiVersion" }
        $fileVersionText = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll.FullName).FileVersion
        if ([string]::IsNullOrWhiteSpace($fileVersionText)) { throw "FileVersion is missing for $($project).dll" }
        $fileVersion = Normalize-Version ([version]::Parse($fileVersionText))
        if ($fileVersion -ne $available) { throw "Product version mismatch for $($project).dll: $fileVersion != $available" }
    }

    Write-Host "CadCore updater release contract PASS: product=v$availableText ABI=$abiVersion host=$HostContractName $HostContractVersion / schema=2 / standalone manifest preflight PASS"
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
