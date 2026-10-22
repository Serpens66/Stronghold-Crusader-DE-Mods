param([Parameter(Mandatory=$true)][string]$GameDir, [string]$EvidencePath)
$ErrorActionPreference = 'Stop'
if (-not $EvidencePath) { $EvidencePath = Join-Path $PSScriptRoot 'catalog-evidence.json' }
$capturedNk = Get-Content -LiteralPath $EvidencePath -Raw | ConvertFrom-Json
$workspaceNk = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
. (Join-Path $workspaceNk 'Shared\Tools\ApiSharedRepository\RepositoryProcess.Common.ps1')
$nativePathNk = Join-Path $GameDir 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
if ((Get-ApiRepositoryHash $nativePathNk) -ine $capturedNk.nativeHash) { throw 'Installed native image is not the captured audit.' }
$nativeBytesNk = [IO.File]::ReadAllBytes($nativePathNk)
function Assert-NativeInstructionNk([int]$Rva, [string]$Expected) {
    $peNk = [BitConverter]::ToInt32($nativeBytesNk, 0x3C)
    $sectionCountNk = [BitConverter]::ToUInt16($nativeBytesNk, $peNk + 6)
    $optionalSizeNk = [BitConverter]::ToUInt16($nativeBytesNk, $peNk + 20)
    $sectionsNk = $peNk + 24 + $optionalSizeNk
    for ($sectionNk = 0; $sectionNk -lt $sectionCountNk; $sectionNk++) {
        $headerNk = $sectionsNk + 40 * $sectionNk
        $startNk = [BitConverter]::ToInt32($nativeBytesNk, $headerNk + 12)
        $lengthNk = [BitConverter]::ToInt32($nativeBytesNk, $headerNk + 16)
        $rawNk = [BitConverter]::ToInt32($nativeBytesNk, $headerNk + 20)
        $expectedLengthNk = $Expected.Length / 2
        if ($Rva -ge $startNk -and $Rva -le $startNk + $lengthNk - $expectedLengthNk) {
            $actualNk = [BitConverter]::ToString($nativeBytesNk, $rawNk + $Rva - $startNk, $expectedLengthNk).Replace('-', '')
            if ($actualNk -cne $Expected) { throw ('Native instruction mismatch at RVA ' + $Rva.ToString('X')) }
            return
        }
    }
    throw ('Native instruction outside raw PE sections: ' + $Rva.ToString('X'))
}
# These are deliberate native subfields, not direct references to a named SE member.
# Full RAID_RETARGET flow establishes manager origin +0x2A and command target stores.
# Independently decoded instructions verify WORD +0x64C and DWORD +0x650 in 0x11E960.
# The public 0x622/0x626 slices can begin within/cross source DWORD declarations.
$nativeSlicesNk = @{
    'GameTribe:1570' = [pscustomobject]@{ Width=2; Rva=0x11FC2C; Bytes='664589B42A4C060000' }
    'GameTribe:1574' = [pscustomobject]@{ Width=4; Rva=0x11FC38; Bytes='45898C2A50060000' }
}
$extenderNk = Join-Path $GameDir 'BepInEx\plugins\000shcdese\SHCDESE.dll'
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDir 'BepInEx\core\Mono.Cecil.dll')))
$assemblyNk = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($extenderNk)
function Get-ScalarWidthNk($typeNk) {
    switch ($typeNk.FullName) {
        'System.Byte' { return 1 } 'System.SByte' { return 1 }
        'System.UInt16' { return 2 } 'System.Int16' { return 2 }
        'System.UInt32' { return 4 } 'System.Int32' { return 4 }
        'System.Single' { return 4 }
        'System.UInt64' { return 8 } 'System.Int64' { return 8 }
        'System.Double' { return 8 }
        default {
            if ($typeNk.IsPointer) { return 8 }
            $definitionNk = $assemblyNk.MainModule.GetType($typeNk.FullName)
            if (-not $definitionNk) { throw ('Unresolved installed layout type: ' + $typeNk.FullName) }
            if ($definitionNk.IsEnum) { return Get-ScalarWidthNk (($definitionNk.Fields | Where-Object Name -eq 'value__').FieldType) }
            if ($definitionNk.ClassSize -gt 0) { return $definitionNk.ClassSize }
            if ($definitionNk.IsValueType -and $definitionNk.IsSequentialLayout -and $definitionNk.PackingSize -eq 1) {
                $totalNk = 0
                foreach ($childNk in @($definitionNk.Fields | Where-Object { -not $_.IsStatic })) {
                    $totalNk += Get-ScalarWidthNk $childNk.FieldType
                }
                if ($totalNk -gt 0) { return $totalNk }
            }
            throw ('Unknown layout extent: ' + $typeNk.FullName)
        }
    }
}
try {
    $verifiedNk = @($capturedNk.fields | Where-Object verified)
    foreach ($groupNk in ($verifiedNk | Group-Object owner)) {
        $typeNk = $assemblyNk.MainModule.GetType('SHCDESE.Interop.' + $groupNk.Name)
        if (-not $typeNk -or -not $typeNk.IsSequentialLayout -or $typeNk.PackingSize -ne 1) {
            throw ('Unexpected installed record layout: ' + $groupNk.Name)
        }
        $offsetNk = 0
        $layoutNk = @()
        foreach ($fieldNk in @($typeNk.Fields | Where-Object { -not $_.IsStatic })) {
            $widthNk = Get-ScalarWidthNk $fieldNk.FieldType
            $layoutNk += [pscustomobject]@{ Name=$fieldNk.Name; Offset=$offsetNk; Width=$widthNk; Public=$fieldNk.IsPublic }
            $offsetNk += $widthNk
        }
        foreach ($contractNk in $groupNk.Group) {
            if ($offsetNk -ne $contractNk.size -or $typeNk.ClassSize -ne $contractNk.size) {
                throw ('Record extent differs: ' + $groupNk.Name)
            }
            $matchesNk = @($layoutNk | Where-Object Offset -eq $contractNk.offset)
            $sliceNk = $nativeSlicesNk[$contractNk.owner + ':' + $contractNk.offset]
            if ($sliceNk) {
                if ($contractNk.count -ne 1 -or $contractNk.stride -ne $sliceNk.Width) { throw 'Native slice width changed.' }
                Assert-NativeInstructionNk $sliceNk.Rva $sliceNk.Bytes
                $sliceEndNk = $contractNk.offset + $sliceNk.Width
                $coverageNk = 0
                foreach ($partNk in $layoutNk) {
                    $coveredNk = [Math]::Min($sliceEndNk, $partNk.Offset + $partNk.Width) - [Math]::Max($contractNk.offset, $partNk.Offset)
                    if ($coveredNk -gt 0) {
                        if (-not $partNk.Public) { throw 'Slice includes non-public storage.' }
                        $coverageNk += $coveredNk
                    }
                }
                if ($coverageNk -ne $sliceNk.Width) { throw 'Native slice not fully covered by the installed record.' }
                Write-Output ($contractNk.id + ' audited native byte slice, matching installed record extent and original machine store')
                continue
            }
            if ($matchesNk.Count -ne 1 -or -not $matchesNk[0].Public -or
                $matchesNk[0].Width -ne ($contractNk.stride * $contractNk.count)) {
                throw ('Installed storage differs for ' + $contractNk.id + ' ' + $contractNk.owner + ' offset ' + $contractNk.offset)
            }
            Write-Output ($contractNk.id + ' ' + $contractNk.owner + '+0x' + ([int]$contractNk.offset).ToString('X') +
                ' width=' + $matchesNk[0].Width + ' installed=' + $matchesNk[0].Name + ' verified')
        }
    }
    Write-Output ('PASS: ' + $verifiedNk.Count + ' read bindings match installed public storage. Raw native signedness is specified by the feature audit; no live-memory/gameplay claim.')
} finally { $assemblyNk.Dispose() }
