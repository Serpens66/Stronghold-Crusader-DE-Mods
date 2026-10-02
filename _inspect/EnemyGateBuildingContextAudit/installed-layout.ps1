$ErrorActionPreference = 'Stop'
$sePath = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\SHCDESE.dll'
$se = [Reflection.Assembly]::LoadFrom($sePath)
$expected = @(
    @('GameUnit', 'r_ControllableForPlayerId', 0x92, 'Byte'),
    @('GameUnit', 'N00000569', 0x93, 'Byte'),
    @('GameUnit', 'r_GlobalId', 148, 'UInt32'),
    @('GameUnitManager', 'GameUnitArray', 0x65C, 'GameUnit'),
    @('GameTribe', 'r_LeaderUnitId', 0x30, 'UInt16'),
    @('GameTribe', 'r_PlayerIdOwner', 2, 'UInt16'),
    @('GameTribe', 'r_GlobalId', 10, 'UInt32'),
    @('GameTribe', 'N00000580', 0x620, 'UInt32'),
    @('GameTribe', 'r_AttackTargetOwnerPlayerId', 0x61C, 'UInt32'),
    @('GameTribeManager', 'GameTribeArray', 0x2A, 'GameTribe')
)
foreach ($row in $expected) {
    $type = $se.GetType('SHCDESE.Interop.' + $row[0], $true)
    $field = $type.GetField($row[1])
    $offset = [Runtime.InteropServices.Marshal]::OffsetOf($type, $row[1]).ToInt32()
    if (!$field.IsPublic -or $offset -ne $row[2] -or $field.FieldType.Name -ne $row[3]) {
        throw ('Installed layout mismatch: ' + $row[0] + '.' + $row[1])
    }
    '{0}.{1}: public {2}, offset=0x{3:X}' -f $row[0], $row[1], $row[3], $offset
}
foreach ($row in @(@('GameUnit', 0x490), @('GameTribe', 0x688))) {
    $type = $se.GetType('SHCDESE.Interop.' + $row[0], $true)
    if ($type.StructLayoutAttribute.Size -ne $row[1]) { throw ('Size mismatch: ' + $row[0]) }
}
'PASS: 10 installed member contracts and 2 record sizes; SE=' + $se.GetName().Version
(Get-FileHash -LiteralPath $sePath -Algorithm SHA256).Hash
