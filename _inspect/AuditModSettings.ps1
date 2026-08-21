$ErrorActionPreference = 'Stop'

$workspace = Split-Path -Parent $PSScriptRoot
$settings = [ordered]@{
    BugfixesAndQoL = 'BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml'
    BuildingCosts = 'BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Override/ScriptExtenderUI/BuildingCostsSettings.xaml'
    BuildingLimit = 'BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Override/ScriptExtenderUI/BuildingLimitSettings.xaml'
    ExtraFeatures = 'ExtraFeatures/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml'
    ImprovedHunters = 'ImprovedHunters/BepInEx/plugins/ImprovedHunters_Serp/Override/ScriptExtenderUI/ImprovedHuntersSettings.xaml'
    RandomEvents = 'RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml'
    CastlePlanner = 'CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Override/ScriptExtenderUI/CastlePlannerSettings.xaml'
    CustomCustomTrail = 'CustomCustomTrail/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml'
    StartConditions = 'StartConditions/BepInEx/plugins/StartConditions_Serp/Override/ScriptExtenderUI/StartConditionsSettings.xaml'
    UnitCosts = 'UnitCosts/BepInEx/plugins/UnitCosts_Serp/Override/ScriptExtenderUI/UnitCostsSettings.xaml'
    UnitLimit = 'UnitLimit/BepInEx/plugins/UnitLimit_Serp/Override/ScriptExtenderUI/UnitLimitSettings.xaml'
}

$localeDirectories = [ordered]@{
    BugfixesAndQoL = 'BugfixesAndQoL/Locales'
    BuildingCosts = 'BuildingCosts/Locales'
    BuildingLimit = 'BuildingLimit/Locales'
    ExtraFeatures = 'ExtraFeatures/Locales'
    ImprovedHunters = 'ImprovedHunters/Locales'
    RandomEvents = 'RandomEvents/Locales'
    CastlePlanner = 'CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Locales'
    CustomCustomTrail = 'CustomCustomTrail/Locales'
    StartConditions = 'StartConditions/Locales'
    UnitCosts = 'UnitCosts/Locales'
    UnitLimit = 'UnitLimit/Locales'
}

