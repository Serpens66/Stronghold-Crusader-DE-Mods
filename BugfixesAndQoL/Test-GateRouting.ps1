[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
$project = Join-Path $PSScriptRoot 'tests\GateRouting.Tests\GateRouting.Tests.csproj'
if ([IO.File]::ReadAllText($project).Contains('Testmods\EnemyGatePathfindingTest')) { throw 'Production tests still depend on legacy sources.' }
& $msbuild $project /t:Build /p:Configuration=Debug /v:q /nologo
if ($LASTEXITCODE -ne 0) { throw 'Gate routing test compilation failed.' }
Push-Location $PSScriptRoot
try { & '.\tests\GateRouting.Tests\bin\GateRouting.Tests.exe'; if ($LASTEXITCODE -ne 0) { throw 'Gate routing regression failed.' } }
finally { Pop-Location }
Push-Location $root
try {
    & dotnet run --project '_inspect/GateMigrationFeatureTests'
    if ($LASTEXITCODE -ne 0) { throw 'Actual gate feature activation regression failed.' }
    & dotnet run --project '_inspect/AssassinDescentRuntimeTests' --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Actual descent reader regression failed.' }
    & dotnet run --project '_inspect/TemporaryGateAcceptanceRuntimeTests' -- $root
    if ($LASTEXITCODE -ne 0) { throw 'Actual route/observer regression failed.' }
} finally { Pop-Location }
Write-Host 'PASS: integrated gate geometry, scopes, adapters, access extensions, route and observation regression.'
