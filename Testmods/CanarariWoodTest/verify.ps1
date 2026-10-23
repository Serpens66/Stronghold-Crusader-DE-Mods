$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = [IO.Path]::GetFullPath((Join-Path $project '..\..'))
$general = Join-Path $workspace 'Testmods\AIBuildDiagnoseTest'
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -File -Recurse -Filter '*.cs')
$runtimeText = (($sourceFiles | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) +
    [IO.File]::ReadAllText((Join-Path $general 'src\Diagnostics\NearbyWoodEvidence.cs'))) -join "`n"
$functions = Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\sem\FBCB9319\exports\semantic-functions.jsonl'
$plugin = [IO.File]::ReadAllText((Join-Path $project 'src\CanarariWoodPlugin.cs'))
if ($runtimeText -match 'HookTransaction|NativeDetour|VirtualProtect|CodePatch\.Write|X64InlineHook|Newtonsoft|System\.Text\.Json|JavaScriptSerializer|JsonUtility' -or
    $runtimeText -match '\b(OnDestroy|OnDisable|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') { throw 'Unexpected companion hook, JSON or lifecycle ownership.' }
if ($runtimeText -notmatch 'namedCopy = active && DisposableProbePolicy.IsNamedCopy' -or
    $runtimeText -notmatch 'probeSession = namedCopy && placementProbeEnabled' -or
    $runtimeText -notmatch 'nearbyCopySession = namedCopy && nearbyWoodTestEnabled') { throw 'Disposable copy gate missing.' }
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
$farmProbe = [IO.File]::ReadAllText((Join-Path $project 'src\FarmSiteContractProbe.cs'))
if ($farmProbe -notmatch 'rat_farm_site_contract_probe\.sav' -or
    $farmProbe -notmatch 'SourceHash = "9CE228735067734138E450CD91902C6F8F28FE75634F0952AC5F0643E20D378E"' -or
    $farmProbe -notmatch 'session\.IsLoadedSave' -or
    $farmProbe -notmatch 'DisposableProbePolicy\.HasExpectedHash\(path, SourceHash' -or
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
$plugin = [IO.File]::ReadAllText((Join-Path $project 'src\CanarariWoodPlugin.cs'))
if ($fixesOverride -notmatch 'TryGetEntry<bool>\(' -or
    $fixesOverride -notmatch '"FixPlacementSelectionNotAccountingForFarmland", "Enabled"' -or
    $fixesOverride -notmatch 'source\.SaveOnConfigSet = false;\s*setting\.Value = false' -or
    $fixesOverride -notmatch 'entry\.Value = originalValue' -or
    $fixesOverride -notmatch 'config\.SaveOnConfigSet = originalSaveOnSet' -or
    $fixesOverride -notmatch 'IsVanillaCandidateGate\(moduleHandle\)' -or
    $fixesOverride -notmatch 'string\.Equals\(HashFile\(config\.ConfigFilePath\), originalFileHash' -or
    $plugin -notmatch 'comparison = FixesFarmFilterOverride\.Begin\(log, suppress\)' -or
    $plugin -notmatch 'comparison\.Complete\(context\.ModuleHandle\)' -or
    $plugin -notmatch 'ready && swap' -or
    $plugin -notmatch 'ready && farms') {
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
    $runtimeText -match 'foreach \(AppleFarmWatch farm in appleFarms\)') {
    throw 'The Canari copy-only demolition and replacement contract differs.'
}
if ($plugin -notmatch 'Config\.Bind\("FixesFarmFilterOverride", "Enabled", false' -or
    $fixesOverride -notmatch 'if \(!suppress && original\)') { throw 'Fixes suppression must be explicitly enabled.' }
foreach ($file in @($sourceFiles) + @(Get-Item (Join-Path $project 'CanarariWoodTest.csproj'), (Join-Path $project 'info.json'), (Join-Path $project 'build.bat'), $MyInvocation.MyCommand.Path)) {
    $content = [IO.File]::ReadAllText($file.FullName)
    if ($content -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF: $($file.FullName)" }
}
Write-Host 'PASS: companion owns opt-in copy-only experiments, bounded overlay/restoration and explicit Fixes override; no native hooks.'
