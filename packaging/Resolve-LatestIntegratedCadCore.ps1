[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$PolicyPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'release\kernel-integration.json')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$policyFile = [IO.Path]::GetFullPath($PolicyPath)
if (-not (Test-Path -LiteralPath $policyFile -PathType Leaf)) {
    throw "Kernel integration policy was not found: $policyFile"
}

$policy = Get-Content -LiteralPath $policyFile -Raw | ConvertFrom-Json
if ([int]$policy.schemaVersion -ne 2) { throw "Unsupported kernel integration policy schema: $($policy.schemaVersion)" }
$kernel = @($policy.kernels | Where-Object { $_.id -eq 'cad' }) | Select-Object -First 1
if (-not $kernel) { throw 'Integrated CAD kernel policy entry is missing.' }
if ([string]$kernel.integrationStatus -cne 'complete') { throw 'CAD kernel integration is not marked complete; refusing to bundle it.' }
if ([string]$kernel.track -cne 'latest-stable') { throw "Unsupported CAD kernel release track: $($kernel.track)" }
if ([string]$kernel.runtime -cne 'x64') { throw "Unsupported CAD kernel runtime: $($kernel.runtime)" }

$hostContractId = [string]$kernel.hostContract
$hostContract = $policy.hostContracts.$hostContractId
if (-not $hostContract) { throw "CAD host contract policy is missing: $hostContractId" }
$hostContractName = [string]$hostContract.name
$hostContractVersion = [version][string]$hostContract.version
$repository = [string]$kernel.repository
$requiredAbi = [string]$kernel.requiredAbiVersion
$requiredManifestSchema = [int]$kernel.manifestSchemaVersion
$assetTemplate = [string]$kernel.assetNameTemplate
$manifestAssetName = [string]$kernel.manifestAssetName
if ([string]::IsNullOrWhiteSpace($repository) -or [string]::IsNullOrWhiteSpace($requiredAbi) -or [string]::IsNullOrWhiteSpace($assetTemplate) -or [string]::IsNullOrWhiteSpace($manifestAssetName) -or [string]::IsNullOrWhiteSpace($hostContractName)) {
    throw 'CAD kernel integration policy is incomplete.'
}

$headers = @{
    Accept = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent' = 'SpatialViewer-release-kernel-resolver'
}
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
    $headers.Authorization = "Bearer $($env:GITHUB_TOKEN)"
}

$releaseUrl = "https://api.github.com/repos/$repository/releases/latest"
$release = Invoke-RestMethod -Uri $releaseUrl -Headers $headers -Method Get
if (-not $release -or $release.draft -or $release.prerelease) {
    throw 'Latest CAD kernel release is not a stable published release.'
}
$tag = [string]$release.tag_name
if ($tag -notmatch '^v(\d+\.\d+\.\d+)$') {
    throw "Unexpected CAD kernel release tag: $tag"
}
$version = $Matches[1]
$assetName = $assetTemplate.Replace('{version}', $version)
$asset = @($release.assets | Where-Object { $_.name -ceq $assetName }) | Select-Object -First 1
if (-not $asset) { throw "CAD kernel release asset is missing: $assetName" }
$manifestAsset = @($release.assets | Where-Object { $_.name -ceq $manifestAssetName }) | Select-Object -First 1
if (-not $manifestAsset) { throw "CAD kernel manifest asset is missing: $manifestAssetName" }

$target = [IO.Path]::GetFullPath($OutputDirectory)
Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $target | Out-Null
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("SpatialViewer-LatestCadCore-" + [Guid]::NewGuid().ToString('N'))
$archive = Join-Path $tempRoot $assetName
$manifestDownload = Join-Path $tempRoot $manifestAssetName
$extracted = Join-Path $tempRoot 'extracted'

function Assert-GitHubDigest([object]$ReleaseAsset, [string]$Path) {
    $actualHash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    $publishedDigest = [string]$ReleaseAsset.digest
    if ([string]::IsNullOrWhiteSpace($publishedDigest) -or $publishedDigest -notmatch '^sha256:([0-9a-fA-F]{64})$') {
        throw "Release asset does not expose a valid SHA-256 digest: $($ReleaseAsset.name)"
    }
    if ($actualHash -cne $Matches[1].ToLowerInvariant()) {
        throw "Release digest mismatch for $($ReleaseAsset.name): downloaded=$actualHash published=$($Matches[1].ToLowerInvariant())"
    }
    return $actualHash
}

function Assert-HostContract([object]$Manifest) {
    if ([int]$Manifest.schemaVersion -ne $requiredManifestSchema) { throw "CAD kernel manifest schema mismatch: $($Manifest.schemaVersion) != $requiredManifestSchema" }
    if (-not $Manifest.hostContract) { throw 'CAD kernel hostContract is missing.' }
    if ([string]$Manifest.hostContract.name -cne $hostContractName) { throw "CAD kernel host contract name mismatch: $($Manifest.hostContract.name) != $hostContractName" }
    $minVersion = [version][string]$Manifest.hostContract.minVersion
    $maxVersion = [version][string]$Manifest.hostContract.maxVersionExclusive
    if ($minVersion -ge $maxVersion) { throw "CAD kernel host contract range is invalid: $minVersion..<${maxVersion}" }
    if ($hostContractVersion -lt $minVersion -or $hostContractVersion -ge $maxVersion) {
        throw "CAD kernel host contract is not supported by this app host: host=$hostContractVersion package=$minVersion..<${maxVersion}"
    }
}

