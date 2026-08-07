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

$session = Connect-Device -Platform Adb
try {
    Install-DeviceApp -Path $ApkPath
    $activity = (adb shell cmd package resolve-activity --brief io.sentry | Where-Object { $_ -match '^io\.sentry/' } | Select-Object -Last 1).Trim()
    if ([string]::IsNullOrWhiteSpace($activity)) {
        throw 'Unable to resolve the installed Android activity.'
    }

    $result = Invoke-DeviceApp -ExecutablePath $activity -Arguments @('-e', 'unity', '-demo', '-e', 'dsn', $Dsn)
    $result.Output | Tee-Object -FilePath android-player.log
    $logs = $result.Output -join "`n"
    if ($logs -notmatch 'Start Game') {
        throw 'Android demo did not reach gameplay.'
    }

    if ($logs -notmatch 'Attempting save_score_to_disk') {
        throw 'Android demo did not reach the expected native crash.'
    }

    $session.Provider.Timeouts['run-timeout'] = 10
    $relaunch = Invoke-DeviceApp -ExecutablePath $activity -Arguments @('-e', 'dsn', $Dsn)
    $relaunch.Output | Tee-Object -FilePath android-player.log -Append
    adb shell am force-stop io.sentry
}
finally {
    Disconnect-Device
}
