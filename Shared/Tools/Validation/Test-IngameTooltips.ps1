[CmdletBinding()]
param([string]$Workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')))
$ErrorActionPreference = 'Stop'
$checked = 0; $disabled = 0; $failures = [Collections.Generic.List[string]]::new()
function Assert-OwnStyle([Xml.XmlElement]$style, [string]$location) {
    $values = @{}
    foreach ($setter in $style.SelectNodes("./*[local-name()='Setter'][@Property and @Value]")) { $values[$setter.GetAttribute('Property')] = $setter.GetAttribute('Value') }
    foreach ($name in @('Background','Foreground','BorderBrush')) {
        if ($style.HasAttribute($name)) { $values[$name] = $style.GetAttribute($name) }
    }
    if ($style.GetAttribute('seui:ToolTipResolutionScale.Enabled') -eq 'True') { $values['seui:ToolTipResolutionScale.Enabled'] = 'True' }
    if ($values['Background'] -ne '#FF1D1710' -or $values['Foreground'] -ne 'White' -or
        $values['BorderBrush'] -ne '#FFF2D48A' -or $values['seui:ToolTipResolutionScale.Enabled'] -ne 'True' -or
        -not $style.SelectSingleNode(".//*[local-name()='ControlTemplate']//*[local-name()='TextBlock'][@TextWrapping='Wrap']")) {
        $failures.Add("Nonstandard mod popup template: $location")
    }
}
foreach ($directory in Get-ChildItem -LiteralPath $Workspace -Directory) {
    if ($directory.Name -match '^\.|^_|^(Shared|Testmods|shcde-script-extender)$') { continue }
    $patches = Join-Path $directory.FullName 'Patches'
    if (-not (Test-Path -LiteralPath $patches)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $patches -Recurse -File -Filter 'HUD_*.xaml') {
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
# Runtime-created HUD buttons store some descriptions in ToolTip but explicitly disable its popup.
$unitHud = Join-Path $Workspace 'APIShared\src\Presentation\UnitHud'
foreach ($part in @('ActionButtons','Categories','Recruitment')) {
    $source = [IO.File]::ReadAllText((Join-Path $unitHud ("UnitHudPresentationService.$part.cs")))
    if ($source -notmatch 'ToolTipService\.SetIsEnabled\([^,]+, false\)') { $failures.Add("Missing runtime popup suppression: $part") }
    if ($part -eq 'ActionButtons' -and $source -cmatch 'ToolTipService\.SetToolTip\(|\.ToolTip\s*=|SetShowDuration\(') { $failures.Add('Action buttons must publish only Vanilla rollover text.') }
}
if ($failures.Count) { throw ($failures -join [Environment]::NewLine) }
Write-Output "PASS: $checked mod-owned ingame popup usages have the modoptions style; $disabled explicitly disabled popups; HUD action/category/recruitment runtime descriptions use Vanilla rollover paths."
