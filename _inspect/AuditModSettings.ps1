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
            $duration = $element.GetAttribute('ToolTipService.ShowDuration')
            if ([string]::IsNullOrWhiteSpace($tooltip) -or $duration -ne '60000') {
                throw "$($entry.Key): $elementName without a nonempty tooltip and exact 60000 ms duration."
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
    foreach ($required in @(
        'TargetType="{x:Type ToolTip}"',
        'VerticalScrollBarVisibility="{x:Static shared:ToolTipPresentation.AutomaticScrollBarVisibility}"',
        'HorizontalScrollBarVisibility="{x:Static shared:ToolTipPresentation.AutomaticScrollBarVisibility}"',
        'Value="#FF1D1710"',
        'MaxWidth="{x:Static shared:ToolTipPresentation.MaximumWidth}"',
        'Value="20"',
        'FontSize="{TemplateBinding FontSize}"',
        'TextWrapping="Wrap"',
        'x:Key="HostRoleHeader"',
        'x:Key="ClientRoleHeader"',
        'x:Key="SectionHeader"',
        'x:Key="HostActivationBorder"',
        'x:Key="ClientActivationBorder"',
        'Text="{Binding PresetText}"')) {
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

$crlfTargets = @($settings.Values) + @(
    $localeDirectories.Values | ForEach-Object {
        Get-ChildItem -LiteralPath (Join-Path $workspace $_) -File -Filter '*.txt' |
            ForEach-Object { [IO.Path]::GetRelativePath($workspace, $_.FullName) }
    }
)
foreach ($relativePath in $crlfTargets) {
    $text = [IO.File]::ReadAllText((Join-Path $workspace $relativePath))
    if ([Text.RegularExpressions.Regex]::IsMatch($text, '(?<!\r)\n')) {
        throw "$relativePath contains bare LF line endings."
    }
}

Write-Output "PASS: $($settings.Count) XAML files, automatic two-axis overflow scrolling, all interactive tooltips, shared styles, locale parity, nonempty translations, and CRLF."
