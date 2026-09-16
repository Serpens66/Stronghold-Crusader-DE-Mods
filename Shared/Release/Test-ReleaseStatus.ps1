. (Join-Path $PSScriptRoot 'Release.Common.ps1')
. (Join-Path $PSScriptRoot 'ReleaseStatus.Common.ps1')

function Assert-True {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw $Message }
}

$config = Get-ReleaseConfiguration
Assert-True ([string]$config.ApiShared.Guid -ceq 'APIShared_Serp') 'The resolved release configuration must expose the APIShared GUID.'
Assert-True ($null -eq $config.ApiShared.PSObject.Properties['Version']) 'The release configuration must not duplicate the current APIShared version.'
Assert-True ((Get-ApiSharedConsumerMinimum -Config $config -ModName 'BugfixesAndQoL') -ceq '0.3.6') 'BugfixesAndQoL must be recognized as an APIShared consumer.'
Assert-True ((Get-ApiSharedConsumerMinimum -Config $config -ModName 'ExtraFeatures') -ceq '0.3.6') 'ExtraFeatures must be recognized as an APIShared consumer.'
Assert-True ((Get-ApiSharedConsumerMinimum -Config $config -ModName 'ExtendedData') -ceq '0.3.6') 'ExtendedData must be recognized as an APIShared consumer.'
Assert-True ((Get-ApiSharedConsumerMinimum -Config $config -ModName 'BuildingCosts') -ceq '0.3.6') 'BuildingCosts must be classified as an editor lifecycle APIShared consumer.'
$releaseIndexEntries = @(Get-ReleaseIndexEntries -Config $config)
Assert-True ([string]$releaseIndexEntries[0].Project -ceq 'SerpsMods') 'The SerpsMods release-index entry must be first.'
Assert-True ([string]$releaseIndexEntries[0].DisplayName -ceq 'SerpsMods (Modpack)') 'The SerpsMods release-index display name must identify the modpack.'
Assert-True (-not [bool]$releaseIndexEntries[0].ShowCodeStatus) 'The SerpsMods release-index entry must not expose a code-status badge.'
Assert-True ((Get-ReleaseIndexAssetName -Entry $releaseIndexEntries[0] -Version '1.2.3') -ceq 'SerpsMods-v1.2.3.zip') 'The SerpsMods release-index entry must target the versioned ZIP asset.'
Assert-True ((Get-ReleaseIndexSha256 -Entry $releaseIndexEntries[0] -ReleaseBody "ZIP SHA-256: ``$('a' * 64)``") -ceq ('a' * 64)) 'The SerpsMods release-index entry must read the ZIP hash from release notes.'
Assert-True ((Get-ReleaseIndexSha256 -Entry $releaseIndexEntries[1] -ReleaseBody "Thin SHA-256: ``$('b' * 64)``") -ceq ('b' * 64)) 'Normal release-index entries must retain support for thin-package hash labels.'
$samplePackRow = New-ReleaseIndexRow -Config $config -Entry $releaseIndexEntries[0] -Version '1.2.3' -Url 'https://example.invalid/SerpsMods-v1.2.3.zip' -Commit '1234567890abcdef' -Sha256 ('a' * 64)
$samplePackPrefix = "| SerpsMods (Modpack) | [1.2.3](https://example.invalid/SerpsMods-v1.2.3.zip) | $([char]0x2014) | [1234567]"
Assert-True ($samplePackRow.StartsWith($samplePackPrefix)) 'The SerpsMods release-index row must link the ZIP directly and render no status badge.'
$apiSharedPackage = Get-ValidatedApiSharedPackage -Config $config -MinimumVersion '0.3.0'
Assert-True ($apiSharedPackage.Directory -ceq (Join-Path $config.Root 'APIShared\BepInEx\plugins\APIShared_Serp')) 'Release builds must resolve the validated workspace APIShared package.'
Assert-True (Test-Path -LiteralPath $apiSharedPackage.DllPath -PathType Leaf) 'The resolved workspace APIShared package must contain APIShared.dll.'
$apiSharedSourceInfo = Get-Content -LiteralPath $apiSharedPackage.SourceInfoPath -Raw | ConvertFrom-Json
Assert-True ($apiSharedPackage.Version -ceq [string]$apiSharedSourceInfo.Version) 'The validated APIShared version must come from the source manifest.'

