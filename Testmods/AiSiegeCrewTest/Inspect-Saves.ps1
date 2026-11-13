#requires -Version 7.0
<#
.SYNOPSIS
Read-only, hash-bound inspection of AI siege crew in SCDE saves; JSON to stdout.
.DESCRIPTION
Run in a fresh pwsh -NoProfile process. The existing MapParser.Core sources are
compiled in memory; no game process, installation, save or output file is changed.
PowerShell 7 supplies the compiler required by the unchanged parser's C# features.

Native contract: SHA256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
DLL_LoadSaveGame (RVA 853F0) passes descriptor table 336B20 to loader 87D0.
Descriptors have stride 24, target VA at +0, size at +12, section ID at +18.
Section 3015 starts at UnitManager+658, NOT the Extender GameUnit struct+65C;
section 2016 starts at TribeManager+28, NOT the Extender GameTribe struct+2A.
One-based native IDs are used directly in the documented anchor formulas.

The type/life/role literals below are part of this exact native hash contract.
Type names agree with eChimps in SHCDESE/Interop/Enums.cs and the native handler
table at RVA 321CB0. No attempt is made to infer another DLL's layout or enums.
CreationTick records creation of the unit identity (often the original peasant),
not completion of its later recruitment. Missing device claims alone do not prove
that an engineer is stranded. Existing modsettings and the opaque save tail are
not interpreted, and this script does not repair already duplicated assignments.
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string[]]$SavePaths = @(
        'C:\Users\Serpens66\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\Saves\test AI engineer dupe before.sav',
        'C:\Users\Serpens66\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\Saves\test AI engineer dupe happened.sav'
    ),
    [ValidateNotNullOrEmpty()]
    [string]$NativeDll = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll',
    [int[]]$FocusUnitIds = @(771, 772, 876, 879, 815)
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$workspaceRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$referenceHash = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'

function Get-BytesSha256([byte[]]$Bytes) {
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash($Bytes)).Replace('-', '') }
    finally { $algorithm.Dispose() }
}

$baseline = Get-Content -LiteralPath (Join-Path $workspaceRoot '_inspect/CrusaderDE-Native-Baseline/CURRENT.json') -Raw | ConvertFrom-Json
if ($baseline.currentNativeHash -cne $referenceHash) { throw 'CURRENT.json differs from the audited save-layout contract.' }
$nativePath = (Resolve-Path -LiteralPath $NativeDll).ProviderPath
$nativeBytes = [IO.File]::ReadAllBytes($nativePath)
$nativeHash = Get-BytesSha256 $nativeBytes
if ($nativeHash -cne $referenceHash) { throw "Unknown CrusaderDE.dll SHA256: $nativeHash" }

# Validate the PE64 table and every serialized target against the canonical image.
if ([BitConverter]::ToUInt16($nativeBytes, 0) -ne 0x5A4D) { throw 'Missing DOS header.' }
$peOffset = [BitConverter]::ToInt32($nativeBytes, 0x3C)
if ($peOffset -lt 0 -or $peOffset -gt $nativeBytes.Length - 264) { throw 'PE header is outside the file.' }
if ([BitConverter]::ToUInt32($nativeBytes, $peOffset) -ne 0x4550 -or
    [BitConverter]::ToUInt16($nativeBytes, $peOffset + 4) -ne 0x8664 -or
    [BitConverter]::ToUInt16($nativeBytes, $peOffset + 24) -ne 0x20B) { throw 'Expected an AMD64 PE32+ DLL.' }
