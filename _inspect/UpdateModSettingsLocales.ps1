param()

$workspace = Split-Path -Parent $PSScriptRoot
$utf8 = [System.Text.UTF8Encoding]::new($false)
$changedFiles = 0

function Set-LocaleKey {
    param(
        [string]$Path,
        [string]$Key,
        [string]$Value
    )

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.AddRange([System.IO.File]::ReadAllLines($Path))
    $prefix = $Key + '='
    $found = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i].StartsWith($prefix, [System.StringComparison]::Ordinal)) {
            $lines[$i] = $prefix + $Value
            $found = $true
            break
        }
    }
    if (-not $found) {
        $lines.Add($prefix + $Value)
    }

    $expected = ($lines -join "`r`n") + "`r`n"
    $before = [System.IO.File]::ReadAllText($Path)
    if (-not [string]::Equals($before, $expected, [System.StringComparison]::Ordinal)) {
        [System.IO.File]::WriteAllText($Path, $expected, $utf8)
        $script:changedFiles++
    }
    $after = [System.IO.File]::ReadAllText($Path)
    if (-not [string]::Equals($after, $expected, [System.StringComparison]::Ordinal)) {
        throw "Ordinal locale readback mismatch: $Path"
    }
}

function Remove-LocaleKey {
    param([string]$Path, [string]$Key)

    $prefix = $Key + '='
    $lines = [System.IO.File]::ReadAllLines($Path) | Where-Object {
        -not $_.StartsWith($prefix, [System.StringComparison]::Ordinal)
    }
    $expected = ($lines -join "`r`n") + "`r`n"
    $before = [System.IO.File]::ReadAllText($Path)
    if (-not [string]::Equals($before, $expected, [System.StringComparison]::Ordinal)) {
        [System.IO.File]::WriteAllText($Path, $expected, $utf8)
        $script:changedFiles++
    }
}

$localeDirectories = @(
    'BugfixesAndQoL\Locales',
    'BuildingCosts\Locales',
    'BuildingLimit\Locales',
    'ExtraFeatures\Locales',
    'ImprovedHunters\Locales',
    'RandomEvents\Locales',
    'CastlePlanner\BepInEx\plugins\CastlePlanner_Serp\Locales',
    'CustomCustomTrail\Locales',
    'StartConditions\Locales',
    'UnitCosts\Locales',
    'UnitLimit\Locales'
)

