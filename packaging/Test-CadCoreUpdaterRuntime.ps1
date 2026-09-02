[CmdletBinding()]
param(
    [string]$Repository = 'KiYouJyo/SpatialViewer.CadCore',
    [string]$MinimumVersion = '0.9.0',
    [string]$RequiredAbiVersion = '1.0.0.0',
    [string]$HostContractName = 'SpatialViewer.CadHost',
    [string]$HostContractVersion = '1.0.0',
    [string]$BaselineCommit = 'd44e0b098fb7fd582b12c2888fdc7fc9266511ee',
    [string]$BaselineVersion = '0.8.0'
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
    if ($min -ge $max) { throw "Invalid host contract range: $min..<$max" }
    if ($host -lt $min -or $host -ge $max) { throw "Host contract is incompatible: host=$host package=$min..<$max" }
}

function New-DownloadClient([bool]$UseProxy) {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $true
    $handler.AutomaticDecompression = [System.Net.DecompressionMethods]::All
    $handler.UseProxy = $UseProxy
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromMinutes(5)
    $client.DefaultRequestHeaders.UserAgent.ParseAdd('SpatialViewer/0.3.1')
    $client.DefaultRequestHeaders.Accept.ParseAdd('application/octet-stream')
    return $client
}

function Download-VerifiedAsset([object]$Asset, [string]$Destination, [System.Net.Http.HttpClient[]]$Clients) {
    if ([string]::IsNullOrWhiteSpace([string]$Asset.digest) -or -not ([string]$Asset.digest).StartsWith('sha256:', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$($Asset.name) does not expose a GitHub SHA-256 digest."
    }
    $expectedHash = ([string]$Asset.digest).Substring(7).ToLowerInvariant()
    $expectedSize = [long]$Asset.size
    $lastFailure = $null
    for ($attempt = 0; $attempt -lt $Clients.Count; $attempt++) {
        Remove-Item -LiteralPath $Destination -Force -ErrorAction SilentlyContinue
        try {
            $response = $Clients[$attempt].GetAsync([string]$Asset.browser_download_url, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
            try {
                $response.EnsureSuccessStatusCode()
                $input = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                try {
                    $output = [IO.File]::Open($Destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                    try { $input.CopyTo($output) } finally { $output.Dispose() }
                } finally { $input.Dispose() }
            } finally { $response.Dispose() }
            $actualSize = (Get-Item -LiteralPath $Destination).Length
            if ($expectedSize -gt 0 -and $actualSize -ne $expectedSize) { throw "Downloaded size mismatch: expected=$expectedSize actual=$actualSize" }
            $actualHash = (Get-FileHash -LiteralPath $Destination -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actualHash -cne $expectedHash) { throw "Downloaded SHA-256 mismatch: expected=$expectedHash actual=$actualHash" }
            return $actualHash
        }
        catch {
            $lastFailure = $_.Exception.Message
            if ($attempt -lt $Clients.Count - 1) { Start-Sleep -Milliseconds (350 * ($attempt + 1)) }
        }
    }
    throw "CadCore updater download failed after proxy retries and direct fallback: $lastFailure"
}

function Remove-CadCoreBuildOutputs([string]$CadCoreRoot) {
    Get-ChildItem -LiteralPath (Join-Path $CadCoreRoot 'src') -Directory | ForEach-Object {
        Remove-Item -LiteralPath (Join-Path $_.FullName 'bin') -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath (Join-Path $_.FullName 'obj') -Recurse -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath 'packaging/CadCoreActivationProbe/bin' -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath 'packaging/CadCoreActivationProbe/obj' -Recurse -Force -ErrorAction SilentlyContinue
}

$headers = @{
    'User-Agent' = 'SpatialViewer-Updater-Smoke/0.3.1'
    'Accept' = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) { $headers['Authorization'] = "Bearer $env:GITHUB_TOKEN" }

$temp = Join-Path $env:RUNNER_TEMP "cadcore-runtime-smoke-$([Guid]::NewGuid().ToString('N'))"
$archive = Join-Path $temp 'CadCore.zip'
$manifestDownload = Join-Path $temp 'cadcore-release.json'
$expanded = Join-Path $temp 'expanded'
$kernelRoot = Join-Path $temp 'LocalState/Kernels/CadCore'
$versionsRoot = Join-Path $kernelRoot 'versions'
$cadCoreRepo = (Resolve-Path 'external/SpatialViewer.CadCore').Path
New-Item -ItemType Directory -Force -Path $expanded, $versionsRoot | Out-Null

$proxyClient = $null
$directClient = $null
$oldRootOverride = $env:SPATIALVIEWER_CADCORE_ROOT
$originalCadCoreSha = (git -C $cadCoreRepo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($originalCadCoreSha)) { throw 'Unable to determine the checked-out CadCore gitlink.' }
$baselineCheckedOut = $false
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers $headers
    if (-not $release -or [string]::IsNullOrWhiteSpace([string]$release.tag_name)) { throw 'Latest CadCore Release metadata is unavailable.' }

    $availableText = ([string]$release.tag_name).TrimStart('v', 'V')
    $available = Normalize-Version ([version]::Parse($availableText))
    $minimum = Normalize-Version ([version]::Parse($MinimumVersion))
    $baseline = Normalize-Version ([version]::Parse($BaselineVersion))
    if ($available -lt $minimum) { throw "Latest CadCore version $available is older than required $minimum." }
    if ($baseline -ge $available) { throw "Pinned activation baseline must be older than latest: baseline=$baseline latest=$available" }

    $assetName = "CadCore-v$availableText-x64.zip"
    $asset = @($release.assets) | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    if (-not $asset) { throw "Required release asset is missing: $assetName" }
    $manifestAsset = @($release.assets) | Where-Object { $_.name -eq 'cadcore-release.json' } | Select-Object -First 1
    if (-not $manifestAsset) { throw 'Standalone cadcore-release.json release asset is missing.' }

    $proxyClient = New-DownloadClient $true
    $directClient = New-DownloadClient $false
    $clients = @($proxyClient, $proxyClient, $directClient)

    $manifestHash = Download-VerifiedAsset $manifestAsset $manifestDownload $clients
    $preflight = Get-Content -LiteralPath $manifestDownload -Raw | ConvertFrom-Json
    if ($preflight.product -cne 'SpatialViewer.CadCore') { throw "Unexpected product: $($preflight.product)" }
    if ($preflight.version -cne $availableText) { throw "Manifest version mismatch: $($preflight.version) != $availableText" }
    if ($preflight.runtime -cne 'x64') { throw "Unsupported CadCore runtime: $($preflight.runtime)" }
    if ($preflight.sourceRepository -cne $Repository) { throw "Unexpected source repository: $($preflight.sourceRepository)" }
    if ([string]$preflight.abiVersion -cne $RequiredAbiVersion) { throw "Unexpected CadCore ABI: $($preflight.abiVersion) != $RequiredAbiVersion" }
    Assert-HostContract $preflight
    Write-Host "CadCore standalone manifest preflight PASS: schema=2 host=$HostContractName $HostContractVersion sha256:$manifestHash"

    $archiveHash = Download-VerifiedAsset $asset $archive $clients
    Write-Host "CadCore archive download PASS: $assetName / sha256:$archiveHash"
    Expand-Archive -LiteralPath $archive -DestinationPath $expanded -Force
    $manifestPath = Join-Path $expanded 'cadcore-release.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'cadcore-release.json is missing.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ([string]$manifest.commit -cne [string]$preflight.commit -or [string]$manifest.version -cne [string]$preflight.version -or [string]$manifest.abiVersion -cne [string]$preflight.abiVersion) {
        throw 'Archive manifest does not match standalone preflight manifest.'
    }
    Assert-HostContract $manifest
    $availableAbi = [version]::Parse([string]$manifest.abiVersion)

    $projects = @(
        'SpatialViewer.Core',
        'SpatialViewer.Formats.Cad',
        'SpatialViewer.Formats.Cad.ACadSharp',
        'SpatialViewer.Rendering',
        'SpatialViewer.Rendering.Windows'
    )
    foreach ($project in $projects) {
        $projectRoot = Join-Path (Join-Path $expanded 'bin') $project
        $dll = Get-ChildItem -LiteralPath $projectRoot -Recurse -Filter "$project.dll" | Select-Object -First 1
        if (-not $dll) { throw "Missing required assembly: $($project).dll" }
        $assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($dll.FullName).Version
        if ($assemblyVersion -ne $availableAbi) { throw "ABI mismatch for $($project).dll: $assemblyVersion != $availableAbi" }
        $fileVersionText = [Diagnostics.FileVersionInfo]::GetVersionInfo($dll.FullName).FileVersion
        if ([string]::IsNullOrWhiteSpace($fileVersionText)) { throw "FileVersion is missing for $($project).dll" }
        $fileVersion = Normalize-Version ([version]::Parse($fileVersionText))
        if ($fileVersion -ne $available) { throw "Product version mismatch for $($project).dll: $fileVersion != $available" }
    }

    $finalDirectory = Join-Path $versionsRoot $availableText
    if (Test-Path -LiteralPath $finalDirectory) { Remove-Item -LiteralPath $finalDirectory -Recurse -Force }
    Move-Item -LiteralPath $expanded -Destination $finalDirectory
    if (-not (Test-Path -LiteralPath (Join-Path $finalDirectory 'cadcore-release.json'))) { throw 'Staged package disappeared after move.' }

    $pendingPath = Join-Path $kernelRoot 'pending.json'
    $pendingTemp = "$pendingPath.$PID.$([Guid]::NewGuid().ToString('N')).tmp"
    @{ Version = $availableText } | ConvertTo-Json -Compress | Set-Content -LiteralPath $pendingTemp -Encoding utf8
    Move-Item -LiteralPath $pendingTemp -Destination $pendingPath -Force
    $pending = Get-Content -LiteralPath $pendingPath -Raw | ConvertFrom-Json
    if ([string]$pending.Version -cne $availableText) { throw "Pending-state version mismatch: $($pending.Version)" }
    Write-Host "CadCore runtime staging PASS: versions/$availableText + pending.json"

    git -C $cadCoreRepo fetch origin $BaselineCommit --depth=1
    if ($LASTEXITCODE -ne 0) { throw "Unable to fetch pinned CadCore baseline $BaselineCommit" }
    git -C $cadCoreRepo checkout --detach $BaselineCommit
    if ($LASTEXITCODE -ne 0) { throw "Unable to checkout pinned CadCore baseline $BaselineCommit" }
    $baselineCheckedOut = $true
    Remove-CadCoreBuildOutputs $cadCoreRepo

    $propsPath = Join-Path $cadCoreRepo 'Directory.Build.props'
    [xml]$baselineProps = Get-Content -LiteralPath $propsPath
    $checkedOutBaselineText = [string]$baselineProps.Project.PropertyGroup.Version
    $checkedOutBaseline = Normalize-Version ([version]::Parse($checkedOutBaselineText))
    if ($checkedOutBaseline -ne $baseline) { throw "Pinned CadCore baseline metadata mismatch: expected=$baseline actual=$checkedOutBaseline" }
    $baselineAbi = [version]::Parse([string]$baselineProps.Project.PropertyGroup.AbiVersion)
    if ($baselineAbi -ne $availableAbi) { throw "Pinned CadCore baseline ABI mismatch: baseline=$baselineAbi online=$availableAbi" }
    Write-Host "CadCore activation regression baseline: product=$baseline ABI=$baselineAbi @ $BaselineCommit; online target=$available"

    $env:SPATIALVIEWER_CADCORE_ROOT = $kernelRoot
    dotnet run --project packaging/CadCoreActivationProbe/CadCoreActivationProbe.csproj -c Release -p:Platform=x64 -- $availableText
    if ($LASTEXITCODE -ne 0) { throw "CadCore early-binding fresh-process activation probe failed: $LASTEXITCODE" }

    if (Test-Path -LiteralPath $pendingPath) { throw 'pending.json still exists after fresh-process activation.' }
    $activePath = Join-Path $kernelRoot 'active.json'
    if (-not (Test-Path -LiteralPath $activePath -PathType Leaf)) { throw 'active.json was not created after fresh-process activation.' }
    $active = Get-Content -LiteralPath $activePath -Raw | ConvertFrom-Json
    if ([string]$active.Version -cne $availableText) { throw "Active-state version mismatch: $($active.Version)" }

    Write-Host "CadCore ABI + host-contract restart activation PASS: bundled=$baseline -> online=$available; ABI=$availableAbi; host=$HostContractName $HostContractVersion"
}
finally {
    $env:SPATIALVIEWER_CADCORE_ROOT = $oldRootOverride
    if ($baselineCheckedOut) {
        git -C $cadCoreRepo checkout --detach $originalCadCoreSha | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Warning "Failed to restore CadCore gitlink to $originalCadCoreSha" }
        Remove-CadCoreBuildOutputs $cadCoreRepo
    }
    if ($proxyClient) { $proxyClient.Dispose() }
    if ($directClient) { $directClient.Dispose() }
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
