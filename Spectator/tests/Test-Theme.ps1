$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../../Shared/Tools/Paths/WorkPaths.ps1')
$output = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) -Kind t -Name 'SpectatorTheme'
& dotnet msbuild (Join-Path $PSScriptRoot 'ToolbarThemeTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
if ($LASTEXITCODE -ne 0) { throw 'Theme persistence fixture compilation failed.' }
& (Join-Path $output 'ToolbarThemeTests.exe') $output
if ($LASTEXITCODE -ne 0) { throw 'Theme persistence regression failed.' }
