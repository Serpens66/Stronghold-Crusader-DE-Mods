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
        $lines.Add("RESCUE_TEST performance-window run=$run iteration=$iteration enabled=$enabled workload=$workload startTick=10 endTick=3610 advancingTicks=3600 requestedPopulation=1000 identity=18446744073709551600 ambientAi=true armyObservation=passive-runtime-log ownArmyRecoveryCalls=unobserved nativeDurationWindow nativeCompleted=3600 advancingTicks=external")
    }
    $lines.Add("RESCUE_TEST performance-endpoints run=$run iteration=$iteration nativeSampleGoalsArrived=32 outsideMeasuredWindow=true")
}
$lines.Add("INGAME_TEST run=$run result=PASS step=performance")
$valid=$lines.ToArray()
# Ordinary BepInEx logs contain blank lines and unrelated startup messages.
$valid=@('startup noise','')+@($valid|ForEach-Object {$_;''})+@('')
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
Reject @($valid|Where-Object {$_ -notmatch 'performance-endpoints .*iteration=2 '}) 'endpoint completion'
Reject @($valid|ForEach-Object {$_.Replace('nativeCompleted=3600','nativeCompleted=3599')}) 'Insufficient actual native work'
Reject @($valid|ForEach-Object {$_.Replace('iteration=3 enabled=True workload=walking','iteration=3 enabled=True workload=idle')}) 'duplicate measurement window'
Reject @($valid|ForEach-Object {$_.Replace('iteration=1 enabled=True','iteration=1 enabled=False')}) 'ABAB activation differs'
Reject @($valid|ForEach-Object {$_.Replace('observeArmyMoves=false','observeArmyMoves=true')}) 'explicitly selected passive population profile'
Reject @($valid|Where-Object {$_ -notmatch 'INGAME_TEST '}) 'terminal PASS'
Reject ($valid+@("INGAME_TEST run=$run result=PASS step=duplicate")) 'terminal PASS'
Reject @($valid|ForEach-Object {$_.Replace('armyObservation=passive-runtime-log','armyObservation=origin-observer')}) 'ABAB activation differs'
# Exact run binding and chronological ABAB evidence are separate from counts.
$foreign='ffffffffffffffffffffffffffffffff'
Reject @($valid|ForEach-Object {if($_ -like '*performance-window *'){$_.Replace('run='+$run,'run='+$foreign)}else{$_}}) 'Window belongs to another run'
Reject @($valid|ForEach-Object {if($_ -like '*performance-endpoints *'){$_.Replace('run='+$run,'run='+$foreign)}else{$_}}) 'Endpoint belongs to another run'
Reject @($valid|ForEach-Object {if($_ -like '*performance-window *'){$_+' run='+$foreign}else{$_}}) 'Expected one run token'
$reversedWindows=@($valid|Where-Object {$_ -like '*performance-window *'})
[array]::Reverse($reversedWindows)
Reject (@($valid|Where-Object {$_ -notlike '*performance-window *'})+$reversedWindows) 'chronology differs'
$reversedBatches=@($valid|Where-Object {$_ -like '*performance-army-manifest *'})
[array]::Reverse($reversedBatches)
Reject (@($valid|Where-Object {$_ -like '*performance-army-begin *'})+$reversedBatches+@($valid|Where-Object {$_ -notlike '*performance-army-begin *' -and $_ -notlike '*performance-army-manifest *'})) 'Army manifest chronology differs'
$earlyEnd=@($valid|Where-Object {$_ -like '*performance-army-manifest-end *'})
Reject ($earlyEnd+@($valid|Where-Object {$_ -notlike '*performance-army-manifest-end *'})) 'Army manifest boundary chronology differs'
$earlyTerminal=@($valid|Where-Object {$_ -like 'INGAME_TEST *'})
Reject ($earlyTerminal+@($valid|Where-Object {$_ -notlike 'INGAME_TEST *'})) 'Terminal PASS precedes'
$earlyEndpoint=@($valid|Where-Object {$_ -like '*performance-endpoints *iteration=0 *'})
Reject (@($valid|Where-Object {$_ -like '*performance-army-*'})+$earlyEndpoint+@($valid|Where-Object {$_ -notlike '*performance-army-*' -and $_ -notlike '*performance-endpoints *iteration=0 *'})) 'Performance evidence chronology differs'
# New higher-tier requests must carry a complete manifest and actual Extreme boundary evidence.
$tierInputs=@{}
foreach($population in @(4000,9871)){
    $scenario='rescue-perf-'+$population+'-idle1000walk-passive-on';$batchCount=[int][Math]::Ceiling($population/32.0)
    $tier=[Collections.Generic.List[string]]::new()
    $tier.Add("RESCUE_TEST performance-extreme-lobby scenario=$scenario extremeTroops=1 beforeLaunch=true")
    $tier.Add("RESCUE_TEST performance-extreme run=$run scenario=$scenario boundary=before-spawn extremeTroops=1 publicReadback=true")
    $after=$population+60
    $tier.Add("RESCUE_TEST performance-population run=$run requested=$population physicalBefore=60 physicalAfter=$after physicalCapacity=9999 ambientReservation=128 freeMarginRequired=64 walkingPopulation=1000 groups=32 actors=$population")
    $tier.Add("RESCUE_TEST performance-army-begin schema=1 run=$run population=$population batches=$batchCount scenario=$scenario walkingPopulation=1000 workloadModel=full-idle-1000-walking observeArmyMoves=false")
    for($batch=0;$batch -lt $batchCount;$batch++){
        $count=[Math]::Min(32,$population-$batch*32);$pairs=[Collections.Generic.List[string]]::new()
        for($index=0;$index -lt $count;$index++){$id=$batch*32+$index+1;$pairs.Add($id.ToString()+'/'+($id+1000).ToString())}
        $tier.Add("RESCUE_TEST performance-army-manifest schema=1 run=$run batch=$batch batches=$batchCount count=$count units=$($pairs -join ',')")
    }
    $tier.Add("RESCUE_TEST performance-army-manifest-end schema=1 run=$run count=$population batches=$batchCount identity=18446744073709551600")
    for($iteration=0;$iteration -lt 4;$iteration++){
        $tier.Add("RESCUE_TEST performance-extreme run=$run scenario=$scenario boundary=load-$iteration extremeTroops=1 publicReadback=true")
        foreach($line in @($valid|Where-Object {$_ -like "*performance-window *iteration=$iteration *"})){
            $tier.Add($line.Replace('requestedPopulation=1000',"requestedPopulation=$population scenario=$scenario walkingPopulation=1000 workloadModel=full-idle-1000-walking"))
        }
        $tier.Add(@($valid|Where-Object {$_ -like "*performance-endpoints *iteration=$iteration *"})[0])
    }
    $tier.Add("INGAME_TEST run=$run result=PASS step=performance")
    $tierInputs[$scenario]=$tier.ToArray()
    $qualified=Assert-RescuePerformanceLog -Lines $tier.ToArray() -Run $run -Scenario $scenario
    if(!$qualified.Qualified -or $qualified.Population -ne $population -or $qualified.ManifestBatches -ne $batchCount){throw 'Higher-tier explicit complete manifest rejected'};$checks++
    $negativeCases=@(
        [pscustomobject]@{Lines=@($tier|Where-Object {$_ -notlike '*boundary=load-2 *'})},
        [pscustomobject]@{Lines=@($tier|ForEach-Object {$_.Replace('walkingPopulation=1000','walkingPopulation=9871')})},
        [pscustomobject]@{Lines=@($tier|Where-Object {$_ -notlike '*batch=100 *'})},
        [pscustomobject]@{Lines=@($tier|ForEach-Object {$_.Replace('physicalBefore=60','physicalBefore=200')})},
        [pscustomobject]@{Lines=@($tier|ForEach-Object {$_.Replace('extremeTroops=1','extremeTroops=0')})},
        [pscustomobject]@{Lines=@($tier|Where-Object {$_ -notlike '*boundary=load-0 *'})+@($tier|Where-Object {$_ -like '*boundary=load-0 *'})}
    )
    foreach($case in $negativeCases){
        $rejected=$false;try{$null=Assert-RescuePerformanceLog -Lines $case.Lines -Run $run -Scenario $scenario}catch{$rejected=$true}
        if(!$rejected){throw 'Incomplete/changed higher tier accepted'};$checks++
    }
    $rejected=$false;try{$null=Assert-RescuePerformanceLog -Lines $tier.ToArray() -Run $run}catch{$rejected=$true}
    if(!$rejected){throw 'Higher-tier log accepted under default1000 request'};$checks++

}
# An otherwise valid passive record must not conceal active damage observers.
Reject @($valid + "RESCUE_TEST performance-health-diagnostic run=$run diagnosticSubscriptions=true performanceAcceptanceClaim=false") 'Active health-event diagnosis'
Reject @($valid + "RESCUE_TEST performance-health-event run=$run reportedArgumentsOnly=true") 'Active health-event diagnosis'
$otherRun=$valid + 'RESCUE_TEST performance-health-diagnostic run=ffffffffffffffffffffffffffffffff diagnosticSubscriptions=true performanceAcceptanceClaim=false'
if(!(Assert-RescuePerformanceLog -Lines $otherRun -Run $run).Qualified){throw 'Unrelated-run diagnostic marker rejected otherwise valid current run'};$checks++
# New marker schema is optional for historical logs, complete and strict when present.
function Add-DiagnosticMarkers([string[]]$inputLines,[int]$population){
$marked=[Collections.Generic.List[string]]::new();[UInt64]$markerStamp=100
foreach($line in $inputLines){
    if($line -like '*performance-window *'){
        $i=[regex]::Match($line,'iteration=(\d)').Groups[1].Value;$w=[regex]::Match($line,'workload=(\w+)').Groups[1].Value
        $mode=if([int]$i%2){'True'}else{'False'};$label='rescue-p'+$population+'-'+$i+'-'+$(if($w -eq 'idle'){'idle'}else{'walk'})
        $marked.Add("RESCUE_TEST performance-window-open schema=1 run=$run iteration=$i workload=$w enabled=$mode label=$label actualTick=10 requestedPopulation=$population monotonicStamp=$markerStamp pausedEngineBoundary=true boundaryThread=1")
        $markerStamp+=10
        $marked.Add("RESCUE_TEST performance-window-close schema=1 run=$run iteration=$i workload=$w enabled=$mode label=$label actualTick=3610 requestedPopulation=$population actualAdvancingTicks=3600 monotonicStamp=$markerStamp pausedEngineBoundary=true boundaryThread=1 boundaryThreadAllocatedBytesDelta=-1 processGc0Delta=0 processGc1Delta=0 processGc2Delta=0 noGcDurationClaim=true")
        $markerStamp+=10;$marked.Add($line+' label='+$label)
    }else{$marked.Add($line)}
}
    return ,$marked
}
$marked=Add-DiagnosticMarkers $valid 1000
$diagnostic=Assert-RescuePerformanceLog -Lines $marked.ToArray() -Run $run
if(!$diagnostic.Qualified){throw 'Complete optional diagnostic boundary schema rejected'};$checks++
foreach($population in @(4000,9871)){
    $profile='rescue-perf-'+$population+'-idle1000walk-passive-on'
    $tierMarked=Add-DiagnosticMarkers $tierInputs[$profile] $population
    $qualified=Assert-RescuePerformanceLog -Lines $tierMarked.ToArray() -Run $run -Scenario $profile
    if(!$qualified.Qualified -or $qualified.Population -ne $population){throw 'Diagnostic higher-tier marker profile rejected'};$checks++
}

