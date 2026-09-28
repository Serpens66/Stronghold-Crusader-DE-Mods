[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$native = Join-Path $game 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$stream = [IO.File]::OpenRead($native)
$sha = [Security.Cryptography.SHA256]::Create()
try {
    $nativeHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '')
}
finally {
    $sha.Dispose()
    $stream.Dispose()
}
if ($nativeHash -cne 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2') {
    throw 'Health-bar native baseline differs from the installed library.'
}
$extender = Join-Path $game 'BepInEx\plugins\000shcdese'
if ([Reflection.AssemblyName]::GetAssemblyName((Join-Path $extender 'RedBird.X64.dll')).Version.ToString() -ne '1.5.0.0') {
    throw 'Health-bar RedBird backend differs from the audited version.'
}

$runtime = [IO.File]::ReadAllText((Join-Path $root 'src\DamagedHealthBarsRuntime.cs'))
$contract = [IO.File]::ReadAllText((Join-Path $root 'src\HealthBarNativeContract.cs'))
$settings = [IO.File]::ReadAllText((Join-Path $root 'src\BugfixesAndQoLViewModel.cs'))
$xaml = [IO.File]::ReadAllText((Join-Path $root 'Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml'))
if ($runtime -notmatch 'InputR3EventHooks\.OnKeyDown' -or
    $runtime -notmatch 'HEALTH_BARS_POST_STARTUP' -or
    $runtime -notmatch 'DisplacedByteCount' -or
    $runtime -notmatch 'controller\.NoesisHasKeyboard' -or
    $runtime -notmatch 'viewModel\.IsMapEditorMode' -or
    $runtime -notmatch 'settings\.EnableDamagedHealthBars' -or
    $runtime -match 'HEALTH_BARS_HOTKEY_REJECTED|args\.Result\s*=|transaction\.Dispose\s*\(') {
    throw 'Health-bar runtime lifecycle, input or logging contract failed.'
}
if ($contract -notmatch 'UnitHealthBarBlocks\], 10' -or
    $contract -notmatch 'assembler\.cmp\(ax, 48\)' -or
    $contract -notmatch 'assembler\.cmp\(ax, 49\)' -or
    $contract -notmatch 'assembler\.cmp\(ax, 54\)' -or
    $contract -notmatch 'assembler\.cmp\(ax, 62\)' -or
    $contract -notmatch 'assembler\.cmp\(ax, 64\)' -or
    $contract -notmatch 'assembler\.cmp\(ax, 68\)' -or
    $contract -notmatch 'assembler\.cmp\(ax, 69\)') {
    throw 'Health-bar full-bar or decorative-unit filter differs from the audited contract.'
}
if ($settings -notmatch '\[Shared\.PresetLocal\]\s+public bool EnableDamagedHealthBars' -or
    $settings -notmatch '\[Shared\.PresetLocal\]\s+public int HealthBarHotkey' -or
    $settings -notmatch 'DefaultHealthBarHotkey = \(int\)KeyCode\.H \| HealthBarAltMask' -or
    $settings -match 'HealthBarHotkeyKeyIndex|private bool healthBarHotkeyAlt') {
    throw 'Health-bar local setting or default hotkey contract failed.'
}
if ($xaml -notmatch 'IsChecked="\{Binding EnableDamagedHealthBars, Mode=TwoWay\}"' -or
    $xaml -notmatch 'Command="\{Binding HealthBarHotkeyInputCommand\}"') {
    throw 'Health-bar settings UI is incomplete.'
}

$files = @(
    (Join-Path $root 'src\BugfixesAndQoLViewModel.cs'),
    (Join-Path $root 'src\DamagedHealthBarsRuntime.cs'),
    (Join-Path $root 'src\HealthBarNativeContract.cs'),
    (Join-Path $root 'Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml')
) + @(Get-ChildItem -LiteralPath (Join-Path $root 'Locales') -Filter '*.txt' -File | ForEach-Object FullName)
foreach ($path in $files) {
    $content = [IO.File]::ReadAllText($path)
    $literalNewlineEscape = ([string][char]92) + 'r' + ([string][char]92) + 'n'
    if ($content -match '(?<!\r)\n' -or $content.Contains($literalNewlineEscape)) {
        throw "Health-bar source CRLF check failed: $path"
    }
}

Write-Output 'Health-bar baseline, runtime, local settings, XAML and CRLF preflight passed.'
