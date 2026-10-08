param([string]$Path = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log')
$ErrorActionPreference = 'Stop'
$lines = @(Get-Content -LiteralPath $Path)
$start = -1
for ($index = 0; $index -lt $lines.Count; $index++) {
    if ($lines[$index] -match '^\[Message:\s+BepInEx\]\s+BepInEx\b.* - Stronghold Crusader Definitive Edition') { $start = $index }
}
if ($start -lt 0) { throw 'No version-independent BepInEx startup marker found.' }
$session = @($lines[$start..($lines.Count - 1)])
$errors = @($session | Where-Object { $_ -match '^\[Error\s*:' })
$fatals = @($session | Where-Object { $_ -match '^\[Fatal\s*:' })
$hosts = @('BugfixesAndQoLFormationButtonHost','BugfixesAndQoLFormationMenuHost','BugfixesAndQoLFormationRolloverHost')
$linked = @($hosts | Where-Object { $hostName = $_; @($session | Where-Object { $_.Contains('ViewModel Linked [' + $hostName + ']') }).Count -gt 0 })
[pscustomobject]@{
    LogLastWrite = (Get-Item -LiteralPath $Path).LastWriteTime
    SessionLines = $session.Count
    Errors = $errors.Count
    Fatals = $fatals.Count
    LinkedFormationHosts = $linked.Count
    AppliedOrders = @($session | Where-Object { $_.Contains('FORMATION_ORDER_APPLIED:') }).Count
    ConsumedReleases = @($session | Where-Object { $_.Contains('FORMATION_RELEASE_CONSUMED:') }).Count
} | Format-List
$session | Where-Object { $_ -match 'cadence-resolver|CADENCE_RESOLVER_UNAVAILABLE|Unit command shared hooks installed|Formation active|Shift.*installed|FORMATION_BINDING_FAILED|SHIFT_QUEUE_UNAVAILABLE|ViewModel Linked \[BugfixesAndQoLFormation|FORMATION_ORDER_APPLIED:|FORMATION_RELEASE_CONSUMED:' }
$errors
$fatals
