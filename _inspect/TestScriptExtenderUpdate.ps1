[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
. (Join-Path $workspace 'Shared\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')

function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }

$inventory = @(Get-Content -Raw -LiteralPath (Join-Path $workspace 'Shared\ScriptExtenderUpdate\mods.json') | ConvertFrom-Json)
Assert-True ($inventory.Count -gt 0) 'The mod inventory is empty.'
Assert-True (@($inventory.Name | Sort-Object -Unique).Count -eq $inventory.Count) 'The mod inventory contains duplicate names.'
$pluginInventory = @($inventory | Where-Object Plugin)
Assert-True (@($pluginInventory.Plugin | Sort-Object -Unique).Count -eq $pluginInventory.Count) 'The mod inventory contains duplicate plugin sources.'
$castlePlannerInventory = @($inventory | Where-Object Name -eq 'CastlePlanner')
Assert-True ($castlePlannerInventory.Count -eq 1 -and
    @($castlePlannerInventory[0].PreservedInstallFiles).Count -eq 1 -and
    $castlePlannerInventory[0].PreservedInstallFiles[0] -eq 'RuntimeData\BlueprintBuildingSizes.tsv') 'CastlePlanner runtime calibration data is not narrowly preserved.'
$activeInventory = @($inventory | Where-Object { $property = $_.PSObject.Properties['Active']; $null -eq $property -or [bool]$property.Value })
Assert-True (@($inventory | Where-Object { $_.PSObject.Properties['Active'] -and -not [bool]$_.Active -and -not [string]$_.InactiveReason }).Count -eq 0) 'An inactive inventory entry has no reason.'
$order = @(Get-SEBuildOrder $activeInventory)
Assert-True ($order.Count -eq @($activeInventory | Where-Object Plugin).Count) 'Build order does not cover every active runtime mod exactly once.'
foreach ($mod in $order) {
    $modIndex = [array]::IndexOf([string[]]$order.Name, [string]$mod.Name)
    foreach ($dependency in @($mod.DependsOn)) {
        Assert-True ([array]::IndexOf([string[]]$order.Name, [string]$dependency) -ge 0) "$($mod.Name) has an unknown active dependency: $dependency"
        Assert-True ([array]::IndexOf([string[]]$order.Name, [string]$dependency) -lt $modIndex) "$($mod.Name) is ordered before dependency $dependency."
    }
}

$categories = Get-SEChangeCategories @('src/SHCDESE.BepInEx/Detours/Test.cs','src/SHCDESE.BepInEx/Interop/Test.cs','ReverseEngineering/structs/test.h','ReverseEngineering/structs/test.rcnet','src/SHCDESE.BepInEx/API/Test.cs','deps/Override/Test.xaml','docs/test.md')
Assert-True ($categories.NativeHookCandidates.Count -eq 1) 'Native hook candidate classification failed.'
Assert-True ($categories.InteropContracts.Count -eq 1) 'Interop classification failed.'
Assert-True ($categories.SemanticHeaders.Count -eq 1 -and $categories.SemanticHeaders[0] -like '*.h') 'Semantic header classification failed.'
Assert-True ($categories.NativeProjectArtifacts.Count -eq 1 -and $categories.NativeProjectArtifacts[0] -like '*.rcnet') 'Native project artifact classification failed.'
Assert-True ($categories.SemanticHeaders -notcontains 'ReverseEngineering/structs/test.rcnet') '.rcnet was incorrectly classified as a semantic Ghidra input.'
Assert-True ($categories.ManagedApi.Count -eq 1) 'Managed API classification failed.'
Assert-True ($categories.Assets.Count -eq 1) 'Asset classification failed.'
Assert-True ($categories.Documentation.Count -eq 1) 'Documentation classification failed.'

