[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../Test-PerformanceLog.ps1')
$run='1234567890abcdef1234567890abcdef'
$lines=[Collections.Generic.List[string]]::new()
$lines.Add("RESCUE_TEST performance-army-begin schema=1 run=$run population=1000 batches=32 observeArmyMoves=false")
for($batch=0;$batch -lt 32;$batch++){
    $count=if($batch -eq 31){8}else{32}
    $pairs=[Collections.Generic.List[string]]::new()
    for($index=0;$index -lt $count;$index++){$id=$batch*32+$index+1;$pairs.Add($id.ToString()+'/'+($id+1000).ToString())}
    $lines.Add("RESCUE_TEST performance-army-manifest schema=1 run=$run batch=$batch batches=32 count=$count units=$($pairs -join ',')")
}
$lines.Add("RESCUE_TEST performance-army-manifest-end schema=1 run=$run count=1000 batches=32 identity=18446744073709551600")
for($iteration=0;$iteration -lt 4;$iteration++){
    $enabled=if($iteration%2){'True'}else{'False'}
    foreach($workload in @('idle','walking')){
        $lines.Add("RESCUE_TEST performance-window iteration=$iteration enabled=$enabled workload=$workload startTick=10 endTick=3610 advancingTicks=3600 requestedPopulation=1000 identity=18446744073709551600 ambientAi=true armyObservation=passive-runtime-log ownArmyRecoveryCalls=unobserved nativeDurationWindow nativeCompleted=3600 advancingTicks=external")
    }
    $lines.Add("RESCUE_TEST performance-endpoints iteration=$iteration nativeSampleGoalsArrived=32 outsideMeasuredWindow=true")
}
$lines.Add("INGAME_TEST run=$run result=PASS step=performance")
$valid=$lines.ToArray()
$result=Assert-RescuePerformanceLog -Lines $valid -Run $run
if(!$result.Qualified -or $result.ManifestBatches -ne 32 -or $result.CompletedWindows -ne 8){throw 'Complete passive result was rejected'}
$checks=1
function Reject([string[]]$InputLines,[string]$Reason){
    $rejected=$false
    try{$null=Assert-RescuePerformanceLog -Lines $InputLines -Run $run}
    catch{if($_.Exception.Message -notlike ('PERFORMANCE_UNQUALIFIED: *'+$Reason+'*')){throw};$rejected=$true}
    if(!$rejected){throw "Invalid result accepted: $Reason"}
    $script:checks++
}
Reject @($valid|Where-Object {$_ -notmatch ' batch=7 '}) '32 army batches'
Reject @($valid|ForEach-Object {$_.Replace('units=1/1001,','units=2/1002,')}) 'Duplicate army ID'
Reject ($valid+@('RESCUE_TEST unit=1/1001 result=Unsupported')) 'rescue attempt affected'
$other=Assert-RescuePerformanceLog -Lines ($valid+@('RESCUE_TEST unit=1001/9001 result=Relocated')) -Run $run
if(!$other.Qualified){throw 'Unrelated ambient rescue was rejected'};$checks++
Reject ($valid+@('[Error  : APIShared] callback failed')) 'runtime/observer failure'
Reject ($valid+@('UNIT_MOVE_OBSERVER_FAILED')) 'runtime/observer failure'
Reject ($valid+@('RESCUE_TEST unit=broken')) 'Malformed rescue result'
Reject @($valid|Where-Object {$_ -notmatch 'performance-endpoints iteration=2 '}) 'endpoint completion'
Reject @($valid|ForEach-Object {$_.Replace('nativeCompleted=3600','nativeCompleted=3599')}) 'Insufficient actual native work'
Reject @($valid|ForEach-Object {$_.Replace('iteration=3 enabled=True workload=walking','iteration=3 enabled=True workload=idle')}) 'duplicate measurement window'
Reject @($valid|ForEach-Object {$_.Replace('iteration=1 enabled=True','iteration=1 enabled=False')}) 'ABAB activation differs'
Reject @($valid|ForEach-Object {$_.Replace('observeArmyMoves=false','observeArmyMoves=true')}) 'passive 1000-unit profile'
Reject @($valid|Where-Object {$_ -notmatch 'INGAME_TEST '}) 'terminal PASS'
Reject ($valid+@("INGAME_TEST run=$run result=PASS step=duplicate")) 'terminal PASS'
Reject @($valid|ForEach-Object {$_.Replace('armyObservation=passive-runtime-log','armyObservation=origin-observer')}) 'ABAB activation differs'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$temporary=New-ShortWorkDirectory -Root $workspace -Kind t -Name rescue-perflog -Identity 'passive log CLI contract'
try{
    $inputFile=Join-Path $temporary 'input.log';$outputFile=Join-Path $temporary 'result.json'
    [IO.File]::WriteAllLines($inputFile,$valid)
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot '../Test-PerformanceLog.ps1') -LogPath $inputFile -ExpectedRun $run -OutputPath $outputFile
    if($LASTEXITCODE -ne 0){throw 'Fresh-process validator failed'}
    $actual=[IO.File]::ReadAllText($outputFile)|ConvertFrom-Json
    if(!$actual.Qualified -or $actual.Run -cne $run -or $actual.Population -ne 1000){throw 'CLI did not write qualified evidence'}
    $checks++
}finally{Remove-ShortWorkDirectory -Path $temporary}
Write-Output "PASS: passive performance log contract $checks checks; complete manifests, lifetime filtering, callback faults and actual completion boundaries; no game execution."
