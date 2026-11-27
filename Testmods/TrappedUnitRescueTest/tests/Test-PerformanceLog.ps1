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
# Migrated production owners remain strict; markers cannot hide behind a renamed logger.
foreach($failure in @(
    '[Error  :Bugfixes and QoL] [2026-11-27 16:15:08.110] TRAPPED_UNIT_RESCUE UNAVAILABLE startup failed',
    '[Error  :Bugfixes and QoL] unknown main feature failure',
    '[Error  :Pathfinding] movement owner unavailable',
    '[Error  :CastlePlanner] geometry owner unavailable',
    '[Fatal  :Bugfixes and QoL] unexpected main failure',
    '[Error  :Bugfixes and QoL] FIXES_INTEGRATION_INACTIVE contract changed',
    '[Error  :RescueMountedScanner] MOUNTED_SCANNER ERROR contract changed',
    '[Fatal  :RescueMountedScanner] unexpected scanner failure',
    '[Error  :OtherOwner] TRAPPED_UNIT_RESCUE unexpected failure',
    'TRAPPED_UNIT_RESCUE SETTINGS_UNAVAILABLE settings threw',
    'MOUNTED_SCANNER ERROR missing native gate',
    'FIXES_INTEGRATION_INACTIVE disabled')){
    Reject ($valid+@($failure)) 'runtime/observer failure'
}
$compositionTrace='[Info   :  HarmonyX] Running transpiler static System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction> PerfProbe.PerfProbeRunTiming::SafeTranspile(System.Collections.Generic.IEnumerable<HarmonyLib.CodeInstruction> instructions, System.Reflection.MethodBase __originalMethod)'
$compositionPitch='[Error  :Bugfixes and QoL] [2026-11-27 16:15:08.110] PITCH_MOAT_LINE_DISABLED: incompatible locked engine tick after managed patch composition.'
$compositionInput=@($compositionTrace,$compositionPitch)+$valid
$limited=Assert-RescuePerformanceLog -Lines $compositionInput -Run $run
if(!$limited.Qualified -or !$limited.DiagnosticCompositionLimited -or $limited.DiagnosticCompositionLine -cne $compositionPitch -or
    $limited.DiagnosticCompositionReason -notlike '*no full BQoL/Pitch compatibility claim*' -or $limited.GeneralPerformanceClaim){throw 'Known startup composition limit was concealed or widened'};$checks++
if($result.DiagnosticCompositionLimited -or $null -ne $result.DiagnosticCompositionLine -or $null -ne $result.DiagnosticCompositionReason){throw 'Clean result invents a diagnostic limitation'};$checks++
Reject (@($compositionPitch)+$valid) 'runtime/observer failure'
Reject (@($compositionPitch,$compositionTrace)+$valid) 'runtime/observer failure'
Reject (@($compositionTrace,$compositionPitch,$compositionPitch)+$valid) 'runtime/observer failure'
foreach($changed in @(
    $compositionPitch.Replace('[Error','[Fatal'),
    $compositionPitch.Replace('Bugfixes and QoL','OtherOwner'),
    $compositionPitch.Replace('locked engine tick','different engine tick'),
    ($compositionPitch+' extra'),
    $compositionPitch.Replace('[Error','[Info'))){
    Reject (@($compositionTrace,$changed)+$valid) 'runtime/observer failure'
}
Reject (@($compositionTrace.Replace('PerfProbe.PerfProbeRunTiming','Other.RunTiming'),$compositionPitch)+$valid) 'runtime/observer failure'
$armyBeginIndex=[Array]::IndexOf($valid,@($valid|Where-Object {$_ -like '*performance-army-begin *'})[0])
Reject (@($valid[0..$armyBeginIndex])+@($compositionTrace,$compositionPitch)+@($valid[($armyBeginIndex+1)..($valid.Count-1)])) 'runtime/observer failure'
Reject ($valid+@($compositionTrace,$compositionPitch)) 'runtime/observer failure'
$windowIndex=[Array]::IndexOf($valid,@($valid|Where-Object {$_ -like '*performance-window *'})[0])
Reject (@($valid[0..$windowIndex])+@($compositionTrace,$compositionPitch)+@($valid[($windowIndex+1)..($valid.Count-1)])) 'runtime/observer failure'
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