$fixtureMinimum = '{0}.{1}.{2}' -f 1,0,0
$fixtureTarget = '{0}.{1}.{2}' -f 1,1,0
$fixtureExcludedMaximum = '{0}.{1}.{2}' -f 1,0,9
$validRange=[pscustomobject]@{MinimumScriptExtenderVersion=$fixtureMinimum;MaximumScriptExtenderVersion=''}
Assert-SEManifestExtenderRange $validRange $fixtureTarget 'valid'
$boundedRange=[pscustomobject]@{MinimumScriptExtenderVersion=$fixtureMinimum;MaximumScriptExtenderVersion=$fixtureTarget}
Assert-SEManifestExtenderRange $boundedRange $fixtureTarget 'bounded'
$undefinedRange=[pscustomobject]@{MinimumScriptExtenderVersion='';MaximumScriptExtenderVersion=''}
Assert-SEManifestExtenderRange $undefinedRange $fixtureTarget 'undefined'
$rangeFailed=$false
try { Assert-SEManifestExtenderRange ([pscustomobject]@{MinimumScriptExtenderVersion=$fixtureMinimum;MaximumScriptExtenderVersion=$fixtureExcludedMaximum}) $fixtureTarget 'excluded' } catch { $rangeFailed=$true }
Assert-True $rangeFailed 'MaximumScriptExtenderVersion did not exclude an unsupported target.'

Assert-SEMetadataMutationArguments $false 'Existing' $false $false
$metadataMutationFailed = $false
try { Assert-SEMetadataMutationArguments $false 'Patch' $false $false } catch { $metadataMutationFailed = $true }
Assert-True $metadataMutationFailed 'Bulk patch-version mutation without a compatibility plan did not fail closed.'
$metadataMutationFailed = $false
try { Assert-SEMetadataMutationArguments $true 'Existing' $false $true } catch { $metadataMutationFailed = $true }
Assert-True $metadataMutationFailed 'Legacy changelog mutation remained available beside a compatibility plan.'

$driverText = [IO.File]::ReadAllText((Join-Path $workspace 'Shared\ScriptExtenderUpdate\Invoke-ScriptExtenderUpdate.ps1'))
Assert-True (-not $driverText.Contains('$targetMinimum = $NewVersion')) 'The update driver still raises unplanned minimum versions.'
Assert-True (-not $driverText.Contains("elseif (-not `$CompatibilityPlanFile)")) 'The update driver still creates implicit compatibility entries.'
Assert-True ($driverText.Contains('Get-SEBaselinePreviousCommit')) 'The update driver does not read the previous commit from baseline provenance.'
Assert-True ($driverText.Contains('Assert-SEImpactReview')) 'The update driver does not enforce the impact review.'

$workaroundMarkers = @(rg -n 'SHCDESE-WORKAROUND\(' $workspace -g '*.cs' -g '*.ps1' -g '*.md' -g '!shcde-script-extender/**' -g '!**/bin/**' -g '!**/obj/**' -g '!**/BepInEx/**')
if ($LASTEXITCODE -gt 1) { throw 'Could not inventory Script Extender workaround markers.' }
Write-Host ("Script Extender workaround markers requiring upstream review: {0}" -f $workaroundMarkers.Count)
foreach ($workaroundMarker in $workaroundMarkers) { Write-Host "  $workaroundMarker" }

$rootInstructions = [IO.File]::ReadAllText((Join-Path $workspace 'AGENTS.md'))
$versionedInstructionLines = @($rootInstructions -split '\r\n' | Where-Object {
    $_ -match 'Script Extender\s+[0-9]+\.[0-9]+\.[0-9]+' -and $_ -notmatch '(?i)bekannt|fehler|fehlalarm'
})
Assert-True ($versionedInstructionLines.Count -eq 0) ("Root AGENTS.md contains a non-error Script Extender version:`n" + ($versionedInstructionLines -join "`n"))
Assert-True (-not $rootInstructions.Contains('KilNpc')) 'Obsolete Script Extender migration history remains in AGENTS.md.'
Assert-True ($rootInstructions.Contains('### Aktuell bekannte Script-Extender-Bugs') -and
    $rootInstructions.Contains('etwa 47 MB großen Minidump') -and
    $rootInstructions.Contains('Bei jedem Script-Extender-Update')) 'Root AGENTS.md does not retain and revalidate current version-neutral Script Extender bugs.'
$scopedInstructions = [IO.File]::ReadAllText((Join-Path $workspace 'Shared\ScriptExtenderUpdate\AGENTS.md'))
Assert-True ($scopedInstructions.Contains('Ohne expliziten Kompatibilitätsplaneintrag') -and
    $scopedInstructions.Contains('ohne Versionsnummer') -and
    $scopedInstructions.Contains('Bei jedem Script-Extender-Update') -and
    $scopedInstructions.Contains('positiver Behebungsnachweis')) 'Scoped Script Extender update rules are incomplete.'
