$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..')).Path
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
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
$repairSource = Join-Path $project 'src\BuildingRepairHudRuntime.cs'
$healerSource = Join-Path $project 'src\HealerTargetsRuntime.cs'
$healerContract = Join-Path $project 'src\HealerNativeContract.cs'
$info = Join-Path $project 'info.json'
$packagedInfo = Join-Path $project 'BepInEx\plugins\ExtraFeatures_Serp\info.json'
$asset = Join-Path $project 'Override\Assets\GUI\Sprites\ExtraFeatures_RepairHammer.png'
if (-not (Test-Path -LiteralPath $asset)) { throw 'Repair hammer image is missing.' }
$textFiles = @($runtimeFiles | ForEach-Object { $_.FullName }) + @(
    $projectFile, $info, $packagedInfo, (Join-Path $project 'README.md'),
    (Join-Path $project 'build.bat'), $MyInvocation.MyCommand.Path) +
    @(Get-ChildItem -LiteralPath (Join-Path $project 'Locales') -File -Filter '*.txt' | ForEach-Object { $_.FullName }) +
    @(Get-ChildItem -LiteralPath (Join-Path $project 'Patches') -Recurse -File -Filter '*.xaml' | ForEach-Object { $_.FullName }) +
    @(Get-ChildItem -LiteralPath (Join-Path $project 'Override') -Recurse -File -Filter '*.xaml' | ForEach-Object { $_.FullName })
$runtimeText = (@($runtimeFiles | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) +
    @([IO.File]::ReadAllText($projectFile))) -join "`n"
$forbiddenJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
if ($runtimeText -match $forbiddenJson) { throw 'Forbidden runtime JSON dependency.' }
if ($runtimeText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause)\s*\(') {
    throw 'Runtime lifecycle teardown is forbidden.'
}
foreach ($file in $runtimeFiles | Where-Object { $_.Name -like '*Plugin.cs' }) {
    if ([IO.File]::ReadAllText($file.FullName) -match '\b(Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') {
        throw "Long-lived MonoBehaviour callback in $($file.Name)."
    }
}
$repairText = [IO.File]::ReadAllText($repairSource)
if (@([regex]::Matches($repairText, '\.Undo\s*\(|\.Dispose\s*\(')).Count -ne 4 -or
    $repairText -notmatch 'Only an unpublished initialization candidate can be rolled back') {
    throw 'Building repair hook teardown changed; inspect candidate-only rollback.'
}
if ($repairText -match 'Assembly-CSharp-publicized') { throw 'Publicized assembly reference detected.' }
$healerText = [IO.File]::ReadAllText($healerSource)
$healerContractText = [IO.File]::ReadAllText($healerContract)
if ($healerText -notmatch 'listBuilder\.Original\(manager, playerId\)' -or
    $healerText -notmatch 'HEALER_TARGETS_POST_STARTUP' -or
    $healerText -notmatch 'Volatile\.Write\(ref enabledMask' -or
    $healerText -notmatch 'HealerListBounds\.IsUsableNextUnitId' -or
    $healerText -notmatch 'HealerListBounds\.IsUsableListCount' -or
    $healerText -match 'TraceCatapult|HEALER_TARGETS_DIAGNOSTIC|HEALER_TARGETS_LIST_SKIPPED' -or
    $healerContractText -notmatch 'ValidateInstalledDetour' -or
    $healerContractText -notmatch 'ProbeBackend' -or
    $healerContractText -notmatch 'probe\.Enable\(\)' -or
    $healerContractText -notmatch 'probe\?\.Dispose\(\)') {
    throw 'Healer hook, minimal logging or copied backend probe contract changed.'
}
$civilianCases = @([regex]::Matches([regex]::Match($healerText,
    '(?s)private static bool IsHumanCivilian\(eChimps type\)(.*?)private void OnTick').Groups[1].Value,
    'case eChimps\.(CHIMP_TYPE_[A-Z0-9_]+):') | ForEach-Object { $_.Groups[1].Value })
$siegeCases = @([regex]::Matches([regex]::Match($healerText,
    '(?s)private static bool IsSiegeEngine\(eChimps type\)(.*?)private static bool IsHumanCivilian').Groups[1].Value,
    'case eChimps\.(CHIMP_TYPE_[A-Z0-9_]+):') | ForEach-Object { $_.Groups[1].Value })
