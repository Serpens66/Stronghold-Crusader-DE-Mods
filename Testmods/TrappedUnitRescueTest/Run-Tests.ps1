[CmdletBinding()]
param(
    [ValidateSet('Minimal','Fixes','Integrated')][string]$Layout='Fixes',
    [string[]]$Scenarios=@('rescue-off','rescue-on'),
    [string]$Map='test AI.map',
    [string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [switch]$SkipBuild
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
if (@(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue).Count) {
    throw 'Close the game before starting this test series.'
}
if ($Map -match '[\r\n]') { throw 'Map must be a single map filename.' }
$allowed=@('rescue-off','rescue-on','rescue-aiwall-off','rescue-aiwall-on','rescue-moat-off','rescue-moat-on','rescue-worker-off','rescue-worker-on','rescue-siege-off','rescue-siege-on')
foreach($scenario in $Scenarios){if($scenario -notin $allowed){throw "Unknown scenario: $scenario"}}
if (!$SkipBuild) { & (Join-Path $PSScriptRoot 'Build.ps1') -nopause -noinstall }
$work=New-ShortWorkDirectory -Root $workspace -Kind t -Name rescue-layout -Identity ("TrappedUnitRescueTest/$Layout")
$evidence=New-ShortWorkDirectory -Root $workspace -Kind a -Name rescue-game -Identity ("TrappedUnitRescueTest/$Layout; "+($Scenarios -join ','))
$plugins=Join-Path $GameDir 'BepInEx/plugins'
$layoutPath=Join-Path $work 'plugins'
$api=Join-Path $work 'api'
$patcher=Join-Path $work 'patcher'
$cfg=Join-Path $GameDir 'BepInEx/config/TrappedUnitRescueTest_Serp.cfg'
$cfgExisted=[IO.File]::Exists($cfg)
$cfgBytes=if($cfgExisted){[IO.File]::ReadAllBytes($cfg)}else{$null}
$failed=$false
try {
    [void][IO.Directory]::CreateDirectory($layoutPath)
    [void][IO.Directory]::CreateDirectory($patcher)
    $packages=@('000shcdese')
    if($Layout -ne 'Minimal'){$packages+='fixes'}
    if($Layout -eq 'Integrated'){$packages+=@('BugfixesAndQoL_Serp','Pathfinding_Serp')}
    foreach($package in $packages){
        $source=Join-Path $plugins $package
        if(![IO.Directory]::Exists($source)){throw "Required installed comparison package absent: $package"}
        Copy-Item -LiteralPath $source -Destination (Join-Path $layoutPath $package) -Recurse
    }
    Copy-Item -LiteralPath (Join-Path $plugins 'APIShared_Serp') -Destination $api -Recurse
    Copy-Item -LiteralPath (Join-Path $GameDir 'BepInEx/patchers/APIShared.DependencyBootstrap.dll') -Destination $patcher
    [IO.File]::WriteAllText($cfg,"[Fixture]`r`nMap = $Map`r`n",[Text.UTF8Encoding]::new($false))
    foreach($scenario in $Scenarios){
        $output=Join-Path $evidence $scenario
        [void][IO.Directory]::CreateDirectory($output)
        & (Join-Path $workspace 'Shared/Tools/Automation/Invoke-InGameTest.ps1') -GameDir $GameDir `
            -ModGuid TrappedUnitRescueTest_Serp -ModPackage (Join-Path $PSScriptRoot 'BepInEx/plugins/TrappedUnitRescueTest_Serp') `
            -ApiPluginPackage $api -ApiPatcherPackage (Join-Path $patcher 'APIShared.DependencyBootstrap.dll') -PluginLayout $layoutPath `
            -Scenario $scenario -OutputDirectory $output -TimeoutSeconds 900
    }
}
catch { $failed=$true; throw }
finally {
    if($cfgExisted){[IO.File]::WriteAllBytes($cfg,$cfgBytes)}else{[IO.File]::Delete($cfg)}
    # The allocator verifies this exact .work/t root before deleting any reproducible copies.
    Remove-ShortWorkDirectory -Path $work
    Write-Output ("Test evidence: "+$evidence+"; seriesFailed="+$failed)
}
