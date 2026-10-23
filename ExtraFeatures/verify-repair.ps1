[CmdletBinding()]
param([string]$GameDir = $env:SHCDE_GAME_DIR, [string]$ExtenderDir = $env:SHCDESE_EXTENDER_DIR, [switch]$SkipSharedPreflight)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..')).Path
if (-not $GameDir) { $GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition' }
$game = $GameDir
if (-not $ExtenderDir) { $ExtenderDir = Join-Path $game 'BepInEx/plugins/000shcdese' }
if (-not $SkipSharedPreflight) {
    & (Join-Path $workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') -ModDirectory 'ExtraFeatures'
    if (-not $?) { throw 'Shared runtime preflight failed.' }
}
$native = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$managed = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'
$expectedNative = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
$expectedManaged = 'BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789'
function Get-Sha256([string]$path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($path)
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $stream.Dispose(); $sha.Dispose() }
}
if ((Get-Sha256 $native) -ne $expectedNative) { throw 'Installed native DLL is not the audited repair build.' }
if ((Get-Sha256 $managed) -ne $expectedManaged) { throw 'Installed managed DLL is not the audited repair build.' }

$runtimeFiles = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' -File)
$projectFile = Join-Path $project 'ExtraFeatures.csproj'
$info = Join-Path $project 'info.json'
$asset = Join-Path $project 'Override\Assets\GUI\Sprites\ExtraFeatures_RepairHammer.png'
if (-not (Test-Path -LiteralPath $asset)) { throw 'Repair hammer image is missing.' }
$textFiles = @($runtimeFiles | ForEach-Object { $_.FullName }) + @(
    $projectFile, $info, (Join-Path $project 'README.md'),
    (Join-Path $project 'build.bat'), $MyInvocation.MyCommand.Path) +
    @(Get-ChildItem -LiteralPath (Join-Path $project 'Locales') -File -Filter '*.txt' | ForEach-Object { $_.FullName }) +
    @(Get-ChildItem -LiteralPath (Join-Path $project 'Patches') -Recurse -File -Filter '*.xaml' | ForEach-Object { $_.FullName }) +
    @(Get-ChildItem -LiteralPath (Join-Path $project 'Override') -Recurse -File -Filter '*.xaml' | ForEach-Object { $_.FullName })
$runtimeText = (@($runtimeFiles | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) +
    @([IO.File]::ReadAllText($projectFile))) -join "`n"
# JSON, lifecycle, ownership and permanent-hook guardrails run once in Shared preflight.
# Exact native/managed fingerprints above remain audited compatibility contracts;
# updating them requires the feature audit described in UpdateToNewDLL.md.
foreach ($path in $textFiles) {
    $content = [IO.File]::ReadAllText($path)
    if ($content -match '(?<!\r)\n' -or $content -match '(?<!\r)\r(?!\n)') {
        throw "Non-CRLF line ending: $path"
    }
    $literalEscapedNewline = ([string][char]92) + 'r' + ([string][char]92) + 'n'
    if ($content.Contains($literalEscapedNewline)) { throw "Literal escaped newline in $path" }
}
foreach ($path in $textFiles | Where-Object { $_ -like '*.xaml' }) {
    [xml]$patch = [IO.File]::ReadAllText($path)
    foreach ($content in $patch.SelectNodes('//*[local-name()="Content"]')) {
        $roots = @($content.ChildNodes | Where-Object { $_.NodeType -eq [Xml.XmlNodeType]::Element })
        if ($roots.Count -ne 1) { throw "XAML Content must have one root: $path" }
    }
}
$settingsXaml = Join-Path $project 'Override\ScriptExtenderUI\ExtraFeaturesSettings.xaml'
[xml]$settingsDocument = [IO.File]::ReadAllText($settingsXaml)
foreach ($binding in @('HealCivilianTargets','HealSiegeTargets','EnableBuildingRepair')) {
    $control = @($settingsDocument.SelectNodes('//*[local-name()="CheckBox"]') | Where-Object {
        $_.GetAttribute('IsChecked') -match ('^\{Binding\s+' + $binding + '(?:\s*,|\s*\})')
    })
    if ($control.Count -ne 1 -or -not $control[0].SelectSingleNode('ancestor::*[@IsEnabled="{Binding CanEditHostSettings}"]')) {
        throw "Missing or unguarded host control: $binding"
    }
}
if (-not $settingsDocument.SelectSingleNode('//*[local-name()="CheckBox" and contains(@IsChecked,"EnableBuildingRepair")]//*[local-name()="Image" and @Source="{Binding BuildingRepairIcon}"]')) {
    throw 'Building repair settings icon binding is missing.'
}
$activeVersion = [regex]::Match($runtimeText, 'PluginVersion\s*=\s*"([^"]+)"').Groups[1].Value
$minimumVersion = [regex]::Match($runtimeText,
    'BepInDependency\(ScriptExtenderGuid,\s*"([^"]+)"\)').Groups[1].Value
# Generated package metadata is replaced after the build. Validate authoritative
# source metadata here; the update driver's post-build hash audit checks the package.
foreach ($path in @($info)) {
    $metadata = [IO.File]::ReadAllText($path) | ConvertFrom-Json
    if (-not $activeVersion -or -not $minimumVersion -or $metadata.Version -ne $activeVersion -or $metadata.MinimumScriptExtenderVersion -ne $minimumVersion -or
        $metadata.SerpChangelog[0].Version -ne $activeVersion) {
        throw "Active version mismatch: $path"
    }
}
if (-not $activeVersion) {
    throw 'Plugin version is missing.'
}
Write-Host 'Repair preflight passed: audited binary hashes, source assets, XAML, CRLF and version consistency.'
& (Join-Path $project 'Test-KeepBuildRange.ps1') -ExtenderDir $ExtenderDir
if (-not $?) { throw 'Keep build range tests failed.' }
& (Join-Path $project 'Test-GatehouseQueryRange.ps1')
if (-not $?) { throw 'Gatehouse query range tests failed.' }
