[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$gameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$extenderDir = Join-Path $gameDir 'BepInEx\plugins\000shcdese'
$assembly = Join-Path $PSScriptRoot 'BepInEx\plugins\SurrenderDesyncDiagnostic_Serp\SurrenderDesyncDiagnostic.dll'
if (-not (Test-Path -LiteralPath $assembly)) { throw "Built diagnostic assembly missing: $assembly" }

foreach ($dependency in @(
    (Join-Path $gameDir 'BepInEx\core\BepInEx.dll'),
    (Join-Path $extenderDir 'System.Memory.dll'),
    (Join-Path $extenderDir 'SHCDESE.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\APIShared_Serp\APIShared.dll'),
    (Join-Path $gameDir 'BepInEx\plugins\BugfixesAndQoL_Serp\BugfixesAndQoL.dll'))) {
    [void][Reflection.Assembly]::LoadFrom($dependency)
}
$extender = [Reflection.Assembly]::LoadFrom((Join-Path $extenderDir 'SHCDESE.dll'))
$diagnostic = [Reflection.Assembly]::LoadFrom($assembly)
$runtime = $diagnostic.GetType('SurrenderDesyncDiagnostic.SurrenderDesyncDiagnosticRuntime', $true)
$project = $runtime.GetMethod('Fields', [Reflection.BindingFlags]'NonPublic, Static')
if ($null -eq $project) { throw 'Pointer projection method missing.' }

function Assert-Projection {
    param([string]$TypeName, [int]$Size, [hashtable]$Fields)
    $record = $extender.GetType("SHCDESE.Interop.$TypeName", $true)
    $actualSize = [Runtime.InteropServices.Marshal]::SizeOf([Type]$record)
    if ($actualSize -ne $Size) { throw "$TypeName size $actualSize instead of $Size" }
    $pointer = [Runtime.InteropServices.Marshal]::AllocHGlobal($actualSize)
    try {
        [Runtime.InteropServices.Marshal]::Copy([byte[]]::new($actualSize), 0, $pointer, $actualSize)
        foreach ($name in $Fields.Keys) {
            $offset = [Runtime.InteropServices.Marshal]::OffsetOf($record, $name).ToInt32()
            if ($offset -ne $Fields[$name].Offset) { throw "$TypeName.$name offset $offset" }
            [Runtime.InteropServices.Marshal]::WriteInt32($pointer, $offset, $Fields[$name].Value)
        }
        $result = [string]$project.Invoke($null, @($record, $pointer))
        foreach ($name in $Fields.Keys) {
            $expected = "$name=$($Fields[$name].Value),"
            if (-not $result.Contains($expected)) { throw "$TypeName missing $expected in projection" }
        }
        Write-Output "$TypeName projection passed: size=$actualSize, fields=$($Fields.Count)"
    }
    finally { [Runtime.InteropServices.Marshal]::FreeHGlobal($pointer) }
}

Assert-Projection 'GamePlayerResources' 22588 @{
    r_LordUnitId = @{ Offset = 0x21F8; Value = 12345 }
    r_IsPaused = @{ Offset = 0x2210; Value = 1 }
    r_WinLossState = @{ Offset = 0x2244; Value = 2 }
}
Assert-Projection 'GameUnit' 0x490 @{
    r_GlobalId = @{ Offset = 0x94; Value = 76543 }
    r_CurrentHealthPercentage = @{ Offset = 0x2D0; Value = 73 }
}
Assert-Projection 'GameBuilding' 0x32C @{
    r_GlobalId = @{ Offset = 216; Value = 54321 }
}
Assert-Projection 'GameTribe' 0x688 @{
    r_GlobalId = @{ Offset = 10; Value = 23456 }
}
Assert-Projection 'GameProjectile' 0xE8 @{
    r_GlobalId = @{ Offset = 68; Value = 34567 }
}
Assert-Projection 'GameVegetation' 0x9C @{
    r_GlobalId = @{ Offset = 88; Value = 45678 }
}
Assert-Projection 'PathConnectionRecord' 0x204 @{
    r_RecordGlobalId = @{ Offset = 8; Value = 56789 }
}
