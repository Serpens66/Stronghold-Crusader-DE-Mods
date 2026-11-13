[CmdletBinding()]
param([switch]$VerifyPackage)
$ErrorActionPreference = 'Stop'
# Runtime rules of the new mod: no JSON serializers, no teardown of published hooks, consistent identity.
$modDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($VerifyPackage) {
    # Compare actual bytes after MSBuild: timestamps cannot prove that the release UI is current.
    $workspaceRoot = [IO.Path]::GetDirectoryName($modDir)
    . (Join-Path $workspaceRoot 'Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1')
    $packageRoot = Join-Path $modDir 'BepInEx/plugins/Pathfinding_Serp'
    $assets = @(Get-Item -LiteralPath (Join-Path $modDir 'info.json'))
    foreach ($directory in @('Locales', 'Patches', 'Override')) {
        $assets += @(Get-ChildItem -LiteralPath (Join-Path $modDir $directory) -Recurse -File |
            Where-Object { $_.Extension -in @('.txt', '.xaml', '.png') })
    }
    foreach ($asset in $assets) {
        $relative = $asset.FullName.Substring($modDir.Length).TrimStart('\', '/')
        $packaged = Join-Path $packageRoot $relative
        if (-not (Test-Path -LiteralPath $packaged -PathType Leaf) -or
            (Get-ApiRepositoryHash $asset.FullName) -cne (Get-ApiRepositoryHash $packaged)) {
            throw "Packaged asset differs from source: $relative"
        }
    }
    Write-Output "PASS package assets: $($assets.Count) manifest, locale, patch and override files match source bytes."
    exit 0
}
$sources = [IO.Directory]::GetFiles((Join-Path $modDir 'src'), '*.cs', [IO.SearchOption]::AllDirectories)
$forbidden = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility'
foreach ($file in $sources) {
    $text = [IO.File]::ReadAllText($file)
    if ([regex]::IsMatch($text, $forbidden)) { throw "Forbidden JSON dependency: $file" }
    if ([regex]::IsMatch($text, 'void\s+(OnDestroy|OnDisable|OnApplicationQuit)\s*\(')) { throw "Forbidden lifecycle teardown: $file" }
    # The engine provider is published once; other features may roll back unpublished candidates.
    if ($file -like '*\src\Engine\*' -and [regex]::IsMatch($text, '\bruntime\??\.Dispose\s*\(')) { throw "Published runtime teardown: $file" }
    if ([regex]::IsMatch($text, '(?<!\r)\n')) { throw "Bare LF: $file" }
}
$plugin = [IO.File]::ReadAllText((Join-Path $modDir 'src\PathfindingPlugin.cs'))
$workspace = [IO.Path]::GetFullPath((Join-Path $modDir '..'))
. (Join-Path $workspace 'APIShared/tools/Validation/DependencyMetadata.Common.ps1')
$manifest = [IO.File]::ReadAllText((Join-Path $modDir 'info.json')) | ConvertFrom-Json
Assert-PluginDependencyMetadata $plugin $manifest 'Pathfinding'
if ($manifest.GUID -ne 'Pathfinding_Serp' -or $manifest.NetworkMode -ne 1) { throw 'Manifest identity mismatch.' }
if ($plugin -notmatch ('PluginVersion = "' + [regex]::Escape([string]$manifest.Version) + '"')) { throw 'Plugin version mismatch.' }
$viewModel = [IO.File]::ReadAllText((Join-Path $modDir 'src\Settings\PathfindingViewModel.cs'))
$movementRuntime = [IO.File]::ReadAllText((Join-Path $modDir 'src\TroopMovement\TroopMovementFixRuntime.cs'))
$cadence = [IO.File]::ReadAllText((Join-Path $modDir 'src\TroopMovement\SynchronizedMovementCadencePatch.cs'))
if (-not $viewModel.Contains('enableRallyTerrainSlowdownFix = true') -or
    -not $viewModel.Contains('[SyncHostOnly] public bool EnableRallyTerrainSlowdownFix') -or
    -not $viewModel.Contains('EnableRallyTerrainSlowdownFix = true;') -or
    -not $movementRuntime.Contains('SetTerrainEnabled(settings.EnableMod && settings.EnableRallyTerrainSlowdownFix)') -or
    -not $movementRuntime.Contains('(settings.EnableTroopMovementFix || settings.EnableRallyTerrainSlowdownFix)')) {
    throw 'Independent host-synchronized rally terrain setting, reset or main switch is missing.'
}
if (-not $cadence.Contains('Interlocked.CompareExchange(ref *terrainEnabledFlag,') -or
    -not $cadence.Contains('terrainCaptureHook.Hook.DisplacedByteCount != 14') -or
    -not $cadence.Contains('returnAddress != instructions[0].IP + 14') -or
    -not $cadence.Contains('EmitTerrainRestore(assembler);') -or
    -not $cadence.Contains('terrainTransaction?.Dispose();')) {
    throw 'Rally terrain native publication, epoch or boundary contract is missing.'
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $modDir 'Locales') -Filter '*.txt') {
    $text = [IO.File]::ReadAllText($file.FullName)
    foreach ($key in @('Pathfinding.EnableRallyTerrainSlowdownFix', 'Pathfinding.EnableRallyTerrainSlowdownFixHelp')) {
        if ([regex]::Matches($text, '(?m)^' + [regex]::Escape($key) + '=.+$').Count -ne 1) {
            throw "Missing or duplicate rally terrain localization: $($file.Name), $key"
        }
    }
}
# Guardrail for literal text keys ("Pathfinding.Name" in code): a key without a locale entry shows up raw
# in game. Keys composed at runtime are outside this check.
$literalKeys = @($sources | ForEach-Object { [regex]::Matches([IO.File]::ReadAllText($_), '"(Pathfinding\.[A-Z][A-Za-z0-9]*)"') } |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $modDir 'Locales') -Filter '*.txt') {
    $text = [IO.File]::ReadAllText($file.FullName)
    foreach ($key in $literalKeys) {
        if ([regex]::Matches($text, '(?m)^' + [regex]::Escape($key) + '=.+$').Count -ne 1) {
            throw "Missing or duplicate localization for a key used in code: $($file.Name), $key"
        }
    }
}
[xml]$settingsXaml =[IO.File]::ReadAllText((Join-Path $modDir 'Override\ScriptExtenderUI\PathfindingSettings.xaml'))
if ($settingsXaml.OuterXml -notmatch 'EnableRallyTerrainSlowdownFix, Mode=TwoWay') { throw 'Rally terrain UI integration is missing.' }
Write-Output "PASS preflight: $($sources.Count) runtime sources, JSON and lifecycle rules, CRLF, manifest identity."
