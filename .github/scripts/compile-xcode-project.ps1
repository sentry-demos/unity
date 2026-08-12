param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,

    [string]$Scheme = 'Unity-iPhone'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ProjectPath)) {
    throw "Xcode project directory not found: $ProjectPath"
}

$projectPathFull = (Resolve-Path $ProjectPath).Path
$xcodeProject = Join-Path $projectPathFull "$Scheme.xcodeproj"
if (-not (Test-Path $xcodeProject)) {
    throw "Xcode project not found: $xcodeProject"
}

# Unity emits these as non-executable, but the Xcode build phases shell out to them.
foreach ($script in @('MapFileParser.sh', 'process_symbols.sh', 'process_symbols_il2cpp.sh', 'sentry-cli-Darwin-universal')) {
    $path = Join-Path $projectPathFull $script
    if (Test-Path $path) {
        & chmod +x $path
    }
}

$archivePath = Join-Path $projectPathFull 'archive'

Write-Host "::group::Building $Scheme for the iOS simulator"
try {
    # Simulator builds are unsigned - the demo has no signing identity in CI.
    xcodebuild `
        -project $xcodeProject `
        -scheme $Scheme `
        -configuration Release `
        -destination 'generic/platform=iOS Simulator' `
        -derivedDataPath (Join-Path $archivePath $Scheme) `
        CODE_SIGNING_ALLOWED=NO `
        CODE_SIGNING_REQUIRED=NO `
        CODE_SIGN_IDENTITY= `
        | Write-Host

    if ($LASTEXITCODE -ne 0) {
        throw "xcodebuild failed with exit code $LASTEXITCODE."
    }
}
finally {
    Write-Host "::endgroup::"
}

$productsPath = Join-Path $archivePath "$Scheme/Build/Products/Release-iphonesimulator"
$app = Get-ChildItem -Path $productsPath -Filter '*.app' -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $app) {
    throw "No .app bundle produced in $productsPath."
}

Write-Host "Built $($app.FullName)"