$ordinaryTagged=@($valid|ForEach-Object {if($_ -like '*performance-army-begin *'){$_+' native-state-fixture=false baseline=ordinary-skirmish'}else{$_}})
if(!(Assert-RescuePerformanceLog -Lines $ordinaryTagged -Run $run).Qualified){throw 'Explicit default false baseline was rejected'};$checks++
Reject @($valid|ForEach-Object {if($_ -like '*performance-army-begin *'){$_+' native-state-fixture=true baseline=jester-spawn-disabled'}else{$_}}) 'Ordinary manifest conceals'
# Separate artificial-world profile: ordinary input cannot silently qualify it.
foreach($noJestersPopulation in @(4000,9871)){
    $noJesters="rescue-perf-$noJestersPopulation-nojesters-passive-on"
    $ordinaryTier="rescue-perf-$noJestersPopulation-idle1000walk-passive-on"
    $world=[Collections.Generic.List[string]]::new()
    [int]$worldBoundary=0;[int]$worldLoad=0;[int]$worldSession=1
    function NativeWorldRow([string]$phase,[int]$writes){
        $generation=3*$worldSession;if($phase -ceq 'boundary'){$generation+=2}
        return "RESCUE_TEST performance-nojesters-world schema=1 run=$run profile=$noJesters phase=$phase session=$worldSession world=$generation flag=0 lady=0 allEightPairsEmpty=true wordWrites=$writes loadCount=$worldLoad boundary=$worldBoundary native-state-fixture=true newSharedBaseline=true generalPerformanceClaim=false"
    }
    function NativeReadyRow{
        $early=3*$worldSession;$generation=$early+2
        return "RESCUE_TEST performance-nojesters-ready schema=1 run=$run profile=$noJesters session=$worldSession earlyWorld=$early world=$generation tokenSame=true flag=0 lady=0 allEightPairsEmpty=true wordWrites=0 loadCount=$worldLoad native-state-fixture=true newSharedBaseline=true generalPerformanceClaim=false"
    }
    foreach($line in $tierInputs[$ordinaryTier]){
        $line=$line.Replace($ordinaryTier,$noJesters)
        if($line -like '*performance-extreme-lobby *'){
            $world.Add($line);$world.Add((NativeWorldRow 'fresh' 1));$world.Add((NativeReadyRow))
            $world.Add("RESCUE_TEST performance-nojesters-baseline schema=1 run=$run profile=$noJesters native-state-fixture=true newSharedBaseline=true generalPerformanceClaim=false defaultProfilesUnchanged=true")
            continue
        }
        if($line -like '*performance-army-begin *'){$worldBoundary++;$world.Add((NativeWorldRow 'boundary' 0));$line+=' native-state-fixture=true baseline=jester-spawn-disabled'}
        if($line -like '*performance-extreme *boundary=load-*'){$worldLoad++;$worldSession++;$world.Add((NativeWorldRow 'load' 0));$world.Add((NativeReadyRow))}
        if($line -like '*performance-window *'){$worldBoundary++;$world.Add((NativeWorldRow 'boundary' 0))}
        $world.Add($line)
    }
    $noJestersValid=$world.ToArray()
    $qualified=Assert-RescuePerformanceLog -Lines $noJestersValid -Run $run -Scenario $noJesters
    if(!$qualified.Qualified -or !$qualified.NativeStateFixture -or $qualified.GeneralPerformanceClaim -or $qualified.Population -ne $noJestersPopulation){throw 'Artificial world lost its separate qualification'};$checks++
    function RejectNativeWorld([string[]]$inputLines){
        $rejected=$false;try{$null=Assert-RescuePerformanceLog -Lines $inputLines -Run $run -Scenario $noJesters}catch{$rejected=$true}
        if(!$rejected){throw 'Changed/missing native-state world evidence was accepted'};$script:checks++
    }
    RejectNativeWorld @($noJestersValid|Where-Object {$_ -notlike '*phase=fresh *'})
    RejectNativeWorld @($noJestersValid|Where-Object {$_ -notlike '*performance-nojesters-baseline *'})
    RejectNativeWorld @($noJestersValid|ForEach-Object {$_.Replace('wordWrites=0','wordWrites=1')})
    RejectNativeWorld @($noJestersValid|ForEach-Object {$_.Replace('allEightPairsEmpty=true','allEightPairsEmpty=false')})
    RejectNativeWorld @($noJestersValid|ForEach-Object {$_.Replace('flag=0','flag=1')})
    RejectNativeWorld @($noJestersValid|ForEach-Object {if($_ -like '*phase=load *loadCount=2 *'){$_.Replace('lady=0','lady=1')}else{$_}})
    RejectNativeWorld @($noJestersValid|ForEach-Object {if($_ -like '*phase=load *loadCount=2 *'){$_.Replace('session=3','session=2')}else{$_}})
    RejectNativeWorld @($noJestersValid|ForEach-Object {if($_ -like '*phase=boundary *boundary=4 *'){[regex]::Replace($_,'(?<= )world=\d+','world=999')}else{$_}})
    RejectNativeWorld @($noJestersValid|ForEach-Object {$_.Replace('phase=fresh','phase=unknown')})
    RejectNativeWorld @($noJestersValid|ForEach-Object {$_.Replace('newSharedBaseline=true','newSharedBaseline=false')})
    RejectNativeWorld @($noJestersValid|ForEach-Object {if($_ -like '*performance-army-begin *'){$_.Replace('native-state-fixture=true','native-state-fixture=false')}else{$_}})
    RejectNativeWorld ($noJestersValid+"RESCUE_TEST performance-nojesters-fault run=$run error=test")
    RejectNativeWorld ($noJestersValid+"RESCUE_TEST performance-health-diagnostic run=$run diagnosticSubscriptions=true")
    RejectNativeWorld @($noJestersValid|Where-Object {$_ -notlike '*performance-nojesters-ready *'})
    $readyExample=@($noJestersValid|Where-Object {$_ -like '*performance-nojesters-ready *'})[0]
    RejectNativeWorld ($noJestersValid+$readyExample)
    RejectNativeWorld @($noJestersValid|ForEach-Object {$_.Replace('tokenSame=true','tokenSame=false')})
    RejectNativeWorld @($noJestersValid|ForEach-Object {if($_ -like '*performance-nojesters-ready *'){[regex]::Replace($_,'earlyWorld=\d+','earlyWorld=999')}else{$_}})
    RejectNativeWorld @($noJestersValid|ForEach-Object {if($_ -like '*performance-nojesters-ready *loadCount=2 *'){$_.Replace('session=3','session=2')}else{$_}})
    RejectNativeWorld @($noJestersValid|ForEach-Object {if($_ -like '*performance-nojesters-ready *'){$_.Replace('wordWrites=0','wordWrites=1')}else{$_}})
    RejectNativeWorld (@($readyExample)+$noJestersValid)
    # A coherent Ready/Boundary sequence after measurement is still too late.
    $lateRecords=@($noJestersValid|Where-Object {($_ -like '*performance-nojesters-ready *' -or $_ -like '*phase=boundary *') -and $_ -like '*session=2 *'})
    $lateReady=[Collections.Generic.List[string]]::new()
    foreach($row in $noJestersValid){
        if($row -cin $lateRecords){continue}
        $lateReady.Add($row)
        if($row -like '*RESCUE_TEST performance-window *iteration=0 *workload=walking*'){foreach($late in $lateRecords){$lateReady.Add($late)}}
    }
    RejectNativeWorld $lateReady.ToArray()
    $sameGeneration=@($noJestersValid|ForEach-Object {
        $row=$_
        for($sid=1;$sid -le 5;$sid++){$row=$row.Replace(' world='+[string](3*$sid+2)+' ',' world='+[string](3*$sid)+' ')}
        $row
    })
    if(!(Assert-RescuePerformanceLog -Lines $sameGeneration -Run $run -Scenario $noJesters).Qualified){throw 'Equal generation with explicit same-native-token handoff was rejected'};$checks++
    $rejected=$false;try{$null=Assert-RescuePerformanceLog -Lines $noJestersValid -Run $run -Scenario $ordinaryTier}catch{$rejected=$true}
    if(!$rejected){throw 'Artificial world was accepted as an ordinary performance baseline'};$checks++
    $tierInputs[$noJesters]=$noJestersValid
}