Reject @($marked|Where-Object {$_ -notlike '*performance-window-open *iteration=2 *workload=walking*'}) 'Incomplete diagnostic measurement boundaries'
Reject @($marked|ForEach-Object {if($_ -like '*performance-window-close *'){$_.Replace('actualTick=3610','actualTick=3609')}else{$_}}) 'Diagnostic boundary tick/stamp chronology differs'
Reject @($marked|ForEach-Object {if($_ -like '*performance-window-open *'){$_.Replace('run='+$run,'run=ffffffffffffffffffffffffffffffff')}else{$_}}) 'Diagnostic boundary identity differs'
Reject @($marked|ForEach-Object {$_.Replace('processGc0Delta=0','processGc0Delta=-2')}) 'Invalid diagnostic GC boundary'
Reject @($marked|ForEach-Object {$_.Replace('boundaryThreadAllocatedBytesDelta=-1','boundaryThreadAllocatedBytesDelta=-2')}) 'Invalid diagnostic allocation/thread boundary'

$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$temporary=New-ShortWorkDirectory -Root $workspace -Kind t -Name rescue-perflog -Identity 'passive log CLI contract'
try{
    $inputFile=Join-Path $temporary 'input.log';$outputFile=Join-Path $temporary 'result.json'
    foreach($profile in @('rescue-perf-1000-passive-on','rescue-perf-4000-idle1000walk-passive-on','rescue-perf-9871-idle1000walk-passive-on')){
        $inputLines=if($profile -eq 'rescue-perf-1000-passive-on'){$valid}else{$tierInputs[$profile]}
        $population=if($profile -eq 'rescue-perf-1000-passive-on'){1000}elseif($profile -eq 'rescue-perf-4000-idle1000walk-passive-on'){4000}else{9871}
        [IO.File]::WriteAllLines($inputFile,[string[]]$inputLines)
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot '../Test-PerformanceLog.ps1') -LogPath $inputFile -ExpectedRun $run -ExpectedScenario $profile -OutputPath $outputFile
        if($LASTEXITCODE -ne 0){throw ('Fresh-process validator failed: '+$profile)}
        $actual=[IO.File]::ReadAllText($outputFile)|ConvertFrom-Json
        if(!$actual.Qualified -or $actual.Run -cne $run -or $actual.Population -ne $population -or $actual.WalkingPopulation -ne 1000){throw 'CLI did not write exactly selected qualified evidence'}
        $checks++
    }

}finally{Remove-ShortWorkDirectory -Path $temporary}
Write-Output "PASS: passive performance log contract $checks checks; complete manifests, lifetime filtering, callback faults and actual completion boundaries; no game execution."
