[CmdletBinding()]
param(
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [ValidateSet('crenel-acceptance', 'crenel-editor', 'crenel-vanilla-topology', 'crenel-heights', 'crenel-ai')][string]$Scenario = 'crenel-acceptance'
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
        TimeoutSeconds = 1200
        HiddenDesktop = $true
    }
    & (Join-Path $workspace 'Shared/Tools/Automation/Invoke-InGameTest.ps1') @options
}
finally {
    [Environment]::SetEnvironmentVariable('SCDEModManagerLaunchId', $previousManager, 'Process')
    if ([IO.Directory]::Exists($layout)) { Remove-Item -LiteralPath $layout -Recurse -Force }
}