# Outcome schema2 is additive to manifest schema1. Historical records remain
# qualified only with an explicit unknown-removal result, never a false zero.
if($result.OutcomeSchema -ne 1 -or $result.OutcomeCountersObserved -or
    $null -ne $result.RemovalRequestCountTotal -or $result.CurrentRemovalOverheadClaim -or
    $result.NativeDeletionCompletionObserved){throw 'Historical outcome limitations were lost'};$checks++
function Add-OutcomeCounters([string[]]$inputLines,[long]$requests=0){
    return @($inputLines|ForEach-Object {
        if($_ -like '*performance-army-begin *'){$_+' outcomeSchema=2'}
        elseif($_ -like '*performance-window *'){$_+" outcomeSchema=2 captureDelta=3 attemptDelta=2 rescueDelta=1 removalRequestDelta=$requests"}
        else{$_}
    })
}
$current=Add-OutcomeCounters $valid
$currentResult=Assert-RescuePerformanceLog -Lines $current -Run $run
if(!$currentResult.Qualified -or !$currentResult.OutcomeCountersObserved -or $currentResult.OutcomeSchema -ne 2 -or
    $currentResult.CaptureCountTotal -ne 24 -or $currentResult.AttemptCountTotal -ne 16 -or
    $currentResult.RescueCountTotal -ne 8 -or $currentResult.RemovalRequestCountTotal -ne 0 -or
    $currentResult.RemovalRequestWindows -ne 0 -or $currentResult.AmbientRemovalRequestsObserved -or
    $currentResult.NativeDeletionCompletionObserved -or $currentResult.GeneralPerformanceClaim -or
    $currentResult.CurrentRemovalOverheadClaim){throw 'Current complete outcome counters were not reported honestly'};$checks++
