$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
[xml]$xml = Get-Content -LiteralPath (Join-Path $root 'Patches\Assets\GUI\XAML\IngameUIScreens.xaml') -Raw
$ns = [Xml.XmlNamespaceManager]::new($xml.NameTable)
$ns.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
function Named($name) { $xml.SelectSingleNode('//*[@x:Name="' + $name + '"]', $ns) }
foreach ($name in @('SpectatorEditorHumanOwners','TestToolsTargets')) {
    $panel = Named $name
    if (-not $panel -or $panel.LocalName -ne 'WrapPanel' -or $panel.Width -ne '608' -or $panel.ItemWidth -ne '152' -or $panel.ItemHeight -ne '46') { throw "Four-column portrait layout changed: $name" }
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
if ($panel.Width -ne '318' -or $panel.HasAttribute('Height')) { throw 'Minimized panel must measure its content.' }
$amount = Named 'TestToolsAmount'
if ($amount.Text -ne '0' -or $amount.HasAttribute('MaxLength')) { throw 'Amount must start at zero without truncating pasted values.' }
$credit = $xml.SelectSingleNode('//Button[@CommandParameter="Credit"]')
if ($credit.IsEnabled -ne '{Binding CanCredit}' -or $amount.ParentNode -ne $credit.ParentNode) { throw 'Amount and enabled credit action must share one line.' }
$goods = @($xml.SelectNodes('//Button[starts-with(@CommandParameter,"G") and @CommandParameter != "Goods"]'))
if ($goods.Count -ne 21 -or @($goods | Where-Object { $_.ParentNode.ParentNode.LocalName -ne 'WrapPanel' }).Count) { throw 'All Vanilla goods must share the compact icon panel.' }
$commands = @($xml.SelectNodes('//Button/@CommandParameter') | ForEach-Object Value)
foreach ($view in @('Placement','Goods','Orders')) {
    $tab = $xml.SelectSingleNode('//Button[@CommandParameter="' + $view + '"]')
    if (-not $tab -or $tab.Width -ne '196') { throw "Three equal compact tabs required: $view" }
}
if (@($commands | Where-Object { $_ -match '^Power' }).Count) { throw 'Own power menu must be absent.' }
$toggle = $xml.SelectSingleNode('//CheckBox[@IsChecked="{Binding PowersEnabled, Mode=TwoWay}"]')
if (-not $toggle -or $toggle.ParentNode -ne (Named 'SpectatorEditorHumanOwners').ParentNode) { throw 'Power toggle must be in the common AI area.' }
if (@($commands | Where-Object { $_ -match '^(?:X[+-]|Prev|Next)' }).Count) { throw 'Obsolete amount or paging buttons remain.' }
$model = [IO.File]::ReadAllText((Join-Path $root 'src\TestToolsViewModel.cs'))
$hud = [IO.File]::ReadAllText((Join-Path $root 'src\SpectatorRuntime.Hud.cs'))
if ($model -match 'private bool expanded\s*=' -or $model -match 'OwnerPage|TargetPage|CreditMath.Adjust') { throw 'Default expansion or obsolete model state remains.' }
if ($hud -notmatch 'button.Visibility = available \? Visibility.Visible : Visibility.Collapsed' -or $hud -notmatch 'target.Visibility = targetAvailable \? Visibility.Visible : Visibility.Collapsed') { throw 'Unavailable cards must collapse and reflow.' }
Write-Host 'PASS: compact layout structure, all eight slots, 1/4/8-slot row geometry, inline action and zero default.'

$reset = $xml.SelectSingleNode('//Button[@CommandParameter="ResetAmount"]')
if ($credit.Width -ne '120' -or $reset.Width -ne '36' -or $reset.Content -ne '0' -or $reset.ParentNode -ne $credit.ParentNode) { throw 'Send/reset layout contract changed.' }
