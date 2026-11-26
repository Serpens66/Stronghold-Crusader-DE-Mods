[CmdletBinding()]
param(
    [ValidateSet('Minimal','Fixes','Integrated','Planner')][string]$Layout='Fixes',
    [string[]]$Scenarios=@('rescue-aivbridge-off','rescue-aivbridge-on'),
    [string]$Map='4spielerkarte.map',
    [ValidateRange(0,180)][int]$PeaceTimeMinutes=60,
    [Parameter(Mandatory)][string]$ApiPluginPackage,
    [Parameter(Mandatory)][string]$ApiPatcherPackage,
    [string]$ExtenderPackage,
    [string]$FixesPackage,
    [string]$BugfixesPackage,
    [string]$PathfindingPackage,
    [string]$CastlePlannerPackage,
    [string]$PerfProbePackage,
    [ValidateRange(60,86400)][int]$TimeoutSeconds=18000,
    [string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [switch]$SkipBuild,
    [switch]$OriginalFixes,
    [switch]$DryRun
)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
. (Join-Path $workspace 'Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1')
function Get-WorkshopReceiptInventory([string]$Game){
    $directory=Join-Path $Game '_APIShared/Workshop/Receipts'
    if([IO.Directory]::Exists($directory)){
        foreach($file in @(Get-ChildItem -LiteralPath $directory -File -Filter '*.json' | Sort-Object Name)){
            [pscustomobject]@{Name=$file.Name;Sha256=(Get-ApiRepositoryHash $file.FullName).ToUpperInvariant()}
        }
    }
}
function Get-WorkshopTestPreflight([string]$Game,[string]$Bootstrap){
    # Execute the supplied bootstrap's READ-ONLY recovery predicate, not a guessed
    # session.lock presence test. Genuine interrupted swaps must never be bypassed.
    $assembly=[Reflection.Assembly]::LoadFile($Bootstrap)
    $type=$assembly.GetType('APIShared.Workshop.Install.InstallRunner',$true)
    $requires=$type.GetMethod('RequiresRecovery',[Reflection.BindingFlags]'Static,NonPublic')
    if(!$requires){throw 'Supplied bootstrap lacks the verified read-only recovery predicate.'}
    $blockers=[Collections.Generic.List[string]]::new()
    if([bool]$requires.Invoke($null,@($Game,$false))){$blockers.Add('active Workshop transaction/journal requires genuine recovery')}
    $work=Join-Path $Game '_APIShared/I'
    foreach($spec in @(@('r','*.request'),@('q','*.batch'))){
        $dir=Join-Path $work $spec[0]
        if([IO.Directory]::Exists($dir)){foreach($file in @(Get-ChildItem -LiteralPath $dir -File -Filter $spec[1])){$blockers.Add($file.FullName)}}
    }
    # These are consumed/cleaned before the runtime manager gate; retain them for
    # ordinary startup/diagnosis rather than letting a test consume user metadata.
    if([IO.File]::Exists((Join-Path $work 'result.txt'))){$blockers.Add((Join-Path $work 'result.txt'))}
    foreach($folder in @('restart','x')){
        $dir=Join-Path $work $folder
        if(![IO.Directory]::Exists($dir)){continue}
        foreach($entry in @(Get-ChildItem -LiteralPath $dir -Directory)){
            $id=[Guid]::Empty;$name=if($folder -eq 'x'){[IO.Path]::GetFileNameWithoutExtension($entry.Name)}else{$entry.Name}
            if([Guid]::TryParseExact($name,'N',[ref]$id)){$blockers.Add($entry.FullName)}
        }
    }
    [pscustomobject]@{Safe=($blockers.Count -eq 0);Blockers=$blockers.ToArray();Receipts=@(Get-WorkshopReceiptInventory $Game)}
}
if([string]::IsNullOrWhiteSpace($Map) -or $Map -match '[\r\n]'){throw 'Map must be one nonempty line.'}
$normalized=[Collections.Generic.List[string]]::new()
foreach($entry in $Scenarios){foreach($part in $entry.Split(',')){if([string]::IsNullOrWhiteSpace($part)){throw 'Empty scenario.'};$normalized.Add($part.Trim())}}
$Scenarios=$normalized.ToArray()
if($Scenarios.Count -lt 1 -or $Scenarios.Count -gt 64){throw 'Provide1..64 scenarios.'}
# Fixture.Owns is the executable routing declaration; no future unwired names.
$fixtureText=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'src/Fixture.cs'))
$owns=[regex]::Match($fixtureText,'internal static bool Owns\(string value\)\s*=>\s*(?<body>[\s\S]*?);')
if(!$owns.Success){throw 'Cannot verify Fixture.Owns declaration; update runner validation before running.'}
$allowed=@([regex]::Matches($owns.Groups['body'].Value,'value\s*==\s*"(?<name>rescue-[a-z0-9-]+)"') | ForEach-Object {$_.Groups['name'].Value})
if(!$allowed.Count){throw 'No declared fixture scenarios.'}
foreach($scenario in $Scenarios){if($scenario -cnotin $allowed){throw "Unknown or unwired scenario: $scenario"}}
if(@($Scenarios | Where-Object {$_.StartsWith('rescue-perf-',[StringComparison]::Ordinal)}).Count){
    if(!$PerfProbePackage){throw 'Performance scenarios require the explicit diagnostic PerfProbe package.'}
    if($Scenarios.Count -ne 1){throw 'Run one performance scenario per transaction; its ABAB members share one run-owned save.'}
}
$game=[IO.Path]::GetFullPath($GameDir);$plugins=Join-Path $game 'BepInEx/plugins'
$api=[IO.Path]::GetFullPath($ApiPluginPackage);$patcher=[IO.Path]::GetFullPath($ApiPatcherPackage)
foreach($source in @($api,$patcher)){
    if($source.StartsWith(([IO.Path]::GetFullPath((Join-Path $game 'BepInEx'))).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
        throw 'Supply the explicitly prepared isolated SDK pair, not the installed APIShared package/bootstrap.'
    }
}
$apiDll=Join-Path $api 'APIShared.dll'
if(![IO.File]::Exists($apiDll) -or ![IO.File]::Exists($patcher)){throw 'Explicit SDK plugin/bootstrap files are missing.'}
$apiVersion=[Reflection.AssemblyName]::GetAssemblyName($apiDll).Version
if($apiVersion -lt [Version]'0.74.0.0'){throw "SDK0.74 or newer required; supplied APIShared is $apiVersion"}
if([Reflection.AssemblyName]::GetAssemblyName($apiDll).Name -cne 'APIShared' -or [Reflection.AssemblyName]::GetAssemblyName($patcher).Name -cne 'APIShared.DependencyBootstrap'){
    throw 'The supplied files are not the APIShared plugin/bootstrap assembly pair.'
}
if(!$ExtenderPackage){$ExtenderPackage=Join-Path $plugins '000shcdese'}
if(!$FixesPackage){$FixesPackage=Join-Path $plugins 'fixes'}
if(!$BugfixesPackage){$BugfixesPackage=Join-Path $workspace 'BugfixesAndQoL/BepInEx/plugins/BugfixesAndQoL_Serp'}
if(!$PathfindingPackage){$PathfindingPackage=Join-Path $workspace 'Pathfinding/BepInEx/plugins/Pathfinding_Serp'}
if(!$CastlePlannerPackage){$CastlePlannerPackage=Join-Path $workspace 'CastlePlanner/BepInEx/plugins/CastlePlanner_Serp'}
$packages=[ordered]@{ '000shcdese'=[IO.Path]::GetFullPath($ExtenderPackage) }
if($Layout -ne 'Minimal'){$packages['fixes']=[IO.Path]::GetFullPath($FixesPackage)}
if($Layout -in @('Integrated','Planner')){$packages['BugfixesAndQoL_Serp']=[IO.Path]::GetFullPath($BugfixesPackage);$packages['Pathfinding_Serp']=[IO.Path]::GetFullPath($PathfindingPackage)}
if($Layout -eq 'Planner'){$packages['CastlePlanner_Serp']=[IO.Path]::GetFullPath($CastlePlannerPackage)}
if($PerfProbePackage){$packages['PerfProbe_Serp']=[IO.Path]::GetFullPath($PerfProbePackage)}
foreach($package in $packages.GetEnumerator()){
    if(![IO.Directory]::Exists($package.Value)){throw "Required comparison package absent: $($package.Value)"}
    $primary=@{'000shcdese'='SHCDESE.dll';'fixes'='fixes.dll';'BugfixesAndQoL_Serp'='BugfixesAndQoL.dll';'Pathfinding_Serp'='Pathfinding.dll';'CastlePlanner_Serp'='CastlePlanner.dll';'PerfProbe_Serp'='PerfProbe.dll'}[$package.Key]
    if(![IO.File]::Exists((Join-Path $package.Value $primary))){throw "Required comparison assembly absent: $($package.Value)/$primary"}
    if(@(Get-ChildItem -LiteralPath $package.Value -File -Filter '*RandomEvents*.dll' -Recurse).Count){throw 'RandomEvents must be absent from the comparison layout.'}
}
if(@(Get-ChildItem -LiteralPath $api -File -Filter '*RandomEvents*.dll' -Recurse).Count){throw 'RandomEvents must be absent from the supplied SDK package.'}
$requestScenario=if($Scenarios.Count -eq 1){$Scenarios[0]}else{'rescue-series'}
$fixesMode=if($Layout -eq 'Minimal'){'Absent'}elseif($OriginalFixes){'Original'}else{'Temporary130'}
$workshop=Get-WorkshopTestPreflight $game $patcher
if(!$workshop.Safe){throw ('Workshop state needs ordinary diagnosis/recovery before this test: '+($workshop.Blockers -join '; '))}
$plan=[ordered]@{Layout=$Layout;Scenarios=$Scenarios;RequestScenario=$requestScenario;Map=$Map;PeaceTimeMinutes=$PeaceTimeMinutes;
    HiddenDesktop=$true;SingleLayoutTransaction=$true;SingleGameProcess=$true;ApiPluginPackage=$api;ApiPatcherPackage=$patcher;
    ApiVersion=$apiVersion.ToString();ComparisonPackages=$packages;TimeoutSeconds=$TimeoutSeconds;FixesMode=$fixesMode;
    WorkshopMode='ProcessLocalTransactionalTestManager';WorkshopPreflight=$workshop}
if($DryRun){
    $evidence=New-ShortWorkDirectory -Root $workspace -Kind a -Name rescue-series-plan -Identity ('read-only '+$Layout)
    [IO.File]::WriteAllText((Join-Path $evidence 'plan.json'),(($plan|ConvertTo-Json -Depth 6)+"`r`n"),[Text.UTF8Encoding]::new($false))
    Write-Output ('DRY_RUN no build, game configuration, installation or launch; plan: '+$evidence)
    [pscustomobject]$plan
    return
}
# Root must release the shared installation/game gate before invoking this path.
# The one generic invocation owns the entire layout transaction and game process.
$seriesMutex=[Threading.Mutex]::new($false,'Local\SHCDE.InGameTest.3024040');$seriesLocked=$false
$managerEnvironmentChanged=$false;$priorManagerLaunch=$null
try{
    try{$seriesLocked=$seriesMutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$seriesLocked=$true}
    if(!$seriesLocked){throw 'Another in-game test is running; no configuration changed.'}
    if(@(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue).Count){throw 'Close the game before starting this series.'}
    $workshop=Get-WorkshopTestPreflight $game $patcher
    if(!$workshop.Safe){throw ('Workshop state changed before layout ownership: '+($workshop.Blockers -join '; '))}
    $plan.WorkshopPreflight=$workshop
    $priorManagerLaunch=[Environment]::GetEnvironmentVariable('SCDEModManagerLaunchId','Process')
    $managerLaunch=if([string]::IsNullOrEmpty($priorManagerLaunch)){'TrappedUnitRescueTest-'+[Guid]::NewGuid().ToString('N')}else{$priorManagerLaunch}
    if(!$SkipBuild){& (Join-Path $PSScriptRoot 'Build.ps1') -nopause -noinstall -ApiSharedDir $api}
    $work=New-ShortWorkDirectory -Root $workspace -Kind t -Name rescue-layout -Identity ('TrappedUnitRescueTest/'+$Layout)
    $evidence=New-ShortWorkDirectory -Root $workspace -Kind a -Name rescue-game -Identity ($Layout+'; '+($Scenarios -join ','))
    $cfg=Join-Path $game 'BepInEx/config/TrappedUnitRescueTest_Serp.cfg'
    $cfgExisted=[IO.File]::Exists($cfg);$cfgBytes=if($cfgExisted){[IO.File]::ReadAllBytes($cfg)}else{$null}
    $perfCfg=Join-Path $game 'BepInEx/config/PerfProbe_Serp.cfg'
    $perfExisted=[IO.File]::Exists($perfCfg);$perfBytes=if($PerfProbePackage -and $perfExisted){[IO.File]::ReadAllBytes($perfCfg)}else{$null}
    $perfBackup=Join-Path $evidence 'original-perf-config.bin'
    if($PerfProbePackage -and $perfExisted){[IO.File]::WriteAllBytes($perfBackup,$perfBytes)}
    $perfRecovery=if($PerfProbePackage){@{Path=$perfCfg;Existed=$perfExisted;Backup=$perfBackup}}else{$null}
    $originalConfig=Join-Path $evidence 'original-test-config.bin'
    if($cfgExisted){[IO.File]::WriteAllBytes($originalConfig,$cfgBytes)}
    $recoveryRecord=Join-Path $evidence 'config-recovery.json'
    $environmentRecord=@{Name='SCDEModManagerLaunchId';Scope='Process';PriorPresent=![string]::IsNullOrEmpty($priorManagerLaunch);Role='transactional test layout manager'}
    [IO.File]::WriteAllText($recoveryRecord,((@{Path=$cfg;Existed=$cfgExisted;Backup=$originalConfig;PerfProbe=$perfRecovery;Pending=$true;Environment=$environmentRecord;WorkshopReceipts=$workshop.Receipts}|ConvertTo-Json -Depth 6)+"`r`n"),[Text.UTF8Encoding]::new($false))
    $failed=$false;$configWritten=$false
    try{
        $layoutPath=Join-Path $work 'plugins';[void][IO.Directory]::CreateDirectory($layoutPath)
        foreach($package in $packages.GetEnumerator()){Copy-Item -LiteralPath $package.Value -Destination (Join-Path $layoutPath $package.Key) -Recurse}
        if($Layout -ne 'Minimal' -and !$OriginalFixes){
            & (Join-Path $PSScriptRoot 'TemporaryFixes/bin/TrappedUnitRescueTest.Fixes130.exe') --patch-copy (Join-Path $layoutPath 'fixes/fixes.dll') *> (Join-Path $evidence 'temporary-fixes.log')
            if($LASTEXITCODE -ne 0){throw 'Temporary Fixes correction rejected disposable dependency copy; game was not started.'}
        }
        [IO.File]::WriteAllText((Join-Path $evidence 'series-plan.json'),(($plan|ConvertTo-Json -Depth 6)+"`r`n"),[Text.UTF8Encoding]::new($false))
        $configWritten=$true
        [IO.File]::WriteAllText($cfg,"[Fixture]`r`nMap = $Map`r`nPeaceTimeMinutes = $PeaceTimeMinutes`r`nScenarios = $($Scenarios -join ',')`r`n",[Text.UTF8Encoding]::new($false))
        if($PerfProbePackage){
            # Equal timing-only instrumentation in both variants. The independent
            # PerfProbe loader and BOTH regular/fallback samplers stay inactive.
            $perfConfig=@'
[Automation]
Enabled = false
AutomationOnly = false
[Sampler]
Enabled = false
LongRunFallback = false
[Spikes]
Enabled = false
RunPhaseTiming = true
TickGapMs = 0
[Zoom]
RenderSnapshots = false
[Extras]
UpdateSweep = false
EngineMarkers = false
BatchCensus = false
GcTracking = false
RecordBreakdown = false
EngineCalls = false
TextureInventory = false
'@
            [IO.File]::WriteAllText($perfCfg,($perfConfig -replace "`r?`n","`r`n"),[Text.UTF8Encoding]::new($false))
        }
        # This program owns the entire disposable layout transaction. Use the
        # documented external-manager contract, not fake UI/caller identity.
        # HiddenDesktop's CreateProcessW inherits this process environment.
        $managerEnvironmentChanged=$true
        [Environment]::SetEnvironmentVariable('SCDEModManagerLaunchId',$managerLaunch,'Process')
        & (Join-Path $workspace 'Shared/Tools/Automation/Invoke-InGameTest.ps1') -GameDir $game -ModGuid TrappedUnitRescueTest_Serp `
            -ModPackage (Join-Path $PSScriptRoot 'BepInEx/plugins/TrappedUnitRescueTest_Serp') -ApiPluginPackage $api -ApiPatcherPackage $patcher `
            -PluginLayout $layoutPath -Scenario $requestScenario -OutputDirectory $evidence -TimeoutSeconds $TimeoutSeconds -HiddenDesktop
        if(@($Scenarios|Where-Object {$_ -cmatch '^rescue-perf-(1000|4000-idle1000walk|9871-idle1000walk)-passive-on$'}).Count){
            # The generic runner has closed the process, flushed/copied the log
            # and restored its transaction. Qualify this separate passive profile
            # before reporting success; outer config restore still runs on failure.
            $runs=@(Get-ChildItem -LiteralPath $evidence -Directory | Where-Object {$_.Name -cmatch '^[0-9a-f]{12}$'})
            if($runs.Count -ne 1){throw 'Passive performance requires one uniquely archived game run.'}
            $savedLog=Join-Path $runs[0].FullName 'LogOutput.log'
            $savedLines=[IO.File]::ReadAllLines($savedLog)
            $terminal=@($savedLines | ForEach-Object {
                $match=[regex]::Match($_,'INGAME_TEST run=(?<run>'+[regex]::Escape($runs[0].Name)+'[0-9a-f]{20}) result=PASS(?:\s|$)')
                if($match.Success){$match.Groups['run'].Value}
            })
            if($terminal.Count -ne 1){throw 'Passive performance lacks the exact archived terminal run identity.'}
            & (Join-Path $PSScriptRoot 'Test-PerformanceLog.ps1') -LogPath $savedLog -ExpectedRun $terminal[0] -ExpectedScenario $Scenarios[0] -OutputPath (Join-Path $evidence 'performance-validation.json')
        }
    }
    catch{$failed=$true;throw}
    finally{
        $gameRunning=@(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue).Count -ne 0
        $innerRecovery=Test-Path -LiteralPath (Join-Path $game 'BepInEx/SerpAutomationRollback')
        if($gameRunning -or $innerRecovery){
            Write-Warning "Recovery deferred: gameRunning=$gameRunning innerRecovery=$innerRecovery; retained $recoveryRecord"
            if(!$failed){throw "Test recovery incomplete: $recoveryRecord"}
        }else{
            if($configWritten){
                if($cfgExisted){[IO.File]::WriteAllBytes($cfg,$cfgBytes)}else{[IO.File]::Delete($cfg)}
                if([IO.File]::Exists($cfg)-ne$cfgExisted){throw 'Test config existence not restored.'}
                if($cfgExisted -and [Convert]::ToBase64String([IO.File]::ReadAllBytes($cfg))-cne[Convert]::ToBase64String($cfgBytes)){throw 'Test config bytes not restored.'}
                if($PerfProbePackage){
                    if($perfExisted){[IO.File]::WriteAllBytes($perfCfg,$perfBytes)}else{[IO.File]::Delete($perfCfg)}
                    if([IO.File]::Exists($perfCfg)-ne$perfExisted){throw 'PerfProbe config existence not restored.'}
                    if($perfExisted -and [Convert]::ToBase64String([IO.File]::ReadAllBytes($perfCfg))-cne[Convert]::ToBase64String($perfBytes)){throw 'PerfProbe config bytes not restored.'}
                }
            }
            $before=@($workshop.Receipts | ForEach-Object {$_.Name+':'+$_.Sha256}) -join '|'
            $after=@(Get-WorkshopReceiptInventory $game | ForEach-Object {$_.Name+':'+$_.Sha256}) -join '|'
            if($before -cne $after){throw 'Workshop receipts changed during the controlled test; evidence retained for diagnosis.'}
            [IO.File]::WriteAllText($recoveryRecord,((@{Path=$cfg;Existed=$cfgExisted;Backup=$originalConfig;PerfProbe=$perfRecovery;Pending=$false;Restored=$true;Environment=$environmentRecord;WorkshopReceipts=$workshop.Receipts}|ConvertTo-Json -Depth 6)+"`r`n"),[Text.UTF8Encoding]::new($false))
            Remove-ShortWorkDirectory -Path $work
        }
        Write-Output ('Test evidence: '+$evidence+'; seriesFailed='+$failed)
    }
}finally{
    try{
        if($managerEnvironmentChanged){
            [Environment]::SetEnvironmentVariable('SCDEModManagerLaunchId',$priorManagerLaunch,'Process')
            if([Environment]::GetEnvironmentVariable('SCDEModManagerLaunchId','Process') -cne $priorManagerLaunch){throw 'Process manager launch environment was not restored.'}
        }
    }finally{if($seriesLocked){$seriesMutex.ReleaseMutex()};$seriesMutex.Dispose()}
}
