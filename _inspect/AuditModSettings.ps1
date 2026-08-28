[CmdletBinding()]
param(
    [ValidateSet(
        'BugfixesAndQoL',
        'BuildingCosts',
        'BuildingLimit',
        'CheatMod',
        'ExtraFeatures',
        'ImprovedHunters',
        'RandomEvents',
        'SerpsModsHost',
        'CastlePlanner',
        'CustomCustomTrail',
        'StartConditions',
        'UnitCosts',
        'UnitLimit')]
    [string] $Mod
)

$ErrorActionPreference = 'Stop'

$workspace = Split-Path -Parent $PSScriptRoot
$settingsByMod = [ordered]@{
    BugfixesAndQoL = 'BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml'
    BuildingCosts = 'BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Override/ScriptExtenderUI/BuildingCostsSettings.xaml'
    BuildingLimit = 'BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Override/ScriptExtenderUI/BuildingLimitSettings.xaml'
    CheatMod = 'CheatMod/Override/ScriptExtenderUI/CheatModSettings.xaml'
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
    CheatMod = 'CheatMod/Locales'
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
    CheatMod = 'CheatMod/src/CheatModSettingsViewModel.cs'
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
    CheatMod = @()
    CastlePlanner = @()
    CustomCustomTrail = @('IsEnabled','SelectedCoopPackage')
    ExtraFeatures = @('AIEnemyProximityMultiplayerValueText','AIEnemyProximitySingleplayerValueText','AITowerGateRebuildDelayValueText','AIGateClosingDistanceValueText','AIGateReopenDelayValueText','AILordHealthPercentText','ApothecaryPlagueSearchDistanceValueText','BuyMultiplier','BuyMultiplierValueText','CampfirePeasantsLimitText','GoldRefundPercentValueText','HumanEnemyProximityMultiplayerValueText','HumanEnemyProximitySingleplayerValueText','HumanGateClosingDistanceValueText','HumanGateReopenDelayValueText','HumanLordHealthPercentText','IronRefundPercentValueText','MarketBuyPriceMultiplierValueText','MarketSellPriceMultiplierValueText','MultiplyGoodsGainAIText','MultiplyGoodsGainHumanText','MultiplyGoodsGainInMoneyAIText','MultiplyGoodsGainInMoneyHumanText','PitchRefundPercentValueText','PlagueDurationMultiplierValueText','SellMultiplier','SellMultiplierValueText','StoneRefundPercentValueText','WoodRefundPercentValueText')
    ImprovedHunters = @('CamelMeatText','ChickenMeatText','DeerMeatText','GoatMeatText','MaxNeutralChickensPerPlayerValueText','RabbitMeatText')
    RandomEvents = @('AppleBlightChanceValueText','ArcherMaxValueText','ArcherMinValueText','ArchersChanceValueText','BanditMaxValueText','BanditMinValueText','BanditsChanceValueText','BardChanceValueText','CooldownMonthsValueText','FairChanceValueText','FireChanceValueText','FireMaxValueText','FireMinValueText','GranaryTheftChanceValueText','HopsBeetlesChanceValueText','IntervalMonthsValueText','LionAttackChanceValueText','LionMaxValueText','LionMinValueText','MadCowsChanceValueText','MarriageChanceValueText','PlagueChanceValueText','PlagueMaxValueText','PlagueMinValueText','RabbitsChanceValueText','TheftMaxValueText','TheftMinValueText','TreeBlightChanceValueText','WheatInfestationChanceValueText')
    StartConditions = @('AddStartGoldAISlider','AddStartGoldAIText','AddStartGoldHumanSlider','AddStartGoldHumanText','AIAmountSlider','AIAmountText','HumanAmountSlider','HumanAmountText','MultiplyStartTroopsAISlider','MultiplyStartTroopsAIText','MultiplyStartTroopsHumanSlider','MultiplyStartTroopsHumanText','SetStartGoldAISlider','SetStartGoldAIText','SetStartGoldHumanSlider','SetStartGoldHumanText')
    UnitCosts = @('AmountText','GoldSlider','GoldText','SliderAmount')
    UnitLimit = @('LimitText','SliderLimit')
}

$selectedModNames = if ($PSBoundParameters.ContainsKey('Mod')) {
    @($Mod)
} else {
    @($settingsByMod.Keys)
}
$settings = [ordered]@{}
foreach ($modName in $selectedModNames) {
    $settings[$modName] = $settingsByMod[$modName]
}

