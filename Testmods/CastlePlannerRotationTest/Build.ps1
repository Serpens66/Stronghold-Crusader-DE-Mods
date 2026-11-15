[CmdletBinding()]
param([switch]$nopause, [switch]$noinstall, [string]$ApiSharedDir)
$ErrorActionPreference = 'Stop'
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) { throw 'Full .NET Framework MSBuild is required.' }
$arguments = @((Join-Path $PSScriptRoot 'CastlePlannerRotationTest.csproj'), '/t:Build', '/p:Configuration=Release', '/verbosity:minimal')
if ($ApiSharedDir) { $arguments += '/p:ApiSharedDir=' + [IO.Path]::GetFullPath($ApiSharedDir) }
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw 'CastlePlannerRotationTest compilation failed.' }
Write-Output 'PASS: build only. No install, game launch, run request or gameplay acceptance.'
