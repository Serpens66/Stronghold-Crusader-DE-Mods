param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot

$groups = @(
    @{
        Files = @('BuildingCosts\BepInEx\plugins\BuildingCosts_Serp\Override\ScriptExtenderUI\BuildingCostsSettings.xaml')
        Editors = @{ WoodText='WoodToolTip'; StoneText='StoneToolTip'; IronText='IronToolTip'; PitchText='PitchToolTip'; GoldText='GoldToolTip' }
    },
    @{
        Files = @('BuildingLimit\BepInEx\plugins\BuildingLimit_Serp\Override\ScriptExtenderUI\BuildingLimitSettings.xaml')
        Editors = @{ LimitText='LimitToolTip' }
    },
    @{
        Files = @('ExtraFeatures\Override\ScriptExtenderUI\ExtraFeaturesSettings.xaml', 'ExtraFeatures\BepInEx\plugins\ExtraFeatures_Serp\Override\ScriptExtenderUI\ExtraFeaturesSettings.xaml')
        Editors = @{
            CampfirePeasantsLimitText='CampfirePeasantsHelpText'; WoodRefundPercentValueText='BulldozeHelpText';
            StoneRefundPercentValueText='BulldozeHelpText'; IronRefundPercentValueText='BulldozeHelpText';
            PitchRefundPercentValueText='BulldozeHelpText'; GoldRefundPercentValueText='BulldozeHelpText';
            MultiplyGoodsGainAIText='MultiplyGoodsGainHelpText'; MultiplyGoodsGainHumanText='MultiplyGoodsGainHelpText';
            MultiplyGoodsGainInMoneyAIText='MultiplyGoodsAsMoneyHelpText'; MultiplyGoodsGainInMoneyHumanText='MultiplyGoodsAsMoneyHelpText';
            PlagueDurationMultiplierValueText='PlagueDurationMultiplierHelpText'; ApothecaryPlagueSearchDistanceValueText='ApothecaryPlagueSearchDistanceHelpText';
            MarketBuyPriceMultiplierValueText='MarketBuyPriceMultiplierHelpText'; MarketSellPriceMultiplierValueText='MarketSellPriceMultiplierHelpText';
            BuyMultiplierValueText='BuyToolTip'; SellMultiplierValueText='SellToolTip'
        }
    },
    @{
        Files = @('ImprovedHunters\BepInEx\plugins\ImprovedHunters_Serp\Override\ScriptExtenderUI\ImprovedHuntersSettings.xaml')
        Editors = @{
            MaxNeutralChickensPerPlayerValueText='MaxNeutralChickensPerPlayerHelpText'; DeerMeatText='MeatHelpText';
            GoatMeatText='MeatHelpText'; RabbitMeatText='MeatHelpText'; CamelMeatText='MeatHelpText'; ChickenMeatText='ChickenHelpText'
        }
    },
    @{
        Files = @('RandomEvents\Override\ScriptExtenderUI\RandomEventsSettings.xaml', 'RandomEvents\BepInEx\plugins\RandomEvents_Serp\Override\ScriptExtenderUI\RandomEventsSettings.xaml')
        Editors = @{
            IntervalMonthsValueText='IntervalHelpText'; CooldownMonthsValueText='CooldownHelpText';
            FairChanceValueText='ChanceHelpText'; PlagueChanceValueText='ChanceHelpText'; WheatInfestationChanceValueText='ChanceHelpText';
            HopsBeetlesChanceValueText='ChanceHelpText'; AppleBlightChanceValueText='ChanceHelpText'; TreeBlightChanceValueText='ChanceHelpText';
            RabbitsChanceValueText='ChanceHelpText'; LionAttackChanceValueText='ChanceHelpText'; BanditsChanceValueText='ChanceHelpText';
            MadCowsChanceValueText='ChanceHelpText'; ArchersChanceValueText='ChanceHelpText'; MarriageChanceValueText='ChanceHelpText';
            BardChanceValueText='ChanceHelpText'; GranaryTheftChanceValueText='ChanceHelpText'; FireChanceValueText='ChanceHelpText';
            PlagueMinValueText='PlagueStrengthHelpText'; PlagueMaxValueText='PlagueStrengthHelpText';
            LionMinValueText='LionStrengthHelpText'; LionMaxValueText='LionStrengthHelpText';
            BanditMinValueText='ScaledStrengthHelpText'; BanditMaxValueText='ScaledStrengthHelpText';
            ArcherMinValueText='ScaledStrengthHelpText'; ArcherMaxValueText='ScaledStrengthHelpText';
            TheftMinValueText='TheftStrengthHelpText'; TheftMaxValueText='TheftStrengthHelpText';
            FireMinValueText='FireStrengthHelpText'; FireMaxValueText='FireStrengthHelpText'
        }
        Renames = @{ PlagueMin='PlagueMinValueText'; PlagueMax='PlagueMaxValueText'; FireMin='FireMinValueText'; FireMax='FireMaxValueText' }
    },
    @{
        Files = @('UnitCosts\BepInEx\plugins\UnitCosts_Serp\Override\ScriptExtenderUI\UnitCostsSettings.xaml')
        Editors = @{ GoldText='GoldToolTip'; AmountText='ToolTip' }
    }
)