function Assert-ApiSharedValidationFails {
    param(
        [Parameter(Mandatory)][string]$SourceGuid,
        [Parameter(Mandatory)][string]$SourceVersion,
        [Parameter(Mandatory)][string]$PackageGuid,
        [Parameter(Mandatory)][string]$PackageVersion,
        [Parameter(Mandatory)][string]$MinimumVersion,
        [Parameter(Mandatory)][string]$ExpectedMessage
    )
    $fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('shcde-api-release-' + [Guid]::NewGuid().ToString('N'))
    try {
        $fixturePackage = Join-Path $fixtureRoot 'APIShared\BepInEx\plugins\APIShared_Serp'
        [void](New-Item -ItemType Directory -Path $fixturePackage -Force)
        [IO.File]::WriteAllText((Join-Path $fixtureRoot 'APIShared\info.json'), "{`"GUID`":`"$SourceGuid`",`"Version`":`"$SourceVersion`"}")
        [IO.File]::WriteAllText((Join-Path $fixturePackage 'info.json'), "{`"GUID`":`"$PackageGuid`",`"Version`":`"$PackageVersion`"}")
        [IO.File]::WriteAllBytes((Join-Path $fixturePackage 'APIShared.dll'), [byte[]]@(0))
        $fixtureConfig = [PSCustomObject]@{
            Root = $fixtureRoot
            ApiShared = [PSCustomObject]@{ Project = 'APIShared'; Guid = 'APIShared_Serp' }
        }
        $failed = $false
        try {
            [void](Get-ValidatedApiSharedPackage -Config $fixtureConfig -MinimumVersion $MinimumVersion)
        } catch {
            $failed = $_.Exception.Message -match $ExpectedMessage
        }
        Assert-True $failed "APIShared validation must reject the fixture with message '$ExpectedMessage'."
    } finally {
        if (Test-Path -LiteralPath $fixtureRoot) { Remove-Item -LiteralPath $fixtureRoot -Recurse -Force }
    }
}

Assert-ApiSharedValidationFails -SourceGuid 'Wrong_GUID' -SourceVersion '1.2.3' -PackageGuid 'Wrong_GUID' -PackageVersion '1.2.3' -MinimumVersion '1.0.0' -ExpectedMessage 'source manifest GUID mismatch'
Assert-ApiSharedValidationFails -SourceGuid 'APIShared_Serp' -SourceVersion 'invalid' -PackageGuid 'APIShared_Serp' -PackageVersion 'invalid' -MinimumVersion '1.0.0' -ExpectedMessage 'source manifest contains invalid version'
Assert-ApiSharedValidationFails -SourceGuid 'APIShared_Serp' -SourceVersion '1.2.3' -PackageGuid 'APIShared_Serp' -PackageVersion '1.2.2' -MinimumVersion '1.0.0' -ExpectedMessage 'package identity mismatch'
Assert-ApiSharedValidationFails -SourceGuid 'APIShared_Serp' -SourceVersion '1.2.3' -PackageGuid 'APIShared_Serp' -PackageVersion '1.2.3' -MinimumVersion '2.0.0' -ExpectedMessage 'below the required minimum'

