# Read-only analysis of DEVDIAG spike lines (PerfProbe + mods using Shared.DevDiag.StopSpike).
# Evaluates only the last game start in the BepInEx log and attributes every freeze
# (simulation tick, main-thread call or frame gap >= FreezeMs) to the spikes inside it.
param(
    [string]$LogPath = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log',
    [int]$FreezeMs = 400,
    [int]$Top = 25
)
$ErrorActionPreference = 'Stop'

# The game keeps the log open for writing; read with shared access.
$stream = New-Object IO.FileStream($LogPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
try {
    $reader = New-Object IO.StreamReader($stream, [Text.Encoding]::UTF8)
    $lines = $reader.ReadToEnd() -split "`r?`n"
}
finally { $stream.Dispose() }
$start = 0
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match '^\[Message:\s+BepInEx\] BepInEx .* - Stronghold Crusader Definitive Edition') { $start = $i }
}

$pattern = 'DEVDIAG spike: (?<name>\S+) ms=(?<ms>[0-9.]+) start=(?<start>[0-9:.]+) end=(?<end>[0-9:.]+) thread=(?<thread>\d+)\s*(?<detail>.*)$'
$culture = [Globalization.CultureInfo]::InvariantCulture
$spikes = New-Object System.Collections.Generic.List[object]
for ($i = $start; $i -lt $lines.Length; $i++) {
    $m = [regex]::Match($lines[$i], $pattern)
    if (-not $m.Success) { continue }
    $spikes.Add([pscustomobject]@{
        Name   = $m.Groups['name'].Value
        Ms     = [double]::Parse($m.Groups['ms'].Value, $culture)
        Start  = [TimeSpan]::ParseExact($m.Groups['start'].Value, 'hh\:mm\:ss\.fff', $culture)
        End    = [TimeSpan]::ParseExact($m.Groups['end'].Value, 'hh\:mm\:ss\.fff', $culture)
        Thread = [int]$m.Groups['thread'].Value
        Detail = $m.Groups['detail'].Value
    })
}
Write-Host ("Log: {0} (last game start at line {1}); spike lines: {2}" -f $LogPath, ($start + 1), $spikes.Count)
if ($spikes.Count -eq 0) { exit 0 }

# Overview per measurement point.
$overview = @($spikes | Group-Object Name | ForEach-Object {
    $values = @($_.Group | ForEach-Object { $_.Ms })
    [pscustomobject]@{
        Name  = $_.Name
        Count = $_.Count
        SumMs = [math]::Round(($values | Measure-Object -Sum).Sum, 1)
        MaxMs = [math]::Round(($values | Measure-Object -Maximum).Maximum, 1)
    }
})
Write-Host ''
Write-Host 'Spike overview (all thresholds as configured):'
$overview | Sort-Object SumMs -Descending | Format-Table -AutoSize | Out-String -Width 200 | Write-Host

# Freezes: PerfProbe outer measurements at or above FreezeMs.
$outerNames = @('sim:EngineInterface.run', 'frame:interval', 'main:Director.Update(total)', 'main:FatControler.NoesisGUIUpdateChecksInGame')
$tolerance = [TimeSpan]::FromMilliseconds(2)
$freezes = @($spikes | Where-Object { $outerNames -contains $_.Name -and $_.Ms -ge $FreezeMs } | Sort-Object Start)
Write-Host ("Freezes >= {0} ms: {1}" -f $FreezeMs, $freezes.Count)
$shown = 0
foreach ($freeze in $freezes) {
    if ($shown -ge $Top) { Write-Host ("... {0} more freezes not shown (-Top)." -f ($freezes.Count - $shown)); break }
    $shown++
    $inner = @($spikes | Where-Object {
        $outerNames -notcontains $_.Name -and $_.Start -ge ($freeze.Start - $tolerance) -and $_.End -le ($freeze.End + $tolerance)
    })
    Write-Host ''
    Write-Host ("{0:hh\:mm\:ss\.fff}-{1:hh\:mm\:ss\.fff}  {2}  {3:F1} ms  {4}" -f $freeze.Start, $freeze.End, $freeze.Name, $freeze.Ms, $freeze.Detail)
    if ($inner.Count -eq 0) { Write-Host '    no mod spike inside (cause outside the measured mod paths)'; continue }
    $groups = @($inner | Group-Object Name | ForEach-Object {
        [pscustomobject]@{ Name = $_.Name; Count = $_.Count; SumMs = ($_.Group | Measure-Object Ms -Sum).Sum; Sample = $_.Group[0].Detail }
    })
    foreach ($group in @($groups | Sort-Object SumMs -Descending)) {
        $share = if ($freeze.Ms -gt 0) { 100.0 * $group.SumMs / $freeze.Ms } else { 0 }
        Write-Host ("    {0,-36} n={1,3} sum={2,8:F1} ms ({3,5:F1}% of freeze)  e.g. {4}" -f $group.Name, $group.Count, $group.SumMs, $share, $group.Sample)
    }
}
