[CmdletBinding()]
param([Parameter(Mandatory)][string]$GameDir, [Parameter(Mandatory)][string]$MSBuild)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$run = New-ShortWorkDirectory -Root $workspace -Kind t -Name cost-overrides -Identity 'UnitCosts and ExtraFeatures ownership regression tests'
# Forward slashes avoid Windows PowerShell's native quoting of a trailing backslash.
$output = (Join-Path $run 'bin').Replace('\', '/') + '/'
$intermediate = (Join-Path $run 'obj').Replace('\', '/') + '/'
Write-Host ('[{0:HH:mm:ss.fff}] Cost ownership tests: {1}' -f (Get-Date), $run)
& $MSBuild (Join-Path $PSScriptRoot 'CostOverrides.Tests.csproj') /nologo /verbosity:minimal /p:Configuration=Release "/p:GameDir=$GameDir" "/p:OutputPath=$output" "/p:BaseIntermediateOutputPath=$intermediate" "/p:IntermediateOutputPath=$intermediate" 2>&1 |
    Tee-Object -FilePath (Join-Path $run 'build.log')
if ($LASTEXITCODE -ne 0) { throw 'Cost ownership test build failed.' }
& (Join-Path $output 'CostOverrides.Tests.exe') 2>&1 | Tee-Object -FilePath (Join-Path $run 'tests.log')
if ($LASTEXITCODE -ne 0) { throw 'Cost ownership behavior tests failed.' }
