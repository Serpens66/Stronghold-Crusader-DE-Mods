param([Parameter(Mandatory = $true)][string]$ProjectDir)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath($ProjectDir)
$textFiles = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.Extension -in '.cs', '.csproj', '.json', '.bat', '.ps1', '.md', '.xaml' -and
    $_.FullName -notmatch '[\\/](bin|obj|BepInEx)[\\/]'
})
$runtimeFiles = @($textFiles | Where-Object { $_.Extension -in '.cs', '.csproj' })
$sourceFiles = @($textFiles | Where-Object { $_.Extension -eq '.cs' })
$pluginFiles = @($sourceFiles | Where-Object { $_.Name -like '*Plugin.cs' })
$forbiddenJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization'
$forbiddenLifecycle = '\b(OnDestroy|OnDisable|OnApplicationQuit)\s*\('
$forbiddenPluginLoop = '\b(Update|LateUpdate|FixedUpdate|StartCoroutine|InvokeRepeating)\s*\('

if ($runtimeFiles | Select-String -Pattern $forbiddenJson) { throw 'Forbidden runtime JSON dependency or serializer found.' }
if ($sourceFiles | Select-String -Pattern $forbiddenLifecycle) { throw 'Forbidden Unity teardown lifecycle method found.' }
if ($pluginFiles | Select-String -Pattern $forbiddenPluginLoop) { throw 'Forbidden long-lived MonoBehaviour callback found in a plugin class.' }
$xamlFiles = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.Extension -eq '.xaml' -and $_.FullName -notmatch '[\\/](bin|obj|BepInEx)[\\/]'
})
foreach ($file in $xamlFiles) {
    [xml]$document = Get-Content -LiteralPath $file.FullName -Raw
    if ($document.DocumentElement.LocalName -ne 'Patch') { continue }
    foreach ($content in @($document.SelectNodes('//*[local-name()="Content"]'))) {
        $roots = @($content.ChildNodes | Where-Object { $_.NodeType -eq [System.Xml.XmlNodeType]::Element })
        if ($roots.Count -ne 1) { throw "XAML patch Content must have exactly one direct root: $($file.FullName)" }
    }
}

$runtimeSource = [System.IO.File]::ReadAllText((Join-Path $root 'src\WaterboyTargetReservationRuntime.cs'))
if ($runtimeSource -match '\btargetSearchHook\s*\.\s*(Dispose|Undo|Disable)\s*\(') {
    throw 'Published runtime hook teardown found.'
}
$forbiddenButtonArtifacts = 'WaterboyButtonViewModel|WaterboyModePacket|setUpInbuilding|TrySendModeChore|MessagePack|Noesis|HUD_Buildings\.xaml'
if ($runtimeFiles | Select-String -Pattern $forbiddenButtonArtifacts) {
    throw 'Removed Waterboy button, Chore, or UI-hook artifacts remain in runtime sources or project files.'
}
$removedPaths = @(
    Join-Path $root 'src\WaterboyButtonViewModel.cs'
    Join-Path $root 'src\WaterboyModePacket.cs'
    Join-Path $root 'Patches\Assets\GUI\XAMLResources\HUD_Buildings.xaml'
)
foreach ($path in $removedPaths) {
    if (Test-Path -LiteralPath $path) { throw "Removed Waterboy UI artifact still exists: $path" }
}
$settingsSource = [System.IO.File]::ReadAllText((Join-Path $root 'src\WaterboySettings.cs'))
if (-not $settingsSource.Contains('[SyncPerPlayer]') -or
    -not $settingsSource.Contains('EnableNearestWaterboyTargetingData') -or
    -not $settingsSource.Contains('ResetSlotsWith(nameof(EnableNearestWaterboyTargeting), () => true)') -or
    -not $settingsSource.Contains('ResolveEffectiveMode')) {
    throw 'The default-enabled SyncPerPlayer companion contract is incomplete.'
}
if (-not $runtimeSource.Contains('settings.ResolveEffectiveMode(realMultiplayer, playerId, localPlayerId)') -or
    -not $runtimeSource.Contains('source={source}') -or
    -not $runtimeSource.Contains('loadedSave={loadedSave}')) {
    throw 'Per-search mode resolution or per-map source diagnostics are incomplete.'
}

foreach ($file in $textFiles) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    if ([regex]::IsMatch($text, '(?<!\r)\n')) { throw "Non-CRLF newline found: $($file.FullName)" }
    $literalEscapedNewline = [string][char]92 + 'r' + [char]92 + 'n'
    if ($text.Contains($literalEscapedNewline)) { throw "Literal escaped newline sequence found: $($file.FullName)" }
}

$versionFiles = @(
    Join-Path $root 'info.json'
    Join-Path $root 'Properties\AssemblyInfo.cs'
    Join-Path $root 'src\WaterboyTargetReservationPlugin.cs'
)
foreach ($file in $versionFiles) {
    $text = [System.IO.File]::ReadAllText($file)
    if (-not $text.Contains('0.1.0')) { throw "Active version 0.1.0 missing: $file" }
}

Write-Host 'WaterboyTargetReservationTest preflight passed.'
