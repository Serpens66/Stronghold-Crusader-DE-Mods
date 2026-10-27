param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$RunDirectory
)
$ErrorActionPreference = 'Stop'
$tools = Split-Path -Parent $PSScriptRoot
$baseline = Split-Path -Parent (Split-Path -Parent $tools)
$workspace = Split-Path -Parent (Split-Path -Parent $baseline)
. (Join-Path $workspace 'Shared\Tools\Paths\WorkPaths.ps1')
. (Join-Path $workspace 'Shared\Tools\Steam\SteamPackPolicy.ps1')
Assert-ShortWorkDirectory -Path $RunDirectory
$current = [IO.File]::ReadAllText((Join-Path $baseline 'CURRENT.json')) | ConvertFrom-Json
$semantic = Join-Path $baseline $current.semanticDirectory
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new package directory; previous artifacts are retained.' }
$native = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
if ((Get-SteamPackFileSha256 $native) -ine $current.currentNativeHash) { throw 'Installed native DLL does not match CURRENT.json.' }
$protected = [ordered]@{}
foreach ($directory in @((Join-Path $baseline $current.currentNativeHash), (Join-Path $semantic 'ghidra'))) {
    foreach ($file in Get-ChildItem -LiteralPath $directory -Recurse -File) {
        $protected[$file.FullName] = Get-SteamPackFileSha256 $file.FullName
    }
}
[void][IO.Directory]::CreateDirectory($OutputDirectory)
$data = Join-Path $OutputDirectory 'data'
$scripts = Join-Path $OutputDirectory 'scripts'
$notices = Join-Path $OutputDirectory 'notices'
foreach ($directory in @($data, $scripts, $notices)) { [void][IO.Directory]::CreateDirectory($directory) }
foreach ($name in @('TransferAnnotations.java', 'ImportNativeKnowledge.java', 'ImportNativeFunctionNames.java')) {
    Copy-Item -LiteralPath (Join-Path $tools $name) -Destination $scripts
}
foreach ($name in @('Create-GhidraProject.ps1', 'START.cmd', 'ANLEITUNG.txt')) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $OutputDirectory
}
Copy-Item -LiteralPath (Join-Path $workspace 'APIShared\LICENSE') -Destination (Join-Path $notices 'APIShared-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $workspace 'shcde-script-extender\LICENSE.txt') -Destination (Join-Path $notices 'ScriptExtender-LICENSE.txt')
foreach ($name in @('native-field-ghidra.json', 'native-field-catalog.json')) {
    Copy-Item -LiteralPath (Join-Path $semantic ('knowledge\' + $name)) -Destination $data
}
$env:JAVA_HOME = Join-Path $workspace '.tools\temurin-jdk-21.0.12.1+1\jdk-21.0.12.1+1'
$headless = Join-Path $workspace '.tools\ghidra-12.1.3\ghidra_12.1.3_PUBLIC\support\analyzeHeadless.bat'
$report = Join-Path $RunDirectory 'package-export.json'
if (Test-Path -LiteralPath $report) { throw 'Run directory already contains a package export.' }
& $headless (Join-Path $semantic 'ghidra') 'CrusaderDE-Semantic' -process 'CrusaderDE.dll' -readOnly -noanalysis -scriptPath $scripts -postScript TransferAnnotations.java export $data $report -log (Join-Path $RunDirectory 'package-export.log') *> (Join-Path $RunDirectory 'package-export-console.log')
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $report)) { throw 'Annotation export failed; inspect package-export-console.log.' }
foreach ($file in $protected.Keys) {
    if ((Get-SteamPackFileSha256 $file) -cne $protected[$file]) { throw "Protected baseline changed during export: $file" }
}
$snapshot = [IO.File]::ReadAllText((Join-Path $data 'annotations.json')) | ConvertFrom-Json
$knowledge = [IO.File]::ReadAllText((Join-Path $data 'native-field-ghidra.json')) | ConvertFrom-Json
$files = @(foreach ($file in Get-ChildItem -LiteralPath $OutputDirectory -Recurse -File | Sort-Object FullName) {
    [ordered]@{path=$file.FullName.Substring($OutputDirectory.Length + 1).Replace('\','/'); bytes=$file.Length; sha256=(Get-SteamPackFileSha256 $file.FullName).ToUpperInvariant()}
})
$head = & git -C $workspace rev-parse HEAD
if ($LASTEXITCODE -ne 0) { throw 'Git source identity unavailable.' }
$branch = & git -C $workspace branch --show-current
$manifest = [ordered]@{
    schemaVersion=1; createdUtc=[DateTime]::UtcNow.ToString('o'); kind='ghidra-annotations'; nativeHash=$current.currentNativeHash
    ghidraVersion='12.1.3'; repository='https://github.com/Serpens66/Stronghold-Crusader-DE-Mods'; sourceCommit=$head; sourceBranch=$branch
    sourceState='Exported from the maintained semantic Ghidra project; file hashes identify the actual snapshot independently of the Git commit.'
    functions=@($snapshot.snapshot.functions.PSObject.Properties).Count; typeDefinitions=@($snapshot.snapshot.types.PSObject.Properties).Count
    nativeKnowledgeFields=@($knowledge.fieldIds).Count; nativeKnowledgeFunctions=@($knowledge.functions).Count
    includesExecutableMemory=$false; requiresOwnMatchingDll=$true; files=$files
}
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'PACKAGE.json'), (($manifest | ConvertTo-Json -Depth 10) + "`r`n"), [Text.UTF8Encoding]::new($false))
Write-Host "Annotation package staged: $OutputDirectory"
Write-Host 'Test from an extracted ZIP before publishing. This command does not upload or create a release.'
