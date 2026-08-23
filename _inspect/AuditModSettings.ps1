$ErrorActionPreference = 'Stop'

$workspace = Split-Path -Parent $PSScriptRoot
$settings = [ordered]@{
    BugfixesAndQoL = 'BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml'
    BuildingCosts = 'BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Override/ScriptExtenderUI/BuildingCostsSettings.xaml'
    BuildingLimit = 'BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Override/ScriptExtenderUI/BuildingLimitSettings.xaml'
    ExtraFeatures = 'ExtraFeatures/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml'
    ImprovedHunters = 'ImprovedHunters/BepInEx/plugins/ImprovedHunters_Serp/Override/ScriptExtenderUI/ImprovedHuntersSettings.xaml'
    RandomEvents = 'RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml'
    SerpsModsHost = 'SerpsModsHost/Override/ScriptExtenderUI/SerpsModsStatus.xaml'
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
    SerpsModsHost = 'SerpsModsHost/Locales'
    CastlePlanner = 'CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Locales'
    CustomCustomTrail = 'CustomCustomTrail/Locales'
    StartConditions = 'StartConditions/Locales'
    UnitCosts = 'UnitCosts/Locales'
    UnitLimit = 'UnitLimit/Locales'
}

$viewModelSources = @{
    BugfixesAndQoL = 'BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs'
    BuildingCosts = 'BuildingCosts/src/BuildingCostsLobbyViewModel.cs'
    BuildingLimit = 'BuildingLimit/src/BuildingLimitLobbyViewModel.cs'
    CastlePlanner = 'CastlePlanner/src/CastlePlannerSettingsViewModel.cs'
    CustomCustomTrail = 'CustomCustomTrail/src/CustomCustomTrailSettingsViewModel.cs'
    ExtraFeatures = 'ExtraFeatures/src/ExtraFeaturesViewModel.cs'
    ImprovedHunters = 'ImprovedHunters/src/ImprovedHuntersViewModel.cs'
    RandomEvents = 'RandomEvents/src/RandomEventsSettingsViewModel.cs'
    SerpsModsHost = 'SerpsModsHost/src/SerpsModsDiagnosticsViewModel.cs'
    StartConditions = 'StartConditions/src/StartConditionsLobbyViewModel.cs'
    UnitCosts = 'UnitCosts/src/UnitCostsLobbyViewModel.cs'
    UnitLimit = 'UnitLimit/src/UnitLimitLobbyViewModel.cs'
}

