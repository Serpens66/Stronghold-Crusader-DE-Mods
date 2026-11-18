# Archives the probe-relevant lines of the last game start and prints a summary.
# Run after each start: LogOutput.log is overwritten by the next start.
param([Parameter(Mandatory)][string]$Label)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Common.ps1')
$log = Join-Path (Get-ProbeGame) 'BepInEx\LogOutput.log'
$lines = [IO.File]::ReadAllLines($log)
$patterns = @(' WLP ', 'WorkshopLoadProbe', 'APIShared.DependencyBootstrap', 'Chainloader startup complete', 'Could not load \[', 'Skipping \[',
    'patch files to Assets/GUI/XAML/FrontendMenus.xaml', 'Mod compatibility check completed')
$relevant = @($lines | Where-Object { $line = $_; @($patterns | Where-Object { $line -match $_ }).Count -gt 0 })
$name = (Get-Date).ToString('yyyyMMdd-HHmmss') + '-' + ($Label -replace '[^A-Za-z0-9_-]', '-') + '.log'
$path = Join-Path (Get-ProbeEvidence 'runs') $name
Write-ProbeText $path $relevant
Write-Output "Archived $($relevant.Count) lines: $path"

function Show([string]$Marker) {
    $found = @($relevant | Where-Object { $_ -match (' WLP ' + $Marker + '( |$)') })
    if ($found.Count -eq 0) { Write-Output ("  {0,-16} -" -f $Marker) }
    else { foreach ($line in $found) { Write-Output ("  {0,-16} {1}" -f $Marker, ($line -replace '^.* WLP \S+ ?', '')) } }
}
Write-Output 'Preloader:'
foreach ($marker in @('INACTIVE', 'CONFIG', 'BEPINEX', 'HOOK_INSTALLED', 'HOOK_REFUSED', 'HOOK_SKIPPED', 'WORKSHOP_DIR', 'LOCAL', 'HASH_CACHE', 'INVENTORY', 'SCAN', 'DECISION', 'RESOLVER', 'RESOLVE', 'DISCOVERY_DONE', 'ERROR')) { Show $marker }
Write-Output 'Runtime:'
foreach ($marker in @('PAYLOAD_AWAKE', 'PAYLOAD_HELPER', 'PAYLOAD_HELPER_FAILED', 'OBSERVER_AWAKE', 'ORDER', 'T4_SKIPPED', 'T4_REGISTER', 'T4_MODHASH', 'T4_PATCHES', 'T4_LOCALES', 'T4_LUA', 'T4_MODLIST', 'MAP_START', 'LUA_INIT', 'LUA_LOAD', 'OBSERVER_ERROR')) { Show $marker }
$acf = @($relevant | Where-Object { $_ -match ' WLP ACF_ITEM ' }).Count
Write-Output "  ACF_ITEM lines   $acf"
Write-Output 'BepInEx/extender:'
$relevant | Where-Object { $_ -match 'Could not load \[|Skipping \[|FrontendMenus.xaml|Mod compatibility check completed|DependencyBootstrap' } | ForEach-Object { Write-Output "  $_" }
