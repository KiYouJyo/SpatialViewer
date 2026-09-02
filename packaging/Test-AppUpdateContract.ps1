param(
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

$ErrorActionPreference = 'Stop'

function Read-Text([string]$relativePath) {
    $path = Join-Path $ProjectRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing update-contract file: $relativePath" }
    return Get-Content -LiteralPath $path -Raw -Encoding UTF8
}

function Assert-Contains([string]$text, [string]$fragment, [string]$message) {
    if (-not $text.Contains($fragment, [StringComparison]::Ordinal)) { throw $message }
}

$release = Read-Text 'release/release.json' | ConvertFrom-Json
$version = [string]$release.product.version
$packageVersion = [string]$release.product.packageVersion
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid product version in release metadata: $version" }
if ($packageVersion -ne "$version.0") { throw "Package version must be product version plus .0: $packageVersion" }

[xml]$project = Read-Text 'src/SpatialViewer.App/SpatialViewer.App.csproj'
[xml]$manifest = Read-Text 'src/SpatialViewer.App/Package.appxmanifest'
$projectVersion = @($project.Project.PropertyGroup | ForEach-Object Version | Where-Object { $_ })[0]
$assemblyVersion = @($project.Project.PropertyGroup | ForEach-Object AssemblyVersion | Where-Object { $_ })[0]
$fileVersion = @($project.Project.PropertyGroup | ForEach-Object FileVersion | Where-Object { $_ })[0]
$informationalVersion = @($project.Project.PropertyGroup | ForEach-Object InformationalVersion | Where-Object { $_ })[0]
if ($projectVersion -ne $version) { throw "Project Version mismatch: $projectVersion != $version" }
if ($assemblyVersion -ne $packageVersion -or $fileVersion -ne $packageVersion) { throw 'Assembly/File version does not match package version.' }
if ($informationalVersion -ne $version) { throw 'InformationalVersion does not match release version.' }
if ($manifest.Package.Identity.Version -ne $packageVersion) { throw 'MSIX manifest version does not match release metadata.' }
if ($manifest.Package.Identity.Publisher -ne 'CN=AppPublisher') { throw 'MSIX publisher changed from CN=AppPublisher.' }

$provider = Read-Text 'src/SpatialViewer.App/AppVersionProvider.cs'
Assert-Contains $provider "public const string Version = `"$version`";" 'AppVersionProvider.Version does not match release metadata.'
Assert-Contains $provider "public const string DisplayVersion = `"v$version`";" 'AppVersionProvider.DisplayVersion does not match release metadata.'
Assert-Contains $provider 'Package.Current.Id.Version' 'AppVersionProvider must prefer the installed package version.'

$aboutXaml = Read-Text 'src/SpatialViewer.App/Views/AboutView.xaml'
foreach ($name in @('DisplayVersionText', 'PackageVersionText', 'CurrentAppVersionText', 'AppUpdateProgressBar')) {
    Assert-Contains $aboutXaml "x:Name=`"$name`"" "About update UI is missing $name."
}
foreach ($forbidden in @('Text="v0.3"', 'Text="0.3.0"')) {
    if ($aboutXaml.Contains($forbidden, [StringComparison]::Ordinal)) { throw "Hard-coded legacy version returned to AboutView.xaml: $forbidden" }
}

$aboutCode = Read-Text 'src/SpatialViewer.App/Views/AboutView.xaml.cs'
Assert-Contains $aboutCode 'DisplayVersionText.Text = AppVersionProvider.DisplayVersion;' 'About display version is not sourced from AppVersionProvider.'
Assert-Contains $aboutCode 'PackageVersionText.Text = AppVersionProvider.GetPackageVersion();' 'About package version is not sourced from AppVersionProvider.'
Assert-Contains $aboutCode 'await _updates.DownloadProductUpdateAsync();' 'About page no longer invokes the verified download step.'
Assert-Contains $aboutCode 'await _updates.InstallProductUpdateAsync();' 'About page no longer invokes the install step.'

$service = Read-Text 'src/SpatialViewer.App/ProductAppUpdateService.cs'
Assert-Contains $service 'ExpectedSignerSubject = "CN=AppPublisher"' 'Trusted signer subject changed.'
Assert-Contains $service 'ExpectedSignerThumbprint = "BD85AD77A651C86CA01A480C8E9BC64952993F98"' 'Trusted signer thumbprint changed.'
Assert-Contains $service 'SpatialViewer_{release.DisplayVersion}.0_x64.msixbundle' 'Expected GitHub MSIXBundle naming contract changed.'
Assert-Contains $service 'SHA256SUMS.txt' 'SHA256SUMS verification contract is missing.'
Assert-Contains $service 'SHA256.HashDataAsync' 'SHA-256 verification is missing.'
Assert-Contains $service '_signatureVerifier.Verify(bundlePath)' 'MSIX signature verification is missing.'
Assert-Contains $service 'new PackageManager()' 'Windows PackageManager deployment path is missing.'
Assert-Contains $service 'DeploymentOptions.ForceApplicationShutdown' 'Package deployment no longer requests ForceApplicationShutdown.'
Assert-Contains $service 'ApplicationRestartRegistration.Register' 'Application restart registration is missing.'

$verifier = Read-Text 'src/SpatialViewer.App/MsixBundleSignatureVerifier.cs'
Assert-Contains $verifier 'WinVerifyTrust' 'WinTrust verification is missing.'
Assert-Contains $verifier 'AppxSignature.p7x' 'MSIX PKCS#7 signature extraction is missing.'
Assert-Contains $verifier 'SignedCms' 'PKCS#7 signature parsing is missing.'

Write-Host "Application update contract PASS: product=$version package=$packageVersion; package-backed version display, SHA-256, WinTrust signer pinning, and PackageManager deployment are intact."