$interactiveNames = @('Button', 'CheckBox', 'ComboBox', 'Slider', 'TextBox')
foreach ($entry in $settings.GetEnumerator()) {
    $path = Join-Path $workspace $entry.Value
    [xml]$xml = [IO.File]::ReadAllText($path)
    $manager = [Xml.XmlNamespaceManager]::new($xml.NameTable)
    $manager.AddNamespace('p', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')

    foreach ($elementName in $interactiveNames) {
        foreach ($element in $xml.SelectNodes("//p:$elementName", $manager)) {
            $tooltip = $element.GetAttribute('ToolTip')
            $explicitTooltip = $element.SelectSingleNode("./p:$elementName.ToolTip/p:ToolTip", $manager)
            $hasExplicitTooltip = $null -ne $explicitTooltip -and
                -not [string]::IsNullOrWhiteSpace($explicitTooltip.GetAttribute('Content'))
            $duration = $element.GetAttribute('ToolTipService.ShowDuration')
            if (([string]::IsNullOrWhiteSpace($tooltip) -and -not $hasExplicitTooltip) -or $duration -ne '60000') {
                throw "$($entry.Key): $elementName without a nonempty tooltip and exact 60000 ms duration."
            }
            if ($elementName -eq 'TextBox' -and
                $element.GetAttribute('KeyboardCaptureBinding.Enabled', 'clr-namespace:SHCDESE.UI;assembly=SHCDESE') -ne 'True') {
                throw "$($entry.Key): TextBox without ui:KeyboardCaptureBinding.Enabled=True."
            }
            if ($hasExplicitTooltip -and
                -not $explicitTooltip.GetAttribute('Style').Contains('ModSettingsToolTipStyle')) {
                throw "$($entry.Key): explicit $elementName tooltip does not use the shared modsettings tooltip style."
            }
        }
    }

    $activationNodes = @($xml.SelectNodes(
        "//*[local-name()='CheckBox' and (contains(@IsChecked, 'EnableMod') or contains(@IsChecked, 'EnableClientFeatures'))]"))
    if ($activationNodes.Count -eq 0) {
        throw "$($entry.Key): no activation checkbox found."
    }
    foreach ($activationNode in $activationNodes) {
        if ($activationNode.ParentNode.LocalName -ne 'Border' -or
            -not $activationNode.ParentNode.GetAttribute('Style').Contains('ActivationBorder')) {
            throw "$($entry.Key): activation checkbox is not inside a colored activation border."
        }
    }

    $text = [IO.File]::ReadAllText($path)
    $requiredMarkers = @(
        'TargetType="{x:Type ToolTip}"',
        'VerticalScrollBarVisibility="Auto"',
        'HorizontalScrollBarVisibility="Auto"',
        'Value="#FF1D1710"',
        'MaxWidth="{x:Static shared:ToolTipPresentation.MaximumWidth}"',
        'Value="{x:Static shared:ToolTipPresentation.FontSize}"',
        'FontSize="{TemplateBinding FontSize}"',
        'TextWrapping="Wrap"')
    $requiredMarkers += @(
        'x:Key="HostRoleHeader"',
        'x:Key="ClientRoleHeader"',
        'x:Key="SectionHeader"',
        'x:Key="HostActivationBorder"',
        'x:Key="ClientActivationBorder"',
        'Text="{Binding PresetText}"')
    foreach ($required in $requiredMarkers) {
        if (-not $text.Contains($required)) {
            throw "$($entry.Key): required shared UI marker is missing: $required"
        }
    }

    if ($entry.Key -eq 'RandomEvents') {
        foreach ($requiredValueBinding in @(
            'IntervalMonthsValueText', 'CooldownMonthsValueText',
            'FairChanceValueText', 'FireChanceValueText',
            'LionMinValueText', 'BanditMinValueText', 'ArcherMinValueText', 'TheftMinValueText')) {
            if (-not $text.Contains($requiredValueBinding)) {
                throw "RandomEvents: Slider unit binding is missing: $requiredValueBinding"
            }
        }
    }
    if ($entry.Key -eq 'CustomCustomTrail') {
        foreach ($required in @(
            'x:Key="ModSettingsToolTipStyle"',
            '<CheckBox.ToolTip>',
            'Style="{StaticResource ModSettingsToolTipStyle}"',
            'Content="{Binding HelpText}"')) {
            if (-not $text.Contains($required)) {
                throw "CustomCustomTrail: dynamic mod checkbox tooltip marker is missing: $required"
            }
        }
    }
    if ($entry.Key -eq 'ExtraFeatures') {
        foreach ($requiredValueBinding in @(
            'MarketBuyPriceMultiplierValueText', 'MarketSellPriceMultiplierValueText',
            'MarketPricesAlsoForAI', 'MarketPricesAlsoForAIHelpText',
            'PlagueDurationMultiplierValueText', 'ApothecaryPlagueSearchDistanceValueText')) {
            if (-not $text.Contains($requiredValueBinding)) {
                throw "ExtraFeatures: Slider unit binding is missing: $requiredValueBinding"
            }
        }
    }
}

$toolTipPresentationPath = Join-Path $workspace 'Shared/ToolTipPresentation.cs'
$toolTipPresentation = [IO.File]::ReadAllText($toolTipPresentationPath)
foreach ($required in @(
    'public static class ToolTipPresentation',
    'SE_ToolTip',
    'public static float FontSize => 25.0f;',
    'public static float MaximumWidth => 1000.0f;')) {
    if (-not $toolTipPresentation.Contains($required)) {
        throw "Shared fixed tooltip presentation marker is missing: $required"
    }
}
$castleRuntimeSource = [IO.File]::ReadAllText((Join-Path $workspace 'CastlePlanner/src/CastlePlannerRuntime.cs'))
foreach ($required in @(
    'expectedAivCastlePlayers',
    'failedAivCastlePlayers',
    'expectedPlayers.SequenceEqual(executedPlayers)',
    'finally')) {
    if (-not $castleRuntimeSource.Contains($required)) {
        throw "CastlePlanner exact-once spawn verification marker is missing: $required"
    }
}
foreach ($forbidden in @(
    'DependencyProperty FontSizeProperty',
    'DependencyProperty MaximumWidthProperty',
    'UnityEngine',
    'Screen.',
    'ResolutionScale',
    'DiagnosticLog',
    'SERP_TOOLTIP_DIAGNOSTIC',
    'ToolTipFontSizeExtension',
    'ToolTipMaximumWidthExtension')) {
    if ($toolTipPresentation.Contains($forbidden)) {
        throw "Obsolete tooltip implementation marker is still present: $forbidden"
    }
}

$currentTooltipXaml = @(
    'BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml',
    'BugfixesAndQoL/BepInEx/plugins/BugfixesAndQoL_Serp/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml',
    'BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Override/ScriptExtenderUI/BuildingCostsSettings.xaml',
    'BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Override/ScriptExtenderUI/BuildingLimitSettings.xaml',
    'CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Override/ScriptExtenderUI/CastlePlannerSettings.xaml',
    'CustomCustomTrail/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml',
    'CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml',
    'ExtraFeatures/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml',
    'ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml',
    'ImprovedHunters/BepInEx/plugins/ImprovedHunters_Serp/Override/ScriptExtenderUI/ImprovedHuntersSettings.xaml',
    'RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml',
    'RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml',
    'SerpsModsHost/Override/ScriptExtenderUI/SerpsModsStatus.xaml',
    'SerpsModsHost/BepInEx/plugins/SerpsMods_Serp/Override/ScriptExtenderUI/SerpsModsStatus.xaml',
    'StartConditions/BepInEx/plugins/StartConditions_Serp/Override/ScriptExtenderUI/StartConditionsSettings.xaml',
    'UnitCosts/BepInEx/plugins/UnitCosts_Serp/Override/ScriptExtenderUI/UnitCostsSettings.xaml',
    'UnitLimit/BepInEx/plugins/UnitLimit_Serp/Override/ScriptExtenderUI/UnitLimitSettings.xaml')
foreach ($relativeXamlPath in $currentTooltipXaml) {
    $xamlPath = Join-Path $workspace $relativeXamlPath
    $xamlText = [IO.File]::ReadAllText($xamlPath)
    foreach ($required in @(
        'x:Static shared:ToolTipPresentation.FontSize',
        'x:Static shared:ToolTipPresentation.MaximumWidth')) {
        if (-not $xamlText.Contains($required)) {
            throw "${relativeXamlPath}: fixed shared tooltip marker is missing: $required"
        }
    }
    foreach ($forbidden in @('shared:ToolTipFontSize', 'shared:ToolTipMaximumWidth')) {
        if ($xamlText.Contains($forbidden)) {
            throw "${relativeXamlPath}: obsolete tooltip markup extension is still present: $forbidden"
        }
    }
}

foreach ($entry in $localeDirectories.GetEnumerator()) {
    $directory = Join-Path $workspace $entry.Value
    $files = @(Get-ChildItem -LiteralPath $directory -File -Filter '*.txt' | Sort-Object Name)
    if ($files.Count -eq 0) {
        throw "$($entry.Key): no locale files found."
    }

    $referenceKeys = $null
    foreach ($file in $files) {
        $values = [ordered]@{}
        foreach ($line in [IO.File]::ReadAllLines($file.FullName)) {
            if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) {
                continue
            }

            $separator = $line.IndexOf('=')
            if ($separator -le 0) {
                throw "$($entry.Key)/$($file.Name): malformed locale line: $line"
            }

            $key = $line.Substring(0, $separator).Trim()
            $value = $line.Substring($separator + 1).Trim()
            if ($values.Contains($key)) {
                throw "$($entry.Key)/$($file.Name): duplicate locale key $key"
            }
            if ([string]::IsNullOrWhiteSpace($value)) {
                throw "$($entry.Key)/$($file.Name): empty locale value for $key"
            }
            $values[$key] = $value
        }

        $keys = @($values.Keys | Sort-Object)
        if ($null -eq $referenceKeys) {
            $referenceKeys = $keys
        } elseif (Compare-Object $referenceKeys $keys) {
            throw "$($entry.Key)/$($file.Name): locale key set differs from the other languages."
        }
    }
}

