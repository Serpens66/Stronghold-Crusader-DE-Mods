[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$xaml = [IO.File]::ReadAllText((Join-Path $root 'Override/ScriptExtenderUI/ArabianIdleSoundTestSettings.xaml'))
$scroll = $xaml.SelectSingleNode("//*[local-name()='ScrollViewer']")
if ($scroll.HorizontalScrollBarVisibility -ne 'Auto' -or $scroll.VerticalScrollBarVisibility -ne 'Auto') { throw 'Outer scrolling contract changed.' }
foreach ($control in $xaml.SelectNodes("//*[local-name()='CheckBox' or local-name()='ComboBox' or local-name()='Button' or local-name()='TextBox' or local-name()='Slider']")) {
    if (-not $control.GetAttribute('ToolTip') -or $control.GetAttribute('ToolTipService.ShowDuration') -ne '60000') { throw "Missing bounded tooltip: $($control.OuterXml)" }
}
$text = [IO.File]::ReadAllText((Join-Path $root 'Override/ScriptExtenderUI/ArabianIdleSoundTestSettings.xaml'))
foreach ($required in @('shared:ToolTipPresentation.FontSize','shared:ToolTipPresentation.MaximumWidth','#FF1D1710','#FFF2D48A','TextWrapping="Wrap"','ClientActivationBorder','CanEditClientSettings','ClientSettingsEnabled','System_OpenPresetLoadCommand','System_OpenPresetSaveCommand','ResetToDefaultCommand','ProbeTitleText','ProbeIndex, Mode=TwoWay','SituationIndex, Mode=TwoWay','UnitTypeIndex, Mode=TwoWay','CooldownText, Mode=TwoWay')) {
    if (-not $text.Contains($required)) { throw "Missing UI contract: $required" }
}
$expected = $null
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $root 'Locales') -File -Filter '*.txt') {
    $keys = @([IO.File]::ReadAllLines($file.FullName) | Where-Object { $_ -match '^[^#;].*=' } | ForEach-Object { ($_ -split '=',2)[0].Trim() } | Sort-Object)
    if (@($keys | Select-Object -Unique).Count -ne $keys.Count) { throw "Duplicate locale key: $($file.Name)" }
    if ($null -eq $expected) { $expected = $keys }
    elseif (@(Compare-Object $expected $keys).Count) { throw "Locale key mismatch: $($file.Name)" }
    foreach ($key in @('AudioTest.ProbeTitle','AudioTest.ProbeHelp','AudioTest.Scope','AudioTest.Volume25','AudioTest.Replace','AudioTest.Suppress','AudioTest.PanLeft','AudioTest.CustomClip','AudioTest.SituationTitle','AudioTest.SituationHelp','AudioTest.UnitTypeTitle','AudioTest.UnitTypeHelp','AudioTest.CooldownTitle','AudioTest.CooldownHelp')) {
        if ($keys -notcontains $key) { throw "Missing locale key: $key" }
    }
}
$source = [IO.File]::ReadAllText((Join-Path $root 'src/SoundTestSettings.cs'))
if ([regex]::Matches($source,'\[PresetLocal\]').Count -ne 5 -or $source -match '\[SyncHostOnly|\[SyncPerPlayer' -or $source -notmatch 'CanMutateSetting' -or $source -notmatch 'SettingChanged\?\.Invoke') { throw 'Settings must publish guarded local values.' }
$plugin = [IO.File]::ReadAllText((Join-Path $root 'src/Plugin.cs'))
if ($plugin -match 'ConfigEntry|Config\.Bind' -or $plugin -notmatch 'private static SoundTestSettings settings;' -or $plugin -notmatch 'LobbyModSettingsPresetRegistration.Register') { throw 'Ingame settings ownership changed.' }
Write-Host 'PASS: client-only ingame settings, preset/search integration, tooltips and all locale key sets.'