$imageBase = [BitConverter]::ToUInt64($nativeBytes, $peOffset + 48)
if ($imageBase -ne 0x180000000L) { throw 'Unexpected preferred image base.' }
$sectionCount = [BitConverter]::ToUInt16($nativeBytes, $peOffset + 6)
$optionalSize = [BitConverter]::ToUInt16($nativeBytes, $peOffset + 20)
$sectionTable = $peOffset + 24 + $optionalSize
$peSections = @(for ($index = 0; $index -lt $sectionCount; $index++) {
    $at = $sectionTable + $index * 40
    if ($at -gt $nativeBytes.Length - 40) { throw 'Truncated PE section table.' }
    [pscustomobject]@{
        Name = [Text.Encoding]::ASCII.GetString($nativeBytes, $at, 8).TrimEnd([char]0)
        VirtualSize = [BitConverter]::ToUInt32($nativeBytes, $at + 8)
        Rva = [BitConverter]::ToUInt32($nativeBytes, $at + 12)
        RawSize = [BitConverter]::ToUInt32($nativeBytes, $at + 16)
        RawOffset = [BitConverter]::ToUInt32($nativeBytes, $at + 20)
        Characteristics = [BitConverter]::ToUInt32($nativeBytes, $at + 36)
    }
})
function Get-NativeRawOffset([long]$Rva, [int]$Length) {
    $matches = @($peSections | Where-Object { $Rva -ge $_.Rva -and $Rva + $Length -le [long]$_.Rva + $_.RawSize })
    if ($matches.Count -ne 1) { throw ('Unmapped or ambiguous file-backed RVA 0x{0:X}.' -f $Rva) }
    $offset = [long]$matches[0].RawOffset + $Rva - $matches[0].Rva
    if ($offset -gt $nativeBytes.Length - $Length) { throw 'PE raw range exceeds file bounds.' }
    return [int]$offset
}
$descriptorSpecs = @(
    @{ SectionId = 3015; Rva = 0x337030; TargetRva = 0x67E8A58; Length = 11680000 },
    @{ SectionId = 1016; Rva = 0x337090; TargetRva = 0x7CC6748; Length = 2508000 },
    @{ SectionId = 2016; Rva = 0x3370A8; TargetRva = 0x7CC6748; Length = 7524000 },
    @{ SectionId = 2022; Rva = 0x337108; TargetRva = 0x379ADD0; Length = 203292 },
    @{ SectionId = 1046; Rva = 0x337210; TargetRva = 0x3665F58; Length = 4 }
)
$descriptorEvidence = @(foreach ($spec in $descriptorSpecs) {
    if (($spec.Rva - 0x336B20) % 24 -ne 0) { throw 'Descriptor is not aligned to the audited table.' }
    $raw = Get-NativeRawOffset $spec.Rva 24
    $target = [BitConverter]::ToUInt64($nativeBytes, $raw)
    $size = [BitConverter]::ToUInt32($nativeBytes, $raw + 12)
    $sectionId = [BitConverter]::ToUInt16($nativeBytes, $raw + 18)
    if ($target -ne $imageBase + $spec.TargetRva -or $size -ne $spec.Length -or $sectionId -ne $spec.SectionId) {
        throw ('Native descriptor contract failed at RVA 0x{0:X}.' -f $spec.Rva)
    }
    $targetSections = @($peSections | Where-Object {
        $spec.TargetRva -ge $_.Rva -and
        [long]$spec.TargetRva + $size -le [long]$_.Rva + [Math]::Max($_.VirtualSize, $_.RawSize) -and
        ($_.Characteristics -band 0x80000000L) -ne 0
    })
    if ($targetSections.Count -ne 1) { throw 'Serialized target is not wholly inside one writable native section.' }
    [pscustomobject]@{
        SectionId = $sectionId; DescriptorRva = ('0x{0:X}' -f $spec.Rva)
        TargetRva = ('0x{0:X}' -f $spec.TargetRva); Length = $size
    }
})

$parserDirectory = Join-Path $workspaceRoot 'Helpers/MapParser/MapParser.Core'
if ('MapParser.Core.MapFileReader' -as [type]) { throw 'Start a fresh pwsh process to compile the current parser sources.' }
$parserSources = @(Get-ChildItem -LiteralPath $parserDirectory -Filter '*.cs' -File | Sort-Object Name)
if ($parserSources.Count -eq 0) { throw 'MapParser.Core sources are missing.' }
Add-Type -Path $parserSources.FullName -ErrorAction Stop

