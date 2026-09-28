[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$workspace = (Resolve-Path -LiteralPath (Join-Path $root '..\..')).Path
$gameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$native = Join-Path $gameDir 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$managed = Join-Path $gameDir 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'
$extender = Join-Path $gameDir 'BepInEx\plugins\000shcdese'
$files = @(
    (Join-Path $root 'DamagedHealthBarsTest.csproj'),
    (Join-Path $root 'info.json'),
    (Join-Path $root 'UpdateToNewDLL.md'),
    (Join-Path $root 'build.bat'),
    (Join-Path $root 'verify.ps1'),
    (Join-Path $root 'Properties\AssemblyInfo.cs')
) + @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -File -Filter '*.cs' |
    ForEach-Object FullName)

foreach ($path in $files) {
    $content = [IO.File]::ReadAllText($path)
    $literalNewlineEscape = ([string][char]92) + 'r' + ([string][char]92) + 'n'
    if ($content -match '(?<!\r)\n' -or $content.Contains($literalNewlineEscape)) {
        throw "CRLF or literal newline escape failure: $path"
    }
}

$project = [IO.File]::ReadAllText((Join-Path $root 'DamagedHealthBarsTest.csproj'))
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -File -Filter '*.cs' |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
$allText = $project + "`n" + $sources
if ($allText -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|Assembly-CSharp-publicized') {
    throw 'Forbidden JSON dependency or publicized game assembly reference.'
}
if ($allText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') {
    throw 'Long-lived work must not use a Unity component lifecycle or frame callback.'
}
if ($allText -match 'CodePatch\.Write|Marshal\.Write|VirtualProtect|NativeDetour|\.Enable\s*\(|\.Disable\s*\(|\.Undo\s*\(|\.Apply\s*\(') {
    throw 'Executable runtime mutation or published-hook teardown found in testmod.'
}
if ($sources -notmatch 'HEALTH_BARS_POST_STARTUP' -or
    $sources -notmatch 'InputR3EventHooks\.OnKeyDown' -or
    $sources -notmatch 'GameTimeManagerAPI\.Instance' -or
    $sources -notmatch 'Interlocked\.Exchange' -or
    $sources -notmatch 'DisplacedByteCount') {
    throw 'A persistent publisher, startup marker, atomic flag or hook-length check is missing.'
}
if ($sources -notmatch 'gameData\.app_mode != 14 && gameData\.app_mode != 16' -or
    $sources -notmatch 'controller\.NoesisHasKeyboard' -or
    $sources -notmatch 'viewModel\.IsMapEditorMode' -or
    $sources -notmatch 'HEALTH_BARS_HOTKEY_REJECTED' -or
    $sources -match 'lastGameState\.app_mode') {
    throw 'Gameplay hotkey gating differs from the audited current-mode and text-input contract.'
}
$hotkeyHandler = [regex]::Match($sources, '(?s)private void OnKeyDown\(UnityInputEventArgs args\).*?private void RejectHotkey').Value
if (-not $hotkeyHandler -or $hotkeyHandler -match 'args\.Result\s*=') {
    throw 'Accepted Alt+H must retain the KeyManager Down/Held/Up state transition.'
}
if ($sources -match 'transaction\.Dispose\s*\(' -or $sources -match 'activeFlag.*FreeHGlobal.*\b(OnDestroy|OnDisable|OnApplicationQuit)') {
    throw 'Published hook transaction or flag has a teardown path.'
}
if (([regex]::Matches($sources, 'pending\?\.Dispose\s*\(')).Count -ne 1) {
    throw 'Expected exactly one unpublished-candidate rollback.'
}

