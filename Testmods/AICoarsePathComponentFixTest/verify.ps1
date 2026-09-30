$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..\..')).Path
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$native = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$expected = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
$nativeStream = [IO.File]::OpenRead($native)
try {
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { $actual = [BitConverter]::ToString($sha256.ComputeHash($nativeStream)).Replace('-', '') }
    finally { $sha256.Dispose() }
}
finally { $nativeStream.Dispose() }
if ($actual -ne $expected) {
    throw 'Installed native DLL differs from the audited coarse-grid contract.'
}
$current = Get-Content -LiteralPath (Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\CURRENT.json') -Raw | ConvertFrom-Json
if ($current.currentNativeHash -ne $expected) { throw 'Native baseline does not match the installed DLL.' }
$bytes = [IO.File]::ReadAllBytes($native)
$pe = [BitConverter]::ToInt32($bytes, 0x3c)
$sections = [BitConverter]::ToUInt16($bytes, $pe + 6)
$optionalSize = [BitConverter]::ToUInt16($bytes, $pe + 20)
$targetRva = 0xE49D0
$offset = -1
for ($i = 0; $i -lt $sections; $i++) {
    $section = $pe + 24 + $optionalSize + $i * 40
    $size = [BitConverter]::ToInt32($bytes, $section + 16)
    $rva = [BitConverter]::ToInt32($bytes, $section + 12)
    if ($rva -le $targetRva -and $targetRva + 8 -le $rva + $size) {
        $offset = [BitConverter]::ToInt32($bytes, $section + 20) + $targetRva - $rva
        break
    }
}
if ($offset -lt 0) { throw 'Native rebuild entry is not in a file-backed section.' }
$prologue = @(0x40,0x53,0x41,0x57,0x48,0x83,0xEC,0x58)
for ($i = 0; $i -lt $prologue.Count; $i++) {
    if ($bytes[$offset + $i] -ne $prologue[$i]) { throw 'Native 0xE49D0 eight-byte prologue differs.' }
}
$woodSiteRva = 0x58B26
$woodSiteOffset = -1
for ($i = 0; $i -lt $sections; $i++) {
    $section = $pe + 24 + $optionalSize + $i * 40
    $size = [BitConverter]::ToInt32($bytes, $section + 16)
    $rva = [BitConverter]::ToInt32($bytes, $section + 12)
    if ($rva -le $woodSiteRva -and $woodSiteRva + 8 -le $rva + $size) {
        $woodSiteOffset = [BitConverter]::ToInt32($bytes, $section + 20) + $woodSiteRva - $rva
        break
    }
}
if ($woodSiteOffset -lt 0) { throw 'Native wood guard site is not in a file-backed section.' }
$woodSiteBytes = @(0x84,0xDB,0x0F,0x84,0x80,0x00,0x00,0x00)
for ($i = 0; $i -lt $woodSiteBytes.Count; $i++) {
    if ($bytes[$woodSiteOffset + $i] -ne $woodSiteBytes[$i]) {
        throw 'Native 0x58B26 eight-byte branch differs.'
    }
}
$extender = Join-Path $game 'BepInEx\plugins\000shcdese'
foreach ($name in @('SHCDESE.dll','R3.dll','RedBird.Abstractions.dll',
    'RedBird.Core.dll','RedBird.X64.dll','RedBird.Backends.NativeX64.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $extender $name))) {
        throw "Installed Script Extender dependency missing: $name"
    }
}
$fixesSource = 'D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Fremde Mods\shcde-fixes-main\src\shcde-fixes\Detours\PathingDetours.cs'
if (-not (Test-Path -LiteralPath $fixesSource)) { throw 'Canonical local Fixes clone is unavailable.' }
$fixesText = [IO.File]::ReadAllText($fixesSource)
if ($fixesText -notmatch '0xE49D0|0xE49D0|ComponentTileCount') {
    throw 'Fixes pathing patch source changed; re-audit the 0xE49D0 entry overlap.'
}
$source = [IO.File]::ReadAllText((Join-Path $project 'src\AiCoarsePathComponentFix.cs'))
$woodGuard = [IO.File]::ReadAllText((Join-Path $project 'src\WoodSiteGuardExperiment.cs'))
$plugin = [IO.File]::ReadAllText((Join-Path $project 'src\AICoarsePathComponentFixTestPlugin.cs'))
$main = [IO.File]::ReadAllText((Join-Path $workspace 'BugfixesAndQoL\src\BugfixesAndQoLRuntime.cs'))
if ($source -notmatch 'DisplacedLength = 8' -or
    $source -notmatch 'probe\.Scheme\.ToString\(\) != "Indirect"' -or
    $source -notmatch 'ValidateInstalledHook\(target\)' -or
    $source -notmatch 'rebuildHook\.Original\(pathingContext, force\)' -or
    $source -notmatch 'MissionEvents\.Started\.Subscribe\(OnSessionStarted\)' -or
    $source -notmatch 'coarse-audit-cell' -or
    $source -notmatch 'coarse-audit-summary' -or
    $source -notmatch 'observationOnly=True; writes=0' -or
    $source -notmatch 'if \(isolationMode >= 1 && isolationMode <= 4\)' -or
    $source -notmatch 'IsolationSaveName = "test_canari_nowoodcutters_probe.sav"' -or
    $source -notmatch 'args\.Context\);' -or
    $source -notmatch 'context\.IsSave' -or
    $source -notmatch 'if \(!isolationArmedForMap\) return rebuilt;' -or
    $source -notmatch 'if \(isolationMode == 5 &&' -or
    $source -notmatch 'isolationMode == 5 && isolationArmedForMap' -or
    $source -notmatch 'if \(isolationMode <= 4\) InstallIsolationHook\(\)' -or
    $source -notmatch 'ReadPathGeneration\(' -or
    $plugin -notmatch 'GameTimeManagerAPI\.Instance\.OnTick \+= OnTick' -or
    $plugin -notmatch '"Mode", 0' -or
    $source -match '\*nativeReference\s*=(?!=)' -or
    $source -match '\.ForeignPathComponentTileCount\s*=(?!=)' -or
    $plugin -notmatch 'AiEconomyOverlayRestored \+= OnOverlayRestored' -or
    $plugin -notmatch '"WoodSiteGuard", "Enabled", false' -or
    $plugin -notmatch '"WoodSiteGuard", "Scope", "CopyOnly"' -or
    $plugin -notmatch '"WoodSiteGuard", "Decision", "Reject"' -or
    $plugin -notmatch 'woodGuardDecision\);' -or
    $woodGuard -notmatch 'Original = \{ 0x84, 0xDB, 0x0F, 0x84, 0x80, 0, 0, 0 \}' -or
    $woodGuard -notmatch 'AiBuildDiagnostic\.TryGetCurrentWoodAttempt' -or
    $woodGuard -notmatch 'AiBuildDiagnostic\.CaptureTiles' -or
    $woodGuard -notmatch 'ReportNativeLibraryVersion\(log, "AI wood site guard"' -or
    $woodGuard -notmatch 'IsOldOverlayEnabled\(' -or
    $woodGuard -notmatch 'MissionEvents\.Started\.Subscribe' -or
    $woodGuard -notmatch 'MissionEvents\.Ended\.Subscribe' -or
    $woodGuard -notmatch 'CopySaveName = "test_canari_nowoodcutters_probe.sav"' -or
    $woodGuard -notmatch 'RatMapName = "spezialist 3vs5.map"' -or
    $woodGuard -notmatch 'RatSaveName = "rat_wood_guard_control_probe.sav"' -or
    $woodGuard -notmatch 'scope == "RatSaveOnly" && context\.IsSave' -or
    $woodGuard -notmatch 'decision != "ObserveOnly" && decision != "Reject"' -or
    $woodGuard -notmatch 'bool apply = reason != 0 && current\.rejectCandidates;' -or
    $woodGuard -notmatch 'RecordCandidate\(attemptId, playerId, coarseX, coarseY, reason, apply\)' -or
    $woodGuard -notmatch 'FlushCandidateTraces\(\);' -or
    $main -match 'new AiCoarsePathComponentFix|processAiCoarsePathComponentFix') {
    throw 'Hook, lifecycle, diagnostic or production-removal contract differs.'
}
$textFiles = @(
    (Join-Path $project 'src\AiCoarsePathComponentFix.cs'),
    (Join-Path $project 'src\AICoarsePathComponentFixTestPlugin.cs'),
    (Join-Path $project 'src\WoodSiteGuardExperiment.cs'),
    (Join-Path $project 'UpdateToNewDLL.md'),
    (Join-Path $project 'Properties\AssemblyInfo.cs'),
    (Join-Path $project 'AICoarsePathComponentFixTest.csproj'),
    (Join-Path $project 'info.json'),
    (Join-Path $project 'build.bat'),
    (Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\sem\FBCB9319\knowledge\AI_WOODCUTTER_BUILD.md'),
    $MyInvocation.MyCommand.Path)
foreach ($path in $textFiles) {
    $content = [IO.File]::ReadAllText($path)
    if ($content -match '(?<!\r)\n' -or $content -match '(?<!\r)\r(?!\n)') {
        throw "Non-CRLF line ending: $path"
    }
    if ($content.Contains(([string][char]92) + 'r' + ([string][char]92) + 'n')) {
        throw "Literal escaped newline: $path"
    }
}
$runtimeText = $source + $plugin + $woodGuard + [IO.File]::ReadAllText((Join-Path $project 'AICoarsePathComponentFixTest.csproj'))
if ($runtimeText -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json' -or
    $runtimeText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|StartCoroutine|Update|LateUpdate|FixedUpdate)\s*\(' -or
    $plugin -match '\bUpdate\s*\(') {
    throw 'Forbidden JSON or Unity lifecycle pattern in the test runtime.'
}
& (Join-Path $workspace 'Shared\Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Permanent native hook regression failed.' }
Write-Host 'PASS: safe tick observation and opt-in isolation coarse-grid test mod preflight.'
