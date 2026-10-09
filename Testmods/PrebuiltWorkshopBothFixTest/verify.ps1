$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..\..')).Path
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' -File)
$pluginFiles = @($sourceFiles | Where-Object { $_.Name -like '*Plugin.cs' })
$projectFile = Join-Path $project 'PrebuiltWorkshopBothFixTest.csproj'
$textFiles = @($sourceFiles) + @(
    (Join-Path $project 'Properties\AssemblyInfo.cs'),
    (Join-Path $project 'info.json'),
    $projectFile,
    (Join-Path $project 'build.bat'),
    $MyInvocation.MyCommand.Path)
$runtimeText = (($sourceFiles + @((Get-Item -LiteralPath $projectFile))) |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
$forbiddenJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
$forbiddenLifecycle = '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\('
$forbiddenMutations = 'CodePatch\.Write|Marshal\.Write|VirtualProtect|NativeDetour|X64InlineHook|HookTransaction|\.Apply\s*\(|\.Undo\s*\(|\.Enable\s*\(|\.Disable\s*\('
if ($runtimeText -match $forbiddenJson) { throw 'Forbidden runtime JSON dependency.' }
if ($runtimeText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause)\s*\(') { throw 'Forbidden runtime lifecycle teardown.' }
foreach ($file in $pluginFiles) {
    if ([IO.File]::ReadAllText($file.FullName) -match $forbiddenLifecycle) { throw "Long-lived MonoBehaviour callback in $($file.Name)." }
}
if ($runtimeText -match $forbiddenMutations) { throw 'Runtime executable mutation or detour detected.' }
if ($runtimeText -match 'Assembly-CSharp-publicized') { throw 'Publicized assembly reference detected.' }
foreach ($path in $textFiles) {
    $fullPath = if ($path -is [IO.FileInfo]) { $path.FullName } else { [string]$path }
    $content = [IO.File]::ReadAllText($fullPath)
    if ($content -match '(?<!\r)\n' -or $content -match '(?<!\r)\r(?!\n)') { throw "Non-CRLF line ending: $path" }
    $literalEscapedNewline = ([string][char]92) + 'r' + ([string][char]92) + 'n'
    if ($content.Contains($literalEscapedNewline)) { throw "Literal backslash-r-backslash-n sequence: $path" }
}
& (Join-Path $workspace 'Shared\Tools\Validation\Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Workspace permanent-hook audit failed.' }
Write-Host 'Runtime JSON, lifecycle, public-member, native-mutation, workspace-change, and CRLF checks passed.'
