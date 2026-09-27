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
foreach ($path in @($info, $packagedInfo)) {
    $metadata = [IO.File]::ReadAllText($path) | ConvertFrom-Json
    if ($metadata.Version -ne '1.0.104' -or $metadata.MinimumScriptExtenderVersion -ne '2.10.4' -or
        $metadata.SerpChangelog[0].Version -ne '1.0.104') {
        throw "Active version mismatch: $path"
    }
}
if ($runtimeText -notmatch 'PluginVersion = "1\.0\.104"' -or
    $runtimeText -notmatch 'BepInDependency\(ApiSharedGuid, "0\.4\.3"\)') {
    throw 'Plugin version or APIShared dependency mismatch.'
}
$addedCode = & git -C $workspace diff --unified=0 -- '*.cs' '*.csproj'
if ($LASTEXITCODE -ne 0) { throw 'Workspace diff audit failed.' }
$addedLines = @($addedCode | Where-Object { $_ -match '^\+[^+]' }) -join "`n"
if ($addedLines -match 'CodePatch\.Write|Marshal\.Write|VirtualProtect|\.Apply\s*\(|\.Enable\s*\(|\.Disable\s*\(') {
    throw 'New executable runtime mutation detected.'
}
Write-Host 'Repair preflight passed: binary hashes, JSON, lifecycle, hook rollback, XAML, CRLF, versions and workspace mutations.'
