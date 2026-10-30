$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
[xml]$xml = Get-Content -LiteralPath (Join-Path $root 'Patches/Assets/GUI/XAML/IngameUIScreens.xaml') -Raw
$ns = [Xml.XmlNamespaceManager]::new($xml.NameTable)
$ns.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
function Named($name) { $xml.SelectSingleNode('//*[@x:Name="' + $name + '"]', $ns) }
$owners = Named 'SpectatorEditorHumanOwners'
if ($owners.LocalName -ne 'Canvas' -or $owners.Width -ne '76') { throw 'The ordered roster must pack into a two-column canvas.' }
foreach ($slot in 1..8) {
    foreach ($prefix in @('SpectatorEditorHumanOwner','TestTarget')) {
        $card = Named ($prefix + $slot)
        $size = if ($prefix -eq 'TestTarget') { '28' } else { '36' }
        if (-not $card -or $card.Width -ne $size -or $card.Height -ne $size) { throw 'Main portraits are 36 units; order targets retain 28 units.' }
        $numberName = if ($prefix -eq 'TestTarget') { 'TestTargetText' } else { 'SpectatorEditorHumanNumber' }
        $number = Named ($numberName + $slot)
        if ($number.Text -ne [string]$slot -or $number.HorizontalAlignment -ne 'Left' -or $number.VerticalAlignment -ne 'Bottom' -or $number.HasAttribute('Background')) { throw 'Coloured numbers belong inside the lower-left corner without a background.' }
        $outline = $number.PreviousSibling
        if ($outline.LocalName -ne 'TextBlock' -or $outline.Text -ne $number.Text -or $outline.StrokeThickness -ne '2.25' -or $outline.Style -ne $number.Style -or $outline.Margin -ne $number.Margin -or $outline.HasAttribute('Background')) { throw 'The light outer stroke must follow the same glyph and metrics.' }
        if ($prefix -eq 'SpectatorEditorHumanOwner' -and $card.ParentNode -ne $owners) { throw 'The existing buttons retain the common packed roster host.' }
    }
}
$amount = Named 'TestToolsAmount'
if ($amount.Text -ne '0' -or $amount.HasAttribute('MaxLength') -or @($xml.SelectNodes('//TextBox')).Count -ne 1) { throw 'A single untruncated amount input starts at zero.' }
$credit = $xml.SelectSingleNode('//Button[@CommandParameter="Credit"]')
$reset = $xml.SelectSingleNode('//Button[@CommandParameter="ResetAmount"]')
if ($credit.IsEnabled -ne '{Binding CanCredit}' -or $credit.ParentNode -ne $amount.ParentNode -or $reset.ParentNode -ne $amount.ParentNode -or $reset.Content -ne '0') { throw 'Amount, validated send and reset share the form.' }
$goods = @($xml.SelectNodes('//Button[starts-with(@CommandParameter,"G") and @CommandParameter != "Goods"]'))
if ($goods.Count -ne 21) { throw 'Every supported good remains available.' }
foreach ($name in @('SpectatorEditorHumanDrag','SpectatorRosterPanel','SpectatorToolPanel','SpectatorHeader','SpectatorLeftWing','SpectatorRightWing','SpectatorOrdersGrid','SpectatorSnapFrame','SpectatorToolSnapFrame','SpectatorPaperSurface')) {
    if (-not (Named $name)) { throw ('Missing common toolbar part: ' + $name) }
}
if ((Named 'SpectatorEditorHumanPanel').HasAttribute('Background') -or (Named 'SpectatorWings').HasAttribute('Background')) { throw 'Gaps between real painted surfaces must not create an invisible input mask.' }
if ((Named 'SpectatorDetails').SelectNodes('.//ScrollViewer[not(@x:Name="PART_ContentHost")]', $ns).Count -ne 0) { throw 'Tools must fit without a scrolling host.' }
foreach ($name in @('SpectatorSnapFrame','SpectatorToolSnapFrame')) {
    if ((Named $name).IsHitTestVisible -ne 'False') { throw 'Dock guides never intercept input.' }
}
foreach ($name in @('SpectatorLeftWing','SpectatorRightWing')) {
    if ((Named $name).Background -ne '{StaticResource SpectatorPaper}' -or (Named $name).IsHitTestVisible -eq 'False') { throw 'The lower surfaces must paint opaque HUD paper and intercept their own clicks.' }
}
$tools = Named 'SpectatorToolPanel'
if ($tools.Visibility -ne '{Binding ExpandedVisibility}') { throw 'Placement, collapse and occupied HUDs must remove the complete tool surface, including category overdraw.' }
$roster = Named 'SpectatorRosterPanel'
foreach ($name in @('SpectatorStopPlacement','SpectatorExpand','SpectatorEditorHumanDrag')) {
    if (-not $roster.SelectSingleNode('.//*[@x:Name="' + $name + '"]', $ns)) { throw 'Drag, expand and stop controls stay with the left-hand roster.' }
}
$style = $xml.SelectSingleNode('//Style[@x:Key="QuickNumber"]', $ns)
if ($style.SelectSingleNode('Setter[@Property="StrokeThickness"]').Value -ne '1.25' -or $style.SelectSingleNode('Setter[@Property="Stroke"]').Value -ne '#FF0B0806') { throw 'The inner glyph stroke must remain dark and crisp.' }
$style = $xml.SelectSingleNode('//Style[@x:Key="OwnerNumber"]', $ns)
if ($style.SelectSingleNode('Setter[@Property="FontSize"]').Value -ne '18' -or $style.SelectSingleNode('Setter[@Property="LineHeight"]').Value -ne '20') { throw 'Main numbers require explicit enlarged glyph metrics.' }
# Structural XAML guardrails only; glyph appearance and actual draw/hit order need ingame acceptance.
Write-Host 'PASS: two-column roster, 36/28 portraits, dual glyph contours, reachable controls, complete form and opaque lower surfaces.'
