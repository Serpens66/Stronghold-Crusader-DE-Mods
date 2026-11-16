$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$files = @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object {
    $_.Extension -in '.cs','.csproj','.xaml','.json','.ps1','.bat','.md' -and $_.FullName -notmatch '[\\/](BepInEx|bin|obj)[\\/]'
})
foreach ($file in $files) {
    if ([IO.File]::ReadAllText($file.FullName) -match '(?<!\r)\n|\r(?!\n)') { throw "Non-CRLF: $($file.FullName)" }
}
$path = Join-Path $PSScriptRoot 'Override\ScriptExtenderUI\AIHunterPreyTestSettings.xaml'
[xml]$xaml = [IO.File]::ReadAllText($path)
foreach ($control in $xaml.SelectNodes("//*[local-name()='Button' or local-name()='CheckBox' or local-name()='ComboBox' or local-name()='TextBox' or local-name()='Slider']")) {
    if ($control.GetAttribute('ToolTipService.ShowDuration') -cne '60000' -or
        !$control.SelectSingleNode("./*[substring(local-name(), string-length(local-name()) - 7) = '.ToolTip']/*[local-name()='ToolTip']")) {
        throw "Missing explicit tooltip: $($control.LocalName)"
    }
}
Write-Host 'PASS: scoped source CRLF, XML and interactive tooltips. In-game appearance still needs manual testing.'
