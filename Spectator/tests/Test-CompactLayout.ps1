$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
[xml]$xml = Get-Content -LiteralPath (Join-Path $root 'Patches/Assets/GUI/XAML/IngameUIScreens.xaml') -Raw
$ns = [Xml.XmlNamespaceManager]::new($xml.NameTable)
$ns.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
function Named($name) { $xml.SelectSingleNode('//*[@x:Name="' + $name + '"]', $ns) }
$owners = Named 'SpectatorEditorHumanOwners'
if ($owners.LocalName -ne 'StackPanel' -or $owners.Orientation -ne 'Horizontal') { throw 'Player selection must remain one ordered portrait row.' }
foreach ($slot in 1..8) {
    foreach ($prefix in @('SpectatorEditorHumanOwner','TestTarget')) {
        $card = Named ($prefix + $slot)
        if (-not $card -or $card.Width -ne $card.Height) { throw 'Each fixed player ID needs a square portrait.' }
        $numberName = if ($prefix -eq 'TestTarget') { 'TestTargetText' } else { 'SpectatorEditorHumanNumber' }
        $number = Named ($numberName + $slot)
        if ($number.Text -ne [string]$slot -or $number.HorizontalAlignment -ne 'Left' -or $number.VerticalAlignment -ne 'Bottom' -or $number.ParentNode.LocalName -ne 'Grid') { throw 'The player number belongs inside the lower-left portrait corner.' }
    }
}
$amount = Named 'TestToolsAmount'
if ($amount.Text -ne '0' -or $amount.HasAttribute('MaxLength') -or @($xml.SelectNodes('//TextBox')).Count -ne 1) { throw 'A single, untruncated amount input starts at zero.' }
$credit = $xml.SelectSingleNode('//Button[@CommandParameter="Credit"]')
$reset = $xml.SelectSingleNode('//Button[@CommandParameter="ResetAmount"]')
if ($credit.IsEnabled -ne '{Binding CanCredit}' -or $credit.ParentNode -ne $amount.ParentNode -or $reset.ParentNode -ne $amount.ParentNode -or $reset.Content -ne '0') { throw 'Amount, validated send and reset share the footer.' }
$goods = @($xml.SelectNodes('//Button[starts-with(@CommandParameter,"G") and @CommandParameter != "Goods"]'))
if ($goods.Count -ne 21) { throw 'Every supported good remains available.' }
foreach ($name in @('SpectatorEditorHumanDrag','SpectatorHeader','SpectatorLeftWing','SpectatorRightWing','SpectatorDetailsScroll')) {
    if (-not (Named $name)) { throw ('Missing common toolbar part: ' + $name) }
}
if ((Named 'SpectatorEditorHumanPanel').HasAttribute('Background') -or (Named 'SpectatorWings').HasAttribute('Background')) { throw 'The notch must have no opaque surface or hit mask.' }
Write-Host 'PASS: common portrait header, in-portrait numbers, one amount input, actions and open wing gaps.'
