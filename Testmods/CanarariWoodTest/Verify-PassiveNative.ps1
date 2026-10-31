$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $MyInvocation.MyCommand.Path
$workspace = (Resolve-Path -LiteralPath (Join-Path $project '..\..')).Path
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $project 'src') -Filter '*.cs' -File -Recurse)
$projectFile = Join-Path $project 'CanarariWoodTest.csproj'
$native = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'
$expectedHash = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'
$nativeStream = [IO.File]::OpenRead($native)
try {
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try { $actualHash = [BitConverter]::ToString($sha256.ComputeHash($nativeStream)).Replace('-', '') }
    finally { $sha256.Dispose() }
}
finally { $nativeStream.Dispose() }
if (-not [string]::Equals($actualHash, $expectedHash, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Installed native DLL differs from the audited woodcutter build.'
}
$baselineRoot = Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline'
$current = Get-Content -LiteralPath (Join-Path $baselineRoot 'CURRENT.json') -Raw | ConvertFrom-Json
$functions = Join-Path (Join-Path $baselineRoot $current.semanticDirectory) 'exports\semantic-functions.jsonl'
$audited = @{}
Get-Content -LiteralPath $functions | Where-Object {
    $_ -match '"rva":"0x(51540|58020|58950|58BE0|C3BF0)"'
} | ForEach-Object { $item = $_ | ConvertFrom-Json; $audited[$item.rva] = $item }
if ($audited['0x51540'].calleeRvas -notcontains '0x58950' -or
    $audited['0x58950'].calleeRvas.Count -ne 0 -or
    $audited['0x58950'].dataRvas -contains '0x50EC690' -or
    $audited['0x58BE0'].calleeRvas -notcontains '0xE2610' -or
    $audited['0x58BE0'].dataRvas -notcontains '0x50EC690') {
    throw 'Audited 0x58950 and 0x58BE0 contracts differ.'
}
$farmAudit = @{}
Get-Content -LiteralPath $functions | Where-Object {
    $_ -match '"rva":"0x(539B0|2DBA0|2C9F0|50D80|575B0)"'
} | ForEach-Object { $item = $_ | ConvertFrom-Json; $farmAudit[$item.rva] = $item }
if ($farmAudit['0x539B0'].calleeRvas -notcontains '0x2DBA0' -or
    $farmAudit['0x539B0'].calleeRvas -notcontains '0x2C9F0' -or
    $farmAudit['0x539B0'].calleeRvas -notcontains '0x50D80' -or
    $farmAudit['0x2DBA0'].calleeRvas -notcontains '0xB81A0' -or
    $farmAudit['0x2C9F0'].dataRvas -notcontains '0x379D824' -or
    $farmAudit['0x50D80'].calleeRvas -notcontains '0x575B0' -or
    $farmAudit['0x575B0'].calleeRvas -notcontains '0x6D580' -or
    $farmAudit['0x575B0'].dataRvas -notcontains '0x379E74A') {
    throw 'Audited farm scheduler, choice, cooldown or search contract differs.'
}
$orchardFunctions = @{}
Get-Content -LiteralPath $functions | Where-Object {
    $_ -match '"rva":"0x(72A50|1071A0|E0850|D90D0|50720)"'
} | ForEach-Object { $item = $_ | ConvertFrom-Json; $orchardFunctions[$item.rva] = $item }
if ($orchardFunctions['0x72A50'].calleeRvas -notcontains '0x1071A0' -or
    $orchardFunctions['0x1071A0'].calleeRvas -notcontains '0xE0850' -or
    $orchardFunctions['0xE0850'].calleeRvas -notcontains '0xD90D0' -or
    $orchardFunctions['0x72A50'].calleeRvas -contains '0x50720') {
    throw 'Audited orchard creation/pathfinding/coarse-grid call chain differs.'
}
$farmCreators = @{}
Get-Content -LiteralPath $functions | Where-Object {
    $_ -match '"rva":"0x(72490|73450|74410|776F0|7B060|C3BF0)"'
} | ForEach-Object { $item = $_ | ConvertFrom-Json; $farmCreators[$item.rva] = $item }
foreach ($rva in @('0x72A50', '0x73450', '0x74410', '0x776F0')) {
    $entry = if ($rva -eq '0x72A50') { $orchardFunctions[$rva] } else { $farmCreators[$rva] }
    if ($null -eq $entry -or $entry.calleeRvas -notcontains '0x72490') {
        throw "Farm parcel writer call differs at $rva."
    }
}
if ($null -eq $farmCreators['0x7B060'] -or $null -eq $farmCreators['0xC3BF0']) {
    throw 'Placement or route function metadata is unavailable.'
}
$nativeBytes = [IO.File]::ReadAllBytes($native)
$pe = [BitConverter]::ToInt32($nativeBytes, 0x3c)
$sectionCount = [BitConverter]::ToUInt16($nativeBytes, $pe + 6)
$optionalSize = [BitConverter]::ToUInt16($nativeBytes, $pe + 20)
$gateRva = 0x58B12
$gateOffset = -1
for ($i = 0; $i -lt $sectionCount; $i++) {
    $section = $pe + 24 + $optionalSize + $i * 40
    $size = [BitConverter]::ToInt32($nativeBytes, $section + 8)
    $rva = [BitConverter]::ToInt32($nativeBytes, $section + 12)
    if ($rva -le $gateRva -and $gateRva + 24 -le $rva + $size) {
        $gateOffset = [BitConverter]::ToInt32($nativeBytes, $section + 20) + $gateRva - $rva
        break
    }
}
if ($gateOffset -lt 0) { throw 'Native farmland candidate gate is unavailable.' }
$expectedGate = [byte[]]@(0x75,0x1A,0x44,0x38,0xB2,0x37,0xB8,0x05,0x00,0x75,0x11,0x44,
    0x38,0xB2,0x38,0xB8,0x05,0x00,0x7F,0x08,0x84,0xDB,0x0F,0x84)
for ($i = 0; $i -lt $expectedGate.Length; $i++) {
    if ($nativeBytes[$gateOffset + $i] -ne $expectedGate[$i]) {
        throw 'Native farmland candidate gate differs from the runtime compatibility check.'
    }
}
$orchardRva = 0x2d3dc0
$orchardOffset = -1
for ($i = 0; $i -lt $sectionCount; $i++) {
    $section = $pe + 24 + $optionalSize + $i * 40
    $size = [BitConverter]::ToInt32($nativeBytes, $section + 8)
    $rva = [BitConverter]::ToInt32($nativeBytes, $section + 12)
    if ($rva -le $orchardRva -and $orchardRva + 64 -le $rva + $size) {
        $orchardOffset = [BitConverter]::ToInt32($nativeBytes, $section + 20) + $orchardRva - $rva
        break
    }
}
if ($orchardOffset -lt 0) { throw 'Native orchard offset table is unavailable.' }
$expectedOrchard = @(5,1,9,1,1,5,5,5,9,5,1,9,5,9,9,9)
for ($i = 0; $i -lt $expectedOrchard.Count; $i++) {
    if ([BitConverter]::ToInt32($nativeBytes, $orchardOffset + $i * 4) -ne $expectedOrchard[$i]) {
        throw 'Native apple-tree offset table differs from the diagnostic.'
    }
}
$woodDirectionRva = 0x2d2e50
$woodDirectionOffset = -1
for ($i = 0; $i -lt $sectionCount; $i++) {
    $section = $pe + 24 + $optionalSize + $i * 40
    $size = [BitConverter]::ToInt32($nativeBytes, $section + 8)
    $rva = [BitConverter]::ToInt32($nativeBytes, $section + 12)
    if ($rva -le $woodDirectionRva -and $woodDirectionRva + 64 -le $rva + $size) {
        $woodDirectionOffset = [BitConverter]::ToInt32($nativeBytes, $section + 20) + $woodDirectionRva - $rva
        break
    }
}
if ($woodDirectionOffset -lt 0) { throw 'Native wood-search direction table unavailable.' }
$expectedWoodDirections = @(0,-1,1,-1,1,0,1,1,0,1,-1,1,-1,0,-1,-1)
for ($i = 0; $i -lt $expectedWoodDirections.Count; $i++) {
    if ([BitConverter]::ToInt32($nativeBytes, $woodDirectionOffset + $i * 4) -ne $expectedWoodDirections[$i]) {
        throw 'Native wood-search direction order differs from the read-only replay.'
    }
}
$rizinRelative = '.tools\Cutter-v2.4.1-Windows-x86_64\Cutter-v2.4.1-Windows-x86_64\rizin.exe'
$rizin = Join-Path $workspace $rizinRelative
if (-not (Test-Path -LiteralPath $rizin -PathType Leaf)) {
    # Ignored machine tools exist only in the primary checkout; branch build copies and other
    # Git worktrees share its Git directory, so resolve the tool from there.
    $commonGit = @(& git -C $workspace rev-parse --path-format=absolute --git-common-dir 2>$null)
    if ($LASTEXITCODE -eq 0 -and $commonGit.Count -eq 1) { $rizin = Join-Path (Split-Path -Parent ([IO.Path]::GetFullPath($commonGit[0].Trim()))) $rizinRelative }
}
if (-not (Test-Path -LiteralPath $rizin -PathType Leaf)) { throw "rizin.exe is required for the native edge audit: $rizinRelative" }
$routeEntry = (& $rizin -q -e scr.color=false -c 's 0x1800c3bf0; p8 7; q' $native) -join ''
if ($routeEntry.Trim().ToLowerInvariant() -ne '4883ec384963c0') {
    throw 'Route hook displaced instruction bytes differ from the audited seven bytes.'
}
$generalEntries = (& $rizin -q -e scr.color=false -c 's 0x1800583a0; p8 6; s 0x180058be0; p8 6; q' $native)
if ($generalEntries.Count -ne 2 -or
    $generalEntries[0].Trim().ToLowerInvariant() -ne '41574883ec20' -or
    $generalEntries[1].Trim().ToLowerInvariant() -ne '895424105355') {
    throw 'General AI search entry bytes or six-byte instruction boundaries differ.'
}
$redbird = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\RedBird.Backends.NativeX64.dll'
if ((Get-Item -LiteralPath $redbird).VersionInfo.FileVersion -ne '1.5.0.0') {
    throw 'Installed NativeX64 backend version differs from the audited detour implementation.'
}
$calls = (& $rizin -q -e scr.color=false -c 's 0x180051587; pd 1; s 0x1800515cd; pd 1; s 0x1800515ff; pd 1; s 0x180051670; pd 1; q' $native) -join "`n"
foreach ($edge in @(@('51587','58020'), @('515cd','58950'),
    @('515ff','c3bf0'), @('51670','6d580'))) {
    if ($calls -notmatch ("0x1800" + $edge[0] + '\s+call\s+0x1800' + $edge[1])) {
        throw "Woodcutter call edge differs: $($edge[0]) -> $($edge[1])"
    }
}
$gridEdges = (& $rizin -q -e scr.color=false -c 's 0x1800575a4; pd 1; s 0x180071a06; pd 1; s 0x180084d1e; pd 1; s 0x180096d49; pd 1; q' $native) -join "`n"
foreach ($edge in @(@('575a4','jmp'), @('71a06','call'),
    @('84d1e','call'), @('96d49','call'))) {
    if ($gridEdges -notmatch ("0x1800" + $edge[0] + '\s+' + $edge[1] + '\s+0x180050720')) {
        throw "Economy-grid update edge differs: $($edge[0]) -> 50720"
    }
}
$placementSource = (& $rizin -q -e scr.color=false -c 's 0x1800724ea; pd 5; s 0x180078878; pd 9; q' $native) -join "`n"
if ($placementSource -notmatch '0x1800724ec\s+or\s+dword \[rbx \+ rax\*4 \+ 0x898400\], 4' -or
    $placementSource -notmatch '0x180078889\s+call\s+0x18007b060' -or
    $placementSource -notmatch '0x180078892\s+mov\s+dword \[rbx \+ 0x204e6fc\], 1') {
    throw 'Apple-farm reservation writer or woodsman placement validator differs.'
}
Write-Host 'PASS: passive wood/farm native contracts.'
