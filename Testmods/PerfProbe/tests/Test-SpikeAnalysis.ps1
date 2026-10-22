$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../tools/SpikeAnalysis.ps1')
function Assert($value,[string]$reason){if(-not $value){throw $reason}}
$lines=@'
[Message:   BepInEx] BepInEx version-old - Stronghold Crusader Definitive Edition
[Message:Perf Probe] [11:00:00.000] DEVDIAG spike: frame:interval ms=999 start=11:00:00.000 end=11:00:00.999 thread=1
[Message:   BepInEx] BepInEx version-new - Stronghold Crusader Definitive Edition
[Message:Perf Probe] [12:00:00.000] DEVDIAG: runContext schema=2 stopwatchFrequency=1000
[Message:Perf Probe] [12:00:00.120] DEVDIAG spike: frame:interval ms=120 start=12:00:00.000 end=12:00:00.120 thread=1 engineFrameLockWaitMs=110 event=1 startStamp=1000 endStamp=1120
[Message:Perf Probe] [12:00:00.111] DEVDIAG spike: main:Director.Update(total) ms=110 start=12:00:00.001 end=12:00:00.111 thread=1 event=2 startStamp=1001 endStamp=1111
[Message:Perf Probe] [12:00:00.100] DEVDIAG spike: main:EngineInterface.GetMeritData ms=50 start=12:00:00.050 end=12:00:00.100 thread=1 lockMeasured=True lockWaitMs=49 simRunsCompletedDuring=223 event=3 startStamp=1050 endStamp=1100
[Message:Bugfixes and QoL] [12:00:00.108] DEVDIAG spike: bfqol.gatehouse.run ms=105 start=12:00:00.003 end=12:00:00.108 thread=17 queries=40 startStamp=1003 endStamp=1108
[Message:Perf Probe] [12:00:00.125] DEVDIAG: spikeSample event=1 thread=main samples=0 retainedStartClipped=True maxSampleGapMs=120 self:
[Message:Foreign] [12:00:00.125] DEVDIAG: spikeSample event=1 thread=main WRONG_SOURCE
[Message:Perf Probe] [12:00:00.125] DEVDIAG: lockHistory event=1 lockHistoryClipped=True lockHistoryLost=1
[Message:Perf Probe] [12:00:00.125] DEVDIAG: samplerAnalysis kind=window startStamp=1010 endStamp=1110 wallMs=100 cpuMs=90
[Message:Perf Probe] [12:00:00.240] DEVDIAG spike: frame:interval ms=120 start=12:00:00.120 end=12:00:00.240 thread=1 event=4 startStamp=1120 endStamp=1240
[Message:Perf Probe] [12:00:01.200] DEVDIAG spike: frame:interval ms=200 start=12:00:01.000 end=12:00:01.200 thread=1 event=5 startStamp=2000 endStamp=2200
[Message:Perf Probe] [12:00:01.200] DEVDIAG spike: main:EngineInterface.LoadSaveFile ms=150 start=12:00:01.050 end=12:00:01.200 thread=1 event=6 startStamp=2050 endStamp=2200
'@ -split '\r?\n'
$r=Get-PerfProbeAnalysis -Lines $lines
Assert ($r.LastStartLine -eq 3 -and $r.Spikes.Count -eq 7) 'Latest game-start selection failed.'
Assert ($r.Groups.Count -eq 3) 'Nested spans must merge; adjacent frames must remain separate.'
Assert ($r.Groups[0].Members.Count -eq 4) 'Cross-thread BFQoL or subthreshold cumulative member missing.'
Assert ($r.Groups[0].Samples.Count -eq 1) 'Event IDs crossed plugin/logger boundaries.'
Assert ($r.Groups[0].LockHistory.Count -eq 1 -and $r.Groups[0].Analysis.Count -eq 1) 'Missing ownership/analysis correlation.'
Assert ($r.Groups[0].Evidence.Count -eq 2 -and $r.Groups[0].Hints.Count -ge 2) 'Measured waits, legacy run semantics or incomplete coverage lost.'
Assert ($r.Groups[2].Phase -eq 'load-wrapper-overlap') 'Save-load event treated as ordinary gameplay.'
Assert (($r.Groups[0].EndMs-$r.Groups[0].StartMs) -eq 120) 'Nested inclusive times were added.'
$r=Get-PerfProbeAnalysis -Lines ($lines+@(
    '[Message:Perf Probe] [12:00:00.120] DEVDIAG: loadSpan method=LoadSaveFile startStamp=1100 endStamp=1110 complete=True',
    '[Message:Perf Probe] [12:00:00.125] DEVDIAG: samplerCapture startStamp=1000 endStamp=1120 windowDrops=2 spikeRequestsSuppressed=3'
))
Assert ($r.Groups[0].Phase -eq 'load-wrapper-overlap') 'A subthreshold load span was ignored.'
Assert ($r.Groups[0].Analysis.Count -eq 2 -and ($r.Groups[0].Hints -join ';') -match 'overload') 'Capture interval/drop evidence missing.'
$legacy=@'
[Message:Perf Probe] [23:59:59.800] DEVDIAG spike: frame:interval ms=200 start=23:59:59.600 end=23:59:59.800 thread=1
[Message:Perf Probe] [00:00:00.100] DEVDIAG spike: frame:interval ms=160 start=23:59:59.940 end=00:00:00.100 thread=1
'@ -split '\r?\n'
$r=Get-PerfProbeAnalysis -Lines $legacy
Assert ($r.Groups.Count -eq 2 -and $r.Groups[1].StartMs -gt $r.Groups[0].EndMs) 'Legacy midnight rollover/order failed.'
'PASS: actual parser: latest start, overlap/adjacency, cumulative subthreshold calls, BFQoL correlation, source-scoped events, coverage, loading and legacy midnight.'
