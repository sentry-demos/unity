param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Windows', 'Linux', 'macOS')]
    [string]$Platform,

    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path $ExecutablePath)) {
    throw "Executable not found: $ExecutablePath"
}

Import-Module "$PSScriptRoot/../../app-runner/app-runner/SentryAppRunner.psm1"

if ($Platform -eq 'macOS') {
    # The artifact upload/download round trip drops the executable bit and the
    # quarantine-free extended attributes the bundle needs to launch.
    & chmod +x $ExecutablePath
    & xattr -cr (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $ExecutablePath)))
}

if ($Platform -eq 'Linux') {
    & chmod +x $ExecutablePath

    $crashHandler = Join-Path (Split-Path $ExecutablePath) 'sentry-crash'
    if (-not (Test-Path $crashHandler)) {
        throw "sentry-crash not found next to executable: $crashHandler"
    }
    & chmod +x $crashHandler
}

$runner = $ExecutablePath
$arguments = @('-demo', '-logFile', '-')

Connect-Device -Platform Local
try {
    $result = Invoke-DeviceApp -ExecutablePath $runner -Arguments $arguments
    $result.Output | Tee-Object -FilePath desktop-player.log
    $logs = $result.Output -join "`n"

    if ($logs -notmatch 'Start Game') {
        throw "$Platform demo did not reach gameplay."
    }

    if ($logs -notmatch 'Attempting save_score_to_disk') {
        throw "$Platform demo did not reach the expected native crash."
    }

    if ($result.ExitCode -eq 0) {
        throw "$Platform demo exited without the expected native crash."
    }

} finally {
    Disconnect-Device
}
