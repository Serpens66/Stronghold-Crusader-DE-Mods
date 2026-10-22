# Offline parser; no game/config mutations. Kept separate for deterministic fixtures.
function Get-PerfProbeAnalysis {
    param([string[]]$Lines, [int]$ThresholdMs=100)
    $culture=[Globalization.CultureInfo]::InvariantCulture
    $lastStart=0
    for($i=0;$i -lt $Lines.Count;$i++){if($Lines[$i] -match '^\[Message:\s+BepInEx\] BepInEx .* - Stronghold Crusader Definitive Edition'){$lastStart=$i}}
    $current=@($Lines|Select-Object -Skip $lastStart)
    $exactNativeIp=@($current|Where-Object {$_ -match 'runContext .*nativeUnknownIp=exact'}).Count -gt 0
    $stubRanges=@(foreach($line in $current){
        if($line -match '\["(?<name>[^"]+)"\] compiled stub at 0x(?<address>[0-9A-Fa-f]+), size: (?<size>\d+) bytes \(target: (?<target>0x[0-9A-Fa-f]+)\)'){
            $address=[Convert]::ToUInt64($Matches['address'],16);$size=[uint64]$Matches['size']
            if($size -gt 0 -and $address -le [uint64]::MaxValue-$size){
                [pscustomobject]@{Start=$address;End=$address+$size;Name=$Matches['name'];Target=$Matches['target']}
            }
        }
    })
    $probe=@($current|Where-Object{$_ -match '^\[[^:]+:\s*Perf ?Probe\]'} )
    $probeState='missing'
    foreach($line in $probe){
        if($line -match 'measurements inactive \(no DEVDIAG build\)'){$probeState='inactive'}
        elseif($line -match 'measurements active \(DEVDIAG build\)|PERFPROBE_READY|DEVDIAG: runContext|DEVDIAG: threadSample'){$probeState='active'}
    }
    $warnings=@()
    if($probeState -eq 'inactive'){$warnings+='PerfProbe is inactive (normal build); run spans do not prove frame lags or lock ownership.'}
    elseif($probeState -eq 'missing'){$warnings+='PerfProbe activity is unverified/missing; absence of frame/sampling data is not absence of lags.'}
    $hasSamples=@($probe|Where-Object{$_ -match '\b(?:threadSample|spikeSample)\b' -and $_ -match '\bsamples=([1-9][0-9]*)\b'}).Count -gt 0
    if($probeState -eq 'active' -and -not $hasSamples){$warnings+='PerfProbe is active, but no thread samples are present; stack attribution is unavailable.'}
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
            $group=[pscustomobject]@{StartMs=$root.StartMs;EndMs=$root.EndMs;Phase='gameplay-or-unknown';Kind='long-call';FrameLagRecorded=$false;Members=@();Samples=@();LockHistory=@();Analysis=@();NativeStubSamples=@();Evidence=@();Hints=@()}
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
        $group.FrameLagRecorded=@($group.Members|Where-Object{$_.Name -eq 'frame:interval' -and $_.Ms -ge $ThresholdMs -and $_.Source -match '^Perf ?Probe$'}).Count -gt 0
        if($group.FrameLagRecorded){$group.Kind='recorded-frame-gap'}
        elseif(@($group.Members|Where-Object{$_.Name -in @('sim:EngineInterface.run','bfqol.gatehouse.run') -and $_.Ms -ge $ThresholdMs}).Count){$group.Kind='long-run-call'}
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
                    if($line -match 'cadenceMode=sleep-degraded'){$group.Hints+='sampling uses degraded sleeping cadence; inspect actual sample gaps'}
                }
            }
        }
        foreach($sample in $group.Samples|Where-Object {$_ -match ' self:'}){
            $record=if($sample -match 'spikeSample event=(\d+).*?thread=(\w+)'){$Matches[1]+'/'+$Matches[2]}else{'unknown'}
            foreach($match in [regex]::Matches($sample,'dyn@0x(?<ip>[0-9A-Fa-f]+)=(?<count>\d+)')){
                $ip=[Convert]::ToUInt64($match.Groups['ip'].Value,16)
                $width=if($exactNativeIp){[uint64]1}else{[uint64]64}
                $owners=@($stubRanges|Where-Object {$ip -ge $_.Start -and $ip -lt $_.End -and $_.End-$ip -ge $width})
                $resolution=if($owners.Count -eq 1){if($exactNativeIp){'exact-range'}else{'legacy-bucket-contained'}}else{'unknown-or-ambiguous'}
                $group.NativeStubSamples+=[pscustomobject]@{Record=$record;IP=('0x{0:X}' -f $ip);Samples=[int]$match.Groups['count'].Value;Resolution=$resolution;
                    Stub=$(if($owners.Count -eq 1){$owners[0].Name}else{'unknown'});Target=$(if($owners.Count -eq 1){$owners[0].Target}else{'unknown'})}
            }
        }
        $group.Evidence=@($group.Evidence|Select-Object -Unique);$group.Hints=@($group.Hints|Select-Object -Unique)
    }
    [pscustomobject]@{LastStartLine=$lastStart+1;ClockFrequency=$frequency;FrequencyEstimated=$estimated;PerfProbeState=$probeState;HasSamples=$hasSamples;Warnings=$warnings;Spikes=@($spikes.ToArray());Groups=@($groups.ToArray());
        Context=@($current|Where-Object{$_ -match 'DEVDIAG: (runContext|runPlugin|patchOwners|loadSpan|gatehouseWindow|runCounters|samplerCapture)'})}
}
