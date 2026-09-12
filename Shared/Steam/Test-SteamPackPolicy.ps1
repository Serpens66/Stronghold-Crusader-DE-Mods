$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SteamPackPolicy.ps1')
. (Join-Path $PSScriptRoot 'SteamWorkshopHistory.ps1')

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Steam pack policy test failed: $Message" }
}

Assert-True ((Resolve-SteamPackVersion -PreviousVersion '1.0.10' -PreparedVersion '1.0.12') -ceq '1.0.12') 'a prepared host version must not be lowered'
Assert-True ((Resolve-SteamPackVersion -PreviousVersion '1.0.11' -PreparedVersion '1.0.11') -ceq '1.0.12') 'an unchanged host version must advance past the published pack'
Assert-True ((Resolve-SteamPackVersion -PreviousVersion $null -PreparedVersion '0.9.0') -ceq '1.0.0') 'the first pack must start at least at 1.0.0'

Assert-True (@(Get-MissingSteamPackPaths -PreviousPaths @('a.dll','folder/b.xaml') -CurrentPaths @('a.dll','folder/b.xaml')).Count -eq 0) 'identical file sets must not report removals'
Assert-True (@(Get-MissingSteamPackPaths -PreviousPaths @('a.dll') -CurrentPaths @('a.dll','new.txt')).Count -eq 0) 'additional files must not report removals'
Assert-True (@(Get-MissingSteamPackPaths -PreviousPaths @('same-path.bin') -CurrentPaths @('same-path.bin')).Count -eq 0) 'changed content at the same path must not report a removal'
$removed = @(Get-MissingSteamPackPaths -PreviousPaths @('keep.dll','old/path.xaml') -CurrentPaths @('keep.dll'))
Assert-True ($removed.Count -eq 1 -and $removed[0] -ceq 'old/path.xaml') 'a removed path must be reported'
Assert-True (@(Get-MissingSteamPackPaths -PreviousPaths @('Folder\MixedCase.DLL') -CurrentPaths @('folder/mixedcase.dll')).Count -eq 0) 'path comparison must normalize separators and ignore case on Windows'

$packGuid = 'SerpsMods_Serp'
$patchPath = "BepInEx/plugins/$packGuid/Mods/BugfixesAndQoL_Serp/Patches/Assets/GUI/XAMLResources/HUD_ControlGroups.xaml"
Assert-True ((Get-SteamMissingPathAction -Path $patchPath -PackGuid $packGuid) -ceq 'XamlTombstone') 'Patches/**/*.xaml must use the automatic XAML tombstone'
Assert-True ((Get-SteamMissingPathAction -Path "BepInEx/plugins/$packGuid/Mods/X/Override/ScriptExtenderUI/X.xaml" -PackGuid $packGuid) -ceq 'Explicit') 'Override XAML must fail closed without an overlay'
Assert-True ((Get-SteamMissingPathAction -Path "BepInEx/plugins/$packGuid/Mods/X/X.dll" -PackGuid $packGuid) -ceq 'Explicit') 'DLL removal must fail closed without an overlay'
Assert-True ((Get-SteamMissingPathAction -Path "BepInEx/plugins/$packGuid/Mods/X/X.pdb" -PackGuid $packGuid) -ceq 'Retain') 'PDB removal must retain the last delivered file'
$unsafeRejected = $false
try { [void](Assert-SteamArchivePath -Path "BepInEx/plugins/$packGuid/../escape.dll" -PackGuid $packGuid) } catch { $unsafeRejected = $true }
Assert-True $unsafeRejected 'parent traversal must be rejected'

