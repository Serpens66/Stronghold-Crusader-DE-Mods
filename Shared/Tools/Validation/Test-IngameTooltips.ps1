[CmdletBinding()]
param([string]$Workspace, [string[]]$ModDirectories)
$ErrorActionPreference = 'Stop'
# Windows PowerShell 5.1 leaves $PSScriptRoot empty inside parameter defaults.
if (-not $Workspace) { $Workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')) }
$checked = 0; $disabled = 0; $runtimeChecked = 0; $failures = [Collections.Generic.List[string]]::new()

function Get-StylePropertyName([Xml.XmlElement]$node, [string]$property) {
    if ($property -match '^([^:]+):(.+)$') {
        $local = $Matches[2]
        switch -Regex ($node.GetNamespaceOfPrefix($Matches[1])) {
            '^clr-namespace:APIShared\.ModSettings(;|$)' { return "apiui:$local" }
            '^clr-namespace:SHCDESE\.UI(;|$)' { return "seui:$local" }
            '^clr-namespace:Spectator(;|$)' { return "specui:$local" }
        }
    }
    return $property
}

function Find-ResourceStyle([Xml.XmlElement]$node, [string]$key) {
    # A style in another patch operation or sibling visual subtree is not in scope.
    for ($scope = $node; $scope -is [Xml.XmlElement]; $scope = $scope.ParentNode) {
        $styles = $scope.SelectNodes("./*[substring(local-name(), string-length(local-name()) - 9) = '.Resources']/*[local-name()='Style' or local-name()='ResourceDictionary']")
        if ($scope.LocalName -eq 'ResourceDictionary' -or $scope.LocalName.EndsWith('.Resources')) {
            $styles = $scope.SelectNodes("./*[local-name()='Style' or local-name()='ResourceDictionary']")
        }
        foreach ($resource in $styles) {
            $candidates = if ($resource.LocalName -eq 'Style') { @($resource) } else { $resource.SelectNodes("./*[local-name()='Style']") }
            foreach ($style in $candidates) {
                $styleKey = $style.GetAttribute('Key', 'http://schemas.microsoft.com/winfx/2006/xaml')
                if ($key -and $styleKey -ceq $key) { return $style }
                if (!$key -and !$styleKey -and $style.GetAttribute('TargetType') -ceq '{x:Type ToolTip}') { return $style }
            }
        }
    }
}

function Assert-OwnStyle([Xml.XmlElement]$style, [string]$location, [switch]$MenuPixels, [switch]$SharedSizingInCode) {
    $values = @{}
    $chain = @($style)
    $base = $style.GetAttribute('BasedOn')
    if ($style.LocalName -eq 'ToolTip') {
        $explicitStyle = $style.SelectSingleNode("./*[local-name()='ToolTip.Style']/*[local-name()='Style']")
        if (!$explicitStyle -and $style.GetAttribute('Style') -match '^\{StaticResource ([^}]+)\}$') {
            $explicitStyle = Find-ResourceStyle $style $Matches[1]
        }
        if (!$explicitStyle) {
            $failures.Add("Popup needs an explicit local Style to avoid Vanilla defaults: $location")
            return
        }
        $chain = @($explicitStyle, $style)
        $base = $explicitStyle.GetAttribute('BasedOn')
    }
    while ($base) {
        $parentStyle = if ($base -match '^\{StaticResource ([^}]+)\}$') { Find-ResourceStyle $chain[0] $Matches[1] }
        if (!$parentStyle -or $chain -contains $parentStyle) {
            $failures.Add("Cannot resolve local popup style inheritance: $location")
            return
        }
        $chain = @($parentStyle) + $chain
        $base = $parentStyle.GetAttribute('BasedOn')
    }
    $template = $null
    foreach ($part in $chain) {
        foreach ($setter in $part.SelectNodes("./*[local-name()='Setter'][@Property and @Value]")) {
            $values[(Get-StylePropertyName $setter $setter.GetAttribute('Property'))] = $setter.GetAttribute('Value')
        }
        $declared = $part.SelectSingleNode("./*[local-name()='ToolTip.Template']/*[local-name()='ControlTemplate'] | ./*[local-name()='Setter'][@Property='Template']/*[local-name()='Setter.Value']/*[local-name()='ControlTemplate']")
        if ($declared -and (!$menuRelative -or $declared.SelectSingleNode(".//*[local-name()='TextBlock'][@TextWrapping='Wrap']"))) {
            $template = $declared
        }
    }
    foreach ($attribute in $style.Attributes) {
        $values[(Get-StylePropertyName $style $attribute.Name)] = $attribute.Value
    }
    # Spectator's toolbar supplies a source reference through its lifecycle adapter,
    # while the shared service remains the only sizing owner for all its popups.
    $menuSizing = $menuRelative -and $values['specui:MenuToolTipSizing.Enabled'] -eq 'True' -and $values['seui:ToolTipResolutionScale.Enabled'] -eq 'False' -and
        $values['apiui:SharedToolTipSizing.Enabled'] -eq 'True' -and $values['FontSize'] -eq '15' -and $values['MaxWidth'] -eq '420'
    $sharedSizing = ($SharedSizingInCode -or $values['apiui:SharedToolTipSizing.Enabled'] -eq 'True') -and $values['seui:ToolTipResolutionScale.Enabled'] -ne 'True'
    # The ESC renderer owns its popup/button pixel ratio directly; behavior tests execute
    # that production helper. This style check only guards its fallback and exclusive opt-in.
    $escSizing = $MenuPixels -and $values['seui:ToolTipResolutionScale.Enabled'] -eq 'False' -and
        $values['apiui:SharedToolTipSizing.Enabled'] -ne 'True' -and $values['FontSize'] -eq '16' -and $values['MaxWidth'] -eq '420'
    $validSizing = if ($MenuPixels) { $escSizing } elseif ($menuRelative) { $menuSizing } elseif (!$apiOwned) { $sharedSizing } else {
        $values['seui:ToolTipResolutionScale.Enabled'] -eq 'True' -or $sharedSizing
    }
    $wrap = $template -and $template.SelectSingleNode(".//*[local-name()='TextBlock'][@TextWrapping='Wrap']")
    $layout = $apiOwned -or $menuRelative -or ($template -and $template.SelectSingleNode(
        "./*[local-name()='Border'][@Padding='{TemplateBinding Padding}' and @BorderThickness='{TemplateBinding BorderThickness}' and @MaxWidth='{TemplateBinding MaxWidth}']") -and
        $template.SelectSingleNode(".//*[local-name()='TextBlock'][@FontSize='{TemplateBinding FontSize}']"))
    if ($values['Background'] -ne '#FF1D1710' -or $values['Foreground'] -ne 'White' -or
        $values['BorderBrush'] -ne '#FFF2D48A' -or !$wrap -or !$layout -or !$validSizing) {
        $failures.Add("Nonstandard mod popup template: $location")
    }
}

function Assert-RuntimePopupFactory {
    $path = Join-Path $Workspace 'Shared/Adapters/APIShared/MenuToolTip.cs'
    $source = [IO.File]::ReadAllText($path)
    # Check the XAML used by the production factory rather than maintaining a second style fixture.
    $definition = [regex]::Match($source, '(?s)\b(?<style>\w+)\s*=\s*\(Style\)\s*GUI\.ParseXaml\(\s*@"(?<xaml><Style\b.*?</Style>)"\s*\)')
    $styleName = $definition.Groups['style'].Value
    $popup = [regex]::Match($source, "\b(?:var|ToolTip)\s+(?<tip>\w+)\s*=\s*new\s+ToolTip\s*\{[^{}]*?\bStyle\s*=\s*$styleName\b")
    $tipName = $popup.Groups['tip'].Value
    if (!$definition.Success -or !$popup.Success -or
        $source -cnotmatch "\bSharedToolTipSizing\.SetEnabled\(\s*$tipName\s*,\s*true\s*\)" -or
        $source -cnotmatch "\breturn\s+$tipName\s*;" -or
        $source -cnotmatch '\bconst\s+int\s+Duration\s*=\s*60000\s*;') {
        $failures.Add("Runtime popup factory needs an explicit Style and the shared sizing/duration contract: $path")
        return
    }
    [xml]$style = $definition.Groups['xaml'].Value.Replace('""', '"')
    $menuRelative = $false
    $apiOwned = $false
    Assert-OwnStyle $style.DocumentElement $path -SharedSizingInCode
}

function Get-MenuXamlFiles([IO.DirectoryInfo]$directory) {
    $plugins = Join-Path $directory.FullName 'BepInEx/plugins'
    foreach ($folder in 'Patches','Override') {
        $source = Join-Path $directory.FullName $folder
        if (Test-Path -LiteralPath $source -PathType Container) {
            Get-ChildItem -LiteralPath $source -Recurse -File -Filter '*.xaml'
        }
        if (!$apiOwned -and (Test-Path -LiteralPath $plugins -PathType Container)) {
            # A mod can author some assets in the package and others in the source tree.
            foreach ($plugin in Get-ChildItem -LiteralPath $plugins -Directory) {
                $path = Join-Path $plugin.FullName $folder
                if ((Test-Path -LiteralPath (Join-Path $plugin.FullName 'info.json')) -and
                    (Test-Path -LiteralPath $path -PathType Container)) {
                    foreach ($file in Get-ChildItem -LiteralPath $path -Recurse -File -Filter '*.xaml') {
                        $relative = $file.FullName.Substring($path.Length + 1)
                        if (!(Test-Path -LiteralPath (Join-Path $source $relative) -PathType Leaf)) { $file }
                    }
                }
            }
        }
    }
}

function Test-RuntimePopups([IO.DirectoryInfo]$directory) {
    $sourceRoot = Join-Path $directory.FullName 'src'
    if (!(Test-Path -LiteralPath $sourceRoot -PathType Container)) { return 0 }
    $owners = '(?:Noesis\.)?(?:UIElement|FrameworkElement|Control|ContentControl|Button|ToggleButton|RadioButton|CheckBox|ComboBox|ComboBoxItem|Slider|TextBox|TextBlock|Border|Grid|ListBoxItem)'
    $factory = '(?:Shared\.)?MenuToolTip\.Create\s*\('
    $count = 0
    foreach ($file in Get-ChildItem -LiteralPath $sourceRoot -Recurse -File -Filter '*.cs') {
        $source = [IO.File]::ReadAllText($file.FullName)
        if ($source -cnotmatch '\bToolTip\s*=|ToolTipService\.SetToolTip\s*\(') { continue }
        # Limited source guardrails for Noesis assignments. ViewModel text properties
        # intentionally remain legal and flow through the XAML checks above.
        $assignments = @([regex]::Matches($source, '\bToolTipService\.SetToolTip\s*\([^,]+,\s*(?<value>\S[^;]*)'))
        $initializer = '(?:[^{}]|\{(?<nested>)|\}(?<-nested>))*?(?(nested)(?!))'
        $assignments += @([regex]::Matches($source, "(?s)\bnew\s+$owners\b(?:\s*\(\s*\))?\s*\{$initializer\bToolTip\s*=\s*(?<value>\S[^,}]*)"))
        foreach ($assignment in [regex]::Matches($source, '\b(?<owner>\w+)\.ToolTip\s*=\s*(?<value>\S[^;]*)')) {
            $owner = $assignment.Groups['owner'].Value
            if ($source -cmatch "\b(?:$owners\s+$owner\b|var\s+$owner\s*=\s*new\s+$owners\b)") { $assignments += $assignment }
        }
        foreach ($assignment in $assignments) {
            $value = $assignment.Groups['value'].Value.Trim()
            if ($value -cmatch "^$factory|^null(?:\)|$)") { $count++; continue }
            if ($value -cmatch '^(?<local>\w+)\s*(?:\)|$)') {
                $local = $Matches['local']
                $creation = "\b(?:var|ToolTip)\s+$local\s*=\s*(?:\w+\s*\?\?\s*)?"
                $explicitStyle = $source -cmatch "$creation(?:new\s+ToolTip\s*\{\s*Style\s*=\s*(?!null\b))" -and
                    $source -cmatch "(?:SharedToolTipSizing|MenuToolTipSizing)\.SetEnabled\(\s*$local\s*,\s*true\s*\)"
                if ($source -cmatch "$creation$factory" -or $explicitStyle -or
                    $source -cmatch "\b$local\s*=\s*\w+\.ToolTip\s*;") { $count++; continue }
            }
            # The Lord Overview factory has its own template and source-relative sizing.
            if ($directory.Name -eq 'Spectator' -and $value -cmatch '^CreateTip\s*\(') {
                $factorySource = [IO.File]::ReadAllText((Join-Path $file.DirectoryName 'OverviewExtensions.cs'))
                if ($factorySource -cmatch 'ToolTip\s+CreateTip\s*\(' -and
                    $factorySource -cmatch 'Style\s*=\s*tipStyle' -and
                    $factorySource -cmatch 'SharedToolTipSizing\.SetEnabled\(tip,\s*true\)') { $count++; continue }
            }
            $line = [regex]::Matches($source.Substring(0, $assignment.Index), '\n').Count + 1
            $failures.Add("Unproven runtime popup styling: $($file.FullName):$line. Use the mod menu popup factory or an explicitly styled ToolTip.")
        }
    }
    return $count
}

$directories = if ($ModDirectories) { @($ModDirectories | ForEach-Object { Get-Item -LiteralPath $_ }) } else {
    @(Get-ChildItem -LiteralPath $Workspace -Directory)
}
foreach ($directory in $directories) {
    if ($directory.Name -match '^\.|^_|^(Shared|Testmods|shcde-script-extender)$') { continue }
    $apiOwned = $directory.Name -eq 'APIShared'
    $popupFiles = @(Get-MenuXamlFiles $directory)
    if (!$apiOwned) { $runtimeChecked += Test-RuntimePopups $directory }
    if ($directory.Name -eq 'Spectator') {
        $runtime = [IO.File]::ReadAllText((Join-Path $directory.FullName 'src/SpectatorRuntime.Hud.cs'))
        if ($runtime -notmatch 'new ToolTip \{ Style = style, Content = (?:text|content) \}' -or $runtime -notmatch 'SetShowDuration\(button, 60000\)') { $failures.Add('Player tooltips must use the checked own style and duration.') }
    }
    foreach ($file in $popupFiles) {
        $menuRelative = $directory.Name -eq 'Spectator' -and $file.FullName -in @(
            (Join-Path $directory.FullName 'Patches\Assets\GUI\XAML\IngameUIScreens.xaml'),
            (Join-Path $directory.FullName 'Patches\Assets\GUI\XAMLResources\HUD_Main.xaml'),
            (Join-Path $directory.FullName 'Patches\Assets\GUI\XAMLResources\HUD_AlliesPanel.xaml'))
        [xml]$document = [IO.File]::ReadAllText($file.FullName)
        foreach ($node in $document.SelectNodes('//*[@ToolTip or @ToolTipService.ToolTip]')) {
            if ($node.GetAttribute('ToolTipService.IsEnabled') -eq 'False' -or
                $node.GetAttribute('ToolTip') -eq '{x:Null}' -or $node.GetAttribute('ToolTipService.ToolTip') -eq '{x:Null}') { $disabled++; continue }
            $style = Find-ResourceStyle $node ''
            if (-not $style) { $failures.Add("Popup falls back to Vanilla lion style: $($file.FullName) [$($node.LocalName)]"); continue }
            Assert-OwnStyle $style $file.FullName; $checked++
            if ($node.GetAttribute('ToolTipService.ShowDuration') -ne '60000') { $failures.Add("Popup duration is not 60000: $($file.FullName)") }
        }
        foreach ($tooltip in $document.SelectNodes("//*[local-name()='ToolTip']")) {
            $property = $tooltip.ParentNode
            if ($property.LocalName.EndsWith('.ToolTip')) {
                $owner = $property.ParentNode
                if ($owner.GetAttribute('ToolTipService.IsEnabled') -eq 'False') { $disabled++; continue }
                if ($owner.GetAttribute('ToolTipService.ShowDuration') -ne '60000') {
                    $failures.Add("Popup duration is not 60000: $($file.FullName) [$($owner.LocalName)]")
                }
            }
            Assert-OwnStyle $tooltip $file.FullName; $checked++
        }
        foreach ($property in $document.SelectNodes("//*[substring(local-name(), string-length(local-name()) - 7) = '.ToolTip'][not(*[local-name()='ToolTip'])]")) {
            if ($property.ParentNode.GetAttribute('ToolTipService.IsEnabled') -eq 'False' -or
                $property.SelectSingleNode("./*[local-name()='Null']")) { $disabled++; continue }
            $failures.Add("Popup property needs an explicitly styled ToolTip: $($file.FullName) [$($property.LocalName)]")
        }
        foreach ($property in $document.SelectNodes("//*[@AttributeName='ToolTip' or @AttributeName='ToolTipService.ToolTip' or @Property='ToolTip' or @Property='ToolTipService.ToolTip']")) {
            if ($property.GetAttribute('Value') -eq '{x:Null}') { $disabled++; continue }
            if (!$property.SelectSingleNode("./*[local-name()='Setter.Value']/*[local-name()='ToolTip']")) {
                $failures.Add("Popup setter needs an explicitly styled ToolTip: $($file.FullName) [$($property.LocalName)]")
            }
        }
    }
}
if ($runtimeChecked -gt 0) { Assert-RuntimePopupFactory }
$menuRelative = $false; $apiOwned = $true
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
if ($failures.Count) { throw (($failures | Select-Object -Unique) -join [Environment]::NewLine) }
Write-Output "PASS: $checked XAML popups, $runtimeChecked runtime assignments, $disabled explicitly disabled popups; APIShared implementation guardrails=$checkApi (source checks, not an in-game rendering proof)."
