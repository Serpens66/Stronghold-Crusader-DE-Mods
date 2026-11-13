[CmdletBinding()]
param([switch]$nopause, [switch]$noinstall, [string]$ApiSharedDir)
$ErrorActionPreference = 'Stop'
$mod = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$arguments = @('msbuild', (Join-Path $mod 'PathfindingHazardTest.csproj'), '/t:Build', '/p:Configuration=Release', '/verbosity:minimal')
if ($ApiSharedDir) { $arguments += '/p:ApiSharedDir=' + [IO.Path]::GetFullPath($ApiSharedDir) }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'PathfindingHazardTest compilation failed.' }
& dotnet run --project (Join-Path $mod 'tests/Tests.csproj') -- (Split-Path -Parent (Split-Path -Parent $mod))
if ($LASTEXITCODE -ne 0) { throw 'PathfindingHazardTest offline contracts failed.' }
Write-Output 'PASS: build and offline contracts only. No install, game launch, run request or gameplay acceptance.'