function Test-ModSelected([string] $Name) {
    return $selectedModNames -contains $Name
}

function Test-HasModSettingsSearchTitle([Xml.XmlElement] $Element) {
    $current = $Element
    for ($depth = 0; $depth -le 4 -and $null -ne $current; $depth++) {
        $titleAttribute = @($current.Attributes | Where-Object { $_.LocalName -eq 'ModSettingsSearch.Title' })
        if ($titleAttribute.Count -ne 0 -and -not [string]::IsNullOrWhiteSpace($titleAttribute[0].Value)) {
            return $true
        }

        if ($depth -eq 0) {
            $content = $current.GetAttribute('Content')
            if (-not [string]::IsNullOrWhiteSpace($content)) {
                return $true
            }
            $nestedText = $current.SelectSingleNode(".//*[local-name()='TextBlock' and string-length(normalize-space(@Text)) > 0]")
            if ($null -ne $nestedText) {
                return $true
            }
        }

        $parent = $current.ParentNode
        if ($parent -is [Xml.XmlElement]) {
            $row = $Element.GetAttribute('Grid.Row')
            $labels = @($parent.SelectNodes("./*[local-name()='TextBlock' and string-length(normalize-space(@Text)) > 0]"))
            $labels += @($parent.SelectNodes("./*//*[local-name()='TextBlock' and string-length(normalize-space(@Text)) > 0]"))
            foreach ($label in $labels) {
                if ($parent.LocalName -ne 'Grid' -or
                    [string]::IsNullOrWhiteSpace($row) -or
                    $label.GetAttribute('Grid.Row') -eq $row) {
                    return $true
                }
            }
        }
        $current = $parent
    }
    return $false
}

