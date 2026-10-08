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
$farmCreators = @{}
Get-Content -LiteralPath $functions | Where-Object {
    $_ -match '"rva":"0x(72490|73450|74410|776F0|7B060|C3BF0)"'
} | ForEach-Object { $item = $_ | ConvertFrom-Json; $farmCreators[$item.rva] = $item }
foreach ($rva in @('0x72A50', '0x73450', '0x74410', '0x776F0')) {
    $entry = if ($rva -eq '0x72A50') { $orchardFunctions[$rva] } else { $farmCreators[$rva] }
    if ($null -eq $entry -or $entry.calleeRvas -notcontains '0x72490') {
        throw "Farm parcel writer call differs at $rva."
    }
}
if ($null -eq $farmCreators['0x7B060'] -or $null -eq $farmCreators['0xC3BF0']) {
    throw 'Placement or route function metadata is unavailable.'
}
$nativeBytes = [IO.File]::ReadAllBytes($native)
$pe = [BitConverter]::ToInt32($nativeBytes, 0x3c)
$sectionCount = [BitConverter]::ToUInt16($nativeBytes, $pe + 6)
$optionalSize = [BitConverter]::ToUInt16($nativeBytes, $pe + 20)
$gateRva = 0x58B12
$gateOffset = -1
for ($i = 0; $i -lt $sectionCount; $i++) {
    $section = $pe + 24 + $optionalSize + $i * 40
    $size = [BitConverter]::ToInt32($nativeBytes, $section + 8)
    $rva = [BitConverter]::ToInt32($nativeBytes, $section + 12)
    if ($rva -le $gateRva -and $gateRva + 24 -le $rva + $size) {
        $gateOffset = [BitConverter]::ToInt32($nativeBytes, $section + 20) + $gateRva - $rva
        break
    }
}
if ($gateOffset -lt 0) { throw 'Native farmland candidate gate is unavailable.' }
$expectedGate = [byte[]]@(0x75,0x1A,0x44,0x38,0xB2,0x37,0xB8,0x05,0x00,0x75,0x11,0x44,
    0x38,0xB2,0x38,0xB8,0x05,0x00,0x7F,0x08,0x84,0xDB,0x0F,0x84)
