param(
    [Parameter(Mandatory = $true)]
    [string]$AppPath,

    [Parameter(Mandatory = $true)]
    [string]$Dsn,

    [string]$Target = 'latest'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $AppPath)) {
    throw "App bundle not found: $AppPath"
}

Import-Module "$PSScriptRoot/../../app-runner/app-runner/SentryAppRunner.psm1"
Import-Module "$PSScriptRoot/lib/DemoRun.psm1" -Force

$bundleId = (& /usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$AppPath/Info.plist").Trim()
if ([string]::IsNullOrWhiteSpace($bundleId)) {
    throw "Unable to read the bundle identifier from $AppPath/Info.plist."
}

# Unity's iOS player doesn't surface simctl launch arguments through
# Environment.GetCommandLineArgs, so the settings go through the environment instead.
# simctl forwards SIMCTL_CHILD_-prefixed variables to the launched app.
$env:SIMCTL_CHILD_SENTRY_DSN = $Dsn
$env:SIMCTL_CHILD_SENTRY_DEMO = '1'

Connect-Device -Platform iOSSimulator -Target $Target | Out-Null
$session = Get-DeviceSession
try {
    Install-DeviceApp -Path $AppPath

    $result = Invoke-DeviceApp -ExecutablePath $bundleId
    $result.Output | Tee-Object -FilePath ios-player.log

    # The app is hosted by the simulator, so its exit code says nothing about the crash.
    Assert-DemoRun -Result $result -Platform 'iOS'

    # Relaunch so the SDK picks up the crash from the previous run and sends it.
    # Without demo mode this time - the app should stay up rather than crash again.
    # The app never exits on its own here, so this timeout is how long it gets to run.
    # It has to cover simulator start-up (~30s) plus the crash upload, and console-pty
    # only flushes the output once the process ends - too short and we capture nothing.
    $env:SIMCTL_CHILD_SENTRY_DEMO = $null
    $session.Provider.Timeouts['run-timeout'] = 90
    $relaunch = Invoke-DeviceApp -ExecutablePath $bundleId
    $relaunch.Output | Tee-Object -FilePath ios-player.log -Append
    & xcrun simctl terminate $session.Identifier $bundleId 2>&1 | Out-Null

    Assert-CrashReported -Result $relaunch -Platform 'iOS'
}
finally {
    Disconnect-Device
}

# Failures leave through the throws above, never through the exit code. Without this
# the script would inherit whatever the last command happened to return.
exit 0
