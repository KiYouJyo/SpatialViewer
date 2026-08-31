[CmdletBinding()]
param(
    [string]$Repository = 'KiYouJyo/SpatialViewer.CadCore',
    [string]$MinimumVersion = '0.2.1',
    [string]$Compatibility = 'SpatialViewer 0.2.x',
    [string]$BaselineCommit = '417a581b01360d2a5fa9aaf81e80bcd6996179d8',
    [string]$BaselineVersion = '0.2.0'
)

$ErrorActionPreference = 'Stop'

function Normalize-Version([version]$Version) {
    return [version]::new($Version.Major, $Version.Minor, [Math]::Max(0, $Version.Build))
}

function New-DownloadClient([bool]$UseProxy) {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $true
    $handler.AutomaticDecompression = [System.Net.DecompressionMethods]::All
    $handler.UseProxy = $UseProxy
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromMinutes(5)
    $client.DefaultRequestHeaders.UserAgent.ParseAdd('SpatialViewer/0.2.4')
    $client.DefaultRequestHeaders.Accept.ParseAdd('application/octet-stream')
    return $client
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
    'User-Agent' = 'SpatialViewer-Updater-Smoke/0.2.4'
    'Accept' = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
}
if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN)) {
    $headers['Authorization'] = "Bearer $env:GITHUB_TOKEN"
}

$temp = Join-Path $env:RUNNER_TEMP "cadcore-runtime-smoke-$([Guid]::NewGuid().ToString('N'))"
$archive = Join-Path $temp 'CadCore.zip'
$expanded = Join-Path $temp 'expanded'
$kernelRoot = Join-Path $temp 'LocalState/Kernels/CadCore'
$versionsRoot = Join-Path $kernelRoot 'versions'
$cadCoreRepo = (Resolve-Path 'external/SpatialViewer.CadCore').Path
New-Item -ItemType Directory -Force -Path $expanded, $versionsRoot | Out-Null

