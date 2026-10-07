$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..')).Path
$runtime = [IO.File]::ReadAllText((Join-Path $project 'src\AiRaidRetargetFixRuntime.cs'))
$observer = [IO.File]::ReadAllText((Join-Path $project 'src\RaidSearchObserver.cs'))
$evaluation = [IO.File]::ReadAllText((Join-Path $project 'src\RaidAttackFieldEvaluation.cs'))
$viewModel = [IO.File]::ReadAllText((Join-Path $project 'src\BugfixesAndQoLViewModel.cs'))
$owner = [IO.File]::ReadAllText((Join-Path $project 'src\BugfixesAndQoLRuntime.cs'))
if ([regex]::Matches($runtime, '\bEvaluate\(').Count -ne 1 -or
    $evaluation -notmatch 'freshness != "nativeSearchObserved"' -or
    $runtime -notmatch 'pre.Evidence.PlayerId == raidPlayer && pre.Evidence.RaidRole == raidGroup') { throw 'Single authoritative search classifier changed.' }
if ($runtime -match 'OnUnitMoveHere|OnBuildingDamage|OnBuildingDelete|RAID_DIAG_|RAID_DIAG_CONTROL_ATTACK|\b(?:OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') { throw 'Diagnostic spam or lifecycle callback in raid runtime.' }
if ($runtime -notmatch 'GameTimeManagerAPI.Instance.OnTick \+= OnTick' -or
    $owner -notmatch 'static AiRaidRetargetFixRuntime processAiRaidRetargetFixRuntime' -or
    $owner -notmatch 'settings.EnableMod && settings.EnableAiRaidRetargetFix' -or
    $runtime -notmatch 'if \(wasActive != active\) ClearObservations\(\)' -or
    $runtime -notmatch 'activation.SessionAllowed = !session.IsEditor;' -or
    $runtime -match 'IsHost|session.IsReplay;') { throw 'Persistent publisher / all-peer logical activation changed.' }
if (!$viewModel.Contains('private bool enableAiRaidRetargetFix = true;') -or
    !$viewModel.Contains('[SyncHostOnly]' + [Environment]::NewLine + '        public bool EnableAiRaidRetargetFix') -or
    !$viewModel.Contains('EnableAiRaidRetargetFix = true;')) { throw 'Host default or reset changed.' }
if ($observer -notmatch '(?s)if \(!published\)\s*\{\s*try \{ pending\?\.Dispose\(\); \}' -or
    $observer -notmatch 'handle.Require\(\).DisplacedByteCount != SpanLength' -or
    ([regex]::Matches($observer,'\.Commit\(')).Count -ne 1 -or
    ([regex]::Matches($observer,'\.Dispose\(')).Count -ne 1 -or
    $observer -match 'transaction\??\.Dispose\(|NativeDetour|CodePatch|VirtualProtect|\.Disable\(') { throw 'Permanent hook contract changed.' }
$retryBegin=$runtime.IndexOf('private void ProcessRaidRetries()')
$retryEnd=$runtime.IndexOf('private static bool IsLiveBuildingIdentity',$retryBegin)
$retry=$runtime.Substring($retryBegin,$retryEnd-$retryBegin)
if ($retry -notmatch '(?s)if \(!commandIssued \|\| fallbackResult == AttackResult.Unknown\).*?finished = true;.*?break;' -or
    $retry.IndexOf('if (finished) continue;') -gt $retry.IndexOf('TargetBuildingIdOffset) = 0;') -or
    $retry -notmatch 'issued < MaximumRetryCommandsPerTick' -or
    $runtime -notmatch 'count < 1 \|\| count > NativeRaidCandidateCapacity' -or
    $runtime -notmatch 'nativeTargetPlayerMismatch' -or
    $runtime -notmatch 'extenderNativeCandidateAddressMismatch' -or
    $runtime -notmatch 'TribeGlobalId == other.TribeGlobalId' -or
    $runtime -notmatch 'private const int RaidGroupCount = 6;') { throw 'Bounded replacement / role identity safety changed.' }
