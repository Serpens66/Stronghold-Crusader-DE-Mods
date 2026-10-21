$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
. (Join-Path $PSScriptRoot '../../Shared/Tools/Paths/WorkPaths.ps1')
$output = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))) -Kind t -Name 'NativePowerContext'
& 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' (Join-Path $PSScriptRoot 'audit_power_context.py')
if ($LASTEXITCODE -ne 0) { throw 'Power baseline edge audit failed.' }
& dotnet msbuild (Join-Path $PSScriptRoot 'NativePowerTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
if ($LASTEXITCODE -ne 0) { throw 'Native power fixture build failed.' }
& (Join-Path $output 'NativePowerTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Native power execution fixture failed.' }