# Every productive lobby-settings registration must pass through the one Shared
# transport/preset/convergence boundary. This also prevents mod-local copies of
# temporary network workarounds from silently diverging.
$productiveRoots = @($settings.Keys) + @('SerpsModsHost')
$productiveCsFiles = @($productiveRoots | ForEach-Object {
    Get-ChildItem -LiteralPath (Join-Path $workspace $_) -Recurse -File -Filter '*.cs'
})
$directRegistrations = @($productiveCsFiles | Where-Object {
    $_.FullName -notlike '*\Shared\PresetLobbyModSettingsViewModel.cs' -and
    [IO.File]::ReadAllText($_.FullName).Contains('RegisterLobbyModSettings(')
})
if ($directRegistrations.Count -ne 0) {
    throw "Direct lobby-settings registration bypasses Shared: $($directRegistrations.FullName -join ', ')"
}

$registrationFiles = @($productiveCsFiles | Where-Object {
    [IO.File]::ReadAllText($_.FullName).Contains('LobbyModSettingsPresetRegistration.Register(')
})
foreach ($registrationFile in $registrationFiles) {
    $projectDirectory = $registrationFile.Directory
    while ($null -ne $projectDirectory -and
        @(Get-ChildItem -LiteralPath $projectDirectory.FullName -File -Filter '*.csproj').Count -eq 0) {
        $projectDirectory = $projectDirectory.Parent
    }
    if ($null -eq $projectDirectory) {
        throw "No project found for lobby-settings registration: $($registrationFile.FullName)"
    }
    $project = @(Get-ChildItem -LiteralPath $projectDirectory.FullName -File -Filter '*.csproj')[0]
    $projectText = [IO.File]::ReadAllText($project.FullName)
    if (-not $projectText.Contains('Shared\PresetLobbyModSettingsViewModel.cs')) {
        throw "$($project.Name): lobby settings do not compile the Shared preset/sync implementation."
    }
}