$releaseModSource = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Release-Mod.ps1'))
Assert-True ($releaseModSource -match '\$env:SHCDE_API_SHARED_DIR = \$apiSharedPackage\.Directory') 'APIShared consumer releases must pass the workspace package to build.bat.'
Assert-True ($releaseModSource -match "Remove-Item -LiteralPath 'Env:SHCDE_API_SHARED_DIR'") 'Release builds must restore an initially undefined APIShared environment override.'
Assert-True ($releaseModSource -match 'BundledVersion = \[string\]\$apiSharedPackage\.Version') 'Release provenance must use the validated APIShared package version.'
$dependencyFixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ('shcde-release-dependencies-' + [Guid]::NewGuid().ToString('N'))
try {
    $fixtureGameDir = Join-Path $dependencyFixtureRoot 'Game'
    $fixtureExtenderDir = Join-Path $dependencyFixtureRoot 'Extender'
    $fixtureCrusaderDll = Join-Path $fixtureGameDir 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
    [void](New-Item -ItemType Directory -Path (Split-Path -Parent $fixtureCrusaderDll), $fixtureExtenderDir -Force)
    [IO.File]::WriteAllBytes($fixtureCrusaderDll, [byte[]]@(0))
    [IO.File]::WriteAllBytes((Join-Path $fixtureExtenderDir 'SHCDESE.dll'), [byte[]]@(0))
    $bugfixMetadata = Get-PluginMetadata -ModName 'BugfixesAndQoL'
    $fixtureMetadata = [PSCustomObject]@{
        Config = [PSCustomObject]@{ Root = $config.Root; GameDir = $fixtureGameDir }
        ModDir = $bugfixMetadata.ModDir
    }
    $bugfixDependencies = @(Get-DependencyRecords -Metadata $fixtureMetadata -ExtenderDir $fixtureExtenderDir -ApiSharedDir $apiSharedPackage.Directory)
    Assert-True (@($bugfixDependencies | Where-Object { $_.Path -ceq '$Repository/APIShared/BepInEx/plugins/APIShared_Serp/APIShared.dll' }).Count -eq 1) 'Release provenance must hash the same workspace APIShared.dll used by the consumer build.'
} finally {
    if (Test-Path -LiteralPath $dependencyFixtureRoot) { Remove-Item -LiteralPath $dependencyFixtureRoot -Recurse -Force }
}
foreach ($consumerBuild in @('BugfixesAndQoL\build.bat', 'ExtendedData\build.bat', 'ExtraFeatures\build.bat', 'Helpers\ActiveAIVDetector\build.bat')) {
    $consumerBuildSource = [IO.File]::ReadAllText((Join-Path $config.Root $consumerBuild))
    Assert-True ($consumerBuildSource -match 'if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"') "$consumerBuild must honor the release APIShared override."
    Assert-True ($consumerBuildSource -match '/p:ApiSharedDir="%API_SHARED_DIR%"') "$consumerBuild must forward the APIShared directory to MSBuild."
}
foreach ($neverReleaseProject in @('ActiveAIVDetector', 'AIDefenseTest', 'MultiplayerLeaveFix', 'SerpsMods', 'VanillaAICExporter')) {
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
Assert-True (Test-RelevantProjectPath -Project 'BuildingCosts' -Path 'BuildingCosts/Override/ScriptExtenderUI/BuildingCostsSettings.xaml') 'Packaged XAML files must be relevant.'
Assert-True (-not (Test-RelevantProjectPath -Project 'BuildingCosts' -Path 'BuildingCosts/UpdateToNewDLL.md')) 'Analysis documentation must be ignored.'
Assert-True (-not (Test-RelevantProjectPath -Project 'BuildingCosts' -Path 'BuildingCosts/README.md')) 'Project README files must be ignored.'
Assert-True (-not (Test-RelevantProjectPath -Project 'BuildingCosts' -Path 'BuildingCosts/Findings/NativeNotes.md')) 'Nested Markdown files must be ignored.'
Assert-True (-not (Test-RelevantProjectPath -Project 'BuildingCosts' -Path 'BuildingCosts/release.bat')) 'Release automation must be ignored.'
Assert-True (-not (Test-RelevantProjectPath -Project 'ExtendedData' -Path 'ExtendedData/README.md')) 'ExtendedData README must be ignored.'
Assert-True (-not (Test-RelevantProjectPath -Project 'ExtendedData' -Path 'ExtendedData/BepInEx/plugins/ExtendedData_Serp/README.md')) 'Package-tree Markdown files must be ignored.'
$extendedDataBuildSource = [IO.File]::ReadAllText((Join-Path $config.Root 'ExtendedData\build.bat'))
Assert-True ($extendedDataBuildSource -notmatch '(?i)README\.md') 'ExtendedData build must not copy or require README.md.'
Assert-True (-not (Test-Path -LiteralPath (Join-Path $config.Root 'ExtendedData\BepInEx\plugins\ExtendedData_Serp\README.md') -PathType Leaf)) 'ExtendedData package must not contain README.md.'

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
