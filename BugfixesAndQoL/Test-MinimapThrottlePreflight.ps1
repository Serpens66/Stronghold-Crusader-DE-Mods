[CmdletBinding()]
param([string]$ExtenderDir = $env:SHCDESE_EXTENDER_DIR)
$ErrorActionPreference = 'Stop'
# Native contract of the V13 minimap throttle detour (UpdateToNewDLL.md, baseline RADAR_PASS_F5A40.md):
# the installed DLL still has the audited prologue at the RVA the runtime uses, 0x86B9B is the only
# rel32 entry from outside, and no internal branch lands inside the six displaced bytes.
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
if (-not $ExtenderDir) { $ExtenderDir = Join-Path $game 'BepInEx\plugins\000shcdese' }
$native = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$source = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\VanillaPerformance\MinimapRefreshThrottle.cs'))
$rvaMatch = [regex]::Match($source, 'RadarPassRva = 0x([0-9A-Fa-f]+);')
$lengthMatch = [regex]::Match($source, 'DisplacedLength = (\d+);')
$prologueMatch = [regex]::Match($source, 'Prologue = \{ ([^}]+) \};')
if (-not $rvaMatch.Success -or -not $lengthMatch.Success -or -not $prologueMatch.Success) { throw 'Minimap throttle constants not found in the runtime source.' }
$entry = [Convert]::ToInt32($rvaMatch.Groups[1].Value, 16)
$displaced = [int]$lengthMatch.Groups[1].Value
$prologue = @($prologueMatch.Groups[1].Value -split ',\s*' | ForEach-Object { [Convert]::ToByte($_.Trim().Substring(2), 16) })
$end = 0xF6DDE # end of 0xF5A40 (.pdata / Rizin function bounds)
$expected = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
$stream = [IO.File]::OpenRead($native)
try { $sha = [Security.Cryptography.SHA256]::Create(); try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') } finally { $sha.Dispose() } }
finally { $stream.Dispose() }
if ($actual -ne $expected) { throw 'Installed native DLL differs from the audited minimap contract.' }
if ($prologue.Count -ne $displaced) { throw 'Prologue length differs from the displaced length.' }
$bytes = [IO.File]::ReadAllBytes($native)
$pe = [BitConverter]::ToInt32($bytes, 0x3c)
$sections = [BitConverter]::ToUInt16($bytes, $pe + 6)
$optionalSize = [BitConverter]::ToUInt16($bytes, $pe + 20)
$text = $null
for ($i = 0; $i -lt $sections; $i++) {
    $section = $pe + 24 + $optionalSize + $i * 40
    if ([Text.Encoding]::ASCII.GetString($bytes, $section, 5) -eq '.text') {
        $text = [pscustomobject]@{ Rva = [BitConverter]::ToInt32($bytes, $section + 12); Size = [BitConverter]::ToInt32($bytes, $section + 16); Raw = [BitConverter]::ToInt32($bytes, $section + 20) }
    }
}
if (-not $text) { throw '.text section not found.' }
$entryOffset = $text.Raw + $entry - $text.Rva
for ($i = 0; $i -lt $prologue.Count; $i++) { if ($bytes[$entryOffset + $i] -ne $prologue[$i]) { throw 'Minimap pass prologue differs.' } }
$callers = @()
for ($o = $text.Raw; $o -lt $text.Raw + $text.Size - 5; $o++) {
    if ($bytes[$o] -eq 0xE8 -or $bytes[$o] -eq 0xE9) {
        $site = $text.Rva + ($o - $text.Raw)
        $target = $site + 5 + [BitConverter]::ToInt32($bytes, $o + 1)
        if ($target -ge $entry -and $target -lt $end -and ($site -lt $entry -or $site -ge $end)) { $callers += $site }
    }
}
if ($callers.Count -ne 1 -or $callers[0] -ne 0x86B9B) { throw ('Unexpected external rel32 references into the minimap pass: ' + (($callers | ForEach-Object { '0x{0:X}' -f $_ }) -join ',')) }
Add-Type -Path (Join-Path $ExtenderDir 'Iced.dll')
$code = New-Object byte[] ($end - $entry)
[Array]::Copy($bytes, $entryOffset, $code, 0, $code.Length)
$decoder = [Iced.Intel.Decoder]::Create(64, (New-Object Iced.Intel.ByteArrayCodeReader -ArgumentList (, $code)))
$decoder.IP = [uint64](0x180000000 + $entry)
$branches = 0
while ($decoder.IP -lt [uint64](0x180000000 + $end)) {
    $instruction = $decoder.Decode()
    if ($instruction.Code -eq [Iced.Intel.Code]::INVALID) { throw ('Invalid instruction at 0x{0:X}' -f ($instruction.IP - 0x180000000)) }
    if ($instruction.FlowControl -eq [Iced.Intel.FlowControl]::ConditionalBranch -or $instruction.FlowControl -eq [Iced.Intel.FlowControl]::UnconditionalBranch) {
        $branches++
        $t = $instruction.NearBranchTarget - 0x180000000
        if ($t -gt $entry -and $t -lt $entry + $displaced) { throw ('Branch into the displaced span at 0x{0:X}' -f ($instruction.IP - 0x180000000)) }
    }
}
if ($branches -lt 50) { throw "Implausibly few branches decoded in the minimap pass ($branches)." }
Write-Output "PASS: minimap throttle native contract (hash, prologue, single caller 0x86B9B, $branches internal branches outside the displaced span)."
