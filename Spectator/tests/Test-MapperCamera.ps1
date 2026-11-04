$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../Shared/Tools/Paths/WorkPaths.ps1')
$output = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) -Kind t -Name 'SpectatorMapperCamera'
& dotnet msbuild (Join-Path $PSScriptRoot 'MapperCameraTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
if ($LASTEXITCODE -ne 0) { throw 'Mapper camera consumer fixture build failed.' }
& (Join-Path $output 'SpectatorMapperCameraTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Mapper camera consumer fixture failed.' }
