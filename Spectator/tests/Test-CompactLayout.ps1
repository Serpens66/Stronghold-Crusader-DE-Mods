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
        if (-not $card -or $card.Width -ne '28' -or $card.Height -ne '28') { throw 'Each fixed player ID needs a square portrait.' }
        $numberName = if ($prefix -eq 'TestTarget') { 'TestTargetText' } else { 'SpectatorEditorHumanNumber' }
        $number = Named ($numberName + $slot)
        if ($number.Text -ne [string]$slot -or $number.HorizontalAlignment -ne 'Left' -or $number.VerticalAlignment -ne 'Bottom' -or $number.ParentNode.LocalName -ne 'Grid' -or $number.HasAttribute('Background') -or $number.Style -ne '{StaticResource QuickNumber}') { throw 'The player number belongs inside the lower-left portrait corner.' }
    }
}
$amount = Named 'TestToolsAmount'
if ($amount.Text -ne '0' -or $amount.HasAttribute('MaxLength') -or @($xml.SelectNodes('//TextBox')).Count -ne 1) { throw 'A single, untruncated amount input starts at zero.' }
$credit = $xml.SelectSingleNode('//Button[@CommandParameter="Credit"]')
$reset = $xml.SelectSingleNode('//Button[@CommandParameter="ResetAmount"]')
if ($credit.IsEnabled -ne '{Binding CanCredit}' -or $credit.ParentNode -ne $amount.ParentNode -or $reset.ParentNode -ne $amount.ParentNode -or $reset.Content -ne '0') { throw 'Amount, validated send and reset share the same form.' }
$goods = @($xml.SelectNodes('//Button[starts-with(@CommandParameter,"G") and @CommandParameter != "Goods"]'))
if ($goods.Count -ne 21) { throw 'Every supported good remains available.' }
foreach ($name in @('SpectatorEditorHumanDrag','SpectatorHeader','SpectatorLeftWing','SpectatorRightWing','SpectatorOrdersGrid','SpectatorSnapFrame','SpectatorPaperSurface')) {
    if (-not (Named $name)) { throw ('Missing common toolbar part: ' + $name) }
}
if ((Named 'SpectatorEditorHumanPanel').HasAttribute('Background') -or (Named 'SpectatorWings').HasAttribute('Background')) { throw 'The notch must have no opaque surface or hit mask.' }
if ((Named 'SpectatorDetails').SelectNodes('.//ScrollViewer[not(@x:Name="PART_ContentHost")]', $ns).Count -ne 0) { throw 'Tool content must actually fit without a scrolling host.' }
if ((Named 'SpectatorSnapFrame').IsHitTestVisible -ne 'False') { throw 'The dock guide must never intercept mouse input.' }
if ((Named 'SpectatorHeader').HasAttribute('Background') -or (Named 'SpectatorDetails').HasAttribute('Background') -or (Named 'SpectatorLeftWing').HasAttribute('Background') -or (Named 'SpectatorRightWing').HasAttribute('Background')) { throw 'Docked paper must show through every toolbar region.' }
$numberStyle = $xml.SelectSingleNode('//Style[@x:Key="QuickNumber"]', $ns)
foreach ($pair in @(@('Height','15'),@('LineHeight','14'),@('LineStackingStrategy','BlockLineHeight'),@('Padding','0'))) {
    if ($numberStyle.SelectSingleNode('Setter[@Property="' + $pair[0] + '"]').Value -ne $pair[1]) { throw 'Number glyph needs explicit bottom-aligned line metrics.' }
}
$shadow = $numberStyle.SelectSingleNode('.//DropShadowEffect')
if ($shadow.ShadowDepth -ne '0' -or $shadow.Color -ne 'Black') { throw 'Numbers need an unshifted dark contour.' }
$layout = Get-Content -LiteralPath (Join-Path $root 'src/SpectatorRuntime.Layout.cs') -Raw
if ($layout -match 'TooltipAnchor|AddInfoBounds|TryPlace') { throw 'Hover state must not participate in toolbar positioning.' }
foreach ($pattern in @('placementRasterTransform.Restore()', 'RestorePlacementRaster();', 'new ToolbarProperty(placementRasterHost, UIElement.RenderTransformProperty)')) {
    if (-not $layout.Contains($pattern)) { throw 'The sole borrowed ancestor transform needs exact property/binding restoration.' }
}
Write-Host 'PASS: 28-unit portrait overlays/line metrics, one amount form, compact orders, transparent paper, hover-independent dock and restorable raster.'
