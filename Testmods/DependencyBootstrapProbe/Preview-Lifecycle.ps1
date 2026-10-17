param([string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Check-Run.ps1') -Mode Active -RequireLifecycle -GameDir $GameDir
$report=Get-Content -LiteralPath (Join-Path $GameDir 'BepInEx/DependencyBootstrapProbe.lifecycle.json') -Raw|ConvertFrom-Json
. (Join-Path $PSScriptRoot 'package/Pair.ps1')
$own=@($report.Owners|Where-Object Guid -ceq 'DependencyBootstrapProbe_Serp')
if($own.Count -ne 1){throw 'Selected probe not uniquely recorded.'}
$pair=Get-ProbePairDecision -Root $GameDir -SelectedGuid $own[0].Guid -SelectedVersion $own[0].Version -SelectedPath $own[0].Path -SelectedHash $own[0].SHA256.ToLowerInvariant() -LoadedBootstrapHash $report.PatcherSHA256.ToLowerInvariant()
# Observation supplies no ownership receipt or authoritative unsubscribe event.
# Never import or invoke the removal driver, nor create an executable removal job.
[pscustomobject]@{RunId=$report.RunId;OwnerState=$own[0].State;OwnerReason=$own[0].Reason;PairPreview=$pair;RemovalAuthorized=$false;Reason='Read-only preview. No ownership receipt or unsubscribe proof. The observation probe does not install a pair manifest.'}
