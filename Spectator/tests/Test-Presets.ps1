$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../_inspect/HostClientPresetTests/SpectatorPreset.Tests.csproj'))
. (Join-Path $PSScriptRoot '../../Shared/Tools/Paths/WorkPaths.ps1')
$output = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) -Kind t -Name 'SpectatorPresets'
& dotnet msbuild $project /t:Build /p:Configuration=Release /p:AssemblyName=SpectatorPresetTests /p:StartupObject=SpectatorPresetTests "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/" /verbosity:quiet
if ($LASTEXITCODE -ne 0) { throw 'Spectator preset fixture compilation failed.' }
& (Join-Path $output 'SpectatorPresetTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Spectator preset fixture failed.' }
