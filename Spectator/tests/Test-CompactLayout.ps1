$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
[xml]$xml = Get-Content -LiteralPath (Join-Path $root 'Patches/Assets/GUI/XAML/IngameUIScreens.xaml') -Raw
$ns = [Xml.XmlNamespaceManager]::new($xml.NameTable)
$ns.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
function Named($name) { $xml.SelectSingleNode('//*[@x:Name="' + $name + '"]', $ns) }
$owners = Named 'SpectatorEditorHumanOwners'
if ($owners.LocalName -ne 'Canvas') { throw 'The same portrait canvas must support both arrangements.' }
foreach ($slot in 1..8) {
    foreach ($prefix in @('SpectatorEditorHumanOwner','TestTarget')) {
        $card = Named ($prefix + $slot)
        $size = if ($prefix -eq 'TestTarget') { '42' } else { '36' }
        if (-not $card -or $card.Width -ne $size -or $card.Height -ne $size) { throw 'Main portraits remain 36 units; order targets are 42 units.' }
        if ($prefix -eq 'TestTarget') {
            $face = $card.SelectSingleNode('Canvas/Image')
            if ($face.Width -ne '40' -or $face.Height -ne '40' -or $face.ParentNode.Width -ne '40' -or $face.ParentNode.Height -ne '40') { throw 'The target sprite itself grows with its portrait button.' }
        }
        $numberName = if ($prefix -eq 'TestTarget') { 'TestTargetText' } else { 'SpectatorEditorHumanNumber' }
        $number = Named ($numberName + $slot)
        if ($number.Text -ne [string]$slot -or $number.ParentNode.LocalName -ne 'Canvas' -or $number.GetAttribute('Canvas.Left') -ne '0' -or $number.GetAttribute('Canvas.Top') -ne '0' -or $number.HasAttribute('Background')) { throw 'Measured glyph layers require their own absolute portrait overlay without a background.' }
        $outline = $number.PreviousSibling
        $stroke = '3'
        if ($outline.LocalName -ne 'TextBlock' -or $outline.Text -ne $number.Text -or $outline.StrokeThickness -ne $stroke -or $outline.Style -ne $number.Style -or $outline.HasAttribute('Background')) { throw 'The stronger light outer stroke must follow the same glyph and metrics.' }
        if ($prefix -eq 'SpectatorEditorHumanOwner' -and $card.ParentNode -ne $owners) { throw 'The existing buttons retain the common packed roster host.' }
    }
}
foreach ($slot in 1..8) {
 foreach ($prefix in @('SpectatorEditorHumanOwner','TestTarget')) {
  $card=Named ($prefix+$slot)
  if ($card.GetAttribute('ToolTipService.InitialShowDelay') -ne '0' -or $card.GetAttribute('ToolTipService.BetweenShowDelay') -ne '0') { throw 'Lord and target portrait help must appear immediately.' }
 }
}
$drag=Named 'SpectatorEditorHumanDrag'
if ($drag.GetAttribute('ToolTipService.InitialShowDelay') -ne '0' -or $drag.GetAttribute('ToolTipService.BetweenShowDelay') -ne '0') { throw 'Drag help must appear immediately.' }
$amount = Named 'TestToolsAmount'
if ($amount.Text -ne '0' -or $amount.HasAttribute('MaxLength') -or @($xml.SelectNodes('//TextBox')).Count -ne 1) { throw 'A single untruncated amount input starts at zero.' }
$credit = $xml.SelectSingleNode('//Button[@CommandParameter="Credit"]')
$debit = $xml.SelectSingleNode('//Button[@CommandParameter="Debit"]')
$reset = $xml.SelectSingleNode('//Button[@CommandParameter="ResetAmount"]')
if ($credit.IsEnabled -ne '{Binding CanCredit}' -or $debit.IsEnabled -ne '{Binding CanCredit}' -or $credit.ParentNode -ne $debit.ParentNode -or $credit.ParentNode.ParentNode -ne $amount.ParentNode -or $credit.ParentNode.GetAttribute('Grid.Row') -ne '1' -or $amount.GetAttribute('Grid.Row') -eq '1' -or $reset.ParentNode -ne $amount.ParentNode -or $reset.Content -ne '0') { throw 'Amount sits above equal validated credit/debit actions in the shared form.' }
$goods = @($xml.SelectNodes('//Button[starts-with(@CommandParameter,"G") and @CommandParameter != "Goods"]'))
if ($goods.Count -ne 21) { throw 'Every supported good remains available.' }
foreach ($name in @('SpectatorEditorHumanDrag','SpectatorRosterPanel','SpectatorToolPanel','SpectatorHeader','SpectatorLeftWing','SpectatorRightWing','SpectatorOrdersGrid','SpectatorSnapFrame','SpectatorToolSnapFrame','SpectatorPaperSurface')) {
    if (-not (Named $name)) { throw ('Missing common toolbar part: ' + $name) }
}
if ((Named 'SpectatorEditorHumanPanel').HasAttribute('Background') -or (Named 'SpectatorWings').HasAttribute('Background')) { throw 'Gaps between real painted surfaces must not create an invisible input mask.' }
if ((Named 'SpectatorDetails').SelectNodes('.//ScrollViewer[not(@x:Name="PART_ContentHost")]', $ns).Count -ne 0) { throw 'Tools must fit without a scrolling host.' }
foreach ($name in @('SpectatorSnapFrame','SpectatorToolSnapFrame','SpectatorSnapGrip')) {
    if ((Named $name).IsHitTestVisible -ne 'False') { throw 'Dock guides never intercept input.' }
}
foreach ($name in @('SpectatorLeftWing','SpectatorRightWing')) {
    if ((Named $name).Background -ne 'Transparent' -or (Named $name).IsHitTestVisible -eq 'False') { throw 'Footer inputs sit above the single clipped paper layer, without restarting the image.' }
}
$tools = Named 'SpectatorToolPanel'
if ($tools.Visibility -ne '{Binding ExpandedVisibility}') { throw 'Placement, collapse and occupied HUDs must remove the complete tool surface, including category overdraw.' }
$roster = Named 'SpectatorRosterPanel'
$grip = Named 'SpectatorEditorHumanDrag'
if ($roster.SelectSingleNode('.//*[@x:Name="SpectatorEditorHumanDrag"]', $ns) -or $grip.ParentNode -ne (Named 'TestToolsRoot') -or $grip.HasAttribute('Width')) { throw 'One independent grip follows the dock gap or the floating left edge.' }
# The floating roster spans the grip and has a transparent hit-testable background.
# Relative sibling order is the contract here; this does not replace a real Noesis mouse test.
if ($grip.ParentNode -ne $roster.ParentNode -or [int]$grip.GetAttribute('Panel.ZIndex') -le [int]$roster.GetAttribute('Panel.ZIndex')) { throw 'Drag grip must receive input above the overlapping portrait container.' }
$paper = Named 'SpectatorPaperSurface'
if ($paper.ParentNode -ne (Named 'TestToolsRoot') -or $paper.IsHitTestVisible -ne 'False' -or $paper.GetAttribute('Panel.ZIndex') -ne '0') { throw 'Pooled parchment patches use an explicit nonnegative common background layer.' }
foreach($name in @('SpectatorRosterPanel','SpectatorControlSlot','SpectatorToolPanel','SpectatorEditorHumanDrag','SpectatorDesign')) {
 if ([int](Named $name).GetAttribute('Panel.ZIndex') -le 1) { throw 'Interactive controls must be above background and footer input covers.' }
}
$design=Named 'SpectatorDesign'
if ($design.CommandParameter -ne 'ToggleDesign' -or $design.GetAttribute('ToolTipService.InitialShowDelay') -ne '0' -or $design.Visibility -ne 'Collapsed') { throw 'Design control starts hidden until floating and has immediate help.' }
$gripStyle=(Named 'SpectatorEditorHumanDrag').SelectSingleNode('Border.Style/Style')
if ($gripStyle.SelectSingleNode('Setter[@Property="Background"]').Value -ne '{Binding MenuGrip}') { throw 'The grip uses its visible theme colour at rest as well as on hover.' }
$slot = Named 'SpectatorControlSlot'
if ($slot.Width -ne '60') { throw 'Both control labels share the slot that fits beside all ten native tabs.' }
foreach ($name in @('SpectatorStopPlacement','SpectatorExpand')) {
    if ($roster.SelectSingleNode('.//*[@x:Name="' + $name + '"]', $ns) -or -not $slot.SelectSingleNode('.//*[@x:Name="' + $name + '"]', $ns)) { throw 'Options and stop share an independent control slot, never an extra roster row.' }
}
$style = $xml.SelectSingleNode('//Style[@x:Key="QuickNumber"]', $ns)
if ($style.SelectSingleNode('Setter[@Property="StrokeThickness"]').Value -ne '1.75' -or $style.SelectSingleNode('Setter[@Property="Stroke"]').Value -ne '#FF0B0806') { throw 'The inner glyph stroke must remain dark and crisp.' }
$style = $xml.SelectSingleNode('//Style[@x:Key="OwnerNumber"]', $ns)
if ($style.SelectSingleNode('Setter[@Property="FontSize"]').Value -ne '18' -or $style.SelectSingleNode('Setter[@Property="LineHeight"]').Value -ne '20') { throw 'Main numbers require explicit enlarged glyph metrics.' }
foreach ($button in (Named 'SpectatorActions').SelectNodes('.//Button')) {
    if ($button.HasAttribute('Height') -or $button.Padding -ne '2,1') { throw 'Navigation height must follow its text and small padding.' }
}
if ((Named 'CurrentOrderText').FontSize -ne '14') { throw 'The current order needs the larger dedicated text row.' }
foreach ($command in @('Attack','Defend','Cancel')) {
    if (-not $xml.SelectSingleNode('//Button[@CommandParameter="'+$command+'"]//Image')) { throw 'Every order action needs its corresponding native icon.' }
}
if ((Named 'ToolsStatus').HasAttribute('Text') -or (Named 'ToolsStatus').SelectSingleNode('TextBlock.ToolTip')) { throw 'Status text and tooltip must have one consistent rich-text writer.' }
$orders = Named 'SpectatorOrdersGrid'
if ($orders.SelectSingleNode('Grid.ColumnDefinitions/ColumnDefinition[1]').Width -ne '352' -or $orders.SelectSingleNode('Grid.RowDefinitions/RowDefinition[1]').Height -ne '18' -or $orders.SelectSingleNode('Grid.RowDefinitions/RowDefinition[2]').Height -ne '48') { throw 'Eight 44-unit target cells fit beside the two-row action grid, within the existing body height.' }
$actions=Named 'SpectatorOrderActions'
if ($actions.SelectNodes('Grid.RowDefinitions/RowDefinition').Count -ne 2) { throw 'Order actions require two rows.' }
foreach ($command in @('Attack','Defend','Cancel')) {
 $button = $actions.SelectSingleNode('Button[@CommandParameter="'+$command+'"]')
 if ($button.Height -ne '24' -or $button.GetAttribute('Grid.Row') -ne $(if($command -eq 'Cancel'){'1'}else{'0'})) { throw 'Order action row/height mismatch.' }
 if ($button.GetAttribute('Grid.Column') -ne $(if($command -eq 'Defend'){'1'}else{'0'}) -or ($command -eq 'Cancel' -and $button.GetAttribute('Grid.ColumnSpan') -ne '2')) { throw 'Attack/defend share the top row; cancel spans the bottom row.' }
}
if ((Named 'ForceAcceptanceSurface').Background -ne '{Binding ForceBackground}' -or (Named 'ForceAcceptanceSurface').GetAttribute('Grid.Column') -ne '1' -or (Named 'ForceAcceptanceToggle').FontSize -ne '12') { throw 'Force acceptance belongs on the bright larger-text top row.' }
if (-not (Named 'SpectatorLeftWing').SelectSingleNode('.//*[@x:Name="OrdersStatus"]',$ns) -or (Named 'OrderAvailability').GetAttribute('Grid.Row') -ne '1' -or (Named 'ToolsStatus').Visibility -ne '{Binding GoodsStatusVisibility}') { throw 'Order feedback and availability use the lower free slot independently of goods feedback.' }
# Structural XAML guardrails only; glyph appearance and actual draw/hit order need ingame acceptance.
Write-Host 'PASS: two-column roster, 36/42 portraits, dual glyph contours, reachable controls, complete form and opaque lower surfaces.'