Assert-True (-not $scopedInstructions.Contains('nur für die tatsächlich installierte Version')) 'Scoped rules still discard bug information solely by version.'

$unsafeTestExitChecks = @()
$buildDriverPaths = @(& git -C $workspace ls-files '*build.bat')
if ($LASTEXITCODE -ne 0) { throw 'Could not enumerate tracked build drivers.' }
foreach ($relativeDriverPath in $buildDriverPaths) {
    $driverPath = Join-Path $workspace $relativeDriverPath
    $lines = [IO.File]::ReadAllLines($driverPath)
    for ($lineIndex = 0; $lineIndex -lt ($lines.Length - 1); $lineIndex++) {
        if (($lines[$lineIndex] -match '(?i)Tests?\.exe"?\s*$' -or
                $lines[$lineIndex] -match '(?i)^\s*dotnet\s+(run|test)\b') -and
            $lines[$lineIndex + 1] -match '(?i)^\s*if\s+errorlevel\s+1\b') {
            $unsafeTestExitChecks += ('{0}:{1}' -f $driverPath, ($lineIndex + 2))
        }
    }
}
Assert-True ($unsafeTestExitChecks.Count -eq 0) ("Managed test executables use a signed-only errorlevel check:`n" + ($unsafeTestExitChecks -join "`n"))

$extenderRoot = Join-Path $workspace 'shcde-script-extender'
$currentCommit = (& git -C $extenderRoot rev-parse HEAD).Trim()
$currentTree = (& git -C $extenderRoot rev-parse 'HEAD^{tree}').Trim()
$releaseProjectNamesForFingerprint = @((Get-Content -Raw -LiteralPath (Join-Path $workspace 'Shared\Release\release-projects.json') | ConvertFrom-Json).Projects)
$hookFingerprint = Get-SEReleaseNativeHookFingerprint $workspace $releaseProjectNamesForFingerprint
Assert-True ($hookFingerprint.SourceFileCount -eq $hookFingerprint.Sources.Count) 'Release hook fingerprint source accounting is inconsistent.'
$missingAuditFailed = $false
try { Assert-SEReleaseHookAudit (Join-Path $workspace 'Shared\ScriptExtenderUpdate\missing.release-hooks.json') $workspace $extenderRoot 'missing-native.dll' | Out-Null } catch { $missingAuditFailed = $true }
Assert-True $missingAuditFailed 'A missing release hook audit did not fail closed.'

$compatibilityPlanPath = Join-Path $workspace 'Shared\ScriptExtenderUpdate\2.7.1-2.8.0.compatibility.json'
$compatibilityPlan = Get-Content -Raw -LiteralPath $compatibilityPlanPath | ConvertFrom-Json
$baselineIdentityPath = Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\sem\FBCB9319\IDENTITY.json'
$baselineIdentity = Get-Content -Raw -LiteralPath $baselineIdentityPath | ConvertFrom-Json
$reviewChangedFiles = @(& git -C $extenderRoot diff --name-only "$($compatibilityPlan.ImpactReview.BaselinePreviousCommit)..$currentCommit")
$reviewSelection = @(Assert-SEImpactReview $compatibilityPlan.ImpactReview $reviewChangedFiles $activeInventory ([string]$compatibilityPlan.ImpactReview.BaselinePreviousCommit))
Assert-True ($reviewSelection.Count -eq 0) 'The 2.7.1 to 2.8.0 impact review unexpectedly selects mod builds.'
Assert-True (@($compatibilityPlan.ImpactReview.CrusaderNativeFiles).Count -eq 0) 'The 2.8.0 review incorrectly requires a Crusader native hook audit.'
Assert-True ([string]$baselineIdentity.scriptExtenderCommit -eq $currentCommit) 'The semantic identity does not point to the current extender commit.'
Assert-True ([string]$baselineIdentity.scriptExtenderTree -eq $currentTree) 'The semantic identity does not point to the current extender tree.'