# These domain values are deliberately confined to the pinned native contract.
$typeNames = @{ 1 = 'Peasant'; 30 = 'Engineer'; 39 = 'Catapult'; 40 = 'Trebuchet'; 41 = 'Mangonel'; 58 = 'SiegeTower'; 59 = 'BatteringRam'; 60 = 'PortableShield'; 61 = 'Ballista'; 77 = 'FireBallista' }
$crewCapacities = @{ 39 = 2; 40 = 3; 41 = 2; 58 = 4; 59 = 4; 60 = 1; 61 = 2; 77 = 2 }
$enumSymbols = @{ 1 = 'PEASANT'; 30 = 'ENGINEER'; 39 = 'CATAPULT'; 40 = 'TREBUCHET'; 41 = 'MANGONEL'; 58 = 'SIEGE_TOWER'; 59 = 'BATTERING_RAM'; 60 = 'PORTABLE_SHIELD'; 61 = 'BALLISTA'; 77 = 'ARAB_BALLISTA' }
$enumSource = Get-Content -LiteralPath (Join-Path $workspaceRoot 'shcde-script-extender/src/SHCDESE.BepInEx/Interop/Enums.cs') -Raw
$enumMatch = [regex]::Match($enumSource, '(?s)public enum eChimps\s*:\s*UInt16\s*\{(?<Body>.*?)\}')
if (-not $enumMatch.Success) { throw 'Cannot verify eChimps against the native type contract.' }
$enumEntries = @($enumMatch.Groups['Body'].Value.Split(',').Trim() | Where-Object { $_.Length -gt 0 })
if (@($enumEntries | Where-Object { $_ -cnotmatch '^CHIMP\w+$' }).Count -ne 0) { throw 'eChimps requires a new enum-value audit.' }
foreach ($entry in $enumSymbols.GetEnumerator()) {
    if ($enumEntries[$entry.Key] -cne ('CHIMP_TYPE_' + $entry.Value)) { throw "eChimps disagrees with native type $($entry.Key)." }
}
$unitStride = 0x490
$unitSerializedBase = 0x658
$tribeStride = 0x688
$tribeSerializedBase = 0x28
$unitBytes = $null
$tribeBytes = $null
$playerBytes = $null
$maximumUnitId = 9999
$maximumTribeId = 4499
$unitCache = @{}

function Get-Unit([int]$UnitId) {
    if ($UnitId -lt 1 -or $UnitId -gt $maximumUnitId) { return $null }
    if ($unitCache.ContainsKey($UnitId)) { return $unitCache[$UnitId] }
    $at = $UnitId * $unitStride - $unitSerializedBase
    $type = [int][BitConverter]::ToUInt16($unitBytes, $at + 0x6E6)
    $state = [BitConverter]::ToUInt32($unitBytes, $at + 0x918)
    $result = [pscustomobject]@{
        Id = $UnitId; GlobalId = [BitConverter]::ToUInt32($unitBytes, $at + 0x6F0)
        Life = [BitConverter]::ToInt16($unitBytes, $at + 0x6E4)
        Type = $type; TypeName = $(if ($typeNames.ContainsKey($type)) { $typeNames[$type] } else { "NativeType$type" })
        Owner = [BitConverter]::ToInt16($unitBytes, $at + 0x6EE)
        CreationTick = [BitConverter]::ToUInt32($unitBytes, $at + 0x6F4)
        Death = [BitConverter]::ToInt16($unitBytes, $at + 0x8F8)
        HealthRaw = [BitConverter]::ToInt32($unitBytes, $at + 0xA20)
        State = ('0x{0:X8}' -f $state); MainState = [int]($state -band 0xFFFF); ResumeState = [int]($state -shr 16)
        Recruitment = [BitConverter]::ToInt16($unitBytes, $at + 0x9CA)
        PendingType = [BitConverter]::ToUInt16($unitBytes, $at + 0x922)
        PendingState = [BitConverter]::ToInt16($unitBytes, $at + 0x924)
        NativeRole = [BitConverter]::ToInt16($unitBytes, $at + 0xA82)
        TribeId = [BitConverter]::ToInt16($unitBytes, $at + 0x930)
        TribeGlobalId = [BitConverter]::ToUInt32($unitBytes, $at + 0x93C)
        X = [BitConverter]::ToInt16($unitBytes, $at + 0x71C); Y = [BitConverter]::ToInt16($unitBytes, $at + 0x71E)
        TargetBuildingGlobalId = [BitConverter]::ToUInt32($unitBytes, $at + 0x6F8)
        Command = [BitConverter]::ToInt16($unitBytes, $at + 0x9F4)
        ContextId = [BitConverter]::ToInt16($unitBytes, $at + 0x9F6)
        ContextGlobalId = [BitConverter]::ToUInt32($unitBytes, $at + 0x9F8)
        MountRaw9B0 = [BitConverter]::ToInt16($unitBytes, $at + 0x9B0)
        CrewCounter974 = [BitConverter]::ToInt32($unitBytes, $at + 0x974)
    }
    $unitCache[$UnitId] = $result
    return $result
}

