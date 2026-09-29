$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..\..')).Path
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' -File)
$projectFile = Join-Path $project 'AIBuildDiagnoseTest.csproj'
$native = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$expectedHash = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
$nativeStream = [IO.File]::OpenRead($native)
try {
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { $actualHash = [BitConverter]::ToString($sha256.ComputeHash($nativeStream)).Replace('-', '') }
    finally { $sha256.Dispose() }
}
finally { $nativeStream.Dispose() }
if (-not [string]::Equals($actualHash, $expectedHash, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Installed native DLL differs from the audited woodcutter build.'
}
$functions = Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\sem\FBCB9319\exports\semantic-functions.jsonl'
$audited = @{}
Get-Content -LiteralPath $functions | Where-Object {
    $_ -match '"rva":"0x(51540|58020|58950|58BE0|C3BF0)"'
} | ForEach-Object { $item = $_ | ConvertFrom-Json; $audited[$item.rva] = $item }
if ($audited['0x51540'].calleeRvas -notcontains '0x58950' -or
    $audited['0x58950'].calleeRvas.Count -ne 0 -or
    $audited['0x58950'].dataRvas -contains '0x50EC690' -or
    $audited['0x58BE0'].calleeRvas -notcontains '0xE2610' -or
    $audited['0x58BE0'].dataRvas -notcontains '0x50EC690') {
    throw 'Audited 0x58950 and 0x58BE0 contracts differ.'
}
$farmAudit = @{}
Get-Content -LiteralPath $functions | Where-Object {
    $_ -match '"rva":"0x(539B0|2DBA0|2C9F0|50D80|575B0)"'
} | ForEach-Object { $item = $_ | ConvertFrom-Json; $farmAudit[$item.rva] = $item }
if ($farmAudit['0x539B0'].calleeRvas -notcontains '0x2DBA0' -or
    $farmAudit['0x539B0'].calleeRvas -notcontains '0x2C9F0' -or
    $farmAudit['0x539B0'].calleeRvas -notcontains '0x50D80' -or
    $farmAudit['0x2DBA0'].calleeRvas -notcontains '0xB81A0' -or
    $farmAudit['0x2C9F0'].dataRvas -notcontains '0x379D824' -or
    $farmAudit['0x50D80'].calleeRvas -notcontains '0x575B0' -or
    $farmAudit['0x575B0'].calleeRvas -notcontains '0x6D580' -or
    $farmAudit['0x575B0'].dataRvas -notcontains '0x379E74A') {
    throw 'Audited farm scheduler, choice, cooldown or search contract differs.'
}
$orchardFunctions = @{}
Get-Content -LiteralPath $functions | Where-Object {
    $_ -match '"rva":"0x(72A50|1071A0|E0850|D90D0|50720)"'
} | ForEach-Object { $item = $_ | ConvertFrom-Json; $orchardFunctions[$item.rva] = $item }
if ($orchardFunctions['0x72A50'].calleeRvas -notcontains '0x1071A0' -or
    $orchardFunctions['0x1071A0'].calleeRvas -notcontains '0xE0850' -or
    $orchardFunctions['0xE0850'].calleeRvas -notcontains '0xD90D0' -or
    $orchardFunctions['0x72A50'].calleeRvas -contains '0x50720') {
    throw 'Audited orchard creation/pathfinding/coarse-grid call chain differs.'
}
$nativeBytes = [IO.File]::ReadAllBytes($native)
$pe = [BitConverter]::ToInt32($nativeBytes, 0x3c)
$sectionCount = [BitConverter]::ToUInt16($nativeBytes, $pe + 6)
$optionalSize = [BitConverter]::ToUInt16($nativeBytes, $pe + 20)
$orchardRva = 0x2d3dc0
$orchardOffset = -1
for ($i = 0; $i -lt $sectionCount; $i++) {
    $section = $pe + 24 + $optionalSize + $i * 40
    $size = [BitConverter]::ToInt32($nativeBytes, $section + 8)
    $rva = [BitConverter]::ToInt32($nativeBytes, $section + 12)
    if ($rva -le $orchardRva -and $orchardRva + 64 -le $rva + $size) {
        $orchardOffset = [BitConverter]::ToInt32($nativeBytes, $section + 20) + $orchardRva - $rva
        break
    }
}
if ($orchardOffset -lt 0) { throw 'Native orchard offset table is unavailable.' }
$expectedOrchard = @(5,1,9,1,1,5,5,5,9,5,1,9,5,9,9,9)
for ($i = 0; $i -lt $expectedOrchard.Count; $i++) {
    if ([BitConverter]::ToInt32($nativeBytes, $orchardOffset + $i * 4) -ne $expectedOrchard[$i]) {
        throw 'Native apple-tree offset table differs from the diagnostic.'
    }
}
$woodDirectionRva = 0x2d2e50
$woodDirectionOffset = -1
for ($i = 0; $i -lt $sectionCount; $i++) {
    $section = $pe + 24 + $optionalSize + $i * 40
    $size = [BitConverter]::ToInt32($nativeBytes, $section + 8)
    $rva = [BitConverter]::ToInt32($nativeBytes, $section + 12)
    if ($rva -le $woodDirectionRva -and $woodDirectionRva + 64 -le $rva + $size) {
        $woodDirectionOffset = [BitConverter]::ToInt32($nativeBytes, $section + 20) + $woodDirectionRva - $rva
        break
    }
}
if ($woodDirectionOffset -lt 0) { throw 'Native wood-search direction table unavailable.' }
$expectedWoodDirections = @(0,-1,1,-1,1,0,1,1,0,1,-1,1,-1,0,-1,-1)
for ($i = 0; $i -lt $expectedWoodDirections.Count; $i++) {
    if ([BitConverter]::ToInt32($nativeBytes, $woodDirectionOffset + $i * 4) -ne $expectedWoodDirections[$i]) {
        throw 'Native wood-search direction order differs from the read-only replay.'
    }
}
$rizin = Join-Path $workspace '.tools\Cutter-v2.4.1-Windows-x86_64\Cutter-v2.4.1-Windows-x86_64\rizin.exe'
$routeEntry = (& $rizin -q -e scr.color=false -c 's 0x1800c3bf0; p8 7; q' $native) -join ''
if ($routeEntry.Trim().ToLowerInvariant() -ne '4883ec384963c0') {
    throw 'Route hook displaced instruction bytes differ from the audited seven bytes.'
}
$redbird = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\RedBird.Backends.NativeX64.dll'
if ((Get-Item -LiteralPath $redbird).VersionInfo.FileVersion -ne '1.5.0.0') {
    throw 'Installed NativeX64 backend version differs from the audited detour implementation.'
}
$calls = (& $rizin -q -e scr.color=false -c 's 0x180051587; pd 1; s 0x1800515cd; pd 1; s 0x1800515ff; pd 1; s 0x180051670; pd 1; q' $native) -join "`n"
foreach ($edge in @(@('51587','58020'), @('515cd','58950'),
    @('515ff','c3bf0'), @('51670','6d580'))) {
    if ($calls -notmatch ("0x1800" + $edge[0] + '\s+call\s+0x1800' + $edge[1])) {
        throw "Woodcutter call edge differs: $($edge[0]) -> $($edge[1])"
    }
}
$gridEdges = (& $rizin -q -e scr.color=false -c 's 0x1800575a4; pd 1; s 0x180071a06; pd 1; s 0x180084d1e; pd 1; s 0x180096d49; pd 1; q' $native) -join "`n"
foreach ($edge in @(@('575a4','jmp'), @('71a06','call'),
    @('84d1e','call'), @('96d49','call'))) {
    if ($gridEdges -notmatch ("0x1800" + $edge[0] + '\s+' + $edge[1] + '\s+0x180050720')) {
        throw "Economy-grid update edge differs: $($edge[0]) -> 50720"
    }
}
$textFiles = @($sourceFiles) + @(
    (Join-Path $project 'Properties\AssemblyInfo.cs'),
    (Join-Path $project 'info.json'),
    $projectFile,
    (Join-Path $project 'build.bat'),
    $MyInvocation.MyCommand.Path)
$runtimeText = (($sourceFiles + @((Get-Item -LiteralPath $projectFile))) |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
if ($runtimeText -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json') {
    throw 'Forbidden runtime JSON dependency.'
}
if ($runtimeText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') {
    throw 'Long-lived or teardown MonoBehaviour callback in diagnostic runtime.'
}
if ($runtimeText -match 'CodePatch\.Write|Marshal\.Write|VirtualProtect|NativeDetour|X64InlineHook|HookTransaction|\.Apply\s*\(|\.Undo\s*\(|\.Enable\s*\(|\.Disable\s*\(') {
    throw 'Diagnostic mod must not own executable-memory mutations.'
}
if ($runtimeText -notmatch 'MissionEvents\.Loading\.Subscribe\(OnMapLoading\)' -or
    $runtimeText -notmatch 'AiBuildDiagnostic\.CaptureEconomyGridEvidence\(gridState, -1\)' -or
    $runtimeText -notmatch 'probeSession = active && session\.IsLoadedSave' -or
    $runtimeText -notmatch 'placementProbeEnabled &&' -or
    $runtimeText -notmatch 'Config\.Bind\("PlacementProbe", "Enabled", false' -or
    $runtimeText -notmatch 'OnVegetationCreate\.Observable\.Subscribe\(OnVegetationCreate\)' -or
    $runtimeText -notmatch 'OrchardObservationTicks = 700' -or
    $runtimeText -notmatch 'building-spawn-pre') {
    throw 'Grid lifecycle or copy-only placement probe guard differs.'
}
if ($runtimeText -notmatch 'WoodSearchDx = \{ 0, 1, 0, -1 \}' -or
    $runtimeText -notmatch 'WoodSearchDy = \{ -1, 0, 1, 0 \}' -or
    $runtimeText -notmatch 'CaptureWallMap\("session-start"\)' -or
    $runtimeText -notmatch 'nextWallMapTick = tick \+ 50' -or
    $runtimeText -notmatch 'DiagnosticBudgetBytes = 64 \* 1024 \* 1024' -or
    $runtimeText -notmatch 'if \(reproduced\)' -or
    $runtimeText -notmatch 'CaptureWoodSearchShadow\(') {
    throw 'Wood shadow, wall-map scan or diagnostic budget contract differs.'
}
$bugfixRuntime = [IO.File]::ReadAllText((Join-Path $workspace 'BugfixesAndQoL\src\AIPreplacedBuildingFixRuntime.cs'))
$publisher = [IO.File]::ReadAllText((Join-Path $workspace 'APIShared\src\AiBuildDiagnostic.cs'))
if ($bugfixRuntime -notmatch 'wood-candidate-scan-request' -or
    $bugfixRuntime -notmatch 'farm-candidate-scan-request' -or
    $bugfixRuntime -match 'PublishWoodCandidateScan|PublishFarmCandidateScan' -or
    $runtimeText -notmatch 'ScanCandidateCells\(' -or
    $runtimeText -notmatch 'wood-candidate-origin' -or
    $runtimeText -notmatch 'farm-candidate-origin' -or
    $publisher -notmatch 'farm-scheduler-" \+ suffix' -or
    $runtimeText -notmatch 'origin-seed-not-tested-as-neighbor' -or
    $runtimeText -notmatch 'coarse-audit-summary' -or
    $runtimeText -notmatch 'hypotheticalGlobalLive=' -or
    $runtimeText -notmatch 'LogFirstSearchOrigin\(record.PlayerId, "wood"' -or
    $runtimeText -notmatch 'LogFirstSearchOrigin\(record.PlayerId, "farm"' -or
    $runtimeText -notmatch 'farmInference=\{farmInference\}') {
    throw 'Wood/farm candidate or scheduler diagnostic contract differs.'
}
foreach ($path in $textFiles) {
    $fullPath = if ($path -is [IO.FileInfo]) { $path.FullName } else { [string]$path }
    $content = [IO.File]::ReadAllText($fullPath)
    if ($content -match '(?<!\r)\n' -or $content -match '(?<!\r)\r(?!\n)') { throw "Non-CRLF line ending: $fullPath" }
    $literalEscapedNewline = ([string][char]92) + 'r' + ([string][char]92) + 'n'
    if ($content.Contains($literalEscapedNewline)) { throw "Literal backslash-r-backslash-n sequence: $fullPath" }
}
& (Join-Path $workspace 'Shared\Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Workspace native runtime regression check failed.' }
Write-Host 'PASS: Diagnostic runtime and CRLF checks.'
