$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dll = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\SHCDESE.dll'
$assembly = [Reflection.Assembly]::LoadFrom($dll)
$unit = $assembly.GetType('SHCDESE.Interop.GameUnit', $true)
if ($unit.StructLayoutAttribute.Size -ne 0x490) { throw 'GameUnit stride differs' }
$expected = @(
    @('r_AliveState',0x88,'AliveState'),
    @('r_UnitChimp',0x8A,'eChimps'),
    @('r_ControllableForPlayerId',0x92,'UInt16'),
    @('r_GlobalId',0x94,'UInt32'),
    @('r_IsKilledByProjectile',0x29C,'UInt16'),
    @('r_WorldDistanceToNearestEnemy',0x2A2,'UInt16')
)
$rows = @(foreach ($item in $expected) {
    $field = $unit.GetField($item[0])
    $offset = [Runtime.InteropServices.Marshal]::OffsetOf($unit,$item[0]).ToInt32()
    if (-not $field.IsPublic -or $field.FieldType.Name -ne $item[2] -or $offset -ne $item[1]) { throw "Mismatch: $($item[0])" }
    [pscustomobject]@{Name=$item[0]; Offset=$offset; Type=$field.FieldType.FullName; Public=$field.IsPublic}
})
$manager = $assembly.GetType('SHCDESE.Interop.GameUnitManager',$true)
if ([Runtime.InteropServices.Marshal]::OffsetOf($manager,'GameUnitArray').ToInt32() -ne 0x65C) { throw 'Manager array origin differs' }
$result = [ordered]@{AssemblySha256=(Get-FileHash -LiteralPath $dll).Hash; Version=$assembly.GetName().Version.ToString(); UnitSize=$unit.StructLayoutAttribute.Size; ManagerArrayOffset=0x65C; Fields=$rows}
$text = ($result | ConvertTo-Json -Depth 8) + [Environment]::NewLine
[IO.File]::WriteAllText((Join-Path $root 'installed-layout.json'),$text,[Text.UTF8Encoding]::new($false))
'PASS: installed layout, public signatures and manager origin'
