[CmdletBinding()]
param([string]$Workspace, [string[]]$ModDirectories)
$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 leaves $PSScriptRoot empty inside parameter defaults.
if (-not $Workspace) { $Workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')) }
$checked = 0; $disabled = 0; $failures = [Collections.Generic.List[string]]::new()
function Assert-OwnStyle([Xml.XmlElement]$style, [string]$location, [switch]$MenuPixels) {
    $values = @{}
    $chain = @($style)
    $base = $style.GetAttribute('BasedOn')
    if ($style.LocalName -eq 'ToolTip') { $base = $style.GetAttribute('Style') }
    while ($base -match '^\{StaticResource ([^}]+)\}$') {
        $parentStyle = $style.OwnerDocument.SelectSingleNode("//*[local-name()='Style'][@*[local-name()='Key']='" + $Matches[1] + "']")
        if (-not $parentStyle -or $chain -contains $parentStyle) { break }
        $chain = @($parentStyle) + $chain
        $base = $parentStyle.GetAttribute('BasedOn')
    }
    $template = $null
    foreach ($part in $chain) {
        foreach ($setter in $part.SelectNodes("./*[local-name()='Setter'][@Property and @Value]")) { $values[$setter.GetAttribute('Property')] = $setter.GetAttribute('Value') }
        if ($part.SelectSingleNode(".//*[local-name()='ControlTemplate']//*[local-name()='TextBlock'][@TextWrapping='Wrap']")) { $template = $part }
    }
    foreach ($setter in $style.SelectNodes("./*[local-name()='Setter'][@Property and @Value]")) { $values[$setter.GetAttribute('Property')] = $setter.GetAttribute('Value') }
    foreach ($name in @('Background','Foreground','BorderBrush')) {
        if ($style.HasAttribute($name)) { $values[$name] = $style.GetAttribute($name) }
    }
    if ($style.GetAttribute('seui:ToolTipResolutionScale.Enabled') -eq 'True') { $values['seui:ToolTipResolutionScale.Enabled'] = 'True' }
    # Spectator's in-game toolbar deliberately matches its rendered menu font. Other
    # owned popups retain the established resolution scaler; colors/template remain common.
    $menuSizing = $menuRelative -and $values['specui:MenuToolTipSizing.Enabled'] -eq 'True' -and $values['seui:ToolTipResolutionScale.Enabled'] -eq 'False' -and
        $values['apiui:SharedToolTipSizing.Enabled'] -ne 'True' -and $values['FontSize'] -eq '14' -and $values['MaxWidth'] -eq '420'
    $sharedSizing = $values['apiui:SharedToolTipSizing.Enabled'] -eq 'True' -and $values['seui:ToolTipResolutionScale.Enabled'] -ne 'True'
    # The ESC renderer owns its popup/button pixel ratio directly; behavior tests execute
    # that production helper. This style check only guards its fallback and exclusive opt-in.
    $escSizing = $MenuPixels -and $values['seui:ToolTipResolutionScale.Enabled'] -eq 'False' -and
        $values['apiui:SharedToolTipSizing.Enabled'] -ne 'True' -and $values['FontSize'] -eq '16' -and $values['MaxWidth'] -eq '420'
    $validSizing = if ($MenuPixels) { $escSizing } elseif ($menuRelative) { $menuSizing } elseif ($requireShared) { $sharedSizing } else {
        $values['seui:ToolTipResolutionScale.Enabled'] -eq 'True' -or $sharedSizing
    }
    if ($values['Background'] -ne '#FF1D1710' -or $values['Foreground'] -ne 'White' -or
        $values['BorderBrush'] -ne '#FFF2D48A' -or -not $template -or -not $validSizing) {
        $failures.Add("Nonstandard mod popup template: $location")
    }
}
$directories = if ($ModDirectories) { @($ModDirectories | ForEach-Object { Get-Item -LiteralPath $_ }) } else {
    @(Get-ChildItem -LiteralPath $Workspace -Directory)
}
foreach ($directory in $directories) {
    if ($directory.Name -match '^\.|^_|^(Shared|Testmods|shcde-script-extender)$') { continue }
    $patches = Join-Path $directory.FullName 'Patches'
    if (-not (Test-Path -LiteralPath $patches)) { continue }
    $requireShared = $directory.Name -eq 'Spectator'
    $popupFiles = @(Get-ChildItem -LiteralPath $patches -Recurse -File -Filter 'HUD_*.xaml')
    if ($requireShared) {
        $popupFiles = @(Get-ChildItem -LiteralPath $patches,(Join-Path $directory.FullName 'Override') -Recurse -File -Filter '*.xaml')
        $runtime = [IO.File]::ReadAllText((Join-Path $directory.FullName 'src/SpectatorRuntime.Hud.cs'))
        if ($runtime -notmatch 'new ToolTip \{ Style = style, Content = (?:text|content) \}' -or $runtime -notmatch 'SetShowDuration\(button, 60000\)') { $failures.Add('Player tooltips must use the checked own style and duration.') }
    }
    foreach ($file in $popupFiles) {
        $menuRelative = $requireShared -and $file.FullName -in @(
            (Join-Path $directory.FullName 'Patches\Assets\GUI\XAML\IngameUIScreens.xaml'),
            (Join-Path $directory.FullName 'Patches\Assets\GUI\XAMLResources\HUD_Main.xaml'))
        [xml]$document = [IO.File]::ReadAllText($file.FullName)
        foreach ($node in $document.SelectNodes('//*[@ToolTip]')) {
            if ($node.GetAttribute('ToolTipService.IsEnabled') -eq 'False') { $disabled++; continue }
            $style = $null; $parent = $node
            while ($parent -is [Xml.XmlElement]) {
                $style = $parent.SelectSingleNode("./*[substring(local-name(), string-length(local-name()) - 9) = '.Resources']/*[local-name()='Style'][@TargetType='{x:Type ToolTip}']")
                if ($style) { break }
                $parent = $parent.ParentNode
            }
            if (-not $style) { $failures.Add("Popup falls back to Vanilla lion style: $($file.FullName) [$($node.LocalName)]"); continue }
            Assert-OwnStyle $style $file.FullName; $checked++
            if ($node.GetAttribute('ToolTipService.ShowDuration') -ne '60000') { $failures.Add("Popup duration is not 60000: $($file.FullName)") }
        }
        foreach ($tooltip in $document.SelectNodes("//*[local-name()='ToolTip']")) {
            Assert-OwnStyle $tooltip $file.FullName; $checked++
        }
    }
}
$menuRelative = $false; $requireShared = $false
# Provider implementation guardrails run only when APIShared itself is selected.
# A DLL dependency does not make its unrelated HUD implementation a consumer check.
$apiRoot = [IO.Path]::GetFullPath((Join-Path $Workspace 'APIShared')).TrimEnd('\','/')
$checkApi = -not $ModDirectories -or @($directories | Where-Object { $_.FullName.TrimEnd('\','/') -ieq $apiRoot }).Count -gt 0
if ($checkApi) {
# Runtime-created HUD buttons store some descriptions in ToolTip but explicitly disable its popup.
$unitHud = Join-Path $Workspace 'APIShared\src\Presentation\UnitHud'
foreach ($part in @('ActionButtons','Categories','Recruitment')) {
    $source = [IO.File]::ReadAllText((Join-Path $unitHud ("UnitHudPresentationService.$part.cs")))
    if ($source -notmatch 'ToolTipService\.SetIsEnabled\([^,]+, false\)') { $failures.Add("Missing runtime popup suppression: $part") }
    if ($part -eq 'ActionButtons' -and $source -cmatch 'ToolTipService\.SetToolTip\(|\.ToolTip\s*=|SetShowDuration\(') { $failures.Add('Action buttons must publish only Vanilla rollover text.') }
}
# Side-HUD buttons deliberately use popups; their default must never fall back to the lion template.
$extrasPatch = Join-Path $Workspace 'APIShared\Patches\Assets\GUI\XAML\IngameUIScreens.xaml'
[xml]$extrasDocument = [IO.File]::ReadAllText($extrasPatch)
$extrasStyle = $extrasDocument.SelectSingleNode("//*[local-name()='Style'][@*[local-name()='Key']='APISharedHudExtrasTooltipStyle']")
if (-not $extrasStyle) { $failures.Add('Missing side-HUD default tooltip style.') }
else { Assert-OwnStyle $extrasStyle $extrasPatch; $checked++ }
$escPatch = Join-Path $apiRoot 'Patches/Assets/GUI/XAMLResources/HUD_IngameMenu.xaml'
[xml]$escDocument = [IO.File]::ReadAllText($escPatch)
$escStyle = $escDocument.SelectSingleNode("//*[local-name()='Style'][@*[local-name()='Key']='APISharedMenuTooltipStyle']")
if (-not $escStyle) { $failures.Add('Missing ESC tooltip style.') }
else { Assert-OwnStyle $escStyle $escPatch -MenuPixels; $checked++ }
$extrasSource = [IO.File]::ReadAllText((Join-Path $Workspace 'APIShared\src\Presentation\HudExtras\HudExtrasButtonsService.cs'))
if ($extrasSource -notmatch 'RequireStyle\("APISharedHudExtrasTooltipStyle"\)' -or
    $extrasSource -notmatch 'SetShowDuration\(visual.Button, 60000\)' -or
    $extrasSource -notmatch 'tip.Style == null && tip.Template == null') {
    $failures.Add('Side-HUD popup path must use the own default style or an explicitly styled custom tooltip.')
}
}
if ($failures.Count) { throw ($failures -join [Environment]::NewLine) }
Write-Output "PASS: $checked scoped mod-owned popup usages have the modoptions style; $disabled explicitly disabled popups; APIShared implementation guardrails=$checkApi (source checks, not an in-game rendering proof)."
