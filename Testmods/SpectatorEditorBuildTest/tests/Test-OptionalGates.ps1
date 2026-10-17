$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$output = Join-Path $workspace ('_inspect\SpectatorHudVanilla-20261016\gates-' + [guid]::NewGuid().ToString('N'))
& dotnet msbuild (Join-Path $PSScriptRoot 'OptionalGateTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
if ($LASTEXITCODE -ne 0) { throw 'Optional gate fixture build failed.' }
& (Join-Path $output 'OptionalGateTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Optional gate fixture failed.' }
