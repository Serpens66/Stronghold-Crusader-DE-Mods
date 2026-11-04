$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..\..')).Path
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' -File -Recurse)
$projectFile = Join-Path $project 'AIBuildDiagnoseTest.csproj'
$native = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$expectedHash = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
$nativeStream = [IO.File]::OpenRead($native)
try {
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { $actualHash = [BitConverter]::ToString($sha256.ComputeHash($nativeStream)).Replace('-', '') }
    finally { $sha256.Dispose() }
}
finally { $nativeStream.Dispose() }
if (-not [string]::Equals($actualHash, $expectedHash, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Installed native DLL differs from the audited woodcutter build.'
}
& 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' (Join-Path $project 'tools\Verify-RemovalNative.py')
if ($LASTEXITCODE -ne 0) { throw 'Economy/removal entry fingerprint or incoming-edge audit failed.' }
$baselineRoot = Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline'
$current = Get-Content -LiteralPath (Join-Path $baselineRoot 'CURRENT.json') -Raw | ConvertFrom-Json
$functions = Join-Path (Join-Path $baselineRoot $current.semanticDirectory) 'exports\semantic-functions.jsonl'
$audited = @{}
Get-Content -LiteralPath $functions | Where-Object {
    $_ -match '"rva":"0x(51540|58020|58950|58BE0|C3BF0)"'
} | ForEach-Object { $item = $_ | ConvertFrom-Json; $audited[$item.rva] = $item }
if ($audited['0x51540'].calleeRvas -notcontains '0x58950' -or
    $audited['0x58950'].calleeRvas.Count -ne 0 -or
    $audited['0x58950'].dataRvas -contains '0x50EC690' -or
    $audited['0x58BE0'].calleeRvas -notcontains '0xE2610' -or
    $audited['0x58BE0'].dataRvas -notcontains '0x50EC690') {
    throw 'Audited 0x58950 and 0x58BE0 contracts differ.'
}
$farmAudit = @{}
Get-Content -LiteralPath $functions | Where-Object {
    $_ -match '"rva":"0x(539B0|2DBA0|2C9F0|50D80|575B0)"'
} | ForEach-Object { $item = $_ | ConvertFrom-Json; $farmAudit[$item.rva] = $item }
if ($farmAudit['0x539B0'].calleeRvas -notcontains '0x2DBA0' -or
    $farmAudit['0x539B0'].calleeRvas -notcontains '0x2C9F0' -or
    $farmAudit['0x539B0'].calleeRvas -notcontains '0x50D80' -or
    $farmAudit['0x2DBA0'].calleeRvas -notcontains '0xB81A0' -or
    $farmAudit['0x2C9F0'].dataRvas -notcontains '0x379D824' -or
    $farmAudit['0x50D80'].calleeRvas -notcontains '0x575B0' -or
    $farmAudit['0x575B0'].calleeRvas -notcontains '0x6D580' -or
    $farmAudit['0x575B0'].dataRvas -notcontains '0x379E74A') {
    throw 'Audited farm scheduler, choice, cooldown or search contract differs.'
}
$textFiles = @($sourceFiles) + @(
    (Join-Path $project 'Properties\AssemblyInfo.cs'),
    (Join-Path $project 'info.json'),
    $projectFile,
    (Join-Path $project 'build.bat'),
    $MyInvocation.MyCommand.Path)
$runtimeText = (($sourceFiles + @((Get-Item -LiteralPath $projectFile))) |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
if ($runtimeText -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|System\.Runtime\.Serialization\.Json') {
    throw 'Forbidden runtime JSON dependency.'
}
if ($runtimeText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|OnApplicationPause|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') {
    throw 'Long-lived or teardown MonoBehaviour callback in diagnostic runtime.'
}
$generalHook = [IO.File]::ReadAllText((Join-Path $project 'src\GeneralSiteSearchHooks.cs'))
$diagnosticHooks = [IO.File]::ReadAllText((Join-Path $project 'src\Diagnostics\AiBuildDiagnostic.NativeHooks.cs'))
# The moved observer owns its two original permanent detours. Scratch probes
# are explicitly copied code, not game targets; only those may Enable/Dispose.
if ($diagnosticHooks -match 'CodePatch\.Write|VirtualProtect|X64InlineHook|\.Undo\s*\(|\.Disable\s*\(' -or
    [regex]::Matches($diagnosticHooks, 'OwnsHooks = false').Count -ne 2 -or
    [regex]::Matches($diagnosticHooks, 'if \(!hook.Success\) pending.Dispose\(\)').Count -ne 2 -or
    $diagnosticHooks -notmatch 'probe.TargetAddress != unchecked\(\(ulong\)copy.ToInt64\(\)\)' -or
    $diagnosticHooks -notmatch 'probe.Scheme.ToString\(\) != "Indirect"') {
    throw 'Moved diagnostic detour lifetime or copied-buffer backend contract differs.'
}
$otherRuntimeText = (($sourceFiles | Where-Object { $_.Name -notin @('GeneralSiteSearchHooks.cs', 'AiBuildDiagnostic.NativeHooks.cs', 'EconomyNativeHooks.cs', 'WorkerCauseNativeHooks.cs') }) |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
if ($otherRuntimeText -match 'CodePatch\.Write|VirtualProtect|NativeDetour|X64InlineHook|HookTransaction|\.Apply\s*\(|\.Undo\s*\(|\.Enable\s*\(|\.Disable\s*\(' -or
    $generalHook -match 'CodePatch\.Write|VirtualProtect|X64InlineHook|\.Apply\s*\(|\.Undo\s*\(|\.Enable\s*\(|\.Disable\s*\(' -or
    $generalHook -notmatch 'published = this' -or
    $generalHook -notmatch 'hook\.Original' -and $generalHook -notmatch 'aivHook\.Original' -or
    $generalHook -notmatch 'fineHook\.Original') {
    throw 'Diagnostic hook ownership, persistence or one-call Vanilla contract differs.'
}
$readOnlyRuntimeText = (($sourceFiles | Where-Object { $_.Name -ne 'WorkerCauseNativeHooks.cs' }) | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
if ($readOnlyRuntimeText -match 'Marshal\.Write|CreatePrefab\(|DeleteBuildingSafe\(|FixesFarmFilterOverride\.Begin|Config\.Bind\("(?:PlacementProbe|NearbyWoodTest|FarmContractProbe|CanariFarmSwap)' ) {
    throw 'The general diagnostic must not contain active probe actions or settings.'
}
foreach ($file in $textFiles) {
    $path = if ($file -is [IO.FileInfo]) { $file.FullName } else { [string]$file }
    if ([IO.File]::ReadAllText($path) -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF: $path" }
}
Write-Host 'PASS: general native ownership, JSON, lifecycle, read-only and CRLF checks.'
$workerHooks = [IO.File]::ReadAllText((Join-Path $project 'src\Diagnostics\WorkerCauseNativeHooks.cs'))
if ($workerHooks -match '\.Disable\s*\(|\.Undo\s*\(|CodePatch\.Write|Marshal\.Write(?:Int|Structure)' -or
    $workerHooks -match 'Marshal\.WriteByte\((?!demand)' -or $workerHooks -notmatch '!publicationAttempted' -or
    $workerHooks -notmatch 'Placement = OverwrittenInstructionPlacement.Suppress') {
    throw 'Worker observer lifetime, native data gate or displaced-instruction ownership differs.'
}
Write-Host 'PASS: worker cause hooks use process-owned data gates and permanent publication; machine tests verify the guards.'

# New read-only decision/removal/economy boundary. Exact instruction/backend checks
# execute in Diagnostics.Tests on private copies; this guard checks ownership and lifetime.
$economyNative = [IO.File]::ReadAllText((Join-Path $project 'src\Diagnostics\EconomyNativeHooks.cs'))
$economyCapture = [IO.File]::ReadAllText((Join-Path $project 'src\Diagnostics\EconomyDiagnostics.cs'))
$economyModel = [IO.File]::ReadAllText((Join-Path $project 'src\Diagnostics\EconomyEvidenceModel.cs'))
if ($economyNative -match '0xCEB10|0xCEB90|0x3B1D0|0xC8FD7|0x2F454' -or
    $economyNative -notmatch 'node.Ready = true' -or
    $economyNative -notmatch 'if \(!node.Handle.Success\) pending\?\.Dispose' -or
    $economyCapture -notmatch 'MarketPriceEvents.TryRegister' -or
    $economyCapture -notmatch 'BeginSettlement' -or
    $economyCapture -notmatch 'missingPost=true; no-booking' -or
    $economyModel -notmatch '8 \* 1024 \* 1024' -or
    $runtimeText -notmatch 'DiagnosticBudgetBytes - EconomyLogBudget.ReservedBytes') {
    throw 'Economy hook ownership, lifetime, accounting or reserved budget contract differs.'
}
if ($economyCapture -match '\.(SkipOriginalFunction|ReplacementTotal)\s*=' -or
    $economyCapture -match 'CreatePrefab\(|SetGoodAmount\(|SetAICFromBytes\(') {
    throw 'Read-only economy diagnostic must not mutate gameplay.'
}
Write-Host 'PASS: General economy, removal and passive integration boundaries.'

# The installed (never workspace-built) Bugfix owner is the compile-time/runtime contract.
# Same lookup as the csproj: modpack layout first, otherwise the standalone installation.
# Any version from the declared minimum on is accepted while it keeps the observed contract.
$gameRoot = Split-Path -Parent (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $native)))
$retainedBugfix = Join-Path $gameRoot 'BepInEx\plugins\SerpsMods_Serp\Mods\BugfixesAndQoL_Serp\BugfixesAndQoL.dll'
if (-not (Test-Path -LiteralPath $retainedBugfix -PathType Leaf)) { $retainedBugfix = Join-Path $gameRoot 'BepInEx\plugins\BugfixesAndQoL_Serp\BugfixesAndQoL.dll' }
if (-not (Test-Path -LiteralPath $retainedBugfix -PathType Leaf)) { throw 'No installed BugfixesAndQoL package was found.' }
$bugfixDependency = @(([IO.File]::ReadAllText((Join-Path $project 'info.json')) | ConvertFrom-Json).Dependencies | Where-Object { $_.GUID -eq 'BugfixesAndQoL_Serp' })
if ($bugfixDependency.Count -ne 1 -or -not $bugfixDependency[0].MinimumVersion) { throw 'info.json must declare one BugfixesAndQoL_Serp minimum version.' }
$minimumBugfix = [version]$bugfixDependency[0].MinimumVersion
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $gameRoot 'BepInEx\core\Mono.Cecil.dll')))
$retainedAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($retainedBugfix)
try {
    if ($retainedAssembly.Name.Version -lt $minimumBugfix) { throw "Installed Bugfix $($retainedAssembly.Name.Version) is older than the declared minimum $minimumBugfix." }
    $contract = $retainedAssembly.MainModule.GetType('BugfixesAndQoL.Diagnostics.IAiBuildDiagnosticSink')
    if (-not $contract -or $contract.Methods.Count -ne 9) { throw 'Retained Bugfix observation contract differs.' }
    if ($retainedAssembly.MainModule.GetType('BugfixesAndQoL.Diagnostics.AiEconomyObservation')) { throw 'Unexpected newer Bugfix economy publisher.' }
    # The selected installed owner's native span is the compatibility boundary,
    # not its private implementation layout. Missing/moved metadata requires a fresh audit.
    $decisionOwners = @($retainedAssembly.MainModule.Types | Where-Object { @($_.Fields | Where-Object { $_.Name -eq 'InaccessibleBuildingDecisionRva' -and $_.HasConstant }).Count -eq 1 })
    if ($decisionOwners.Count -ne 1) { throw 'Cannot verify the installed accessibility owner span; audit this installed owner before building.' }
    $decisionRva = [int](@($decisionOwners[0].Fields | Where-Object Name -eq 'InaccessibleBuildingDecisionRva')[0].Constant)
    $decisionLengthField = @($decisionOwners[0].Fields | Where-Object { $_.Name -eq 'InaccessibleBuildingDecisionLength' -and $_.HasConstant })
    if ($decisionLengthField.Count -ne 1) { throw 'Installed accessibility owner has no auditable span length.' }
    $decisionEnd = $decisionRva + [int]$decisionLengthField[0].Constant
    foreach ($span in @(@(0x3B270,10), @(0xC8F50,9), @(0x55E10,8))) {
        if ($span[0] -lt $decisionEnd -and $span[0] + $span[1] -gt $decisionRva) { throw 'Removal observer overlaps the installed accessibility owner.' }
    }
    Write-Host ('PASS: additional removal entries disjoint from installed accessibility owner 0x{0:X}..0x{1:X}.' -f $decisionRva,$decisionEnd)
} finally { $retainedAssembly.Dispose() }
if ($economyCapture -match 'AiEconomyObservation' -or $economyCapture -notmatch 'protection-decisions' -or
    $economyCapture -notmatch 'ProtectionSnapshot.Capture' -or
    [IO.File]::ReadAllText($projectFile) -notmatch 'SerpsMods_Serp\\Mods\\BugfixesAndQoL_Serp' -or
    [IO.File]::ReadAllText($projectFile) -notmatch 'plugins\\BugfixesAndQoL_Serp<') {
    throw 'Diagnostic must use the installed Bugfix and explicitly report unavailable protection decisions.'
}
Write-Host "PASS: installed Bugfix $minimumBugfix+ observation contract and explicit protection coverage gap."

