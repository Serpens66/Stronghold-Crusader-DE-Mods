$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'SteamPackPolicy.ps1')
. (Join-Path $PSScriptRoot 'SteamWorkshopHistory.ps1')

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Steam pack policy test failed: $Message" }
}

function Get-TestFileSha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $sha256 = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    }
    finally {
        $sha256.Dispose()
        $stream.Dispose()
    }
}

$toolOnly = @('tools/Validation/Test-WorkshopPackaging.ps1','tools/WorkshopPackage.ps1','tools/BuildProof.ps1','tools/Build.ps1','tests/Core.Tests/Core.Tests.csproj','Directory.Packages.props','CONTRIBUTING.md')
Assert-True (@(Get-SteamApiReleaseRelevantChanges $toolOnly).Count -eq 0) 'tests and maintainer tooling must not force a new APIShared package version'
$delivered = @('src/Workshop/WorkshopInstallation.cs','Properties/AssemblyInfo.cs','Override/example.xaml','Patches/example.xaml','Workshop/workshop-updater.ps1','Workshop/Bootstrap/Plugin.cs.template','tools/WorkshopUploader/Program.cs','tools/WorkshopUploader/UploadMod.bat','tools/WorkshopUploader/UploadMod-linux.sh','APIShared.csproj','info.json','Directory.Build.props')
Assert-True (@(Get-SteamApiReleaseRelevantChanges $delivered).Count -eq $delivered.Count) 'runtime, resources and shipped publisher changes must invalidate APIShared reuse'

$packagePath = 'ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp'
$rebuiltOnly = @(Get-SteamReleaseRelevantChanges -ModName 'ExtraFeatures' -PackageDirectoryPath $packagePath `
    -ChangedPaths @("$packagePath/ExtraFeatures.dll", "$packagePath/ExtraFeatures.pdb"))
Assert-True ($rebuiltOnly.Count -eq 0) 'rebuilt binaries alone must reuse the published release'
$sourceChanged = @(Get-SteamReleaseRelevantChanges -ModName 'ExtraFeatures' -PackageDirectoryPath $packagePath `
    -ChangedPaths @("$packagePath/ExtraFeatures.dll", 'ExtraFeatures/src/ExtraFeaturesRuntime.cs'))
Assert-True ($sourceChanged.Count -eq 1 -and $sourceChanged[0] -ceq 'ExtraFeatures/src/ExtraFeaturesRuntime.cs') 'changed original source must require a new release'
$resourceChanged = @(Get-SteamReleaseRelevantChanges -ModName 'ExtraFeatures' -PackageDirectoryPath $packagePath `
    -ChangedPaths @("$packagePath/Locales/en-US.txt", 'ExtraFeatures/Patches/Assets/GUI/X.xaml'))
Assert-True ($resourceChanged.Count -eq 2) 'packaged and source resources must require a new release'
$testOnly = @(Get-SteamReleaseRelevantChanges -ModName 'ExtraFeatures' -PackageDirectoryPath $packagePath `
    -ChangedPaths @('ExtraFeatures/tests/Example.Tests/Program.cs', 'ExtraFeatures/README.md'))
Assert-True ($testOnly.Count -eq 0) 'tests and documentation must not require a mod release'
$nestedSource = @(Get-SteamReleaseRelevantChanges -ModName 'ExtendedData' `
    -PackageDirectoryPath 'ExtendedData/BepInEx/plugins/ExtendedData_Serp' `
    -ChangedPaths @('ExtendedData/ExtendedData.Core/ModSettingsJson.cs'))
Assert-True ($nestedSource.Count -eq 1) 'nested project sources must require a new release'

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

