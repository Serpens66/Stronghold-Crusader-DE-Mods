# Prints what changed between two snapshots from Snapshot-Workshop.ps1.
param([Parameter(Mandatory)][string]$Before, [Parameter(Mandatory)][string]$After)
$ErrorActionPreference = 'Stop'
$a = [IO.File]::ReadAllText($Before) | ConvertFrom-Json
$b = [IO.File]::ReadAllText($After) | ConvertFrom-Json
Write-Output "Before: $($a.TimeLocal) $($a.Label) steam=$($a.SteamRunning) game=$($a.GameRunning)"
Write-Output "After:  $($b.TimeLocal) $($b.Label) steam=$($b.SteamRunning) game=$($b.GameRunning)"

function Get-Fields($Object, [string]$Prefix) {
    $result = [ordered]@{}
    if ($null -eq $Object) { return $result }
    foreach ($property in $Object.PSObject.Properties) {
        if ($property.Value -is [string] -or $null -eq $property.Value) { $result[$Prefix + $property.Name] = [string]$property.Value }
        else { foreach ($entry in (Get-Fields $property.Value ($Prefix + $property.Name + '.')).GetEnumerator()) { $result[$entry.Key] = $entry.Value } }
    }
    return $result
}
$fa = Get-Fields $a.Acf 'acf.'; $fb = Get-Fields $b.Acf 'acf.'
foreach ($key in @($fa.Keys + $fb.Keys | Sort-Object -Unique)) {
    if ($fa[$key] -cne $fb[$key]) { Write-Output ("ACF  {0}: '{1}' -> '{2}'" -f $key, $fa[$key], $fb[$key]) }
}
foreach ($area in @('Content', 'Downloads', 'Temp')) {
    $left = @{}; $right = @{}
    if ($null -eq $a.$area) { Write-Output "$area before: folder missing" } else { foreach ($f in $a.$area) { $left[$f.Path] = $f } }
    if ($null -eq $b.$area) { Write-Output "$area after: folder missing" } else { foreach ($f in $b.$area) { $right[$f.Path] = $f } }
    foreach ($path in @($left.Keys + $right.Keys | Sort-Object -Unique)) {
        if (-not $right.ContainsKey($path)) { Write-Output "$area removed: $path" }
        elseif (-not $left.ContainsKey($path)) { Write-Output "$area added:   $path" }
        elseif ($left[$path].Sha256 -ne $right[$path].Sha256) { Write-Output "$area changed: $path" }
        elseif ($left[$path].LastWriteUtc -ne $right[$path].LastWriteUtc) { Write-Output "$area touched: $path (same content, new time)" }
    }
}