# Retained controller compatibility boundary; native backend tests cover the active entries.
$economyContracts = [IO.File]::ReadAllText((Join-Path $project 'src\Diagnostics\EconomyHookContract.cs'))
if ($economyNative -match '(?i)0x3f270|trade-controller' -or
    $economyContracts -match '(?i)0x3f270|4533C04863C24C69C83C5800004C8D158CCF62034789841128161300438B8411' -or
    $economyCapture -match 'kind == "trade-controller"' -or
    $economyCapture -notmatch 'Coverage\("trade-controller", false, TradeControllerCoverageDetail, expectedUnavailable: true\)' -or
    $economyCapture -notmatch 'foreach \(string stage in SummaryStages\)') {
    throw 'Release Fixes compatibility requires no controller detour/contract/active expectation and an explicit coverage gap.'
}
$summaryStages = [regex]::Match($economyCapture, 'string\[\] SummaryStages = \{([^}]+)\}').Groups[1].Value
if (-not $summaryStages -or $summaryStages -match 'trade-controller') {
    throw 'An unobserved controller must not produce a zero-call summary finding.'
}
foreach ($stage in @('purchase-decision', 'ai-buy', 'ai-sell')) {
    if ($summaryStages -notmatch [regex]::Escape('"' + $stage + '"')) { throw "Lost partial trade summary: $stage" }
}
foreach ($entry in @('ActionPair(context, 0x3EC90, "purchase-decision")', 'ActionPair(context, 0x40740, "ai-recruitment")',
    'Add(context, 0x29650, buy,', 'Add(context, 0x29700, sell,',
    'owner.SafeBegin("ai-buy",', 'owner.SafeBegin("ai-sell",')) {
    if (-not $economyNative.Contains($entry)) { throw "Lost retained observation: $entry" }
}
foreach ($wrapper in @('buy', 'sell', 'node')) {
    $originalCall = [regex]::Escape($wrapper + '.Handle.Original(state, player')
    $expectedCount = if ($wrapper -eq 'node') { 2 } else { 1 }
    if ([regex]::Matches($economyNative, $originalCall).Count -ne $expectedCount) {
        throw "Retained wrapper original-call count differs: $wrapper"
    }
}
foreach ($boundary in @('market-gate=unknown', 'buy-sell-alternation=unknown', 'aborted-sale-decisions=unknown',
    'missing-controller-observations-are-not-evidence-of-no-trade')) {
    if (-not $economyCapture.Contains($boundary)) { throw "Lost controller coverage boundary: $boundary" }
}
Write-Host 'PASS: controller absent from installation/contracts/counters; retained partial trade observations and explicit unknown coverage.'