# These bindings are deliberate editable proxies. Their setters update a classified
# parent setting or a classified serialized table through the row callback.
$editableProxyBindings = @{
    BuildingCosts = @('GoldSlider','GoldText','IronSlider','IronText','PitchSlider','PitchText','StoneSlider','StoneText','WoodSlider','WoodText')
    BuildingLimit = @('LimitText','SliderLimit')
    CastlePlanner = @()
    CustomCustomTrail = @('IsEnabled','SelectedCoopPackage')
    ExtraFeatures = @('AIGateClosingDistanceValueText','AIGateReopenDelayValueText','AILordHealthPercentText','ApothecaryPlagueSearchDistanceValueText','BuyMultiplier','BuyMultiplierValueText','CampfirePeasantsLimitText','GoldRefundPercentValueText','HumanGateClosingDistanceValueText','HumanGateReopenDelayValueText','HumanLordHealthPercentText','IronRefundPercentValueText','MarketBuyPriceMultiplierValueText','MarketSellPriceMultiplierValueText','MultiplyGoodsGainAIText','MultiplyGoodsGainHumanText','MultiplyGoodsGainInMoneyAIText','MultiplyGoodsGainInMoneyHumanText','PitchRefundPercentValueText','PlagueDurationMultiplierValueText','SellMultiplier','SellMultiplierValueText','StoneRefundPercentValueText','WoodRefundPercentValueText')
    ImprovedHunters = @('CamelMeatText','ChickenMeatText','DeerMeatText','GoatMeatText','MaxNeutralChickensPerPlayerValueText','RabbitMeatText')
    RandomEvents = @('AppleBlightChanceValueText','ArcherMaxValueText','ArcherMinValueText','ArchersChanceValueText','BanditMaxValueText','BanditMinValueText','BanditsChanceValueText','BardChanceValueText','CooldownMonthsValueText','FairChanceValueText','FireChanceValueText','FireMaxValueText','FireMinValueText','GranaryTheftChanceValueText','HopsBeetlesChanceValueText','IntervalMonthsValueText','LionAttackChanceValueText','LionMaxValueText','LionMinValueText','MadCowsChanceValueText','MarriageChanceValueText','PlagueChanceValueText','PlagueMaxValueText','PlagueMinValueText','RabbitsChanceValueText','TheftMaxValueText','TheftMinValueText','TreeBlightChanceValueText','WheatInfestationChanceValueText')
    StartConditions = @('AddStartGoldAISlider','AddStartGoldAIText','AddStartGoldHumanSlider','AddStartGoldHumanText','AIAmountSlider','AIAmountText','HumanAmountSlider','HumanAmountText','MultiplyStartTroopsAISlider','MultiplyStartTroopsAIText','MultiplyStartTroopsHumanSlider','MultiplyStartTroopsHumanText','SetStartGoldAISlider','SetStartGoldAIText','SetStartGoldHumanSlider','SetStartGoldHumanText')
    UnitCosts = @('AmountText','GoldSlider','GoldText','SliderAmount')
    UnitLimit = @('LimitText','SliderLimit')
}

