$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
[xml]$xml = Get-Content -LiteralPath (Join-Path $root 'Patches\Assets\GUI\XAML\IngameUIScreens.xaml') -Raw
$ns = [Xml.XmlNamespaceManager]::new($xml.NameTable)
$ns.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
function Named($name) { $xml.SelectSingleNode('//*[@x:Name="' + $name + '"]', $ns) }
foreach ($name in @('SpectatorEditorHumanOwners','TestToolsTargets')) {
    $panel = Named $name
    if (-not $panel -or $panel.LocalName -ne 'Grid' -or $panel.Width -ne '608') { throw "Four-column portrait layout changed: $name" }
    # Auto rows collapse completely when no visible card is assigned to them.
    if (@($panel.SelectNodes('./Grid.RowDefinitions/RowDefinition[@Height="Auto"]')).Count -ne 2 -or
        @($panel.SelectNodes('./Grid.ColumnDefinitions/ColumnDefinition[@Width="152"]')).Count -ne 4) { throw 'Portrait rows must measure only visible content.' }
    $cards = @($panel.SelectNodes('./Button'))
    if ($cards.Count -ne 8) { throw "All eight slots must exist: $name" }
    foreach ($card in $cards) {
        if ($card.Width -ne '148' -or $card.Height -ne '42' -or -not $card.SelectSingleNode('.//TextBlock[@TextTrimming="CharacterEllipsis"]')) { throw 'Portrait size or long-name trimming changed.' }
    }
}
foreach ($slot in 1..8) {
    $quick = Named ('SpectatorQuickOwner' + $slot)
    if (-not $quick -or $quick.Width -ne '32' -or $quick.Height -ne '28') { throw 'Missing compact one-click player button.' }
}
foreach ($removed in @('Reports','Allies','Statistics','RefreshStatistics','CloseStatistics')) {
    if ($xml.SelectSingleNode('//Button[@CommandParameter="' + $removed + '"]')) { throw ('Redundant toolbar action: ' + $removed) }
}
$panel = Named 'SpectatorEditorHumanPanel'
if ($panel.Width -ne '318' -or $panel.HasAttribute('Height') -or (Named 'SpectatorDetails').ParentNode -ne $panel.ParentNode) { throw 'Minimized panel must measure its content.' }
$amount = Named 'TestToolsAmount'
if ($amount.Text -ne '0' -or $amount.HasAttribute('MaxLength')) { throw 'Amount must start at zero without truncating pasted values.' }
$credit = $xml.SelectSingleNode('//Button[@CommandParameter="Credit"]')
if ($credit.IsEnabled -ne '{Binding CanCredit}' -or $amount.ParentNode -ne $credit.ParentNode) { throw 'Amount and enabled credit action must share one line.' }
$goods = @($xml.SelectNodes('//Button[starts-with(@CommandParameter,"G") and @CommandParameter != "Goods"]'))
if ($goods.Count -ne 21 -or @($goods | Where-Object { $_.ParentNode.ParentNode.LocalName -ne 'WrapPanel' }).Count) { throw 'All Vanilla goods must share the compact icon panel.' }
$commands = @($xml.SelectNodes('//Button/@CommandParameter') | ForEach-Object Value)
foreach ($view in @('Placement','Goods','Orders','TogglePowers')) {
    $tab = $xml.SelectSingleNode('//Button[@CommandParameter="' + $view + '"]')
    if (-not $tab -or $tab.Width -ne '148' -or $tab.Height -ne '36') { throw "Four equal compact tool buttons required: $view" }
}
if (@($commands | Where-Object { $_ -match '^Power' }).Count) { throw 'Own power menu must be absent.' }
$toggle = $xml.SelectSingleNode('//Button[@CommandParameter="TogglePowers"]')
$placement = $xml.SelectSingleNode('//Button[@CommandParameter="Placement"]')
if ($toggle.ParentNode -ne $placement.ParentNode -or $toggle.IsEnabled -ne '{Binding ToolsAvailable}') { throw 'Power switch must share the tool row and retain availability.' }
if ($xml.SelectSingleNode('//*[@Text="{Binding SelectedPlayer}"]') -or
    $xml.SelectSingleNode('//CheckBox[@IsChecked="{Binding PowersEnabled, Mode=TwoWay}"]')) { throw 'Redundant heading or power checkbox remains.' }
if (-not (Named 'SpectatorDetailsHeader') -or (Named 'SpectatorDetailsScroll').GetAttribute('Grid.Row') -ne '1' -or
    $panel.GetAttribute('Panel.ZIndex') -ne '1') { throw 'Fixed quick bar must overlay the common header above scrollable content.' }
if (@($commands | Where-Object { $_ -match '^(?:X[+-]|Prev|Next)' }).Count) { throw 'Obsolete amount or paging buttons remain.' }
$model = [IO.File]::ReadAllText((Join-Path $root 'src\TestToolsViewModel.cs'))
$hud = [IO.File]::ReadAllText((Join-Path $root 'src\SpectatorRuntime.Hud.cs'))
if ($model -match 'private bool expanded\s*=\s*true' -or $model -match 'OwnerPage|TargetPage|CreditMath.Adjust') { throw 'Default expansion or obsolete model state remains.' }
if ($hud -notmatch 'button.Visibility = available \? Visibility.Visible : Visibility.Collapsed' -or $hud -notmatch 'target.Visibility = targetAvailable \? Visibility.Visible : Visibility.Collapsed') { throw 'Unavailable cards must collapse and reflow.' }
Write-Host 'PASS: compact layout structure, all eight slots, auto-row portrait structure (runtime packing is tested separately), inline action and zero default.'

$reset = $xml.SelectSingleNode('//Button[@CommandParameter="ResetAmount"]')
if ($credit.Width -ne '120' -or $reset.Width -ne '36' -or $reset.Content -ne '0' -or $reset.ParentNode -ne $credit.ParentNode) { throw 'Send/reset layout contract changed.' }
