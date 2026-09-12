. (Join-Path $PSScriptRoot 'Release.Common.ps1')
. (Join-Path $PSScriptRoot 'ReleaseStatus.Common.ps1')

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$config = Get-ReleaseConfiguration
Assert-True ([string]$config.ApiShared.Guid -ceq 'APIShared_Serp') 'The resolved release configuration must expose the APIShared GUID.'
Assert-True ([string]$config.ApiShared.Version -ceq '0.3.3') 'The resolved release configuration must expose APIShared v0.3.3.'
Assert-True ((Get-ApiSharedConsumerMinimum -Config $config -ModName 'BugfixesAndQoL') -ceq '0.3.0') 'BugfixesAndQoL must be recognized as an APIShared consumer.'
Assert-True ((Get-ApiSharedConsumerMinimum -Config $config -ModName 'ExtraFeatures') -ceq '0.3.0') 'ExtraFeatures must be recognized as an APIShared consumer.'
Assert-True ($null -eq (Get-ApiSharedConsumerMinimum -Config $config -ModName 'BuildingCosts')) 'BuildingCosts must not be classified as an APIShared consumer.'
$apiSharedPackage = Get-ValidatedApiSharedPackage -Config $config -MinimumVersion '0.3.0'
Assert-True ($apiSharedPackage.Directory -ceq (Join-Path $config.Root 'APIShared\BepInEx\plugins\APIShared_Serp')) 'Release builds must resolve the validated workspace APIShared package.'
Assert-True (Test-Path -LiteralPath $apiSharedPackage.DllPath -PathType Leaf) 'The resolved workspace APIShared package must contain APIShared.dll.'
$releaseModSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Release-Mod.ps1'))
Assert-True ($releaseModSource -match '\$env:SHCDE_API_SHARED_DIR = \$apiSharedPackage\.Directory') 'APIShared consumer releases must pass the workspace package to build.bat.'
Assert-True ($releaseModSource -match "Remove-Item -LiteralPath 'Env:SHCDE_API_SHARED_DIR'") 'Release builds must restore an initially undefined APIShared environment override.'
$bugfixMetadata = Get-PluginMetadata -ModName 'BugfixesAndQoL'
$bugfixDependencies = @(Get-DependencyRecords -Metadata $bugfixMetadata -ExtenderDir (Get-ExtenderDirectory -Metadata $bugfixMetadata) -ApiSharedDir $apiSharedPackage.Directory)
Assert-True (@($bugfixDependencies | Where-Object { $_.Path -ceq '$Repository/APIShared/BepInEx/plugins/APIShared_Serp/APIShared.dll' }).Count -eq 1) 'Release provenance must hash the same workspace APIShared.dll used by the consumer build.'
foreach ($consumerBuild in @('BugfixesAndQoL\build.bat', 'ExtraFeatures\build.bat', 'Helpers\ActiveAIVDetector\build.bat')) {
    $consumerBuildSource = [IO.File]::ReadAllText((Join-Path $config.Root $consumerBuild))
    Assert-True ($consumerBuildSource -match 'if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"') "$consumerBuild must honor the release APIShared override."
    Assert-True ($consumerBuildSource -match '/p:ApiSharedDir="%API_SHARED_DIR%"') "$consumerBuild must forward the APIShared directory to MSBuild."
}
foreach ($neverReleaseProject in @('ActiveAIVDetector', 'AIDefenseTest', 'CustomLordUpload', 'MultiplayerLeaveFix', 'VanillaAICExporter')) {
    Assert-True ($neverReleaseProject -notin $config.Projects) "$neverReleaseProject must not be release-enabled."
    $rejected = $false
    try {
        [void](Get-PluginMetadata -ModName $neverReleaseProject)
    } catch {
        $rejected = $_.Exception.Message -ceq "Project is not release-enabled: $neverReleaseProject"
    }
    Assert-True $rejected "$neverReleaseProject must be rejected explicitly as not release-enabled."
}
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
$apiSharedInputs = @(Get-ExternalProjectInputs -Config $config -Project 'APIShared' -HeadCommit $head -TrackedHeadPaths $trackedHead)
Assert-True ($buildingInputs -contains 'Shared/SerpLocalization.cs') 'BuildingCosts must track its linked localization helper.'
Assert-True (-not ($apiSharedInputs -contains 'Shared/SerpLocalization.cs')) 'APIShared must not track an unreferenced localization helper.'
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
