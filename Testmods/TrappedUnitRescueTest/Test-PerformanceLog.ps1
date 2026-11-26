[CmdletBinding()]
param([string]$LogPath,[string]$ExpectedRun,[string]$OutputPath,
    [ValidateSet('rescue-perf-1000-passive-on','rescue-perf-4000-idle1000walk-passive-on','rescue-perf-9871-idle1000walk-passive-on','rescue-perf-4000-nojesters-passive-on')]
    [string]$ExpectedScenario='rescue-perf-1000-passive-on')
$ErrorActionPreference='Stop'

# This schema is the explicit fixture/runner evidence contract. It does not
# infer native execution from arbitrary log wording or a live process.
function Assert-RescuePerformanceLog {
    param([Parameter(Mandatory)][AllowEmptyString()][string[]]$Lines,[Parameter(Mandatory)][string]$Run,[string]$Scenario='rescue-perf-1000-passive-on')
    function Require([bool]$Condition,[string]$Reason){if(!$Condition){throw "PERFORMANCE_UNQUALIFIED: $Reason"}}
    function Token([string]$Line,[string]$Name){
        $matches=[regex]::Matches($Line,'(?:^|\s)'+[regex]::Escape($Name)+'=(?<value>\S+)')
        Require ($matches.Count -eq 1) "Expected one $Name token"
        return $matches[0].Groups['value'].Value
    }
    # The archived log can contain unrelated messages between evidence records.
    # Compare positions in the original input, never sorted/filter-only order.
    function Position([string]$Line){return [Array]::IndexOf($Lines,$Line)}
    Require ($Run -cmatch '^[0-9a-f]{32}$') 'Invalid exact run identity'
    Require (@($Lines|Where-Object {($_.Contains('performance-health-diagnostic ') -or $_.Contains('performance-health-event ')) -and (Token $_ 'run') -ceq $Run}).Count -eq 0) 'Active health-event diagnosis is not a passive performance sample'
    $population=switch -CaseSensitive ($Scenario){
        'rescue-perf-1000-passive-on'{1000}
        'rescue-perf-4000-idle1000walk-passive-on'{4000}
        'rescue-perf-4000-nojesters-passive-on'{4000}
        'rescue-perf-9871-idle1000walk-passive-on'{9871}
        default{throw 'PERFORMANCE_UNQUALIFIED: Unknown explicitly selected performance scenario'}
    }
    $nativeStateFixture=$Scenario -ceq 'rescue-perf-4000-nojesters-passive-on'
    $worldRows=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-nojesters-world ') -and (Token $_ 'run') -ceq $Run})
    $readyRows=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-nojesters-ready ') -and (Token $_ 'run') -ceq $Run})
    $baselineRows=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-nojesters-baseline ') -and (Token $_ 'run') -ceq $Run})
    Require (@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-nojesters-fault ') -and (Token $_ 'run') -ceq $Run}).Count -eq 0) 'Native-state fixture callback failure'
    if($nativeStateFixture){
        Require ($baselineRows.Count -eq 1 -and $worldRows.Count -ge 13 -and $worldRows.Count -le 69 -and $readyRows.Count -eq 5) 'Missing bounded native-state fixture evidence'
        $baseline=$baselineRows[0]
        Require ((Token $baseline 'schema') -ceq '1' -and (Token $baseline 'profile') -ceq $Scenario -and
            (Token $baseline 'native-state-fixture') -ieq 'true' -and (Token $baseline 'newSharedBaseline') -ieq 'true' -and
            (Token $baseline 'generalPerformanceClaim') -ieq 'false' -and (Token $baseline 'defaultProfilesUnchanged') -ieq 'true') 'Invalid native-state baseline qualification'
        $fresh=@($worldRows|Where-Object {(Token $_ 'phase') -ceq 'fresh'});$ownedLoads=@($worldRows|Where-Object {(Token $_ 'phase') -ceq 'load'})
        $boundaryRows=@($worldRows|Where-Object {(Token $_ 'phase') -ceq 'boundary'})
        Require ($fresh.Count -eq 1 -and $ownedLoads.Count -eq 4 -and $boundaryRows.Count -ge 8 -and
            $worldRows.Count -eq 5+$boundaryRows.Count) 'Incomplete native-state source/load/boundaries'
        $sessions=[Collections.Generic.HashSet[long]]::new();$worlds=[Collections.Generic.HashSet[long]]::new()
        [long]$lastPosition=-1;[int]$loadCount=0;[int]$boundaryNumber=0;[long]$liveSession=0;[long]$liveWorld=0
        $ready=$false
        $lady=Token $fresh[0] 'lady';[UInt16]$ladyValue=0;Require ([UInt16]::TryParse($lady,[ref]$ladyValue)) 'Invalid preserved Lady word'
        $stateRows=@($Lines|Where-Object {($_.Contains('RESCUE_TEST performance-nojesters-world ') -or $_.Contains('RESCUE_TEST performance-nojesters-ready ')) -and (Token $_ 'run') -ceq $Run})
        foreach($row in $stateRows){
            Require ((Token $row 'schema') -ceq '1' -and (Token $row 'profile') -ceq $Scenario -and
                (Token $row 'flag') -ceq '0' -and (Token $row 'lady') -ceq $lady -and
                (Token $row 'allEightPairsEmpty') -ieq 'true' -and (Token $row 'native-state-fixture') -ieq 'true' -and
                (Token $row 'newSharedBaseline') -ieq 'true' -and (Token $row 'generalPerformanceClaim') -ieq 'false') 'Native-state fixture words/lifetimes differ'
            [long]$sid=0;[long]$generation=0
            Require ([long]::TryParse((Token $row 'session'),[ref]$sid) -and [long]::TryParse((Token $row 'world'),[ref]$generation) -and
                $sid -gt 0 -and $generation -gt 0 -and (Position $row) -gt $lastPosition) 'Native-state world identity/chronology differs'
            $lastPosition=Position $row
            if($row.Contains('RESCUE_TEST performance-nojesters-ready ')){
                [long]$earlyWorld=0
                Require (!$ready -and $liveSession -gt 0 -and $sid -eq $liveSession -and
                    [long]::TryParse((Token $row 'earlyWorld'),[ref]$earlyWorld) -and $earlyWorld -eq $liveWorld -and
                    (Token $row 'tokenSame') -ieq 'true' -and (Token $row 'wordWrites') -ceq '0' -and
                    (Token $row 'loadCount') -ceq [string]$loadCount) 'Ready did not prove its single exact native-world handoff'
                Require ($generation -eq $liveWorld -or $worlds.Add($generation)) 'Ready reused a foreign world generation'
                if($loadCount -eq 0){Require ((Position $row) -lt (Position $baseline)) 'Fresh Ready handoff followed performance preparation'}
                $liveWorld=$generation;$ready=$true
                continue
            }
            $phase=Token $row 'phase'
            if($phase -ceq 'fresh'){
                Require ($liveSession -eq 0 -and (Token $row 'wordWrites') -ceq '1' -and $loadCount -eq 0) 'Fresh native-state write did not occur exactly once'
                Require ((Position $row) -lt (Position $baseline)) 'Native-state setup was not before performance preparation'
                Require ($sessions.Add($sid) -and $worlds.Add($generation)) 'Duplicate native-state fresh world'
                $liveSession=$sid;$liveWorld=$generation
            }elseif($phase -ceq 'load'){
                Require ($ready) 'Owned load preceded the prior Ready handoff'
                $loadCount++
                Require ((Token $row 'wordWrites') -ceq '0' -and $sessions.Add($sid) -and $worlds.Add($generation)) 'Owned save repaired the flag or reused its old world'
                $liveSession=$sid;$liveWorld=$generation;$ready=$false
            }else{
                Require ($phase -ceq 'boundary' -and $ready) 'Boundary lacks its exact Ready handoff'
                $boundaryNumber++
                Require ((Token $row 'wordWrites') -ceq '0' -and $sid -eq $liveSession -and $generation -eq $liveWorld) 'Boundary crossed/repaired native state'
            }
            Require ((Token $row 'loadCount') -ceq [string]$loadCount -and (Token $row 'boundary') -ceq [string]$boundaryNumber) 'Native-state load/boundary sequence differs'
        }
        for($i=0;$i -lt 4;$i++){
            $window=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-window ') -and (Token $_ 'iteration') -ceq [string]$i})
            Require ($window.Count -eq 2 -and (Position $ownedLoads[$i]) -lt (Position $window[0])) 'Native-state load is not before its measured windows'
            $readyForLoad=@($readyRows|Where-Object {(Token $_ 'loadCount') -ceq [string]($i+1)})
            $opens=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-window-open ') -and (Token $_ 'iteration') -ceq [string]$i -and (Token $_ 'run') -ceq $Run})
            $firstWindow=if($opens.Count){$opens[0]}else{$window[0]}
            Require ($readyForLoad.Count -eq 1 -and (Position $readyForLoad[0]) -lt (Position $firstWindow)) 'Native-state Ready handoff was not before its measured windows'
            if($i -lt 3){Require ((Position $window[1]) -lt (Position $ownedLoads[$i+1])) 'Native-state next load preceded old windows'}
        }
    }else{
        Require ($worldRows.Count -eq 0 -and $readyRows.Count -eq 0 -and $baselineRows.Count -eq 0) 'Artificial native-state world cannot qualify as an ordinary baseline'
    }
    $batchCount=[int][Math]::Ceiling($population/32.0)
    $begin=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-army-begin ')})
    $end=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-army-manifest-end ')})
    Require ($begin.Count -eq 1 -and $end.Count -eq 1) 'Missing or duplicate army manifest boundary'
    Require ((Token $begin[0] 'run') -ceq $Run -and (Token $end[0] 'run') -ceq $Run) 'Manifest belongs to another run'
    Require ((Token $begin[0] 'schema') -ceq '1' -and (Token $end[0] 'schema') -ceq '1') 'Unknown manifest schema'
    Require ((Token $begin[0] 'population') -ceq [string]$population -and (Token $begin[0] 'batches') -ceq [string]$batchCount -and
        (Token $begin[0] 'observeArmyMoves') -ieq 'false') 'Expected explicitly selected passive population profile'
    $stateTokens=[regex]::Matches($begin[0],'(?:^|\s)native-state-fixture=(?<value>\S+)')
    if($nativeStateFixture){
        Require ($stateTokens.Count -eq 1 -and $stateTokens[0].Groups['value'].Value -ieq 'true' -and
            (Token $begin[0] 'baseline') -ceq 'jester-spawn-disabled') 'Manifest lacks the new artificial baseline'
    }else{
        Require ($stateTokens.Count -eq 0 -or ($stateTokens.Count -eq 1 -and $stateTokens[0].Groups['value'].Value -ieq 'false')) 'Ordinary manifest conceals a native-state fixture'
    }
    Require ((Token $end[0] 'count') -ceq [string]$population -and (Token $end[0] 'batches') -ceq [string]$batchCount) 'Incomplete army manifest'
    if($population -gt 1000){
        Require ((Token $begin[0] 'scenario') -ceq $Scenario -and (Token $begin[0] 'walkingPopulation') -ceq '1000' -and
            (Token $begin[0] 'workloadModel') -ceq 'full-idle-1000-walking') 'Wrong selected population/cohort profile'
        $extreme=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-extreme ')})
        Require ($extreme.Count -eq 5) 'Missing native Extreme readbacks'
        $boundaries=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach($line in $extreme){
            Require ((Token $line 'run') -ceq $Run -and (Token $line 'scenario') -ceq $Scenario -and
                (Token $line 'extremeTroops') -ceq '1' -and (Token $line 'publicReadback') -ieq 'true' -and
                $boundaries.Add((Token $line 'boundary'))) 'Invalid or duplicate native Extreme readback'
        }
        foreach($boundary in @('before-spawn','load-0','load-1','load-2','load-3')){Require ($boundaries.Contains($boundary)) 'Missing native Extreme boundary'}
    }
    if($population -gt 1000){
        $lobby=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-extreme-lobby ')})
        Require ($lobby.Count -eq 1 -and (Token $lobby[0] 'scenario') -ceq $Scenario -and
            (Token $lobby[0] 'extremeTroops') -ceq '1' -and (Token $lobby[0] 'beforeLaunch') -ieq 'true') 'Missing genuine Extreme lobby precondition'
        $populationRows=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-population ')})
        Require ($populationRows.Count -eq 1) 'Missing full physical population census'
        $row=$populationRows[0];[int]$before=0;[int]$after=0;[int]$capacity=0;[int]$reserve=0
        Require ((Token $row 'run') -ceq $Run -and (Token $row 'requested') -ceq [string]$population -and
            (Token $row 'actors') -ceq [string]$population -and (Token $row 'walkingPopulation') -ceq '1000' -and
            (Token $row 'groups') -ceq '32' -and (Token $row 'freeMarginRequired') -ceq '64' -and
            [int]::TryParse((Token $row 'physicalBefore'),[ref]$before) -and [int]::TryParse((Token $row 'physicalAfter'),[ref]$after) -and
            [int]::TryParse((Token $row 'physicalCapacity'),[ref]$capacity) -and [int]::TryParse((Token $row 'ambientReservation'),[ref]$reserve) -and
            $before -ge 0 -and $after -eq $before+$population -and $capacity -eq 9999 -and
            $reserve -eq [Math]::Max(128,[int]([Math]::Ceiling(($before+64)/64.0)*64)) -and $population+$reserve -le $capacity) 'Incomplete tier or insufficient actual ambient reservation'
        $spawn=@($extreme|Where-Object {(Token $_ 'boundary') -ceq 'before-spawn'})[0]
        Require ((Position $lobby[0]) -lt (Position $spawn) -and (Position $spawn) -lt (Position $row) -and
            (Position $row) -lt (Position $begin[0])) 'Extreme/spawn chronology differs'
    }
    $identity=Token $end[0] 'identity'
    [UInt64]$identityValue=0
    Require ([UInt64]::TryParse($identity,[ref]$identityValue)) 'Invalid army identity checksum'
    $ids=[Collections.Generic.HashSet[int]]::new()
    $lifetimes=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $batches=[Collections.Generic.HashSet[int]]::new()
    $manifest=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-army-manifest ')})
    Require ($manifest.Count -eq $batchCount) ("Expected exactly $batchCount army batches")
    $cursor=Position $begin[0];$expectedBatch=0
    foreach($line in $manifest){
        Require ((Token $line 'run') -ceq $Run -and (Token $line 'schema') -ceq '1' -and (Token $line 'batches') -ceq [string]$batchCount) 'Foreign army batch'
        [int]$batch=0
        Require ([int]::TryParse((Token $line 'batch'),[ref]$batch) -and $batch -ge 0 -and $batch -lt $batchCount -and $batches.Add($batch)) 'Invalid or duplicate army batch'
        $position=Position $line
        Require ($batch -eq $expectedBatch -and $position -gt $cursor) 'Army manifest chronology differs'
        $cursor=$position;$expectedBatch++
        $count=[Math]::Min(32,$population-$batch*32)
        Require ((Token $line 'count') -ceq [string]$count) 'Wrong army batch size'
        $units=(Token $line 'units').Split(',')
        Require ($units.Length -eq $count) 'Truncated army batch'
        foreach($unit in $units){
            $match=[regex]::Match($unit,'^(?<id>[1-9][0-9]*)/(?<global>[1-9][0-9]*)$')
            Require $match.Success 'Malformed army lifetime'
            [int]$unitId=0;[UInt32]$global=0
            Require ([int]::TryParse($match.Groups['id'].Value,[ref]$unitId) -and $unitId -le 9999 -and
                [UInt32]::TryParse($match.Groups['global'].Value,[ref]$global) -and $global -gt 0) 'Army lifetime outside native contract'
            Require ($ids.Add($unitId) -and $lifetimes.Add($unitId.ToString()+'/'+$global.ToString())) 'Duplicate army ID'
        }
    }
    Require ($ids.Count -eq $population) 'Incomplete physical army identity set'
    Require ((Position $end[0]) -gt $cursor) 'Army manifest boundary chronology differs'
    $cursor=Position $end[0]
    $armyResults=0
    foreach($line in $Lines){
        Require ($line -notmatch '\[(?:Error|Fatal)\s*:\s*(?:APIShared|Trapped Unit Rescue Test|SHCDE-SE|Perf Probe|Fixes)\s*\]' -and
            $line -notmatch 'UNIT_MOVE_OBSERVER_FAILED|UNIT_MOVE_NATIVE_OWNER_CHANGED|Simulation tick callback failed:|WORLD_MUTATION_CALLBACK_FAILED|INIT_FAILED|FIXES_INTEGRATION_INACTIVE') 'Relevant runtime/observer failure'
        if($line.Contains('RESCUE_TEST unit=')){
            $result=[regex]::Match($line,'RESCUE_TEST unit=(?<id>[1-9][0-9]*)/(?<global>[1-9][0-9]*) result=(?<status>\S+)')
            Require $result.Success 'Malformed rescue result; cannot exclude army mutation'
            if($lifetimes.Contains($result.Groups['id'].Value+'/'+$result.Groups['global'].Value)){$armyResults++}
        }
    }
    Require ($armyResults -eq 0) 'A rescue attempt affected a performance army lifetime'
    $windows=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-window ')})
    Require ($windows.Count -eq 8) 'Expected eight completed measurement windows'
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $windowPositions=[Collections.Generic.Dictionary[string,int]]::new([StringComparer]::Ordinal)
    foreach($line in $windows){
        Require ((Token $line 'run') -ceq $Run) 'Window belongs to another run'
        $iteration=Token $line 'iteration';$workload=Token $line 'workload'
        Require ($iteration -cmatch '^[0-3]$' -and $workload -cin @('idle','walking') -and $seen.Add($iteration+'/'+$workload)) 'Wrong or duplicate measurement window'
        $windowPositions.Add($iteration+'/'+$workload,(Position $line))
        $enabled=if([int]$iteration % 2){'True'}else{'False'}
        Require ((Token $line 'enabled') -ieq $enabled -and (Token $line 'identity') -ceq $identity -and
            (Token $line 'requestedPopulation') -ceq [string]$population -and (Token $line 'ambientAi') -ieq 'true' -and
            (Token $line 'armyObservation') -ceq 'passive-runtime-log' -and (Token $line 'ownArmyRecoveryCalls') -ceq 'unobserved') 'Window identity or ABAB activation differs'
        if($population -gt 1000){Require ((Token $line 'scenario') -ceq $Scenario -and
            (Token $line 'walkingPopulation') -ceq '1000' -and (Token $line 'workloadModel') -ceq 'full-idle-1000-walking') 'Window cohort differs'}
        # The native summary separately labels advancingTicks=external. Parse
        # the fixture's actual clock boundary before that summary, not the duplicate.
        $head=$line.Split(@(' nativeDurationWindow '),[StringSplitOptions]::None)[0]
        [UInt32]$ticks=0;[UInt32]$completed=0
        Require ([UInt32]::TryParse((Token $head 'advancingTicks'),[ref]$ticks) -and $ticks -ge 3600 -and
            [UInt32]::TryParse((Token $line 'nativeCompleted'),[ref]$completed) -and $completed -ge 3600) 'Insufficient actual native work'
    }
    # Optional additive schema: old completed logs deliberately have no such markers.
    $opens=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-window-open ')})
    $closes=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-window-close ')})
    if($opens.Count -ne 0 -or $closes.Count -ne 0){
        Require ($opens.Count -eq 8 -and $closes.Count -eq 8) 'Incomplete diagnostic measurement boundaries'
        foreach($window in $windows){
            $iteration=Token $window 'iteration';$workload=Token $window 'workload'
            $open=@($opens|Where-Object {(Token $_ 'iteration') -ceq $iteration -and (Token $_ 'workload') -ceq $workload})
            $close=@($closes|Where-Object {(Token $_ 'iteration') -ceq $iteration -and (Token $_ 'workload') -ceq $workload})
            Require ($open.Count -eq 1 -and $close.Count -eq 1) 'Duplicate diagnostic measurement boundary'
            $head=$window.Split(@(' nativeDurationWindow '),[StringSplitOptions]::None)[0]
            [UInt64]$opened=0;[UInt64]$closed=0
            foreach($boundary in @($open[0],$close[0])){
                Require ((Token $boundary 'run') -ceq $Run -and (Token $boundary 'schema') -ceq '1' -and
                    (Token $boundary 'enabled') -ieq (Token $window 'enabled') -and (Token $boundary 'requestedPopulation') -ceq [string]$population -and
                    (Token $boundary 'pausedEngineBoundary') -ieq 'true' -and (Token $boundary 'label') -ceq (Token $window 'label')) 'Diagnostic boundary identity differs'
            }
            [long]$bytes=0;[int]$openThread=0;[int]$closeThread=0
            Require ([int]::TryParse((Token $open[0] 'boundaryThread'),[ref]$openThread) -and $openThread -gt 0 -and
                [int]::TryParse((Token $close[0] 'boundaryThread'),[ref]$closeThread) -and $closeThread -gt 0 -and
                [long]::TryParse((Token $close[0] 'boundaryThreadAllocatedBytesDelta'),[ref]$bytes) -and $bytes -ge -1 -and
                ($openThread -eq $closeThread -or $bytes -eq -1) -and (Token $close[0] 'noGcDurationClaim') -ieq 'true') 'Invalid diagnostic allocation/thread boundary'
            foreach($name in @('processGc0Delta','processGc1Delta','processGc2Delta')){
                [int]$gc=0;Require ([int]::TryParse((Token $close[0] $name),[ref]$gc) -and $gc -ge -1) 'Invalid diagnostic GC boundary'
            }
            Require ((Token $open[0] 'actualTick') -ceq (Token $head 'startTick') -and (Token $close[0] 'actualTick') -ceq (Token $head 'endTick') -and
                (Token $close[0] 'actualAdvancingTicks') -ceq (Token $head 'advancingTicks') -and
                [UInt64]::TryParse((Token $open[0] 'monotonicStamp'),[ref]$opened) -and [UInt64]::TryParse((Token $close[0] 'monotonicStamp'),[ref]$closed) -and $closed -gt $opened -and
                (Position $open[0]) -lt (Position $close[0]) -and (Position $close[0]) -lt (Position $window)) 'Diagnostic boundary tick/stamp chronology differs'
        }
    }
    $endpoints=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-endpoints ')})
    Require ($endpoints.Count -eq 4) 'Missing physical endpoint completion'
    $endpointSeen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $endpointPositions=[Collections.Generic.Dictionary[string,int]]::new([StringComparer]::Ordinal)
    foreach($line in $endpoints){
        Require ((Token $line 'run') -ceq $Run) 'Endpoint belongs to another run'
        $iteration=Token $line 'iteration'
        Require ($iteration -cmatch '^[0-3]$' -and $endpointSeen.Add($iteration) -and
            (Token $line 'nativeSampleGoalsArrived') -ceq '32' -and (Token $line 'outsideMeasuredWindow') -ieq 'true') 'Invalid physical endpoint result'
        $endpointPositions.Add($iteration,(Position $line))
    }
    for($iteration=0;$iteration -lt 4;$iteration++){
        if($population -gt 1000){
            $load=@($extreme|Where-Object {(Token $_ 'boundary') -ceq ('load-'+$iteration)})[0]
            $position=Position $load
            Require ($position -gt $cursor -and $position -lt $windowPositions[$iteration.ToString()+'/idle']) 'Native Extreme load chronology differs'
            $cursor=$position
        }
        foreach($workload in @('idle','walking')){
            $position=$windowPositions[$iteration.ToString()+'/'+$workload]
            if($opens.Count -ne 0){
                $openedLine=@($opens|Where-Object {(Token $_ 'iteration') -ceq [string]$iteration -and (Token $_ 'workload') -ceq $workload})[0]
                $closedLine=@($closes|Where-Object {(Token $_ 'iteration') -ceq [string]$iteration -and (Token $_ 'workload') -ceq $workload})[0]
                Require ((Position $openedLine) -gt $cursor -and (Position $closedLine) -gt (Position $openedLine) -and (Position $closedLine) -lt $position) 'Diagnostic boundaries cross workload/mission chronology'
            }
            Require ($position -gt $cursor) 'Performance evidence chronology differs'
            $cursor=$position
        }
        $position=$endpointPositions[$iteration.ToString()]
        Require ($position -gt $cursor) 'Performance evidence chronology differs'
        $cursor=$position
    }
    $terminal=@($Lines|Where-Object {$_ -cmatch ('INGAME_TEST run='+[regex]::Escape($Run)+' result=')})
    Require ($terminal.Count -eq 1 -and $terminal[0] -cmatch ' result=PASS(?:\s|$)') 'Missing exact own terminal PASS'
    Require ((Position $terminal[0]) -gt $cursor) 'Terminal PASS precedes completed performance evidence'
    return [pscustomobject][ordered]@{Schema=1;Run=$Run;Profile='no-extra-unitmove-demand';NativeStateFixture=$nativeStateFixture;GeneralPerformanceClaim=$false;Population=$population;WalkingPopulation=1000;Scenario=$Scenario;ManifestBatches=$batchCount;
        ArmyResultCount=$armyResults;CompletedWindows=8;EndpointCompletions=4;Identity=$identity;Qualified=$true;
        Limitation=$(if($nativeStateFixture){'Artificial no-new-Jesters world; its separate common-save baseline only; not ordinary-world or general overhead acceptance'}else{'Ambient AI; log/pose/endpoints qualify this profile, not higher populations or pure feature overhead'})}
}

if($MyInvocation.InvocationName -ne '.'){
    if(!$LogPath -or !$ExpectedRun -or !$OutputPath){throw 'Supply LogPath, ExpectedRun and OutputPath.'}
    $result=Assert-RescuePerformanceLog -Lines ([IO.File]::ReadAllLines((Resolve-Path -LiteralPath $LogPath).Path)) -Run $ExpectedRun -Scenario $ExpectedScenario
    $destination=$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
    [IO.File]::WriteAllText($destination,(($result|ConvertTo-Json -Depth 4)+"`r`n"),[Text.UTF8Encoding]::new($false))
    Write-Output ('PASS: passive performance log qualified for run '+$ExpectedRun)
}
