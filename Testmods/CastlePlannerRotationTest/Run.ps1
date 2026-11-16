[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('castle-0','castle-90','castle-180','castle-270','keep-90','unchanged-east','castle-90-save','editor-east')][string]$Scenario,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [string]$PluginLayout,
    [string]$ApiPluginPackage,
    [string]$ApiPatcherPackage
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$optional = @{}
foreach ($name in @('PluginLayout','ApiPluginPackage','ApiPatcherPackage')) {
    $value = Get-Variable -Name $name -ValueOnly
    if ($value) { $optional[$name] = $value }
}
& (Join-Path $workspace 'Shared/Tools/Automation/Invoke-InGameTest.ps1') -GameDir $GameDir `
    -ModGuid CastlePlannerRotationTest_Serp `
    -ModPackage (Join-Path $PSScriptRoot 'BepInEx/plugins/CastlePlannerRotationTest_Serp') `
    -Scenario $Scenario -OutputDirectory $OutputDirectory -TimeoutSeconds 400 @optional
