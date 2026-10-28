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
Assert ($r.PerfProbeState -eq 'active' -and $r.Groups[0].FrameLagRecorded -and $r.Groups[0].Kind -eq 'recorded-frame-gap') 'Recorded frame gap was not distinguished from a run call.'
Assert (-not $r.HasSamples -and $r.Warnings.Count -gt 0) 'Empty sample records must not claim usable stack samples.'
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
$incomplete=@'
[Info   :Perf Probe] [23:59:28.331] Perf Probe 0.1.0 loaded; measurements inactive (no DEVDIAG build).
[Message:Bugfixes and QoL] [00:01:00.120] DEVDIAG spike: bfqol.gatehouse.run ms=120 start=00:01:00.000 end=00:01:00.120 thread=15 queries=0
'@ -split '\r?\n'
$r=Get-PerfProbeAnalysis -Lines $incomplete
Assert ($r.PerfProbeState -eq 'inactive' -and $r.Warnings.Count -gt 0 -and -not $r.HasSamples) 'Inactive diagnostic build was not reported.'
Assert ($r.Groups.Count -eq 1 -and $r.Groups[0].Kind -eq 'long-run-call' -and -not $r.Groups[0].FrameLagRecorded) 'Whole run was falsely classified as a recorded frame lag.'
$r=Get-PerfProbeAnalysis -Lines @($incomplete[1])
Assert ($r.PerfProbeState -eq 'missing' -and $r.Warnings.Count -gt 0) 'Missing profiler must remain unknown, not silently active.'
$r=Get-PerfProbeAnalysis -Lines @('[Message:Perf Probe] [00:00:00.000] DEVDIAG: PERFPROBE_READY: installed')
Assert ($r.PerfProbeState -eq 'active' -and -not $r.HasSamples -and $r.Warnings.Count -gt 0) 'READY without samples was mistaken for full attribution coverage.'
$r=Get-PerfProbeAnalysis -Lines @('[Message:Perf Probe] [00:00:00.000] DEVDIAG: threadSample thread=main samples=12 self: CDE=12')
Assert ($r.PerfProbeState -eq 'active' -and $r.HasSamples) 'Usable legacy samples were not detected.'
'PASS: actual parser: latest start, overlap/adjacency, cumulative subthreshold calls, BFQoL correlation, source-scoped events, coverage, loading and legacy midnight.'
$stubLines=@(
 '[Message:RedBird] [12:00:00.000] ["filter"] compiled stub at 0x1000, size: 80 bytes (target: 0x2000)',
 '[Message:Perf Probe] [12:00:00.000] DEVDIAG: runContext schema=2 stopwatchFrequency=1000 nativeUnknownIp=exact',
 '[Message:Perf Probe] [12:00:00.120] DEVDIAG spike: frame:interval ms=120 start=12:00:00.000 end=12:00:00.120 thread=1 event=1 startStamp=1000 endStamp=1120',
 '[Message:Perf Probe] [12:00:00.125] DEVDIAG: spikeSample event=1 thread=main samples=9 self: dyn@0x104F=5,dyn@0x1050=4',
 '[Message:Perf Probe] [12:00:00.125] DEVDIAG: samplerCapture startStamp=1000 endStamp=1120 cadenceMode=sleep-degraded missedIntervals=8'
)
$r=Get-PerfProbeAnalysis -Lines $stubLines
Assert ($r.Groups[0].NativeStubSamples.Count -eq 2) 'Exact unknown instruction addresses lost.'
Assert ($r.Groups[0].NativeStubSamples[0].Resolution -eq 'exact-range' -and $r.Groups[0].NativeStubSamples[0].Stub -eq 'filter') 'Last byte of exact logged stub was not attributed.'
Assert ($r.Groups[0].NativeStubSamples[1].Resolution -eq 'unknown-or-ambiguous') 'First byte outside stub was falsely attributed.'
Assert (($r.Groups[0].Hints -join ';') -match 'degraded') 'Sleeping fallback sampling coverage warning missing.'
$legacyStub=@($stubLines|Where-Object {$_ -notmatch 'nativeUnknownIp=exact'})
$r=Get-PerfProbeAnalysis -Lines $legacyStub
Assert ($r.Groups[0].NativeStubSamples[0].Resolution -eq 'unknown-or-ambiguous') 'Legacy 64-byte bucket crossing stub end must remain ambiguous.'
$r=Get-PerfProbeAnalysis -Lines ($stubLines+@('[Message:RedBird] [12:00:00.000] ["overlap"] compiled stub at 0x1040, size: 32 bytes (target: 0x3000)'))
Assert ($r.Groups[0].NativeStubSamples[0].Resolution -eq 'unknown-or-ambiguous') 'Overlapping stub ranges were resolved without evidence.'
'PASS: exact native stub boundaries, legacy ambiguity, overlapping ranges and degraded sampling cadence.'
# Game-time stalls with smooth frames (trigger 2026-10-28), deferred write delay, resume phase and run summary.
$tickLines=@'
[Message:   BepInEx] BepInEx version - Stronghold Crusader Definitive Edition
[Message:Perf Probe] [12:59:58.000] DEVDIAG: runContext schema=2 stopwatchFrequency=1000
[Message:Perf Probe] [12:59:58.900] DEVDIAG: inputMark key=ScrollLock mark=1 stamp=900 frame=10 lastTickAgeMs=5.0 simRunActive=False
[Message:Perf Probe] [13:00:00.000] DEVDIAG: inputMark key=Mouse1 mark=2 stamp=2000 frame=99 lastTickAgeMs=5.0 simRunActive=False
[Message:Perf Probe] [13:00:01.010] DEVDIAG spike: tick:gap ms=800.0 start=13:00:00.210 end=13:00:01.010 thread=1 source=onTick tickBefore=10 tickAfter=11 gapMs=800.0 haltedMs=0.0 afterHalt=False haltKind=none expectedTickMs=18.2 runCallsDuring=44 nativeCallsDuring=44 skipRunsDuring=0 noBufferDuring=0 simBusyMs=200.0 simBusyShare=0.25 framesDuring=130 mapTicksDuring=0 lowFpsLevel=1 emittedAfterMs=10.0 timerProbe=True timerCallsDuring=1 timerMaxIntervalMs=780.0 expectedTimerMs=18.2 framesToProcessMax=1 framesToProcessAtEnd=0 threadPoolBusyWorkersAtEmit=2 threadPoolBusyIoAtEmit=0 freeze=true event=2 startStamp=2200 endStamp=3000
[Message:Perf Probe] [13:00:01.020] DEVDIAG: spikeSample event=2 name=tick:gap thread=sim samples=200 maxSampleGapMs=2 self: CDE:0x41D60=150(75.0%)
[Message:Perf Probe] [13:00:04.305] DEVDIAG spike: tick:gap ms=300.0 start=13:00:04.005 end=13:00:04.305 thread=1 source=onTick tickBefore=11 tickAfter=12 gapMs=2300.0 haltedMs=2000.0 afterHalt=True haltKind=paused expectedTickMs=18.2 runCallsDuring=0 nativeCallsDuring=0 skipRunsDuring=0 noBufferDuring=0 simBusyMs=0.0 simBusyShare=0.00 framesDuring=400 mapTicksDuring=0 lowFpsLevel=1 emittedAfterMs=5.0 timerProbe=True timerCallsDuring=16 timerMaxIntervalMs=19.0 expectedTimerMs=18.2 framesToProcessMax=16 framesToProcessAtEnd=15 threadPoolBusyWorkersAtEmit=0 threadPoolBusyIoAtEmit=0 event=3 startStamp=6000 endStamp=6300
[Message:Perf Probe] [13:00:05.000] DEVDIAG: tickCounters windowS=5.0 subscribed=True ticks=200 ticksPerS=40.0 gapsOverThreshold=1 maxActiveGapMs=800.0 haltedGapsExcluded=2 mapTicks=150 mapTicksAvailable=True nativeRunsCounted=250 runsWithoutTick=50 runsMultiTick=0 maxTicksPerRun=1 runsWithoutMapTick=60 gapQueueDropped=0 lastTick=12
[Message:Perf Probe] [13:00:10.000] DEVDIAG: tickCounters windowS=5.0 subscribed=True ticks=200 ticksPerS=40.0 gapsOverThreshold=1 maxActiveGapMs=300.0 haltedGapsExcluded=0 mapTicks=200 mapTicksAvailable=True nativeRunsCounted=200 runsWithoutTick=0 runsMultiTick=3 maxTicksPerRun=2 runsWithoutMapTick=0 gapQueueDropped=0 lastTick=400
'@ -split '\r?\n'
$r=Get-PerfProbeAnalysis -Lines $tickLines
Assert ($r.Groups.Count -eq 2 -and $r.Groups[0].Kind -eq 'game-time-stall' -and $r.Groups[0].GameTimeStall -and -not $r.Groups[0].FrameLagRecorded) 'Game-time stall with smooth frames not classified.'
Assert ([Math]::Abs($r.Groups[0].EndMs-[TimeSpan]::Parse('13:00:01').TotalMilliseconds) -lt 0.01) 'Deferred write delay distorted the stamp clock calibration.'
Assert ($r.Groups[0].Samples.Count -eq 1 -and $r.Groups[0].Evidence.Count -ge 1) 'Stall stack slice/evidence not correlated.'
$hints=$r.Groups[0].Hints -join ';'
Assert ($hints -match 'mostly idle' -and $hints -match 'map tick' -and $hints -match 'frames kept rendering') 'Stall hints (idle simulation, no map tick, smooth frames) missing.'
Assert ($r.Groups[1].Phase -eq 'after-pause-or-load' -and ($r.Groups[1].Hints -join ';') -match 'no run call') 'Stall after unpausing not separated or unscheduled simulation not reported.'
# Pacing separates a starved Vanilla timer (no frames requested) from a sim thread held before run.
Assert ($hints -match 'timer callbacks were missing' -and $hints -notmatch 'held before run') 'Missing timer callbacks not identified.'
$held=$r.Groups[1].Hints -join ';'
Assert ($held -match 'held before run' -and $held -notmatch 'timer callbacks were missing' -and ($r.Groups[1].Evidence -join ';') -match 'framesToProcessMax=16') 'Requested but unrun engine frames not identified.'
$t=$r.Summary.TickCounters
Assert ($t.windows -eq 2 -and $t.ticks -eq 400 -and $t.gapsOverThreshold -eq 2 -and $t.maxActiveGapMs -eq 800 -and $t.maxTicksPerRun -eq 2 -and $t.runsWithoutTick -eq 50) 'Tick counters were not summed/maximized per run.'
$text=(Format-PerfProbeSummary -Analysis $r) -join "`n"
Assert ($r.Summary.Kinds.Count -eq 2 -and $text -match 'game-time-stall' -and $text -match 'after-pause-or-load' -and $text -match 'runsWithoutTick=50/450') 'Run summary incomplete.'
Assert ($r.Groups[0].PrecedingInput.Key -eq 'Mouse1' -and [Math]::Abs($r.Groups[0].PrecedingInput.OffsetMs+200) -lt 0.01) 'Latest preceding click not linked to the stall.'
Assert ($r.Summary.InputMarks -eq 2 -and $r.Summary.EventsAfterInput -eq 2 -and $text -match 'inputMarks=2') 'Input marker summary incomplete.'
$r=Get-PerfProbeAnalysis -Lines @($tickLines|Where-Object{$_ -notmatch 'inputMark key=Mouse1'})
Assert ($r.Groups[1].PrecedingInput -eq $null -and $r.Groups[0].PrecedingInput.Key -eq 'ScrollLock') 'Markers older than 5 s or the latest earlier marker were mislinked.'
'PASS: game-time stalls: smooth-frame classification, deferred line clock, resume phase, idle/unscheduled hints, Vanilla pacing (timer starvation vs. held sim thread), input markers and per-run tick summary.'
