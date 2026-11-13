[CmdletBinding()]
param([switch]$nopause, [switch]$noinstall, [string]$ApiSharedDir)
$ErrorActionPreference = 'Stop'
$arguments = @('msbuild', (Join-Path $PSScriptRoot 'PathfindingSurfaceTest.csproj'), '/t:Build', '/p:Configuration=Release', '/verbosity:minimal')
if ($ApiSharedDir) { $arguments += '/p:ApiSharedDir=' + [IO.Path]::GetFullPath($ApiSharedDir) }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'PathfindingSurfaceTest compilation failed.' }
Write-Output 'PASS: build only. No install, game launch, run request or gameplay acceptance.'
