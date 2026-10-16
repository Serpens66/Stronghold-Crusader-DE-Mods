$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$workspace = Split-Path -Parent (Split-Path -Parent $root)
$output = Join-Path $workspace ('_inspect\SpectatorPowersGates-20261016\fixtures\' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($output)
$fixturePath = Join-Path $output 'ExtremePowers.API.dll'
# Isolated CLI fixtures use the production late-binding adapter, not a game process or native hooks.
foreach ($project in @('OptionalPowerFixture','OptionalPowerTests')) {
    & dotnet msbuild (Join-Path $PSScriptRoot ($project+'.csproj')) /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/$project/"
    if ($LASTEXITCODE -ne 0) { throw "Optional API fixture build failed: $project" }
}
& (Join-Path $output 'OptionalPowerTests.exe') $fixturePath
if ($LASTEXITCODE -ne 0) { throw 'Optional API behavior fixture failed.' }
