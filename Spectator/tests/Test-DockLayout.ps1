$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$baseline = Join-Path $workspace '_inspect/CrusaderDE-Native-Baseline'
if ($env:SHCDE_NATIVE_BASELINE_ROOT) { $baseline = $env:SHCDE_NATIVE_BASELINE_ROOT }
$current = Get-Content -LiteralPath (Join-Path $baseline 'CURRENT.json') -Raw | ConvertFrom-Json
$vanilla = Join-Path (Join-Path $baseline $current.semanticDirectory) 'resources/xaml/Assets/GUI/XAMLResources/HUD_Main.xaml'
$output = New-ShortWorkDirectory -Root $workspace -Kind t -Name 'SpectatorDock'
& dotnet msbuild (Join-Path $PSScriptRoot 'DockLayoutTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/"
if ($LASTEXITCODE -ne 0) { throw 'Dock layout harness compilation failed.' }
& (Join-Path $output 'DockLayoutTests.exe') $vanilla (Join-Path $PSScriptRoot '../Patches/Assets/GUI/XAMLResources/HUD_Main.xaml')
if ($LASTEXITCODE -ne 0) { throw 'Dock patch integration failed.' }
