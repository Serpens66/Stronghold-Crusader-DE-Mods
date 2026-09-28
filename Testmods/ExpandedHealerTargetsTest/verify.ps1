[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$gameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$native = Join-Path $gameDir 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$extenderDir = Join-Path $gameDir 'BepInEx\plugins\000shcdese'
$extenderSource = (Resolve-Path -LiteralPath (Join-Path $root '..\..\shcde-script-extender\src\SHCDESE.BepInEx\Interop\Enums.cs')).Path
$files = @(
    (Join-Path $root 'ExpandedHealerTargetsTest.csproj'),
    (Join-Path $root 'info.json'),
    (Join-Path $root 'build.bat'),
    (Join-Path $root 'verify.ps1'),
    (Join-Path $root 'Properties\AssemblyInfo.cs'),
    (Join-Path $root 'tests\ExpandedHealerTargets.NativeProbe.csproj'),
    (Join-Path $root 'tests\Program.cs')
) + @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -File -Filter '*.cs' |
    ForEach-Object FullName)

foreach ($path in $files) {
    $content = [IO.File]::ReadAllText($path)
    $literalNewlineEscape = ([string][char]92) + 'r' + ([string][char]92) + 'n'
    if ($content -match '(?<!\r)\n' -or $content.Contains($literalNewlineEscape)) {
        throw "CRLF or literal newline escape failure: $path"
    }
}

$project = [IO.File]::ReadAllText((Join-Path $root 'ExpandedHealerTargetsTest.csproj'))
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -File -Filter '*.cs' |
    ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
$allText = $project + "`n" + $sources
if ($allText -match 'System\.Web\.Extensions|JavaScriptSerializer|System\.Text\.Json|Newtonsoft\.Json|DataContractJsonSerializer|JsonUtility|Assembly-CSharp-publicized') {
    throw 'Forbidden JSON dependency or publicized game assembly reference.'
}
if ($allText -match '\b(OnDestroy|OnDisable|OnApplicationQuit|Update|LateUpdate|FixedUpdate|StartCoroutine)\s*\(') {
    throw 'Plugin or runtime uses a destroyed Unity component callback.'
}
if ($sources -match 'CodePatch\.Write|Marshal\.Write|VirtualProtect|\.Disable\s*\(|\.Undo\s*\(|\.Apply\s*\(' -or
    $sources -match 'transaction\.Dispose\s*\(') {
    throw 'Runtime executable mutation or published-hook teardown found.'
}
if (([regex]::Matches($sources, '\.Enable\s*\(')).Count -ne 1 -or
    $sources -notmatch 'probe\.Enable\s*\(' -or
    $sources -notmatch 'probe\?\.Dispose\s*\(' -or
    $sources -match 'listBuilder\.Hook\.Dispose|transaction\?\.Dispose') {
    throw 'Native executable mutation must be confined to the copied, unpublished backend probe.'
}
if ($sources -notmatch 'pending\?\.Dispose\s*\(' -or
    $sources -notmatch 'CrusaderLibrary\.Instance\.LibraryLoaded' -or
    $sources -notmatch 'GameTimeManagerAPI\.Instance' -or
    $sources -notmatch 'HEALER_TARGETS_POST_STARTUP' -or
    $sources -notmatch 'ProbeBackend' -or
    $sources -notmatch 'ValidateInstalledDetour' -or
    $sources -notmatch 'listBuilder\.Original\(manager, playerId\)') {
    throw 'Persistent callback, backend proof, startup marker or Vanilla forwarding is missing.'
}

$manifest = Get-Content -Raw -LiteralPath (Join-Path $root 'info.json') | ConvertFrom-Json
if ($manifest.NetworkMode -ne 1 -or $manifest.GUID -ne 'ExpandedHealerTargetsTest_Serp' -or
    $manifest.Version -ne '0.1.0') {
    throw 'Manifest identity, network mode or initial version differs.'
}
function Get-Sha256([string]$path) {
    $stream = [IO.File]::OpenRead($path)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
    finally { $sha.Dispose(); $stream.Dispose() }
}
if ((Get-Sha256 $native) -ne
    'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2') {
    throw 'Installed CrusaderDE.dll differs from the audited native baseline.'
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

$entry = [BitConverter]::ToString((Get-PeRvaBytes $native 0x181340 23)).Replace('-', '')
if ($entry -ne '48895C241048896C24205741544155415641574883EC30') {
    throw 'Healer list-builder prologue differs from the audited 23-byte entry.'
}

function Get-EnumNames([string]$text) {
    $inside = $false
    $names = [Collections.Generic.List[string]]::new()
    foreach ($line in ($text -split "`n")) {
        if ($line -match 'public enum eChimps') { $inside = $true; continue }
        if (-not $inside) { continue }
        if ($line -match 'CHIMP_NUM_TYPES') { break }
        if ($line -match '^\s*(CHIMP_[A-Za-z0-9_]+)') { $names.Add($Matches[1]) }
    }
    return $names.ToArray()
}
$sourceEnum = @(Get-EnumNames ([IO.File]::ReadAllText($extenderSource)))
$installedEnumText = & ilspycmd -t SHCDESE.Interop.eChimps (Join-Path $extenderDir 'SHCDESE.dll')
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect the installed Script Extender enum.' }
$installedEnum = @(Get-EnumNames ($installedEnumText -join "`n"))
if ($sourceEnum.Count -ne $installedEnum.Count) { throw 'Extender source and installed eChimps lengths differ.' }
for ($i = 0; $i -lt $sourceEnum.Count; $i++) {
    if ($sourceEnum[$i] -ne $installedEnum[$i]) {
        throw "Extender source and installed eChimps differ at index $i."
    }
}
$expectedSiege = @{
    CHIMP_TYPE_CATAPULT = 39; CHIMP_TYPE_TREBUCHET = 40; CHIMP_TYPE_MANGONEL = 41
    CHIMP_TYPE_SIEGE_TOWER = 58; CHIMP_TYPE_BATTERING_RAM = 59
    CHIMP_TYPE_PORTABLE_SHIELD = 60; CHIMP_TYPE_BALLISTA = 61; CHIMP_TYPE_ARAB_BALLISTA = 77
}
foreach ($name in $expectedSiege.Keys) {
    if ([array]::IndexOf($installedEnum, $name) -ne $expectedSiege[$name]) {
        throw "Installed siege enum value differs for $name."
    }
}

Write-Host 'Expanded healer target verification passed: CRLF, runtime gates, native hash/entry, enum source/assembly, hook forwarding.'
