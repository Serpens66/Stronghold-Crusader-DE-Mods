$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
. (Join-Path $PSScriptRoot '../../../Shared/Tools/Paths/WorkPaths.ps1')
$output = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -Kind t -Name 'OptionalGates'
& dotnet msbuild (Join-Path $PSScriptRoot 'OptionalGateTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
if ($LASTEXITCODE -ne 0) { throw 'Optional gate fixture build failed.' }
& (Join-Path $output 'OptionalGateTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Optional gate fixture failed.' }