for ($i = 0; $i -lt $expectedGate.Length; $i++) {
    if ($nativeBytes[$gateOffset + $i] -ne $expectedGate[$i]) {
        throw 'Native farmland candidate gate differs from the runtime compatibility check.'
    }
}
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
$generalEntries = (& $rizin -q -e scr.color=false -c 's 0x1800583a0; p8 6; s 0x180058be0; p8 6; q' $native)
if ($generalEntries.Count -ne 2 -or
    $generalEntries[0].Trim().ToLowerInvariant() -ne '41574883ec20' -or
    $generalEntries[1].Trim().ToLowerInvariant() -ne '895424105355') {
    throw 'General AI search entry bytes or six-byte instruction boundaries differ.'
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
$placementSource = (& $rizin -q -e scr.color=false -c 's 0x1800724ea; pd 5; s 0x180078878; pd 9; q' $native) -join "`n"
if ($placementSource -notmatch '0x1800724ec\s+or\s+dword \[rbx \+ rax\*4 \+ 0x898400\], 4' -or
    $placementSource -notmatch '0x180078889\s+call\s+0x18007b060' -or
    $placementSource -notmatch '0x180078892\s+mov\s+dword \[rbx \+ 0x204e6fc\], 1') {
    throw 'Apple-farm reservation writer or woodsman placement validator differs.'
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
$generalHook = [IO.File]::ReadAllText((Join-Path $project 'src\GeneralSiteSearchHooks.cs'))
$otherRuntimeText = (($sourceFiles | Where-Object { $_.Name -ne 'GeneralSiteSearchHooks.cs' }) |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
if ($otherRuntimeText -match 'CodePatch\.Write|VirtualProtect|NativeDetour|X64InlineHook|HookTransaction|\.Apply\s*\(|\.Undo\s*\(|\.Enable\s*\(|\.Disable\s*\(' -or
    $generalHook -match 'CodePatch\.Write|VirtualProtect|X64InlineHook|\.Apply\s*\(|\.Undo\s*\(|\.Enable\s*\(|\.Disable\s*\(' -or
    $generalHook -notmatch 'published = this' -or
    $generalHook -notmatch 'hook\.Original' -and $generalHook -notmatch 'aivHook\.Original' -or
    $generalHook -notmatch 'fineHook\.Original') {
    throw 'Diagnostic hook ownership, persistence or one-call Vanilla contract differs.'
}
if ($runtimeText -match 'Marshal\.Write(?!Byte\(new IntPtr\(cell\), 1\)|Byte\(new IntPtr\(address\), 0\))' -or
    $runtimeText -notmatch 'nearbyCopySession = active && session\.IsLoadedSave && nearbyWoodTestEnabled' -or
    $runtimeText -notmatch 'nearbyCalibrated') {
    throw 'Nearby data overlay has an unexpected write or session guard.'
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
if ($runtimeText -notmatch 'AI_BUILD_GENERIC_STRUCTURE:' -or
    $runtimeText -notmatch 'AI_BUILD_GENERIC_SPAWN:' -or
    $runtimeText -notmatch 'AI_BUILD_GENERIC_EVENT_SUMMARY:' -or
    $runtimeText -notmatch 'AI_BUILD_AIV_WALL_PLAN_CAPTURE:' -or
    $runtimeText -notmatch 'matchedSpawn=\{spawned\}, aivWallPlan=\{planned\}' -or
    $runtimeText -notmatch 'GetCoarseGrid\(\)' -or
    $runtimeText -notmatch 'postEventReturnValueNotAuthoritative=true' -or
    $runtimeText -notmatch 'eventEvidenceOnly=true' -or
    $runtimeText -notmatch 'genericBuildEvents\.Clear\(\)' -or
    $runtimeText -notmatch 'genericSpawnById\.Clear\(\)' -or
    $runtimeText -notmatch 'aivWallPlanByTile\.Clear\(\)') {
    throw 'Generic build/spawn correlation or map-switch reset is incomplete.'
}
if ($runtimeText -notmatch 'NearbyDx = \{ 0, 1, 1, 1, 0, -1, -1, -1 \}' -or
    $runtimeText -notmatch 'NearbyDy = \{ -1, -1, 0, 1, 1, 1, 0, -1 \}' -or
    $runtimeText -notmatch 'BeginNearbyWoodOverlay\(' -or
    $runtimeText -notmatch 'RestoreNearbyWoodOverlay\(changed, attemptId\)' -or
    $runtimeText -notmatch 'nearbyCalibrated' -or
    $runtimeText -notmatch 'CandidateTrace\.Count >= 256' -or
    $runtimeText -notmatch 'footprint\.ExcludeForCopyProbe' -or
    $runtimeText -notmatch 'result\.MaskedCandidates\.Add' -or
    $runtimeText -notmatch 'ExpectedFootprint\.NoMeasuredBlockers' -or
    $runtimeText -notmatch 'liveFootprint\.ExcludeForCopyProbe' -or
    $runtimeText -notmatch 'string\.Equals\(liveFootprint\.Tiles, candidate\.FootprintTiles' -or
    $runtimeText -notmatch 'Marshal\.WriteByte\(new IntPtr\(cell\), 1\)' -or
    $runtimeText -notmatch 'Marshal\.WriteByte\(new IntPtr\(address\), 0\)' -or
    $runtimeText -notmatch 'NearbyCopyMaxApplications = 12' -or
    $runtimeText -notmatch 'nearbyCopyApplications >= NearbyCopyMaxApplications' -or
    $runtimeText -notmatch 'nearbyCopyApplications\+\+' -or
    $runtimeText -notmatch 'AI_BUILD_NEARBY_TEST_LIMIT_REACHED' -or
    $runtimeText -notmatch 'restoreFailed=\{failed\}' -or
    $runtimeText -notmatch 'Config\.Bind\("NearbyWoodTest", "Enabled", false') {
    throw 'Nearby wood shadow or copy-only overlay contract differs.'
}
if ($runtimeText -notmatch 'NativePlacementReservationFlag = 0x4' -or
    $runtimeText -notmatch 'ReadCoarseReservation\(AiEconomyGridEvidence evidence\)' -or
    $runtimeText -notmatch '0x5B83F \+' -or
    $runtimeText -notmatch 'bit4Tiles=\{CountReservationTiles\(' -or
    $runtimeText -notmatch 'storedReservation=\{ReadCoarseReservation\(' -or
    $runtimeText -notmatch 'Marshal\.ReadInt32\(IntPtr\.Add\(tileManager, 0x204E704\)\)' -or
    $runtimeText -notmatch 'placementReasonMayBeStale=true' -or
    $runtimeText -notmatch 'CaptureFarmParcel\(args\.TileX, args\.TileY, 0, "build-structure-" \+ args\.Phase\)' -or
    $runtimeText -notmatch 'CaptureFarmParcel\(farm\.X, farm\.Y, farm\.BuildingId, stage\)' -or
    $runtimeText -notmatch 'first-path-generation-after-tracking' -or
    $runtimeText -notmatch 'CaptureFarmGridRaw\(record\.Stage, record\.EconomyGridEvidence\)' -or
    $runtimeText -notmatch 'Marshal\.ReadByte\(new IntPtr\(address \+ 11\)\)' -or
    $runtimeText -notmatch 'farmGridPairCaptured = true' -or
    $runtimeText -notmatch 'nearbyDetailedByPlayer\[playerId\]' -or
    $runtimeText -notmatch 'CaptureNearbyFootprint\(x, y\)' -or
    $runtimeText -notmatch '0x60AD660 \+ 0x74') {
    throw 'Farm-parcel or raw native placement-reason diagnostic contract differs.'
}
$farmProbe = [IO.File]::ReadAllText((Join-Path $project 'src\FarmSiteContractProbe.cs'))
if ($farmProbe -notmatch 'rat_farm_site_contract_probe\.sav' -or
    $farmProbe -notmatch 'SourceHash = "9CE228735067734138E450CD91902C6F8F28FE75634F0952AC5F0643E20D378E"' -or
    $farmProbe -notmatch 'session\.IsLoadedSave' -or
    $farmProbe -notmatch 'string\.Equals\(Hash\(path\), SourceHash' -or
    $farmProbe -notmatch 'CreatePrefab\(PlayerId, x, y,' -or
    $farmProbe -notmatch 'mapper, scale, 15, freeCost, false\)' -or
    $farmProbe -notmatch 'eMappers\.MAPPER_WOODSMAN, 3, false\)' -or
    $farmProbe -match 'Marshal\.Write|CodePatch\.Write|bypassPlacementRules: true' -or
    $runtimeText -notmatch 'Config\.Bind\("FarmContractProbe", "Enabled", false' -or
    $runtimeText -notmatch 'farmContractProbe\.OnTick\(tick, ReadPathGeneration\(\)\)') {
    throw 'The active farm contract probe is not copy-only, bounded or Vanilla-placement preserving.'
}
$swap = [IO.File]::ReadAllText((Join-Path $project 'src\CanariFarmSwapProbe.cs'))
$fixesOverride = [IO.File]::ReadAllText((Join-Path $project 'src\FixesFarmFilterOverride.cs'))
$plugin = [IO.File]::ReadAllText((Join-Path $project 'src\AIBuildDiagnosePlugin.cs'))
if ($fixesOverride -notmatch 'TryGetEntry<bool>\(' -or
    $fixesOverride -notmatch '"FixPlacementSelectionNotAccountingForFarmland", "Enabled"' -or
    $fixesOverride -notmatch 'source\.SaveOnConfigSet = false;\s*setting\.Value = false' -or
    $fixesOverride -notmatch 'entry\.Value = originalValue' -or
    $fixesOverride -notmatch 'config\.SaveOnConfigSet = originalSaveOnSet' -or
    $fixesOverride -notmatch 'IsVanillaCandidateGate\(moduleHandle\)' -or
    $fixesOverride -notmatch 'string\.Equals\(HashFile\(config\.ConfigFilePath\), originalFileHash' -or
    $plugin -notmatch 'fixesFarmFilterOverride = FixesFarmFilterOverride\.Begin\(log\)' -or
    $plugin -notmatch 'fixesFarmFilterOverride\.Complete\(context\.ModuleHandle\)' -or
    $plugin -notmatch 'vanillaComparisonReady && canariFarmSwapEnabled' -or
    $plugin -notmatch 'vanillaComparisonReady && farmContractProbeEnabled') {
    throw 'Fixes in-memory startup override or active-probe fail-closed contract differs.'
}
$demolition = @{}
Get-Content -LiteralPath $functions | Where-Object {
    $_ -match '"rva":"0x(B8310|61FC0|62240|62780)"'
} | ForEach-Object { $item = $_ | ConvertFrom-Json; $demolition[$item.rva] = $item }
if ($demolition['0xB8310'].calleeRvas -notcontains '0x61FC0' -or
    $demolition['0x61FC0'].calleeRvas -notcontains '0x62240' -or
    $demolition['0x61FC0'].calleeRvas -notcontains '0x62780' -or
    $swap -notmatch 'OriginalHash =\s*"17BAB0CA7C73F1254765CEC0DD6BCFEDBA73343D74DE1D7F35DD6715C3EAC31A"' -or
    $swap -notmatch 'DeleteBuildingSafe\(oldId\)' -or
    $swap -notmatch 'OriginalOrchardPresent\(\)' -or
    $swap -match 'r_OccupyTileGridSize != (OrchardSize|scale)' -or
    $swap -notmatch 'parcelBit4=' -or
    $swap -notmatch 'OldBuildingGone\(\) \|\| !ParcelClean\(\)' -or
    $swap -notmatch 'generation == removalGeneration' -or
    $swap -notmatch 'CreatePrefab\(owner,' -or
    $swap -notmatch 'OrchardX, OrchardY, mapper, scale, 15, true, false\)' -or
    $swap -notmatch 'ShouldDeferWoodBuild\(int playerId\)' -or
    $swap -notmatch 'playerId != 6' -or
    $swap -notmatch 'Volatile\.Read\(ref state\) == 5' -or
    $swap -notmatch 'replacement-path-rebuild-timeout' -or
    $swap -notmatch 'if \(!ReplacementPresent\(\) \|\| !OldAppleTilesGone\(\)\)' -or
    $swap -match 'path-rebuild-not-observed[^\n]*state = 5' -or
    $swap -match 'Marshal\.Write|CodePatch\.Write|bypassPlacementRules: true' -or
    $runtimeText -notmatch 'Config\.Bind\("CanariFarmSwap", "Enabled", false' -or
    $runtimeText -notmatch 'lock \(appleFarmsSync\) return appleFarms\.ToArray\(\)' -or
    $runtimeText -notmatch 'initialAppleFarmScanComplete = true' -or
    $runtimeText -notmatch '!initialAppleFarmScanComplete\) return' -or
    $runtimeText -match 'foreach \(AppleFarmWatch farm in appleFarms\)') {
    throw 'The Canari copy-only demolition and replacement contract differs.'
}
$coarseNative = [IO.File]::ReadAllText((Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\sem\FBCB9319\exports\semantic-decompiled-functions.c'))
foreach ($contract in @('FUNCTION FUN_180050620', 'FUNCTION FUN_180050720',
    'FUNCTION FUN_1800575b0', 'FUNCTION FUN_180058950', '0x5b83f')) {
    if (-not $coarseNative.Contains($contract)) {
        throw "Native coarse reservation reader/writer contract missing: $contract"
    }
}
$bugfixRuntime = [IO.File]::ReadAllText((Join-Path $workspace 'BugfixesAndQoL\src\AIPreplacedBuildingFixRuntime.cs'))
$publisher = [IO.File]::ReadAllText((Join-Path $workspace 'APIShared\src\Diagnostics\AiBuildDiagnostic.cs'))
if ($bugfixRuntime -notmatch 'APIShared\.AiBuildDiagnostic\.BeginNearbyWoodObservation\(' -or
    $bugfixRuntime -notmatch 'AiBuildDiagnostic\.ShouldDeferWoodBuild\(playerId\)' -or
    $bugfixRuntime -notmatch 'Publish\("wood-build-deferred", playerId\)' -or
    $publisher -notmatch 'TryRegisterWoodBuildGate\(' -or
    $publisher -notmatch 'Volatile\.Write\(ref woodBuildGate, gate\)' -or
    $runtimeText -notmatch 'candidate\.ShouldDeferCanariWoodBuild' -or
    $bugfixRuntime -notmatch 'APIShared\.AiBuildDiagnostic\.EndNearbyWoodObservation\(restore,' -or
    $publisher -notmatch 'PublishNearbyPathEvidence\("wood-nearby-path-before"' -or
    $publisher -notmatch 'PublishNearbyPathEvidence\("wood-nearby-path-after"' -or
    $publisher -notmatch 'Publish\("wood-nearby-before"' -or
    $publisher -notmatch 'Publish\("wood-nearby-after"' -or
    $publisher -notmatch 'TryRegisterNearbyWoodOverlay\(' -or
    $publisher -notmatch 'AllocateRouteProbeNear\(entry\)' -or
    $publisher -notmatch 'catch \(Exception ex\) \{ failures.Add\("scheduler: "') {
    throw 'Main-mod overlay restoration or independent native observation contract differs.'
}
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
