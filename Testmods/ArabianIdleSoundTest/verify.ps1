[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '../..')).Path
$gameDirectory = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$msbuildPath = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
[xml]$project = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ArabianIdleSoundTest.csproj'))
$sources = @($project.SelectNodes("//*[local-name()='Compile']") | ForEach-Object {
    (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot $_.Include)).Path
})
foreach ($path in @((Join-Path $PSScriptRoot 'ArabianIdleSoundTest.csproj')) + $sources) {
    $text = [IO.File]::ReadAllText($path)
    if ($text -match 'JavaScriptSerializer|System\.Web\.Extensions|System\.Text\.Json|Newtonsoft|DataContractJsonSerializer|JsonUtility|publicized') { throw "Forbidden runtime reference: $path" }
    if ($text -match '\b(?:void|IEnumerator)\s+(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|Start|OnEnable)\s*\(|\bStartCoroutine\s*\(') { throw "Unaudited runtime lifecycle: $path" }
    if ($text -match '\bVirtualProtect\s*\(|\bFlushInstructionCache\s*\(|\bCodePatch\.Write\s*\(|\bMarshal\.Write\w*\s*\(|\.(?:Disable|Undo)\s*\(') { throw "Executable mutation/teardown: $path" }
}
$plugin = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/Plugin.cs'))
if ($plugin -notmatch 'SoundEvents.TryRegister' -or $plugin -match 'NativeDetour|ILHook|LibraryLoaded|TryGetById') { throw 'Consumer must use the shared sound API without owning hooks' }
foreach ($file in Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object {
    $_.FullName -notmatch '[\\/](bin|obj|BepInEx)[\\/]' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.md','.json','.py','.xaml','.txt')
}) {
    if ([IO.File]::ReadAllText($file.FullName) -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF file: $($file.FullName)" }
}
& (Join-Path $PSScriptRoot 'tests/Test-IngameSettings.ps1')
& (Join-Path $workspace 'Shared/Tools/Validation/Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'Testmods/ArabianIdleSoundTest'
& (Join-Path $PSScriptRoot 'tests/Test-SharedGateRegression.ps1')
# This builds only the isolated test host, never the mod or installed outputs.
& $msbuildPath (Join-Path $PSScriptRoot 'tests/ArabianIdleSoundTests.csproj') /nologo /verbosity:minimal /p:Configuration=Debug
if ($LASTEXITCODE -ne 0) { throw 'Isolated test host compilation failed' }
& (Join-Path $PSScriptRoot 'tests/bin/Debug/ArabianIdleSoundTests.exe') $gameDirectory
if ($LASTEXITCODE -ne 0) { throw 'Shared audio consumer contract tests failed' }
Write-Host 'PASS: ArabianIdleSoundTest runtime preflight, CRLF, shared boundaries and isolated consumer tests.'
