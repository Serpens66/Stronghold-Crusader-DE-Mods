$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
foreach ($mod in @('UnitCosts','UnitLimit')) {
    $relative = if ($mod -eq 'UnitCosts') { 'src/UnitCostsRuntime.cs' } else { 'src/UnitLimitRuntime.RecruitmentAvailability.cs' }
    $path = Join-Path $root ($mod + '/' + $relative)
    $source = [IO.File]::ReadAllText($path)
    $helper = if ($mod -eq 'UnitCosts') { 'DisableRecruitmentButtonIfMissingExtraCosts' } else { 'DisableRecruitmentButtonIfLimitReached' }
    $pattern = '            ' + $helper + '\(playerId, (?:amount, )?eChimps\.(\w+), panel\.(\w+)\);'
    $matches = [regex]::Matches($source,$pattern)
    if ($matches.Count -ne 26) { throw "Unexpected button map size for ${mod}: $($matches.Count)" }
    $rows = @($matches | ForEach-Object { '            new RecruitmentButtonRule(eChimps.' + $_.Groups[1].Value + ', panel => panel.' + $_.Groups[2].Value + '),' })
    $fields = @'
        private readonly System.Collections.Generic.List<RecruitmentButtonRule> configuredRecruitmentButtons =
            new System.Collections.Generic.List<RecruitmentButtonRule>();
        private static readonly RecruitmentButtonRule[] RecruitmentButtonRules =
        {
'@
    $fields += "`r`n" + ($rows -join "`r`n") + "`r`n        };`r`n`r`n"
    $fields += @'
        private readonly struct RecruitmentButtonRule
        {
            internal RecruitmentButtonRule(eChimps unitType, System.Func<HUD_Buildings, Noesis.UIElement> getButton)
            {
                UnitType = unitType;
                GetButton = getButton;
            }
            internal eChimps UnitType { get; }
            internal System.Func<HUD_Buildings, Noesis.UIElement> GetButton { get; }
        }

        private void RebuildConfiguredRecruitmentButtons()
        {
            configuredRecruitmentButtons.Clear();
            foreach (RecruitmentButtonRule rule in RecruitmentButtonRules)
'@
    $predicate = if ($mod -eq 'UnitCosts') { 'TryGetHumanExtraCosts(rule.UnitType, out _)' } else { 'activeUnitLimits.TryGetValue(rule.UnitType, out int limit) && limit >= 0' }
    $fields += "`r`n                if ($predicate)`r`n                    configuredRecruitmentButtons.Add(rule);`r`n        }`r`n`r`n"
    $fields = [regex]::Replace($fields,'\r?\n',"`r`n")
    $first = $matches[0].Index; $last = $matches[$matches.Count-1]
    $end = $last.Index + $last.Length
    $arguments = if ($mod -eq 'UnitCosts') { 'playerId, amount, rule.UnitType, rule.GetButton(panel)' } else { 'playerId, rule.UnitType, rule.GetButton(panel)' }
    $source = $source.Substring(0,$first) + "            // Resolve from the current panel; retain Vanilla/other-mod disabled states.`r`n            foreach (RecruitmentButtonRule rule in configuredRecruitmentButtons)`r`n                $helper($arguments);" + $source.Substring($end)
    $source = $source.Replace('        internal void RefreshRecruitmentButtonAvailability()', $fields + '        internal void RefreshRecruitmentButtonAvailability()')
    $dictionary = if ($mod -eq 'UnitCosts') { 'humanExtraCosts' } else { 'activeUnitLimits' }
    $source = $source.Replace('|| ' + $dictionary + '.Count == 0)', '|| configuredRecruitmentButtons.Count == 0)')
    if ($mod -eq 'UnitCosts') {
        $source = $source.Replace('            humanExtraCosts.Clear();', "            humanExtraCosts.Clear();`r`n            configuredRecruitmentButtons.Clear();")
        $source = $source.Replace('            Shared.DebugLogHelper.LogDebug(log, "Applied human extra unit cost rows:", configuredUnits);', "            RebuildConfiguredRecruitmentButtons();`r`n            Shared.DebugLogHelper.LogDebug(log, `"Applied human extra unit cost rows:`", configuredUnits);")
    }
    [IO.File]::WriteAllText($path,$source,[Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($path) -cne $source -or [regex]::Matches($source,'(?<!\r)\n').Count) { throw "Write verification $path" }
    "${mod}: 26 original mappings retained; CRLF=$([regex]::Matches($source,"`r`n").Count)"
}
foreach ($relative in @('UnitLimit/src/UnitLimitRuntime.cs','UnitLimit/src/UnitLimitRuntime.UnitLimits.cs')) {
    $path = Join-Path $root $relative
    $source = [IO.File]::ReadAllText($path)
    $source = $source.Replace('            activeUnitLimits.Clear();', "            activeUnitLimits.Clear();`r`n            configuredRecruitmentButtons.Clear();")
    $source = $source.Replace('            LogDebug("Applied active unit limit rules:", activeUnitLimits.Count);', "            RebuildConfiguredRecruitmentButtons();`r`n            LogDebug(`"Applied active unit limit rules:`", activeUnitLimits.Count);")
    [IO.File]::WriteAllText($path,$source,[Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($path) -cne $source -or [regex]::Matches($source,'(?<!\r)\n').Count) { throw "Write verification $path" }
}