$proxyClient = $null
$directClient = $null
$oldRootOverride = $env:SPATIALVIEWER_CADCORE_ROOT
$originalCadCoreSha = (git -C $cadCoreRepo rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($originalCadCoreSha)) { throw 'Unable to determine the checked-out Cad Core gitlink.' }
$baselineCheckedOut = $false
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers $headers
    if (-not $release -or [string]::IsNullOrWhiteSpace([string]$release.tag_name)) {
        throw 'Latest CadCore Release metadata is unavailable.'
    }

    $availableText = ([string]$release.tag_name).TrimStart('v', 'V')
    $available = Normalize-Version ([version]::Parse($availableText))
    $minimum = Normalize-Version ([version]::Parse($MinimumVersion))
    $baseline = Normalize-Version ([version]::Parse($BaselineVersion))
    if ($available -lt $minimum) { throw "Latest CadCore version $available is older than required $minimum." }
    if ($baseline -ge $available) { throw "Pinned activation baseline must be older than latest: baseline=$baseline latest=$available" }

    $assetName = "CadCore-v$availableText-x64.zip"
    $asset = @($release.assets) | Where-Object { $_.name -eq $assetName } | Select-Object -First 1
    if (-not $asset) { throw "Required release asset is missing: $assetName" }
    if ([string]::IsNullOrWhiteSpace([string]$asset.digest) -or -not ([string]$asset.digest).StartsWith('sha256:', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$assetName does not expose a GitHub SHA-256 digest."
    }

    $expectedHash = ([string]$asset.digest).Substring(7).ToLowerInvariant()
    $expectedSize = [long]$asset.size
    $proxyClient = New-DownloadClient $true
    $directClient = New-DownloadClient $false
    $clients = @($proxyClient, $proxyClient, $directClient)
    $downloaded = $false
    $lastFailure = $null

    for ($attempt = 0; $attempt -lt $clients.Count; $attempt++) {
        Remove-Item -LiteralPath $archive -Force -ErrorAction SilentlyContinue
        try {
            $response = $clients[$attempt].GetAsync([string]$asset.browser_download_url, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
            try {
                $response.EnsureSuccessStatusCode()
                $input = $response.Content.ReadAsStreamAsync().GetAwaiter().GetResult()
                try {
                    $output = [IO.File]::Open($archive, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                    try { $input.CopyTo($output) } finally { $output.Dispose() }
                } finally { $input.Dispose() }
            } finally { $response.Dispose() }

            $actualSize = (Get-Item -LiteralPath $archive).Length
            if ($expectedSize -gt 0 -and $actualSize -ne $expectedSize) {
                throw "Downloaded size mismatch: expected=$expectedSize actual=$actualSize"
            }
            $actualHash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($actualHash -cne $expectedHash) {
                throw "Downloaded SHA-256 mismatch: expected=$expectedHash actual=$actualHash"
            }

            $downloaded = $true
            $route = if ($attempt -eq 2) { 'direct fallback' } else { 'system proxy/default route' }
            Write-Host "CadCore download PASS via ${route}: $assetName / sha256:$actualHash"
            break
        }
        catch {
            $lastFailure = $_.Exception.Message
            if ($attempt -lt $clients.Count - 1) { Start-Sleep -Milliseconds (350 * ($attempt + 1)) }
        }
    }
    if (-not $downloaded) { throw "CadCore updater download failed after proxy retries and direct fallback: $lastFailure" }

    Expand-Archive -LiteralPath $archive -DestinationPath $expanded -Force
    $manifestPath = Join-Path $expanded 'cadcore-release.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'cadcore-release.json is missing.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ($manifest.product -cne 'SpatialViewer.CadCore') { throw "Unexpected product: $($manifest.product)" }
    if ($manifest.version -cne $availableText) { throw "Manifest version mismatch: $($manifest.version) != $availableText" }
    if ($manifest.runtime -cne 'x64') { throw "Unsupported CadCore runtime: $($manifest.runtime)" }
    if ($manifest.sourceRepository -cne $Repository) { throw "Unexpected source repository: $($manifest.sourceRepository)" }
    if ($manifest.compatibility -cne $Compatibility) { throw "Unexpected compatibility contract: $($manifest.compatibility)" }

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
        $assemblyVersion = Normalize-Version ([Reflection.AssemblyName]::GetAssemblyName($dll.FullName).Version)
        if ($assemblyVersion -ne $available) { throw "Assembly version mismatch for $($project).dll: $assemblyVersion != $available" }
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

    # The product may already bundle the current latest Cad Core. For a stable
    # regression test, temporarily compile the probe against the known v0.2.0
    # release commit, then restore the product gitlink before packaging.
    git -C $cadCoreRepo fetch origin $BaselineCommit --depth=1
    if ($LASTEXITCODE -ne 0) { throw "Unable to fetch pinned Cad Core baseline $BaselineCommit" }
    git -C $cadCoreRepo checkout --detach $BaselineCommit
    if ($LASTEXITCODE -ne 0) { throw "Unable to checkout pinned Cad Core baseline $BaselineCommit" }
    $baselineCheckedOut = $true
    Remove-CadCoreBuildOutputs $cadCoreRepo

    $propsPath = Join-Path $cadCoreRepo 'Directory.Build.props'
    [xml]$baselineProps = Get-Content -LiteralPath $propsPath
    $checkedOutBaselineText = [string]$baselineProps.Project.PropertyGroup.Version
    $checkedOutBaseline = Normalize-Version ([version]::Parse($checkedOutBaselineText))
    if ($checkedOutBaseline -ne $baseline) { throw "Pinned Cad Core baseline metadata mismatch: expected=$baseline actual=$checkedOutBaseline" }
    Write-Host "CadCore activation regression baseline: $baseline @ $BaselineCommit; online target=$available"

    # Environment is inherited by the fresh process. ModuleInitializer executes
    # before Main and must preload the online target into the default ALC before
    # the probe's compile-time v0.2.0 Cad Core references are bound.
    $env:SPATIALVIEWER_CADCORE_ROOT = $kernelRoot
    dotnet run --project packaging/CadCoreActivationProbe/CadCoreActivationProbe.csproj -c Release -p:Platform=x64 -- $availableText
    if ($LASTEXITCODE -ne 0) { throw "CadCore early-binding fresh-process activation probe failed: $LASTEXITCODE" }

    if (Test-Path -LiteralPath $pendingPath) { throw 'pending.json still exists after fresh-process activation.' }
    $activePath = Join-Path $kernelRoot 'active.json'
    if (-not (Test-Path -LiteralPath $activePath -PathType Leaf)) { throw 'active.json was not created after fresh-process activation.' }
    $active = Get-Content -LiteralPath $activePath -Raw | ConvertFrom-Json
    if ([string]$active.Version -cne $availableText) { throw "Active-state version mismatch: $($active.Version)" }

    Write-Host "CadCore early static-binding activation contract PASS: online $available is active over compiled baseline $baseline"
}
finally {
    $env:SPATIALVIEWER_CADCORE_ROOT = $oldRootOverride
    if ($baselineCheckedOut) {
        git -C $cadCoreRepo checkout --detach $originalCadCoreSha | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Warning "Failed to restore Cad Core gitlink to $originalCadCoreSha" }
        Remove-CadCoreBuildOutputs $cadCoreRepo
    }
    if ($proxyClient) { $proxyClient.Dispose() }
    if ($directClient) { $directClient.Dispose() }
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