$interactiveNames = @('Button', 'CheckBox', 'ComboBox', 'Slider', 'TextBox')
foreach ($entry in $settings.GetEnumerator()) {
    $path = Join-Path $workspace $entry.Value
    [xml]$xml = [IO.File]::ReadAllText($path)
    $manager = [Xml.XmlNamespaceManager]::new($xml.NameTable)
    $manager.AddNamespace('p', 'http://schemas.microsoft.com/winfx/2006/xaml/presentation')
    $editableBindings = @()

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
            foreach ($attributeName in @('IsChecked','SelectedValue','SelectedIndex','SelectedItem','Value','Text')) {
                $binding = $element.GetAttribute($attributeName)
                if ($binding -match '^\{Binding\s+([^,}\s]+)') {
                    $editableBindings += $matches[1]
                }
            }
        }
    }

    $viewModelSource = [IO.File]::ReadAllText((Join-Path $workspace $viewModelSources[$entry.Key]))
    $classifiedProperties = @([Text.RegularExpressions.Regex]::Matches(
        $viewModelSource,
        '(?s)\[(?:[^\]]*?)(?:SyncHostOnly|SyncPerPlayer|PresetLocal)(?:[^\]]*?)\]\s*public\s+[\w<>,\.\[\]\s]+?\s+(?<name>\w+)\s*(?:\{|=>)') |
        ForEach-Object { $_.Groups['name'].Value } |
        Sort-Object -Unique)
    $allowedProxies = @($editableProxyBindings[$entry.Key])
    $unclassifiedBindings = @($editableBindings |
        Sort-Object -Unique |
        Where-Object {
            $_ -ne 'SelectedPreset' -and
            $_ -notin @('HostSettingsEnabled', 'ClientSettingsEnabled') -and
            $_ -notin $classifiedProperties -and
            $_ -notin $allowedProxies
        })
    if ($unclassifiedBindings.Count -ne 0) {
        throw "$($entry.Key): editable bindings lack a sync/preset classification or reviewed proxy route: $($unclassifiedBindings -join ', ')"
    }

    $activationNodes = @($xml.SelectNodes(
        "//*[local-name()='CheckBox' and (contains(@IsChecked, 'HostSettingsEnabled') or contains(@IsChecked, 'ClientSettingsEnabled'))]"))
    if ($entry.Key -ne 'SerpsModsHost' -and $activationNodes.Count -ne 2) {
        throw "$($entry.Key): expected exactly two shared activation checkboxes."
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
    if ($entry.Key -ne 'SerpsModsHost') {
        $requiredMarkers += @(
            'x:Key="HostRoleHeader"',
            'x:Key="ClientRoleHeader"',
            'x:Key="SectionHeader"',
            'x:Key="HostActivationBorder"',
            'x:Key="ClientActivationBorder"',
            'Text="{Binding ModEnabledText}"',
            'IsChecked="{Binding HostSettingsEnabled, Mode=TwoWay}"',
            'IsChecked="{Binding ClientSettingsEnabled, Mode=TwoWay}"')
    }
    foreach ($required in $requiredMarkers) {
        if (-not $text.Contains($required)) {
            throw "$($entry.Key): required shared UI marker is missing: $required"
        }
    }

    if ($entry.Key -ne 'SerpsModsHost') {
        $headerIndex = $text.IndexOf('Text="{Binding ModEnabledText}"', [StringComparison]::Ordinal)
        $hostIndex = $text.IndexOf('IsChecked="{Binding HostSettingsEnabled, Mode=TwoWay}"', [StringComparison]::Ordinal)
        $clientIndex = $text.IndexOf('IsChecked="{Binding ClientSettingsEnabled, Mode=TwoWay}"', [StringComparison]::Ordinal)
        $presetIndex = $text.IndexOf('ItemsSource="{Binding PresetOptions}"', [StringComparison]::Ordinal)
        $resetIndex = $text.IndexOf('Command="{Binding ResetToDefaultCommand}"', [StringComparison]::Ordinal)
        if (-not ($headerIndex -lt $hostIndex -and $hostIndex -lt $clientIndex -and $clientIndex -lt $presetIndex -and $presetIndex -lt $resetIndex)) {
            throw "$($entry.Key): shared header controls are not in the required order."
        }
        if ($text.Contains('Text="{Binding PresetText}"') -or
            $text.Contains('IsChecked="{Binding EnableMod, Mode=TwoWay}"') -or
            $text.Contains('IsChecked="{Binding EnableClientFeatures, Mode=TwoWay}"')) {
            throw "$($entry.Key): obsolete preset label or section activation checkbox remains."
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
            'PlagueDurationMultiplierValueText', 'ApothecaryPlagueSearchDistanceValueText',
            'InaccessibleAIBuildingDemolitionProtectionValueText',
            'InaccessibleAIBuildingDemolitionProtectionHelpText')) {
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
    'expectedPlayers.Except(failedPlayers).SequenceEqual(executedPlayers)',
    'preview.TryGetCommittedSelections',
    'CaptureImportedCandidates(request.PlayerId - 1)',
    'selectBestFit(aivState, specIndex, 0)',
    'finally')) {
    if (-not $castleRuntimeSource.Contains($required)) {
        throw "CastlePlanner exact-once spawn verification marker is missing: $required"
    }
}
$castleSettingsSource = [IO.File]::ReadAllText((Join-Path $workspace 'CastlePlanner/src/CastlePlannerSettingsViewModel.cs'))
foreach ($required in @(
    'Task.Run(() =>',
    'AivFileCatalog.PrepareDiscovery(',
    'PumpCastleCatalogLoad()',
    '[Shared.PresetLocal]',
    'TryPrepareSelectedCastle(')) {
    if (-not $castleSettingsSource.Contains($required)) {
        throw "CastlePlanner fail-closed manifest marker is missing: $required"
    }
}
$castleHudXaml = [IO.File]::ReadAllText((Join-Path $workspace 'CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Patches/Assets/GUI/XAML/IngameUIScreens.xaml'))
if ([Text.RegularExpressions.Regex]::Matches(
        $castleHudXaml,
        'ItemsSource="\{Binding CastleOptions\}"').Count -ne 1) {
    throw 'CastlePlanner must expose exactly one AIVJSON dropdown in the Blueprint HUD.'
}
foreach ($forbidden in @('PreviewCastleOptions', 'PreviewSelectedCastle')) {
    if ($castleHudXaml.Contains($forbidden)) {
        throw "CastlePlanner retained the duplicate preview castle binding: $forbidden"
    }
}
foreach ($required in @(
    'Width="{Binding PanelWidth}"',
    'Height="{Binding PanelHeight}"',
    'VerticalScrollBarVisibility="Auto"',
    'HorizontalScrollBarVisibility="Auto"',
    'x:Name="CastlePlannerCastleComboBox"',
    'x:Name="CastlePlannerRotationComboBox"',
    'IsEnabled="{Binding CanSelectCastle}"',
    'IsEnabled="{Binding CanSelectRotation}"',
    'Command="{Binding ConfirmCastleCommand}"')) {
    if (-not $castleHudXaml.Contains($required)) {
        throw "CastlePlanner unified preview HUD marker is missing: $required"
    }
}
foreach ($forbidden in @('ToolTip=', 'ToolTipService.')) {
    if ($castleHudXaml.Contains($forbidden)) {
        throw "CastlePlanner Ingame Blueprint HUD retained a hover tooltip: $forbidden"
    }
}
$castleHudSource = [IO.File]::ReadAllText((Join-Path $workspace 'CastlePlanner/src/BlueprintHudViewModel.cs'))
foreach ($required in @(
    'ObservableCollection<string> CastleOptions => PreviewVisible',
    '? preview.CastleChoices',
    ': settings.CastleOptions;',
    'SettingsPanelVisible = true;',
    'current.PreviewMouseWheel += OnComboBoxPreviewMouseWheel;',
    'castleComboBox?.IsDropDownOpen == true',
    'rotationComboBox?.IsDropDownOpen == true',
    'ProcessOpenDropDownWheel()',
    'UnityEngine.Input.mouseScrollDelta.y',
    'scrollViewer.LineUp();',
    'scrollViewer.LineDown();',
    'get => PreviewVisible ? preview.SelectedChoice : settings.SelectedCastle;',
    'preview.SelectedChoice = value;',
    'ClampPanelExtent(',
    'OnPropertyChanged(nameof(PanelWidth));',
    'OnPropertyChanged(nameof(PanelHeight));',
    'availableWidth - PanelWidth - ScreenInset',
    'availableHeight - PanelHeight - ScreenInset')) {
    if (-not $castleHudSource.Contains($required)) {
        throw "CastlePlanner unified selector or panel-bound marker is missing: $required"
    }
}
$castleBlueprintRuntimeSource = [IO.File]::ReadAllText((Join-Path $workspace 'CastlePlanner/src/BlueprintRuntimeController.cs'))
foreach ($required in @(
    'InstallCameraWheelGuard();',
    'Hud?.EnsureInteractiveElementsAttached();',
    'Hud?.ProcessOpenDropDownWheel();',
    'Hud?.ShouldSuppressMapZoom() == true',
    'camera.AllowZoom = false;')) {
    if (-not $castleBlueprintRuntimeSource.Contains($required)) {
        throw "CastlePlanner dropdown-exclusive wheel marker is missing: $required"
    }
}
if (-not $castleBlueprintRuntimeSource.Contains('preview.IsPreviewActive && !preview.HasSelectedCastle')) {
    throw 'CastlePlanner does not hide the Blueprint for the No castle preview selection.'
}
$castlePreviewRuntimeSource = [IO.File]::ReadAllText((Join-Path $workspace 'CastlePlanner/src/FreeCastlePreviewRuntime.cs'))
foreach ($required in @(
    'ObservableCollection<string> CastleChoices => castleChoices;',
    'ObservableCollection<string> RotationChoices => rotations;',
    'nativeRotation={SelectedNativeRotation}',
    'ResetRotationToDefault();',
    'initFastMethod.Invoke(platform, null);',
    'platform.initFastFollowOn();',
    'Director.instance.StartMultiplayerGame();',
    'Free-castle multiplayer restart handshake activated:',
    'state == PreviewState.AwaitingGameplay',
    'viewModel.Show_BlackOut',
    'viewModel.Show_HUD_Briefing',
    'viewModel.Show_HUD_Main',
    'Vanilla start-situation screen closed; castle selection opened.')) {
    if (-not $castlePreviewRuntimeSource.Contains($required)) {
        throw "CastlePlanner preview selector or start-screen lifecycle marker is missing: $required"
    }
}
$restartOrder = @(
    'Director.instance.stopSimThread();',
    'initFastMethod.Invoke(platform, null);',
    'platform.initFastFollowOn();',
    'startGameTrampoline(',
    'Director.instance.StartMultiplayerGame();')
