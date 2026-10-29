[CmdletBinding()]
param([Parameter(Mandatory)][string]$LogPath, [int]$Top = 6)
# Summary of one automated pathfinding run (saved LogOutput.log): tick-gap distribution after the
# map start, sampler attribution of the largest gaps (innermost mod frame and self frame), totals
# of the MoatMove anchor windows and the PerfProbe frame statistics. Development tooling only.
$ErrorActionPreference = 'Stop'
$lines = [IO.File]::ReadAllLines((Resolve-Path -LiteralPath $LogPath).ProviderPath)
$start = 0
for ($i = 0; $i -lt $lines.Length; $i++) { if ($lines[$i] -like '*MoatMove map-start*') { $start = $i; break } }
$run = $lines[$start..($lines.Length - 1)]

$gaps = foreach ($line in $run) {
    if ($line -match 'DEVDIAG spike: tick:gap ms=([\d.]+) start=([\d:.]+)') { [pscustomobject]@{ Ms = [double]$matches[1]; At = $matches[2] } }
}
$ticks = ($run | Where-Object { $_ -match 'tickCounters' } | ForEach-Object { if ($_ -match ' ticks=(\d+)') { [int]$matches[1] } } | Measure-Object -Sum).Sum
"ticks=$ticks gaps=$(@($gaps).Count) " + (@(
    @{ n = '100-150'; lo = 0; hi = 150 }, @{ n = '150-200'; lo = 150; hi = 200 }, @{ n = '200-300'; lo = 200; hi = 300 },
    @{ n = '300-500'; lo = 300; hi = 500 }, @{ n = '>=500'; lo = 500; hi = 1e9 }) | ForEach-Object {
        $b = $_; "$($b.n):" + @($gaps | Where-Object { $_.Ms -ge $b.lo -and $_.Ms -lt $b.hi }).Count }) -join ' '

function Get-Attribution([string[]]$sampleLines, [string]$kind, [int]$count) {
    $h = @{}; $total = 0
    foreach ($x in $sampleLines) {
        if ($x -match ' samples=(\d+)') { $total += [int]$matches[1] }
        if ($x -match (' ' + $kind + ': (.*)$')) { foreach ($p in ($matches[1] -split ' \| ')) { if ($p -match '^(.*)=(\d+)\(') { $h[$matches[1]] += [int]$matches[2] } } }
    }
    "    samples=$total"
    $h.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First $count | ForEach-Object { '    {0,6} {1}' -f $_.Value, $_.Name }
}
foreach ($gap in ($gaps | Sort-Object Ms -Descending | Select-Object -First $Top)) {
    $duration = [math]::Floor($gap.Ms)
    $samples = $run | Where-Object { $_ -match 'spikeSample' -and $_ -match 'name=tick:gap thread=sim' -and $_ -match "durationMs=$duration" }
    "  gap $($gap.At) $($gap.Ms) ms"
    Get-Attribution ($samples | Where-Object { $_ -match ' inner: ' }) 'inner' 6
}
"  all gaps (inner):"
Get-Attribution ($run | Where-Object { $_ -match 'spikeSample' -and $_ -match 'name=tick:gap thread=sim' -and $_ -match ' inner: ' }) 'inner' 10

$sums = @{}
foreach ($line in ($run | Where-Object { $_ -like '*:  MoatMove] *' -and $_ -match 'stage=anchor-(breakdown|performance)' })) {
    foreach ($m in [regex]::Matches($line, '[ (](\w+)=([\d,]+)(?=[ ),.])')) {
        $key = $m.Groups[1].Value; $value = [double]($m.Groups[2].Value -replace ',', '.')
        if ($key -like 'max*') { if ($value -gt $sums[$key]) { $sums[$key] = $value } } else { $sums[$key] += $value }
    }
}
'  anchor: ' + (('moveMs', 'maxMoveMs', 'queryMs', 'publishMs', 'fieldMs', 'tickMs', 'refreshMs', 'buildMs', 'sweepMs', 'sweepNodes', 'resweeps', 'accessChanges',
    'gateRefreshes', 'sharedReuses', 'hits', 'units', 'lazyTasks', 'aiMoves', 'humanMoves', 'encodeRejects', 'auditSkips', 'subAnchors', 'subMs', 'vanillaCalls', 'vanillaFails', 'vanillaMs', 'vanillaFailMs', 'afterUnreachable', 'afterUnreachableMs', 'foundAfterUnreachable') |
    ForEach-Object { "$_=" + [math]::Round([double]$sums[$_], 1) }) -join ' '
$run | Where-Object { $_ -match 'AUTOMATION .*(framestats phase=run|result)' } | ForEach-Object { '  ' + ($_ -replace '^.*AUTOMATION ', '') }