try {
    New-Item -ItemType Directory -Force -Path $tempRoot, $extracted | Out-Null
    Invoke-WebRequest -Uri ([string]$manifestAsset.browser_download_url) -Headers $headers -OutFile $manifestDownload
    $manifestHash = Assert-GitHubDigest $manifestAsset $manifestDownload
    $preflightManifest = Get-Content -LiteralPath $manifestDownload -Raw | ConvertFrom-Json
    if ([string]$preflightManifest.product -cne [string]$kernel.product) { throw "Unexpected CAD kernel product: $($preflightManifest.product)" }
    if ([string]$preflightManifest.version -cne $version) { throw "CAD kernel manifest version mismatch: $($preflightManifest.version) != $version" }
    if ([string]$preflightManifest.tag -cne $tag) { throw "CAD kernel manifest tag mismatch: $($preflightManifest.tag) != $tag" }
    if ([string]$preflightManifest.abiVersion -cne $requiredAbi) { throw "CAD kernel ABI is not integrated by this app release: $($preflightManifest.abiVersion) != $requiredAbi" }
    if ([string]$preflightManifest.runtime -cne 'x64') { throw "Unexpected CAD kernel runtime: $($preflightManifest.runtime)" }
    if ([string]$preflightManifest.sourceRepository -cne $repository) { throw "Unexpected CAD kernel source repository: $($preflightManifest.sourceRepository)" }
    Assert-HostContract $preflightManifest

    Invoke-WebRequest -Uri ([string]$asset.browser_download_url) -Headers $headers -OutFile $archive
    $archiveHash = Assert-GitHubDigest $asset $archive
    Expand-Archive -LiteralPath $archive -DestinationPath $extracted -Force
    $manifestPath = @(Get-ChildItem -LiteralPath $extracted -Recurse -Filter 'cadcore-release.json' -File) | Select-Object -First 1
    if (-not $manifestPath) { throw 'cadcore-release.json is missing from the CAD kernel release package.' }
    $manifest = Get-Content -LiteralPath $manifestPath.FullName -Raw | ConvertFrom-Json
    if ([string]$manifest.product -cne [string]$preflightManifest.product -or [string]$manifest.version -cne [string]$preflightManifest.version -or [string]$manifest.abiVersion -cne [string]$preflightManifest.abiVersion -or [string]$manifest.commit -cne [string]$preflightManifest.commit) {
        throw 'The manifest inside the CAD kernel archive does not match the standalone preflight manifest.'
    }
    Assert-HostContract $manifest

    $requiredAssemblies = @(
        'SpatialViewer.Core.dll',
        'SpatialViewer.Rendering.dll',
        'SpatialViewer.Rendering.Windows.dll',
        'SpatialViewer.Formats.Cad.dll',
        'SpatialViewer.Formats.Cad.ACadSharp.dll'
    )
    foreach ($name in $requiredAssemblies) {
        $project = [IO.Path]::GetFileNameWithoutExtension($name)
        $projectOutputRoot = Join-Path $extracted "bin\$project"
        if (-not (Test-Path -LiteralPath $projectOutputRoot -PathType Container)) { throw "CAD kernel project output directory is missing: bin/$project" }
        $matches = @(Get-ChildItem -LiteralPath $projectOutputRoot -Recurse -Filter $name -File)
        if ($matches.Count -ne 1) { throw "Expected exactly one canonical $name under bin/$project, found $($matches.Count)." }
        $canonical = $matches[0]
        $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($canonical.FullName).Version.ToString()
        if ($assemblyVersion -cne $requiredAbi) { throw "CAD kernel ABI mismatch for ${name}: $assemblyVersion != $requiredAbi" }
        $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($canonical.FullName).FileVersion
        if ([string]::IsNullOrWhiteSpace($fileVersion) -or -not $fileVersion.StartsWith("$version.")) { throw "CAD kernel product version mismatch for ${name}: $fileVersion does not match $version" }
        Copy-Item -LiteralPath $canonical.FullName -Destination (Join-Path $target $name) -Force
    }

    $resolution = [ordered]@{
        id = 'cad'
        product = [string]$manifest.product
        repository = $repository
        version = $version
        tag = $tag
        abiVersion = [string]$manifest.abiVersion
        manifestSchemaVersion = [int]$manifest.schemaVersion
        hostContractName = [string]$manifest.hostContract.name
        hostContractMinVersion = [string]$manifest.hostContract.minVersion
        hostContractMaxVersionExclusive = [string]$manifest.hostContract.maxVersionExclusive
        hostVersion = $hostContractVersion.ToString(3)
        commit = [string]$manifest.commit
        releaseUrl = [string]$release.html_url
        asset = $assetName
        sha256 = $archiveHash
        manifestAsset = $manifestAssetName
        manifestSha256 = $manifestHash
    }
    $resolutionPath = Join-Path $target 'cadcore-resolution.json'
    $resolution | ConvertTo-Json | Set-Content -LiteralPath $resolutionPath -Encoding utf8
    Write-Host "Resolved integrated CAD kernel: $tag, ABI $requiredAbi, host $hostContractName $hostContractVersion."
    [PSCustomObject]$resolution
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
