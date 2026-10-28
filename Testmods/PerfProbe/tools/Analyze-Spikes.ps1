# Read-only offline analysis. No game/config changes.
param(
    [string]$LogPath='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log',
    [int]$FreezeMs=100,
    [int]$Top=25
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot SpikeAnalysis.ps1)
$stream=[IO.FileStream]::new($LogPath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
try{$reader=[IO.StreamReader]::new($stream);$lines=$reader.ReadToEnd() -split '\r?\n'}finally{$stream.Dispose()}
$result=Get-PerfProbeAnalysis -Lines $lines -ThresholdMs $FreezeMs
Write-Output "Log: $LogPath; last start line=$($result.LastStartLine); spike lines=$($result.Spikes.Count); merged slow events=$($result.Groups.Count)"
Format-PerfProbeSummary -Analysis $result
Write-Output 'Nested spans are inclusive: do not add them. Missing measurements are unknown.'
foreach($context in $result.Context|Where-Object{$_ -match 'runContext|runPlugin|patchOwners|tickMonitor '}|Select-Object -First 50){Write-Output $context}
foreach($group in $result.Groups|Select-Object -First $Top){
    $start=[TimeSpan]::FromMilliseconds($group.StartMs);$end=[TimeSpan]::FromMilliseconds($group.EndMs)
    Write-Output ('{0:hh\:mm\:ss\.fff}-{1:hh\:mm\:ss\.fff} spanMs={2:F1} phase={3} kind={4}' -f $start,$end,($group.EndMs-$group.StartMs),$group.Phase,$group.Kind)
    if($group.PrecedingInput){Write-Output ('    INPUT: {0} mark={1} at {2:F0} ms relative to the event start (time reference, not proof of cause)' -f $group.PrecedingInput.Key,$group.PrecedingInput.Mark,$group.PrecedingInput.OffsetMs)}
    foreach($member in $group.Members){Write-Output ('    {0} inclusiveMs={1:F1} line={2} {3}' -f $member.Name,$member.Ms,$member.Line,$member.Detail)}
    foreach($evidence in $group.Evidence){Write-Output "    EVIDENCE: $evidence"}
    foreach($hint in $group.Hints){Write-Output "    LIMIT: $hint"}
    foreach($stub in $group.NativeStubSamples){Write-Output "    STUB record=$($stub.Record) samples=$($stub.Samples) ip=$($stub.IP) resolution=$($stub.Resolution) name=$($stub.Stub) target=$($stub.Target) (includes displaced original instructions)"}
    foreach($detail in @($group.LockHistory)+@($group.Samples)+@($group.Analysis)){Write-Output "    $detail"}
}
if($result.Groups.Count -gt $Top){Write-Output "Further events: $($result.Groups.Count-$Top) (-Top)."}
