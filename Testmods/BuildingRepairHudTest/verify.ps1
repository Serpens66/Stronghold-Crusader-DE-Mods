$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..\..')).Path
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
if ((Get-Sha256 $native) -ne $expectedNative) { throw 'Installed native DLL is not the audited build.' }
if ((Get-Sha256 $managed) -ne $expectedManaged) { throw 'Installed managed DLL is not the audited build.' }

$runtimeFiles = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' -File)
$projectFile = Join-Path $project 'BuildingRepairHudTest.csproj'
$xaml = Join-Path $project 'BepInEx\plugins\BuildingRepairHudTest_Serp\Patches\Assets\GUI\XAMLResources\HUD_Buildings.xaml'
$textFiles = @($runtimeFiles) + @(
    (Join-Path $project 'Properties\AssemblyInfo.cs'),
    (Join-Path $project 'info.json'),
    $projectFile,
    $xaml,
    (Join-Path $project 'build.bat'),
    $MyInvocation.MyCommand.Path)
$runtimeText = (($runtimeFiles + @((Get-Item -LiteralPath $projectFile))) |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
$forbiddenJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
$forbiddenLifecycle = '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\('
$forbiddenMutations = 'CodePatch\.Write|Marshal\.Write|VirtualProtect|NativeDetour|X64InlineHook|HookTransaction|\.Apply\s*\(|\.Enable\s*\(|\.Disable\s*\('
if ($runtimeText -match $forbiddenJson) { throw 'Forbidden runtime JSON dependency.' }
if ($runtimeText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause)\s*\(') { throw 'Runtime lifecycle teardown is forbidden.' }
foreach ($file in $runtimeFiles | Where-Object { $_.Name -like '*Plugin.cs' }) {
    if ([IO.File]::ReadAllText($file.FullName) -match $forbiddenLifecycle) { throw "Long-lived MonoBehaviour callback in $($file.Name)." }
}
if ($runtimeText -match $forbiddenMutations) { throw 'Executable runtime mutation detected.' }
if ($runtimeText -match 'Assembly-CSharp-publicized') { throw 'Publicized assembly reference detected.' }
$rollback = @([regex]::Matches($runtimeText, '\.Undo\s*\(|\.Dispose\s*\('))
if ($rollback.Count -ne 6 -or $runtimeText -notmatch 'Roll back only this unpublished initialization candidate') {
    throw 'Hook rollback changed; review candidate-only teardown before build.'
}
foreach ($path in $textFiles) {
    $fullPath = if ($path -is [IO.FileInfo]) { $path.FullName } else { [string]$path }
    $content = [IO.File]::ReadAllText($fullPath)
    if ($content -match '(?<!\r)\n' -or $content -match '(?<!\r)\r(?!\n)') { throw "Non-CRLF line ending: $path" }
    $literalEscapedNewline = ([string][char]92) + 'r' + ([string][char]92) + 'n'
    if ($content.Contains($literalEscapedNewline)) { throw "Literal backslash-r-backslash-n sequence: $path" }
}

[xml]$patch = [IO.File]::ReadAllText($xaml)
foreach ($content in $patch.SelectNodes('//Content')) {
    $elements = @($content.ChildNodes | Where-Object { $_.NodeType -eq [Xml.XmlNodeType]::Element })
    if ($elements.Count -ne 1) { throw 'XAML patch Content must have exactly one direct root element.' }
}
$allPatches = @(Get-ChildItem -LiteralPath (Join-Path $project 'BepInEx') -Recurse -File -Filter '*.xaml')
foreach ($file in $allPatches) {
    [xml]$document = [IO.File]::ReadAllText($file.FullName)
    foreach ($content in $document.SelectNodes('//Content')) {
        $elements = @($content.ChildNodes | Where-Object { $_.NodeType -eq [Xml.XmlNodeType]::Element })
        if ($elements.Count -ne 1) { throw "XAML patch Content has multiple roots: $($file.FullName)" }
    }
}
$addedCode = & git -C $workspace diff --unified=0 -- '*.cs' '*.csproj'
if ($LASTEXITCODE -ne 0) { throw 'Workspace diff audit failed.' }
$addedLines = @($addedCode | Where-Object { $_ -match '^\+[^+]' }) -join "`n"
if ($addedLines -match $forbiddenMutations) { throw 'New workspace executable mutation detected.' }
Write-Host 'Native and managed hashes, JSON, lifecycle, hook teardown, XAML, workspace mutation, and CRLF checks passed.'