$movedRecords = @([pscustomobject]@{ Path = 'new/path.xaml'; Sha256 = 'abc' })
Assert-True ((Find-SteamMovedPath -OldPath 'old/path.xaml' -OldSha256 'abc' -CurrentRecords $movedRecords) -ceq 'new/path.xaml') 'an identical hash should label a moved source'
Assert-True ($null -eq (Find-SteamMovedPath -OldPath 'old/path.xaml' -OldSha256 'changed' -CurrentRecords $movedRecords)) 'changed moved content must not be guessed as an exact move'
Assert-True (@(Get-MissingSteamPackPaths -PreviousPaths @('old/path.xaml') -CurrentPaths @('new/path.xaml')).Count -eq 1) 'a moved source must require a tombstone even when move pairing is unavailable'
Assert-True (@(Get-MissingSteamPackPaths -PreviousPaths @('Mods/ExtraFeatures.dll','Mods/BugfixesAndQoL.dll') -CurrentPaths @('Mods/ExtraFeatures.dll','Mods/BugfixesAndQoL.dll')).Count -eq 0) 'a source-class move with stable DLL paths must not create a package tombstone'

$tombstoneText = Get-SteamXamlTombstoneText
$tombstoneBytes = [Text.UTF8Encoding]::new($false).GetBytes($tombstoneText)
Assert-True ($tombstoneBytes.Length -eq 106) 'the standard XAML tombstone must remain exactly 106 UTF-8 bytes'
Assert-True ($tombstoneText -notmatch '(?<!\r)\n') 'the standard XAML tombstone must contain CRLF only'
Assert-True (-not $tombstoneText.Contains('\r\n')) 'the standard XAML tombstone must not contain literal backslash-r-backslash-n text'

$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$seedPath = Join-Path $PSScriptRoot 'SerpsMods.upload-history.seed.json'
$seed = Get-Content -LiteralPath $seedPath -Raw | ConvertFrom-Json
[void](Assert-SteamWorkshopUploadHistory -History $seed -AppId 3024040 -StateName 'SerpsMods')
$expectedVersions = @('1.0.2','1.0.3','1.0.5','1.0.6','1.0.7','1.0.8','1.0.9','1.0.10','1.0.12')
Assert-True ((@($seed.Uploads.PackVersion) -join ',') -ceq ($expectedVersions -join ',')) 'the seed must contain exactly the nine confirmed public Steam versions'
Assert-True ('1.0.4' -notin @($seed.Uploads.PackVersion) -and '1.0.11' -notin @($seed.Uploads.PackVersion)) 'non-uploaded GitHub releases must be excluded'
Assert-True ('1.0.0' -notin @($seed.Uploads.PackVersion) -and '1.0.1' -notin @($seed.Uploads.PackVersion) -and
    ($seed | ConvertTo-Json -Depth 10) -notmatch 'CustomCustomTrail') 'private predecessors and CustomCustomTrail must be excluded'

