$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter 'Spectator*.cs' -File)
$projectFile = Get-Item -LiteralPath (Join-Path $projectRoot 'BugfixesAndQoL.csproj')
$runtimeFiles = @($sourceFiles) + @($projectFile)
$forbiddenJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility'
$forbiddenLifecycle = '\b(OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\('
$forbiddenPatch = 'CodePatch\.Write|Marshal\.Write|VirtualProtect|\.Apply\(|\.Undo\(|\.Disable\(|\.Unpatch\(|UnpatchAll\(|NativeDetour|X64InlineHook'
foreach ($file in $runtimeFiles) {
    $body = [IO.File]::ReadAllText($file.FullName)
    if ([regex]::IsMatch($body, $forbiddenJson)) { throw "Forbidden JSON dependency: $($file.FullName)" }
    if ([regex]::IsMatch($body, $forbiddenLifecycle)) { throw "Forbidden lifecycle callback: $($file.FullName)" }
    if ([regex]::IsMatch($body, $forbiddenPatch)) { throw "Unexpected executable hook mutation: $($file.FullName)" }
}
$plugin = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\BugfixesAndQoLPlugin.cs'))
if ($plugin -match '\bvoid\s+(Update|LateUpdate|FixedUpdate)\s*\(') { throw 'Plugin MonoBehaviour callback found.' }
$runtime = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorPerspectiveRuntime.cs'))
$missionStart = [regex]::Match($runtime, '(?s)private static void OnMissionStart\(MissionLifecycleNotification notification\).*?private static void OnMissionEnd\(').Value
$mapReset = [regex]::Match($runtime, '(?s)private static void BeginMapLoad\(\).*?private static void ArmRenderIfNeeded\(').Value
$advanceInitialization = [regex]::Match($runtime, '(?s)private static void AdvanceInitialization\(EngineInterface\.PlayState state\).*?private static bool TryRecognizeSavedSpectator\(').Value
if (-not $missionStart -or $missionStart -notmatch 'initializationPending = true;' -or
    $missionStart -match 'GameData\.Instance\?\.game_type' -or
    $runtime -notmatch 'state != null && \(stateBeforeLoad == null \|\| !ReferenceEquals\(state, stateBeforeLoad\)\)' -or
    $advanceInitialization -notmatch 'state\.game_type != \(int\)eGameTypeModes\.GAMETYPE_MULTIPLAYER' -or
    $advanceInitialization -notmatch 'initializationPending = false;' -or
    $advanceInitialization -notmatch 'loadedFromSave && TryRecognizeSavedSpectator\(state, out int view\)' -or
    $mapReset -notmatch 'failureLogged = false;' -or
    $mapReset -notmatch 'recoveryIdentityApplied = false;' -or
    $runtime -notmatch 'if \(preparedSessionId != notification\.Context\.SessionId\) return;\s*BeginMapLoad\(\);') {
    throw 'Fresh PlayState classification, ordinary-mission exit, save recovery or per-session error reset is incomplete.'
}
if ($runtime -notmatch 'Application\.onBeforeRender\s*\+=\s*OnPendingRender' -or
    $runtime -notmatch 'Application\.onBeforeRender\s*-=\s*OnPendingRender' -or
    $runtime -match 'Application\.onBeforeRender\s*\+=\s*OnBeforeRender') {
    throw 'Missing temporary render publisher or unsubscribe path.'
}
if ($runtime -match 'MapLoaderR3EventHooks\.' -or
    $runtime -notmatch 'ApiShared\.Current\.TryGetMissionLifecycle\(BugfixesAndQoLPlugin\.PluginGuid' -or
    $runtime -notmatch 'lifecycle\.TryRegisterObserver\("BugfixesAndQoL\.SpectatorPerspective"' -or
    $runtime -notmatch 'OnMissionStart, OnMissionEnd, OnMissionInitialization') {
    throw 'Spectator session lifecycle must be owned by APIShared.'
}
if ($runtime -notmatch 'preparedSessionId == notification\.Context\.SessionId\) return;' -or
    $runtime -notmatch 'if \(preparedSessionId != notification\.Context\.SessionId\)\s*PrepareSession\(notification\.Context\.SessionId\);' -or
    $runtime -notmatch 'if \(preparedSessionId != notification\.Context\.SessionId\) return;\s*BeginMapLoad\(\);' -or
    $runtime -notmatch 'var previousState = GameData\.Instance\?\.lastGameState;\s*BeginMapLoad\(\);\s*stateBeforeLoad = previousState;' -or
    $runtime -notmatch 'loadedFromSave = notification\.Context\.IsSave;' -or
    $runtime -notmatch 'stateBeforeLoad == null \|\| !ReferenceEquals\(state, stateBeforeLoad\)' -or
    $runtime -notmatch 'SPECTATOR_PERSPECTIVE_READY_TIMEOUT: ' -or
    $runtime -notmatch 'OnHudAvailable\(\)') {
    throw 'Session-deduplicated save load, delayed readiness or HUD recovery path missing.'
}
$bootstrap = [regex]::Match($runtime, '(?s)internal static void Initialize\(ManualLogSource logger, BugfixesAndQoLViewModel currentSettings\).*?private static void OnMissionInitialization\(').Value
if (-not $bootstrap -or
    $bootstrap -notmatch 'RegisterModDataHandler\(' -or
    $bootstrap -notmatch 'SpectatorReportHooks\.Install\(\)' -or
    $bootstrap -notmatch 'SpectatorAllyHooks\.Install\(\)' -or
    $bootstrap -notmatch 'TryRegisterObserver\(' -or
    $bootstrap -notmatch 'featureReady = true;' -or
    $bootstrap.IndexOf('featureReady = true;') -lt $bootstrap.IndexOf('TryRegisterObserver(') -or
    $bootstrap -notmatch 'catch \(Exception error\)') {
    throw 'Incomplete bootstrap must not activate spectator switching.'
}
if ($runtime -notmatch 'return featureReady && sessionEnabled && mapReady && spectatorActive' -or
    $runtime -notmatch 'return featureReady && state != null && state\.game_type == 3' -or
    $runtime -notmatch 'IsSpectatorActionRestricted\(\) => featureReady &&' -or
    $runtime -notmatch 'recoveryIdentityApplied \|\| IsActiveSpectator\(\) \|\| IsOriginalSpectator\(\)' -or
    $runtime -notmatch 'int candidate = IsActiveSpectator\(\) \? selectedPlayer : PlayerPerspectiveAPI\.GetRawNativeViewPlayerId\(\)' -or
    $runtime -match 'UnregisterModDataHandler\(|UnpatchAll\(') {
    throw 'Partially installed hooks and save handler must remain dormant when bootstrap fails.'
}
if ($runtime -notmatch 'PlayerPerspectiveAPI\.TrySetSpectatorView\(initialView\)' -or
    $runtime -notmatch 'PlayerPerspectiveAPI\.TrySetSpectatorView\(player\)' -or
    $runtime -notmatch 'PlayerPerspectiveAPI\.ClearSpectatorView\(\)' -or
    $runtime -match 'EngineInterface\.SetEditorPlayer\(') {
    throw 'Spectator view changes must be owned by APIShared.'
}
$settings = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\BugfixesAndQoLViewModel.cs'))
$settingsXaml = [IO.File]::ReadAllText((Join-Path $projectRoot 'Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml'))
if ($settings -notmatch 'private bool enableSpectatorPerspective = true;' -or
    $settings -notmatch '\[SyncHostOnly\]\s*public bool EnableSpectatorPerspective' -or
    $settings -notmatch 'EnableSpectatorPerspective = true;' -or
    $settingsXaml -notmatch 'IsChecked="\{Binding EnableSpectatorPerspective, Mode=TwoWay\}"' -or
    $runtime -notmatch 'sessionEnabled = settings\.EnableMod && settings\.EnableSpectatorPerspective;' -or
    $runtime -notmatch 'if \(!sessionEnabled\) return;' -or
    $runtime -notmatch 'featureReady && !GameModeHelper\.IsRealMultiplayer\(\)' -or
    $plugin -notmatch 'SpectatorPerspectiveRuntime\.Initialize\(Logger, Settings\)') {
    throw 'Host setting, disabled-session save safety or runtime bootstrap is incomplete.'
}
foreach ($culture in @('de-DE', 'en-US')) {
    $locale = [IO.File]::ReadAllText((Join-Path $projectRoot ("Locales\" + $culture + '.txt')))
    if ($locale -notmatch 'BugfixesAndQoL\.EnableSpectatorPerspective=' -or
        $locale -notmatch 'BugfixesAndQoL\.EnableSpectatorPerspectiveHelp=') {
        throw "Spectator host-setting localization missing: $culture"
    }
}
$reportHooks = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorReportHooks.cs'))
if ($reportHooks -notmatch 'ButtonReports' -or $reportHooks -notmatch 'ButtonChangeEdibleState' -or
    $reportHooks -notmatch 'PlayerNameText' -or $reportHooks -notmatch 'UseSelectedReportName' -or
    $reportHooks -notmatch 'replaced != 1') {
    throw 'Report navigation hook, food action guard or stable name hook missing.'
}
$foodGuard = [regex]::Match($runtime, '(?s)private static void GuardFoodControls\(\).*?private static void OnHudUnavailable\(').Value
if (-not $foodGuard -or $foodGuard -notmatch 'button\.IsHitTestVisible = false' -or
    $foodGuard -notmatch 'guardedFoodButtons\[index\]\.IsHitTestVisible = foodButtonHitTestBeforeGuard\[index\]' -or
    $foodGuard -match '\.IsEnabled\s*=') {
    throw 'Food controls must preserve their visible button style while blocking mouse hit testing.'
}
$allyHooks = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorAllyHooks.cs'))
if ($allyHooks -notmatch 'GetAllyList' -or $allyHooks -notmatch 'GetEnemyList' -or
    $allyHooks -notmatch 'UpdateAllies' -or $allyHooks -notmatch 'Ally_CancelOrders' -or
    $allyHooks -notmatch 'AllowAllyAction' -or $runtime -notmatch 'CanIssueAllyAction' -or
    $runtime -notmatch 'WaitForFreshAllies') {
    throw 'Selected-player ally view, CPU-only action guard or event-driven refresh missing.'
}
if ($allyHooks -notmatch 'if \(state && active\) RefreshControlState\(\)' -or
    $allyHooks -notmatch 'if \(SpectatorPerspectiveRuntime\.IsNetworkSpectator\(\)\)' -or
    $allyHooks -notmatch 'SpectatorPerspectiveRuntime\.IsActiveSpectator\(\) \|\|' -or
    $runtime -notmatch 'internal static bool IsNetworkSpectator\(\)' -or
    $runtime -notmatch '!state.is_valid_player\(selectedPlayer\)' -or
    $runtime -notmatch '!GameModeHelper\.IsRealMultiplayer\(\)' -or
    $runtime -match 'GameData\.Instance\.playerID\s*=') {
    throw 'Ally actions must remain local, spectator/CPU scoped without changing the managed player identity.'
}
$hud = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorPerspectiveHud.cs'))
if ($hud -notmatch 'DefaultRightInset = 52f' -or
    $hud -notmatch 'Canvas\.SetLeft\(bar, Math\.Max\(0f, canvas\.ActualWidth - bar\.Width - DefaultRightInset\)\)' -or
    $hud -notmatch 'Canvas\.SetTop\(bar, 0f\)') {
    throw 'The default HUD position must leave room for the clock and touch the top edge.'
}
if ($hud -notmatch 'SizeChanged\s*\+=' -or $hud -notmatch 'Unloaded\s*\+=' -or
    $hud -notmatch 'screen\.Loaded \+= OnScreenLoaded' -or
    $hud -notmatch 'screen\.Loaded -= OnScreenLoaded' -or
    $hud -notmatch 'canvas\.Loaded \+= OnCanvasLoaded' -or
    $hud -notmatch 'canvas\.Loaded -= OnCanvasLoaded' -or
    $hud -notmatch 'MainViewModel\.Instance' -or
    $hud -notmatch 'nextViewModel\?\.IngameUI' -or
    $hud -match 'FindGlobalElement\(' -or
    $runtime -match 'hud\.Show\(' -or $runtime -match 'RefreshReport\(') {
    throw 'Spectator HUD must update on lifecycle, selection and resize rather than every render.'
}
if ($hud -notmatch 'viewModel\.PropertyChanged \+= OnViewModelPropertyChanged' -or
    $hud -notmatch 'viewModel\.PropertyChanged -= OnViewModelPropertyChanged' -or
    $hud -notmatch 'if \(IsBriefingVisible\) \{ Hide\(\); return false; \}' -or
    $hud -notmatch 'briefingChanged\(visible\)' -or
    $runtime -notmatch 'else if \(hud\.IsBriefingVisible\)\s*hudPending = false;' -or
    $runtime -notmatch 'private static void OnBriefingChanged\(bool visible\)' -or
    $runtime -notmatch 'StopRenderIfIdle\(\);') {
    throw 'Briefing visibility must hide the bar and resume it via a detachable view-model event.'
}
if ($runtime -notmatch 'loadedFromSave && TryRecognizeSavedSpectator\(state, out int view\)' -or
    $runtime -notmatch 'saveRecoveryStage == 1' -or
    $runtime -notmatch 'saveRecoveryStage == 2' -or
    $runtime -notmatch 'EngineInterface\.GameAction\(Enums\.GameActionCommand\.SpectatorMode, 0, 0\)' -or
    $runtime -notmatch 'EditorDirector\.instance\.SetLocalPlayer\(-1\)' -or
    $runtime -notmatch 'PlayerPerspectiveAPI\.GetRawNativeViewPlayerId\(\) != selectedPlayer' -or
    $runtime -notmatch 'cpuOnly \? state\.is_skirmish_player\(player\)' -or
    $runtime -notmatch 'IsSpectatorActionRestricted\(' -or
    $runtime -notmatch 'RestoreSavedViewWithoutExtensions\(savedView\)' -or
    $runtime -notmatch 'saveRecoveryStage = 0;' -or
    $runtime -match 'GameData\.Instance\.playerID\s*=') {
    throw 'Saved spectator recovery must be staged, roster-guarded and keep network identity unchanged.'
}
$marker = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorSaveMarker.cs'))
if ($runtime -notmatch 'ModSaveDataAPI\.Instance\.RegisterModDataHandler\(SpectatorSaveMarker\.Identifier' -or
    $runtime -notmatch 'context\.IsSaveFile' -or
    $runtime -notmatch 'SpectatorSaveMarker\.Encode\(view\)' -or
    $runtime -notmatch 'GameMapArchiveManagerAPI\.Instance\.TryReadBinaryFile\(SpectatorSaveMarker\.EntryName\)' -or
    $runtime -notmatch 'SpectatorSaveMarker\.TryDecode\(marker' -or
    $runtime -notmatch 'SpectatorSavePolicy\.TryRecognizeMarked\(' -or
    $runtime -notmatch 'SpectatorSavePolicy\.TryRecognize\(' -or
    $runtime -notmatch 'else if \(markedView == 0\)\s*return false;' -or
    $marker -notmatch 'internal const string EntryName = "_SE_ModData_" \+ Identifier \+ "\.msgpack"' -or
    [IO.File]::ReadAllText($projectFile.FullName) -notmatch 'src\\SpectatorSaveMarker.cs') {
    throw 'Versioned save marker, explicit normal-save marker or current-archive recovery path missing.'
}
if ([IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorReportHooks.cs')) -notmatch 'IsSpectatorActionRestricted\(' -or
    [IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorAllyHooks.cs')) -notmatch 'IsSpectatorActionRestricted\(' -or
    [IO.File]::ReadAllText($projectFile.FullName) -notmatch 'src\\SpectatorSavePolicy.cs') {
    throw 'Pending recovery must block report and ally actions and compile the save policy.'
}
& (Join-Path $projectRoot 'Test-SpectatorSavePolicy.ps1')
if ($hud -notmatch 'SpriteMapping\.RemapMPLoadedColour\(player\)' -or
    $hud -notmatch 'SpriteMapping\.remapColours' -or $hud -notmatch 'OnScreenText\.Instance\.MPTeamColours' -or
    $hud -notmatch 'numbers\[player\]\.Foreground' -or $hud -notmatch 'bar\.Width = 12f \+ occupiedCount \* 34f' -or
    $hud -match 'buttons\[selected\]\.Content\s*=') {
    throw 'Compact HUD must use Vanilla player colours once and mark selection without replacing numbers.'
}
if ($hud -notmatch 'PreviewMouseDown \+= jumpHandlers\[player\]' -or
    $hud -notmatch 'PreviewMouseDown -= jumpHandlers\[player\]' -or
    $hud -notmatch 'args\.ChangedButton != MouseButton\.Right' -or
    $hud -notmatch 'args\.ChangedButton != MouseButton\.Middle' -or
    $hud -notmatch 'args\.Handled = true' -or
    $hud -notmatch 'jumpToPlayer\(slot, args\.ChangedButton\)') {
    throw 'Camera mouse handlers must be scoped to right/middle clicks and detached with the HUD.'
}
$cameraJump = [regex]::Match($runtime, '(?s)private static unsafe void JumpToPlayer\(.*$').Value
if (-not $cameraJump -or $cameraJump -notmatch 'IsActiveSpectator\(\)' -or
    $cameraJump -notmatch 'GetPlayerKeepId\(player\)' -or
    $cameraJump -notmatch 'GetLordUnitId\(player\)' -or
    $cameraJump -notmatch 'GetLordUnitGlobalId\(player\)' -or
    $cameraJump -notmatch 'r_AliveState != AliveState\.IsAlive' -or
    $cameraJump -notmatch 'r_PlayerIdOwner != player' -or
    $cameraJump -notmatch 'r_ControllableForPlayerId != player' -or
    $cameraJump -notmatch 'r_GlobalId != unchecked\(\(uint\)lordGlobalId\)' -or
    $cameraJump -notmatch 'SetScreenCenterToBuilding\(keepId\)' -or
    $cameraJump -notmatch 'SetScreenCenterToUnit\(lordId\)' -or
    $cameraJump -match 'SetEditorPlayer\(') {
    throw 'Camera jump must validate the live target and leave the spectator perspective unchanged.'
}
if ([IO.File]::ReadAllText($projectFile.FullName) -notmatch '0Harmony') {
    throw 'Installed Harmony reference missing.'
}
$apiDependency = [regex]::Match($plugin, 'BepInDependency\(ApiSharedGuid, "([^"]+)"\)')
if ([IO.File]::ReadAllText($projectFile.FullName) -notmatch 'APIShared' -or
    -not $apiDependency.Success -or [version]$apiDependency.Groups[1].Value -lt [version]'0.4.6') {
    throw 'Spectator perspective requires APIShared 0.4.6 or newer.'
}
if ([IO.File]::ReadAllText($projectFile.FullName) -notmatch '<AllowUnsafeBlocks>true</AllowUnsafeBlocks>') {
    throw 'Native target validation requires an unsafe-enabled project.'
}
$textFiles = @($sourceFiles) + @($projectFile) + @(
    (Get-Item -LiteralPath (Join-Path $projectRoot 'build.bat')),
    (Get-Item -LiteralPath (Join-Path $projectRoot 'Test-SpectatorPerspectivePreflight.ps1')),
    (Get-Item -LiteralPath (Join-Path $projectRoot 'Test-SpectatorSavePolicy.ps1')),
    (Get-Item -LiteralPath (Join-Path $projectRoot 'info.json')),
    (Get-Item -LiteralPath (Join-Path $projectRoot 'Patches\Assets\GUI\XAML\IngameUIScreens.xaml'))
)
foreach ($file in $textFiles) {
    $body = [IO.File]::ReadAllText($file.FullName)
    $escapedLineEnding = [string][char]92 + 'r' + [string][char]92 + 'n'
    if ([regex]::IsMatch($body, '(?<!\r)\n') -or $body.Contains($escapedLineEnding)) {
        throw "Invalid line endings: $($file.FullName)"
    }
}
[xml]$project = Get-Content -LiteralPath $projectFile.FullName -Raw
$metadata = Get-Content -LiteralPath (Join-Path $projectRoot 'info.json') -Raw | ConvertFrom-Json
$pluginVersion = [regex]::Match($plugin, 'PluginVersion\s*=\s*"([^"]+)"').Groups[1].Value
if ($metadata.GUID -ne 'BugfixesAndQoL_Serp' -or -not $pluginVersion -or $metadata.Version -ne $pluginVersion) { throw 'Mod metadata mismatch.' }
if ($metadata.NetworkMode -ne 1) { throw 'Gameplay-affecting ally actions require NetworkMode=1.' }
$patchPath = Join-Path $projectRoot 'Patches\Assets\GUI\XAML\IngameUIScreens.xaml'
[xml]$patch = Get-Content -LiteralPath $patchPath -Raw
$contents = @($patch.SelectNodes('/Patch/Operation/Content'))
if ($contents.Count -ne 2) { throw 'Expected ForeignTroopHud and spectator XAML operations.' }
if ($patch.OuterXml -notmatch 'ForeignTroopHudPanel') { throw 'Existing ForeignTroopHud XAML was lost.' }
$spectatorContent = @($contents | Where-Object { $_.OuterXml -match 'SpectatorPerspectiveCanvas' })
if ($spectatorContent.Count -ne 1) { throw 'Expected one spectator XAML operation.' }
$elements = @($spectatorContent[0].ChildNodes | Where-Object { $_.NodeType -eq [Xml.XmlNodeType]::Element })
if ($elements.Count -ne 1) { throw 'XAML Content must have exactly one direct root element.' }
$canvas = $elements[0]
if ($canvas.LocalName -ne 'Canvas' -or
    @($canvas.SelectNodes('.//*[@*[local-name()="Name"]="SpectatorPerspectiveDrag"]')).Count -ne 1 -or
    @($canvas.SelectNodes('.//*[local-name()="Button" and starts-with(@*[local-name()="Name"], "SpectatorPerspectivePlayer")]')).Count -ne 8 -or
    @($canvas.SelectNodes('.//*[local-name()="TextBlock" and starts-with(@*[local-name()="Name"], "SpectatorPerspectiveNumber")]')).Count -ne 8 -or
    $canvas.OuterXml -match 'Zuschauer|▶') {
    throw 'Compact HUD XAML must contain a drag strip and eight number-only player buttons.'
}
Write-Output 'BugfixesAndQoL spectator perspective preflight passed: JSON, lifecycle, permanent hooks, publisher, CRLF, project, metadata and XAML root.'