function Test-IsInsideExcludedModSettingsSearchArea([Xml.XmlElement] $Element) {
    $current = $Element
    while ($current -is [Xml.XmlElement]) {
        $excludeAttribute = @($current.Attributes | Where-Object { $_.LocalName -eq 'ModSettingsSearch.Exclude' })
        if ($excludeAttribute.Count -ne 0 -and $excludeAttribute[0].Value -eq 'True') {
            return $true
        }
        $current = $current.ParentNode
    }
    return $false
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
            $isSearchUi = Test-IsInsideExcludedModSettingsSearchArea $element
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
            if (-not $isSearchUi -and -not (Test-HasModSettingsSearchTitle $element)) {
                $searchIdentity = @('IsChecked','SelectedValue','SelectedIndex','SelectedItem','Value','Text') |
                    ForEach-Object { $element.GetAttribute($_) } |
                    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
                    Select-Object -First 1
                throw "$($entry.Key): $elementName [$searchIdentity] cannot be assigned an automatic mod-settings search title; add an unambiguous same-row/container title or explicit shared:ModSettingsSearch metadata."
            }
            if ($isSearchUi) {
                continue
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
    $hasHostSettings = [Text.RegularExpressions.Regex]::IsMatch(
        $viewModelSource,
        '(?s)\[[^\]]*SyncHostOnly[^\]]*\]\s*public')
    $hasClientSettings = [Text.RegularExpressions.Regex]::IsMatch(
        $viewModelSource,
        '(?s)\[[^\]]*(?:SyncPerPlayer|PresetLocal)[^\]]*\]\s*public')
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
    $expectedActivationBindings = @()
    if ($hasHostSettings -and $classifiedProperties -contains 'EnableMod') {
        $expectedActivationBindings += 'HostSettingsEnabled'
    }
    if ($hasClientSettings -and
        ($classifiedProperties -contains 'EnableClientFeatures' -or $classifiedProperties -contains 'EnableMod')) {
        $expectedActivationBindings += 'ClientSettingsEnabled'
    }
    $actualActivationBindings = @()
    if ($activationNodes.IsChecked -match 'HostSettingsEnabled') {
        $actualActivationBindings += 'HostSettingsEnabled'
    }
    if ($activationNodes.IsChecked -match 'ClientSettingsEnabled') {
        $actualActivationBindings += 'ClientSettingsEnabled'
    }
    $missingActivationBindings = @($expectedActivationBindings | Where-Object { $_ -notin $actualActivationBindings })
    if ($entry.Key -ne 'SerpsModsHost' -and
        ($missingActivationBindings.Count -ne 0 -or $activationNodes.Count -gt 2)) {
        throw "$($entry.Key): invalid shared activation checkboxes; missing=$($missingActivationBindings -join ', '), found=$($activationNodes.Count)."
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
        'FontSize="{x:Static shared:ToolTipPresentation.FontSize}"',
        'TextWrapping="Wrap"')
    if ($entry.Key -ne 'SerpsModsHost') {
        $requiredMarkers += @('x:Key="SectionHeader"', 'Text="{Binding ModEnabledText}"')
        if ($hasHostSettings) {
            $requiredMarkers += 'x:Key="HostRoleHeader"'
        }
        if ($hasClientSettings) {
            $requiredMarkers += 'x:Key="ClientRoleHeader"'
        }
        if ($actualActivationBindings -contains 'HostSettingsEnabled') {
            $requiredMarkers += @(
                'x:Key="HostActivationBorder"',
                'IsChecked="{Binding HostSettingsEnabled, Mode=TwoWay}"')
        }
        if ($actualActivationBindings -contains 'ClientSettingsEnabled') {
            $requiredMarkers += @(
                'x:Key="ClientActivationBorder"',
                'IsChecked="{Binding ClientSettingsEnabled, Mode=TwoWay}"')
        }
    }
    foreach ($required in $requiredMarkers) {
        if (-not $text.Contains($required)) {
            throw "$($entry.Key): required shared UI marker is missing: $required"
        }
    }

    if ($entry.Key -ne 'SerpsModsHost') {
        $headerIndex = $text.IndexOf('Text="{Binding ModEnabledText}"', [StringComparison]::Ordinal)
        $presetIndex = $text.IndexOf('ItemsSource="{Binding PresetOptions}"', [StringComparison]::Ordinal)
        $resetIndex = $text.IndexOf('Command="{Binding ResetToDefaultCommand}"', [StringComparison]::Ordinal)
        $orderedHeaderIndices = @($headerIndex)
        if ($actualActivationBindings -contains 'HostSettingsEnabled') {
            $orderedHeaderIndices += $text.IndexOf('IsChecked="{Binding HostSettingsEnabled, Mode=TwoWay}"', [StringComparison]::Ordinal)
        }
        if ($actualActivationBindings -contains 'ClientSettingsEnabled') {
            $orderedHeaderIndices += $text.IndexOf('IsChecked="{Binding ClientSettingsEnabled, Mode=TwoWay}"', [StringComparison]::Ordinal)
        }
        $orderedHeaderIndices += @($presetIndex, $resetIndex)
        $headerOrderValid = $orderedHeaderIndices -notcontains -1
        for ($index = 1; $headerOrderValid -and $index -lt $orderedHeaderIndices.Count; $index++) {
            $headerOrderValid = $orderedHeaderIndices[$index - 1] -lt $orderedHeaderIndices[$index]
        }
        if (-not $headerOrderValid) {
            throw "$($entry.Key): shared header controls are not in the required order."
        }
        if ($text.Contains('IsChecked="{Binding EnableMod, Mode=TwoWay}"') -or
            $text.Contains('IsChecked="{Binding EnableClientFeatures, Mode=TwoWay}"')) {
            throw "$($entry.Key): obsolete section activation checkbox remains."
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
            'InaccessibleAIBuildingDemolitionProtectionHelpText',
            'HumanEnemyProximitySingleplayerValueText',
            'HumanEnemyProximityMultiplayerValueText',
            'AIEnemyProximitySingleplayerValueText',
            'AIEnemyProximityMultiplayerValueText',
            'AITowerGateRebuildDelayValueText')) {
            if (-not $text.Contains($requiredValueBinding)) {
                throw "ExtraFeatures: Slider unit binding is missing: $requiredValueBinding"
            }
        }

        $viewModelText = [IO.File]::ReadAllText((Join-Path $workspace $viewModelSources.ExtraFeatures))
        $enemyProximityFields = [ordered]@{
            HumanEnemyProximitySingleplayer = 'humanEnemyProximitySingleplayer'
            HumanEnemyProximityMultiplayer = 'humanEnemyProximityMultiplayer'
            AIEnemyProximitySingleplayer = 'aiEnemyProximitySingleplayer'
            AIEnemyProximityMultiplayer = 'aiEnemyProximityMultiplayer'
        }
        foreach ($propertyName in $enemyProximityFields.Keys) {
            if ($viewModelText -notmatch "\[SyncHostOnly\]\s+public int $propertyName\b") {
                throw "ExtraFeatures: enemy-proximity property is not SyncHostOnly: $propertyName"
            }
            $fieldName = [Regex]::Escape($enemyProximityFields[$propertyName])
            if ($viewModelText -notmatch "private int $fieldName = EnemyProximityPolicy\.VanillaMode;") {
                throw "ExtraFeatures: enemy-proximity default is not Vanilla (-1): $propertyName"
            }
            if ($viewModelText -notmatch "$propertyName\s*=\s*EnemyProximityPolicy\.VanillaMode;") {
                throw "ExtraFeatures: enemy-proximity reset is not Vanilla (-1): $propertyName"
            }
            if ($viewModelText -notmatch "SetIntSetting\([^;]*EnemyProximityPolicy\.MinimumRadius, EnemyProximityPolicy\.MaximumRadius, nameof\($propertyName\)") {
                throw "ExtraFeatures: enemy-proximity range is not the shared -1..100 range: $propertyName"
            }
        }
        if ($viewModelText -match '\bAIRepairEnemyProximity\b') {
            throw 'ExtraFeatures: obsolete AIRepairEnemyProximity property remains in the ViewModel.'
        }
    }
}

