# Shared helpers for the run-demo-*.ps1 scripts.

Set-StrictMode -Version Latest

# The markers the demo run is verified against. These are the contract between the
# game and CI: if the log lines below change in Assets/Scripts, these change too.
$script:GameplayMarker = 'Start Game'                            # TitleSceneManager.cs
$script:NativeCrashMarker = 'Attempting save_score_to_disk'      # BattleSceneManager.cs

# Logged immediately after the native call in BattleSceneManager.SaveScoreToDisk.
# The crash is meant to take the process down, so any of these appearing means it
# did not - the call returned, or it threw and was caught.
$script:CrashSurvivedMarkers = @(
    'save_score_to_disk completed without crash'
    'save_score_to_disk threw exception'
    'ForceCrash also failed'
)
# Logged by the .NET SDK transport (Sentry/Http/HttpTransportBase.cs) once an envelope
# reaches Sentry. It logs "Envelope successfully sent." without an event ID and
# "Envelope '<id>' successfully sent." with one - a crash report always carries an ID,
# so the optional quoted ID in the middle is the case that actually matters here.
$script:EnvelopeSentPattern = "Envelope ('[^']*' )?successfully sent"

<#
.SYNOPSIS
Asserts that a demo run reached gameplay and then hit the expected native crash.

.DESCRIPTION
The demo drives itself into a deliberate native crash, so a run that exits cleanly
means the crash never happened and the native handler was never exercised.

.PARAMETER Result
The object returned by Invoke-DeviceApp.

.PARAMETER Platform
Platform name used in the failure messages.

#>
function Assert-DemoRun {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result,

        [Parameter(Mandatory = $true)]
        [string]$Platform
    )

    $logs = $Result.Output -join "`n"

    if ($logs -notmatch [regex]::Escape($script:GameplayMarker)) {
        throw "$Platform demo did not reach gameplay (no '$script:GameplayMarker' in the log)."
    }

    if ($logs -notmatch [regex]::Escape($script:NativeCrashMarker)) {
        throw "$Platform demo did not reach the expected native crash (no '$script:NativeCrashMarker' in the log)."
    }

    # The crash is proven by what is missing, not by the exit code. BattleSceneManager
    # logs one of these right after the native call, so seeing either means the call
    # returned or was caught and the process stayed alive. Exit codes are not usable
    # here: a Windows access violation surfaces through $LASTEXITCODE differently than
    # a POSIX signal, so identical crashes report differently per platform.
    foreach ($marker in $script:CrashSurvivedMarkers) {
        if ($logs -match [regex]::Escape($marker)) {
            throw "$Platform demo survived the native crash - the player logged '$marker'."
        }
    }
}

<#
.SYNOPSIS
Asserts that the relaunched app sent the crash captured by the previous run.

.DESCRIPTION
Catching the crash is only half the job - the SDK sends it on the next launch, so
without this the upload could break silently and the run would still pass.
#>
function Assert-CrashReported {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Result,

        [Parameter(Mandatory = $true)]
        [string]$Platform
    )

    $logs = $Result.Output -join "`n"

    if ($logs -notmatch $script:EnvelopeSentPattern) {
        throw "$Platform relaunch did not send the crash captured by the previous run (no 'Envelope ... successfully sent' in the log)."
    }
}

Export-ModuleMember -Function Assert-DemoRun, Assert-CrashReported
