[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$RunDirectory,[string]$ControlDirectory,[string]$ReviewFile)
$ErrorActionPreference='Stop'
function Read-Run([string]$path) {
    $file=Join-Path $path 'events.jsonl'
    $events=@(Get-Content -LiteralPath $file | Where-Object { $_.Trim().Length -gt 0 } | ForEach-Object { $_ | ConvertFrom-Json })
    $sessions=@($events | Where-Object marker -eq 'SESSION')
    $ticks=@($events | Where-Object marker -eq 'POST_STARTUP_TICK')
    $errors=@($events | Where-Object marker -eq 'CALLBACK_ERROR')
    $valid=$sessions.Count -eq 1 -and $ticks.Count -eq 1 -and $errors.Count -eq 0 -and $sessions[0].isolated -and $sessions[0].fixes124
    foreach($group in @($events | Where-Object marker -eq 'SPAWN' | Group-Object -Property gameId,globalId)) {
        if($group.Count -gt 1) { $valid=$false }
    }
    [pscustomobject]@{ Path=$path; Valid=$valid; Events=$events; Session=$sessions; CallbackErrors=$errors.Count }
}
$run=Read-Run $RunDirectory
$control=if($ControlDirectory) { Read-Run $ControlDirectory } else { $null }
$spawns=@($run.Events | Where-Object marker -eq 'SPAWN')
$summary=[ordered]@{
    status='INCONCLUSIVE'; automatedPrerequisitesValid=$run.Valid
    controlPrerequisitesValid=($null -ne $control -and $control.Valid)
    spawns=$spawns.Count; callbackErrors=$run.CallbackErrors
    candidatePopularitySpawns=@($spawns | Where-Object { $_.player.popularity -ge 5000 -and $_.player.popularity -lt 9000 })
    missingEvidence=@('Actual selected AIC/AIV/map/save hashes and matched initialization state',
        'Natural AI/AIV origin; pending and buildable AIV demand in the control',
        'Suitable surviving crew and prior accessibility for siege tests',
        'Concrete gameplay effect and matched control; absence alone is not a pass')
    policy='CONFIRMED only after the concrete game effect and control are independently verified. NOT_REPRODUCED needs complete conditions and sufficient observation. No automatic author report.'
}
# A completed independent review is bound to these exact event files.
if($ReviewFile) {
    $review=Get-Content -LiteralPath $ReviewFile -Raw | ConvertFrom-Json
    $runHash=(Get-FileHash -LiteralPath (Join-Path $RunDirectory 'events.jsonl') -Algorithm SHA256).Hash
    $controlHash=if($ControlDirectory) { (Get-FileHash -LiteralPath (Join-Path $ControlDirectory 'events.jsonl') -Algorithm SHA256).Hash } else { '' }
    $required=@('naturalOrigin','effectivePreferencesVerified','fixtureAndSaveIdentityVerified','matchedInitialState','controlDemandBuildable','sufficientObservation')
    if($PSScriptRoot -like '*SiegeTent*') { $required+=@('suitableSurvivingCrew','noCombatLosses','priorAccessibilityVerified','leaderAndSearchStateVerified') }
    $missing=@($required | Where-Object { $review.conditions.$_ -ne $true })
    $references=@($review.evidenceFiles)
    $validReferences=$references.Count -gt 0
    foreach($reference in $references) {
        if(-not (Test-Path -LiteralPath $reference.path -PathType Leaf)) { $validReferences=$false; continue }
        if((Get-FileHash -LiteralPath $reference.path -Algorithm SHA256).Hash -ne $reference.sha256) { $validReferences=$false }
    }
    $reviewValid=$run.Valid -and $null -ne $control -and $control.Valid -and $review.runEventsSha256 -eq $runHash -and
        $review.controlEventsSha256 -eq $controlHash -and $missing.Count -eq 0 -and $validReferences -and
        -not [string]::IsNullOrWhiteSpace($review.reviewer) -and -not [string]::IsNullOrWhiteSpace($review.explanation)
    if($reviewValid -and $review.outcome -eq 'CONFIRMED' -and $review.conditions.concreteGameplayEffect -eq $true -and $review.conditions.causalControlVerified -eq $true) {
        $summary.status='CONFIRMED'
    } elseif($reviewValid -and $review.outcome -eq 'NOT_REPRODUCED' -and $review.conditions.normalCompletionVerified -eq $true) {
        $summary.status='NOT_REPRODUCED'
    }
    $summary['independentReviewValid']=$reviewValid
    $summary['missingReviewConditions']=$missing
    $summary['reviewer']=$review.reviewer
    $summary['explanation']=$review.explanation
}
$summary | ConvertTo-Json -Depth 15