$guard=$runtime.IndexOf('if (args.Phase != EventHookPhase.Post || !buildingCommand || !raid) return;')
if($guard -lt $runtime.IndexOf('pre = TakePostFrame(postStack);') -or $guard -gt $runtime.IndexOf('AttackResult result = Evaluate(')) { throw 'Manual command nesting barrier changed.' }
# Frame cancellation and runtime publication are managed contracts, not new native hooks.
if ($runtime -notmatch 'internal readonly TribeIssueOrderWithTargetEventArgs PreEvent;' -or
    $runtime -notmatch 'stack.Peek\(\).PreEvent.SkipOriginalFunction' -or
    $runtime -notmatch 'PeekSearchFrame\(stack\) == null' -or
    $runtime -match 'if \(!tribeAlive && args.Phase == EventHookPhase.Pre\) return;' -or
    $runtime -notmatch 'buildingCommand && tribeAlive \? ReadAttackCandidateSnapshot\(\)' -or
    $runtime -notmatch 'active = initialized && activation.Active;' -or
    $runtime -notmatch '(?s)private void CompleteInitialization.*?lock \(attackCaptureLock\).*?initialized = true;.*?active = activation.Active;' -or
    $runtime -notmatch 'CompleteInitialization\(pendingOrder, pendingSession\);' -or
    $runtime -notmatch 'FailInitialization\(\);' -or
    $runtime -notmatch 'RejectionCleanupIntervalTicks = 200;' -or
    $runtime -notmatch 'RejectedTargetDurationTicks = 300;' -or
    $runtime -notmatch 'if \(target.Value <= lastTick\) expired.Add\(target.Key\)' -or
    $runtime -notmatch 'nextRejectionCleanupTick = 0;') { throw 'Raid cancellation, initialization or expiry cleanup contract changed.' }
$preStart=$runtime.IndexOf('if (args.Phase == EventHookPhase.Pre)')
$preEnd=$runtime.IndexOf('else if (args.Phase == EventHookPhase.Post',$preStart)
if($runtime.Substring($preStart,$preEnd-$preStart) -match 'PeekSearchFrame|TakePostFrame|SkipOriginalFunction') { throw 'New Pre must not prune a still running outer publisher.' }
foreach($method in @('OnTribeOrder','OnSearchObserved')) {
 $start=$runtime.IndexOf('internal void '+$method+'(')
 $end=if($method -eq 'OnTribeOrder'){$runtime.IndexOf('        private void LogAttackCandidates(',$start)}else{$runtime.IndexOf('        private void PushCommandFrame(',$start)}
 $tail=$runtime.Substring($start,$end-$start)
 if($tail -notmatch '(?s)lock \(attackCaptureLock\)\s*\{\s*if \(!active\) return;') { throw "$method lacks the in-lock active check." }
}
$manifest=[IO.File]::ReadAllText((Join-Path $project 'info.json')) | ConvertFrom-Json
if($manifest.Version -ne '1.0.176') { throw 'Manifest version mismatch' }
foreach($path in @('src\BugfixesAndQoLPlugin.cs','src\Properties\AssemblyInfo.cs','BugfixesAndQoL.csproj')) {
 $text=[IO.File]::ReadAllText((Join-Path $project $path))
 if(!$text.Contains('1.0.176') -or $text.Contains('1.0.173')) { throw "Active version mismatch: $path" }
}
$files=@(Get-ChildItem -LiteralPath $project -File -Recurse | Where-Object {
 $_.FullName -notmatch '\\(?:obj|bin|BepInEx)\\' -and $_.Extension -in '.cs','.csproj','.ps1','.bat','.json','.xaml','.txt','.md','.config'
})
foreach($file in $files) {
 $text=[IO.File]::ReadAllText($file.FullName)
 if($text -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF: $($file.FullName)" }
}
& (Join-Path $project 'tests\AiRaidRetarget.Tests\Verify-NativeSearchSpan.ps1')
if(!$?) { throw 'Native observer validation failed' }
Write-Host 'PASS: AI raid host setting, lifecycle, single classifier, permanent hook, compact logs, bounded retry, versions and CRLF.'
