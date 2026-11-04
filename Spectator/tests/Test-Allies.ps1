$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$output = New-ShortWorkDirectory -Root $workspace -Kind t -Name 'SpectatorAllies'
& dotnet msbuild (Join-Path $PSScriptRoot 'AlliesTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
if ($LASTEXITCODE -ne 0) { throw 'Panel controller harness compilation failed.' }
& (Join-Path $output 'AlliesTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Panel controller checks failed.' }
