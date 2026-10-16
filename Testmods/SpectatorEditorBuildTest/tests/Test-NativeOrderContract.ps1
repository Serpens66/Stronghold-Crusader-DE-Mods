$ErrorActionPreference = 'Stop'
$gameDirectory = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$bytes = [IO.File]::ReadAllBytes((Join-Path $gameDirectory 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'))
$sha = [Security.Cryptography.SHA256]::Create()
try { $hash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','') } finally { $sha.Dispose() }
if ($hash -ne 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2') { throw 'Native order fixture has a different hash.' }
$pe = [BitConverter]::ToInt32($bytes,60)
$table = $pe + 24 + [BitConverter]::ToUInt16($bytes,$pe+20)
$sections = [BitConverter]::ToUInt16($bytes,$pe+6)
$body = New-Object byte[] 587
$found = $false
for ($index=0; $index -lt $sections; $index++) {
    $header = $table + 40*$index
    $rva = [BitConverter]::ToInt32($bytes,$header+12)
    $length = [BitConverter]::ToInt32($bytes,$header+16)
    if (0xD7580 -ge $rva -and 0xD7580+587 -le $rva+$length) {
        $offset = [BitConverter]::ToInt32($bytes,$header+20) + 0xD7580 - $rva
        [Array]::Copy($bytes,$offset,$body,0,$body.Length)
        $found = $true
    }
}
if (-not $found) { throw 'Order function is outside the PE sections.' }
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $gameDirectory 'BepInEx\plugins\000shcdese\Iced.dll')))
$reader = [Iced.Intel.ByteArrayCodeReader]::new($body)
$decoder = [Iced.Intel.Decoder]::Create(64,$reader)
$decoder.IP = 0xD7580
$decoded = 0
$calls = @()
while ($decoded -lt $body.Length) {
    $instruction = $decoder.Decode()
    if ($instruction.IsInvalid -or $decoder.LastError.ToString() -ne 'None') { throw "Invalid native instruction at $decoded" }
    if ($instruction.FlowControl.ToString() -eq 'Call') { $calls += $instruction.NearBranchTarget }
    $decoded += $instruction.Length
}
if ($decoded -ne 587) { throw 'Native function boundary does not end on an instruction.' }
foreach ($target in @(0x2A500,0x41500,0x2A580)) { if ($calls -notcontains $target) { throw ('Missing native AI order dependency: 0x{0:X}' -f $target) } }
Write-Host 'PASS: full 587-byte native order function decodes; defend/attack acceptance and attack preparation calls match.'
