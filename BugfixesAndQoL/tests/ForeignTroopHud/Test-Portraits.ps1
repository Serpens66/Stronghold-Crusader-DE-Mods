#Requires -Version 7.0
[CmdletBinding()]
param([string]$ModDirectory = (Join-Path $PSScriptRoot '..\..'))
$ErrorActionPreference = 'Stop'
$modRoot = [IO.Path]::GetFullPath($ModDirectory)
$runtime = [IO.File]::ReadAllText((Join-Path $modRoot 'src\ForeignTroopHudRuntime.cs'))
$entryStart = $runtime.IndexOf('    internal sealed class ForeignTroopEntry')
if ($entryStart -lt 0) { throw 'Cannot find production entry type.' }
$entryEnd = $runtime.IndexOf('    internal static class ForeignTroopHudRuntime', $entryStart)
if ($entryEnd -le $entryStart) { throw 'Cannot isolate production entry type.' }
$entry = 'namespace BugfixesAndQoL {' + $runtime.Substring($entryStart, $entryEnd - $entryStart) + '}'
$captureStart = $runtime.IndexOf('int key = (owner << 8) | type;')
if ($captureStart -lt 0) { throw 'Cannot find production owner/type capture block.' }
$captureEndText = 'entry.MaxHealth += unit.r_MaxHealth;'
$captureEnd = $runtime.IndexOf($captureEndText, $captureStart)
if ($captureEnd -le $captureStart) { throw 'Cannot isolate production capture block.' }
$capture = $runtime.Substring($captureStart, $captureEnd + $captureEndText.Length - $captureStart)
$fixture = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'PortraitTests.cs'))
if (-not $fixture.Contains('/* PRODUCTION_CAPTURE_BLOCK */')) { throw 'Capture fixture marker missing.' }
$fixture = $fixture.Replace('/* PRODUCTION_CAPTURE_BLOCK */', $capture)
$sources = @(
    $entry,
    [IO.File]::ReadAllText((Join-Path $modRoot 'src\ForeignTroopPortraits.cs')),
    [IO.File]::ReadAllText((Join-Path $modRoot 'src\ForeignTroopHudView.cs')),
    $fixture
)
# Compile the actual helper, entry and complete view; doubles replace only game/Noesis boundaries.
$body = ($sources | ForEach-Object { [regex]::Replace($_, '(?m)^using [^\r\n]+;\r?\n', '') }) -join [Environment]::NewLine
$imports = 'using System; using System.Collections.Generic; using BugfixesAndQoL; using CrusaderDE; using Noesis; using SHCDESE.Interop;'
Add-Type -TypeDefinition ($imports + [Environment]::NewLine + $body)
Write-Host ([ForeignTroopPortraitTests]::Run())

# Narrow integration guard: runtime must validate the field before its native span is used.
# Its exact offset is the audited native ABI, not an implementation preference.
if (-not $runtime.Contains('nameof(GameUnit.r_GameMaterialIndex)).ToInt32() != 0x08')) {
    throw 'Audited material layout guard missing.'
}
Write-Host 'PASS: audited material offset guard.'
