# Offline parser; no game/config mutations. Kept separate for deterministic fixtures.
function Get-PerfProbeAnalysis {
    param([string[]]$Lines, [int]$ThresholdMs=100)
    $culture=[Globalization.CultureInfo]::InvariantCulture
    $lastStart=0
    for($i=0;$i -lt $Lines.Count;$i++){if($Lines[$i] -match '^\[Message:\s+BepInEx\] BepInEx .* - Stronghold Crusader Definitive Edition'){$lastStart=$i}}
    $current=@($Lines|Select-Object -Skip $lastStart)
    $frequency=0.0; $estimated=$false
    foreach($line in $current){if($line -match '\bstopwatchFrequency=(\d+)'){$frequency=[double]$Matches[1];break}}
    if($frequency -eq 0){foreach($line in $current){
        if($line -match 'DEVDIAG spike: .*? ms=([0-9.]+).*? startStamp=(\d+) endStamp=(\d+)'){
            $ms=[double]::Parse($Matches[1],$culture)
            if($ms -gt 0){$frequency=([double]$Matches[3]-[double]$Matches[2])*1000/$ms;$estimated=$true;break}
        }
    }}
    $spikes=New-Object 'System.Collections.Generic.List[object]'
    $clockOffset=$null; $day=0.0; $lastEnd=0.0
    $pattern='DEVDIAG spike: (?<name>\S+) ms=(?<ms>[0-9.]+) start=(?<start>[0-9:.]+) end=(?<end>[0-9:.]+) thread=(?<thread>\d+)\s*(?<detail>.*)$'
    for($i=0;$i -lt $current.Count;$i++){
        $line=$current[$i];$m=[regex]::Match($line,$pattern);if(-not $m.Success){continue}
        $ms=[double]::Parse($m.Groups['ms'].Value,$culture)
        $end=[TimeSpan]::ParseExact($m.Groups['end'].Value,'hh\:mm\:ss\.fff',$culture).TotalMilliseconds
        if($end+$day -lt $lastEnd-43200000){$day+=86400000}
        $end+=$day;$lastEnd=$end;$start=$end-$ms;$detail=$m.Groups['detail'].Value;$stamped=$false
        if($frequency -gt 0 -and $detail -match '\bstartStamp=(\d+) endStamp=(\d+)'){
            $a=[double]$Matches[1];$b=[double]$Matches[2]
            if($null -eq $clockOffset){$clockOffset=$end-$b*1000/$frequency}
            $start=$a*1000/$frequency+$clockOffset;$end=$b*1000/$frequency+$clockOffset;$stamped=$true
        }
        $source='unknown';if($line -match '^\[[^:]+:([^\]]+)\]'){$source=$Matches[1].Trim()}
        $eventId='';if($detail -match '\bevent=(\d+)\b'){$eventId=$Matches[1]}
        $spikes.Add([pscustomobject]@{Name=$m.Groups['name'].Value;Ms=$ms;StartMs=$start;EndMs=$end;Source=$source;
            Thread=[int]$m.Groups['thread'].Value;Event=$eventId;Detail=$detail;Stamped=$stamped;Line=$lastStart+$i+1})
    }
    $roots=@($spikes|Where-Object{$_.Ms -ge $ThresholdMs}|Sort-Object StartMs,EndMs)
    $groups=New-Object 'System.Collections.Generic.List[object]'
    foreach($root in $roots){
        $group=if($groups.Count){$groups[$groups.Count-1]}else{$null}
        if($null -eq $group -or $root.StartMs -ge $group.EndMs-0.001){
            $group=[pscustomobject]@{StartMs=$root.StartMs;EndMs=$root.EndMs;Phase='gameplay-or-unknown';Members=@();Samples=@();LockHistory=@();Analysis=@();Evidence=@();Hints=@()}
            $groups.Add($group)
        }else{$group.EndMs=[Math]::Max($group.EndMs,$root.EndMs)}
    }
    $loads=@($spikes|Where-Object Name -eq 'main:EngineInterface.LoadSaveFile')
    if($frequency -gt 0 -and $null -ne $clockOffset){foreach($line in $current){
        if($line -match 'loadSpan method=LoadSaveFile startStamp=(\d+) endStamp=(\d+)'){
            $loads+= [pscustomobject]@{StartMs=[double]$Matches[1]*1000/$frequency+$clockOffset;EndMs=[double]$Matches[2]*1000/$frequency+$clockOffset}
        }
    }}
    $firstLoad=if($loads.Count){($loads|Measure-Object StartMs -Minimum).Minimum}else{$null}
    foreach($group in $groups){
        $group.Members=@($spikes|Where-Object{$_.StartMs -lt $group.EndMs -and $_.EndMs -gt $group.StartMs})
        if(@($loads|Where-Object{$_.StartMs -lt $group.EndMs -and $_.EndMs -gt $group.StartMs}).Count){$group.Phase='load-wrapper-overlap'}
        elseif($null -ne $firstLoad -and $group.EndMs -le $firstLoad){$group.Phase='startup-before-first-save-load'}
        foreach($member in $group.Members){
            $detail=$member.Detail
            if($detail -match '\blockMeasured=True\b' -and $detail -match '\blockWaitMs=([0-9.]+)'){$group.Evidence+=('measured lock wait: '+$member.Name+' '+$Matches[1]+' ms')}
            if($detail -match '\bengineFrameLockWaitMs=([0-9.]+)'){$group.Evidence+=('measured cumulative frame lock wait: '+$Matches[1]+' ms')}
            if($member.Name -eq 'sim:EngineInterface.run' -and $detail -match '\brunPhasesMeasured=True\b' -and $detail -match '\bnativeInclusiveMs=[0-9]'){
                $group.Evidence+='instrumented native/run phases; native time includes mod callbacks'
            }
            if($detail -match '\bsimRunsCompletedDuring='){$group.Hints+='legacy simRunsCompletedDuring counts run calls, not proven native/game ticks'}
        }
        foreach($line in $current){
            if($line -notmatch 'spikeSample event=|lockHistory event=|samplerAnalysis |samplerCapture '){continue}
            if($line -match '(?:spikeSample|lockHistory) event=(\d+)\b'){
                $eventId=$Matches[1];$source='unknown';if($line -match '^\[[^:]+:([^\]]+)\]'){$source=$Matches[1].Trim()}
                if(@($group.Members|Where-Object{$_.Event -eq $eventId -and $_.Source -eq $source}).Count -eq 0){continue}
                if($line -match 'lockHistory event='){$group.LockHistory+=$line}else{$group.Samples+=$line}
                if($line -match 'retainedStartClipped=True|lockHistoryClipped=True|lockHistoryLost=[1-9]'){$group.Hints+='sample/history retention incomplete; missing data is not zero cost'}
                if($line -match '\bmaxSampleGapMs=([0-9.]+)' -and [double]::Parse($Matches[1],$culture) -ge 10){$group.Hints+='sampling gap >=10 ms; sparse samples cannot explain the entire event'}
            }elseif($frequency -gt 0 -and $null -ne $clockOffset -and $line -match 'startStamp=(\d+) endStamp=(\d+)'){
                $a=[double]$Matches[1]*1000/$frequency+$clockOffset;$b=[double]$Matches[2]*1000/$frequency+$clockOffset
                if($a -lt $group.EndMs -and $b -gt $group.StartMs){
                    $group.Analysis+=$line
                    if($line -match 'windowDrops=[1-9]|spikeRequestsSuppressed=[1-9]'){$group.Hints+='diagnostic overload dropped windows/requests; attribution is incomplete'}
                }
            }
        }
        $group.Evidence=@($group.Evidence|Select-Object -Unique);$group.Hints=@($group.Hints|Select-Object -Unique)
    }
    [pscustomobject]@{LastStartLine=$lastStart+1;ClockFrequency=$frequency;FrequencyEstimated=$estimated;Spikes=@($spikes.ToArray());Groups=@($groups.ToArray());
        Context=@($current|Where-Object{$_ -match 'DEVDIAG: (runContext|runPlugin|patchOwners|loadSpan|gatehouseWindow|runCounters|samplerCapture)'})}
}
