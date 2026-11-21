[CmdletBinding()]
param(
    [ValidateSet('Minimal','Fixes','Integrated','Planner')][string]$Layout='Fixes',
    [string[]]$Scenarios=@('rescue-off','rescue-on'),
    [string]$Map='test AI.map',
    [ValidateRange(0,180)][int]$PeaceTimeMinutes=0,
    [string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [switch]$SkipBuild
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
# Own the same process-wide game transaction before reading/configuring any installed state.
# Invoke-InGameTest runs synchronously on this thread and acquires this mutex recursively.
$seriesMutex=[Threading.Mutex]::new($false,'Local\SHCDE.InGameTest.3024040')
$seriesLocked=$false
try {
    try { $seriesLocked=$seriesMutex.WaitOne(0) }
    catch [Threading.AbandonedMutexException] { $seriesLocked=$true }
    if(!$seriesLocked){throw 'Another in-game test is already running; no test configuration was changed.'}
if (@(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue).Count) {
    throw 'Close the game before starting this test series.'
}
if ($Map -match '[\r\n]') { throw 'Map must be a single map filename.' }
$allowed=@('rescue-off','rescue-on','rescue-group-off','rescue-group-on','rescue-aiwall-off','rescue-aiwall-on','rescue-moat-off','rescue-moat-on','rescue-moatworker-off','rescue-moatworker-on','rescue-worker-off','rescue-worker-on','rescue-ox-off','rescue-ox-on','rescue-siege-off','rescue-siege-on','rescue-mountedkeep-off','rescue-mountedkeep-on','rescue-aivbridge-off','rescue-aivbridge-on')
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
$configWritten=$false
$originalConfig=Join-Path $evidence 'original-test-config.bin'
if($cfgExisted){[IO.File]::WriteAllBytes($originalConfig,$cfgBytes)}
$recoveryRecord=Join-Path $evidence 'config-recovery.json'
[IO.File]::WriteAllText($recoveryRecord,((@{Path=$cfg;Existed=$cfgExisted;Backup=$originalConfig;Pending=$true} | ConvertTo-Json)+"`r`n"),[Text.UTF8Encoding]::new($false))
try {
    [void][IO.Directory]::CreateDirectory($layoutPath)
    [void][IO.Directory]::CreateDirectory($patcher)
    $packages=@('000shcdese')
    if($Layout -ne 'Minimal'){$packages+='fixes'}
    if($Layout -in @('Integrated','Planner')){$packages+=@('BugfixesAndQoL_Serp','Pathfinding_Serp')}
    if($Layout -eq 'Planner'){$packages+='CastlePlanner_Serp'}
    foreach($package in $packages){
        $source=Join-Path $plugins $package
        if(![IO.Directory]::Exists($source)){throw "Required installed comparison package absent: $package"}
        Copy-Item -LiteralPath $source -Destination (Join-Path $layoutPath $package) -Recurse
    }
    # Temporary upstream correction operates only on this disposable dependency copy.
    # Removing this invocation and TemporaryFixes retires it; the runner restores the
    # original installed Fixes package after every scenario.
    if($Layout -ne 'Minimal'){
        & (Join-Path $PSScriptRoot 'TemporaryFixes/bin/TrappedUnitRescueTest.Fixes130.exe') `
            --patch-copy (Join-Path $layoutPath 'fixes/fixes.dll') *> (Join-Path $evidence 'temporary-fixes.log')
        if($LASTEXITCODE -ne 0){throw 'Temporary Fixes correction rejected this dependency copy; no game was started.'}
    }
    Copy-Item -LiteralPath (Join-Path $plugins 'APIShared_Serp') -Destination $api -Recurse
    Copy-Item -LiteralPath (Join-Path $GameDir 'BepInEx/patchers/APIShared.DependencyBootstrap.dll') -Destination $patcher
    $configWritten=$true
    [IO.File]::WriteAllText($cfg,"[Fixture]`r`nMap = $Map`r`nPeaceTimeMinutes = $PeaceTimeMinutes`r`n",[Text.UTF8Encoding]::new($false))
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
    $gameRunning=@(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue).Count -ne 0
    $innerRecovery=Test-Path -LiteralPath (Join-Path $GameDir 'BepInEx/SerpAutomationRollback')
    if($gameRunning -or $innerRecovery){
        Write-Warning ("Recovery deferred: gameRunning=$gameRunning innerRecovery=$innerRecovery. Config and layout retained; recovery record: $recoveryRecord")
        if(!$failed){throw "Test recovery is incomplete: $recoveryRecord"}
    } else {
        if($configWritten){
            if($cfgExisted){[IO.File]::WriteAllBytes($cfg,$cfgBytes)}else{[IO.File]::Delete($cfg)}
            if([IO.File]::Exists($cfg) -ne $cfgExisted){throw 'Test config existence was not restored.'}
            if($cfgExisted -and [Convert]::ToBase64String([IO.File]::ReadAllBytes($cfg)) -cne [Convert]::ToBase64String($cfgBytes)){throw 'Test config bytes were not restored.'}
        }
        [IO.File]::WriteAllText($recoveryRecord,((@{Path=$cfg;Existed=$cfgExisted;Backup=$originalConfig;Pending=$false;Restored=$true} | ConvertTo-Json)+"`r`n"),[Text.UTF8Encoding]::new($false))
        # The allocator verifies this exact .work/t root before removing reproducible copies.
        Remove-ShortWorkDirectory -Path $work
    }
    Write-Output ("Test evidence: "+$evidence+"; seriesFailed="+$failed)
}

} finally {
    if($seriesLocked){$seriesMutex.ReleaseMutex()}
    $seriesMutex.Dispose()
}