function Get-TestMapPaths([string]$MapPath) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $bytes = [IO.File]::ReadAllBytes($MapPath)
    $offset = -1
    for ($index = 0; $index -le $bytes.Length - 4; $index++) {
        if ($bytes[$index] -eq 0x50 -and $bytes[$index + 1] -eq 0x4b -and $bytes[$index + 2] -eq 0x03 -and $bytes[$index + 3] -eq 0x04) { $offset = $index; break }
    }
    if ($offset -lt 0) { throw "ZIP payload not found: $MapPath" }
    $stream = [IO.MemoryStream]::new($bytes, $offset, $bytes.Length - $offset, $false)
    $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read)
    try { return @($archive.Entries | Where-Object Name | ForEach-Object { $_.FullName.Replace('\','/').TrimStart('/') } | Sort-Object -Unique) }
    finally { $archive.Dispose(); $stream.Dispose() }
}

foreach ($upload in $seed.Uploads) {
    $mapPath = Join-Path $workspace ".release-output\SerpsMods\v$($upload.PackVersion)\SerpsMods.map"
    Assert-True (Test-Path -LiteralPath $mapPath -PathType Leaf) "confirmed map v$($upload.PackVersion) must be locally available for the archive regression test"
    Assert-True ((Get-FileHash -LiteralPath $mapPath -Algorithm SHA256).Hash.ToLowerInvariant() -ceq [string]$upload.MapSha256) "confirmed map v$($upload.PackVersion) hash must match history"
}
$allPublishedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($upload in $seed.Uploads) {
    $publishedMap = Join-Path $workspace ".release-output\SerpsMods\v$($upload.PackVersion)\SerpsMods.map"
    foreach ($publishedPath in @(Get-TestMapPaths $publishedMap)) { [void]$allPublishedPaths.Add($publishedPath) }
}
$paths109 = @(Get-TestMapPaths (Join-Path $workspace '.release-output\SerpsMods\v1.0.9\SerpsMods.map'))
$paths1010 = @(Get-TestMapPaths (Join-Path $workspace '.release-output\SerpsMods\v1.0.10\SerpsMods.map'))
$removed109To1010 = @(Get-MissingSteamPackPaths -PreviousPaths $paths109 -CurrentPaths $paths1010)
Assert-True ($removed109To1010.Count -eq 1 -and $removed109To1010[0] -ceq $patchPath) '1.0.9 -> 1.0.10 must contain exactly the HUD_ControlGroups.xaml removal'
$paths1012 = @(Get-TestMapPaths (Join-Path $workspace '.release-output\SerpsMods\v1.0.12\SerpsMods.map'))
$missingFromLatest = @(Get-MissingSteamPackPaths -PreviousPaths @($allPublishedPaths) -CurrentPaths $paths1012)
Assert-True ($missingFromLatest.Count -eq 1 -and $missingFromLatest[0] -ceq $patchPath) 'the complete confirmed Steam history must have exactly one path missing from 1.0.12'

$sourceTombstonePath = Join-Path $workspace 'BugfixesAndQoL\Patches\Assets\GUI\XAMLResources\HUD_ControlGroups.xaml'
$sourceTombstoneBytes = [IO.File]::ReadAllBytes($sourceTombstonePath)
Assert-True ($sourceTombstoneBytes.Length -eq 106) 'the source HUD_ControlGroups tombstone must be 106 bytes'
Assert-True ([Convert]::ToBase64String($sourceTombstoneBytes) -ceq [Convert]::ToBase64String($tombstoneBytes)) 'the source HUD_ControlGroups tombstone must equal the standard no-op patch'
$createScriptText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Create-SteamModPack.ps1'))
$outerPathGuard = $createScriptText.IndexOf('if (-not $path.StartsWith($packageRoot, [StringComparison]::OrdinalIgnoreCase)) { continue }', [StringComparison]::Ordinal)
Assert-True ($outerPathGuard -ge 0) 'existing-tombstone detection must skip outer map files before applying the internal package path policy'
$archiveTraversalGuard = $createScriptText.IndexOf("Where-Object { `$_ -in @('','.', '..') }", [StringComparison]::Ordinal)
Assert-True ($archiveTraversalGuard -ge 0) 'historical map inventory must reject empty, current-directory and parent-directory path segments'
$preflightCall = $createScriptText.IndexOf('$safeHistoricalReplacements = Assert-HistoricalDeletionPolicyPreflight', [StringComparison]::Ordinal)
$releaseCall = $createScriptText.IndexOf('Invoke-ModRelease $mod', [StringComparison]::Ordinal)
Assert-True ($preflightCall -ge 0 -and $releaseCall -gt $preflightCall) 'fail-closed historical deletion preflight must run before GitHub mod releases'
$uploadScriptText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Upload-Workshop.ps1'))
$steamConfirmationCheck = $uploadScriptText.IndexOf('if (-not $updated)', [StringComparison]::Ordinal)
$historyAppendCall = $uploadScriptText.LastIndexOf('Add-SteamWorkshopUploadHistoryEntry', [StringComparison]::Ordinal)
Assert-True ($steamConfirmationCheck -ge 0 -and $historyAppendCall -gt $steamConfirmationCheck) 'upload history may only be appended after Steam confirms the update'
Assert-True ((Assert-ScriptExtenderXamlPatchContract -Directory (Join-Path $workspace 'APIShared')) -gt 0) 'the delivered APIShared XAML patches must satisfy the single-root contract'
Assert-True ((Assert-ScriptExtenderXamlPatchContract -Directory (Join-Path $workspace 'BugfixesAndQoL')) -gt 0) 'the delivered BugfixesAndQoL XAML patches, including empty tombstones, must satisfy the contract'

$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('serps-steam-policy-' + [Guid]::NewGuid().ToString('N'))
try {
    $validRoot = Join-Path $testRoot 'valid\Patches'
    $invalidRoot = Join-Path $testRoot 'invalid\Patches'
    [void](New-Item -ItemType Directory -Path $validRoot,$invalidRoot -Force)
    [IO.File]::WriteAllText((Join-Path $validRoot 'Valid.xaml'), '<Patch><Operation Type="Add" XPath="/root"><Content><Grid><Button /></Grid></Content></Operation></Patch>')
    [IO.File]::WriteAllText((Join-Path $invalidRoot 'Invalid.xaml'), '<Patch><Operation Type="Add" XPath="/root"><Content><Border /><Button /></Content></Operation></Patch>')
    Assert-True ((Assert-ScriptExtenderXamlPatchContract -Directory (Join-Path $testRoot 'valid')) -eq 1) 'a single Content root must be accepted'
    $rejected = $false
    try { [void](Assert-ScriptExtenderXamlPatchContract -Directory (Join-Path $testRoot 'invalid')) }
    catch { $rejected = $_.Exception.Message -match 'exactly one direct Content root element' }
    Assert-True $rejected 'multiple Content roots must be rejected'

    $generatedTombstone = Join-Path $testRoot 'generated\Patches\Removed.xaml'
    Write-SteamXamlTombstone -Path $generatedTombstone
    $generatedBytes = [IO.File]::ReadAllBytes($generatedTombstone)
    Assert-True ($generatedBytes.Length -eq 106 -and [Convert]::ToBase64String($generatedBytes) -ceq [Convert]::ToBase64String($tombstoneBytes)) 'automatic tombstone creation must write the valid standard 106-byte XAML file'

    $historyPath = Join-Path $testRoot 'history.json'
    $history = Get-SteamWorkshopUploadHistory -Path $historyPath -SeedPath $seedPath -AppId 3024040 -StateName 'SerpsMods'
    Assert-True (@($history.Uploads).Count -eq 9) 'initialization must copy the nine-entry seed'
    Assert-True (Add-SteamWorkshopUploadHistoryEntry -Path $historyPath -SeedPath $seedPath -AppId 3024040 -StateName 'SerpsMods' -ItemId '3788821961' -PackVersion '1.0.13' -MapSha256 ('a' * 64) -UploadedUtc ([DateTimeOffset]'2026-09-13T00:00:00Z')) 'a confirmed new upload must append once'
    Assert-True (-not (Add-SteamWorkshopUploadHistoryEntry -Path $historyPath -SeedPath $seedPath -AppId 3024040 -StateName 'SerpsMods' -ItemId '3788821961' -PackVersion '1.0.13' -MapSha256 ('a' * 64) -UploadedUtc ([DateTimeOffset]'2026-09-13T00:00:00Z'))) 'repeating the same confirmed upload must be idempotent'
    Assert-True (@((Get-Content -LiteralPath $historyPath -Raw | ConvertFrom-Json).Uploads).Count -eq 10) 'history must contain the appended upload'
    $truncatedHistory = Get-Content -LiteralPath $historyPath -Raw | ConvertFrom-Json
    $truncatedHistory.Uploads = @($truncatedHistory.Uploads | Where-Object { [string]$_.PackVersion -cne '1.0.9' })
    Write-SteamWorkshopJson -Path $historyPath -Value $truncatedHistory
    $truncationRejected = $false
    try { [void](Get-SteamWorkshopUploadHistory -Path $historyPath -SeedPath $seedPath -AppId 3024040 -StateName 'SerpsMods') }
    catch { $truncationRejected = $_.Exception.Message -match 'confirmed baseline entry for pack 1\.0\.9' }
    Assert-True $truncationRejected 'runtime history must fail closed if any confirmed baseline upload disappears'
} finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}

Write-Host 'Steam pack policy tests succeeded.' -ForegroundColor Green
