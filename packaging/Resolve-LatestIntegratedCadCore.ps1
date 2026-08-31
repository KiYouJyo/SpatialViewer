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
$kernel = @($policy.kernels | Where-Object { $_.id -eq 'cad' }) | Select-Object -First 1
if (-not $kernel) { throw 'Integrated CAD kernel policy entry is missing.' }
if ([string]$kernel.integrationStatus -cne 'complete') { throw 'CAD kernel integration is not marked complete; refusing to bundle it.' }
if ([string]$kernel.track -cne 'latest-stable') { throw "Unsupported CAD kernel release track: $($kernel.track)" }
if ([string]$kernel.runtime -cne 'x64') { throw "Unsupported CAD kernel runtime: $($kernel.runtime)" }

$repository = [string]$kernel.repository
$requiredAbi = [string]$kernel.requiredAbiVersion
$requiredCompatibility = [string]$kernel.compatibility
$assetTemplate = [string]$kernel.assetNameTemplate
if ([string]::IsNullOrWhiteSpace($repository) -or [string]::IsNullOrWhiteSpace($requiredAbi) -or [string]::IsNullOrWhiteSpace($assetTemplate)) {
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
if (-not $asset) {
    throw "CAD kernel release asset is missing: $assetName"
}

$target = [IO.Path]::GetFullPath($OutputDirectory)
Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $target | Out-Null
$tempRoot = Join-Path ([IO.Path]::GetTempPath()) ("SpatialViewer-LatestCadCore-" + [Guid]::NewGuid().ToString('N'))
$archive = Join-Path $tempRoot $assetName
$extracted = Join-Path $tempRoot 'extracted'

try {
    New-Item -ItemType Directory -Force -Path $tempRoot, $extracted | Out-Null
    Invoke-WebRequest -Uri ([string]$asset.browser_download_url) -Headers $headers -OutFile $archive

    $archiveHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    $publishedDigest = [string]$asset.digest
    if (-not [string]::IsNullOrWhiteSpace($publishedDigest)) {
        if ($publishedDigest -notmatch '^sha256:([0-9a-fA-F]{64})$') {
            throw "Unexpected CAD kernel release digest format: $publishedDigest"
        }
        if ($archiveHash -cne $Matches[1].ToLowerInvariant()) {
            throw "CAD kernel release digest mismatch: downloaded=$archiveHash published=$($Matches[1].ToLowerInvariant())"
        }
    }

    Expand-Archive -LiteralPath $archive -DestinationPath $extracted -Force
    $manifestPath = @(Get-ChildItem -LiteralPath $extracted -Recurse -Filter 'cadcore-release.json' -File) | Select-Object -First 1
    if (-not $manifestPath) { throw 'cadcore-release.json is missing from the CAD kernel release package.' }
    $manifest = Get-Content -LiteralPath $manifestPath.FullName -Raw | ConvertFrom-Json
    if ([string]$manifest.product -cne [string]$kernel.product) { throw "Unexpected CAD kernel product: $($manifest.product)" }
    if ([string]$manifest.version -cne $version) { throw "CAD kernel manifest version mismatch: $($manifest.version) != $version" }
    if ([string]$manifest.tag -cne $tag) { throw "CAD kernel manifest tag mismatch: $($manifest.tag) != $tag" }
    if ([string]$manifest.abiVersion -cne $requiredAbi) { throw "CAD kernel ABI is not integrated by this app release: $($manifest.abiVersion) != $requiredAbi" }
    if ([string]$manifest.runtime -cne 'x64') { throw "Unexpected CAD kernel runtime: $($manifest.runtime)" }
    if ([string]$manifest.sourceRepository -cne $repository) { throw "Unexpected CAD kernel source repository: $($manifest.sourceRepository)" }
    if ([string]$manifest.compatibility -cne $requiredCompatibility) { throw "CAD kernel compatibility mismatch: $($manifest.compatibility) != $requiredCompatibility" }

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
        if (-not (Test-Path -LiteralPath $projectOutputRoot -PathType Container)) {
            throw "CAD kernel project output directory is missing: bin/$project"
        }
        $matches = @(Get-ChildItem -LiteralPath $projectOutputRoot -Recurse -Filter $name -File)
        if ($matches.Count -ne 1) {
            throw "Expected exactly one canonical $name under bin/$project, found $($matches.Count)."
        }
        $canonical = $matches[0]
        $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($canonical.FullName).Version.ToString()
        if ($assemblyVersion -cne $requiredAbi) {
            throw "CAD kernel ABI mismatch for ${name}: $assemblyVersion != $requiredAbi"
        }
        $fileVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($canonical.FullName).FileVersion
        if ([string]::IsNullOrWhiteSpace($fileVersion) -or -not $fileVersion.StartsWith("$version.")) {
            throw "CAD kernel product version mismatch for ${name}: $fileVersion does not match $version"
        }
        Copy-Item -LiteralPath $canonical.FullName -Destination (Join-Path $target $name) -Force
    }

    $resolution = [ordered]@{
        id = 'cad'
        product = [string]$manifest.product
        repository = $repository
        version = $version
        tag = $tag
        abiVersion = [string]$manifest.abiVersion
        compatibility = [string]$manifest.compatibility
        commit = [string]$manifest.commit
        releaseUrl = [string]$release.html_url
        asset = $assetName
        sha256 = $archiveHash
    }
    $resolutionPath = Join-Path $target 'cadcore-resolution.json'
    $resolution | ConvertTo-Json | Set-Content -LiteralPath $resolutionPath -Encoding utf8
    Write-Host "Resolved integrated CAD kernel: $tag, ABI $requiredAbi, commit $($manifest.commit)."
    [PSCustomObject]$resolution
}
finally {
    Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
}
