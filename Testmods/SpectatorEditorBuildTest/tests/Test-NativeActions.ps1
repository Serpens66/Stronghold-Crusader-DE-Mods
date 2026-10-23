$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$bytes = [IO.File]::ReadAllBytes((Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'))
$sha = [Security.Cryptography.SHA256]::Create()
try { $hash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','') } finally { $sha.Dispose() }
if ($hash -ne 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2') { throw 'Unknown native action fixture.' }
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $game 'BepInEx\plugins\000shcdese\Iced.dll')))
$pe = [BitConverter]::ToInt32($bytes,60)
$table = $pe+24+[BitConverter]::ToUInt16($bytes,$pe+20)
$sections = [BitConverter]::ToUInt16($bytes,$pe+6)
foreach ($entry in @(@(0xCD630,512,@(0xDD6C0,0xE1E70,0x1264D0,0x7530)),@(0xD5810,96,@(0xC5300)),@(0xD57B0,64,@(0xC5300)))) {
    $rva = $entry[0]; $length = $entry[1]; $body = New-Object byte[] $length; $found = $false
    for ($i=0; $i -lt $sections; $i++) {
        $header = $table+40*$i; $start = [BitConverter]::ToInt32($bytes,$header+12); $size = [BitConverter]::ToInt32($bytes,$header+16)
        if ($rva -ge $start -and $rva+$length -le $start+$size -and ([BitConverter]::ToUInt32($bytes,$header+36) -band 0x20000000)) {
            [Array]::Copy($bytes, [BitConverter]::ToInt32($bytes,$header+20)+$rva-$start, $body, 0, $length); $found=$true
        }
    }
    if (-not $found) { throw 'Action outside executable section.' }
    $decoder = [Iced.Intel.Decoder]::Create(64,[Iced.Intel.ByteArrayCodeReader]::new($body)); $decoder.IP=$rva
    $decoded=0; $calls=@()
    while ($decoded -lt $length) {
        $instruction=$decoder.Decode()
        if ($instruction.IsInvalid) { throw 'Invalid action instruction.' }
        if ($instruction.FlowControl.ToString() -in @('Call','UnconditionalBranch')) { $calls += $instruction.NearBranchTarget }
        $decoded += $instruction.Length
    }
    if ($decoded -ne $length) { throw 'Action ends inside an instruction.' }
    foreach ($expected in $entry[2]) { if ($calls -notcontains $expected) { throw ('Missing action dependency 0x{0:X}' -f $expected) } }
}
Write-Host 'PASS: complete power/gate/bridge instruction fixtures and effect/coupling call targets.'
