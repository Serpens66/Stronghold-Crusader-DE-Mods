[CmdletBinding()]
param([switch]$nopause, [switch]$noinstall, [string]$ApiSharedDir)
$ErrorActionPreference = 'Stop'
$temporaryFixProject = Join-Path $PSScriptRoot 'TemporaryFixes/TrappedUnitRescueTest.Fixes130.csproj'
& dotnet msbuild $temporaryFixProject /t:Build /p:Configuration=Release /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Temporary Fixes scanner correction did not compile.' }
& (Join-Path $PSScriptRoot 'TemporaryFixes/bin/TrappedUnitRescueTest.Fixes130.exe') --self-test `
    'E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/plugins/fixes/fixes.dll'
if ($LASTEXITCODE -ne 0) { throw 'Temporary Fixes scanner correction contract tests failed.' }
$arguments = @('msbuild', (Join-Path $PSScriptRoot 'TrappedUnitRescueTest.csproj'), '/t:Build', '/p:Configuration=Release', '/verbosity:minimal')
if ($ApiSharedDir) { $arguments += '/p:ApiSharedDir=' + [IO.Path]::GetFullPath($ApiSharedDir) }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'TrappedUnitRescueTest compilation failed.' }
$testArguments = @('msbuild', (Join-Path $PSScriptRoot 'tests/TrappedUnitRescueTest.Tests.csproj'), '/t:Build', '/p:Configuration=Release', '/verbosity:minimal')
if ($ApiSharedDir) { $testArguments += '/p:ApiSharedDir=' + [IO.Path]::GetFullPath($ApiSharedDir) }
& dotnet @testArguments
if ($LASTEXITCODE -ne 0) { throw 'TrappedUnitRescueTest native/policy tests did not compile.' }
& (Join-Path $PSScriptRoot 'tests/bin/TrappedUnitRescueTest.Tests.exe')
if ($LASTEXITCODE -ne 0) { throw 'TrappedUnitRescueTest native/policy tests failed.' }
Write-Output 'PASS: package built; installation and game runs use the transactional Invoke-InGameTest runner.'
