[CmdletBinding()]
param(
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [ValidateSet('crenel-acceptance', 'crenel-editor', 'crenel-vanilla-topology', 'crenel-heights', 'crenel-ai', 'crenel-stress', 'crenel-diagonal-walk', 'crenel-look', 'crenel-look-corners')][string]$Scenario = 'crenel-acceptance'
)
# One hidden-desktop in-game acceptance of CrenelatedWallTest. Minimal plugin layout (Script
# Extender, Fixes, APIShared, CrenelatedWallTest, this testmod); the runner restores the
# original plugins, settings and saves afterwards.
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
$layout = New-ShortWorkDirectory -Root $workspace -Kind t -Name 'crenel-layout' -Identity 'Minimal plugin layout for CrenelatedWallAcceptance' -Transient
$out = New-ShortWorkDirectory -Root $workspace -Kind a -Name 'crenel-ingame' -Identity 'CrenelatedWallTest parity rules in-game acceptance evidence'
$previousManager = [Environment]::GetEnvironmentVariable('SCDEModManagerLaunchId', 'Process')
try {
    $installed = Join-Path $GameDir 'BepInEx/plugins'
    $plugins = Join-Path $layout 'plugins'
    New-Item -ItemType Directory -Path $plugins | Out-Null
    foreach ($name in @('000shcdese', 'fixes')) { Copy-Item -Recurse -LiteralPath (Join-Path $installed $name) -Destination $plugins }
    Copy-Item -Recurse -LiteralPath (Join-Path $workspace 'Testmods/CrenelatedWallTest/BepInEx/plugins/CrenelatedWallTest_Serp') -Destination $plugins
    # The tests read per-build log lines that only a diagnostics build writes. Compile one into the
    # temporary layout; the installed and committed CrenelatedWallTest stays a normal build.
    $diagOut = Join-Path $layout 'diag'
    $msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    & $msbuild (Join-Path $workspace 'Testmods/CrenelatedWallTest/CrenelatedWallTest.csproj') /t:Rebuild /p:Configuration=Debug /p:SerpDevDiagnostics=true `
        ('/p:ApiSharedDir=' + (Join-Path $workspace 'APIShared/BepInEx/plugins/APIShared_Serp')) ('/p:OutputPath=' + $diagOut + '\') ('/p:IntermediateOutputPath=' + (Join-Path $layout 'obj') + '\') /nologo /verbosity:minimal | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Diagnostics build of CrenelatedWallTest failed.' }
    foreach ($name in 'CrenelatedWallTest.dll', 'CrenelatedWallTest.pdb') { Copy-Item -LiteralPath (Join-Path $diagOut $name) -Destination (Join-Path $plugins ('CrenelatedWallTest_Serp/' + $name)) -Force }
    Write-Host ('[{0:HH:mm:ss.fff}] layout={1} evidence={2}' -f [DateTime]::Now, $plugins, $out)
    $env:SCDEModManagerLaunchId = 'SerpAcceptance-Crenel-' + [Guid]::NewGuid().ToString('N')
    $options = @{
        GameDir = $GameDir
        ModGuid = 'CrenelatedWallAcceptance_Serp'
        ModPackage = Join-Path $workspace 'Testmods/CrenelatedWallAcceptance/BepInEx/plugins/CrenelatedWallAcceptance_Serp'
        ApiPluginPackage = Join-Path $workspace 'APIShared/BepInEx/plugins/APIShared_Serp'
        ApiPatcherPackage = Join-Path $workspace 'APIShared/BepInEx/patchers/APIShared.DependencyBootstrap.dll'
        PluginLayout = $plugins
        Scenario = $Scenario
        OutputDirectory = $out
        TimeoutSeconds = $(if ($Scenario -eq 'crenel-stress') { 5400 } else { 1200 })
        HiddenDesktop = $true
    }
    & (Join-Path $workspace 'Shared/Tools/Automation/Invoke-InGameTest.ps1') @options
}
finally {
    [Environment]::SetEnvironmentVariable('SCDEModManagerLaunchId', $previousManager, 'Process')
    if ([IO.Directory]::Exists($layout)) { Remove-Item -LiteralPath $layout -Recurse -Force }
}
