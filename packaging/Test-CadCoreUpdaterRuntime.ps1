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

function New-DownloadClient([bool]$UseProxy) {
    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $true
    $handler.AutomaticDecompression = [System.Net.DecompressionMethods]::All
    $handler.UseProxy = $UseProxy
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromMinutes(5)
    $client.DefaultRequestHeaders.UserAgent.ParseAdd('SpatialViewer/0.2.3')
    $client.DefaultRequestHeaders.Accept.ParseAdd('application/octet-stream')
    return $client
}

$headers = @{
    'User-Agent' = 'SpatialViewer-Updater-Smoke/0.2.3'
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
New-Item -ItemType Directory -Force -Path $expanded, $versionsRoot | Out-Null

$proxyClient = $null
$directClient = $null
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repository/releases/latest" -Headers $headers
    if (-not $release -or [string]::IsNullOrWhiteSpace([string]$release.tag_name)) {
        throw 'Latest CadCore Release metadata is unavailable.'
    }

    $availableText = ([string]$release.tag_name).TrimStart('v', 'V')
    $available = Normalize-Version ([version]::Parse($availableText))
    $minimum = Normalize-Version ([version]::Parse($MinimumVersion))
    if ($available -lt $minimum) { throw "Latest CadCore version $available is older than required $minimum." }

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

    $appDll = Get-ChildItem src/SpatialViewer.App/bin -Recurse -Filter SpatialViewer.App.dll |
        Where-Object { $_.FullName -match '\\Release\\' } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    if (-not $appDll) { throw 'Release SpatialViewer.App.dll was not found for fresh-process activation.' }

    dotnet run --project packaging/CadCoreActivationProbe/CadCoreActivationProbe.csproj -c Release -- $appDll.FullName $kernelRoot $availableText
    if ($LASTEXITCODE -ne 0) { throw "CadCore fresh-process activation probe failed: $LASTEXITCODE" }

    if (Test-Path -LiteralPath $pendingPath) { throw 'pending.json still exists after fresh-process activation.' }
    $activePath = Join-Path $kernelRoot 'active.json'
    if (-not (Test-Path -LiteralPath $activePath -PathType Leaf)) { throw 'active.json was not created after fresh-process activation.' }
    $active = Get-Content -LiteralPath $activePath -Raw | ConvertFrom-Json
    if ([string]$active.Version -cne $availableText) { throw "Active-state version mismatch: $($active.Version)" }

    Write-Host "CadCore fresh-process activation contract PASS: $availableText is active"
}
finally {
    if ($proxyClient) { $proxyClient.Dispose() }
    if ($directClient) { $directClient.Dispose() }
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}
