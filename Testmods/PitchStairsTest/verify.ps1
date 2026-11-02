$ErrorActionPreference = 'Stop'
# Pre-build guard: the native contract of this test mod is bound to one CrusaderDE.dll hash.
# Site bytes, continuations and stub behavior are checked by PitchStairsTest.StubTests.exe.
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..\..')).Path
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$native = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$expected = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
$stream = [IO.File]::OpenRead($native)
try {
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { $actual = [BitConverter]::ToString($sha256.ComputeHash($stream)).Replace('-', '') }
    finally { $sha256.Dispose() }
}
finally { $stream.Dispose() }
if ($actual -ne $expected) { throw "Installed native DLL $actual differs from the audited pitch-stairs contract." }
$current = Get-Content -LiteralPath (Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\CURRENT.json') -Raw | ConvertFrom-Json
if ($current.currentNativeHash -ne $expected) { throw 'Native baseline does not match the installed DLL.' }
$source = Get-Content -LiteralPath (Join-Path $project 'src\NativeCode.cs') -Raw
if ($source -notmatch [regex]::Escape($expected)) { throw 'NativeCode.cs is not bound to the verified native hash.' }
$info = Get-Content -LiteralPath (Join-Path $project 'info.json') -Raw | ConvertFrom-Json
$plugin = Get-Content -LiteralPath (Join-Path $project 'src\PitchStairsTestPlugin.cs') -Raw
if ($plugin -notmatch ('Version = "' + [regex]::Escape($info.Version) + '"')) { throw 'Plugin and info.json versions differ.' }
if ($info.NetworkMode -ne 1) { throw 'Pitch stairs changes gameplay and must use NetworkMode 1.' }
Write-Host 'PASS: pitch-stairs native hash, baseline and manifest checks.'
