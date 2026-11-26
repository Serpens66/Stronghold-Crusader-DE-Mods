[CmdletBinding()]
param([string]$LogPath,[string]$ExpectedRun,[string]$OutputPath)
$ErrorActionPreference='Stop'

# This schema is the explicit fixture/runner evidence contract. It does not
# infer native execution from arbitrary log wording or a live process.
function Assert-RescuePerformanceLog {
    param([Parameter(Mandatory)][string[]]$Lines,[Parameter(Mandatory)][string]$Run)
    function Require([bool]$Condition,[string]$Reason){if(!$Condition){throw "PERFORMANCE_UNQUALIFIED: $Reason"}}
    function Token([string]$Line,[string]$Name){
        $matches=[regex]::Matches($Line,'(?:^|\s)'+[regex]::Escape($Name)+'=(?<value>\S+)')
        Require ($matches.Count -eq 1) "Expected one $Name token"
        return $matches[0].Groups['value'].Value
    }
    Require ($Run -cmatch '^[0-9a-f]{32}$') 'Invalid exact run identity'
    $begin=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-army-begin ')})
    $end=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-army-manifest-end ')})
    Require ($begin.Count -eq 1 -and $end.Count -eq 1) 'Missing or duplicate army manifest boundary'
    Require ((Token $begin[0] 'run') -ceq $Run -and (Token $end[0] 'run') -ceq $Run) 'Manifest belongs to another run'
    Require ((Token $begin[0] 'schema') -ceq '1' -and (Token $end[0] 'schema') -ceq '1') 'Unknown manifest schema'
    Require ((Token $begin[0] 'population') -ceq '1000' -and (Token $begin[0] 'batches') -ceq '32' -and
        (Token $begin[0] 'observeArmyMoves') -ieq 'false') 'Expected passive 1000-unit profile'
    Require ((Token $end[0] 'count') -ceq '1000' -and (Token $end[0] 'batches') -ceq '32') 'Incomplete army manifest'
    $identity=Token $end[0] 'identity'
    [UInt64]$identityValue=0
    Require ([UInt64]::TryParse($identity,[ref]$identityValue)) 'Invalid army identity checksum'
    $ids=[Collections.Generic.HashSet[int]]::new()
    $lifetimes=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $batches=[Collections.Generic.HashSet[int]]::new()
    $manifest=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-army-manifest ')})
    Require ($manifest.Count -eq 32) 'Expected exactly 32 army batches'
    foreach($line in $manifest){
        Require ((Token $line 'run') -ceq $Run -and (Token $line 'schema') -ceq '1' -and (Token $line 'batches') -ceq '32') 'Foreign army batch'
        [int]$batch=0
        Require ([int]::TryParse((Token $line 'batch'),[ref]$batch) -and $batch -ge 0 -and $batch -lt 32 -and $batches.Add($batch)) 'Invalid or duplicate army batch'
        $count=if($batch -eq 31){8}else{32}
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
    Require ($ids.Count -eq 1000) 'Incomplete physical army identity set'
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
    foreach($line in $windows){
        $iteration=Token $line 'iteration';$workload=Token $line 'workload'
        Require ($iteration -cmatch '^[0-3]$' -and $workload -cin @('idle','walking') -and $seen.Add($iteration+'/'+$workload)) 'Wrong or duplicate measurement window'
        $enabled=if([int]$iteration % 2){'True'}else{'False'}
        Require ((Token $line 'enabled') -ieq $enabled -and (Token $line 'identity') -ceq $identity -and
            (Token $line 'requestedPopulation') -ceq '1000' -and (Token $line 'ambientAi') -ieq 'true' -and
            (Token $line 'armyObservation') -ceq 'passive-runtime-log' -and (Token $line 'ownArmyRecoveryCalls') -ceq 'unobserved') 'Window identity or ABAB activation differs'
        # The native summary separately labels advancingTicks=external. Parse
        # the fixture's actual clock boundary before that summary, not the duplicate.
        $head=$line.Split(@(' nativeDurationWindow '),[StringSplitOptions]::None)[0]
        [UInt32]$ticks=0;[UInt32]$completed=0
        Require ([UInt32]::TryParse((Token $head 'advancingTicks'),[ref]$ticks) -and $ticks -ge 3600 -and
            [UInt32]::TryParse((Token $line 'nativeCompleted'),[ref]$completed) -and $completed -ge 3600) 'Insufficient actual native work'
    }
    $endpoints=@($Lines|Where-Object {$_.Contains('RESCUE_TEST performance-endpoints ')})
    Require ($endpoints.Count -eq 4) 'Missing physical endpoint completion'
    $endpointSeen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($line in $endpoints){
        $iteration=Token $line 'iteration'
        Require ($iteration -cmatch '^[0-3]$' -and $endpointSeen.Add($iteration) -and
            (Token $line 'nativeSampleGoalsArrived') -ceq '32' -and (Token $line 'outsideMeasuredWindow') -ieq 'true') 'Invalid physical endpoint result'
    }
    $terminal=@($Lines|Where-Object {$_ -cmatch ('INGAME_TEST run='+[regex]::Escape($Run)+' result=')})
    Require ($terminal.Count -eq 1 -and $terminal[0] -cmatch ' result=PASS(?:\s|$)') 'Missing exact own terminal PASS'
    return [pscustomobject][ordered]@{Schema=1;Run=$Run;Profile='no-extra-unitmove-demand';Population=1000;ManifestBatches=32;
        ArmyResultCount=$armyResults;CompletedWindows=8;EndpointCompletions=4;Identity=$identity;Qualified=$true;
        Limitation='Ambient AI; log/pose/endpoints qualify this profile, not higher populations or pure feature overhead'}
}

if($MyInvocation.InvocationName -ne '.'){
    if(!$LogPath -or !$ExpectedRun -or !$OutputPath){throw 'Supply LogPath, ExpectedRun and OutputPath.'}
    $result=Assert-RescuePerformanceLog -Lines ([IO.File]::ReadAllLines((Resolve-Path -LiteralPath $LogPath).Path)) -Run $ExpectedRun
    $destination=$ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
    [IO.File]::WriteAllText($destination,(($result|ConvertTo-Json -Depth 4)+"`r`n"),[Text.UTF8Encoding]::new($false))
    Write-Output ('PASS: passive performance log qualified for run '+$ExpectedRun)
}
