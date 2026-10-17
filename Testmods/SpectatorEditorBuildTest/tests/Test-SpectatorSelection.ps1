$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
$output = Join-Path $workspace ('_inspect\SpectatorHudVanilla-20261016\selection-' + [guid]::NewGuid().ToString('N'))
& dotnet msbuild (Join-Path $PSScriptRoot 'SpectatorSelectionTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
if ($LASTEXITCODE -ne 0) { throw 'Spectator selection fixture build failed.' }
& (Join-Path $output 'SpectatorSelectionTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Spectator selection fixture failed.' }