$manifest = Get-Content -Raw -LiteralPath (Join-Path $root 'info.json') | ConvertFrom-Json
if ($manifest.NetworkMode -ne 1 -or $manifest.GUID -ne 'DamagedHealthBarsTest_Serp') {
    throw 'Manifest identity or network mode differs.'
}
function Get-Sha256([string]$path) {
    $stream = [IO.File]::OpenRead($path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $sha.Dispose(); $stream.Dispose() }
}
if ((Get-Sha256 $native) -ne 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2') {
    throw 'Installed native DLL differs from the audited baseline.'
}
function Get-PeRvaBytes([string]$path, [int]$rva, [int]$length) {
    $stream = [IO.File]::OpenRead($path)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        $stream.Position = 0x3c
        $peOffset = $reader.ReadInt32()
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x4550) { throw 'Invalid PE signature.' }
        $stream.Position = $peOffset + 6
        $sectionCount = $reader.ReadUInt16()
        $stream.Position = $peOffset + 20
        $optionalSize = $reader.ReadUInt16()
        $stream.Position = $peOffset + 24 + $optionalSize
        for ($i = 0; $i -lt $sectionCount; $i++) {
            $sectionStart = $stream.Position
            $stream.Position = $sectionStart + 8
            $virtualSize = $reader.ReadUInt32()
            $virtualAddress = $reader.ReadUInt32()
            $rawSize = $reader.ReadUInt32()
            $rawOffset = $reader.ReadUInt32()
            if ($rva -ge $virtualAddress -and ($rva + $length) -le ($virtualAddress + $rawSize) -and
                ($rva + $length) -le ($virtualAddress + $virtualSize)) {
                $stream.Position = $rawOffset + ($rva - $virtualAddress)
                return $reader.ReadBytes($length)
            }
            $stream.Position = $sectionStart + 40
        }
        throw ('Native RVA 0x{0:X} is outside file-backed sections.' -f $rva)
    }
    finally { $reader.Dispose() }
}
$nativeSpans = @(
    @{ Rva = 0x489C4; Hex = '833D0DD561031075083B3DC1F979067420' },
    @{ Rva = 0x4F9B4; Hex = '833D1D6561031075083B35D1897906741F' },
    @{ Rva = 0x1A1945; Hex = '6639B42F8C8A7E0675224038B42FB48E7E060F8439020000' }
)
foreach ($span in $nativeSpans) {
    $actual = [BitConverter]::ToString((Get-PeRvaBytes $native $span.Rva ($span.Hex.Length / 2))).Replace('-', '')
    if ($actual -ne $span.Hex) {
        throw ('Native hook bytes at RVA 0x{0:X} differ from the audited instruction span.' -f $span.Rva)
    }
}
if ((Get-Sha256 $managed) -ne 'BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789') {
    throw 'Installed managed game DLL differs from the audited baseline.'
}
foreach ($name in @('SHCDESE.dll', 'Microsoft.Extensions.Logging.Abstractions.dll', 'R3.dll', 'System.Memory.dll', 'Iced.dll', 'RedBird.Abstractions.dll', 'RedBird.Core.dll', 'RedBird.X64.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $extender $name) -PathType Leaf)) {
        throw "Installed dependency missing: $name"
    }
}
if (([Reflection.AssemblyName]::GetAssemblyName((Join-Path $extender 'RedBird.X64.dll')).Version.ToString()) -ne '1.5.0.0') {
    throw 'Installed RedBird backend differs from the audited version.'
}
if (-not (Test-Path -LiteralPath 'D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Fremde Mods\shcde-fixes-main' -PathType Container)) {
    throw 'Canonical local Fixes source is unavailable.'
}

$xamlPatches = @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.xaml' -ErrorAction SilentlyContinue)
foreach ($patch in $xamlPatches) {
    [xml]$document = Get-Content -Raw -LiteralPath $patch.FullName
    foreach ($content in @($document.SelectNodes('//*[local-name()="Content"]'))) {
        if (@($content.ChildNodes | Where-Object NodeType -eq Element).Count -ne 1) {
            throw "Script Extender XAML Content must have exactly one direct child: $($patch.FullName)"
        }
    }
}

# Workspace regression: report newly added executable code mutations outside this mod.
# Those changes belong to other in-progress work and are reviewed independently.
$diff = & git -c core.safecrlf=false -C $workspace diff --unified=0 -- '*.cs' 2>$null
$newMutation = @($diff | Where-Object {
    $_ -match '^\+(?!\+\+).*?(CodePatch\.Write|Marshal\.Write|VirtualProtect|\.Enable\s*\(|\.Disable\s*\(|\.Undo\s*\(|\.Apply\s*\()'
})
$untracked = @(& git -C $workspace ls-files --others --exclude-standard -- '*.cs' 2>$null)
foreach ($relative in $untracked) {
    if ($relative -like 'Testmods/DamagedHealthBarsTest/*') { continue }
    $path = Join-Path $workspace $relative
    if ((Test-Path -LiteralPath $path -PathType Leaf) -and
        ([IO.File]::ReadAllText($path) -match 'CodePatch\.Write|Marshal\.Write|VirtualProtect|\.Enable\s*\(|\.Disable\s*\(|\.Undo\s*\(|\.Apply\s*\(')) {
        $newMutation += $relative
    }
}
if ($newMutation.Count -gt 0) {
    Write-Warning 'Other workspace changes contain executable mutation calls requiring separate review.'
    $newMutation | ForEach-Object { Write-Warning $_ }
}

Write-Host 'Damaged health bars source and installed dependency preflight passed. No build was run.'
