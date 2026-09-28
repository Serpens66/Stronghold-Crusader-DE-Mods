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
$reportHooks = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorReportHooks.cs'))
if ($reportHooks -notmatch 'ButtonReports' -or $reportHooks -notmatch 'ButtonChangeEdibleState' -or
    $reportHooks -notmatch 'PlayerNameText' -or $reportHooks -notmatch 'UseSelectedReportName' -or
    $reportHooks -notmatch 'replaced != 1' -or $runtime -notmatch 'SPECTATOR_REPORT_HOOKS_READY') {
    throw 'Report navigation hook, food action guard, stable name hook or post-startup marker missing.'
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
    $runtime -notmatch 'WaitForFreshAllies' -or $runtime -notmatch 'SPECTATOR_ALLY_HOOKS_READY') {
    throw 'Selected-player ally view, CPU-only action guard or event-driven refresh missing.'
}
if ($allyHooks -notmatch 'state && SpectatorPerspectiveRuntime.IsActiveSpectator\(\)' -or
    $runtime -notmatch '!state.is_valid_player\(selectedPlayer\)' -or
    $runtime -match 'GameData\.Instance\.playerID\s*=') {
    throw 'Ally actions must remain spectator/CPU scoped without changing the managed player identity.'
}
$hud = [IO.File]::ReadAllText((Join-Path $projectRoot 'src\SpectatorPerspectiveHud.cs'))
if ($hud -notmatch 'SizeChanged\s*\+=' -or $hud -notmatch 'Unloaded\s*\+=' -or
    $runtime -match 'hud\.Show\(' -or $runtime -match 'RefreshReport\(') {
    throw 'Spectator HUD must update on lifecycle, selection and resize rather than every render.'
}
if ([IO.File]::ReadAllText($projectFile.FullName) -notmatch '0Harmony') {
    throw 'Installed Harmony reference missing.'
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
Write-Output 'SpectatorPerspectiveTest preflight passed: JSON, lifecycle, permanent hooks, publisher, CRLF, project, metadata and XAML root.'
