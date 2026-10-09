[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$extender = Join-Path $game 'BepInEx/plugins/000shcdese'
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $extender 'SHCDESE.dll'))
# Layout, not a version equality, is the runtime contract. Newer compatible versions remain allowed.
$contracts = @(
    @('GameUnit',1168,'r_ControllableForPlayerId','System.UInt16',0x92),
    @('GameUnit',1168,'r_GlobalId','System.UInt32',0x94),
    @('GameUnit',1168,'r_IsKilledByProjectile','System.UInt16',0x29C),
    @('GameTribe',1672,'r_GlobalId','System.UInt32',0xA),
    @('GameTribe',1672,'r_LeaderUnitId','System.UInt16',0x30),
    @('GameBuilding',812,'r_AIWalkableState','SHCDESE.Interop.Enums.GatePathOverrideMode',0xD4),
    @('GameBuilding',812,'r_GlobalId','System.UInt32',0xD8),
    @('GameBuilding',812,'r_GateState','System.Byte',0x2A2),
    @('GameBuilding',812,'r_CapturedByPlayerId','System.UInt16',0x2C6),
    @('GameBuilding',812,'r_GatehouseId','System.UInt16',0x2D2)
)
foreach ($contract in $contracts) {
    $type = $assembly.GetType('SHCDESE.Interop.'+$contract[0],$true)
    $field = $type.GetField($contract[2])
    if ($null -eq $field -or -not $field.IsPublic -or $field.FieldType.FullName -cne $contract[3] -or
        $type.StructLayoutAttribute.Size -ne $contract[1] -or
        [Runtime.InteropServices.Marshal]::OffsetOf($type,$contract[2]).ToInt64() -ne $contract[4]) { throw "Installed layout mismatch: $($contract[0]).$($contract[2])" }
}
$source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/EnemyGatePathfindingNativeDefinition.cs'))
function Constant([string]$name) {
    $match = [regex]::Match($source,'\b'+[regex]::Escape($name)+'\s*=\s*(0x[0-9A-Fa-f]+|[0-9]+)\s*;')
    if (-not $match.Success) { throw "Missing native span constant $name" }
    $value = $match.Groups[1].Value
    if ($value.StartsWith('0x')) { return [Convert]::ToInt32($value.Substring(2),16) }
    return [int]$value
}
function ArrayValues([string]$name) {
    $match = [regex]::Match($source,'\b'+[regex]::Escape($name)+'\s*=\s*\{([^}]+)\}',[Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) { throw "Missing native span array $name" }
    return @([regex]::Matches($match.Groups[1].Value,'0x[0-9A-Fa-f]+|\b[0-9]+\b') | ForEach-Object {
        if ($_.Value.StartsWith('0x')) { [Convert]::ToInt32($_.Value.Substring(2),16) } else { [int]$_.Value }
    })
}
$spans = @(@((Constant 'PclGraphCapturedByFilterRva'),(Constant 'PclGraphCapturedByFilterHookLength')),
    @((Constant 'BuilderPrecheckCapturedByFilterRva'),(Constant 'BuilderPrecheckCapturedByFilterHookLength')),
    @((Constant 'DirectCursorSearchBlockRva'),(Constant 'DirectCursorSearchBlockLength')),
    @((Constant 'CursorPclDecisionRva'),(Constant 'CursorPclDecisionLength')))
foreach ($prefix in @('DirectionFilter','AiTacticalFilter')) {
    $starts = ArrayValues ($prefix+'Rvas'); $lengths = ArrayValues ($prefix+'Lengths')
    if ($starts.Count -ne $lengths.Count) { throw 'Span arrays disagree' }
    for ($index=0; $index -lt $starts.Count; $index++) { $spans += ,@($starts[$index],$lengths[$index]) }
}
# The archived 2.14.1 audit includes every actual RedBird displacement, not minimum HookSize.
$sites = Get-Content -Raw -LiteralPath (Join-Path $workspace '_inspect/ScriptExtender2141Audit/native-sites.json') | ConvertFrom-Json
foreach ($site in $sites) {
    foreach ($span in $spans) {
        if ($site.rva -lt $span[0]+$span[1] -and $span[0] -lt $site.rva+$site.length) { throw 'Extender engage-range hook overlaps gate adapter' }
    }
}
Write-Host "PASS: installed public layouts/full control WORD; $($spans.Count) gate adapter spans vs $($sites.Count) actual SE 2.14.1 engage-range spans. Version is audit provenance only."
