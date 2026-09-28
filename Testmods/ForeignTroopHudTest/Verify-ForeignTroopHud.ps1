$ErrorActionPreference = 'Stop'
$modDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$files = @(
    (Join-Path $modDir 'src\ForeignTroopHudPlugin.cs'),
    (Join-Path $modDir 'src\ForeignTroopHudRuntime.cs'),
    (Join-Path $modDir 'src\ForeignTroopHudView.cs'),
    (Join-Path $modDir 'ForeignTroopHudTest.csproj'),
    (Join-Path $modDir 'Patches\Assets\GUI\XAML\IngameUIScreens.xaml'),
    (Join-Path $modDir 'info.json'),
    (Join-Path $modDir 'build.bat'),
    $MyInvocation.MyCommand.Path,
    (Join-Path $modDir '..\..\Shared\LordPortraitPalette.cs')
)
foreach ($file in $files) {
    $text = [IO.File]::ReadAllText($file)
    if ($file -ne $MyInvocation.MyCommand.Path -and $text.Contains('\r\n')) { throw "Literal newline escape found: $file" }
    if ([regex]::IsMatch($text, '(?<!\r)\n')) { throw "Bare LF found: $file" }
}
$sources = [IO.File]::ReadAllText($files[0]) + [IO.File]::ReadAllText($files[1]) + [IO.File]::ReadAllText($files[2]) + [IO.File]::ReadAllText($files[8])
$project = [IO.File]::ReadAllText($files[3])
$forbidden = @(
    'System\.Web\.Extensions', 'JavaScriptSerializer', 'System\.Text\.Json', 'Newtonsoft\.Json',
    'DataContractJsonSerializer', 'JsonUtility', 'OnDestroy\s*\(', 'OnDisable\s*\(',
    'OnApplicationQuit\s*\(', 'StartCoroutine\s*\(',
    '(?m)^\s*(?:private|public|internal|protected)\s+(?:override\s+)?(?:void|IEnumerator)\s+(?:Update|LateUpdate|FixedUpdate)\s*\(',
    '\.Dispose\s*\(', '\.Undo\s*\(', '\.Disable\s*\(', 'VirtualProtect', 'CodePatch\.Write',
    'Marshal\.Write', 'NativeDetour', 'X64InlineHook', 'HookTransaction'
)
foreach ($pattern in $forbidden) {
    if ([regex]::IsMatch($sources + $project, $pattern)) { throw "Forbidden runtime pattern: $pattern" }
}
if ($sources -notmatch 'Application\.onBeforeRender\s*\+=\s*OnBeforeRender') { throw 'Persistent publisher missing.' }
if ($sources -notmatch 'TryGetMissionLifecycle' -or $sources -notmatch 'TryRegisterObserver') { throw 'APIShared mission lifecycle missing.' }
if ($sources -notmatch 'session\.IsEditor' -or $sources -notmatch 'ActivePlayerID' -or $sources -notmatch 'spectatorMode') { throw 'Editor or spectator player handling missing.' }
if ($sources -match 'app_mode\s*!=\s*14|MapLoaderR3EventHooks') { throw 'Legacy mode or map gate remains.' }
if ($sources -notmatch 'FOREIGN_TROOP_HUD_RUNTIME_ALIVE') { throw 'Post-cleanup marker missing.' }
if ($sources -notmatch 'FOREIGN_TROOP_HUD_DIAGNOSTIC' -or
    $sources -notmatch 'now - lastDiagnosticAt < 5f' -or
    $sources -notmatch 'ReportStatus\("no-foreign-marked-units"' -or
    $sources -notmatch 'ReportStatus\("shown"' -or
    $sources -notmatch 'ReportStatus\("missing-vanilla-element:"' -or
    $sources -notmatch 'ReportStatus\("missing-mod-element:"' -or
    $sources -notmatch 'FindNamed<FrameworkElement>\(main\.HUDTroopPanel' -or
    $sources -notmatch 'FindNamed<Canvas>\(screen, "IngameUI", "ForeignTroopHudPanel"' -or
    $sources -notmatch 'host\.FindName\(name\)' -or
    $sources -notmatch ':not-found' -or $sources -notmatch ':wrong-type:actual=' -or
    $sources -match 'FindElementByName\(|FindGlobalElement\(') {
    throw 'Diagnostic state, heartbeat, or exact missing-element reporting is incomplete.'
}
if ($sources -notmatch 'r_UnitHover' -or $sources -match 'r_UnitHover\s*=(?!=)') { throw 'Hover marker must be read only.' }
if ($sources -notmatch 'pixel\.r = pixel\.g = pixel\.b = 0' -or
    $sources -notmatch 'pixel\.r \* pixel\.a \+ 127' -or
    $sources -notmatch 'Canvas\.SetTop\(portraits\[i\], entry\.Type == 55 \? 65f : 53f\)') {
    throw 'Lord alpha and type-specific portrait placement contract missing.'
}
if ($sources -match 'r_UnitSelected\s*=(?!=)|GetSelectedChimps\s*\(') { throw 'Native command selection must not be changed.' }
if ($sources -match 'r_CurrentHealth\s*=(?!=)|r_MaxHealth\s*=(?!=)|EngineInterface\.GameAction\s*\(') { throw 'HUD must not change HP or issue commands.' }
if ($sources -match 'Show_HUD_Main\s*=' -or $sources -match 'Show_HUD_Book\s*=' -or
    $sources -match 'TroopsSelectedGameAction\s*\(' -or
    $sources -notmatch 'main\.Show_HUD_Troops\s*=\s*true' -or
    $sources -notmatch 'main\.Show_HUD_Troops\s*=\s*false' -or
    $sources -notmatch 'new RectangleGeometry\(new Rect\(130, 0, 670, 155\)\)' -or
    $sources -notmatch 'new RectangleGeometry\(new Rect\(660, 0, 254, 306\)\)' -or
    $sources -notmatch '"StanceTabs", "UnitControls"' -or
    $sources -notmatch 'troopRoot\.IsHitTestVisible\s*=\s*false') {
    throw 'Vanilla troop frame, safe input state, and unchanged right HUD contract missing.'
}
$xamlFiles = Get-ChildItem -LiteralPath (Join-Path $modDir 'Patches') -Recurse -Filter '*.xaml' -File
foreach ($xamlFile in $xamlFiles) {
    [xml]$document = Get-Content -LiteralPath $xamlFile.FullName -Raw
    foreach ($content in $document.SelectNodes('//*[local-name()="Content"]')) {
        $roots = @($content.ChildNodes | Where-Object { $_.NodeType -eq [Xml.XmlNodeType]::Element })
        if ($roots.Count -ne 1) { throw "XAML Content must have one element: $($xamlFile.FullName)" }
    }
}
$hudPatch = [IO.File]::ReadAllText($files[4])
$portraitButtons = @([regex]::Matches($hudPatch, '(?s)<Button x:Name="ForeignTroopImage\d+".*?/>'))
if ($portraitButtons.Count -ne 8) { throw 'Expected eight read-only portrait buttons.' }
foreach ($portrait in $portraitButtons) {
    if ($portrait.Value -notmatch 'IsHitTestVisible="False"' -or
        $portrait.Value -notmatch 'Focusable="False"' -or
        $portrait.Value -notmatch 'Style="\{StaticResource BTN_Image\}"' -or
        $portrait.Value -match 'Command=|Click=|EventTrigger') {
        throw 'A portrait button is interactive or lacks the Vanilla visual style.'
    }
}
if ($hudPatch -notmatch "XPath=.*/n:Grid\[@x:Name='MainHUD'\]" -or
    [regex]::Matches($hudPatch, '<Button\s').Count -ne 10 -or
    [regex]::Matches($hudPatch, 'Canvas.Top="53"').Count -ne 16 -or
    [regex]::Matches($hudPatch, 'Canvas.Left="14" Canvas.Top="56"').Count -ne 8 -or
    $hudPatch -notmatch 'Width="416" Height="170" Margin="0,0,242,0"' -or
    $hudPatch -match 'UI-HUD 006|Command=|Control_Group|ForeignTroopHudCanvas') {
    throw 'HUD patch must be inside MainHUD with only two local page buttons.'
}
$workspace = Split-Path -Parent (Split-Path -Parent $modDir)
$gitDiff = & git -C $workspace diff --unified=0 -- '*.cs' '*.csproj'
if ($LASTEXITCODE -ne 0) { throw 'Workspace hook regression diff failed.' }
$newLines = @($gitDiff | Where-Object { $_ -match '^\+[^+]' }) -join "`n"
if ([regex]::IsMatch($newLines, 'CodePatch\.Write|Marshal\.Write|VirtualProtect|\.Undo\s*\(|\.Disable\s*\(|NativeDetour.*Dispose')) {
    throw 'Workspace diff contains a new executable patch or hook teardown; audit before build.'
}
Write-Host 'ForeignTroopHud preflight passed: lifecycle, JSON, hooks, publisher, XAML, CRLF, selection.'
