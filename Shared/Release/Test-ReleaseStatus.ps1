. (Join-Path $PSScriptRoot 'Release.Common.ps1')
. (Join-Path $PSScriptRoot 'ReleaseStatus.Common.ps1')

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$config = Get-ReleaseConfiguration
Assert-True (Test-RelevantProjectPath -Project 'BuildingCosts' -Path 'BuildingCosts/src/BuildingCostsRuntime.cs') 'Mod source must be relevant.'
Assert-True (Test-RelevantProjectPath -Project 'BuildingCosts' -Path 'BuildingCosts/Locales/en-US.txt') 'Locale files must be relevant.'
Assert-True (-not (Test-RelevantProjectPath -Project 'BuildingCosts' -Path 'BuildingCosts/UpdateToNewDLL.md')) 'Analysis documentation must be ignored.'
Assert-True (-not (Test-RelevantProjectPath -Project 'BuildingCosts' -Path 'BuildingCosts/release.bat')) 'Release automation must be ignored.'
Assert-True (Test-RelevantProjectPath -Project 'CustomCustomTrail' -Path 'CustomCustomTrail/README.md') 'The packaged CustomCustomTrail README must be relevant.'

$sample = @'
public const string BuildingCostsTitle = "BuildingCosts.Title";
public const string EnableMod = "Common.EnableMod";
'@
$map = Get-LocalizationConstantMap -Text $sample
$keys = Get-KeysFromLocalizationLines -Lines @('-        { BuildingCostsTitle, "Old" },', '+        { BuildingCostsTitle, "New" },') -BaseConstantMap $map -HeadConstantMap $map
Assert-True ($keys.Contains('BuildingCosts.Title')) 'A fallback-text change must resolve through its constant.'
Assert-True (-not $keys.Contains('Common.EnableMod')) 'Unchanged localization constants must not be inferred.'

$buildingKeySet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
[void]$buildingKeySet.Add('BuildingCosts.Title')
[void]$buildingKeySet.Add('Common.EnableMod')
$extraKeySet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
[void]$extraKeySet.Add('SomeSettings.InstantHorse')
[void]$extraKeySet.Add('Common.EnableMod')
$buildingOnlyLines = @('-        { BuildingCostsTitle, "Old" },', '+        { BuildingCostsTitle, "New" },')
$buildingDecision = Get-LocalizationHunkDecision -ChangedLines $buildingOnlyLines -ProjectKeys $buildingKeySet -BaseConstantMap $map -HeadConstantMap $map
$extraDecision = Get-LocalizationHunkDecision -ChangedLines $buildingOnlyLines -ProjectKeys $extraKeySet -BaseConstantMap $map -HeadConstantMap $map
Assert-True $buildingDecision.IsRelevant 'A mod-specific key must affect its consumer.'
Assert-True (-not $extraDecision.IsRelevant) 'A mod-specific key must not affect another mod.'
$commonLines = @('-        { EnableMod, "Old" },', '+        { EnableMod, "New" },')
Assert-True ((Get-LocalizationHunkDecision -ChangedLines $commonLines -ProjectKeys $buildingKeySet -BaseConstantMap $map -HeadConstantMap $map).IsRelevant) 'A common key must affect BuildingCosts.'
Assert-True ((Get-LocalizationHunkDecision -ChangedLines $commonLines -ProjectKeys $extraKeySet -BaseConstantMap $map -HeadConstantMap $map).IsRelevant) 'A common key must affect ExtraFeatures.'
$logicDecision = Get-LocalizationHunkDecision -ChangedLines @('+        loadedLocale = locale;') -ProjectKeys $extraKeySet -BaseConstantMap $map -HeadConstantMap $map
Assert-True ($logicDecision.IsRelevant -and $logicDecision.IsGlobal) 'General localization logic must affect every consumer.'

$head = ((Invoke-StatusGit -Config $config -Arguments @('rev-parse', 'HEAD^{commit}')).Output -join '').Trim()
$trackedHead = @((Invoke-StatusGit -Config $config -Arguments @('ls-tree', '-r', '--name-only', $head)).Output | ForEach-Object { ([string]$_).Replace('\', '/') })
$buildingInputs = @(Get-ExternalProjectInputs -Config $config -Project 'BuildingCosts' -HeadCommit $head -TrackedHeadPaths $trackedHead)
$defenseInputs = @(Get-ExternalProjectInputs -Config $config -Project 'AIDefenseTest' -HeadCommit $head -TrackedHeadPaths $trackedHead)
Assert-True ($buildingInputs -contains 'Shared/SerpLocalization.cs') 'BuildingCosts must track its linked localization helper.'
Assert-True (-not ($defenseInputs -contains 'Shared/SerpLocalization.cs')) 'AIDefenseTest must not track an unreferenced localization helper.'
$sameCommitComparison = Get-ModStatusComparison -Config $config -Project 'BuildingCosts' -BaseCommit $head -HeadCommit $head
Assert-True $sameCommitComparison.IsCurrent 'Identical source trees must be current.'
$serpText = Get-GitText -Config $config -Revision $head -Path 'Shared/SerpLocalization.cs'
$serpMap = Get-LocalizationConstantMap -Text $serpText
$buildingKeys = Get-ProjectLocalizationKeys -Config $config -Project 'BuildingCosts' -BaseCommit $head -HeadCommit $head -BaseConstantMap $serpMap -HeadConstantMap $serpMap
$extraKeys = Get-ProjectLocalizationKeys -Config $config -Project 'ExtraFeatures' -BaseCommit $head -HeadCommit $head -BaseConstantMap $serpMap -HeadConstantMap $serpMap
Assert-True ($buildingKeys.Contains('BuildingCosts.Title')) 'BuildingCosts must consume BuildingCosts.Title.'
Assert-True (-not $extraKeys.Contains('BuildingCosts.Title')) 'ExtraFeatures must not consume BuildingCosts.Title.'
Assert-True ($buildingKeys.Contains('Common.EnableMod')) 'BuildingCosts must consume Common.EnableMod.'
Assert-True ($extraKeys.Contains('Common.EnableMod')) 'ExtraFeatures must consume Common.EnableMod.'

Write-Host 'Release status tests succeeded.' -ForegroundColor Green
