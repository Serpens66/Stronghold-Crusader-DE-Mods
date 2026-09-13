[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
. (Join-Path $workspace 'Shared\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')

function Assert-True([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message } }

$inventory = @(Get-Content -Raw -LiteralPath (Join-Path $workspace 'Shared\ScriptExtenderUpdate\mods.json') | ConvertFrom-Json)
Assert-True ($inventory.Count -eq 32) 'Expected 32 inventoried mods.'
Assert-True (@($inventory | Where-Object Plugin).Count -eq 31) 'Expected 31 inventoried C# runtime mods.'
$castlePlannerInventory = @($inventory | Where-Object Name -eq 'CastlePlanner')
Assert-True ($castlePlannerInventory.Count -eq 1 -and
    @($castlePlannerInventory[0].PreservedInstallFiles).Count -eq 1 -and
    $castlePlannerInventory[0].PreservedInstallFiles[0] -eq 'RuntimeData\BlueprintBuildingSizes.tsv') 'CastlePlanner runtime calibration data is not narrowly preserved.'
$activeInventory = @($inventory | Where-Object { $property = $_.PSObject.Properties['Active']; $null -eq $property -or [bool]$property.Value })
Assert-True ($activeInventory.Count -eq 29) 'Expected 29 active inventory entries.'
$order = @(Get-SEBuildOrder $activeInventory)
Assert-True ($order[0].Name -eq 'APIShared') 'APIShared must build first.'
Assert-True ([array]::IndexOf([string[]]$order.Name, 'ActiveAIVDetector') -gt [array]::IndexOf([string[]]$order.Name, 'APIShared')) 'ActiveAIVDetector must follow APIShared.'
Assert-True ($order[-1].Name -eq 'BugfixesAndQoL') 'BugfixesAndQoL must build last.'

$categories = Get-SEChangeCategories @('src/SHCDESE.BepInEx/Detours/Test.cs','src/SHCDESE.BepInEx/Interop/Test.cs','ReverseEngineering/structs/test.h','src/SHCDESE.BepInEx/API/Test.cs','deps/Override/Test.xaml','docs/test.md')
Assert-True ($categories.Native.Count -eq 3) 'Native/AOB/interop classification failed.'
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

$rootInstructions = [IO.File]::ReadAllText((Join-Path $workspace 'AGENTS.md'))
$versionedInstructionLines = @($rootInstructions -split '\r\n' | Where-Object {
    $_ -match 'Script Extender\s+[0-9]+\.[0-9]+\.[0-9]+' -and $_ -notmatch '(?i)bekannt|fehler|fehlalarm'
})
Assert-True ($versionedInstructionLines.Count -eq 0) ("Root AGENTS.md contains a non-error Script Extender version:`n" + ($versionedInstructionLines -join "`n"))
Assert-True (-not $rootInstructions.Contains('KilNpc')) 'Obsolete Script Extender migration history remains in AGENTS.md.'
$scopedInstructions = [IO.File]::ReadAllText((Join-Path $workspace 'Shared\ScriptExtenderUpdate\AGENTS.md'))
Assert-True ($scopedInstructions.Contains('Ohne expliziten Kompatibilitätsplaneintrag') -and
    $scopedInstructions.Contains('tatsächlich installierte Version')) 'Scoped Script Extender update rules are incomplete.'

$nativePath = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$hookAuditPath = Join-Path $workspace 'Shared\ScriptExtenderUpdate\2.5.0-2.6.0.release-hooks.json'
$hookFingerprint = Assert-SEReleaseHookAudit $hookAuditPath $workspace (Join-Path $workspace 'shcde-script-extender') $nativePath
Assert-True ($hookFingerprint.SourceFileCount -eq 49 -and $hookFingerprint.OperationCount -eq 96) 'Release native hook/patch coverage changed unexpectedly.'
$missingAuditFailed = $false
try { Assert-SEReleaseHookAudit ($hookAuditPath + '.missing') $workspace (Join-Path $workspace 'shcde-script-extender') $nativePath | Out-Null } catch { $missingAuditFailed = $true }
Assert-True $missingAuditFailed 'A missing release hook audit did not fail closed.'
$invalidAuditPath = Join-Path ([IO.Path]::GetTempPath()) ('SHCDE-SEHookAudit-' + [Guid]::NewGuid().ToString('N') + '.json')
try {
    $invalidAudit = Get-Content -Raw -LiteralPath $hookAuditPath | ConvertFrom-Json
    $invalidAudit.ReviewedInteractions[0].Classification = 'Unknown'
    [IO.File]::WriteAllText($invalidAuditPath, ($invalidAudit | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    $unknownClassificationFailed = $false
    try { Assert-SEReleaseHookAudit $invalidAuditPath $workspace (Join-Path $workspace 'shcde-script-extender') $nativePath | Out-Null } catch { $unknownClassificationFailed = $true }
    Assert-True $unknownClassificationFailed 'An unknown release hook classification did not fail closed.'
}
finally {
    if (Test-Path -LiteralPath $invalidAuditPath) { Remove-Item -LiteralPath $invalidAuditPath -Force }
}

$tempBase = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$temp = Join-Path $tempBase ('SHCDE-SEUpdateTests-' + [Guid]::NewGuid().ToString('N'))
try {
    [IO.Directory]::CreateDirectory($temp) | Out-Null
    & git -C $temp init --quiet; & git -C $temp config user.email test@example.invalid; & git -C $temp config user.name Test
    [IO.File]::WriteAllText((Join-Path $temp 'tracked.txt'), "one`r`n", [Text.UTF8Encoding]::new($false))
    & git -C $temp add tracked.txt; & git -C $temp commit --quiet -m initial
    $commit=(& git -C $temp rev-parse HEAD).Trim();$tree=(& git -C $temp rev-parse 'HEAD^{tree}').Trim()
    Assert-True (Test-SEGitIdentity $temp $commit $tree) 'Clean Git identity was rejected.'
    [IO.File]::WriteAllText((Join-Path $temp 'ignored.tmp'), 'ignored', [Text.UTF8Encoding]::new($false))
    Assert-True (Test-SEGitIdentity $temp $commit $tree) 'Untracked build-like artifact changed provenance.'
    [IO.File]::WriteAllText((Join-Path $temp 'tracked.txt'), "two`r`n", [Text.UTF8Encoding]::new($false))
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

Write-Host 'PASS: Script Extender update inventory, metadata isolation, hook audit, provenance, reference selection and build order.'
