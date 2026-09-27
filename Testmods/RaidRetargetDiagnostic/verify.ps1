$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..\..')).Path
$sources = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' -File)
$projectFile = Join-Path $project 'RaidRetargetDiagnostic.csproj'
$textFiles = @($sources) + @(
    (Join-Path $project 'Properties\AssemblyInfo.cs'),
    (Join-Path $project 'info.json'),
    $projectFile,
    (Join-Path $project 'build.bat'),
    $MyInvocation.MyCommand.Path)
$runtimeText = (($sources + @((Get-Item -LiteralPath $projectFile))) |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
$forbiddenJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
$forbiddenLifecycle = '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\('
$forbiddenMutations = 'CodePatch\.Write|Marshal\.Write|VirtualProtect|NativeDetour|X64InlineHook|HookTransaction|\.Apply\s*\(|\.Undo\s*\(|\.Enable\s*\(|\.Disable\s*\('
if ($runtimeText -match $forbiddenJson) { throw 'Forbidden runtime JSON dependency.' }
if ($runtimeText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause)\s*\(') { throw 'Forbidden runtime lifecycle teardown.' }
$pluginText = [IO.File]::ReadAllText((Join-Path $project 'src\RaidRetargetDiagnosticPlugin.cs'))
if ($pluginText -match $forbiddenLifecycle) { throw 'Long-lived MonoBehaviour callback in plugin.' }
if ($runtimeText -match $forbiddenMutations) { throw 'Runtime executable mutation or detour detected.' }
if ($runtimeText -match 'Assembly-CSharp-publicized') { throw 'Publicized assembly reference detected.' }
$xamlFiles = @(Get-ChildItem -LiteralPath $project -Recurse -Filter '*.xaml' -File)
foreach ($xaml in $xamlFiles) {
    [xml]$document = [IO.File]::ReadAllText($xaml.FullName)
    $contents = @($document.SelectNodes("//*[local-name()='Content']"))
    foreach ($content in $contents) {
        if (@($content.ChildNodes | Where-Object { $_.NodeType -eq 'Element' }).Count -ne 1) {
            throw "XAML Content must have one root element: $($xaml.FullName)"
        }
    }
}
foreach ($path in $textFiles) {
    $fullPath = if ($path -is [IO.FileInfo]) { $path.FullName } else { [string]$path }
    $content = [IO.File]::ReadAllText($fullPath)
    if ($content -match '(?<!\r)\n' -or $content -match '(?<!\r)\r(?!\n)') { throw "Non-CRLF line ending: $fullPath" }
    $literalEscapedNewline = ([string][char]92) + 'r' + ([string][char]92) + 'n'
    if ($content.Contains($literalEscapedNewline)) { throw "Literal backslash-r-backslash-n sequence: $fullPath" }
}
& (Join-Path $workspace 'Shared\Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Workspace native runtime regression check failed.' }
Write-Host 'PASS: Runtime JSON, lifecycle, plugin callbacks, native mutation, XAML, and CRLF checks.'