function Get-Tribe([int]$TribeId) {
    if ($TribeId -lt 1 -or $TribeId -gt $maximumTribeId) { return $null }
    $at = $TribeId * $tribeStride - $tribeSerializedBase
    # Native 119F90 reads 625 UInt16 bitmap words; bit index is the native UnitId.
    $members = @(for ($unitId = 1; $unitId -le $maximumUnitId; $unitId++) {
        if (($tribeBytes[$at + 0x60 + ($unitId -shr 3)] -band (1 -shl ($unitId -band 7))) -ne 0) { Get-Unit $unitId }
    })
    [pscustomobject]@{
        Id = $TribeId; GlobalId = [BitConverter]::ToUInt32($tribeBytes, $at + 0x34)
        Life = [BitConverter]::ToInt16($tribeBytes, $at + 0x40)
        Owner = [BitConverter]::ToInt16($tribeBytes, $at + 0x2C)
        CountRaw = [BitConverter]::ToInt16($tribeBytes, $at + 0x5C)
        BitmapCount = $members.Count; Members = $members
    }
}

$saveResults = @(foreach ($requestedPath in $SavePaths) {
    $savePath = (Resolve-Path -LiteralPath $requestedPath).ProviderPath
    $fileBytes = [IO.File]::ReadAllBytes($savePath)
    $saveHash = Get-BytesSha256 $fileBytes
    # Parse(byte[]) intentionally supports this container without the .map/.trail path filter.
    $document = [MapParser.Core.MapFileReader]::Parse($fileBytes)
    if (-not $document.SectionsAvailable -or $document.FormatKind.ToString() -ne 'CrusaderDefinitiveEdition') { throw "Unsupported save container: $savePath" }
    $contents = @{}
    $sectionEvidence = @(foreach ($spec in $descriptorSpecs) {
        $section = $document.GetSection($spec.SectionId)
        if ($section.UncompressedSize -ne $spec.Length) { throw "Unexpected section $($spec.SectionId) length in $savePath" }
        # ReadContent checks declared lengths, DCL stream and CRC for compressed sections.
        $content = $section.ReadContent()
        if ($content.Length -ne $spec.Length) { throw 'Decoded section length differs from native serialization length.' }
        $contents[$spec.SectionId] = $content
        [pscustomobject]@{
            SectionId = $section.SectionId; Length = $content.Length; StoredSize = $section.StoredSize
            Storage = $section.StorageKind.ToString()
            Validation = $(if ($section.StorageKind.ToString() -eq 'PkwareDcl') { 'DCL+length+CRC32' } else { 'Raw length; no CRC in this format' })
        }
    })
    $unitBytes = $contents[3015]; $tribeBytes = $contents[2016]; $playerBytes = $contents[2022]
    if ([Convert]::ToBase64String($contents[1016]) -cne [Convert]::ToBase64String($tribeBytes, 0, $contents[1016].Length)) {
        throw 'Tribe section1016 does not equal the prefix of section2016.'
    }
    $unitCache = @{}
    $devices = [Collections.Generic.List[object]]::new()
    $role22Engineers = [Collections.Generic.List[object]]::new()
    $claims = @{}
    for ($unitId = 1; $unitId -le $maximumUnitId; $unitId++) {
        $at = $unitId * $unitStride - $unitSerializedBase
        if ([BitConverter]::ToInt16($unitBytes, $at + 0x6E4) -ne 2 -or [BitConverter]::ToInt16($unitBytes, $at + 0x8F8) -ne 0) { continue }
        $type = [int][BitConverter]::ToUInt16($unitBytes, $at + 0x6E6)
        if ($type -eq 30 -and [BitConverter]::ToInt16($unitBytes, $at + 0xA82) -eq 22) { $role22Engineers.Add((Get-Unit $unitId)) }
        if (-not $crewCapacities.ContainsKey($type)) { continue }
        $unit = Get-Unit $unitId
        $count = [BitConverter]::ToInt16($unitBytes, $at + 0xA0C)
        $capacity = $crewCapacities[$type]
        $crew = @(for ($seat = 0; $seat -lt [Math]::Min([Math]::Max($count, 0), $capacity); $seat++) {
            $crewId = [BitConverter]::ToInt16($unitBytes, $at + 0x96C + $seat * 2)
            $crewGlobal = [BitConverter]::ToUInt32($unitBytes, $at + 0x974 + $seat * 4)
            $engineer = Get-Unit $crewId
            $identityMatches = $null -ne $engineer -and $engineer.GlobalId -eq $crewGlobal
            if ($crewId -gt 0) {
                $key = "$crewId/$crewGlobal"
                if (-not $claims.ContainsKey($key)) { $claims[$key] = [Collections.Generic.List[object]]::new() }
                $claims[$key].Add([pscustomobject]@{ Id = $unitId; GlobalId = $unit.GlobalId; Owner = $unit.Owner; Seat = $seat })
            }
            [pscustomobject]@{
                Seat = $seat; UnitId = $crewId; GlobalId = $crewGlobal; IdentityMatches = $identityMatches
                LivingEngineerSameOwner = $identityMatches -and $engineer.Type -eq 30 -and $engineer.Life -eq 2 -and $engineer.Death -eq 0 -and $engineer.Owner -eq $unit.Owner
                Unit = $engineer
            }
        })
        $devices.Add([pscustomobject]@{ Unit = $unit; CountRaw = $count; Capacity = $capacity; CountInRange = $count -ge 0 -and $count -le $capacity; Crew = $crew })
    }
    $duplicates = @(foreach ($key in ($claims.Keys | Sort-Object)) {
        $deviceClaims = $claims[$key]
        if (@($deviceClaims | Select-Object -ExpandProperty Id -Unique).Count -gt 1) {
            [pscustomobject]@{ CrewIdentity = $key; Devices = $deviceClaims.ToArray() }
        }
    })
    $focusUnits = @(foreach ($focusId in ($FocusUnitIds | Select-Object -Unique)) {
        [pscustomobject]@{ RequestedUnitId = $focusId; Unit = (Get-Unit $focusId) }
    })
    $playerReserves = @(for ($owner = 1; $owner -le 8; $owner++) {
        $playerAt = $owner * 0x583C - 0x379ADD0
        $reserveId = [BitConverter]::ToInt16($playerBytes, $playerAt + 0x379F7BA)
        $reserveGlobal = [BitConverter]::ToUInt32($playerBytes, $playerAt + 0x379FB5C)
        $reserve = Get-Tribe $reserveId
        [pscustomobject]@{
            Owner = $owner; NativeRole22Count = [BitConverter]::ToInt32($playerBytes, $playerAt + 0x379E79C)
            PhysicalRole22EngineerCount = @($role22Engineers | Where-Object Owner -eq $owner).Count
            NativeDeviceCount = [BitConverter]::ToInt32($playerBytes, $playerAt + 0x379E6EC)
            Cooldown = [BitConverter]::ToInt32($playerBytes, $playerAt + 0x379E6E8)
            ReserveId = $reserveId; ReserveGlobalId = $reserveGlobal
            ReserveIdentityMatches = $null -ne $reserve -and $reserve.GlobalId -eq $reserveGlobal
            Reserve = $reserve
        }
    })
    [pscustomobject]@{
        Path = $savePath; Sha256 = $saveHash; Length = $fileBytes.Length
        MapTick = [BitConverter]::ToUInt32($contents[1046], 0)
        DirectoryTag = $document.Directory.DirectoryTag; FormatVersion = $document.Directory.FormatVersion
        SectionCount = $document.Directory.SectionCount; MapName = $document.Metadata.StandaloneFileName
        WorldSize = $document.Metadata.WorldSize; Sections = $sectionEvidence
        TribePrefixMatches = $true; AliveCrewDevices = $devices.ToArray(); DuplicateCrewRefs = $duplicates
        FocusUnits = $focusUnits; PlayerReserves = $playerReserves
        Role22EngineersWithoutDeviceClaim = @($role22Engineers | Where-Object { -not $claims.ContainsKey("$($_.Id)/$($_.GlobalId)") })
    }
})
[pscustomobject]@{
    InspectionUtc = [DateTime]::UtcNow.ToString('o')
    NativeContract = [pscustomobject]@{
        Path = $nativePath; Sha256 = $nativeHash; DescriptorTableRva = '0x336B20'; Descriptors = $descriptorEvidence
        UnitOffsetFormula = 'UnitId*0x490 + rawFieldOffset - 0x658; UnitId=1..9999'
        TribeOffsetFormula = 'TribeId*0x688 + rawFieldOffset - 0x28; TribeId=1..4499'
        PlayerOffsetFormula = 'Owner*0x583C + fieldRva - 0x379ADD0; Owner=1..8'
        AliveDeviceFilter = 'Life=2, Death=0, one of the eight hash-bound siege unit types'
        CreationTickMeaning = 'Identity creation; not recruitment completion'
        Types = @(foreach ($value in ($typeNames.Keys | Sort-Object)) {
            [pscustomobject]@{ Value = $value; Name = $typeNames[$value]; Symbol = ('CHIMP_TYPE_' + $enumSymbols[$value]) }
        })
    }
    Parser = [pscustomobject]@{ SourceDirectory = $parserDirectory; InMemoryCompilation = $true; DecodedSections = @(3015, 1016, 2016, 2022, 1046); OpaqueTailInterpreted = $false }
    Saves = $saveResults
} | ConvertTo-Json -Depth 16 -Compress
