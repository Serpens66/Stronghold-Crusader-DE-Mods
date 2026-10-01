$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..\..')).Path
$sources = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' -File)
$projectFile = Join-Path $project 'RaidRetargetDiagnostic.csproj'
$textFiles = @($sources) + @(
    (Join-Path $project 'Properties\AssemblyInfo.cs'),
    (Join-Path $project 'info.json'),
    $projectFile,
    (Join-Path $project 'build.bat'),
    (Join-Path $project 'Verify-NativeSearchSpan.ps1'),
    (Join-Path $project 'UpdateToNewDLL.md'),
    $MyInvocation.MyCommand.Path)
$runtimeText = (($sources + @((Get-Item -LiteralPath $projectFile))) |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
$forbiddenJson = 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json'
$forbiddenLifecycle = '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\('
$forbiddenMutations = 'CodePatch\.Write|Marshal\.Write|VirtualProtect|NativeDetour|\.Apply\s*\(|\.Undo\s*\(|\.Enable\s*\(|\.Disable\s*\('
if ($runtimeText -match $forbiddenJson) { throw 'Forbidden runtime JSON dependency.' }
if ($runtimeText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause)\s*\(') { throw 'Forbidden runtime lifecycle teardown.' }
$pluginText = [IO.File]::ReadAllText((Join-Path $project 'src\RaidRetargetDiagnosticPlugin.cs'))
if ($pluginText -match $forbiddenLifecycle) { throw 'Long-lived MonoBehaviour callback in plugin.' }
if ($runtimeText -match $forbiddenMutations) { throw 'Runtime executable mutation or detour detected.' }
foreach ($source in $sources) {
    $sourceText = [IO.File]::ReadAllText($source.FullName)
    if ($source.Name -ne 'RaidSearchObserver.cs' -and $sourceText -match 'X64InlineHook|HookTransaction') {
        throw "Native hooks outside the audited observer: $($source.FullName)"
    }
}
$observer = [IO.File]::ReadAllText((Join-Path $project 'src\RaidSearchObserver.cs'))
if ($observer -notmatch '(?s)if \(!published\)\s*\{\s*try \{ pending\?\.Dispose\(\); \}' -or
    $observer -notmatch 'Volatile.Write\(ref enabled, 1\)' -or
    $observer -notmatch 'handle.Require\(\).DisplacedByteCount != SpanLength' -or
    ([regex]::Matches($observer, '\.Commit\(')).Count -ne 1 -or
    ([regex]::Matches($observer, '\.Dispose\(')).Count -ne 1 -or
    $observer -match 'transaction\??\.Dispose\(|\bpublic void Dispose\(') {
    throw 'Permanent observation-hook publication contract differs.'
}
if ($runtimeText -match 'Assembly-CSharp-publicized') { throw 'Publicized assembly reference detected.' }
$xamlFiles = @(Get-ChildItem -LiteralPath $project -Recurse -Filter '*.xaml' -File)
foreach ($xaml in $xamlFiles) {
    [xml]$document = [IO.File]::ReadAllText($xaml.FullName)
    $contents = @($document.SelectNodes("//*[local-name()='Content']"))
    foreach ($content in $contents) {
        if (@($content.ChildNodes | Where-Object { $_.NodeType -eq 'Element' }).Count -ne 1) {
            throw "XAML Content must have one root element: $($xaml.FullName)"
        }
    }
}
foreach ($path in $textFiles) {
    $fullPath = if ($path -is [IO.FileInfo]) { $path.FullName } else { [string]$path }
    $content = [IO.File]::ReadAllText($fullPath)
    if ($content -match '(?<!\r)\n' -or $content -match '(?<!\r)\r(?!\n)') { throw "Non-CRLF line ending: $fullPath" }
    $literalEscapedNewline = ([string][char]92) + 'r' + ([string][char]92) + 'n'
    if ($content.Contains($literalEscapedNewline)) { throw "Literal backslash-r-backslash-n sequence: $fullPath" }
}
# The execution marker is mandatory for both original and replacement commands.
$runtimeSource = [IO.File]::ReadAllText((Join-Path $project 'src\RaidRetargetDiagnosticRuntime.cs'))
$evaluationSource = [IO.File]::ReadAllText((Join-Path $project 'src\RaidAttackFieldEvaluation.cs'))
if ([regex]::Matches($runtimeSource, '\bEvaluate\(').Count -ne 1 -or
    $runtimeSource -match 'diagnosticResult|unchangedPossiblyStale|changedFromPre' -or
    $evaluationSource -notmatch 'freshness != "nativeSearchObserved"' -or
    $runtimeSource -notmatch 'pre.Evidence.PlayerId == raidPlayer && pre.Evidence.RaidRole == raidGroup') {
    throw 'Shared execution-evidence classifier or Post identity guards differ.'
}
# Manual commands still consume nesting frames, then return before classification/logging.
$manualGuard = $runtimeSource.IndexOf('if (args.Phase != EventHookPhase.Post || !buildingCommand || !raid) return;')
if ($manualGuard -lt $runtimeSource.IndexOf('pre = postStack.Pop();') -or
    $manualGuard -gt $runtimeSource.IndexOf('AttackResult result = Evaluate(') -or
    $runtimeSource -match 'RAID_DIAG_CONTROL_ATTACK' -or
    $pluginText.IndexOf('RaidSearchObserver.Install(') -gt $pluginText.IndexOf('RAID_DIAG_READY:') -or
    $pluginText.IndexOf('RaidSearchObserver.Install(') -lt $pluginText.IndexOf('candidateSession?.Dispose();') -or
    $pluginText -notmatch 'raidMeleeRetarget=\{RaidSearchObserver.IsAvailable\}') {
    throw 'Manual nesting barrier or post-publication readiness contract differs.'
}
$manifest = [IO.File]::ReadAllText((Join-Path $project 'info.json')) | ConvertFrom-Json
$versionMatch = [regex]::Match($pluginText, 'public const string Version = "([^"]+)";')
$version = $versionMatch.Groups[1].Value
$assembly = [IO.File]::ReadAllText((Join-Path $project 'Properties\AssemblyInfo.cs'))
if ($version -ne '0.1.1' -or $manifest.Version -ne $version -or
    $assembly -notmatch ([regex]::Escape('AssemblyVersion("' + $version + '.0")')) -or
    $assembly -notmatch ([regex]::Escape('AssemblyFileVersion("' + $version + '.0")')) -or
    $assembly -notmatch ([regex]::Escape('AssemblyInformationalVersion("' + $version + '")'))) {
    throw 'Active source/manifest/assembly versions differ.'
}
Write-Host 'PASS: manual nesting/log suppression, post-hook readiness and version 0.1.1 consistency.'
# Existing bounded retries must stop before the exhausted-list clearing path on U.
$retryBegin = $runtimeSource.IndexOf('private void ProcessRaidRetries()')
$retryEnd = $runtimeSource.IndexOf('private static bool IsLiveBuildingIdentity', $retryBegin)
$retrySource = $runtimeSource.Substring($retryBegin, $retryEnd - $retryBegin)
if ($retrySource -notmatch '(?s)if \(!commandIssued \|\| fallbackResult == AttackResult.Unknown\).*?finished = true;.*?break;' -or
    $retrySource.IndexOf('if (finished) continue;') -gt $retrySource.IndexOf('TargetBuildingIdOffset) = 0;') -or
    $retrySource -notmatch 'issued < MaximumRetryCommandsPerTick' -or
    $runtimeSource -notmatch 'count < 1 \|\| count > NativeRaidCandidateCapacity' -or
    $runtimeSource -notmatch 'selectedBuildingMissingFromCandidateList' -or
    $runtimeSource -notmatch 'nativeTargetPlayerMismatch' -or
    $runtimeSource -notmatch 'extenderNativeCandidateAddressMismatch') {
    throw 'Bounded retry, unknown-abort or inconsistent-candidate guard differs.'
}
Write-Host 'PASS: shared classification, bounded retries, unknown-abort and candidate-list safety guards.'
& (Join-Path $workspace 'Shared\Test-PermanentNativeRuntimePatches.ps1')
if (-not $?) { throw 'Workspace native runtime regression check failed.' }
& (Join-Path $project 'Verify-NativeSearchSpan.ps1')
if (-not $?) { throw 'Native search span check failed.' }
Write-Host 'PASS: Runtime JSON, lifecycle, plugin callbacks, native mutation, XAML, and CRLF checks.'
