$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$baseline = Join-Path $workspace '_inspect/CrusaderDE-Native-Baseline'
if ($env:SHCDE_NATIVE_BASELINE_ROOT) { $baseline = $env:SHCDE_NATIVE_BASELINE_ROOT }
$current = Get-Content -LiteralPath (Join-Path $baseline 'CURRENT.json') -Raw | ConvertFrom-Json
$vanilla = Join-Path (Join-Path $baseline $current.semanticDirectory) 'resources/xaml/Assets/GUI/XAML/IngameUIScreens.xaml'
$extender = Join-Path $workspace 'shcde-script-extender'
if (-not (Test-Path -LiteralPath $extender -PathType Container)) {
    # The independent Extender checkout exists only beside the primary checkout; Git worktrees
    # share its Git directory, so resolve it from there.
    $commonGit = @(& git -C $workspace rev-parse --path-format=absolute --git-common-dir 2>$null)
    if ($LASTEXITCODE -eq 0 -and $commonGit.Count -eq 1) { $extender = Join-Path (Split-Path -Parent ([IO.Path]::GetFullPath($commonGit[0].Trim()))) 'shcde-script-extender' }
}
$output = New-ShortWorkDirectory -Root $workspace -Kind t -Name 'SpectatorDock'
& dotnet msbuild (Join-Path $PSScriptRoot 'DockLayoutTests.csproj') /t:Build /verbosity:quiet "/p:OutputPath=$output/" "/p:IntermediateOutputPath=$output/obj/" "/p:ScriptExtenderSourceRoot=$extender"
if ($LASTEXITCODE -ne 0) { throw 'Dock layout harness compilation failed.' }
& (Join-Path $output 'DockLayoutTests.exe') $vanilla (Join-Path $PSScriptRoot '../Patches/Assets/GUI/XAML/IngameUIScreens.xaml') (Join-Path (Split-Path -Parent (Split-Path -Parent $vanilla)) 'XAMLResources/HUD_Main.xaml')
if ($LASTEXITCODE -ne 0) { throw 'Dock patch integration failed.' }
