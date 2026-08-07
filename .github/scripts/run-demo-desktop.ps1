param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Windows', 'Linux')]
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

if ($Platform -eq 'Linux') {
    & chmod +x $ExecutablePath
    $runner = 'timeout'
    $arguments = @(
        '--signal=TERM',
        '--kill-after=30s',
        '9m',
        'xvfb-run',
        '--auto-servernum',
        $ExecutablePath,
        '-demo',
        '-screen-fullscreen', '0',
        '-screen-width', '1280',
        '-screen-height', '720',
        '-logFile', '-'
    )
} else {
    $runner = $ExecutablePath
    $arguments = @('-demo', '-logFile', '-')
}

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

    if ($Platform -eq 'Linux' -and $result.ExitCode -eq 124) {
        throw 'Linux demo exceeded its nine-minute limit.'
    }
} finally {
    Disconnect-Device
}