$previousRestartMarker = -1
foreach ($marker in $restartOrder) {
    $markerIndex = $castlePreviewRuntimeSource.IndexOf(
        $marker,
        $previousRestartMarker + 1,
        [StringComparison]::Ordinal)
    if ($markerIndex -le $previousRestartMarker) {
        throw "CastlePlanner multiplayer restart sequence is missing or out of order: $marker"
    }
    $previousRestartMarker = $markerIndex
}
foreach ($forbidden in @(
    'SpawnSelectedCastleData',
    'SpawnInventoryManifestData',
    'TryCreateSpawnPlan(',
    'The local AIVJSON inventory changed after its last lobby announcement.',
    'bool multiplayer = lobbyPlayers.HumanMemberCount > 1;')) {
    if ($castleSettingsSource.Contains($forbidden)) {
        throw "CastlePlanner retained obsolete whole-inventory or player-count logic: $forbidden"
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
$productiveRoots = @($settings.Keys)
$productiveCsFiles = @($productiveRoots | ForEach-Object {
    Get-ChildItem -LiteralPath (Join-Path $workspace "$_/src") -File -Filter '*.cs'
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
foreach ($root in $productiveRoots) {
    $sourceDirectory = [IO.Path]::GetFullPath((Join-Path $workspace "$root/src"))
    $rootRegistrations = @($registrationFiles | Where-Object {
        $_.Directory.FullName.Equals($sourceDirectory, [StringComparison]::OrdinalIgnoreCase)
    })
    if ($rootRegistrations.Count -ne 1) {
        throw "${root}: expected exactly one Shared lobby-settings registration, found $($rootRegistrations.Count)."
    }
}
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

$settingsViewModels = @($productiveCsFiles | Where-Object {
    [IO.File]::ReadAllText($_.FullName).Contains('PresetLobbyModSettingsViewModel')
})
foreach ($viewModelFile in $settingsViewModels) {
    $relativePath = [IO.Path]::GetRelativePath($workspace, $viewModelFile.FullName)
    $source = [IO.File]::ReadAllText($viewModelFile.FullName)
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
    if ($source.Contains('MainViewModel.Instance') -and
        -not $source.Contains('MainViewModel.viewModelLoaded')) {
        throw "${relativePath}: MainViewModel.Instance is used without an early-lifecycle viewModelLoaded guard."
    }
}

$sharedSettingsSource = [IO.File]::ReadAllText((Join-Path $workspace 'Shared/PresetLobbyModSettingsViewModel.cs'))
foreach ($required in @(
    'ActivatePerPlayerLobbySettings',
    'PerPlayerLobbySettingsBuilder',
    'ResetSlotsWith',
    'RequireReport',
    'must return one stable array instance',
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
    'platform.gameMembers.Any(member =>',
    'never need to resolve or guess their own player ID',
    'Observe(null, null, false, 0, mapTransition)',
    'The lobby roster could not be observed; waiting for a successful retry.',
    'viewModel.DeactivatePerPlayerLobbySettings();',
    'SendReliableLobbyPacket',
    'SendMessageToUser returned',
    'fromThread && data != null',
    'Lobby mod settings registration aborted')) {
    if (-not $sharedSettingsSource.Contains($required)) {
        throw "Shared per-player lifecycle/roster marker is missing: $required"
    }
}
if ($sharedSettingsSource.Contains('sendPacketToSteamIdMethod')) {
    throw 'Shared reliable lobby delivery must not route back through gameMembers-aware SendPacketToSteamId.'
}
$sharedGameModeSource = [IO.File]::ReadAllText((Join-Path $workspace 'Shared/GameModeHelper.cs'))
foreach ($required in @(
    'public static bool IsMapEditor()',
    'if (!MainViewModel.viewModelLoaded)',
    'if (member.SkirmishMember)',
    'member != null && !member.skirmishAI',
    'bool mapEditor = IsMapEditor();',
    'skirmishLobbyMembers == lobbyMembers',
    '(skirmishGameType >= 0 || localSkirmishTransition)')) {
    if (-not $sharedGameModeSource.Contains($required)) {
        throw "Shared lifecycle-safe game-mode marker is missing: $required"
    }
}
$unsafeEditorChecks = Get-ChildItem -LiteralPath $workspace -Directory |
    Where-Object { $settings.Contains($_.Name) } |
    ForEach-Object { Get-ChildItem -LiteralPath (Join-Path $_.FullName 'src') -File -Filter '*.cs' -ErrorAction SilentlyContinue }
foreach ($sourceFile in $unsafeEditorChecks) {
    $sourceText = [IO.File]::ReadAllText($sourceFile.FullName)
    if ($sourceText.Contains('.IsMapEditorMode')) {
        throw "$($sourceFile.FullName): use Shared.GameModeHelper.IsMapEditor() so early startup cannot construct MainViewModel."
    }
}
$castleSettingsSource = [IO.File]::ReadAllText((Join-Path $workspace 'CastlePlanner/src/CastlePlannerSettingsViewModel.cs'))
if ($castleSettingsSource.Contains('SpawnSelectedCastleData[GetLocalPlayerId()]') -or
    $castleSettingsSource.Contains('SpawnInventoryManifestData[GetLocalPlayerId()]')) {
    throw 'CastlePlanner writes personal companion data through an unresolved slot-1 fallback.'
}
if ($castleSettingsSource.Contains('HasActiveMultiplayerGameMembers')) {
    throw 'CastlePlanner must use Shared game-mode and roster helpers instead of a private gameMembers-count heuristic.'
}
if ($castleSettingsSource.Contains('SpawnSelectedCastleData[localPlayerId]') -or
    $castleSettingsSource.Contains('SpawnInventoryManifestData[localPlayerId] =')) {
    throw 'CastlePlanner must let Shared mirror local personal values into companion slots.'
}
$customTrailSettingsSource = [IO.File]::ReadAllText((Join-Path $workspace 'CustomCustomTrail/src/CustomCustomTrailSettingsViewModel.cs'))
if ($customTrailSettingsSource.Contains('GameNetworkAPI.GetLocalPlayerId()')) {
    throw 'CustomCustomTrail must let Shared mirror its local status into the companion slot.'
}
$customTrailCoordinatorSource = [IO.File]::ReadAllText((Join-Path $workspace 'CustomCustomTrail/src/TrailMissionSettingsCoordinator.cs'))
foreach ($required in @(
    'page.Loaded += loaded;',
    'page.Loaded -= loaded;',
    'Could not find the logical title element after Loaded')) {
    if (-not $customTrailCoordinatorSource.Contains($required)) {
        throw "CustomCustomTrail lifecycle-safe Coop presentation marker is missing: $required"
    }
}

$crlfTargets = @($settings.Values) + @(
    $localeDirectories.Values | ForEach-Object {
        Get-ChildItem -LiteralPath (Join-Path $workspace $_) -File -Filter '*.txt' |
            ForEach-Object { [IO.Path]::GetRelativePath($workspace, $_.FullName) }
    }
) + @(
    'Shared/PresetLobbyModSettingsViewModel.cs',
    'Shared/GameModeHelper.cs',
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
