param([ValidateSet('Stage','After')][string]$Phase='Stage', [string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'tools/ProbeContinuation.ps1')
. (Join-Path $PSScriptRoot 'tools/UpdateTrialVerification.ps1')
$root=Join-Path $PSScriptRoot 'update-trial'
$proof=Get-Content -LiteralPath (Join-Path $root 'proof.json') -Raw|ConvertFrom-Json
$variant=if($Phase -eq 'Stage'){'A'}else{'B'}
$expected=Join-Path $root $variant
& (Join-Path $PSScriptRoot 'Check-Run.ps1') -Mode Active -RequirePair -UpdateStage:($Phase -eq 'Stage') -ExpectedArtifactRoot $expected -GameDir $GameDir
if((Get-ContinuationHash (Join-Path $GameDir 'BepInEx/plugins/APIShared_Serp/APIShared.dll')) -cne $proof.APISharedSHA256 -or (Get-ContinuationHash (Join-Path $GameDir 'BepInEx/plugins/000shcdese/data/mod-updater.ps1')) -cne $proof.UpdaterSHA256){throw 'Unrelated APIShared/updater identity changed.'}
$request=Join-Path $GameDir 'BepInEx/config/DependencyBootstrapProbe.local-update.json'
Assert-ContinuationUnlinked $request
if((Get-ContinuationHash $request) -cne (Get-ContinuationHash (Join-Path $root 'request.json'))){throw 'Trial request changed.'}
$map=Join-Path $root 'remote/DependencyBootstrapProbeLocalTrial.map';Assert-ContinuationUnlinked $map
if((Get-ContinuationHash $map) -cne $proof.MapSHA256){throw 'Test map changed.'}
$report=Get-Content -LiteralPath (Join-Path $GameDir 'BepInEx/DependencyBootstrapProbe.lifecycle.json') -Raw|ConvertFrom-Json
$remote=[IO.Path]::GetFullPath((Join-Path $root 'remote'))
if(@($report.SyntheticPaths).Count -ne 1 -or $report.SyntheticPaths[0] -cne $remote -or $report.WorkshopPaths -contains $remote -or
    (@($report.EffectivePaths)-join '|') -cne ((@($report.WorkshopPaths)+@($remote))-join '|')){throw 'Real and synthetic path evidence differ.'}
if(@($report.DeleteManifest).Count){throw 'Deletion entries present; do not approve update.'}
$own=@($report.Owners|Where-Object Guid -ceq 'DependencyBootstrapProbe_Serp')
if($own.Count -ne 1 -or @($own[0].Claims).Count){throw 'Synthetic path was treated as ownership.'}
foreach($row in $proof.Payload){
    $installed=Join-Path $GameDir $row.Path;Assert-ContinuationUnlinked $installed
    if((Get-ContinuationHash $installed) -cne (Get-ContinuationHash (Join-Path $expected $row.Path))){throw "Installed $variant payload mismatch: $($row.Path)"}
}
$stage=Join-Path $GameDir '_SE/.staging'
$actual=@(Get-TrialStagedFiles $stage)
$receiptRelative='_SE/DependencyBootstrapProbeLocalTrial.json'
if($Phase -eq 'Stage'){
    Assert-TrialStaging $stage $proof.Payload $report.StagedFiles
    $receipt=Join-Path $stage $receiptRelative
    if((Get-ContinuationHash (Join-Path $GameDir $receiptRelative)) -cne $proof.BaselineReceiptSHA256){throw 'Synthetic A baseline changed.'}
}else{
    if($actual.Count -or @($report.StagedFiles).Count){throw 'Update still pending after B restart.'}
    $receipt=Join-Path $GameDir $receiptRelative
}
Assert-ContinuationUnlinked $receipt
$metadata=Get-Content -LiteralPath $receipt -Raw|ConvertFrom-Json
$expectedMetadata=Get-Content -LiteralPath (Join-Path $root 'B/BepInEx/plugins/DependencyBootstrapProbe_Serp/info.json') -Raw|ConvertFrom-Json
foreach($p in $expectedMetadata.PSObject.Properties){if([string]$metadata.($p.Name) -cne [string]$p.Value){throw "Managed receipt differs: $($p.Name)"}}
Write-Host "PASS: $Phase exact local update trial; APIShared/updater unchanged; no foreign staging or deletion."
