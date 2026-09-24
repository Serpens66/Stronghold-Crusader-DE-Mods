param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$workspace = [IO.Path]::GetFullPath((Join-Path $root '..'))
$nativePath = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$current = Get-Content -Raw -LiteralPath (Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\CURRENT.json') | ConvertFrom-Json
$sha = [Security.Cryptography.SHA256]::Create()
$stream = [IO.File]::OpenRead($nativePath)
try { $nativeHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
finally { $stream.Dispose(); $sha.Dispose() }
if ($nativeHash -cne $current.currentNativeHash) { throw 'Tannery native hash differs from CURRENT.json.' }
$decompiledPath = Join-Path $workspace ('_inspect\CrusaderDE-Native-Baseline\' +
    $current.semanticDirectory + '\exports\semantic-decompiled-functions.c')
$decompiled = [IO.File]::ReadAllText($decompiledPath)
$managerMatch = [regex]::Match($decompiled, 'FUN_1800c60f0\(&DAT_([0-9a-f]+)\)', 'IgnoreCase')
if (-not $managerMatch.Success -or
    [Convert]::ToInt64($managerMatch.Groups[1].Value, 16) -ne
    [Convert]::ToInt64('1864CCBB0', 16)) {
    throw 'Tannery building manager base differs from the native baseline.'
}
$updaterStart = $decompiled.IndexOf('/* FUNCTION FUN_1800b10d0', [StringComparison]::Ordinal)
$updaterEnd = $decompiled.IndexOf('/* FUNCTION ', $updaterStart + 12, [StringComparison]::Ordinal)
if ($updaterStart -lt 0 -or $updaterEnd -lt 0) {
    throw 'Tannery updater is absent from the native baseline.'
}
$updater = $decompiled.Substring($updaterStart, $updaterEnd - $updaterStart)
foreach ($address in @('1864ccc28', '1864cccca', '1864ccd24')) {
    if (-not $updater.Contains('DAT_' + $address)) {
        throw "Tannery updater field $address differs from the native baseline."
    }
}

$source = [IO.File]::ReadAllText((Join-Path $root 'src\NativeTannerFade.cs'))
$state = [IO.File]::ReadAllText((Join-Path $root 'src\NativeTannerFadeState.cs'))
$runtime = [IO.File]::ReadAllText((Join-Path $root 'src\BugfixesAndQoLRuntime.cs'))
$view = [IO.File]::ReadAllText((Join-Path $root 'src\BugfixesAndQoLViewModel.cs'))
$xaml = [IO.File]::ReadAllText((Join-Path $root 'Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml'))
[xml]$xamlDocument = $xaml
foreach ($contract in @(
    'TannerUpdaterRva = 0xB10D0', 'CurrentBuildingIdRva = 0x8F9BE4',
    'BuildingArrayRva = 0x64CCBB0', 'BuildingStride = 0x32C',
    'ImageOffset = 0x78', 'AlphaOffset = 0x11A', 'AliveStateOffset = 0x12C',
    'BuildingTypeOffset = 0x12E', 'PhaseOffset = 0x174',
    'eStructs.STRUCT_TANNERS_WORKSHOP', 'scheme != "Indirect"',
    'detour.DisplacedByteCount != 7', 'ShouldBridgeExitFrame(restoredVanilla',
    'Volatile.Read(ref enabled) == 0')) {
    if (-not $source.Contains($contract)) { throw "Missing tannery native contract: $contract" }
}
if ($source.Contains('GameTimeManagerAPI.Instance.OnTick') -or
    $source.Contains('PacketWriterRva') -or
    $source -match '\.(Undo|Disable|Apply)\s*\(' -or
    -not $state.Contains('FadeUpdates = 64') -or
    -not $runtime.Contains('private static NativeTannerFade processNativeTannerFade;') -or
    -not [regex]::IsMatch($view, '\[SyncHostOnly\]\s+public bool EnableTanneryAnimationFix') -or
    -not $xaml.Contains('IsChecked="{Binding EnableTanneryAnimationFix, Mode=TwoWay}"') -or
    -not $xaml.Contains('ToolTip="{Binding EnableTanneryAnimationFixHelpText}"')) {
    throw 'Tannery lifecycle, settings or hook contract failed.'
}

$bytes = [IO.File]::ReadAllBytes($nativePath)
$peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
$sectionCount = [BitConverter]::ToUInt16($bytes, $peOffset + 6)
$sectionTable = $peOffset + 24 + [BitConverter]::ToUInt16($bytes, $peOffset + 20)
$entryOffset = -1
for ($section = 0; $section -lt $sectionCount; $section++) {
    $header = $sectionTable + $section * 40
    $virtualSize = [BitConverter]::ToUInt32($bytes, $header + 8)
    $virtualAddress = [BitConverter]::ToUInt32($bytes, $header + 12)
    $rawSize = [BitConverter]::ToUInt32($bytes, $header + 16)
    if (0xB10D0 -ge $virtualAddress -and
        0xB10D0 + 7 -le $virtualAddress + [Math]::Min($virtualSize, $rawSize)) {
        $entryOffset = [BitConverter]::ToUInt32($bytes, $header + 20) + 0xB10D0 - $virtualAddress
        break
    }
}
if ($entryOffset -lt 0 -or
    [BitConverter]::ToString($bytes, $entryOffset, 7) -cne '40-53-55-41-54-41-57') {
    throw 'Installed tannery updater entry differs from the audited detour span.'
}

$locales = @(Get-ChildItem -LiteralPath (Join-Path $root 'Locales') -Filter '*.txt' -File)
foreach ($file in $locales) {
    $locale = [IO.File]::ReadAllText($file.FullName)
    foreach ($key in @('BugfixesAndQoL.EnableTanneryAnimationFix=',
            'BugfixesAndQoL.EnableTanneryAnimationFixHelp=')) {
        if (([regex]::Matches($locale, '(?m)^' + [regex]::Escape($key))).Count -ne 1) {
            throw "Missing or duplicate locale key $key in $($file.Name)"
        }
    }
}

$paths = @(
    (Join-Path $root 'src\NativeTannerFade.cs'),
    (Join-Path $root 'src\NativeTannerFadeState.cs'),
    (Join-Path $root 'src\BugfixesAndQoLRuntime.cs'),
    (Join-Path $root 'src\BugfixesAndQoLViewModel.cs'),
    (Join-Path $root 'Override\ScriptExtenderUI\BugfixesAndQoLSettings.xaml'),
    (Join-Path $root 'tests\TannerFade.Tests\Program.cs'),
    (Join-Path $root 'tests\TannerFade.Tests\TannerFade.Tests.csproj'),
    (Join-Path $root 'README.md'), (Join-Path $root 'info.json'),
    (Join-Path $root 'BugfixesAndQoL.csproj'), (Join-Path $root 'build.bat'),
    $PSCommandPath
) + @($locales | ForEach-Object FullName)
foreach ($path in $paths) {
    $content = [IO.File]::ReadAllText($path)
    $literalEscape = [string][char]92 + 'r' + [string][char]92 + 'n'
    if ([regex]::IsMatch($content, '(?<!\r)\n') -or $content.Contains($literalEscape)) {
        throw "Text newline contract failed: $path"
    }
}
Write-Host 'Tannery native, settings, locales, lifecycle and CRLF preflight passed.'