$patternsPath = Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\sem\FBCB9319\sources\patterns.jsonl'
$matchesPath = Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\sem\FBCB9319\sources\pattern-matches.jsonl'
$patterns = @(Get-Content -LiteralPath $patternsPath | ForEach-Object { $_ | ConvertFrom-Json })
$matches = @(Get-Content -LiteralPath $matchesPath | ForEach-Object { $_ | ConvertFrom-Json })
$monoPatterns = @($patterns | Where-Object targetModule -eq 'mono-2.0-bdwgc.dll')
Assert-True ($monoPatterns.Count -gt 0) 'External Mono AOBs have no targetModule metadata.'
Assert-True (@($matches | Where-Object targetModule -eq 'mono-2.0-bdwgc.dll').Count -eq 0) 'External Mono AOBs were scanned against CrusaderDE.dll.'
Assert-True (@($patterns | Where-Object { -not [string]$_.targetModule }).Count -eq 0) 'An AOB has no targetModule.'

$ghidraDecisionPath = Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\sem\FBCB9319\validation\script-extender-update.json'
$ghidraDecision = Get-Content -Raw -LiteralPath $ghidraDecisionPath | ConvertFrom-Json
Assert-True (-not [bool]$ghidraDecision.ghidraRequired) 'Unchanged actual Ghidra inputs did not take the fast path.'
Assert-True (@($ghidraDecision.changedInputs).Count -eq 0) 'The fast path reports unexpected changed Ghidra inputs.'

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$temp = Join-Path $tempBase ('SHCDE-SEUpdateTests-' + [Guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory($temp) | Out-Null
    & git -C $temp init --quiet; & git -C $temp config user.email test@example.invalid; & git -C $temp config user.name Test
    [IO.File]::WriteAllText((Join-Path $temp 'tracked.txt'), "one`r`n", [Text.UTF8Encoding]::new($false))
    & git -C $temp add tracked.txt; & git -C $temp commit --quiet -m initial
    $previousCommit=(& git -C $temp rev-parse HEAD).Trim()
    $identityPath = Join-Path $temp 'IDENTITY.json'
    [IO.File]::WriteAllText($identityPath, (([ordered]@{scriptExtenderCommit=$previousCommit} | ConvertTo-Json) + "`r`n"), [Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText((Join-Path $temp 'tracked.txt'), "two`r`n", [Text.UTF8Encoding]::new($false))
    & git -C $temp add tracked.txt; & git -C $temp commit --quiet -m target
    $commit=(& git -C $temp rev-parse HEAD).Trim();$tree=(& git -C $temp rev-parse 'HEAD^{tree}').Trim()
    Assert-True ((Get-SEBaselinePreviousCommit $identityPath $temp $commit) -eq $previousCommit) 'Baseline provenance was not used as the previous commit.'
    Assert-True (Test-SEGitIdentity $temp $commit $tree) 'Clean Git identity was rejected.'
    [IO.File]::WriteAllText((Join-Path $temp 'ignored.tmp'), 'ignored', [Text.UTF8Encoding]::new($false))
    Assert-True (Test-SEGitIdentity $temp $commit $tree) 'Untracked build-like artifact changed provenance.'
    [IO.File]::WriteAllText((Join-Path $temp 'tracked.txt'), "three`r`n", [Text.UTF8Encoding]::new($false))
    Assert-True (-not (Test-SEGitIdentity $temp $commit $tree)) 'Tracked modification was not detected.'
}
finally {
    if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp -Recurse -Force }
}

$missing = Join-Path $tempBase ('missing-' + [Guid]::NewGuid().ToString('N'))
$failed=$false;try{Resolve-SEExtenderDirectory '' $missing|Out-Null}catch{$failed=$true}
Assert-True $failed 'Missing installed extender did not fail closed.'
$alternate = Join-Path $tempBase ('alternate-' + [Guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory($alternate) | Out-Null
    [IO.File]::WriteAllBytes((Join-Path $alternate 'SHCDESE.dll'), [byte[]]@(0))
    Assert-True ((Resolve-SEExtenderDirectory $alternate $missing) -eq (Resolve-Path -LiteralPath $alternate).Path) 'Explicit ExtenderDir was not selected.'
}
finally {
    if (Test-Path -LiteralPath $alternate) { Remove-Item -LiteralPath $alternate -Recurse -Force }
}

$buildTemp = Join-Path $tempBase ('SHCDE-SEBuildTests-' + [Guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory($buildTemp) | Out-Null
    $hitOne=Join-Path $buildTemp 'one.hits';$hitTwo=Join-Path $buildTemp 'two.hits'
    $nl=[Environment]::NewLine
    $oneText='@echo off'+$nl+'echo hit>>"'+$hitOne+'"'+$nl+'exit /b 0'+$nl
    $twoFailText='@echo off'+$nl+'echo hit>>"'+$hitTwo+'"'+$nl+'exit /b 7'+$nl
    $twoPassText='@echo off'+$nl+'echo hit>>"'+$hitTwo+'"'+$nl+'exit /b 0'+$nl
    [IO.File]::WriteAllText((Join-Path $buildTemp 'one.bat'), $oneText, [Text.Encoding]::ASCII)
    [IO.File]::WriteAllText((Join-Path $buildTemp 'two.bat'), $twoFailText, [Text.Encoding]::ASCII)
    [IO.File]::WriteAllText((Join-Path $buildTemp 'fake.csproj'), '<Project />', [Text.Encoding]::ASCII)
    $fakeMods=@(
        [pscustomobject]@{Name='One';Plugin='one';Project='fake.csproj';BuildDriver='one.bat';BuildOrder=1;DependsOn=@()},
        [pscustomobject]@{Name='Two';Plugin='two';Project='fake.csproj';BuildDriver='two.bat';BuildOrder=2;DependsOn=@('One')}
    )
    Assert-True (@(Get-SEBuildSelection $fakeMods @() @()).Count -eq 0) 'An empty impact review selected mod builds.'
    $providerClosure = @(Get-SEBuildSelection $fakeMods @('One') @())
    Assert-True (($providerClosure.Name -join ',') -eq 'One,Two') 'Affected provider did not select its consumer closure.'
    $consumerClosure = @(Get-SEBuildSelection $fakeMods @('Two') @())
    Assert-True (($consumerClosure.Name -join ',') -eq 'One,Two') 'Affected consumer did not select its required provider.'
    $fakeState=@{CompletedBuilds=@()}
    $stopped=$false
    try { Invoke-SECheckpointBuild $fakeMods $buildTemp $buildTemp $fakeState {} {} } catch { $stopped=$true }
    Assert-True ($stopped -and $fakeState.CompletedBuilds.Count -eq 1 -and $fakeState.CompletedBuilds[0] -eq 'One') 'Failed build did not preserve the last safe checkpoint.'
    [IO.File]::WriteAllText((Join-Path $buildTemp 'two.bat'), $twoPassText, [Text.Encoding]::ASCII)
    Invoke-SECheckpointBuild $fakeMods $buildTemp $buildTemp $fakeState {} {}
    Assert-True (@(Get-Content -LiteralPath $hitOne).Count -eq 1) 'Resume rebuilt an already successful mod.'
    Assert-True ($fakeState.CompletedBuilds.Count -eq 2) 'Resume did not complete the remaining build.'
}
finally {
    if (Test-Path -LiteralPath $buildTemp) { Remove-Item -LiteralPath $buildTemp -Recurse -Force }
}

$releaseProjectNames = @((Get-Content -Raw -LiteralPath (Join-Path $workspace 'Shared\Release\release-projects.json') | ConvertFrom-Json).Projects)
$releaseInventory = @($activeInventory | Where-Object { $_.Name -in $releaseProjectNames })
$projects=@($releaseInventory|Where-Object Plugin|ForEach-Object{Join-Path $workspace $_.Project})
foreach($project in $projects){$text=[IO.File]::ReadAllText($project);$installed=$text.IndexOf('BepInEx\plugins\000shcdese</ExtenderDir>');$local=$text.IndexOf("LocalScriptExtenderBuildOutput)\SHCDESE.dll");Assert-True ($installed-ge0-and($local-lt0-or$installed-lt$local)) "Installed extender is not first in $project"}
foreach($driver in @($releaseInventory|Where-Object Plugin|ForEach-Object{Join-Path $workspace $_.BuildDriver})){Assert-True ([IO.File]::ReadAllText($driver).Contains('SHCDESE_EXTENDER_DIR')) "Explicit override missing in $driver"}

Write-Host 'PASS: Script Extender update relevance, module filtering, provenance, metadata isolation, reference selection and affected build closure.'
