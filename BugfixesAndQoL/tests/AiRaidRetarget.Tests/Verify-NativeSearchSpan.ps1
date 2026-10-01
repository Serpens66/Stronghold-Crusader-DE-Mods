$ErrorActionPreference = 'Stop'
$extender = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese'
$nativePath = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$image = [IO.File]::ReadAllBytes($nativePath)
$hasher = [Security.Cryptography.SHA256]::Create()
try { $nativeHash = [BitConverter]::ToString($hasher.ComputeHash($image)).Replace('-', '') }
finally { $hasher.Dispose() }
if ($nativeHash -ne 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2') {
    throw 'Native search observation requires the audited native hash.'
}
Add-Type -Path (Join-Path $extender 'Iced.dll')
$pe = [BitConverter]::ToInt32($image, 0x3C)
$sectionCount = [BitConverter]::ToUInt16($image, $pe + 6)
$sectionTable = $pe + 24 + [BitConverter]::ToUInt16($image, $pe + 20)
$bias = $null
for ($section = 0; $section -lt $sectionCount; $section++) {
    $header = $sectionTable + 40 * $section
    $virtual = [BitConverter]::ToInt32($image, $header + 12)
    $raw = [BitConverter]::ToInt32($image, $header + 20)
    $size = [BitConverter]::ToInt32($image, $header + 16)
    if (0x11E960 -ge $virtual -and 0x121D4C -le $virtual + $size) { $bias = $raw - $virtual }
}
if ($null -eq $bias) { throw 'Command code section unavailable.' }
$span = [byte[]]$image[(0x11FFA7 + $bias)..(0x11FFB7 + $bias)]
if ([BitConverter]::ToString($span) -ne 'E8-E4-30-00-00-39-35-F6-89-FA-05-0F-84-68-1B-00-00') {
    throw 'Native observation span differs.'
}
$code = [byte[]]$image[(0x11E960 + $bias)..(0x121D4B + $bias)]
$decoder = [Iced.Intel.Decoder]::Create(64, [Iced.Intel.ByteArrayCodeReader]::new($code))
$decoder.IP = 0x11E960
$siteInstructions = @()
while ($decoder.IP -lt 0x121D4C) {
    $instruction = $decoder.Decode()
    if ($instruction.IsInvalid) { throw 'Command function contains invalid decoding.' }
    if ($instruction.IP -ge 0x11FFA7 -and $instruction.IP -lt 0x11FFB8) { $siteInstructions += $instruction }
    if ($instruction.Op0Kind.ToString() -match 'NearBranch' -and
        $instruction.NearBranch64 -gt 0x11FFA7 -and $instruction.NearBranch64 -lt 0x11FFB8) {
        throw "Branch enters observation span: $instruction"
    }
}
if ($siteInstructions.Count -ne 3 -or
    $siteInstructions[0].NearBranch64 -ne 0x123090 -or
    $siteInstructions[1].MemoryDisplacement64 -ne 0x60C89A8 -or
    $siteInstructions[2].NearBranch64 -ne 0x121B20 -or
    $siteInstructions[2].NextIP -ne 0x11FFB8) {
    throw 'Native consumer/gate/continuation contract differs.'
}
Write-Host 'PASS: native hash, exact 17-byte span, incoming branches, filter and both gate continuations.'
