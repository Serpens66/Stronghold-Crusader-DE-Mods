[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$native = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
$expected = @{
    'RedBird.X64.dll'='e085b83b2b87b5d2142c980090d8d9ec616a90a443bd125c17b41e0bb47a44eb'
    'RedBird.Core.dll'='ee4b036c486077f7d8898ae054f124edde73e6ae1a059e8991c361e2b720d29f'
    'RedBird.Abstractions.dll'='339cece116f66f59ecc7da5e9a5c0b2dfe24f4e7a5e7efc005864fa77c027a1e'
    'Iced.dll'='cc915d1c2e7be60933c5b129842ca3d08d1fe3cda6c6d6ade8824dcdde16191f'
}
foreach ($name in $expected.Keys) {
    if ((Get-Sha256Hex (Join-Path $game ('BepInEx\plugins\000shcdese\'+$name))) -cne $expected[$name]) { throw "Installed backend changed: $name" }
}
$files = @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|BepInEx)[\\/]' -and $_.Extension -in @('.cs','.csproj','.ps1','.bat','.json','.md','.py') })
foreach ($file in $files) {
    $content = [IO.File]::ReadAllText($file.FullName)
    if ($content -match '(?<!\r)\n') { throw "Bare LF in $($file.FullName)" }
    if ($file.Extension -eq '.cs' -and $file.FullName -notmatch '[\\/]tests[\\/]') {
        if ($content -match 'System\.Text\.Json|Newtonsoft|JsonUtility|JavaScriptSerializer|DataContractJsonSerializer|System\.Web\.Extensions') { throw 'Forbidden runtime JSON.' }
        if ($content -match '\b(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|StartCoroutine|InvokeRepeating)\s*\(') { throw 'Forbidden plugin lifecycle/scheduling.' }
        if ($content -match '\.(?:Enable|Disable|Apply|Undo|Unpatch|UnpatchAll)\s*\(|CodePatch\.Write|VirtualProtect|FlushInstructionCache|VirtualFree') { throw 'Unexpected runtime executable mutation.' }
    }
}
# Additional guardrail because the shared published-hook scanner deliberately excludes testmods.
# Dispose is allowed only in the private initialization rollback guarded by published.
$runtime = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src\SellRuntime.cs'))
if (@([regex]::Matches($runtime,'transaction\?\.Dispose\(\)')).Count -ne 1 -or
    $runtime -notmatch 'if \(published\) throw new InvalidOperationException\("Published hooks cannot be rolled back\."\);\s*transaction\?\.Dispose\(\)') { throw 'Initialization-only rollback contract changed.' }
& (Join-Path $workspace 'Shared\Tools\Validation\Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'Testmods\AISellThresholdFixTest' -IndependentRuntime -ConsumerOnly
if (-not $?) { throw 'Scoped runtime preflight failed.' }
$metadata = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'info.json') | ConvertFrom-Json
if ($metadata.GUID -ne 'AISellThresholdFixTest_Serp' -or $metadata.Version -ne '0.1.0' -or $metadata.NetworkMode -ne 1 -or @($metadata.Dependencies).Count -ne 0) { throw 'Metadata mismatch.' }
& 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' (Join-Path $PSScriptRoot 'tests\audit_native.py') $native
if ($LASTEXITCODE -ne 0) { throw 'Native machine audit failed.' }
foreach ($configuration in @('normal','devdiag')) {
    $diag = if ($configuration -eq 'devdiag') { 'true' } else { 'false' }
    & dotnet build (Join-Path $PSScriptRoot 'tests\AISellThresholdFixTest.Tests.csproj') --nologo -v minimal "/p:SerpDevDiagnostics=$diag"
    if ($LASTEXITCODE -ne 0) { throw "Native harness build failed ($configuration)." }
    & dotnet (Join-Path $PSScriptRoot 'tests\bin\Debug\net10.0\AISellThresholdFixTest.Tests.dll') $native
    if ($LASTEXITCODE -ne 0) { throw "Native execution tests failed ($configuration)." }
}
Write-Output 'PASS: scoped JSON/lifecycle/permanent hooks, CRLF, identity, native audit, normal and DEVDIAG native execution.'