$changedFiles = 0
$changedEditors = 0
foreach ($group in $groups) {
    foreach ($relativePath in $group.Files) {
        $path = Join-Path $workspaceRoot $relativePath
        $content = [IO.File]::ReadAllText($path)

        if ($group.ContainsKey('Renames')) {
            foreach ($rename in $group.Renames.GetEnumerator()) {
                $old = 'Text="{Binding ' + $rename.Key + '}"'
                $new = 'Text="{Binding ' + $rename.Value + '}"'
                if ($content.IndexOf($old, [StringComparison]::Ordinal) -lt 0) {
                    throw "Expected binding '$old' was not found in $relativePath"
                }
                $content = $content.Replace($old, $new)
            }
        }

        foreach ($editor in $group.Editors.GetEnumerator()) {
            $property = [regex]::Escape($editor.Key)
            $pattern = '<TextBlock\b(?=[^>]*\bText="\{Binding\s+' + $property + '(?:,[^}]*)?\}")[^>]*/>'
            $tagMatches = [regex]::Matches($content, $pattern, [Text.RegularExpressions.RegexOptions]::Singleline)
            if ($tagMatches.Count -ne 1) {
                throw "Expected exactly one value label for '$($editor.Key)' in $relativePath, found $($tagMatches.Count)."
            }

            $tagMatch = $tagMatches[0]
            $tag = $tagMatch.Value.Replace('<TextBlock', '<TextBox')
            $bindingPattern = 'Text="\{Binding\s+' + $property + '(?:,[^}]*)?\}"'
            $binding = 'Text="{Binding ' + $editor.Key + ', Mode=TwoWay, UpdateSourceTrigger=LostFocus}"'
            $tag = [regex]::Replace($tag, $bindingPattern, $binding, 1)
            if ($tag -notmatch '\bToolTip=') {
                $tag = $tag.Replace('/>', ' ToolTipService.ShowDuration="60000" ToolTip="{Binding ' + $editor.Value + '}"/>')
            }
            $appearance = ' ui:KeyboardCaptureBinding.Enabled="True" MinWidth="56" MaxWidth="120" Padding="4,1" TextAlignment="Center" Background="#CC1A1A1A" BorderBrush="#FFB08A4A" BorderThickness="1"'
            $tag = $tag.Replace('/>', $appearance + '/>')
            $content = $content.Substring(0, $tagMatch.Index) + $tag + $content.Substring($tagMatch.Index + $tagMatch.Length)
            $changedEditors++
        }

        if ($Apply) {
            $normalized = [regex]::Replace($content, "\r?\n", "`r`n")
            [IO.File]::WriteAllText($path, $normalized, [Text.UTF8Encoding]::new($false))
        }
        $changedFiles++
    }
}

"Validated files=$changedFiles editor replacements=$changedEditors apply=$Apply"