foreach ($relativeDirectory in $localeDirectories) {
    $directory = Join-Path $workspace $relativeDirectory
    foreach ($file in Get-ChildItem -LiteralPath $directory -Filter '*.txt') {
        $german = $file.Name -eq 'de-DE.txt'
        Set-LocaleKey $file.FullName 'Common.HostActivationLabel' '(Host-)'
        Set-LocaleKey $file.FullName 'Common.ClientActivationLabel' $(if ($german) { '(Client-Settings)' } else { '(Client settings)' })
        Set-LocaleKey $file.FullName 'Common.HostSettingsActivationHelp' $(if ($german) { 'Aktiviert oder deaktiviert alle vom Host gesteuerten Einstellungen dieser Mod.' } else { 'Enables or disables all host-controlled settings of this mod.' })
        Set-LocaleKey $file.FullName 'Common.ClientSettingsActivationHelp' $(if ($german) { 'Aktiviert oder deaktiviert alle lokalen und persönlichen Client-Einstellungen dieser Mod.' } else { 'Enables or disables all local and personal client settings of this mod.' })
        Set-LocaleKey $file.FullName 'Common.Preset' 'Preset'
        Set-LocaleKey $file.FullName 'Common.ActionsScopeHost' $(if ($german) { 'Preset und Zurücksetzen betreffen Host-Einstellungen und deine lokalen Client-Optionen.' } else { 'Preset and reset affect host settings and your local client settings.' })
        Set-LocaleKey $file.FullName 'Common.ActionsScopeClient' $(if ($german) { 'Preset und Zurücksetzen betreffen nur deine lokalen Client-Optionen.' } else { 'Preset and reset affect only your local client settings.' })
    }
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspace 'BugfixesAndQoL\Locales') -Filter '*.txt') {
    $german = $file.Name -eq 'de-DE.txt'
    Set-LocaleKey $file.FullName 'BugfixesAndQoL.EnableClientFeatures' $(if ($german) { 'Lokale Client-Funktionen aktivieren' } else { 'Enable local client features' })
    Set-LocaleKey $file.FullName 'BugfixesAndQoL.EnableClientFeaturesHelp' $(if ($german) { 'Aktiviert oder deaktiviert die lokalen Oberflächen- und Steuerungsfunktionen dieses Mods nur für dich.' } else { "Enables or disables this mod's local interface and control features for you." })
    Set-LocaleKey $file.FullName 'BugfixesAndQoL.EnableHostFeatures' $(if ($german) { 'Host-Funktionen aktivieren' } else { 'Enable host features' })
    Set-LocaleKey $file.FullName 'BugfixesAndQoL.EnableHostFeaturesHelp' $(if ($german) { 'Aktiviert oder deaktiviert die vom Host gesteuerten Fehlerbehebungen für das Match.' } else { 'Enables or disables the host-controlled fixes for the match.' })
    Set-LocaleKey $file.FullName 'BugfixesAndQoL.ClientInterfaceTitle' $(if ($german) { 'Oberfläche und Steuerung' } else { 'Interface and Controls' })
    Set-LocaleKey $file.FullName 'BugfixesAndQoL.AiAivTitle' $(if ($german) { 'KI und AIV' } else { 'AI and AIV' })
    Set-LocaleKey $file.FullName 'BugfixesAndQoL.TroopMovementTitle' $(if ($german) { 'Truppenbewegung' } else { 'Troop Movement' })
    Set-LocaleKey $file.FullName 'BugfixesAndQoL.PlagueTitle' $(if ($german) { 'Pest' } else { 'Plague' })
    Set-LocaleKey $file.FullName 'BugfixesAndQoL.AIEconomyProtectionTitle' $(if ($german) { 'KI-Wirtschaftsschutz' } else { 'AI Economy Protection' })
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspace 'ExtraFeatures\Locales') -Filter '*.txt') {
    $german = $file.Name -eq 'de-DE.txt'
    Set-LocaleKey $file.FullName 'SomeSettings.NewFeaturesTitle' $(if ($german) { 'Neue Spielfunktionen' } else { 'New Gameplay Features' })
    Set-LocaleKey $file.FullName 'SomeSettings.TilesValueFormat' $(if ($german) { '{0} Felder' } else { '{0} tiles' })
    Set-LocaleKey $file.FullName 'SomeSettings.BuildingsProductionTitle' $(if ($german) { 'Gebäude und Produktion' } else { 'Buildings and Production' })
    Set-LocaleKey $file.FullName 'SomeSettings.CampfirePeasants' $(if ($german) { 'Wartende Bauern am Burgfeuer' } else { 'Peasants waiting at the campfire' })
    Set-LocaleKey $file.FullName 'SomeSettings.CampfirePeasantsHelp' $(if ($german) { '-1 = unverändert. Erlaubter Bereich: -1 bis 500. Legt die maximale Anzahl Bauern fest, die am Burgfeuer warten.' } else { '-1 = unchanged. Allowed range: -1 to 500. Sets the maximum peasants waiting at the campfire.' })
    Set-LocaleKey $file.FullName 'SomeSettings.EnableExtraChurchPriestsHelp' $(if ($german) { 'Wenn aktiviert, erhalten Kirchen zwei Priester und Kathedralen drei Priester.' } else { 'When enabled, churches receive two priests and cathedrals receive three priests.' })
    Set-LocaleKey $file.FullName 'SomeSettings.BulldozeTitle' $(if ($german) { 'Gebäudeabriss' } else { 'Building Demolition' })
    Set-LocaleKey $file.FullName 'SomeSettings.EconomyBuffsTitle' $(if ($german) { 'Wirtschaftsboni' } else { 'Economy Bonuses' })
    Set-LocaleKey $file.FullName 'SomeSettings.MarketPriceMultipliersTitle' $(if ($german) { 'Marktpreise' } else { 'Market Prices' })
    Set-LocaleKey $file.FullName 'SomeSettings.PlagueTitle' $(if ($german) { 'Pest' } else { 'Plague' })
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspace 'UnitLimit\Locales') -Filter '*.txt') {
    Remove-LocaleKey $file.FullName 'UnitLimit.CampfirePeasants'
    Remove-LocaleKey $file.FullName 'UnitLimit.CampfirePeasantsHelp'
    Set-LocaleKey $file.FullName 'UnitLimit.Title' $(if ($file.Name -eq 'de-DE.txt') { 'Einheitenlimits (Mensch)' } else { 'Unit Limits (Human)' })
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspace 'RandomEvents\Locales') -Filter '*.txt') {
    $german = $file.Name -eq 'de-DE.txt'
    Set-LocaleKey $file.FullName 'RandomEvents.MonthsValueFormat' $(if ($german) { '{0} Monate' } else { '{0} months' })
    Set-LocaleKey $file.FullName 'RandomEvents.GroupsValueFormat' $(if ($german) { '{0} Gruppen' } else { '{0} groups' })
    Set-LocaleKey $file.FullName 'RandomEvents.PositiveEventsTitle' $(if ($german) { 'Positive Ereignisse' } else { 'Positive Events' })
    Set-LocaleKey $file.FullName 'RandomEvents.NegativeEventsTitle' $(if ($german) { 'Negative Ereignisse' } else { 'Negative Events' })
    Set-LocaleKey $file.FullName 'RandomEvents.ScheduleTitle' $(if ($german) { 'Zeitplan' } else { 'Schedule' })
    Set-LocaleKey $file.FullName 'RandomEvents.ChancesTitle' $(if ($german) { 'Ereigniswahrscheinlichkeiten (%)' } else { 'Event Probabilities (%)' })
    Set-LocaleKey $file.FullName 'RandomEvents.StrengthTitle' $(if ($german) { 'Ereignisstärken' } else { 'Event Strengths' })
    Set-LocaleKey $file.FullName 'RandomEvents.MultiplayerTitle' $(if ($german) { 'Mehrspieler' } else { 'Multiplayer' })
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspace 'CastlePlanner\BepInEx\plugins\CastlePlanner_Serp\Locales') -Filter '*.txt') {
    $german = $file.Name -eq 'de-DE.txt'
    Set-LocaleKey $file.FullName 'CastlePlanner.CastleSectionTitle' $(if ($german) { 'Burgvorlage' } else { 'Castle Blueprint' })
    Set-LocaleKey $file.FullName 'CastlePlanner.PlacementControlsTitle' $(if ($german) { 'Platzierung und Steuerung' } else { 'Placement and Controls' })
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspace 'StartConditions\Locales') -Filter '*.txt') {
    $german = $file.Name -eq 'de-DE.txt'
    if ($german) {
        Set-LocaleKey $file.FullName 'StartConditions.Deathmatch' 'Deathmatch'
    }
    Set-LocaleKey $file.FullName 'StartConditions.StartGoldTitle' $(if ($german) { 'Startgold' } else { 'Start Gold' })
    Set-LocaleKey $file.FullName 'StartConditions.StartGoodsTitle' $(if ($german) { 'Startwaren' } else { 'Start Goods' })
    Set-LocaleKey $file.FullName 'StartConditions.StartTroopsTitle' $(if ($german) { 'Starttruppen' } else { 'Start Troops' })
    Set-LocaleKey $file.FullName 'StartConditions.StartTroopArmiesTitle' $(if ($german) { 'Armee-Multiplikator' } else { 'Start-troop Armies' })
    Set-LocaleKey $file.FullName 'StartConditions.ExtraStartUnitsTitle' $(if ($german) { 'Zusätzliche Einheiten' } else { 'Additional Start Units' })
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspace 'UnitCosts\Locales') -Filter '*.txt') {
    Set-LocaleKey $file.FullName 'UnitCosts.Title' $(if ($file.Name -eq 'de-DE.txt') { 'Grundkosten (Mensch und KI)' } else { 'Base Costs (Human and AI)' })
    Set-LocaleKey $file.FullName 'UnitCosts.ExtraTitle' $(if ($file.Name -eq 'de-DE.txt') { 'Zusatzkosten (nur Mensch)' } else { 'Additional Costs (Human only)' })
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspace 'BuildingCosts\Locales') -Filter '*.txt') {
    Set-LocaleKey $file.FullName 'BuildingCosts.Title' $(if ($file.Name -eq 'de-DE.txt') { 'Baukosten' } else { 'Building Costs' })
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspace 'BuildingLimit\Locales') -Filter '*.txt') {
    Set-LocaleKey $file.FullName 'BuildingLimit.Title' $(if ($file.Name -eq 'de-DE.txt') { 'Gebäudelimits (Mensch)' } else { 'Building Limits (Human)' })
}

foreach ($file in Get-ChildItem -LiteralPath (Join-Path $workspace 'ImprovedHunters\Locales') -Filter '*.txt') {
    $german = $file.Name -eq 'de-DE.txt'
    Set-LocaleKey $file.FullName 'ImprovedHunters.Title' $(if ($german) { 'Verbesserte Jäger' } else { 'Improved Hunters' })
    Set-LocaleKey $file.FullName 'ImprovedHunters.BehaviorTitle' $(if ($german) { 'Verhalten' } else { 'Behavior' })
    Set-LocaleKey $file.FullName 'ImprovedHunters.TargetsYieldTitle' $(if ($german) { 'Ziele und Fleischertrag' } else { 'Targets and Meat Yield' })
}

Write-Output "Locale files changed: $changedFiles"