$expectedCivilians = @(
    'CHIMP_TYPE_PEASANT','CHIMP_TYPE_WOODCUTTER','CHIMP_TYPE_FLETCHER',
    'CHIMP_TYPE_HUNTER','CHIMP_TYPE_QUARRY_MASON','CHIMP_TYPE_QUARRY_GRUNT',
    'CHIMP_TYPE_PITCHMAN','CHIMP_TYPE_FARMER_WHEAT','CHIMP_TYPE_FARMER_HOPS',
    'CHIMP_TYPE_FARMER_APPLE','CHIMP_TYPE_FARMER_CATTLE','CHIMP_TYPE_MILLER',
    'CHIMP_TYPE_BAKER','CHIMP_TYPE_BREWER','CHIMP_TYPE_POLETURNER',
    'CHIMP_TYPE_BLACKSMITH','CHIMP_TYPE_ARMOURER','CHIMP_TYPE_TANNER',
    'CHIMP_TYPE_PRIEST','CHIMP_TYPE_HEALER','CHIMP_TYPE_DRUNKARD',
    'CHIMP_TYPE_INNKEEPER','CHIMP_TYPE_TRADER','CHIMP_TYPE_FIREMAN',
    'CHIMP_TYPE_LADY','CHIMP_TYPE_JESTER','CHIMP_TYPE_MOTHER',
    'CHIMP_TYPE_CHILD','CHIMP_TYPE_JUGGLER','CHIMP_TYPE_FIREEATER'
)
$expectedSiege = @(
    'CHIMP_TYPE_CATAPULT','CHIMP_TYPE_TREBUCHET','CHIMP_TYPE_MANGONEL',
    'CHIMP_TYPE_SIEGE_TOWER','CHIMP_TYPE_BATTERING_RAM','CHIMP_TYPE_PORTABLE_SHIELD',
    'CHIMP_TYPE_BALLISTA','CHIMP_TYPE_ARAB_BALLISTA'
)
if ($civilianCases.Count -ne 30 -or $siegeCases.Count -ne 8 -or
    [string]::Join('|', @($civilianCases | Sort-Object)) -cne
    [string]::Join('|', @($expectedCivilians | Sort-Object)) -or
    [string]::Join('|', @($siegeCases | Sort-Object)) -cne
    [string]::Join('|', @($expectedSiege | Sort-Object))) {
    throw 'Healer civilian or siege whitelist differs from the reviewed target policy.'
}
$enumText = & ilspycmd -t SHCDESE.Interop.eChimps (Join-Path $game 'BepInEx\plugins\000shcdese\SHCDESE.dll')
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the installed eChimps enum.' }
$enumNames = @($enumText | Select-String -Pattern '^\s*(CHIMP_[A-Za-z0-9_]+)' |
    ForEach-Object { $_.Matches[0].Groups[1].Value } |
    Where-Object { $_ -ne 'CHIMP_NUM_TYPES' })
$enumHasher = [Security.Cryptography.SHA256]::Create()
try {
    $enumHash = [BitConverter]::ToString($enumHasher.ComputeHash(
        [Text.Encoding]::UTF8.GetBytes([string]::Join('|', $enumNames)))).Replace('-', '')
}
finally { $enumHasher.Dispose() }
if ($enumNames.Count -ne 89 -or
    $enumHash -ne 'EF3DAC4B4C29123F95D37B538218EBE20FBA01DB119B597ADAC9E304B060C1D9' -or
    @($expectedCivilians + $expectedSiege | Where-Object { $enumNames -cnotcontains $_ }).Count -ne 0) {
    throw 'Installed eChimps differs from the reviewed enum; recheck healer classification.'
}

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
$healerRow = $settingsDocument.SelectSingleNode('//*[local-name()="Grid" and .//*[local-name()="CheckBox" and @IsChecked="{Binding HealCivilianTargets, Mode=TwoWay}"]]')
if ($null -eq $healerRow -or
    $null -eq $healerRow.SelectSingleNode('.//*[local-name()="CheckBox" and @IsChecked="{Binding HealCivilianTargets, Mode=TwoWay}"]') -or
    $null -eq $healerRow.SelectSingleNode('.//*[local-name()="CheckBox" and @IsChecked="{Binding HealSiegeTargets, Mode=TwoWay}"]')) {
    throw 'Healer host controls are missing from the settings row.'
}
$repairCheckbox = $settingsDocument.SelectSingleNode('//*[local-name()="CheckBox" and @IsChecked="{Binding EnableBuildingRepair, Mode=TwoWay}"]')
$repairContent = @($repairCheckbox.ChildNodes | Where-Object { $_.NodeType -eq [Xml.XmlNodeType]::Element })
$repairChildren = @($repairContent[0].ChildNodes | Where-Object { $_.NodeType -eq [Xml.XmlNodeType]::Element })
if ($repairContent.Count -ne 1 -or $repairContent[0].LocalName -ne 'StackPanel' -or
    $repairChildren.Count -ne 2 -or $repairChildren[0].LocalName -ne 'TextBlock' -or
    $repairChildren[1].LocalName -ne 'Image' -or
    $repairChildren[1].GetAttribute('Source') -ne '{Binding BuildingRepairIcon}') {
    throw 'Building repair settings icon must be bound to the right of its text.'
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
if (-not $activeVersion -or $runtimeText -notmatch 'BepInDependency\(ApiSharedGuid, "0\.4\.6"\)') {
    throw 'Plugin version or APIShared dependency mismatch.'
}
$addedCode = & git -C $workspace diff --unified=0 -- '*.cs' '*.csproj'
if ($LASTEXITCODE -ne 0) { throw 'Workspace diff audit failed.' }
$addedLines = @($addedCode | Where-Object { $_ -match '^\+[^+]' }) -join "`n"
if ($addedLines -match 'CodePatch\.Write|Marshal\.Write|VirtualProtect|\.Apply\s*\(|\.Enable\s*\(|\.Disable\s*\(') {
    throw 'New executable runtime mutation detected.'
}
Write-Host 'Repair preflight passed: binary hashes, JSON, lifecycle, hook rollback, XAML, CRLF, versions and workspace mutations.'
& (Join-Path $project 'Test-KeepBuildRange.ps1')