$toolTipPresentationPath = Join-Path $workspace 'Shared/ToolTipPresentation.cs'
$toolTipPresentation = [IO.File]::ReadAllText($toolTipPresentationPath)
foreach ($required in @(
    'public static class ToolTipPresentation',
    'SE_ToolTip',
    'public static float FontSize => 50.0f;',
    'public static float MaximumWidth => 1000.0f;')) {
    if (-not $toolTipPresentation.Contains($required)) {
        throw "Shared fixed tooltip presentation marker is missing: $required"
    }
}

# Noesis can reset the outer ToolTip.FontSize when moving between popup owners.
# The rendered content must therefore consume the shared fixed value directly.
$sharedToolTipXamlPaths = @($settings.Values)
if (Test-ModSelected 'BugfixesAndQoL') {
    $sharedToolTipXamlPaths += @(
        'BugfixesAndQoL/Patches/Assets/GUI/XAMLResources/FRONT_Multiplayer.xaml',
        'BugfixesAndQoL/Patches/Assets/GUI/XAMLResources/FRONT_Multiplayer_AISettings.xaml')
}
if (Test-ModSelected 'ExtraFeatures') {
    $sharedToolTipXamlPaths += 'ExtraFeatures/Patches/Assets/GUI/XAMLResources/HUD_Buildings.xaml'
}
foreach ($relativePath in @($sharedToolTipXamlPaths | Sort-Object -Unique)) {
    $xamlText = [IO.File]::ReadAllText((Join-Path $workspace $relativePath))
    $contentTextBlocks = [Text.RegularExpressions.Regex]::Matches(
        $xamlText,
        '<TextBlock\b(?=[^>]*\bText="\{TemplateBinding Content\}")[^>]*>',
        [Text.RegularExpressions.RegexOptions]::Singleline)
    if ($contentTextBlocks.Count -eq 0) {
        throw "Shared tooltip template content TextBlock is missing: $relativePath"
    }
    foreach ($contentTextBlock in $contentTextBlocks) {
        if (-not $contentTextBlock.Value.Contains(
            'FontSize="{x:Static shared:ToolTipPresentation.FontSize}"')) {
            throw "Shared tooltip content must bind FontSize directly instead of through the Noesis ToolTip host: $relativePath"
        }
    }
}
if (Test-ModSelected 'CastlePlanner') {
$castleRuntimeSource = [IO.File]::ReadAllText((Join-Path $workspace 'CastlePlanner/src/CastlePlannerRuntime.cs'))
foreach ($required in @(
    'expectedAivCastlePlayers',
    'failedAivCastlePlayers',
    'preview.TryGetCommittedSelections',
    'CaptureImportedCandidates(request.PlayerId - 1)',
    'selectBestFit(aivState, specIndex, 0)',
    'finally')) {
    if (-not $castleRuntimeSource.Contains($required)) {
        throw "CastlePlanner exact-once spawn verification marker is missing: $required"
    }
}
$immediateSpawnInvariant =
    $castleRuntimeSource.Contains(
        'expectedPlayers.Except(failedPlayers).SequenceEqual(executedPlayers)')
$deferredSpawnInvariant =
    $castleRuntimeSource.Contains('int[] acceptedPlayers = deferredPlayers') -and
    $castleRuntimeSource.Contains('.Concat(executedPlayers)') -and
    $castleRuntimeSource.Contains(
        'expectedPlayers.Except(failedPlayers).SequenceEqual(acceptedPlayers)')
if (-not $immediateSpawnInvariant -and -not $deferredSpawnInvariant) {
    throw 'CastlePlanner must verify every non-failed expected castle as either executed immediately or accepted for deferred execution.'
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
    'x:Name="CastlePlannerCastleSelector"',
    'x:Name="CastlePlannerCastleOpenSurface"',
    'x:Name="CastlePlannerCastleSearchPopup"',
    'x:Name="CastlePlannerCastleSearchTextBox"',
    'x:Name="CastlePlannerCastleSearchResults"',
    'x:Name="CastlePlannerRotationComboBox"',
    'IsEnabled="{Binding CanSelectCastle}"',
    'IsEnabled="{Binding CanSelectRotation}"',
    'Placement="Bottom"',
    'StaysOpen="False"',
    'ScrollViewer.HorizontalScrollBarVisibility="Auto"',
    'ScrollViewer.VerticalScrollBarVisibility="Auto"',
    'Placeholder="{Binding SearchText}"',
    'Command="{Binding ConfirmCastleCommand}"')) {
    if (-not $castleHudXaml.Contains($required)) {
        throw "CastlePlanner unified preview HUD marker is missing: $required"
    }
}
foreach ($required in @(
    'ToolTip="{Binding ShowFortificationsHelpText}"',
    'ToolTip="{Binding ShowBuildingsHelpText}"',
    'ToolTip="{Binding ShowDefensiveGroundFeaturesHelpText}"',
    'ToolTip="{Binding ShowFearFactorBuildingsHelpText}"')) {
    if (-not $castleHudXaml.Contains($required)) {
        throw "CastlePlanner Blueprint filter tooltip is missing: $required"
    }
}
if ([Text.RegularExpressions.Regex]::Matches(
        $castleHudXaml,
        'ToolTip="').Count -ne 4 -or
    [Text.RegularExpressions.Regex]::Matches(
        $castleHudXaml,
        'ToolTipService\.ShowDuration="60000"').Count -ne 4) {
    throw 'CastlePlanner must expose exactly four 60-second Blueprint HUD tooltips.'
}
if ($castleHudXaml.Contains('ToolTip="{Binding SearchHelpText}"')) {
    throw 'CastlePlanner search selector must not expose redundant tooltips.'
}
if ($castleHudXaml.Contains('VerticalOffset="-30"')) {
    throw 'CastlePlanner search popup must not overlap its movable selector.'
}
$castleHudSource = [IO.File]::ReadAllText((Join-Path $workspace 'CastlePlanner/src/BlueprintHudViewModel.cs'))
foreach ($required in @(
    'ObservableCollection<string> CastleOptions =>',
    'filteredCastleOptions;',
    'ObservableCollection<string> source = PreviewVisible',
    'BlueprintSearchPolicy.Matches(displayName, castleSearchText)',
    'castleSearchPopup.Opened += OnCastleSearchPopupOpened;',
    'castleSearchPopup.Closed += OnCastleSearchPopupClosed;',
    'castleOpenSurface.MouseLeftButtonDown +=',
    'castleSearchPopup.StaysOpen = true;',
    'CompleteCastleSearchOpeningClick()',
    'castleSearchTextBox.IsKeyboardFocusedChanged +=',
    'castleSearchResults.SelectionChanged +=',
    'MainViewModel.Instance.SetNoesisKeyboardState(focused);',
    'castleSearchTextBox.SelectAll();',
    'SettingsPanelVisible = true;',
    'current.PreviewMouseWheel += OnComboBoxPreviewMouseWheel;',
    'castleSearchPopup?.IsOpen == true',
    'rotationComboBox?.IsDropDownOpen == true',
    'ProcessOpenDropDownWheel()',
    'UnityEngine.Input.mouseScrollDelta.y',
    'scrollViewer.LineUp();',
    'scrollViewer.LineDown();',
    'string option = PreviewVisible',
    '? preview.SelectedChoice',
    ': settings.SelectedCastle;',
    'settings.TryResolveCastleDisplayName(',
    'preview.SelectedChoice = previewOption;',
    'settings.SelectedCastle = selectedOption;',
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
    'Application.focusChanged += OnApplicationFocusChanged;',
    'Hud?.CloseSearchPopupForApplicationFocusLoss() == true',
    'Hud?.EnsureInteractiveElementsAttached();',
    'Hud?.CompleteCastleSearchOpeningClick();',
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

$currentTooltipXamlByMod = @{
    BugfixesAndQoL = @(
        'BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml',
        'BugfixesAndQoL/BepInEx/plugins/BugfixesAndQoL_Serp/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml')
    BuildingCosts = @('BuildingCosts/BepInEx/plugins/BuildingCosts_Serp/Override/ScriptExtenderUI/BuildingCostsSettings.xaml')
    BuildingLimit = @('BuildingLimit/BepInEx/plugins/BuildingLimit_Serp/Override/ScriptExtenderUI/BuildingLimitSettings.xaml')
    CheatMod = @('CheatMod/Override/ScriptExtenderUI/CheatModSettings.xaml')
    CastlePlanner = @('CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Override/ScriptExtenderUI/CastlePlannerSettings.xaml')
    CustomCustomTrail = @(
        'CustomCustomTrail/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml',
        'CustomCustomTrail/BepInEx/plugins/CustomCustomTrail_Serp/Override/ScriptExtenderUI/CustomCustomTrailSettings.xaml')
    ExtraFeatures = @(
        'ExtraFeatures/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml',
        'ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml')
    ImprovedHunters = @('ImprovedHunters/BepInEx/plugins/ImprovedHunters_Serp/Override/ScriptExtenderUI/ImprovedHuntersSettings.xaml')
    RandomEvents = @(
        'RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml',
        'RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml')
    SerpsModsHost = @(
        'SerpsModsHost/Override/ScriptExtenderUI/SerpsModsStatus.xaml',
        'SerpsModsHost/BepInEx/plugins/SerpsMods_Serp/Override/ScriptExtenderUI/SerpsModsStatus.xaml')
    StartConditions = @('StartConditions/BepInEx/plugins/StartConditions_Serp/Override/ScriptExtenderUI/StartConditionsSettings.xaml')
    UnitCosts = @('UnitCosts/BepInEx/plugins/UnitCosts_Serp/Override/ScriptExtenderUI/UnitCostsSettings.xaml')
    UnitLimit = @('UnitLimit/BepInEx/plugins/UnitLimit_Serp/Override/ScriptExtenderUI/UnitLimitSettings.xaml')
}
$currentTooltipXaml = @($selectedModNames | ForEach-Object { $currentTooltipXamlByMod[$_] })
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

foreach ($modName in $selectedModNames) {
    $entry = [Collections.DictionaryEntry]::new($modName, $localeDirectories[$modName])
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
    if (-not $projectText.Contains('Shared\ModSettingsSearch.cs')) {
        throw "$($project.Name): lobby settings do not compile the Shared mod-settings search anchor implementation."
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
$sharedSearchSource = [IO.File]::ReadAllText((Join-Path $workspace 'Shared/ModSettingsSearch.cs'))
foreach ($required in @(
    'DependencyProperty.RegisterAttached(',
    'System_GetModSettingsSearchEntries',
    'GetExclude',
    'BuildAutomaticKey',
    'ToolTipService.GetToolTip',
    'EnumerateChildren',
    'current is Panel panel',
    'current is Decorator decorator',
    'current is ContentControl contentControl',
    'VisualTreeHelper.GetChildrenCount',
    'ModSettingsSearch.RegisterSource',
    'XDocument.Load',
    'ResolveDataContexts',
    'ResolveElementContent',
    'return source.GetEntries(viewModel);')) {
    $searchContractText = $sharedSearchSource + $sharedSettingsSource
    if (-not $searchContractText.Contains($required)) {
        throw "Shared mod-settings search contract marker is missing: $required"
    }
}

$hostSearchSource = [IO.File]::ReadAllText((Join-Path $workspace 'SerpsModsHost/src/ModSettingsSearchViewModel.cs'))
$hostEditorSource = [IO.File]::ReadAllText((Join-Path $workspace 'SerpsModsHost/src/ModSettingsSearchEditorFactory.cs'))
$hostSettingsXaml = [IO.File]::ReadAllText((Join-Path $workspace 'SerpsModsHost/Override/ScriptExtenderUI/SerpsModsStatus.xaml'))
$hostPluginSource = [IO.File]::ReadAllText((Join-Path $workspace 'SerpsModsHost/src/SerpsModsHostPlugin.cs'))
$hostBuildSource = [IO.File]::ReadAllText((Join-Path $workspace 'SerpsModsHost/build.bat'))
foreach ($required in @(
    'System_GetModSettingsSearchEntries',
    'ReadAutomaticEntries',
    'BringIntoView()',
    'EnqueueDeferred',
    'using automatic text search',
    'Hub opened; indexing is deferred until the first query.',
    'Building search index without changing the selected tab.',
    'Catalog results intentionally remain navigation-only.',
    'editor = null;',
    'IncludeToolTips',
    'ModSettingsSearch.Exclude="True"',
    'HorizontalScrollBarVisibility="Disabled"',
    'ToolTip="{Binding DisplayToolTip}"',
    'Content="{Binding Editor}"',
    'CloneBinding',
    'BindingOperations.SetBinding',
    'new Binding(nameof(UIElement.IsEnabled), source)',
    'KeyboardCaptureBinding.SetEnabled',
    'DirectUnavailableVisibility',
    'diagnostics.SetSearch')) {
    if (-not ($hostSearchSource + $hostEditorSource + $hostSettingsXaml + $hostPluginSource).Contains($required)) {
        throw "SerpsModsHost search implementation marker is missing: $required"
    }
}
if ($hostSearchSource.Contains('InvalidateAfterSelectedTabChange') -or
    $hostSearchSource.Contains('ResolveCurrentTarget') -or
    -not $hostSettingsXaml.Contains('x:Name="SerpsModSettingsSearchTextBox"') -or
    -not $hostPluginSource.Contains('searchTextBox.PreviewKeyDown += OnSearchTextBoxPreviewKeyDown') -or
    -not $hostPluginSource.Contains('args.Key == NoesisKey.Return') -or
    -not $hostPluginSource.Contains('args.Handled = true')) {
    throw 'Search catalogs must not be reindexed across tabs, and Enter must be consumed by the search field.'
}
if ($hostEditorSource.Contains('PropertyInfo.SetValue') -or
    $hostSearchSource.Contains('ModSettingsSearchEditorFactory.Create(') -or
    $hostSearchSource.Contains('Plugin.ModSettingsHubViewModel.SelectedTab = tab') -or
    $hostSearchSource.Contains('view.Measure(') -or
    $hostSearchSource.Contains('view.Arrange(') -or
    $hostPluginSource.Contains('SerpsModSettingsSearchPanel') -or
    $hostPluginSource.Contains('SerpsModSettingsSearchResults') -or
    $hostBuildSource.Contains('xcopy "%PROJECT_DIR%Patches"') -or
    (Test-Path -LiteralPath (Join-Path $workspace 'SerpsModsHost/Patches/Assets/GUI/XAMLResources/FRONT_Multiplayer.xaml'))) {
    throw 'SerpsModsHost search must live only in its own modsettings and must not traverse or clone realized setting controls.'
}
$hubOpenHandler = [Text.RegularExpressions.Regex]::Match(
    $hostSearchSource,
    'private void OnHubPropertyChanged[\s\S]*?private void RebuildModFilters')
if (-not $hubOpenHandler.Success -or
    $hubOpenHandler.Value.Contains('RebuildIndex()') -or
    $hubOpenHandler.Value.Contains('SelectedTab =') -or
    $hubOpenHandler.Value.Contains('PrepareView') -or
    $hubOpenHandler.Value.Contains('ModSettingsSearchEditorFactory')) {
    throw 'Opening the native modsettings modal must not index, switch tabs, lay out views, or construct result editors.'
}
if ($hostSettingsXaml.Contains('<TextBlock Text="{Binding ToolTip}"') -or
    $hostSettingsXaml.IndexOf('<Border shared:ModSettingsSearch.Exclude="True"', [StringComparison]::Ordinal) -lt
        $hostSettingsXaml.IndexOf('<TextBlock Text="{Binding ErrorsText}"', [StringComparison]::Ordinal)) {
    throw 'SerpsModsHost search must follow the existing status text and expose result descriptions only as wrapping hover tooltips.'
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
if (Test-ModSelected 'CastlePlanner') {
    $castleSettingsSource = [IO.File]::ReadAllText((Join-Path $workspace 'CastlePlanner/src/CastlePlannerSettingsViewModel.cs'))
    $castleSettingsXaml = [IO.File]::ReadAllText((Join-Path $workspace $settingsByMod.CastlePlanner))
    if (-not $castleSettingsSource.Contains("[SyncHostOnly]`r`n        public bool SpawnBraziersAndFlags") -or
        $castleSettingsSource.Contains('SpawnBraziersAndFlagsData') -or
        $castleSettingsSource.Contains('SpawnBraziersAndFlagsReport')) {
        throw 'CastlePlanner braziers and flags must be one host-only setting without per-player companions.'
    }
    $braziersBinding = 'IsChecked="{Binding SpawnBraziersAndFlags, Mode=TwoWay}"'
    $braziersBindingIndex = $castleSettingsXaml.IndexOf($braziersBinding, [StringComparison]::Ordinal)
    if ($braziersBindingIndex -lt 0 -or
        $castleSettingsXaml.IndexOf($braziersBinding, $braziersBindingIndex + 1, [StringComparison]::Ordinal) -ge 0 -or
        $braziersBindingIndex -gt $castleSettingsXaml.IndexOf('Text="{Binding ClientOptionsText}"', [StringComparison]::Ordinal)) {
        throw 'CastlePlanner braziers and flags must appear exactly once in the host-options section.'
    }
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
}
if (Test-ModSelected 'CustomCustomTrail') {
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
}

$additionalCrlfTargetsByMod = @{
    BugfixesAndQoL = @(
        'BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs',
        'BugfixesAndQoL/src/BugfixesAndQoLPlugin.cs')
    CastlePlanner = @(
        'CastlePlanner/src/CastlePlannerSettingsViewModel.cs',
        'CastlePlanner/src/CastlePlannerPlugin.cs')
    CheatMod = @(
        'CheatMod/src/CheatModSettingsViewModel.cs',
        'CheatMod/src/CheatModPlugin.cs',
        'CheatMod/src/CheatModRuntime.cs',
        'CheatMod/CheatMod.csproj',
        'CheatMod/info.json',
        'CheatMod/build.bat',
        'CheatMod/release.bat')
    CustomCustomTrail = @(
        'CustomCustomTrail/src/CustomCustomTrailSettingsViewModel.cs',
        'CustomCustomTrail/src/CustomCustomTrailRuntime.cs')
}
$selectedLocaleDirectories = @($selectedModNames | ForEach-Object { $localeDirectories[$_] })
$selectedAdditionalCrlfTargets = @($selectedModNames |
    Where-Object { $additionalCrlfTargetsByMod.ContainsKey($_) } |
    ForEach-Object { $additionalCrlfTargetsByMod[$_] })
$crlfTargets = @($settings.Values) + @(
    $selectedLocaleDirectories | ForEach-Object {
        Get-ChildItem -LiteralPath (Join-Path $workspace $_) -File -Filter '*.txt' |
            ForEach-Object { [IO.Path]::GetRelativePath($workspace, $_.FullName) }
    }
) + @(
    'Shared/PresetLobbyModSettingsViewModel.cs',
    'Shared/ModSettingsSearch.cs',
    'Shared/GameModeHelper.cs',
    'SerpsModsHost/src/ModSettingsSearchPolicy.cs',
    'SerpsModsHost/src/ModSettingsSearchEditorFactory.cs',
    'SerpsModsHost/src/ModSettingsSearchViewModel.cs',
    '_inspect/HostClientPresetTests/Program.cs')
$crlfTargets += $selectedAdditionalCrlfTargets
foreach ($relativePath in $crlfTargets) {
    $text = [IO.File]::ReadAllText((Join-Path $workspace $relativePath))
    if ([Text.RegularExpressions.Regex]::IsMatch($text, '(?<!\r)\n')) {
        throw "$relativePath contains bare LF line endings."
    }
}

$auditScope = if ($PSBoundParameters.ContainsKey('Mod')) {
    "mod $Mod plus Shared"
} else {
    "all $($settings.Count) mods plus Shared"
}
Write-Output "PASS ($auditScope): XAML, shared-only registration, personal-setting declarations, automatic two-axis overflow scrolling, all interactive tooltips, shared styles, locale parity, nonempty translations, and CRLF."
