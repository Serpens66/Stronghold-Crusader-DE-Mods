param([string]$GameDir, [string]$ExtenderDir)
$ErrorActionPreference = 'Stop'
$apiRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (-not $GameDir) { $GameDir = $env:SHCDE_GAME_DIR }
if (-not $GameDir) { throw 'Set SHCDE_GAME_DIR or supply -GameDir for installed interop verification.' }
if (-not $ExtenderDir) { $ExtenderDir = Join-Path $GameDir 'BepInEx\plugins\000shcdese' }
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')))
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $ExtenderDir 'SHCDESE.dll'))
function Get-NativeSize($type) {
    switch ($type.FullName) {
        'System.Byte' { return 1 } 'System.SByte' { return 1 }
        'System.UInt16' { return 2 } 'System.Int16' { return 2 }
        'System.UInt32' { return 4 } 'System.Int32' { return 4 }
        'System.UInt64' { return 8 } 'System.Int64' { return 8 }
        default {
            $definition = $assembly.MainModule.GetType($type.FullName.Replace('/', '+'))
            if ($null -eq $definition) { $definition = $type.Resolve() }
            if ($definition.IsEnum) { return Get-NativeSize ($definition.Fields | Where-Object Name -eq 'value__').FieldType }
            if ($definition.ClassSize -gt 0) { return $definition.ClassSize }
            throw "Unknown native field size: $type"
        }
    }
}
$source = [IO.File]::ReadAllText((Join-Path $apiRoot 'src\UnitCommands\FormationRuntime.cs')) + [IO.File]::ReadAllText((Join-Path $apiRoot 'src\Units\UnitAccess.cs'))
$consumed = @([regex]::Matches($source, '\br_\w+') | ForEach-Object Value | Sort-Object -Unique)
$rows = @()
$expected = @{
    'GameUnit.r_AliveState' = 'SHCDESE.Interop.Enums.AliveState|136|2'
    'GameUnit.r_UnitChimp' = 'SHCDESE.Interop.eChimps|138|2'
    'GameUnit.r_GlobalId' = 'System.UInt32|148|4'
    'GameUnit.r_CurrentTilePositionX' = 'System.UInt16|192|2'
    'GameUnit.r_CurrentTilePositionY' = 'System.UInt16|194|2'
    'GameUnit.r_TargetTilePositionX' = 'System.UInt16|196|2'
    'GameUnit.r_TargetTilePositionY' = 'System.UInt16|198|2'
    'GameUnit.r_IsKilledByProjectile' = 'System.UInt16|668|2'
    'GameUnit.r_TribeId' = 'System.UInt16|724|2'
    'GameUnit.r_AttackMoveToTargetTileX' = 'System.UInt16|728|2'
    'GameUnit.r_AttackMoveToTargetTileY' = 'System.UInt16|730|2'
    'GameTribe.r_PlayerIdOwner' = 'System.UInt16|2|2'
    'GameTribe.r_GlobalId' = 'System.UInt32|10|4'
    'GameTribe.r_AliveState' = 'SHCDESE.Interop.Enums.AliveState|22|2'
    'GameCursorManager.r_IsCursorInGame' = 'System.UInt32|8|4'
    'GameCursorManager.r_HoverOverBuildingId' = 'System.UInt32|36|4'
    'GameCursorManager.r_HoverOverUnitId' = 'System.UInt32|48|4'
    'GameCursorManager.r_MouseTileX' = 'System.UInt32|108|4'
    'GameCursorManager.r_MouseTileY' = 'System.UInt32|112|4'
    'GameCursorManager.r_HoveringOverWall' = 'System.UInt32|116|4'
}
$found = @()
foreach ($name in @('GameUnit','GameTribe','GameCursorManager')) {
    $type = $assembly.MainModule.GetType('SHCDESE.Interop.' + $name)
    if ($null -eq $type -or -not $type.IsSequentialLayout -or $type.PackingSize -ne 1) { throw "Unexpected layout: $name" }
    $offset = 0
    foreach ($field in $type.Fields | Where-Object { -not $_.IsStatic }) {
        $size = Get-NativeSize $field.FieldType
        if ($consumed -contains $field.Name) {
            if (-not $field.IsPublic) { throw "Private interop field $name.$($field.Name)" }
            $key = "$name.$($field.Name)"
            $actual = "$($field.FieldType.FullName)|$offset|$size"
            if ($expected[$key] -cne $actual) { throw "Changed or unaudited field $key : $actual" }
            $found += $field.Name
            $rows += "$name.$($field.Name) type=$($field.FieldType.FullName) offset=0x$($offset.ToString('X')) width=$size stride=$($type.ClassSize)"
        }
        $offset += $size
    }
    if ($type.ClassSize -gt 0 -and $offset -gt $type.ClassSize) { throw "Fields exceed stride: $name $offset" }
    $requiredSize = @{ GameUnit = 1168; GameTribe = 1672; GameCursorManager = 1152 }[$name]
    if ($offset -ne $requiredSize) { throw "Changed structure size: $name $offset" }
    $rows += "$name computedSize=$offset declaredSize=$($type.ClassSize) pack=$($type.PackingSize)"
}
$rows | ForEach-Object { Write-Output $_ }
foreach ($fieldName in $consumed) {
    if ($found -notcontains $fieldName) { throw "Unresolved consumed interop member: $fieldName" }
}
$unit = $assembly.MainModule.GetType('SHCDESE.Interop.GameUnit')
if ($unit.ClassSize -ne 0x490 -or $assembly.MainModule.GetType('SHCDESE.Interop.GameTribe').ClassSize -ne 0x688) { throw 'Native unit/tribe stride changed' }
$assembly.Dispose()
Write-Output 'Installed interop fields, widths and sequential offsets audited.'