$perPlayerViewModels = @(
    'BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs',
    'CastlePlanner/src/CastlePlannerSettingsViewModel.cs',
    'CustomCustomTrail/src/CustomCustomTrailSettingsViewModel.cs')
foreach ($relativePath in $perPlayerViewModels) {
    $source = [IO.File]::ReadAllText((Join-Path $workspace $relativePath))
    $nestedTypeStart = $source.IndexOf('private sealed class', [StringComparison]::Ordinal)
    if ($nestedTypeStart -ge 0) {
        $source = $source.Substring(0, $nestedTypeStart)
    }
    $matches = [Text.RegularExpressions.Regex]::Matches(
        $source,
        '(?s)\[SyncPerPlayer[^\]]*\]\s*public\s+[\w<>,\.\[\]\s]+?\s+(?<name>\w+)\s*\{')
    foreach ($match in $matches) {
        $propertyName = $match.Groups['name'].Value
        if (-not $source.Contains("$($propertyName)Data")) {
            throw "${relativePath}: [SyncPerPlayer] property $propertyName has no companion ${propertyName}Data."
        }
    }
    if ($matches.Count -gt 0 -and
        -not $source.Contains('ConfigurePerPlayerLobbySettings(')) {
        throw "${relativePath}: personal settings do not declare their Shared lobby policy."
    }
}

$sharedSettingsSource = [IO.File]::ReadAllText((Join-Path $workspace 'Shared/PresetLobbyModSettingsViewModel.cs'))
foreach ($required in @(
    'ActivatePerPlayerLobbySettings',
    'PerPlayerLobbySettingsBuilder',
    'ResetSlotsWith',
    'RequireReport',
    'System_ArePerPlayerSettingsReady',
    'ScriptExtenderMultiplayerSyncWorkaround.EnsureInstalled')) {
    if (-not $sharedSettingsSource.Contains($required)) {
        throw "Shared multiplayer-settings contract marker is missing: $required"
    }
}
if ([Text.RegularExpressions.Regex]::Matches(
        $sharedSettingsSource,
        'internal sealed class PerPlayerLobbySettingsCoordinator').Count -ne 1) {
    throw 'Shared must compile one common per-player coordinator in production and tests.'
}
foreach ($required in @(
    'OnUnloadMap.Observable.Subscribe',
    'args.Phase == EventHookPhase.Post',
    'member.dummyToBeKicked',
    'member.SkirmishMember && !member.SkirmishHumanMember',
    'Lobby mod settings registration aborted')) {
    if (-not $sharedSettingsSource.Contains($required)) {
        throw "Shared per-player lifecycle/roster marker is missing: $required"
    }
}

$crlfTargets = @($settings.Values) + @(
    $localeDirectories.Values | ForEach-Object {
        Get-ChildItem -LiteralPath (Join-Path $workspace $_) -File -Filter '*.txt' |
            ForEach-Object { [IO.Path]::GetRelativePath($workspace, $_.FullName) }
    }
) + @(
    'Shared/PresetLobbyModSettingsViewModel.cs',
    'BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs',
    'BugfixesAndQoL/src/BugfixesAndQoLPlugin.cs',
    'CastlePlanner/src/CastlePlannerSettingsViewModel.cs',
    'CastlePlanner/src/CastlePlannerPlugin.cs',
    'CustomCustomTrail/src/CustomCustomTrailSettingsViewModel.cs',
    'CustomCustomTrail/src/CustomCustomTrailRuntime.cs',
    '_inspect/HostClientPresetTests/Program.cs')
foreach ($relativePath in $crlfTargets) {
    $text = [IO.File]::ReadAllText((Join-Path $workspace $relativePath))
    if ([Text.RegularExpressions.Regex]::IsMatch($text, '(?<!\r)\n')) {
        throw "$relativePath contains bare LF line endings."
    }
}

Write-Output "PASS: $($settings.Count) XAML files, shared-only registration, personal-setting declarations, automatic two-axis overflow scrolling, all interactive tooltips, shared styles, locale parity, nonempty translations, and CRLF."