$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$seedPath = Join-Path $PSScriptRoot 'SerpsMods.upload-history.seed.json'
$seed = Get-Content -LiteralPath $seedPath -Raw | ConvertFrom-Json
[void](Assert-SteamWorkshopUploadHistory -History $seed -AppId 3024040 -StateName 'SerpsMods')
$expectedVersions = @('1.0.2','1.0.3','1.0.5','1.0.6','1.0.7','1.0.8','1.0.9','1.0.10','1.0.12')
Assert-True ((@($seed.Uploads.PackVersion) -join ',') -ceq ($expectedVersions -join ',')) 'the seed must contain exactly the nine confirmed public Steam versions'
Assert-True ('1.0.4' -notin @($seed.Uploads.PackVersion) -and '1.0.11' -notin @($seed.Uploads.PackVersion)) 'non-uploaded GitHub releases must be excluded'
Assert-True ('1.0.0' -notin @($seed.Uploads.PackVersion) -and '1.0.1' -notin @($seed.Uploads.PackVersion) -and
    ($seed | ConvertTo-Json -Depth 10) -notmatch 'ExtendedData') 'private predecessors and ExtendedData must be excluded'

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
    Assert-True ((Get-TestFileSha256 $mapPath) -ceq [string]$upload.MapSha256) "confirmed map v$($upload.PackVersion) hash must match history"
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
$missingApiRelease = Resolve-ApiSharedReleaseAction -PreparedVersion '0.3.7' -PublishedVersion $null
Assert-True $missingApiRelease.NeedsRelease 'a missing APIShared release must be planned automatically'
$newerApiRelease = Resolve-ApiSharedReleaseAction -PreparedVersion '0.3.7' -PublishedVersion '0.3.6'
Assert-True $newerApiRelease.NeedsRelease 'a prepared newer APIShared version must be released'
$currentApiRelease = Resolve-ApiSharedReleaseAction -PreparedVersion '0.3.7' -PublishedVersion '0.3.7' `
    -PublishedContentIsCurrent $true -PublishedArtifactIsValid $true
Assert-True (-not $currentApiRelease.NeedsRelease) 'a current valid APIShared release must be reused'
$staleSameVersionRejected = $false
try {
    [void](Resolve-ApiSharedReleaseAction -PreparedVersion '0.3.7' -PublishedVersion '0.3.7' `
        -PublishedContentIsCurrent $false -PublishedArtifactIsValid $true)
} catch {
    $staleSameVersionRejected = $_.Exception.Message -match 'changed packaged content but no prepared higher version/changelog'
}
Assert-True $staleSameVersionRejected 'changed APIShared content at the published version must require a higher prepared version'
$invalidApiArtifactRejected = $false
try {
    [void](Resolve-ApiSharedReleaseAction -PreparedVersion '0.3.7' -PublishedVersion '0.3.7' `
        -PublishedContentIsCurrent $true -PublishedArtifactIsValid $false)
} catch {
    $invalidApiArtifactRejected = $_.Exception.Message -match 'missing or invalid and cannot be replaced automatically'
}
Assert-True $invalidApiArtifactRejected 'an invalid published APIShared artifact must fail closed'
$createScriptText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Create-SteamModPack.ps1'))
$hostBuildText = [IO.File]::ReadAllText((Join-Path $workspace 'SerpsModsHost\build.bat'))
Assert-True ($hostBuildText.Contains('if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"')) 'host build must honor the APIShared release override'
Assert-True ($hostBuildText.Contains('if not exist "%API_SHARED_DIR%\APIShared.dll" (')) 'host build must reject a missing APIShared DLL before clearing output'
Assert-True ($hostBuildText.Contains('echo APIShared.dll wurde nicht gefunden: !API_SHARED_DIR!\APIShared.dll')) 'host build must identify the missing APIShared DLL path'
Assert-True ($hostBuildText.IndexOf('if not exist "%API_SHARED_DIR%\APIShared.dll" (') -lt $hostBuildText.IndexOf('rmdir /S /Q "%LOCAL_PLUGIN_DIR%"')) 'host build must validate APIShared before deleting local output'
Assert-True ($hostBuildText.Contains('/p:ApiSharedDir="%API_SHARED_DIR%"')) 'host build must pass APIShared directory to MSBuild'
Assert-True ($createScriptText.Contains('Assert-ApiConsumerPackage $script:Root $apiSharedInfrastructure.Directory')) 'Steam staging must revalidate the published SDK before building the host'
Assert-True ($createScriptText.Contains('$env:SHCDE_API_SHARED_RELEASE_PROOF = $apiPackage.ProvenancePath')) 'Consumer builds must inherit the verified published SDK proof'
Assert-True ($createScriptText.Contains('$apiSharedAssembly.Version -cne [string]$apiSharedSourceInfo.Version')) 'Steam staging must compare the APIShared DLL version with the source manifest'
Assert-True (-not $createScriptText.Contains('releaseConfig.ApiShared.Version')) 'Steam staging must not depend on a duplicated configured APIShared version'
Assert-True ($createScriptText.Contains('Get-ApiSharedReleasePackage -Infrastructure $apiSharedInfrastructure')) 'Steam staging must consume a validated published APIShared package'
Assert-True ($createScriptText.Contains('SHA-256 file mismatch for $tag.')) 'APIShared release validation must reject a mismatched hash file'
Assert-True ($createScriptText.Contains('Provenance mismatch for $tag.')) 'APIShared release validation must reject mismatched provenance'
Assert-True ($createScriptText.Contains('APIShared release ZIP must contain exactly the BepInEx root directory.')) 'APIShared release validation must enforce the package root'
Assert-True ($createScriptText.Contains('APIShared release DLL identity/dependency mismatch for $tag.')) 'APIShared release validation must enforce DLL identity and dependencies'
Assert-True ($createScriptText.Contains('$assets = @($ZipPath,$ZipShaPath,$MapPath,$MapShaPath,$ProvenancePath)')) 'pack releases must publish ZIP, ZIP hash, map, map hash and provenance together'
Assert-True ($createScriptText.Contains('Package = [ordered]@{ File = $zipPackage.File; RootDirectory = $zipPackage.RootDirectory; Sha256 = $zipPackage.Sha256; Size = $zipPackage.Size; Files = $zipPackage.Files }')) 'pack provenance must describe the audited ZIP package'
Assert-True (-not $createScriptText.Contains('Update-ReleaseIndex.ps1')) 'pack preparation must leave README unchanged'
$outerPathGuard = $createScriptText.IndexOf('if (-not $path.StartsWith($packageRoot, [StringComparison]::OrdinalIgnoreCase)) { continue }', [StringComparison]::Ordinal)
Assert-True ($outerPathGuard -ge 0) 'existing-tombstone detection must skip outer map files before applying the internal package path policy'
$archiveTraversalGuard = $createScriptText.IndexOf("Where-Object { `$_ -in @('','.', '..') }", [StringComparison]::Ordinal)
Assert-True ($archiveTraversalGuard -ge 0) 'historical map inventory must reject empty, current-directory and parent-directory path segments'
$preflightCall = $createScriptText.IndexOf('$historicalSteamFiles = Get-HistoricalSteamFileIndex', [StringComparison]::Ordinal)
$apiReleaseCall = $createScriptText.IndexOf('Invoke-ApiSharedRelease $apiSharedInfrastructure', [StringComparison]::Ordinal)
$releaseCall = $createScriptText.IndexOf('Invoke-ModRelease $mod', [StringComparison]::Ordinal)
Assert-True ($preflightCall -ge 0 -and $releaseCall -gt $preflightCall) 'verified historical archive inventory must run before GitHub mod releases'
Assert-True ($apiReleaseCall -lt 0) 'SerpsMods preparation must not publish APIShared implicitly'
Assert-True (-not $createScriptText.Contains('Assert-WorkshopPlatformProof')) 'manual releases must not require platform approval'
Assert-True ($createScriptText.Contains('Prepare-SerpsModsWorkshop.ps1') -and $createScriptText.Contains('-WorkshopZipPath $workshopZipPath')) 'release must include loose Workshop content and the dedicated bootstrap'
Assert-True (-not $createScriptText.Contains('$historicalReplacements = @(Add-HistoricalSteamReplacements')) 'new loose releases must not add historical retention overlays'
Assert-True ($createScriptText.Contains('Assert-WorkshopContentZip $workshopZip') -and $createScriptText.Contains('ExtractToDirectory($workshopZip,$reuseStage)')) 'reused releases must validate and restore the full ZIP before replacing output'
Assert-True ($createScriptText.Contains('ReleaseUrl = $apiSharedInfrastructure.ReleaseUrl')) 'pack provenance must use the validated APIShared release URL'
Assert-True ($createScriptText.Contains('SourceCommit = $apiSharedInfrastructure.SourceCommit')) 'pack provenance must use the APIShared release source commit'
$uploadScriptText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Upload-Workshop.ps1'))
$steamConfirmationCheck = $uploadScriptText.IndexOf("`$result.Status -cne 'Succeeded'", [StringComparison]::Ordinal)
$historyAppendCall = $uploadScriptText.LastIndexOf('Add-SteamWorkshopUploadHistoryEntry', [StringComparison]::Ordinal)
Assert-True ($steamConfirmationCheck -ge 0 -and $historyAppendCall -gt $steamConfirmationCheck) 'upload history may only be appended after Steam confirms the update'
Assert-True ((Assert-ScriptExtenderXamlPatchContract -Directory (Join-Path $workspace 'APIShared')) -gt 0) 'the delivered APIShared XAML patches must satisfy the single-root contract'
Assert-True ((Assert-ScriptExtenderXamlPatchContract -Directory (Join-Path $workspace 'BugfixesAndQoL')) -gt 0) 'the delivered BugfixesAndQoL XAML patches, including empty tombstones, must satisfy the contract'

. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
$testRoot = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -Kind t -Name 'SteamPackPolicy'
try {
    $zipPackage = Join-Path $testRoot 'zip-stage\SerpsMods_Serp'
    [void](New-Item -ItemType Directory -Path (Join-Path $zipPackage 'Mods\Example_Serp') -Force)
    [IO.File]::WriteAllText((Join-Path $zipPackage 'SerpsModsHost.dll'), 'host')
    $cleanupRoot = Join-Path $testRoot 'host-cleanup'
    [IO.Directory]::CreateDirectory((Join-Path $cleanupRoot 'Infrastructure\APIShared_Serp')) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $cleanupRoot 'LobbyModSettings')) | Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $cleanupRoot 'OldHostFiles')) | Out-Null
    $installedApi = Join-Path $cleanupRoot 'Infrastructure\APIShared_Serp\APIShared.dll'
    $savedSettings = Join-Path $cleanupRoot 'LobbyModSettings\settings.json'
    [IO.File]::WriteAllText($installedApi, 'independently installed APIShared')
    [IO.File]::WriteAllText($savedSettings, 'player settings')
    [IO.File]::WriteAllText((Join-Path $cleanupRoot 'OldHostFiles\old.txt'), 'old host output')
    [IO.File]::WriteAllText((Join-Path $cleanupRoot 'old-host.dll'), 'old host output')
    if (-not [IO.Path]::GetFullPath($cleanupRoot).StartsWith([IO.Path]::GetFullPath($testRoot) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe cleanup fixture path' }
    $cleanupBlock = [regex]::Match($hostBuildText, '(?s)if exist "%GAME_PLUGIN_DIR%\\" \(.*?(?=xcopy "%LOCAL_PLUGIN_DIR%")').Value
    Assert-True (-not [string]::IsNullOrEmpty($cleanupBlock)) 'host installation cleanup must be available for the fixture'
    $cleanupDriver = Join-Path $testRoot 'host-cleanup.bat'
    $cleanupText = '@echo off' + "`r`nsetlocal EnableExtensions EnableDelayedExpansion`r`n" + 'set "GAME_PLUGIN_DIR=' + $cleanupRoot + '"' + "`r`n" + $cleanupBlock + "exit /b 0`r`n:copy_failed`r`nexit /b 1`r`n"
    [IO.File]::WriteAllText($cleanupDriver, $cleanupText, [Text.UTF8Encoding]::new($false))
    & $cleanupDriver
    Assert-True ($LASTEXITCODE -eq 0) 'host cleanup fixture must succeed'
    Assert-True ([IO.File]::ReadAllText($installedApi) -ceq 'independently installed APIShared') 'host installation must preserve independently installed APIShared infrastructure'
    Assert-True ([IO.File]::ReadAllText($savedSettings) -ceq 'player settings') 'host installation must preserve player settings'
    Assert-True (-not (Test-Path -LiteralPath (Join-Path $cleanupRoot 'OldHostFiles')) -and -not (Test-Path -LiteralPath (Join-Path $cleanupRoot 'old-host.dll'))) 'host cleanup must remove obsolete host output'
    $localApi = Join-Path $testRoot 'local-api.dll'
    $publishedApi = Join-Path $testRoot 'published-api.dll'
    [IO.File]::WriteAllBytes($localApi, [byte[]]@(1,2,3,4))
    [IO.File]::WriteAllBytes($publishedApi, [byte[]]@(1,2,3,4))
    Assert-SteamApiSharedBinary -LocalDll $localApi -PublishedDll $publishedApi
    [IO.File]::WriteAllBytes($publishedApi, [byte[]]@(1,2,3,5))
    $differentApiRejected = $false
    try { Assert-SteamApiSharedBinary -LocalDll $localApi -PublishedDll $publishedApi }
    catch { $differentApiRejected = $_.Exception.Message -match 'differs from the local DLL' }
    Assert-True $differentApiRejected 'a different published APIShared DLL must block staging'
    $missingApiRejected = $false
    try { Assert-SteamApiSharedBinary -LocalDll (Join-Path $testRoot 'missing.dll') -PublishedDll $publishedApi }
    catch { $missingApiRejected = $true }
    Assert-True $missingApiRejected 'a missing local APIShared DLL must block staging'
    [IO.File]::WriteAllText((Join-Path $zipPackage 'Mods\Example_Serp\Example.dll'), 'example')
    $zipPath = Join-Path $testRoot 'SerpsMods-v9.8.7.zip'
    $zipRecord = New-SteamPackReleaseZip -PackageDirectory $zipPackage -DestinationPath $zipPath
    Assert-True ($zipRecord.File -ceq 'SerpsMods-v9.8.7.zip') 'the release ZIP must retain its versioned filename'
    Assert-True ($zipRecord.RootDirectory -ceq 'SerpsMods_Serp') 'the release ZIP must contain the pack directory as its single root'
    Assert-True ($zipRecord.Sha256 -ceq (Get-TestFileSha256 $zipPath)) 'the release ZIP record must contain the archive SHA-256'
    Assert-True ($zipRecord.Files.Count -eq 2) 'the release ZIP audit must record every staged pack file'
    Assert-True (@($zipRecord.Files | Where-Object { [string]$_.Path -ceq 'SerpsMods_Serp/Mods/Example_Serp/Example.dll' }).Count -eq 1) 'the release ZIP must preserve nested pack paths below its root directory'

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

Assert-True ($createScriptText.Contains("State = 'External'; RelativePath = ''")) 'APIShared must be recorded as an external prerequisite'
Assert-True ($createScriptText.Contains('EXTERNAL-V1|')) 'old bundled content signatures must not be reused'
Assert-True ($createScriptText.Contains('Get-LegacyApiSharedFiles -History $uploadHistory')) 'migration evidence must cover confirmed historical deliveries'
Assert-True ($createScriptText.Contains('New-ApiWorkshopInventory -Directory $hostStage -LegacyFiles $legacyApiSharedFiles')) 'the final host payload must ship its historical migration inventory'
Assert-True ($createScriptText.Contains("-Arguments @('/nopause','/noinstall') -FailureCode 4")) 'compatibility builds must not install'
Assert-True ($createScriptText.IndexOf("Fail-Pack 2 'Prepare APIShared first") -lt $createScriptText.IndexOf('foreach ($mod in @($mods | Where-Object NeedsSourceEdit)) { Set-ModCompatibility')) 'missing APIShared releases must fail before consumer source changes'
foreach ($modName in @('BugfixesAndQoL','BuildingCosts','BuildingLimit','ExtraFeatures','RandomEvents','CastlePlanner','StartConditions','UnitCosts','UnitLimit','ExtendedData')) {
    $driver = [IO.File]::ReadAllText((Join-Path $workspace "$modName/build.bat"))
    Assert-True ($driver.Contains('if "%NO_INSTALL%"=="1" goto prepared_no_install')) "$modName must support preparation without touching installed files"
}
