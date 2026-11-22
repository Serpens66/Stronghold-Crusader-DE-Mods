$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..\..')).Path
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' -File)
$projectFile = Join-Path $project 'AIMarketOverbuyDiagnostic.csproj'
$testDirectory = Join-Path $workspace '_inspect\AIMarketOverbuyDiagnosticTests'
$textFiles = @($sourceFiles) + @(
    (Join-Path $project 'Properties\AssemblyInfo.cs'),
    (Join-Path $project 'info.json'),
    $projectFile,
    (Join-Path $project 'build.bat'),
    $MyInvocation.MyCommand.Path,
    (Join-Path $testDirectory 'Tests.csproj'),
    (Join-Path $testDirectory 'Program.cs'),
    (Join-Path $testDirectory 'Test.ps1'),
    (Join-Path $project 'tests\TradeSession\TradeSession.Tests.csproj'),
    (Join-Path $project 'tests\TradeSession\Program.cs'),
    (Join-Path $project 'tests\TradeSession\StaleEquipmentTargetTests.cs'),
    (Join-Path $project 'tests\TradeEntries\TradeEntries.Tests.csproj'),
    (Join-Path $project 'tests\TradeEntries\Program.cs'))
$runtimeText = (($sourceFiles + @((Get-Item -LiteralPath $projectFile))) |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
$forbiddenJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
$forbiddenLifecycle = '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\('
$forbiddenMutations = 'CodePatch\.Write|Marshal\.Write|VirtualProtect|X64InlineHook|HookTransaction|\.Apply\s*\(|\.Undo\s*\(|\.Enable\s*\(|\.Disable\s*\('
if ($runtimeText -match $forbiddenJson) { throw 'Forbidden runtime JSON dependency.' }
if ($runtimeText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause)\s*\(') { throw 'Forbidden runtime lifecycle teardown.' }
$pluginText = [IO.File]::ReadAllText((Join-Path $project 'src\AIMarketOverbuyDiagnosticPlugin.cs'))
if ($pluginText -match $forbiddenLifecycle) { throw 'Long-lived MonoBehaviour callback in plugin.' }
if ($runtimeText -match $forbiddenMutations) { throw 'Runtime executable mutation or detour detected.' }
if ($runtimeText -match 'Assembly-CSharp-publicized') { throw 'Publicized assembly reference detected.' }
foreach ($path in $textFiles) {
    $fullPath = if ($path -is [IO.FileInfo]) { $path.FullName } else { [string]$path }
    $content = [IO.File]::ReadAllText($fullPath)
    if ($content -match '(?<!\r)\n' -or $content -match '(?<!\r)\r(?!\n)') { throw "Non-CRLF line ending: $fullPath" }
    $literalEscapedNewline = ([string][char]92) + 'r' + ([string][char]92) + 'n'
    if ($content.Contains($literalEscapedNewline)) { throw "Literal backslash-r-backslash-n sequence: $fullPath" }
}
& (Join-Path $workspace 'Shared\Tools\Validation\Test-PermanentNativeRuntimePatches.ps1') -ModDirectory 'Testmods\AIMarketOverbuyDiagnostic'
if (-not $?) { throw 'Workspace native runtime regression check failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $testDirectory 'Test.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Market observation behavior/performance tests failed.' }
# Keep the numeric lifecycle test in its .NET 8 process. The native backend fixture
# must execute as its x64 .NET Framework EXE, never load its CLR dependencies into
# Windows PowerShell 5.1 (or run it through dotnet). It hooks only private copies.
$sessionProject = Join-Path $project 'tests\TradeSession\TradeSession.Tests.csproj'
& dotnet build $sessionProject --configuration Release --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Trade session test compilation failed.' }
& dotnet (Join-Path $project 'tests\TradeSession\bin\Release\net8.0\TradeSession.Tests.dll')
if ($LASTEXITCODE -ne 0) { throw 'Nested/concurrent trade session lifecycle tests failed.' }
$entriesProject = Join-Path $project 'tests\TradeEntries\TradeEntries.Tests.csproj'
$entryBuildArguments = @('build', $entriesProject, '--configuration', 'Release', '--nologo', '-v', 'minimal')
if ($env:SHCDE_GAME_DIR) { $entryBuildArguments += ('/p:GameDir=' + $env:SHCDE_GAME_DIR) }
if ($env:SHCDESE_EXTENDER_DIR) { $entryBuildArguments += ('/p:ExtenderDir=' + $env:SHCDESE_EXTENDER_DIR) }
& dotnet @entryBuildArguments
if ($LASTEXITCODE -ne 0) { throw 'Native trade entry test compilation failed.' }
& (Join-Path $project 'tests\TradeEntries\bin\Release\net481\TradeEntries.Tests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Installed NativeX64 trade entry ABI/trampoline/foreign-patch fixtures failed.' }
Write-Host 'PASS: Runtime JSON, lifecycle, plugin callbacks, native mutation, layout source, and CRLF checks.'
