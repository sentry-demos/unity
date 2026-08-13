param(
    [Parameter(Mandatory = $true)]
    [string]$ApkPath,

    [Parameter(Mandatory = $true)]
    [string]$Dsn
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ApkPath)) {
    throw "APK not found: $ApkPath"
}

Import-Module "$PSScriptRoot/../../app-runner/app-runner/SentryAppRunner.psm1"
Import-Module "$PSScriptRoot/lib/DemoRun.psm1" -Force

Connect-Device -Platform Adb | Out-Null
$session = Get-DeviceSession
try {
    Install-DeviceApp -Path $ApkPath
    $activity = (adb shell cmd package resolve-activity --brief io.sentry | Where-Object { $_ -match '^io\.sentry/' } | Select-Object -Last 1).Trim()
    if ([string]::IsNullOrWhiteSpace($activity)) {
        throw 'Unable to resolve the installed Android activity.'
    }

    $result = Invoke-DeviceApp -ExecutablePath $activity -Arguments @('-e', 'unity', '-demo', '-e', 'dsn', $Dsn)
    $result.Output | Tee-Object -FilePath android-player.log

    # The activity is hosted by the OS, so its exit code says nothing about the crash.
    Assert-DemoRun -Result $result -Platform 'Android'

    # Relaunch so the SDK picks up the crash from the previous run and sends it.
    # Without demo mode this time - the app should stay up rather than crash again.
    $session.Provider.Timeouts['run-timeout'] = 10
    $relaunch = Invoke-DeviceApp -ExecutablePath $activity -Arguments @('-e', 'dsn', $Dsn)
    $relaunch.Output | Tee-Object -FilePath android-player.log -Append
    adb shell am force-stop io.sentry

    Assert-CrashReported -Result $relaunch -Platform 'Android'
}
finally {
    Disconnect-Device
}

# Failures leave through the throws above, never through the exit code. Without this
# the script would inherit whatever the last command happened to return.
exit 0
