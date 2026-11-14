[CmdletBinding()]
param([switch]$nopause, [switch]$noinstall)
$ErrorActionPreference = 'Stop'
$arguments = @('msbuild', (Join-Path $PSScriptRoot 'GameStateProbe.csproj'), '/t:Build', '/p:Configuration=Release', '/verbosity:minimal')
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'GameStateProbe compilation failed.' }
Write-Output 'PASS: build only. Package: Testmods\GameStateProbe\BepInEx\plugins\GameStateProbe_Serp (install only for dev sessions).'
