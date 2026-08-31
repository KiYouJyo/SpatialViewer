[CmdletBinding()]
param(
    [string]$Repository = 'KiYouJyo/SpatialViewer.CadCore',
    [string]$MinimumVersion = '0.3.1',
    [string]$Compatibility = 'SpatialViewer 0.2.x'
)

$ErrorActionPreference = 'Stop'

function Normalize-Version([version]$Version) {
    return [version]::new($Version.Major, $Version.Minor, [Math]::Max(0, $Version.Build))
}

$headers = @{
    'User-Agent' = 'SpatialViewer-Acceptance/0.2.4'
    'Accept' = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
    $headers['Authorization'] = "Bearer $env:GITHUB_TOKEN"
}

$temp = Join-Path $env:RUNNER_TEMP "cadcore-update-contract-$([Guid]::NewGuid().ToString('N'))"
$archive = Join-Path $temp 'CadCore.zip'
$expanded = Join-Path $temp 'expanded'
New-Item -ItemType Directory -Force -Path $expanded | Out-Null

try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers $headers
    if (-not $release -or [string]::IsNullOrWhiteSpace([string]$release.tag_name)) {
        throw 'Latest CadCore Release metadata is unavailable.'
    }

    $availableText = ([string]$release.tag_name).TrimStart('v', 'V')
    $available = Normalize-Version ([version]::Parse($availableText))
    $minimum = Normalize-Version ([version]::Parse($MinimumVersion))
    if ($available -lt $minimum) {
        throw "Latest CadCore version $available is older than required $minimum."
    }

    $assetName = "CadCore-v$availableText-x64.zip"
    $asset = @($release.assets) | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    if (-not $asset) { throw "Required release asset is missing: $assetName" }
    if ([string]::IsNullOrWhiteSpace([string]$asset.digest) -or -not ([string]$asset.digest).StartsWith('sha256:', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$assetName does not expose a GitHub SHA-256 digest."
    }

    Invoke-WebRequest -Uri ([string]$asset.browser_download_url) -Headers @{ 'User-Agent' = 'SpatialViewer-Acceptance/0.2.4' } -OutFile $archive
    $actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    $expectedHash = ([string]$asset.digest).Substring(7).ToLowerInvariant()
    if ($actualHash -cne $expectedHash) {
        throw "CadCore release digest mismatch: expected=$expectedHash actual=$actualHash"
    }

    Expand-Archive -LiteralPath $archive -DestinationPath $expanded -Force
    $manifestPath = Join-Path $expanded 'cadcore-release.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'cadcore-release.json is missing.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.product -cne 'SpatialViewer.CadCore') { throw "Unexpected product: $($manifest.product)" }
    if ($manifest.version -cne $availableText) { throw "Manifest version mismatch: $($manifest.version) != $availableText" }
    if ($manifest.tag -cne "v$availableText") { throw "Manifest tag mismatch: $($manifest.tag)" }
    if ($manifest.runtime -cne 'x64') { throw "Unsupported CadCore runtime: $($manifest.runtime)" }
    if ($manifest.sourceRepository -cne $Repository) { throw "Unexpected source repository: $($manifest.sourceRepository)" }
    if ($manifest.compatibility -cne $Compatibility) { throw "Unexpected compatibility contract: $($manifest.compatibility)" }
    if ([string]::IsNullOrWhiteSpace([string]$manifest.abiVersion)) { throw 'Manifest abiVersion is missing.' }
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
        if ($assemblyVersion -ne $abiVersion) {
            throw "ABI mismatch for $($project).dll: $assemblyVersion != $abiVersion"
        }
        $fileVersionText = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll.FullName).FileVersion
        if ([string]::IsNullOrWhiteSpace($fileVersionText)) { throw "FileVersion is missing for $($project).dll" }
        $fileVersion = Normalize-Version ([version]::Parse($fileVersionText))
        if ($fileVersion -ne $available) {
            throw "Product version mismatch for $($project).dll: $fileVersion != $available"
        }
    }

    Write-Host "CadCore updater release contract PASS: product=v$availableText ABI=$abiVersion / $assetName / sha256:$actualHash"
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
