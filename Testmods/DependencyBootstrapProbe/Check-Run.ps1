param(
    [ValidateSet('Observe','Active','Disabled')][string]$Mode = 'Observe',
    [string]$LogPath,
    [switch]$RequireShadow,
    [switch]$RequireCorrection,
    [switch]$RequireLifecycle,
    [switch]$RequirePair,
    [switch]$UpdateStage,
    [string]$RecordedGameDir,
    [string]$LifecycleReportPath,
    [string]$ExpectedArtifactRoot = $PSScriptRoot,
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
)
$ErrorActionPreference = 'Stop'
if(-not $RecordedGameDir){$RecordedGameDir=$GameDir}
if($UpdateStage){$RequirePair=$true}
if($RequirePair){$RequireLifecycle=$true}
if($RequireLifecycle){$RequireCorrection=$true;$RequireShadow=$true}
if (-not $LogPath) { $LogPath = Join-Path $GameDir 'BepInEx\DependencyBootstrapProbe.trace.log' }
function FileHash([string]$path) {
    $stream = [IO.File]::OpenRead($path); $sha = [Security.Cryptography.SHA256]::Create()
    try { [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','') } finally { $sha.Dispose(); $stream.Dispose() }
}
$patcherPath = 'BepInEx\patchers\DependencyBootstrapProbe.Patcher.dll'
$pluginPath = 'BepInEx\plugins\DependencyBootstrapProbe_Serp\DependencyBootstrapProbe.dll'
$patcherHash = FileHash (Join-Path $ExpectedArtifactRoot $patcherPath)
$pluginHash = FileHash (Join-Path $ExpectedArtifactRoot $pluginPath)
if ((FileHash (Join-Path $GameDir $patcherPath)) -cne $patcherHash -or (FileHash (Join-Path $GameDir $pluginPath)) -cne $pluginHash) { throw 'Installed probe differs from expected local build.' }
$lines = @(Get-Content -LiteralPath $LogPath)
$starts = @(for ($i=0; $i -lt $lines.Count; $i++) { if ($lines[$i] -match '\[DBP:SESSION\]' -or $lines[$i] -match '\[Message:\s*BepInEx\].*BepInEx .* - Stronghold Crusader Definitive Edition') { $i } })
if ($starts.Count -eq 0) { throw 'No BepInEx session marker; cannot assign evidence to a start.' }
$session = @($lines[$starts[-1]..($lines.Count-1)])
$events = @(for ($i=0; $i -lt $session.Count; $i++) {
    if ($session[$i] -match '\[DBP:([A-Z_]+)\]\s*(.*)$') { [pscustomobject]@{ Marker=$Matches[1]; Detail=$Matches[2]; Line=$i } }
})
function One([string]$name) {
    $hits = @($events | Where-Object Marker -eq $name)
    if ($hits.Count -ne 1) { throw "Missing or repeated marker: $name ($($hits.Count))" }
    $hits[0]
}
function Ordered([string[]]$names) {
    $previous = -1
    foreach ($name in $names) { $entry = One $name; if ($entry.Line -le $previous) { throw "Out-of-order marker: $name" }; $previous = $entry.Line }
}
if ($events.Marker -contains 'ERROR' -or $events.Marker -contains 'SKIP') { throw 'Probe error or skipped hook in latest start.' }
$finish = One 'FINISH'
if ($finish.Detail -notmatch ('(?:^|\s)patcher=' + $patcherHash + '(?:\s|$)')) { throw 'Stale or unidentified patcher build in latest start.' }
if ($Mode -eq 'Disabled') {
    Ordered @('FINISH','DISABLED','LISTENER_ATTACHED','READY_OBSERVED','PLUGIN_DISABLED','CHAIN_COMPLETE')
    if (@($events | Where-Object { $_.Marker -notin @('SESSION','FINISH','DISABLED','LISTENER_ATTACHED','READY_OBSERVED','PLUGIN_DISABLED','CHAIN_COMPLETE') }).Count) { throw 'Active instrumentation appeared during disabled control run.' }
    $plugin = One 'PLUGIN_DISABLED'
    if ((One 'CHAIN_COMPLETE').Detail -cne 'snapshot=False extender=False checks=0') { throw 'Disabled run counters changed.' }
} elseif ($Mode -eq 'Observe') {
    Ordered @('FINISH','MODE','LISTENER_ATTACHED','READY_OBSERVED','PLUGIN_AWAKE','CHAIN_COMPLETE','POST_CLEANUP_RENDER')
    if ((One 'MODE').Detail -cne 'Observe' -or (One 'READY_OBSERVED').Detail -notmatch '^mode=Observe;') { throw 'Not an observation run.' }
    if (@($events | Where-Object { $_.Marker -notin @('SESSION','FINISH','MODE','LISTENER_ATTACHED','READY_OBSERVED','PLUGIN_AWAKE','CHAIN_COMPLETE','POST_CLEANUP_RENDER') }).Count) { throw 'Unexpected instrumentation during observation.' }
    if ((One 'CHAIN_COMPLETE').Detail -cne 'snapshot=False extender=False checks=0') { throw 'Observation changed runtime counters.' }
    $plugin = One 'PLUGIN_AWAKE'
} else {
    if ((One 'MODE').Detail -cne 'Active' -or (One 'READY_OBSERVED').Detail -notmatch '^mode=Active;') { throw 'Not an active run.' }
    if ($events.Marker -contains 'DISABLED' -or $events.Marker -contains 'PLUGIN_DISABLED') { throw 'Probe disabled; use -Mode Disabled for that control run.' }
    $required=@('FINISH','MODE','LISTENER_ATTACHED','READY_OBSERVED','CHAIN_PREPARE','CORE_BINARY','CHAIN_TRANSFORMED','CHAIN_DELEGATE_BEGIN','CHAIN_DELEGATE_END','CHAIN_JIT_BEGIN','CHAIN_JIT_END','CHAIN_COPY_COMPILED','CHAIN_INSTALL','CHAIN_HOOK_INSTALLED','CHAIN_ENTER','SNAPSHOT','BEFORE_EXTENDER','SE_BINARY','SE_PREPARE','SE_TRANSFORMED','SE_DELEGATE_BEGIN','SE_DELEGATE_END','SE_JIT_BEGIN','SE_JIT_END','SE_COPY_COMPILED','SE_INSTALL','SE_HOOK_INSTALLED','COMPATIBILITY_ENTER')
    if(-not $UpdateStage){$required+=@('PLUGIN_AWAKE','CHAIN_COMPLETE','POST_CLEANUP_RENDER')}
    Ordered $required
    if ((One 'BEFORE_EXTENDER').Detail -notmatch '^snapshot=True type=') { throw 'Snapshot not ready before Extender.' }
    if ((One 'COMPATIBILITY_ENTER').Detail -cne 'count=1') { throw 'Compatibility check count mismatch.' }
    if(-not $UpdateStage){
    $plugin = One 'PLUGIN_AWAKE'
    if ((One 'CHAIN_COMPLETE').Detail -cne 'snapshot=True extender=True checks=1') { throw 'Unexpected completion counters.' }
    $post = One 'POST_CLEANUP_RENDER'
    if ($post.Detail -notmatch '^original Unity component destroyed; distinct render frames=(\d+)$' -or [int]$Matches[1] -lt 120) { throw 'Post-cleanup lifetime not proven.' }
    }
    $coreHash = FileHash (Join-Path $GameDir 'BepInEx\core\BepInEx.dll')
    $seHash = FileHash (Join-Path $GameDir 'BepInEx\plugins\000shcdese\SHCDESE.dll')
    if ((One 'CORE_BINARY').Detail -cne "sha256=$coreHash" -or (One 'SE_BINARY').Detail -cne "sha256=$seHash") { throw 'Core/SE binaries differ from tested startup.' }
}
if ($Mode -ne 'Disabled' -and -not $UpdateStage) {
    $post = One 'POST_CLEANUP_RENDER'
    if ($post.Detail -notmatch '^original Unity component destroyed; distinct render frames=(\d+)$' -or [int]$Matches[1] -lt 120) { throw 'Post-cleanup lifetime not proven.' }
}
if ($finish.Detail -notmatch ('(?:^|\s)mode=' + $Mode + '(?:\s|$)')) { throw 'Startup mode mismatch.' }
if (-not $UpdateStage -and $plugin.Detail -notmatch ('(?:^|\s)plugin=' + $pluginHash + '(?:\s|$)')) { throw 'Stale or unidentified plugin build in latest start.' }
if ($RequireShadow -or $RequireCorrection -or $events.Marker -contains 'SHADOW_REQUIRED') {
    if ($Mode -ne 'Active') { throw 'Shadow requires Active mode.' }
    $required=@('READY_OBSERVED','SHADOW_REQUIRED','CHAIN_HOOK_INSTALLED','COMPATIBILITY_ENTER','SHADOW_BEGIN','SHADOW_COMPLETE')
    if(-not $UpdateStage){$required+=@('PLUGIN_AWAKE','CHAIN_COMPLETE')};Ordered $required
    $summary = One 'SHADOW_COMPLETE'
    if ($summary.Detail -notmatch '^candidates=(\d+) original=(\d+) added=(\d+) dependencies=(\d+) issuesBefore=(\d+) issuesAfter=(\d+) originalUnchanged=True$') { throw 'Invalid shadow summary.' }
    $counts = @(1..6 | ForEach-Object { [int]$Matches[$_] })
    $candidateEvents = @($events | Where-Object Marker -eq 'SHADOW_CANDIDATE')
    $dependencyEvents = @($events | Where-Object Marker -eq 'SHADOW_DEPENDENCY')
    $indexEvents = @($events | Where-Object Marker -eq 'SHADOW_INDEX')
    foreach ($event in @($candidateEvents) + @($dependencyEvents) + @($indexEvents)) {
        if ($event.Line -le (One 'SHADOW_BEGIN').Line -or $event.Line -ge $summary.Line) { throw 'Shadow record outside evaluation phase.' }
    }
    $candidates = @($candidateEvents | ForEach-Object { $_.Detail | ConvertFrom-Json })
    $dependencies = @($dependencyEvents | ForEach-Object { $_.Detail | ConvertFrom-Json })
    $indices = @($indexEvents | ForEach-Object { $_.Detail | ConvertFrom-Json })
    if ($counts[0] -ne $candidates.Count -or $counts[1] -ne $indices.Count -or $counts[2] -ne @($candidates | Where-Object state -eq 'Eligible').Count -or $counts[3] -ne $dependencies.Count -or $counts[4] -ne @($dependencies | Where-Object originalIssue -eq $true).Count -or $counts[5] -ne @($dependencies | Where-Object shadowIssue -eq $true).Count) { throw 'Shadow counts inconsistent.' }
    if (@($candidates | Where-Object { $_.state -notin @('Eligible','Excluded','Unclear') -or -not $_.guid -or -not $_.reason }).Count) { throw 'Incomplete candidate decision.' }
    if (@($candidates | Group-Object guid | Where-Object Count -gt 1).Count) { throw 'Duplicate shadow candidate.' }
    if (@($dependencies | Where-Object { $_.originalIssue -isnot [bool] -or $_.shadowIssue -isnot [bool] -or (-not $_.originalIssue -and $_.shadowIssue) }).Count) { throw 'Invalid/regressing shadow result.' }
    foreach ($c in @($candidates | Where-Object state -eq 'Eligible')) { if ($indices.guid -contains $c.guid) { throw 'Original index entry proposed for replacement.' } }
    if ((One 'SNAPSHOT').Detail -notmatch '^selected=(\d+)' -or [int]$Matches[1] -ne $candidates.Count) { throw 'Incomplete shadow inventory.' }
    Write-Host "PASS: shadow inventory complete; $($counts[2]) proposed additions; issues $($counts[4]) -> $($counts[5]); original index unchanged."
}
if ($RequireCorrection -or $events.Marker -contains 'CORRECTION_MODE') {
    $correctionMode = (One 'CORRECTION_MODE').Detail
    if ($correctionMode -notin @('Enabled','Disabled')) { throw 'Invalid correction mode.' }
    if ($RequireCorrection -and $correctionMode -ne 'Enabled') { throw 'Correction was not enabled.' }
    if ($events.Marker -contains 'CORRECTION_FALLBACK') { throw 'Correction fell back to original index.' }
    $required=@('SHADOW_REQUIRED','CORRECTION_MODE','COMPATIBILITY_ENTER','SHADOW_COMPLETE','ORIGINAL_RESULT')
    if(-not $UpdateStage){$required+='PLUGIN_AWAKE'};Ordered $required
    $result = One 'ORIGINAL_RESULT'
    if ($result.Detail -notmatch '^issues=(\d+) expected=(\d+) correction=(True|False)$') { throw 'Invalid original result.' }
    $actualCount=[int]$Matches[1]; $expectedCount=[int]$Matches[2]; $applied=$Matches[3] -eq 'True'
    $issueEvents=@($events|Where-Object Marker -eq 'ORIGINAL_ISSUE')
    $issues=@($issueEvents|ForEach-Object{$_.Detail|ConvertFrom-Json})
    if ($actualCount -ne $expectedCount -or $actualCount -ne $issues.Count -or $applied -ne ($correctionMode -eq 'Enabled')) { throw 'Original result mismatch.' }
    foreach($event in $issueEvents) { if($event.Line -le (One 'SHADOW_COMPLETE').Line -or $event.Line -ge $result.Line) { throw 'Original issue outside return phase.' } }
    if ($applied) {
        $forecast=@($dependencies|Where-Object shadowIssue -eq $true|ForEach-Object { $_.consumer+'|'+$_.dependency }|Sort-Object)
        $returned=@($issues|ForEach-Object { $_.consumer+'|'+$_.dependency }|Sort-Object)
        if(($forecast -join "`n") -cne ($returned -join "`n")){throw 'Original errors do not match predicted consumer/dependency pairs.'}
        Ordered @('SHADOW_COMPLETE','CORRECTION_PREPARED','CORRECTION_APPLIED','ORIGINAL_RESULT')
        if ((One 'CORRECTION_PREPARED').Detail -cne "original=$($counts[1]) effective=$($counts[1]+$counts[2]) added=$($counts[2]) expectedIssues=$($counts[5])" -or (One 'CORRECTION_APPLIED').Detail -cne "effective=$($counts[1]+$counts[2])" -or $actualCount -ne $counts[5]) { throw 'Effective correction counters mismatch.' }
        $receipt=Get-Content -Raw -LiteralPath (Join-Path $ExpectedArtifactRoot 'trial\expectations.json')|ConvertFrom-Json
        if(-not $RequireLifecycle){
        $controlPath=Join-Path $GameDir 'BepInEx\plugins\DependencyBootstrapProbeControl_Serp\info.json'
        if ((FileHash $controlPath) -cne $receipt.ManifestSHA256) { throw 'Control fixture identity mismatch.' }
        $control=Get-Content -Raw -LiteralPath $controlPath|ConvertFrom-Json
        if ($control.GUID -cne $receipt.ControlGuid -or $control.Dependencies.Count -ne 3 -or $actualCount -ne 3) { throw 'Expected exactly three control issues.' }
        foreach($dep in $control.Dependencies) {
            $hits=@($issues|Where-Object { $_.consumer -ceq $receipt.ControlGuid -and $_.dependency -ceq $dep.GUID -and [string]$_.minimum -ceq [string]$dep.MinimumVersion -and [string]$_.maximum -ceq [string]$dep.MaximumVersion -and -not [string]::IsNullOrWhiteSpace($_.reason) })
            if($hits.Count -ne 1) { throw 'Original check did not preserve an exact control requirement.' }
        }
        }
        foreach($pair in $receipt.ResolvedPairs) {
            if($RequireLifecycle -and $pair.consumer -cnotin $candidates.guid){continue}
            $hits=@($dependencies|Where-Object { $_.consumer -ceq $pair.consumer -and $_.dependency -ceq $pair.dependency -and $_.originalIssue -and -not $_.shadowIssue })
            if($hits.Count -ne 1 -or @($issues|Where-Object { $_.consumer -ceq $pair.consumer -and $_.dependency -ceq $pair.dependency }).Count) { throw 'Known false warning was not corrected.' }
        }
        if ($indices.guid -contains $receipt.MissingGuid -or $candidates.guid -contains $receipt.MissingGuid) { throw 'Missing control provider unexpectedly present.' }
        if($RequireLifecycle){Write-Host 'PASS: correction retained; original results match evaluated requirements.'}else{Write-Host 'PASS: local corrected index used; original check returned exactly the three expected control errors.'}
    } elseif ($events.Marker -contains 'CORRECTION_APPLIED' -or $events.Marker -contains 'CORRECTION_PREPARED' -or $actualCount -ne $counts[4]) { throw 'Shadow-only mode changed effective results.' }
}
if($RequireLifecycle){
    Ordered @('READY_OBSERVED','BEFORE_EXTENDER','LIFECYCLE_PREPARE','LIFECYCLE_TRANSFORMED','LIFECYCLE_DELEGATE_BEGIN','LIFECYCLE_DELEGATE_END','LIFECYCLE_JIT_BEGIN','LIFECYCLE_JIT_END','LIFECYCLE_INSTALL','LIFECYCLE_HOOK_INSTALLED','COMPATIBILITY_ENTER')
    if((One 'LIFECYCLE_PREPARE').Detail -cne "binary=$seHash"){throw 'Lifecycle binary mismatch.'}
    if(-not $LifecycleReportPath){$LifecycleReportPath=Join-Path $GameDir 'BepInEx/DependencyBootstrapProbe.lifecycle.json'}
    $pathEvents=@($events|Where-Object Marker -eq 'LIFECYCLE_PATHS')
    $stageEvents=@($events|Where-Object Marker -eq 'LIFECYCLE_STAGED')
    $reportEvents=@($events|Where-Object Marker -eq 'LIFECYCLE_REPORT')
    if(-not $pathEvents.Count -or $pathEvents.Count -ne $stageEvents.Count -or $pathEvents.Count -ne $reportEvents.Count){throw 'Missing lifecycle invocation phase.'}
    $lastLine=(One 'LIFECYCLE_HOOK_INSTALLED').Line
    $seen=@{}
    for($invocation=0;$invocation -lt $pathEvents.Count;$invocation++){
    $pathEvent=$pathEvents[$invocation];$stageEvent=$stageEvents[$invocation];$reportEvent=$reportEvents[$invocation]
    if($pathEvent.Line -le $lastLine -or $stageEvent.Line -le $pathEvent.Line -or $reportEvent.Line -le $stageEvent.Line){throw 'Lifecycle invocation order mismatch.'}
    $lastLine=$reportEvent.Line
    if($reportEvent.Detail -cnotmatch '^run=([a-f0-9]{32}) sha256=[A-F0-9]{64} complete=True$'){throw 'Invalid lifecycle report marker.'}
    $id=$Matches[1];if($seen.ContainsKey($id)){throw 'Repeated lifecycle identity.'};$seen[$id]=$true
    $snapshotPath=Join-Path (Split-Path $LifecycleReportPath) ('DependencyBootstrapProbe.lifecycle.'+$id+'.json')
    $report=Get-Content -LiteralPath $snapshotPath -Raw|ConvertFrom-Json
    $effectivePaths=if($report.PSObject.Properties.Name -contains 'EffectivePaths'){@($report.EffectivePaths)}else{@($report.WorkshopPaths)}
    if($report.Schema -ne 1 -or $report.Complete -isnot [bool] -or -not $report.Complete -or $report.RemovalAuthorized -isnot [bool] -or $report.RemovalAuthorized -or @($report.Errors).Count){throw 'Incomplete or unsafe lifecycle report.'}
    if($report.RunId -cne $id -or $reportEvent.Detail -cne "run=$id sha256=$(FileHash $snapshotPath) complete=True" -or $pathEvent.Detail -cne "run=$id count=$($effectivePaths.Count)" -or $stageEvent.Detail -cne "run=$id before deployment decision"){throw 'Lifecycle report is not bound to this run.'}
    if($report.PatcherSHA256 -cne $patcherHash -or $report.PluginSHA256 -cne $pluginHash -or $report.CoreSHA256 -cne $coreHash -or $report.ExtenderSHA256 -cne $seHash){throw 'Lifecycle build mismatch.'}
    if(@($report.Maps).Count -ne $effectivePaths.Count -or @($report.Owners).Count -ne $candidates.Count -or @($report.Owners|Group-Object Guid|Where-Object Count -gt 1).Count){throw 'Lifecycle inventory incomplete.'}
    foreach($owner in $report.Owners){
        $candidate=@($candidates|Where-Object guid -ceq $owner.Guid)
        if($candidate.Count -ne 1 -or $candidate[0].version -cne $owner.Version -or $candidate[0].path -ine $owner.Path){throw 'Lifecycle owner differs from selected inventory.'}
        if($owner.Guid -cnotin $candidates.guid -or $owner.State -notin @('Unique','Ambiguous','NotFound','Unverifiable') -or -not $owner.Reason -or -not $owner.Path -or -not $owner.Version -or $owner.SHA256 -cnotmatch '^[A-F0-9]{64}$'){throw 'Invalid ownership decision.'}
        if(($owner.State -eq 'Unique' -and @($owner.Claims).Count -ne 1) -or ($owner.State -eq 'Ambiguous' -and @($owner.Claims).Count -lt 2) -or ($owner.State -eq 'NotFound' -and @($owner.Claims).Count -ne 0)){throw 'Ownership evidence inconsistent.'}
    }
    Write-Host 'PASS: lifecycle capture complete and build/run bound; ownership is observation only, never deletion authority.'
    }
    if((FileHash $LifecycleReportPath) -cne (FileHash $snapshotPath)){throw 'Latest lifecycle report does not match final invocation.'}
}
if($RequirePair -or $events.Marker -contains 'PAIR_PREPARED'){
    Ordered @('READY_OBSERVED','PAIR_PREPARED','CHAIN_PREPARE','SNAPSHOT','PAIR_BOUND','BEFORE_EXTENDER','CORRECTION_APPLIED')
    $manifest=Join-Path $GameDir 'BepInEx/plugins/DependencyBootstrapProbe_Serp/dependency-bootstrap-pair.json'
    $pair=Get-Content -LiteralPath $manifest -Raw|ConvertFrom-Json
    if($pair.Schema -ne 1 -or $pair.Guid -cne 'DependencyBootstrapProbe_Serp' -or $pair.RuntimeSHA256 -cne $pluginHash.ToLowerInvariant() -or $pair.BootstrapSHA256 -cne $patcherHash.ToLowerInvariant()){throw 'Pair identities differ from current build.'}
    if((One 'PAIR_PREPARED').Detail -cne ('manifest='+ (FileHash $manifest))){throw 'Pair manifest differs from startup.'}
    $own=@($candidates|Where-Object guid -ceq $pair.Guid)
    if($own.Count -ne 1 -or $own[0].version -cne $pair.Version -or [IO.Path]::GetFullPath($own[0].path) -ine [IO.Path]::GetFullPath((Join-Path $RecordedGameDir $pluginPath))){throw 'Wrong selected pair provider.'}
    if((One 'PAIR_BOUND').Detail -cne ('guid='+$pair.Guid+' version='+$pair.Version+' runtime='+$pluginHash)){throw 'Pair winner binding mismatch.'}
    if($events.Marker -contains 'PAIR_REJECTED'){throw 'Pair rejected.'}
    Write-Host 'PASS: complete package and actual selected provider bound before correction.'
}
Write-Host "PASS: $Mode probe markers and exact build identities in latest session."
if($UpdateStage){Write-Host 'Staging-phase markers only: no completion/menu proof; validate exact staged payload before accepting original dialog.'}else{Write-Host 'Manual acceptance still required: expected dialog and usable main menu; inspect current Unity/BepInEx errors. No gameplay or APIShared integration proof.'}
