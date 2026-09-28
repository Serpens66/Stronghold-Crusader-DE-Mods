$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' -File)
$projectFile = Get-Item -LiteralPath (Join-Path $projectRoot 'SpectatorPerspectiveTest.csproj')
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
$plugin = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorPerspectivePlugin.cs'))
if ($plugin -match '\bvoid\s+(Update|LateUpdate|FixedUpdate)\s*\(') { throw 'Plugin MonoBehaviour callback found.' }
$runtime = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorPerspectiveRuntime.cs'))
if ($runtime -notmatch 'Application\.onBeforeRender\s*\+=\s*OnPendingRender' -or
    $runtime -notmatch 'Application\.onBeforeRender\s*-=\s*OnPendingRender' -or
    $runtime -notmatch 'SPECTATOR_PERSPECTIVE_RUNTIME_ALIVE' -or
    $runtime -match 'Application\.onBeforeRender\s*\+=\s*OnBeforeRender') {
    throw 'Missing temporary post-cleanup render publisher, unsubscribe path or runtime marker.'
}
if ($runtime -notmatch 'MapLoaderR3EventHooks\.OnPostLoad' -or $runtime -notmatch 'MapLoaderR3EventHooks\.OnUnloadMap') {
    throw 'Missing map lifecycle event registration.'
}
if ($runtime -notmatch 'PlayerPerspectiveAPI\.TrySetSpectatorView\(first\)' -or
    $runtime -notmatch 'PlayerPerspectiveAPI\.TrySetSpectatorView\(player\)' -or
    $runtime -notmatch 'PlayerPerspectiveAPI\.ClearSpectatorView\(\)' -or
    $runtime -match 'EngineInterface\.SetEditorPlayer\(') {
    throw 'Spectator view changes must be owned by APIShared.'
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
    $runtime -notmatch '!state.is_valid_player\(selectedPlayer\)' -or
    $runtime -match 'GameData\.Instance\.playerID\s*=') {
    throw 'Ally actions must remain spectator/CPU scoped without changing the managed player identity.'
}
$hud = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorPerspectiveHud.cs'))
if ($hud -notmatch 'SizeChanged\s*\+=' -or $hud -notmatch 'Unloaded\s*\+=' -or
    $runtime -match 'hud\.Show\(' -or $runtime -match 'RefreshReport\(') {
    throw 'Spectator HUD must update on lifecycle, selection and resize rather than every render.'
}
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
if ([IO.File]::ReadAllText($projectFile.FullName) -notmatch 'APIShared' -or
    $plugin -notmatch 'BepInDependency\("APIShared_Serp", "0\.4\.6"\)') {
    throw 'APIShared 0.4.6 dependency missing.'
}
if ([IO.File]::ReadAllText($projectFile.FullName) -notmatch '<AllowUnsafeBlocks>true</AllowUnsafeBlocks>') {
    throw 'Native target validation requires an unsafe-enabled project.'
}
$textFiles = @($sourceFiles) + @($projectFile) + @(
    (Get-Item -LiteralPath (Join-Path $projectRoot 'build.bat')),
    (Get-Item -LiteralPath (Join-Path $projectRoot 'Verify-SpectatorPerspective.ps1')),
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
if ($metadata.GUID -ne 'SpectatorPerspectiveTest_Serp' -or $metadata.Version -ne '0.1.0') { throw 'Mod metadata mismatch.' }
if ($metadata.NetworkMode -ne 1) { throw 'Gameplay-affecting ally actions require NetworkMode=1.' }
$patchPath = Join-Path $projectRoot 'Patches\Assets\GUI\XAML\IngameUIScreens.xaml'
[xml]$patch = Get-Content -LiteralPath $patchPath -Raw
$contents = @($patch.SelectNodes('/Patch/Operation/Content'))
if ($contents.Count -ne 1) { throw 'Expected exactly one XAML Content node.' }
$elements = @($contents[0].ChildNodes | Where-Object { $_.NodeType -eq [Xml.XmlNodeType]::Element })
if ($elements.Count -ne 1) { throw 'XAML Content must have exactly one direct root element.' }
$canvas = $elements[0]
if ($canvas.LocalName -ne 'Canvas' -or
    @($canvas.SelectNodes('.//*[@*[local-name()="Name"]="SpectatorPerspectiveDrag"]')).Count -ne 1 -or
    @($canvas.SelectNodes('.//*[local-name()="Button" and starts-with(@*[local-name()="Name"], "SpectatorPerspectivePlayer")]')).Count -ne 8 -or
    @($canvas.SelectNodes('.//*[local-name()="TextBlock" and starts-with(@*[local-name()="Name"], "SpectatorPerspectiveNumber")]')).Count -ne 8 -or
    $canvas.OuterXml -match 'Zuschauer|▶') {
    throw 'Compact HUD XAML must contain a drag strip and eight number-only player buttons.'
}
Write-Output 'SpectatorPerspectiveTest preflight passed: JSON, lifecycle, permanent hooks, publisher, CRLF, project, metadata and XAML root.'
