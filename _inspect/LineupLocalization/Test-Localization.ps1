[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$mod = Join-Path $workspace 'BugfixesAndQoL'
$newKeys = @('LineupDensity','LineupTight','LineupNormal','LineupFar','LineupVeryFar','LineupRangedSiege','LineupOff','LineupRear','LineupCenter','LineupRoleMarkers','LineupOn','LineupVanilla','LineupBlock','LineupLine','LineupColumn','LineupWedge','LineupCircle')
$affectedKeys = @('EnableMoveFormationEnhancements','EnableMoveFormationEnhancementsHelp','RememberRows','RememberRowsHelp','ArrangementTooltip') + $newKeys
$affectedKeys = @($affectedKeys | ForEach-Object { 'BugfixesAndQoL.' + $_ })
$localeFiles = @(Get-ChildItem -LiteralPath (Join-Path $mod 'Locales') -Filter '*.txt')
if ($localeFiles.Count -ne 21) { throw 'Expected 21 locales' }
$source = [IO.File]::ReadAllText((Join-Path $mod 'src\FormationMenuViewModel.cs'))
$properties = @([regex]::Matches($source, 'public string (\w+) => SerpLocalization.Get\("(BugfixesAndQoL\.[^"]+)"\)') | ForEach-Object { $_.Groups[1].Value })
$localeTables = @{}
foreach ($file in $localeFiles) {
    $text = [IO.File]::ReadAllText($file.FullName)
    if ($text -match '(?<!\r)\n|\\r\\n') { throw "Line endings: $($file.Name)" }
    $values = @{}
    foreach ($line in $text -split '\r\n') {
        if ($line -notmatch '^([^#=]+)=(.*)$') { continue }
        $key = $Matches[1]; $value = $Matches[2]
        if ($values.ContainsKey($key)) { throw "Duplicate locale key: $($file.Name): $key" }
        $values[$key] = $value
    }
    foreach ($key in $affectedKeys) {
        if (-not $values.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($values[$key])) { throw "Missing locale value: $($file.Name): $key" }
    }
    if ($file.BaseName -ne 'de-DE' -and $values['BugfixesAndQoL.ArrangementTooltip'] -eq 'Aufstellung') { throw "German title in $($file.Name)" }
    $original = @(& git -C $workspace show ('HEAD:BugfixesAndQoL/Locales/' + $file.Name))
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read original locale' }
    foreach ($line in $original) {
        if ($line -match '^([^#=]+)=(.*)$' -and $Matches[1] -notin $affectedKeys) {
            if ($values[$Matches[1]] -cne $Matches[2]) { throw "Unrelated locale changed: $($file.Name): $($Matches[1])" }
        }
    }
    $localeTables[$file.BaseName] = $values
}
if ($localeTables['en-US']['BugfixesAndQoL.ArrangementTooltip'] -cne 'Lineup' -or $localeTables['de-DE']['BugfixesAndQoL.ArrangementTooltip'] -cne 'Aufstellung') { throw 'Feature title regression' }

$hudPath = Join-Path $mod 'Patches\Assets\GUI\XAMLResources\HUD_Troops.xaml'
$hudText = [IO.File]::ReadAllText($hudPath)
[xml]$hud = $hudText
$originalHudLines = @(& git -C $workspace show 'HEAD:BugfixesAndQoL/Patches/Assets/GUI/XAMLResources/HUD_Troops.xaml')
if ($LASTEXITCODE -ne 0) { throw 'Cannot read original HUD patch' }
[xml]$originalHud = $originalHudLines -join [Environment]::NewLine
$currentCommands = @($hud.SelectNodes('//*[@CommandParameter]') | ForEach-Object { $_.GetAttribute('Command') + ':' + $_.GetAttribute('CommandParameter') })
$originalCommands = @($originalHud.SelectNodes('//*[@CommandParameter]') | ForEach-Object { $_.GetAttribute('Command') + ':' + $_.GetAttribute('CommandParameter') })
if (($currentCommands -join '|') -cne ($originalCommands -join '|')) { throw 'Command or rollover parameters changed' }
$menu = $hud.SelectSingleNode("//*[@*[local-name()='Name']='BugfixesAndQoLFormationMenuHost']")
foreach ($node in $menu.SelectNodes('.//*[@Text or @Content]')) {
    if ($node.LocalName -eq 'ContentControl') { continue } # Icon template payload is never a visible string.
    foreach ($attribute in @($node.Attributes | Where-Object { $_.Name -in @('Text','Content') })) {
        if ($attribute.Value -eq '{TemplateBinding Content}') { continue }
        if ($attribute.Value -notmatch '^\{Binding (\w+)\}$' -or $Matches[1] -notin $properties) { throw "Unlocalized or invalid binding: $($attribute.Value)" }
    }
}
foreach ($kind in @('Vanilla','Block','Line','Column','Wedge','Circle')) {
    if ($source -notmatch ('case "' + $kind + '": key = "BugfixesAndQoL.Lineup' + $kind + '"; break;')) { throw "Missing rollover mapping: $kind" }
}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $mod 'Patches') -Recurse -Filter '*.xaml') {
    [xml]$document = [IO.File]::ReadAllText($file.FullName)
    foreach ($content in $document.SelectNodes('/Patch/Operation/Content')) {
        if (@($content.ChildNodes | Where-Object { $_ -is [Xml.XmlElement] }).Count -ne 1) { throw "Patch Content contract: $($file.Name)" }
    }
}
foreach ($relative in @('src\FormationMenuViewModel.cs','info.json','Patches\Assets\GUI\XAMLResources\HUD_Troops.xaml')) {
    if ([IO.File]::ReadAllText((Join-Path $mod $relative)) -match '(?<!\r)\n|\\r\\n') { throw "CRLF: $relative" }
}
$info = [IO.File]::ReadAllText((Join-Path $mod 'info.json')) | ConvertFrom-Json
$entry = @($info.SerpChangelog | Where-Object { $_.Version -eq $info.Version })
$change = 'Fixed mixed-language labels and hover texts in the unit lineup menu across all supported languages; standardized the English feature name to Lineup.'
if ($info.Version -ne '1.0.187' -or $entry.Count -ne 1 -or $change -notin $entry[0].Changes) { throw 'Current-version changelog regression' }
Write-Output 'PASS: 21 locales, 17 new keys per language, preserved unrelated translations, localized menu/rollovers, unchanged commands, XAML Content contracts, CRLF and 1.0.187 changelog.'
