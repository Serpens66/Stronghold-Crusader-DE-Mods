param(
    [Parameter(Mandatory=$true)][string]$TracePath,
    [Parameter(Mandatory=$true)][string]$OutputPath,
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
)
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'tests\Run-Tests.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
& (Join-Path $PSScriptRoot 'tests\bin\DependencyBootstrapProbe.Tests.exe') $GameDir offline ([IO.Path]::GetFullPath($TracePath)) ([IO.Path]::GetFullPath($OutputPath))
if ($LASTEXITCODE -ne 0) { throw 'Offline shadow analysis failed.' }