$ambient=Add-OutcomeCounters $valid 2
$ambientResult=Assert-RescuePerformanceLog -Lines ($ambient+'RESCUE_TEST unit=1001/9001 result=Removed') -Run $run
if(!$ambientResult.Qualified -or $ambientResult.RemovalRequestCountTotal -ne 16 -or
    $ambientResult.RemovalRequestWindows -ne 8 -or !$ambientResult.AmbientRemovalRequestsObserved){throw 'Unrelated ambient removal requests were rejected or hidden'};$checks++
Reject ($ambient+'RESCUE_TEST unit=1/1001 result=Removed') 'rescue attempt affected'
foreach($name in @('captureDelta','attemptDelta','rescueDelta','removalRequestDelta')){
    Reject @($current|ForEach-Object {[regex]::Replace($_,' '+$name+'=\d+','')}) ('Expected one '+$name+' token')
    Reject @($current|ForEach-Object {if($_ -like '*performance-window *'){$_+' '+$name+'=0'}else{$_}}) ('Expected one '+$name+' token')
    foreach($invalid in @('-1','1.5','+1','01','9223372036854775808','bad')){
        Reject @($current|ForEach-Object {[regex]::Replace($_,'(?<= )'+$name+'=\d+',$name+'='+$invalid)}) ('Invalid nonnegative outcome counter '+$name)
    }
}
Reject @($current|ForEach-Object {if($_ -like '*performance-window *iteration=2 *workload=idle*'){$_.Replace(' outcomeSchema=2','')}else{$_}}) 'Mixed outcome schemas'
Reject @($current|ForEach-Object {if($_ -like '*performance-army-begin *'){$_.Replace(' outcomeSchema=2','')}else{$_}}) 'Mixed outcome schemas'
Reject @($current|ForEach-Object {$_.Replace('outcomeSchema=2','outcomeSchema=3')}) 'Unknown outcome schema'
Reject @($current|ForEach-Object {if($_ -like '*performance-window *'){$_+' outcomeSchema=2'}else{$_}}) 'Duplicate outcomeSchema token'
Reject @($current|ForEach-Object {if($_ -like '*performance-army-begin *'){$_+' outcomeSchema=2'}else{$_}}) 'Duplicate outcomeSchema token'
Reject @($current|ForEach-Object {$_.Replace('outcomeSchema=2','outcomeSchema=1')}) 'Historical outcome schema contains undeclared removal requests'
Reject @($current|ForEach-Object {$_.Replace('removalRequestDelta=0','removalRequestDelta=9223372036854775807')}) 'Outcome counter total overflow'
foreach($profile in @('rescue-perf-4000-idle1000walk-passive-on','rescue-perf-9871-idle1000walk-passive-on','rescue-perf-4000-nojesters-passive-on','rescue-perf-9871-nojesters-passive-on')){
    $currentTier=Add-OutcomeCounters $tierInputs[$profile] 1
    $outcome=Assert-RescuePerformanceLog -Lines $currentTier -Run $run -Scenario $profile
    if(!$outcome.Qualified -or !$outcome.OutcomeCountersObserved -or $outcome.RemovalRequestCountTotal -ne 8){throw 'Current higher-tier outcome schema rejected'};$checks++
}

