$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../Shared/Tools/Paths/WorkPaths.ps1')
$output = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) -Kind t -Name 'SpectatorAi'
& dotnet msbuild (Join-Path $PSScriptRoot 'AiPlacementTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
if ($LASTEXITCODE -ne 0) { throw 'AI placement controller build failed.' }
& (Join-Path $output 'AiPlacementTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'AI placement controller tests failed.' }