$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$temporary=New-ShortWorkDirectory -Root $workspace -Kind t -Name rescue-perflog -Identity 'passive log CLI contract'
try{
    $inputFile=Join-Path $temporary 'input.log';$outputFile=Join-Path $temporary 'result.json'
    foreach($profile in @('rescue-perf-1000-passive-on','rescue-perf-4000-idle1000walk-passive-on','rescue-perf-9871-idle1000walk-passive-on','rescue-perf-4000-nojesters-passive-on','rescue-perf-9871-nojesters-passive-on')){
        $inputLines=if($profile -eq 'rescue-perf-1000-passive-on'){$valid}else{$tierInputs[$profile]}
        $population=if($profile -eq 'rescue-perf-1000-passive-on'){1000}elseif($profile -in @('rescue-perf-4000-idle1000walk-passive-on','rescue-perf-4000-nojesters-passive-on')){4000}else{9871}
        [IO.File]::WriteAllLines($inputFile,[string[]]$inputLines)
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot '../Test-PerformanceLog.ps1') -LogPath $inputFile -ExpectedRun $run -ExpectedScenario $profile -OutputPath $outputFile
        if($LASTEXITCODE -ne 0){throw ('Fresh-process validator failed: '+$profile)}
        $actual=[IO.File]::ReadAllText($outputFile)|ConvertFrom-Json
        if(!$actual.Qualified -or $actual.Run -cne $run -or $actual.Population -ne $population -or $actual.WalkingPopulation -ne 1000){throw 'CLI did not write exactly selected qualified evidence'}
        $checks++
    }
    [IO.File]::WriteAllLines($inputFile,[string[]]$compositionInput)
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot '../Test-PerformanceLog.ps1') -LogPath $inputFile -ExpectedRun $run -OutputPath $outputFile
    if($LASTEXITCODE -ne 0){throw 'Fresh-process known startup diagnostic qualification failed'}
    $actual=[IO.File]::ReadAllText($outputFile)|ConvertFrom-Json
    if(!$actual.Qualified -or !$actual.DiagnosticCompositionLimited -or $actual.DiagnosticCompositionLine -cne $compositionPitch -or
        $actual.DiagnosticCompositionReason -notlike '*rescue-profile comparison only*' -or $actual.GeneralPerformanceClaim){throw 'CLI concealed diagnostic-only feature-set limit'};$checks++
    [IO.File]::WriteAllLines($inputFile,[string[]]$ambient)
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot '../Test-PerformanceLog.ps1') -LogPath $inputFile -ExpectedRun $run -OutputPath $outputFile
    if($LASTEXITCODE -ne 0){throw 'Fresh-process current outcome validator failed'}
    $actual=[IO.File]::ReadAllText($outputFile)|ConvertFrom-Json
    if(!$actual.Qualified -or !$actual.OutcomeCountersObserved -or $actual.RemovalRequestCountTotal -ne 16 -or
        $actual.NativeDeletionCompletionObserved -or $actual.CurrentRemovalOverheadClaim){throw 'CLI lost current request/completion distinction'};$checks++

}finally{Remove-ShortWorkDirectory -Path $temporary}
Write-Output "PASS: passive performance log contract $checks checks; complete manifests, lifetime filtering, callback faults and actual completion boundaries; no game execution."
