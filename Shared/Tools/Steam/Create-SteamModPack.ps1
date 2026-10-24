param(
    [Parameter(Mandatory)][string]$PackName,
    [Parameter(Mandatory)][string]$PackGuid,
    [Parameter(Mandatory)][uint32]$SteamAppId,
    [Parameter(Mandatory)][string]$SteamStateName,
    [Parameter(Mandatory)][string]$WorkshopPackagerPath,
    [Parameter(Mandatory)][string]$PreviewPath,
    [string]$ApiSharedPreviewPath,
    [string]$ApiSharedWorkshopItemId,
    [ValidateSet('main','develop')][string]$ModpackBranch='main',
    [switch]$Validate,
    [switch]$TestMode,
    [switch]$NoPause
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$previousApiSharedDirectory = $env:SHCDE_API_SHARED_DIR
$previousApiSharedReleaseProof = $env:SHCDE_API_SHARED_RELEASE_PROOF
$previousApiSharedReleaseProofHash = $env:SHCDE_API_SHARED_RELEASE_PROOF_SHA256
$previousApiSharedTestMode = $env:SHCDE_WORKSHOP_TEST
. (Join-Path $PSScriptRoot '../ApiSharedRepository/MainSource.Common.ps1')
. (Join-Path $PSScriptRoot 'ApiSharedSdkPolicy.ps1')
$mainWorkspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if(-not $env:SHCDE_API_SHARED_MAIN_SELECTION){$env:SHCDE_API_SHARED_MAIN_SELECTION=Resolve-ApiMainSelection $mainWorkspace}
if(-not $Validate){Ensure-ApiMainBuild $mainWorkspace;Ensure-ApiMainConsumerSource $mainWorkspace}
$script:ApiMainRoot=Get-WorkshopApiRoot $mainWorkspace
$previousGameDirectory = $env:SHCDE_GAME_DIR
$previousExtenderDirectory = $env:SHCDESE_EXTENDER_DIR
$previousMsBuild = $env:SHCDE_MSBUILD
$previousNativeBaselineRoot = $env:SHCDE_NATIVE_BASELINE_ROOT
# As in branch builds: the Git-ignored native databases exist only in the coordinator workspace.
if (-not $env:SHCDE_NATIVE_BASELINE_ROOT) {
    $nativeBaselineRoot = Join-Path (Get-ApiMainWorkspace $mainWorkspace) '_inspect/CrusaderDE-Native-Baseline'
    if (Test-Path -LiteralPath (Join-Path $nativeBaselineRoot 'CURRENT.json')) { $env:SHCDE_NATIVE_BASELINE_ROOT = $nativeBaselineRoot }
}
. (Join-Path $PSScriptRoot 'SteamPackPolicy.ps1')
. (Join-Path $PSScriptRoot 'SteamWorkshopHistory.ps1')
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot 'ReleaseAssetCache.ps1')
. (Join-Path $script:ApiMainRoot 'tools/WorkshopPackage.ps1')

class SteamPackFailure : System.Exception {
    [int]$ExitCode
    SteamPackFailure([int]$exitCode, [string]$message) : base($message) { $this.ExitCode = $exitCode }
}

$script:Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$script:OutputRoot = Join-Path $script:Root '.release-output\SerpsMods'
if ($TestMode) { $script:OutputRoot = Join-Path $script:Root '.release-output\SteamWorkshop\test\SerpsMods' }
$script:RunId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$script:LogDir = Join-Path $script:OutputRoot 'logs'
$script:LogPath = Join-Path $script:LogDir "$($script:RunId).log"
$script:JournalPath = Join-Path $script:OutputRoot 'journal.json'
$script:Repository = 'Serpens66/Stronghold-Crusader-DE-Mods'
$script:Branch = $ModpackBranch
if($ModpackBranch -eq 'develop' -and -not $TestMode){throw 'Develop requires private TestMode.'}
$script:CompatibilityChange = 'Added optional Serps Mods Host dependency for Workshop mod-pack load ordering.'
$script:CecilSourcePath = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\core\Mono.Cecil.dll'
$script:CecilLoadPath = $null
$script:BepInExCorePath = Split-Path -Parent $script:CecilSourcePath
# The Git-ignored local Extender checkout exists only in the coordinator workspace, not in release worktrees.
$script:ScriptExtenderRoot = @($script:Root, (Get-ApiMainWorkspace $script:Root)) | ForEach-Object { Join-Path $_ 'shcde-script-extender' } |
    Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
if (-not $script:ScriptExtenderRoot) { $script:ScriptExtenderRoot = Join-Path $script:Root 'shcde-script-extender' }
$script:ScriptExtenderOutputPath = Join-Path $script:ScriptExtenderRoot 'src\SHCDESE.BepInEx\bin\net481'
$script:ReleaseList = @()
$script:UploadHistoryPath = Join-Path (Get-ApiMainWorkspace $script:Root) ".release-output\SteamWorkshop\items\$SteamAppId-$SteamStateName.upload-history.json"
$script:UploadHistorySeedPath = Join-Path $PSScriptRoot "$SteamStateName.upload-history.seed.json"

[void](New-Item -ItemType Directory -Path $script:LogDir -Force)

function Write-RunLog {
    param([string]$Message, [ValidateSet('INFO','WARN','ERROR','OK')][string]$Level = 'INFO')
    $line = '[{0}] [{1}] {2}' -f ([DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss.fff')), $Level, $Message
    [IO.File]::AppendAllText($script:LogPath, $line + "`r`n", [Text.UTF8Encoding]::new($false))
    $color = switch ($Level) { 'ERROR' { 'Red' } 'WARN' { 'Yellow' } 'OK' { 'Green' } default { 'Gray' } }
    Write-Host $line -ForegroundColor $color
}

function Fail-Pack {
    param([int]$Code, [string]$Message)
    throw [SteamPackFailure]::new($Code, $Message)
}

function Write-Utf8CrLf {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Text)
    $normalized = ($Text -replace "`r?`n", "`r`n").TrimEnd("`r", "`n") + "`r`n"
    [IO.File]::WriteAllText($Path, $normalized, [Text.UTF8Encoding]::new($false))
    $actual = [IO.File]::ReadAllText($Path, [Text.UTF8Encoding]::new($false))
    if (-not [string]::Equals($actual, $normalized, [StringComparison]::Ordinal)) {
        Fail-Pack 3 "CRLF write verification failed: $Path"
    }
}

function Write-JsonCrLf {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)]$Value)
    Write-Utf8CrLf -Path $Path -Text ($Value | ConvertTo-Json -Depth 20)
}

function Find-JsonArrayEnd {
    param([Parameter(Mandatory)][string]$Text, [Parameter(Mandatory)][int]$OpenIndex)
    $depth = 0
    $inString = $false
    $escaped = $false
    for ($index = $OpenIndex; $index -lt $Text.Length; $index++) {
        $character = $Text[$index]
        if ($inString) {
            if ($escaped) { $escaped = $false; continue }
            if ($character -eq '\') { $escaped = $true; continue }
            if ($character -eq '"') { $inString = $false }
            continue
        }
        if ($character -eq '"') { $inString = $true; continue }
        if ($character -eq '[') { $depth++ }
        elseif ($character -eq ']') {
            $depth--
            if ($depth -eq 0) { return $index }
        }
    }
    Fail-Pack 3 "Unterminated JSON array starting at character $OpenIndex."
}

function Update-ModManifestText {
    param(
        [Parameter(Mandatory)][string]$Text,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][bool]$AddChangelogEntry,
        [Parameter(Mandatory)][bool]$AddCompatibilityChange
    )
    $newline = if ($Text.Contains("`r`n")) { "`r`n" } else { "`n" }
    $versionMatch = [regex]::Match($Text, '(?m)^(\s*"Version"\s*:\s*")[^"]+("\s*,?\s*)$')
    if (-not $versionMatch.Success) { Fail-Pack 3 'Could not locate the top-level manifest Version property.' }
    $Text = $Text.Substring(0, $versionMatch.Index) +
        $versionMatch.Groups[1].Value + $Version + $versionMatch.Groups[2].Value +
        $Text.Substring($versionMatch.Index + $versionMatch.Length)

    if (-not $AddChangelogEntry -and -not $AddCompatibilityChange) { return $Text }
    $changelogMatch = [regex]::Match($Text, '"SerpChangelog"\s*:\s*\[')
    if (-not $changelogMatch.Success) { Fail-Pack 3 'Could not locate the SerpChangelog array.' }
    $changelogOpen = $Text.IndexOf('[', $changelogMatch.Index)

    if ($AddChangelogEntry) {
        $lineStart = $Text.LastIndexOf("`n", $changelogMatch.Index)
        $propertyIndent = $Text.Substring($lineStart + 1, $changelogMatch.Index - $lineStart - 1)
        $entryIndent = $propertyIndent + '  '
        $memberIndent = $entryIndent + '  '
        $valueIndent = $memberIndent + '  '
        $escapedChange = $script:CompatibilityChange | ConvertTo-Json -Compress
        $entry = $newline + $entryIndent + '{' +
            $newline + $memberIndent + '"Version": "' + $Version + '",' +
            $newline + $memberIndent + '"Changes": [' +
            $newline + $valueIndent + $escapedChange +
            $newline + $memberIndent + ']' +
            $newline + $entryIndent + '}'
        $close = Find-JsonArrayEnd -Text $Text -OpenIndex $changelogOpen
        $existing = $Text.Substring($changelogOpen + 1, $close - $changelogOpen - 1)
        if (-not [string]::IsNullOrWhiteSpace($existing)) { $entry += ',' }
        return $Text.Insert($changelogOpen + 1, $entry)
    }

    $tail = $Text.Substring($changelogOpen + 1)
    $entryMatch = [regex]::Match($tail, '"Version"\s*:\s*"' + [regex]::Escape($Version) + '"')
    if (-not $entryMatch.Success) { Fail-Pack 3 "Could not locate changelog entry for version $Version." }
    $entryIndex = $changelogOpen + 1 + $entryMatch.Index
    $changesMatch = [regex]::Match($Text.Substring($entryIndex), '"Changes"\s*:\s*\[')
    if (-not $changesMatch.Success) { Fail-Pack 3 "Could not locate Changes for version $Version." }
    $changesOpen = $entryIndex + $Text.Substring($entryIndex).IndexOf('[', $changesMatch.Index)
    $changesClose = Find-JsonArrayEnd -Text $Text -OpenIndex $changesOpen
    $changesContent = $Text.Substring($changesOpen + 1, $changesClose - $changesOpen - 1)
    $lastValueIndex = $changesContent.Length - 1
    while ($lastValueIndex -ge 0 -and [char]::IsWhiteSpace($changesContent[$lastValueIndex])) { $lastValueIndex-- }
    if ($lastValueIndex -lt 0) { Fail-Pack 3 "Empty Changes array for version $Version is not supported." }
    $changesPropertyIndex = $entryIndex + $changesMatch.Index
    $lineStart = $Text.LastIndexOf("`n", $changesPropertyIndex)
    $propertyIndent = $Text.Substring($lineStart + 1, $changesPropertyIndex - $lineStart - 1)
    $valueIndent = $propertyIndent + '  '
    $escapedChange = $script:CompatibilityChange | ConvertTo-Json -Compress
    return $Text.Insert($changesOpen + 1 + $lastValueIndex + 1, ',' + $newline + $valueIndent + $escapedChange)
}

function Get-Sha256 {
    param([Parameter(Mandatory)][string]$Path)
    $stream = [IO.File]::Open(
        $Path,
        [IO.FileMode]::Open,
        [IO.FileAccess]::Read,
        [IO.FileShare]::Read)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString(
            $algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
    } finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}

function Resolve-CecilLoadPath {
    if ($null -ne $script:CecilLoadPath) { return $script:CecilLoadPath }
    if (-not (Test-Path -LiteralPath $script:CecilSourcePath -PathType Leaf)) {
        Fail-Pack 2 "Mono.Cecil not found: $($script:CecilSourcePath)"
    }

    $sourceHash = Get-Sha256 $script:CecilSourcePath
    $cacheDirectory = Join-Path $script:OutputRoot "tools\Mono.Cecil\$sourceHash"
    $cachePath = Join-Path $cacheDirectory 'Mono.Cecil.dll'
    [void](New-Item -ItemType Directory -Path $cacheDirectory -Force)

    if (-not (Test-Path -LiteralPath $cachePath -PathType Leaf) -or
        (Get-Sha256 $cachePath) -ne $sourceHash) {
        # Writing the bytes to a new local file intentionally omits an inherited
        # Zone.Identifier that can make Add-Type reject the game-directory DLL.
        [IO.File]::WriteAllBytes(
            $cachePath,
            [IO.File]::ReadAllBytes($script:CecilSourcePath))
    }
    $cacheHash = Get-Sha256 $cachePath
    if ($cacheHash -ne $sourceHash) {
        Fail-Pack 2 "Mono.Cecil cache hash mismatch: source=$sourceHash, cache=$cacheHash."
    }

    $script:CecilLoadPath = $cachePath
    Write-RunLog "Prepared trusted Mono.Cecil cache copy: $cachePath (sha256=$cacheHash)." 'OK'
    return $script:CecilLoadPath
}

function Invoke-Checked {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$Arguments = @(),
        [int]$FailureCode = 2,
        [string]$WorkingDirectory = $script:Root,
        [switch]$AllowFailure
    )
    Write-RunLog "RUN: $FilePath $($Arguments -join ' ')"
    Push-Location $WorkingDirectory
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # Git and other native tools legitimately use stderr for progress output.
        # Capture that output for the log and decide success from the exit code.
        $ErrorActionPreference = 'Continue'
        $output = @(& $FilePath @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
        Pop-Location
    }
    foreach ($line in $output) { Write-RunLog ([string]$line) }
    if ($exitCode -ne 0 -and -not $AllowFailure) {
        Fail-Pack $FailureCode "Command failed with exit code ${exitCode}: $FilePath"
    }
    return [pscustomobject]@{ ExitCode = $exitCode; Output = @($output | ForEach-Object { [string]$_ }) }
}

function Invoke-Git {
    param([string[]]$Arguments, [int]$FailureCode = 5, [switch]$AllowFailure)
    return Invoke-Checked -FilePath 'git' -Arguments (@('-C', $script:Root) + $Arguments) -FailureCode $FailureCode -AllowFailure:$AllowFailure
}

function Get-ModNames {
    $raw = [Environment]::GetEnvironmentVariable('SERPS_STEAM_MODS')
    if ([string]::IsNullOrWhiteSpace($raw)) { Fail-Pack 2 'SERPS_STEAM_MODS is empty.' }
    $mods = @($raw.Split('|') | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    if ($mods.Count -eq 0) { Fail-Pack 2 'The configured mod list is empty.' }
    $duplicates = @($mods | Group-Object | Where-Object Count -gt 1)
    if ($duplicates.Count -gt 0) { Fail-Pack 2 "Duplicate mod names: $(@($duplicates.Name) -join ', ')" }
    return $mods
}

function Test-PublicBooleanProperty {
    param(
        [Parameter(Mandatory)][array]$SourceFiles,
        [Parameter(Mandatory)][string]$PropertyName
    )

    $propertyPattern = '(?s)public\s+bool\s+' +
        [regex]::Escape($PropertyName) + '\s*(?:\{|=>)'
    foreach ($sourceFile in $SourceFiles) {
        if ([IO.File]::ReadAllText($sourceFile.FullName) -match $propertyPattern) {
            return $true
        }
    }
    return $false
}

function Test-ClassifiedBooleanProperty {
    param(
        [Parameter(Mandatory)][array]$SourceFiles,
        [Parameter(Mandatory)][string]$PropertyName,
        [Parameter(Mandatory)][string]$ClassificationPattern
    )

    $propertyPattern = '(?s)' + $ClassificationPattern + '.{0,500}?public\s+bool\s+' +
        [regex]::Escape($PropertyName) + '\s*(?:\{|=>)'
    foreach ($sourceFile in $SourceFiles) {
        if ([IO.File]::ReadAllText($sourceFile.FullName) -match $propertyPattern) {
            return $true
        }
    }
    return $false
}

function Test-ActivationSettingContract {
    param(
        [Parameter(Mandatory)][string]$ModName,
        [Parameter(Mandatory)][string]$ModDirectory,
        [Parameter(Mandatory)][array]$SourceFiles
    )

    $xamlFiles = @(Get-ChildItem -LiteralPath $ModDirectory -Recurse -File -Filter '*.xaml' | Where-Object {
        $_.FullName -notmatch '\\(bin|obj)\\'
    })
    $contracts = @()
    foreach ($xamlFile in $xamlFiles) {
        try {
            [xml]$document = [IO.File]::ReadAllText($xamlFile.FullName)
        } catch {
            Fail-Pack 3 "Invalid Modsettings XAML for ${ModName}: $($xamlFile.FullName): $($_.Exception.Message)"
        }

        $checkBoxes = @($document.SelectNodes("//*[local-name()='CheckBox']"))
        foreach ($checkBox in $checkBoxes) {
            $role = $null
            $ancestor = $checkBox.ParentNode
            while ($null -ne $ancestor -and $null -eq $role) {
                if ($ancestor.LocalName -eq 'Border') {
                    $style = $ancestor.GetAttribute('Style')
                    if ($style -match 'HostActivationBorder') { $role = 'Host' }
                    elseif ($style -match 'ClientActivationBorder') { $role = 'Client' }
                }
                $ancestor = $ancestor.ParentNode
            }
            if ($null -eq $role) { continue }

            $binding = $checkBox.GetAttribute('IsChecked')
            $bindingMatch = [regex]::Match($binding, '^\{Binding\s+([A-Za-z_][A-Za-z0-9_]*)\b')
            if (-not $bindingMatch.Success) {
                Fail-Pack 3 "${ModName} has an activation CheckBox without a direct boolean IsChecked binding in $($xamlFile.FullName)."
            }
            $contracts += [pscustomobject]@{
                Role = $role
                Property = $bindingMatch.Groups[1].Value
                Xaml = $xamlFile.FullName
            }
        }
    }

    $contracts = @($contracts | Sort-Object Role, Property -Unique)
    if ($contracts.Count -eq 0) {
        Fail-Pack 3 "$ModName does not expose a recognizable activation setting contract in its Modsettings XAML."
    }

    $hostClassificationPattern = '\[(?:Shared\.)?SyncHostOnly(?:Attribute)?\]'
    $clientClassificationPattern = '\[(?:(?:APIShared\.ModSettings\.)?PresetLocal(?:Attribute)?|(?:Shared\.)?SyncPerPlayer(?:Attribute)?)\]'
    $usesSharedActivationProxy = $false

    foreach ($contract in $contracts) {
        $isSharedProxy =
            ($contract.Role -eq 'Host' -and $contract.Property -eq 'HostSettingsEnabled') -or
            ($contract.Role -eq 'Client' -and $contract.Property -eq 'ClientSettingsEnabled')
        if ($isSharedProxy) {
            # These UI properties are non-persisted facades. LobbyPresetController resolves
            # them to classified EnableMod/EnableClientFeatures properties at runtime.
            $usesSharedActivationProxy = $true
            continue
        }

        $classificationPattern = if ($contract.Role -eq 'Host') {
            $hostClassificationPattern
        } else {
            $clientClassificationPattern
        }
        if (-not (Test-ClassifiedBooleanProperty -SourceFiles $SourceFiles -PropertyName $contract.Property -ClassificationPattern $classificationPattern)) {
            $expectedClassification = if ($contract.Role -eq 'Host') { 'SyncHostOnly' } else { 'PresetLocal or SyncPerPlayer' }
            Fail-Pack 3 "${ModName} activation property '$($contract.Property)' must be a public bool classified as $expectedClassification."
        }
    }

    $backingSummary = @()
    if ($usesSharedActivationProxy) {
        $enableModExists = Test-PublicBooleanProperty -SourceFiles $SourceFiles -PropertyName 'EnableMod'
        $enableModIsHost = Test-ClassifiedBooleanProperty -SourceFiles $SourceFiles -PropertyName 'EnableMod' -ClassificationPattern $hostClassificationPattern
        $enableModIsClient = Test-ClassifiedBooleanProperty -SourceFiles $SourceFiles -PropertyName 'EnableMod' -ClassificationPattern $clientClassificationPattern
        $enableClientFeaturesExists = Test-PublicBooleanProperty -SourceFiles $SourceFiles -PropertyName 'EnableClientFeatures'
        $enableClientFeaturesIsClient = Test-ClassifiedBooleanProperty -SourceFiles $SourceFiles -PropertyName 'EnableClientFeatures' -ClassificationPattern $clientClassificationPattern

        if ($enableModExists -and -not ($enableModIsHost -or $enableModIsClient)) {
            Fail-Pack 3 "${ModName} shared activation backing property 'EnableMod' must be classified as SyncHostOnly, PresetLocal or SyncPerPlayer."
        }
        if ($enableClientFeaturesExists -and -not $enableClientFeaturesIsClient) {
            Fail-Pack 3 "${ModName} shared activation backing property 'EnableClientFeatures' must be classified as PresetLocal or SyncPerPlayer."
        }
        if (-not ($enableModIsHost -or $enableModIsClient -or $enableClientFeaturesIsClient)) {
            Fail-Pack 3 "${ModName} uses shared activation bindings but declares no classified EnableMod or EnableClientFeatures backing property."
        }

        $hostBacking = if ($enableModIsHost) { 'EnableMod' } else { '(hidden)' }
        $clientBacking = if ($enableClientFeaturesIsClient) {
            'EnableClientFeatures'
        } elseif ($enableModIsClient) {
            'EnableMod'
        } else {
            '(hidden)'
        }
        $backingSummary += "shared backings Host:$hostBacking, Client:$clientBacking"
    }

    $summaryParts = @($contracts | ForEach-Object { "$($_.Role):$($_.Property)" }) + $backingSummary
    $summary = $summaryParts -join ', '
    Write-RunLog "Activation setting contract for ${ModName}: $summary"
}

function Get-PluginSourceMetadata {
    param([string]$ModName)
    $modDir = Join-Path $script:Root $ModName
    $buildBat = Join-Path $modDir 'build.bat'
    $releaseBat = Join-Path $modDir 'release.bat'
    if (-not (Test-Path -LiteralPath $modDir -PathType Container)) { Fail-Pack 2 "Missing mod directory: $modDir" }
    if (-not (Test-Path -LiteralPath $buildBat -PathType Leaf)) { Fail-Pack 2 "Missing build.bat for ${ModName}: $buildBat" }
    if (-not (Test-Path -LiteralPath $releaseBat -PathType Leaf)) { Fail-Pack 2 "Missing release.bat for ${ModName}: $releaseBat" }

    $sourceFiles = @(Get-ChildItem -LiteralPath $modDir -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\(bin|obj|BepInEx)\\' })
    $pluginSources = @()
    foreach ($file in $sourceFiles) {
        $text = [IO.File]::ReadAllText($file.FullName)
        if ($text -match '\[BepInPlugin\s*\(') { $pluginSources += $file }
    }
    if ($pluginSources.Count -ne 1) { Fail-Pack 3 "Expected exactly one BepInPlugin source for $ModName, found $($pluginSources.Count)." }
    $pluginText = [IO.File]::ReadAllText($pluginSources[0].FullName)
    $guidMatch = [regex]::Match($pluginText, 'PluginGuid\s*=\s*"([^"]+)"')
    $versionMatch = [regex]::Match($pluginText, 'PluginVersion\s*=\s*"([^"]+)"')
    if (-not $guidMatch.Success -or -not $versionMatch.Success) { Fail-Pack 3 "Could not read PluginGuid/PluginVersion for $ModName." }

    $manifestFiles = @(Get-ChildItem -LiteralPath $modDir -Recurse -File -Filter 'info.json' | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' })
    $matchingManifests = @()
    foreach ($file in $manifestFiles) {
        try { $json = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json } catch { continue }
        if ([string]$json.GUID -eq $guidMatch.Groups[1].Value) { $matchingManifests += [pscustomobject]@{ File = $file; Json = $json } }
    }
    if ($matchingManifests.Count -eq 0) { Fail-Pack 3 "No info.json matching GUID $($guidMatch.Groups[1].Value) was found for $ModName." }
    $canonicalRows = @($matchingManifests | Where-Object { $_.File.FullName -match '\\BepInEx\\plugins\\' })
    if ($canonicalRows.Count -ne 1) { Fail-Pack 3 "Expected exactly one packaged info.json for $ModName, found $($canonicalRows.Count)." }
    # A mod-root info.json is the source its build copies into the package; the committed package
    # copy can lag behind after working-tree builds. Mods without one keep the package manifest.
    $sourceRows = @($matchingManifests | Where-Object { $_.File.FullName -ieq (Join-Path $modDir 'info.json') })
    $manifest = if ($sourceRows.Count -eq 1) { $sourceRows[0].Json } else { $canonicalRows[0].Json }
    if ([string]$manifest.Version -notmatch '^\d+\.\d+\.\d+$') { Fail-Pack 3 "Invalid semantic version for ${ModName}: $($manifest.Version)" }
    $changes = @($manifest.SerpChangelog | Where-Object { [string]$_.Version -eq [string]$manifest.Version })
    if ($changes.Count -ne 1 -or @($changes[0].Changes).Count -eq 0) { Fail-Pack 3 "Missing current changelog entry for $ModName v$($manifest.Version)." }
    Test-ActivationSettingContract -ModName $ModName -ModDirectory $modDir -SourceFiles $sourceFiles

    $dependencyPattern = '\[BepInDependency\s*\(\s*"' + [regex]::Escape($PackGuid) + '"\s*,\s*BepInDependency\.DependencyFlags\.SoftDependency\s*\)\s*\]'
    return [pscustomobject]@{
        Name = $ModName
        Directory = $modDir
        BuildBat = $buildBat
        ReleaseBat = $releaseBat
        PluginSource = $pluginSources[0].FullName
        PluginGuid = $guidMatch.Groups[1].Value
        PluginVersion = $versionMatch.Groups[1].Value
        ManifestVersion = [string]$manifest.Version
        Manifest = $manifest
        ManifestPaths = @($matchingManifests.File.FullName)
        PackageDirectory = $canonicalRows[0].File.Directory.FullName
        HasDependency = [regex]::Matches($pluginText, $dependencyPattern).Count -eq 1
        DependencyCount = [regex]::Matches($pluginText, $dependencyPattern).Count
        NeedsProjectRegistration = $false
        LatestVersion = $null
        LatestTag = $null
        LatestUrl = $null
        LatestCommit = $null
        ReleaseContentIsCurrent = $false
        ReleaseContentChanges = @()
        ReleaseArtifactValid = $false
        NeedsRelease = $false
        TargetVersion = [string]$manifest.Version
        NeedsVersionBump = $false
        NeedsSourceEdit = $false
        Package = $null
    }
}

# Manually published prereleases (e.g. v1.0.29BETA) are not release baselines.
function Test-StableReleaseTag($Release, [string]$Prefix) {
    if ($Release.PSObject.Properties['isPrerelease'] -and [bool]$Release.isPrerelease) { return $false }
    $tag = [string]$Release.tagName
    return $tag.StartsWith($Prefix, [StringComparison]::Ordinal) -and $tag.Substring($Prefix.Length) -match '^\d+\.\d+\.\d+(?:-[0-9a-f]{12})?$'
}

function Get-LatestReleaseForMod {
    param([string]$ModName, [object[]]$Releases, [string]$Repository = $script:Repository)
    $candidate = @($Releases | Where-Object {
        (-not [bool]$_.isDraft) -and (Test-StableReleaseTag $_ ($ModName + '/v'))
    } | Sort-Object publishedAt -Descending | Select-Object -First 1)
    Write-RunLog "Release lookup for ${ModName}: inputs=$($Releases.Count), matches=$($candidate.Count)."
    if ($candidate.Count -eq 0) { return $null }
    $view = Invoke-Checked -FilePath 'gh' -Arguments @('release','view',[string]$candidate[0].tagName,'--repo',$Repository,'--json','targetCommitish,url') -FailureCode 2
    $details = ($view.Output -join "`n") | ConvertFrom-Json
    return [pscustomobject]@{
        Tag = [string]$candidate[0].tagName
        Version = ([string]$candidate[0].tagName).Substring($ModName.Length + 2)
        Commit = [string]$details.targetCommitish
        Url = [string]$details.url
    }
}

function Get-ReleasePackage {
    param([Parameter(Mandatory)]$Mod)
    $isTestRelease=$TestMode -and $Mod.LatestTag -like '*/test/*'
    $tag = if ($TestMode -and $Mod.LatestTag -like '*/test/*') {$Mod.LatestTag} else {"$($Mod.Name)/v$($Mod.TargetVersion)"}
    $view = Invoke-Checked -FilePath 'gh' -Arguments @('release','view',$tag,'--repo',$script:Repository,'--json','tagName,isDraft,targetCommitish') -FailureCode 7
    $release = ($view.Output -join "`n") | ConvertFrom-Json
    if ($release.tagName -cne $tag -or [bool]$release.isDraft -ne [bool]$isTestRelease) { Fail-Pack 7 "Release identity/mode mismatch: $tag" }
    $cache = Join-Path $script:OutputRoot "cache\$($Mod.Name)\v$($Mod.TargetVersion)"
    if ($TestMode) { $cache = Join-Path $cache ($tag -replace '^.*/v','') }
    [void](New-Item -ItemType Directory -Path $cache -Force)
    $base = "$($Mod.Name)-v$($Mod.TargetVersion)"
    $zip = Join-Path $cache "$base.zip"
    $sha = Join-Path $cache "$base.zip.sha256"
    $provenance = Join-Path $cache "$base.provenance.json"
    $provenance = Get-CachedSteamReleaseAsset $cache "$base.provenance.json" $tag $script:Repository {
        param($path)
        $p = [IO.File]::ReadAllText($path) | ConvertFrom-Json
        return $p.Tag -ceq $tag -and $p.Mod -ceq $Mod.Name -and $p.PluginGuid -ceq $Mod.PluginGuid -and
            $p.Version -ceq $Mod.TargetVersion -and $p.Commit -ceq $release.targetCommitish -and
            $p.Repository -ceq ('https://github.com/' + $script:Repository) -and $p.Package.Sha256 -match '^[a-f0-9]{64}$'
    } 7
    $expected = [string](([IO.File]::ReadAllText($provenance) | ConvertFrom-Json).Package.Sha256)
    $zip = Get-CachedSteamReleaseAsset $cache "$base.zip" $tag $script:Repository {param($path) Test-ReleaseAssetHash $path $expected} 7
    $sha = Get-CachedSteamReleaseAsset $cache "$base.zip.sha256" $tag $script:Repository {param($path) Test-ReleaseChecksum $path $expected} 7
    foreach ($path in @($zip,$sha,$provenance)) { if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Fail-Pack 7 "Missing release asset: $path" } }
    $actual = Get-Sha256 $zip
    $shaText = [IO.File]::ReadAllText($sha)
    $declaredMatch = [regex]::Match($shaText, '(?i)\b([0-9a-f]{64})\b')
    if (-not $declaredMatch.Success -or $declaredMatch.Groups[1].Value.ToLowerInvariant() -ne $actual) { Fail-Pack 7 "SHA-256 file mismatch for $($Mod.Name)." }
    $prov = Get-Content -LiteralPath $provenance -Raw | ConvertFrom-Json
    if ([string]$prov.Package.Sha256 -ne $actual -or [string]$prov.PluginGuid -ne $Mod.PluginGuid -or [string]$prov.Version -ne $Mod.TargetVersion) {
        Fail-Pack 7 "Provenance mismatch for $($Mod.Name)."
    }
    $audit = Join-Path $cache 'audit'
    $audit=[IO.Path]::GetFullPath($audit)
    if(-not $audit.StartsWith([IO.Path]::GetFullPath($cache).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){Fail-Pack 7 'Unsafe APIShared audit directory.'}
    Assert-ApiWorkshopUnlinkedPath $audit
    if(Test-Path -LiteralPath $audit){Assert-ApiWorkshopUnlinkedTree $audit}
    if (Test-Path -LiteralPath $audit) { Remove-Item -LiteralPath $audit -Recurse -Force }
    Expand-Archive -LiteralPath $zip -DestinationPath $audit
    $roots = @(Get-ChildItem -LiteralPath $audit -Directory)
    if ($roots.Count -ne 1) { Fail-Pack 7 "Release ZIP for $($Mod.Name) must contain exactly one root directory." }
    try { [void](Assert-ScriptExtenderXamlPatchContract -Directory $roots[0].FullName) }
    catch { Fail-Pack 7 "Release ZIP for $($Mod.Name) violates the XAML patch contract: $($_.Exception.Message)" }
    return [pscustomobject]@{ Zip = $zip; Sha256 = $actual; Provenance = $prov; PackageDirectory = $roots[0].FullName }
}

function Get-ApiSharedReleasePackage {
    param(
        [Parameter(Mandatory)]$Infrastructure
    )

    $apiTest=$TestMode
    $tag = "APIShared/v$($Infrastructure.Version)"
    if ($apiTest) { $apiCommit=((Invoke-Checked -FilePath 'git' -Arguments @('-C',$Infrastructure.ProjectDirectory,'rev-parse','HEAD') -FailureCode 3).Output -join '').Trim(); $tag="APIShared/test/v$($Infrastructure.Version)-$($apiCommit.Substring(0,12))" }
    $base = "APIShared-v$($Infrastructure.Version)"
    $expectedAssetNames = @("$base.zip", "$base.zip.sha256", "$base.provenance.json")
    $releaseResult = Invoke-Checked -FilePath 'gh' -Arguments @(
            'release','view',$tag,'--repo',$Infrastructure.Repository,'--json','tagName,isDraft,targetCommitish,url,assets'
    ) -FailureCode 7 -AllowFailure
    if ($releaseResult.ExitCode -ne 0) { Fail-Pack 7 "Required APIShared release $tag is unavailable. Prepare APIShared first (CreateSteamModPack.bat: APIShared or Beide), which creates its release from main." }
    $release = ($releaseResult.Output -join "`n") | ConvertFrom-Json
    if ([string]$release.tagName -cne $tag -or [bool]$release.isDraft -ne [bool]$apiTest) {
        Fail-Pack 7 "APIShared release $tag is missing, still a draft, or resolved to an unexpected tag."
    }
    foreach ($assetName in $expectedAssetNames) {
        if (@($release.assets | Where-Object { [string]$_.name -ceq $assetName }).Count -ne 1) {
            Fail-Pack 7 "APIShared release $tag must contain exactly one $assetName asset."
        }
    }

    $assetResponse = Invoke-Checked -FilePath 'gh' -Arguments @('api',("repos/"+$Infrastructure.Repository+"/releases/tags/"+[Uri]::EscapeDataString($tag))) -FailureCode 7
    $assetRelease = ($assetResponse.Output -join "`n") | ConvertFrom-Json
    if($assetRelease.tag_name -cne $tag -or [bool]$assetRelease.draft -ne [bool]$apiTest -or
       $assetRelease.target_commitish -cne $release.targetCommitish){Fail-Pack 7 'APIShared release changed during identity selection.'}
    $releaseAssets = @(Get-ApiSharedReleaseAssetRecords $assetRelease.assets $expectedAssetNames)
    $provenanceAsset = @($releaseAssets | Where-Object Name -ceq "$base.provenance.json")[0]
    $zipAsset = @($releaseAssets | Where-Object Name -ceq "$base.zip")[0]
    $checksumAsset = @($releaseAssets | Where-Object Name -ceq "$base.zip.sha256")[0]

    $cache = Join-Path (Get-ApiMainWorkspace $script:Root) ".release-output\APIShared\cache\v$($Infrastructure.Version)"
    if ($apiTest) { $cache=Join-Path $script:OutputRoot ("cache/APIShared/v$($Infrastructure.Version)-"+$apiCommit.Substring(0,12)) }
    [void](New-Item -ItemType Directory -Path $cache -Force)
    $zip = Join-Path $cache "$base.zip"
    $shaPath = Join-Path $cache "$base.zip.sha256"
    $provenancePath = Join-Path $cache "$base.provenance.json"
    $provenancePath = Get-CachedSteamReleaseAsset $cache "$base.provenance.json" $tag $Infrastructure.Repository {
        param($path)
        $p = [IO.File]::ReadAllText($path) | ConvertFrom-Json
        return (Get-SteamPackFileSha256 $path) -ceq $provenanceAsset.Sha256 -and
            (Get-Item -LiteralPath $path).Length -eq $provenanceAsset.Size -and
            $p.Mod -ceq 'APIShared' -and $p.PluginGuid -ceq $Infrastructure.Guid -and
            $p.Version -ceq $Infrastructure.Version -and $p.Tag -ceq $tag -and
            $p.Commit -ceq $release.targetCommitish -and $p.Package.Sha256 -ceq $zipAsset.Sha256
    } 7
    $expected = [string](([IO.File]::ReadAllText($provenancePath) | ConvertFrom-Json).Package.Sha256)
    $zip = Get-CachedSteamReleaseAsset $cache "$base.zip" $tag $Infrastructure.Repository {param($path) Test-ReleaseAssetHash $path $expected} 7
    $shaPath = Get-CachedSteamReleaseAsset $cache "$base.zip.sha256" $tag $Infrastructure.Repository {
        param($path) (Test-ReleaseChecksum $path $expected) -and
            (Get-SteamPackFileSha256 $path) -ceq $checksumAsset.Sha256 -and
            (Get-Item -LiteralPath $path).Length -eq $checksumAsset.Size
    } 7
    foreach ($path in @($zip,$shaPath,$provenancePath)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Fail-Pack 7 "Missing APIShared release asset: $path" }
    }

    Assert-ApiSharedReleaseAssetFiles $cache $releaseAssets
    $actualHash = Get-Sha256 $zip
    $shaText = [IO.File]::ReadAllText($shaPath)
    $declaredMatch = [regex]::Match($shaText, '(?i)\b([0-9a-f]{64})\b')
    if (-not $declaredMatch.Success -or $declaredMatch.Groups[1].Value.ToLowerInvariant() -ne $actualHash) {
        Fail-Pack 7 "SHA-256 file mismatch for $tag."
    }
    $provenance = Get-Content -LiteralPath $provenancePath -Raw | ConvertFrom-Json
    if ([string]$provenance.Mod -cne 'APIShared' -or
        [string]$provenance.PluginGuid -cne $Infrastructure.Guid -or
        [string]$provenance.Version -cne $Infrastructure.Version -or
        [string]$provenance.Tag -cne $tag -or
        [string]$provenance.Package.Sha256 -cne $actualHash -or
        [string]$provenance.Commit -cnotmatch '^[0-9a-f]{40}$' -or
        [string]$provenance.Commit -cne [string]$release.targetCommitish) {
        Fail-Pack 7 "Provenance mismatch for $tag."
    }

    $mainProof=Assert-ApiMainSelection $script:Root $env:SHCDE_API_SHARED_MAIN_SELECTION
    $mainAncestor=Invoke-ApiRepositoryProcess 'git' @('-C',$Infrastructure.ProjectDirectory,'merge-base','--is-ancestor',[string]$provenance.Commit,$mainProof.Commit)
    if($mainAncestor.ExitCode -ne 0){Fail-Pack 7 'APIShared release source is not in selected main history.'}
    $sourcePaths=@(Invoke-ApiRepositoryGit $Infrastructure.ProjectDirectory @('diff','--name-only',[string]$provenance.Commit,$mainProof.Commit))
    if(@(Get-SteamApiReleaseRelevantChanges $sourcePaths).Count){
        Fail-Pack 7 'APIShared release-relevant main sources differ from the selected artifact. Publish the updated APIShared separately first.'
    }

    Assert-ApiWorkshopZipLayout -ZipPath $zip -Guid $Infrastructure.Guid
    $audit = Join-Path $cache 'audit'
    $audit=[IO.Path]::GetFullPath($audit)
    if(-not $audit.StartsWith([IO.Path]::GetFullPath($cache).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){Fail-Pack 7 'Unsafe APIShared audit directory.'}
    Assert-ApiWorkshopUnlinkedPath $audit
    if(Test-Path -LiteralPath $audit){Assert-ApiWorkshopUnlinkedTree $audit}
    if (Test-Path -LiteralPath $audit) { Remove-Item -LiteralPath $audit -Recurse -Force }
    Expand-Archive -LiteralPath $zip -DestinationPath $audit
    $roots = @(Get-ChildItem -LiteralPath $audit -Directory)
    $outerFiles = @(Get-ChildItem -LiteralPath $audit -File)
    if ($roots.Count -ne 1 -or $outerFiles.Count -ne 0 -or $roots[0].Name -cne 'BepInEx') {
        Fail-Pack 7 'APIShared release ZIP must contain exactly the BepInEx root directory.'
    }
    $packageDirectory = Join-Path $audit "BepInEx\plugins\$($Infrastructure.Guid)"
    $manifestPath = Join-Path $packageDirectory 'info.json'
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { Fail-Pack 7 "APIShared release package lacks info.json." }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    if ([string]$manifest.GUID -cne $Infrastructure.Guid -or [string]$manifest.Version -cne $Infrastructure.Version) {
        Fail-Pack 7 "APIShared release manifest identity mismatch for $tag."
    }
    Assert-ApiWorkshopInventory -Directory $packageDirectory
    # APIShared's release also owns its separately inventoried publisher under
    # BepInEx/tools. Provenance covers the entire already validated ZIP.
    $actualFileRecords = @(Get-FileRecords $audit)
    if (($actualFileRecords | ConvertTo-Json -Depth 5 -Compress) -cne
        (@($provenance.Package.Files | Sort-Object Path) | ConvertTo-Json -Depth 5 -Compress)) {
        Fail-Pack 7 "APIShared release files differ from provenance for $tag."
    }
    try { [void](Assert-ScriptExtenderXamlPatchContract -Directory $packageDirectory) }
    catch { Fail-Pack 7 "APIShared release $tag violates the XAML patch contract: $($_.Exception.Message)" }
    $forbidden = @(Get-ChildItem -LiteralPath $packageDirectory -File -Recurse | Where-Object {
        $_.Name -ieq 'SHCDESE.dll' -or $_.Name -like 'RedBird*.dll'
    })
    $apiCopies = @(Get-ChildItem -LiteralPath $packageDirectory -File -Recurse -Filter 'APIShared.dll')
    if ($forbidden.Count -ne 0 -or $apiCopies.Count -ne 1) {
        Fail-Pack 7 "APIShared release package must contain exactly one APIShared.dll and no private SHCDESE/RedBird DLLs."
    }
    $assembly = Get-CecilPluginMetadata -Directory $packageDirectory
    if ($assembly.Guid -cne $Infrastructure.Guid -or
        $assembly.Version -cne $Infrastructure.Version -or
        $assembly.HostDependencyCount -ne 0) {
        Fail-Pack 7 "APIShared release DLL identity/dependency mismatch for $tag."
    }

    return [pscustomobject]@{
        Zip = $zip
        Sha256 = $actualHash
        Provenance = $provenance
        PackageDirectory = $packageDirectory
        ReleaseTag = $tag
        ReleaseUrl = [string]$release.url
        TargetCommit = [string]$release.targetCommitish
        ProvenancePath = $provenancePath
        ProvenanceSha256 = $provenanceAsset.Sha256
        ReleaseAssets = $releaseAssets
    }
}

function Get-PackagedContentComparison {
    param(
        [Parameter(Mandatory)]$Mod,
        [Parameter(Mandatory)][string]$BaseCommit,
        [Parameter(Mandatory)][string]$HeadCommit
    )

    if ($Mod.Name -eq 'APIShared') {
        $apiRoot = $script:ApiMainRoot
        $result = Invoke-Checked -FilePath 'git' -Arguments @('-C',$apiRoot,'diff','--name-only',$BaseCommit,'HEAD') -FailureCode 3
        $relevant = @(Get-SteamApiReleaseRelevantChanges @($result.Output))
        return [pscustomobject]@{ IsCurrent = $relevant.Count -eq 0; Paths = $relevant }
    }
    $rootPrefix = $script:Root.TrimEnd('\') + '\'
    if (-not $Mod.PackageDirectory.StartsWith($rootPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        Fail-Pack 3 "Packaged plugin directory is outside the repository for $($Mod.Name): $($Mod.PackageDirectory)"
    }

    # Compare original mod sources and packaged resources. Rebuilt DLL/PDB files
    # alone reuse the published release artifact for the same mod version.
    $repositoryPath = $Mod.PackageDirectory.Substring($rootPrefix.Length).Replace('\', '/')
    $result = Invoke-Git -Arguments @(
        'diff', '--name-only', '--diff-filter=ACDMRTUXB', $BaseCommit, $HeadCommit, '--', $Mod.Name
    ) -FailureCode 3
    $allChangedPaths = @($result.Output | Where-Object { -not [string]::IsNullOrWhiteSpace([string]$_) })
    $changedPaths = @(Get-SteamReleaseRelevantChanges -ModName $Mod.Name `
        -PackageDirectoryPath $repositoryPath -ChangedPaths $allChangedPaths)
    if ($allChangedPaths.Count -gt 0 -and $changedPaths.Count -eq 0) {
        Write-RunLog "Reusing published $($Mod.Name) release: original sources and packaged resources are unchanged."
    }
    return [pscustomobject]@{
        IsCurrent = $changedPaths.Count -eq 0
        Paths = $changedPaths
    }
}

function Get-CecilPluginMetadata {
    param([string]$Directory)
    if (-not ('Mono.Cecil.AssemblyDefinition' -as [type])) {
        Add-Type -Path (Resolve-CecilLoadPath)
    }
    $found = @()
    foreach ($dll in @(Get-ChildItem -LiteralPath $Directory -File -Filter '*.dll')) {
        $assembly = $null
        $resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
        try {
            $resolver.AddSearchDirectory($dll.DirectoryName)
            $resolver.AddSearchDirectory($script:BepInExCorePath)
            if (Test-Path -LiteralPath $script:ScriptExtenderOutputPath -PathType Container) {
                $resolver.AddSearchDirectory($script:ScriptExtenderOutputPath)
            }
            $readerParameters = [Mono.Cecil.ReaderParameters]::new()
            $readerParameters.AssemblyResolver = $resolver
            $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($dll.FullName, $readerParameters)
            foreach ($type in $assembly.MainModule.Types) {
                $pluginAttr = @($type.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInPlugin' })
                if ($pluginAttr.Count -eq 0) { continue }
                $dependencies = @($type.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'BepInEx.BepInDependency' })
                $hostDependencies = @($dependencies | Where-Object {
                    $_.ConstructorArguments.Count -eq 2 -and
                    [string]$_.ConstructorArguments[0].Value -eq $PackGuid -and
                    [int]$_.ConstructorArguments[1].Value -eq 2
                })
                $found += [pscustomobject]@{
                    Dll = $dll.FullName
                    Guid = [string]$pluginAttr[0].ConstructorArguments[0].Value
                    Version = [string]$pluginAttr[0].ConstructorArguments[2].Value
                    HostDependencyCount = $hostDependencies.Count
                }
            }
        } catch {
        } finally {
            if ($null -ne $assembly) { $assembly.Dispose() }
            $resolver.Dispose()
        }
    }
    if ($found.Count -ne 1) { Fail-Pack 7 "Expected one BepInEx plugin DLL below $Directory, found $($found.Count)." }
    return $found[0]
}

function Set-ModCompatibility {
    param([Parameter(Mandatory)]$Mod)
    $text = [IO.File]::ReadAllText($Mod.PluginSource)
    if (-not $Mod.HasDependency) {
        $matches = [regex]::Matches($text, '(?m)^(\s*)\[BepInPlugin\s*\(')
        if ($matches.Count -ne 1) { Fail-Pack 3 "Could not identify dependency insertion point for $($Mod.Name)." }
        $indent = $matches[0].Groups[1].Value
        $attribute = $indent + '[BepInDependency("' + $PackGuid + '", BepInDependency.DependencyFlags.SoftDependency)]' + "`r`n"
        $text = $text.Insert($matches[0].Index, $attribute)
    }
    $text = [regex]::Replace($text, '(PluginVersion\s*=\s*")[^"]+("\s*;)', '${1}' + $Mod.TargetVersion + '${2}', 1)
    Write-Utf8CrLf -Path $Mod.PluginSource -Text $text

    foreach ($manifestPath in $Mod.ManifestPaths) {
        $manifestText = [IO.File]::ReadAllText($manifestPath)
        $json = $manifestText | ConvertFrom-Json
        $entry = @($json.SerpChangelog | Where-Object { [string]$_.Version -eq $Mod.TargetVersion })
        $addEntry = $entry.Count -eq 0
        $addChange = $entry.Count -gt 0 -and @($entry[0].Changes) -notcontains $script:CompatibilityChange
        if ($entry.Count -eq 0) {
            Write-RunLog "Adding changelog entry for $($Mod.Name) v$($Mod.TargetVersion)."
        } elseif ($addChange) {
            Write-RunLog "Appending Workshop compatibility note to $($Mod.Name) v$($Mod.TargetVersion)."
        }
        $updatedManifest = Update-ModManifestText -Text $manifestText -Version $Mod.TargetVersion `
            -AddChangelogEntry $addEntry -AddCompatibilityChange $addChange
        Write-Utf8CrLf -Path $manifestPath -Text $updatedManifest
    }
}

function Add-ReleaseProjectIfNeeded {
    param([object[]]$Mods)
    $path = Join-Path $script:Root 'Shared\Tools\Release\release-projects.json'
    $config = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $changed = $false
    foreach ($mod in $Mods) {
        if ($mod.NeedsProjectRegistration) { $config.Projects = @($config.Projects) + $mod.Name; $changed = $true }
    }
    if ($changed) { $config.Projects = @($config.Projects | Sort-Object -Unique); Write-JsonCrLf -Path $path -Value $config }
}

function Invoke-ModBuild {
    param([Parameter(Mandatory)]$Mod)
    Write-RunLog "Building $($Mod.Name) for compatibility validation."
    Invoke-Checked -FilePath $Mod.BuildBat -Arguments @('/nopause','/noinstall') -FailureCode 4 -WorkingDirectory $Mod.Directory | Out-Null
    try { [void](Assert-ScriptExtenderXamlPatchContract -Directory $Mod.PackageDirectory) }
    catch { Fail-Pack 4 "Built package for $($Mod.Name) violates the XAML patch contract: $($_.Exception.Message)" }
    $built = Get-CecilPluginMetadata -Directory $Mod.PackageDirectory
    if ($built.Guid -ne $Mod.PluginGuid -or $built.Version -ne $Mod.TargetVersion -or $built.HostDependencyCount -ne 1) {
        Fail-Pack 4 "Built DLL audit failed for $($Mod.Name): GUID=$($built.Guid), version=$($built.Version), host dependencies=$($built.HostDependencyCount)."
    }
}

function Invoke-ModRelease {
    param([Parameter(Mandatory)]$Mod)
    try { [void](Assert-ScriptExtenderXamlPatchContract -Directory $Mod.PackageDirectory) }
    catch { Fail-Pack 6 "Package for $($Mod.Name) violates the XAML patch contract before release: $($_.Exception.Message)" }
    Write-RunLog "Publishing required individual release $($Mod.Name) v$($Mod.TargetVersion)."
    $previousPreparationMode = $env:SHCDE_PREPARE_ONLY
    try {
        $env:SHCDE_PREPARE_ONLY = '1'
        $previousWorkshopTest=$env:SHCDE_WORKSHOP_TEST; $env:SHCDE_WORKSHOP_TEST=$(if($TestMode){'1'}else{''})
    Invoke-Checked -FilePath $Mod.ReleaseBat -Arguments @('/noprompt','/nopause') -FailureCode 6 -WorkingDirectory $Mod.Directory | Out-Null
    } finally { $env:SHCDE_PREPARE_ONLY = $previousPreparationMode; $env:SHCDE_WORKSHOP_TEST=$previousWorkshopTest }
    $Mod.LatestTag = if($TestMode){"$($Mod.Name)/test/v$($Mod.TargetVersion)-$(((Invoke-Git @('rev-parse','HEAD')).Output -join '').Trim().Substring(0,12))"}else{"$($Mod.Name)/v$($Mod.TargetVersion)"}
    $Mod.LatestUrl = "https://github.com/$($script:Repository)/releases/tag/$([Uri]::EscapeDataString($Mod.LatestTag))"
}

function Get-LegacyApiSharedFiles {
    param($History)
    $prefix = "BepInEx/plugins/$PackGuid/Infrastructure/APIShared_Serp/"
    $known = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($upload in @($History.Uploads)) {
        $map = Get-HistoryMapPath -Upload $upload
        foreach ($file in @(Get-MapFileRecords -MapPath $map -SourcePack "SerpsMods/v$($upload.PackVersion)")) {
            if (-not ([string]$file.Path).StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { continue }
            $relative = ([string]$file.Path).Substring("BepInEx/plugins/$PackGuid/".Length)
            if ($relative -match '(?i)(^|/)(LobbyModSettings|config)/') { Fail-Pack 11 'Historical APIShared inventory contains settings.' }
            $known["$relative|$($file.Sha256)"] = [ordered]@{ Path = $relative; Sha256 = $file.Sha256 }
        }
    }
    return @($known.Values | Sort-Object { $_.Path }, { $_.Sha256 })
}

function Get-NextPatchVersion {
    param([string]$Version)
    $value = [version]$Version
    return '{0}.{1}.{2}' -f $value.Major, $value.Minor, ($value.Build + 1)
}

function Get-HostContentSignature {
    param([object[]]$Mods, [object[]]$RetiredMods = @(), [AllowNull()]$Infrastructure, [object[]]$HistoricalUploads = @())
    $lines = [Collections.Generic.List[string]]::new()
    $lines.Add('FORMAT|APISHARED-WORKSHOP-1')
    $lines.Add("WORKSHOP|$ApiSharedWorkshopItemId")
    foreach ($upload in @($HistoricalUploads | Where-Object { $_.PSObject.Properties.Name -notcontains 'Format' -or $_.Format -eq 'Map' } | Sort-Object PackVersion)) { $lines.Add("HISTORY|$($upload.PackVersion)|$($upload.MapSha256)") }
    foreach ($mod in @($Mods | Sort-Object PluginGuid)) { $lines.Add("MOD|$($mod.PluginGuid)|$($mod.TargetVersion)|$($mod.Package.Sha256)") }
    if ($null -ne $Infrastructure) { $lines.Add("EXTERNAL-V1|$($Infrastructure.Guid)|$($Infrastructure.Version)|$($Infrastructure.DirectorySha256)") }
    foreach ($mod in @($RetiredMods | Sort-Object Guid)) { $lines.Add("RETIRED|$($mod.Guid)|$($mod.Version)|$($mod.TombstoneSha256)") }
    $hostRoot = Join-Path $script:Root 'SerpsModsHost'
    $files = @(Get-ChildItem -LiteralPath $hostRoot -Recurse -File | Where-Object {
        $_.FullName -notmatch '\\(?:BepInEx|bin|obj|\.local|\.git)\\' -and $_.Name -notin @('info.json','serps-modpack.json','steam-preview.png','README.md')
    } | Sort-Object FullName)
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($hostRoot.Length).TrimStart('\').Replace('\','/')
        $text = if ($file.Extension -in @('.cs','.csproj','.xaml','.txt','.bat')) { [IO.File]::ReadAllText($file.FullName) } else { Get-Sha256 $file.FullName }
        if ($file.Name -in @('SerpsModsHostPlugin.cs','SerpsModsHostBootstrap.cs')) { $text = [regex]::Replace($text, 'PluginVersion\s*=\s*"[^"]+"', 'PluginVersion="<PACK_VERSION>"') }
        $bytes = [Text.Encoding]::UTF8.GetBytes("$relative`n$text")
        $fileSha = [Security.Cryptography.SHA256]::Create()
        try { $lines.Add("FILE|$relative|" + ([BitConverter]::ToString($fileSha.ComputeHash($bytes)).Replace('-','').ToLowerInvariant())) }
        finally { $fileSha.Dispose() }
    }
    foreach ($relative in @('Shared/Tools/Steam/Prepare-SerpsModsWorkshop.ps1','APIShared/tools/Prepare-WorkshopPackage.ps1','APIShared/tools/New-WorkshopBootstrap.ps1','APIShared/tools/WorkshopPackage.ps1')) {
        $packagerInput=if($relative.StartsWith('APIShared/')){Join-Path $script:ApiMainRoot $relative.Substring(10)}else{Join-Path $script:Root $relative}
        $lines.Add("PACKAGER|$relative|"+(Get-Sha256 $packagerInput))
    }
    $payload = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $payloadSha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($payloadSha.ComputeHash($payload)).Replace('-','').ToLowerInvariant() }
    finally { $payloadSha.Dispose() }
}

function Copy-DirectoryContents {
    param([Parameter(Mandatory)][string]$Source, [Parameter(Mandatory)][string]$Destination)
    if (-not (Test-Path -LiteralPath $Source -PathType Container)) { Fail-Pack 8 "Copy source directory is missing: $Source" }
    [void](New-Item -ItemType Directory -Path $Destination -Force)
    foreach ($item in @(Get-ChildItem -LiteralPath $Source -Force)) {
        Copy-Item -LiteralPath $item.FullName -Destination $Destination -Recurse -Force
    }
}

function Find-MapZipOffset {
    param([Parameter(Mandatory)][string]$MapPath)
    $stream = [IO.File]::Open($MapPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        # Scan blocks in .NET rather than making one PowerShell call per byte.
        # Keep three bytes between blocks so a split ZIP header is still found.
        $buffer = [byte[]]::new(65539)
        $carry = 0
        $position = 0L
        while (($read = $stream.Read($buffer, $carry, 65536)) -gt 0) {
            $count = $carry + $read
            $cursor = 0
            while ($cursor -le $count - 4) {
                $candidate = [Array]::IndexOf($buffer, [byte]0x50, $cursor, $count - $cursor)
                if ($candidate -lt 0 -or $candidate -gt $count - 4) { break }
                if ($buffer[$candidate + 1] -eq 0x4B -and $buffer[$candidate + 2] -eq 0x03 -and $buffer[$candidate + 3] -eq 0x04) { return $position + $candidate }
                $cursor = $candidate + 1
            }
            $carry = [Math]::Min(3, $count)
            [Array]::Copy($buffer, $count - $carry, $buffer, 0, $carry)
            $position += $count - $carry
        }
    } finally {
        $stream.Dispose()
    }
    Fail-Pack 9 "Map audit failed: ZIP payload header not found in $MapPath."
}

function Test-MapContents {
    param([Parameter(Mandatory)][string]$MapPath, [Parameter(Mandatory)][string]$StageDirectory)
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $offset = Find-MapZipOffset -MapPath $MapPath
    $mapStream = [IO.File]::Open($MapPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $zipStream = [IO.MemoryStream]::new()
    $archive = $null
    try {
        [void]$mapStream.Seek($offset, [IO.SeekOrigin]::Begin)
        $mapStream.CopyTo($zipStream)
        $zipStream.Position = 0
        $archive = [IO.Compression.ZipArchive]::new($zipStream, [IO.Compression.ZipArchiveMode]::Read, $true)
        $expected = @(Get-ChildItem -LiteralPath $StageDirectory -Recurse -File | ForEach-Object {
            $_.FullName.Substring($StageDirectory.Length).TrimStart('\').Replace('\','/')
        } | Sort-Object)
        $actual = @($archive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) } | ForEach-Object { $_.FullName.Replace('\','/') } | Sort-Object)
        if (($expected -join "`n") -cne ($actual -join "`n")) { Fail-Pack 9 'Map audit failed: archive paths differ from the staging directory.' }
        foreach ($entry in @($archive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) })) {
            $sourcePath = Join-Path $StageDirectory $entry.FullName.Replace('/', '\')
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $archiveHash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant() }
            finally { $stream.Dispose(); $sha.Dispose() }
            if ($archiveHash -ne (Get-Sha256 $sourcePath)) { Fail-Pack 9 "Map audit hash mismatch: $($entry.FullName)" }
        }
    } finally {
        if ($null -ne $archive) { $archive.Dispose() }
        $zipStream.Dispose()
        $mapStream.Dispose()
    }
}

function Get-MapFilePaths {
    param([Parameter(Mandatory)][string]$MapPath)
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $offset = Find-MapZipOffset -MapPath $MapPath
    $mapStream = [IO.File]::Open($MapPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $zipStream = [IO.MemoryStream]::new()
    $archive = $null
    try {
        [void]$mapStream.Seek($offset, [IO.SeekOrigin]::Begin)
        $mapStream.CopyTo($zipStream)
        $zipStream.Position = 0
        $archive = [IO.Compression.ZipArchive]::new($zipStream, [IO.Compression.ZipArchiveMode]::Read, $true)
        return @($archive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) } | ForEach-Object {
            $_.FullName.Replace('\','/').TrimStart('/')
        } | Sort-Object -Unique)
    } finally {
        if ($null -ne $archive) { $archive.Dispose() }
        $zipStream.Dispose()
        $mapStream.Dispose()
    }
}

function Get-MapFileRecords {
    param(
        [Parameter(Mandatory)][string]$MapPath,
        [Parameter(Mandatory)][string]$SourcePack
    )
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $offset = Find-MapZipOffset -MapPath $MapPath
    $mapStream = [IO.File]::Open($MapPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $zipStream = [IO.MemoryStream]::new()
    $archive = $null
    try {
        [void]$mapStream.Seek($offset, [IO.SeekOrigin]::Begin)
        $mapStream.CopyTo($zipStream)
        $zipStream.Position = 0
        $archive = [IO.Compression.ZipArchive]::new($zipStream, [IO.Compression.ZipArchiveMode]::Read, $true)
        $records = @()
        $paths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in @($archive.Entries | Where-Object { -not [string]::IsNullOrEmpty($_.Name) })) {
            $entryPath = $entry.FullName.Replace('\','/')
            if ($entryPath.StartsWith('/', [StringComparison]::Ordinal) -or
                $entryPath -match '^[A-Za-z]:' -or
                @($entryPath.Split('/') | Where-Object { $_ -in @('','.', '..') }).Count -gt 0) {
                Fail-Pack 11 "Unsafe archive path in $SourcePack`: $($entry.FullName)"
            }
            if (-not $paths.Add($entryPath)) { Fail-Pack 11 "Duplicate or case-ambiguous archive path in $SourcePack`: $entryPath" }
            $entryStream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($sha.ComputeHash($entryStream)).Replace('-','').ToLowerInvariant() }
            finally { $entryStream.Dispose(); $sha.Dispose() }
            $records += [pscustomobject]@{
                Path = $entryPath
                Sha256 = $hash
                Size = [long]$entry.Length
                SourcePack = $SourcePack
                MapPath = $MapPath
            }
        }
        return @($records | Sort-Object Path)
    } finally {
        if ($null -ne $archive) { $archive.Dispose() }
        $zipStream.Dispose()
        $mapStream.Dispose()
    }
}

function Export-MapEntry {
    param(
        [Parameter(Mandatory)][string]$MapPath,
        [Parameter(Mandatory)][string]$EntryPath,
        [Parameter(Mandatory)][string]$Destination
    )
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $offset = Find-MapZipOffset -MapPath $MapPath
    $mapStream = [IO.File]::Open($MapPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    $zipStream = [IO.MemoryStream]::new()
    $archive = $null
    try {
        [void]$mapStream.Seek($offset, [IO.SeekOrigin]::Begin)
        $mapStream.CopyTo($zipStream)
        $zipStream.Position = 0
        $archive = [IO.Compression.ZipArchive]::new($zipStream, [IO.Compression.ZipArchiveMode]::Read, $true)
        $matches = @($archive.Entries | Where-Object {
            -not [string]::IsNullOrEmpty($_.Name) -and
            [string]::Equals($_.FullName.Replace('\','/').TrimStart('/'), $EntryPath, [StringComparison]::OrdinalIgnoreCase)
        })
        if ($matches.Count -ne 1) { Fail-Pack 11 "Historical entry is missing or ambiguous in $MapPath`: $EntryPath" }
        [void](New-Item -ItemType Directory -Path (Split-Path -Parent $Destination) -Force)
        $input = $matches[0].Open()
        $output = [IO.File]::Open($Destination, [IO.FileMode]::Create, [IO.FileAccess]::Write, [IO.FileShare]::None)
        try { $input.CopyTo($output) } finally { $output.Dispose(); $input.Dispose() }
    } finally {
        if ($null -ne $archive) { $archive.Dispose() }
        $zipStream.Dispose()
        $mapStream.Dispose()
    }
}

function Get-HistoryMapPath {
    param([Parameter(Mandatory)]$Upload)

    $version = [string]$Upload.PackVersion
    $localPath = Join-Path $script:OutputRoot "v$version\SerpsMods.map"
    Assert-ApiWorkshopUnlinkedPath $localPath
    if (-not (Test-Path -LiteralPath $localPath -PathType Leaf) -or
        -not (Test-ReleaseAssetHash $localPath ([string]$Upload.MapSha256))) {
        $cache = Join-Path $script:OutputRoot "cache\history\v$version"
        $localPath = Get-CachedSteamReleaseAsset $cache 'SerpsMods.map' "SerpsMods/v$version" $script:Repository {
            param($path) Test-ReleaseAssetHash $path ([string]$Upload.MapSha256)
        }
    }
    if (-not (Test-Path -LiteralPath $localPath -PathType Leaf)) {
        Fail-Pack 10 "Confirmed Steam pack v$version is unavailable for the historical path audit."
    }
    $actualHash = Get-Sha256 $localPath
    if (-not [string]::Equals($actualHash, [string]$Upload.MapSha256, [StringComparison]::OrdinalIgnoreCase)) {
        Fail-Pack 10 "Confirmed Steam pack v$version has SHA-256 $actualHash, expected $($Upload.MapSha256)."
    }
    return $localPath
}

function Get-HistoricalSteamFileIndex {
    param([Parameter(Mandatory)]$History, [Parameter(Mandatory)][string]$PackGuid)

    $index = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    $canonical = [Collections.Generic.Dictionary[string,string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($upload in @($History.Uploads | Where-Object { $_.PSObject.Properties.Name -notcontains 'Format' -or $_.Format -eq 'Map' } | Sort-Object { [version]$_.PackVersion })) {
        $sourcePack = "SerpsMods/v$([string]$upload.PackVersion)"
        $mapPath = Get-HistoryMapPath -Upload $upload
        foreach ($record in @(Get-MapFileRecords -MapPath $mapPath -SourcePack $sourcePack)) {
            $pluginRoot = 'BepInEx/plugins/'
            $packRoot = "BepInEx/plugins/$PackGuid/"
            if ($record.Path.StartsWith($pluginRoot, [StringComparison]::OrdinalIgnoreCase) -and
                -not $record.Path.StartsWith($packRoot, [StringComparison]::OrdinalIgnoreCase)) {
                Fail-Pack 11 "Historical archive path is outside the configured pack root ${packRoot}: $($record.Path)"
            }
            if ($canonical.ContainsKey($record.Path) -and $canonical[$record.Path] -cne $record.Path) {
                Fail-Pack 11 "Case-ambiguous historical archive path: '$($canonical[$record.Path])' and '$($record.Path)'."
            }
            $canonical[$record.Path] = $record.Path
            $index[$record.Path] = $record
        }
    }
    return $index
}

function Get-StageFileIndex {
    param([Parameter(Mandatory)][string]$Stage)

    $index = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    $stageRootItem = Get-Item -LiteralPath $Stage -Force
    if (($stageRootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        Fail-Pack 11 "The Steam stage root must not be a reparse point: $Stage"
    }
    $reparsePoints = @(Get-ChildItem -LiteralPath $Stage -Recurse -Force | Where-Object {
        ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
    })
    if ($reparsePoints.Count -gt 0) { Fail-Pack 11 "Reparse points are forbidden in the Steam stage: $($reparsePoints[0].FullName)" }
    foreach ($file in @(Get-ChildItem -LiteralPath $Stage -Recurse -File -Force)) {
        $relative = $file.FullName.Substring($Stage.Length).TrimStart('\').Replace('\','/')
        if ($index.ContainsKey($relative)) { Fail-Pack 11 "Case-ambiguous or duplicate staged path: $relative" }
        $index[$relative] = [pscustomobject]@{ Path = $relative; Sha256 = Get-Sha256 $file.FullName; File = $file.FullName }
    }
    return $index
}

function Add-HistoricalSteamReplacements {
    param(
        [Parameter(Mandatory)][string]$Stage,
        [Parameter(Mandatory)][string]$PackGuid,
        [Parameter(Mandatory)]$HistoricalIndex
    )

    $overlayRoot = Join-Path $script:Root 'SerpsModsHost\SteamTombstones'
    $overlayPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $stageIndex = Get-StageFileIndex -Stage $Stage
    $missing = @(Get-MissingSteamPackPaths -PreviousPaths @($HistoricalIndex.Keys) -CurrentPaths @($stageIndex.Keys))
    if (Test-Path -LiteralPath $overlayRoot -PathType Container) {
        $overlayRootItem = Get-Item -LiteralPath $overlayRoot -Force
        if (($overlayRootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail-Pack 11 "Tombstone overlay root is a reparse point: $overlayRoot" }
        $overlayReparsePoints = @(Get-ChildItem -LiteralPath $overlayRoot -Recurse -Force | Where-Object {
            ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
        })
        if ($overlayReparsePoints.Count -gt 0) { Fail-Pack 11 "Reparse point in tombstone overlay: $($overlayReparsePoints[0].FullName)" }
        foreach ($file in @(Get-ChildItem -LiteralPath $overlayRoot -Recurse -File -Force | Where-Object { $_.Name -cne '.gitkeep' })) {
            $relative = $file.FullName.Substring($overlayRoot.Length).TrimStart('\').Replace('\','/')
            $relative = Assert-SteamArchivePath -Path $relative -PackGuid $PackGuid
            if (-not $overlayPaths.Add($relative)) { Fail-Pack 11 "Duplicate tombstone overlay path: $relative" }
            $destination = Join-Path $Stage $relative.Replace('/','\')
            [void](New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force)
            Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        }
    }

    $stageIndex = Get-StageFileIndex -Stage $Stage
    $records = @()
    $noop = Get-SteamXamlTombstoneText
    foreach ($path in $missing) {
        $path = Assert-SteamArchivePath -Path $path -PackGuid $PackGuid
        $old = $HistoricalIndex[$path]
        $destination = Join-Path $Stage $path.Replace('/','\')
        $action = Get-SteamMissingPathAction -Path $path -PackGuid $PackGuid
        if ($overlayPaths.Contains($path)) {
            $action = 'ExplicitTombstone'
        } elseif ($action -ceq 'XamlTombstone') {
            Write-SteamXamlTombstone -Path $destination
        } elseif ($action -ceq 'Retain') {
            Export-MapEntry -MapPath $old.MapPath -EntryPath $path -Destination $destination
        } else {
            $expected = Join-Path $overlayRoot $path.Replace('/','\')
            Fail-Pack 11 "Unsafe automatic deletion for '$path'. Add a reviewed format-specific replacement at '$expected'. DLL removals require a verified inert assembly."
        }
        $replacementHash = Get-Sha256 $destination
        $moveCandidates = @($stageIndex.Values | Where-Object { -not $HistoricalIndex.ContainsKey([string]$_.Path) })
        $movedTo = Find-SteamMovedPath -OldPath $path -OldSha256 $old.Sha256 -CurrentRecords $moveCandidates
        $status = if ($action -ceq 'Retain') { 'Retained' } elseif (-not [string]::IsNullOrWhiteSpace($movedTo)) { 'MovedSource' } else { 'Tombstone' }
        $records += [ordered]@{
            Path = $path
            Status = $status
            SourcePack = $old.SourcePack
            OriginalSha256 = $old.Sha256
            ReplacementSha256 = $replacementHash
            MovedTo = $movedTo
        }
        Write-RunLog "Historical path $status`: $path (source $($old.SourcePack))." 'OK'
        $stageIndex[$path] = [pscustomobject]@{ Path = $path; Sha256 = $replacementHash; File = $destination }
    }
    $noopPath = Join-Path $Stage '.serps-noop-hash.xaml'
    [IO.File]::WriteAllText($noopPath, $noop, [Text.UTF8Encoding]::new($false))
    $noopHash = Get-Sha256 $noopPath
    Remove-Item -LiteralPath $noopPath -Force
    $recordedPaths = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($record in $records) { [void]$recordedPaths.Add([string]$record.Path) }
    foreach ($path in @($HistoricalIndex.Keys)) {
        if ($recordedPaths.Contains($path) -or -not $stageIndex.ContainsKey($path)) { continue }
        $packageRoot = "BepInEx/plugins/$PackGuid/"
        if (-not $path.StartsWith($packageRoot, [StringComparison]::OrdinalIgnoreCase)) { continue }
        $current = $stageIndex[$path]
        $old = $HistoricalIndex[$path]
        $isNoopPatch = (Get-SteamMissingPathAction -Path $path -PackGuid $PackGuid) -ceq 'XamlTombstone' -and $current.Sha256 -ceq $noopHash
        $isExplicitOverlay = $overlayPaths.Contains($path) -and $current.Sha256 -cne $old.Sha256
        if (-not $isNoopPatch -and -not $isExplicitOverlay) { continue }
        $records += [ordered]@{
            Path = $path
            Status = 'Tombstone'
            SourcePack = $old.SourcePack
            OriginalSha256 = $old.Sha256
            ReplacementSha256 = $current.Sha256
            MovedTo = $null
        }
        [void]$recordedPaths.Add($path)
        Write-RunLog "Historical path Tombstone: $path (source $($old.SourcePack))." 'OK'
    }
    $remaining = @(Get-MissingSteamPackPaths -PreviousPaths @($HistoricalIndex.Keys) -CurrentPaths @($stageIndex.Keys))
    if ($remaining.Count -gt 0) { Fail-Pack 11 "Final historical map audit still has missing paths: $($remaining -join ', ')" }
    return @($records | Sort-Object Path)
}

function Assert-HistoricalDeletionPolicyPreflight {
    param(
        [Parameter(Mandatory)]$HistoricalIndex,
        [Parameter(Mandatory)][string]$PackGuid,
        [Parameter(Mandatory)][array]$Mods,
        [Parameter(Mandatory)]$Infrastructure,
        [AllowEmptyCollection()][array]$RetiredMods = @()
    )

    $planned = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $addDirectory = {
        param([string]$Directory, [string]$Prefix)
        if (-not (Test-Path -LiteralPath $Directory -PathType Container)) { return }
        $directoryItem = Get-Item -LiteralPath $Directory -Force
        if (($directoryItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail-Pack 11 "Planned Steam content root is a reparse point: $Directory" }
        $reparsePoints = @(Get-ChildItem -LiteralPath $Directory -Recurse -Force | Where-Object {
            ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
        })
        if ($reparsePoints.Count -gt 0) { Fail-Pack 11 "Reparse point in planned Steam content: $($reparsePoints[0].FullName)" }
        foreach ($file in @(Get-ChildItem -LiteralPath $Directory -Recurse -File -Force)) {
            $relative = $file.FullName.Substring($Directory.Length).TrimStart('\').Replace('\','/')
            [void]$planned.Add(($Prefix.TrimEnd('/') + '/' + $relative).TrimEnd('/'))
        }
    }
    & $addDirectory (Join-Path $script:Root "SerpsModsHost\BepInEx\plugins\$PackGuid") "BepInEx/plugins/$PackGuid"
    # APIShared is an external dependency; historical removals are inventoried separately.
    foreach ($mod in $Mods) { & $addDirectory $mod.PackageDirectory "BepInEx/plugins/$PackGuid/Mods/$($mod.PluginGuid)" }
    foreach ($mod in $RetiredMods) { & $addDirectory $mod.Directory "BepInEx/plugins/$PackGuid/Mods/$($mod.Guid)" }
    foreach ($guaranteed in @('info.json','preview.png',"BepInEx/plugins/$PackGuid/serps-modpack.json","BepInEx/plugins/$PackGuid/Provenance/pack-inputs.json")) {
        [void]$planned.Add($guaranteed)
    }

    $overlayRoot = Join-Path $script:Root 'SerpsModsHost\SteamTombstones'
    $overlay = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    if (Test-Path -LiteralPath $overlayRoot -PathType Container) {
        $overlayRootItem = Get-Item -LiteralPath $overlayRoot -Force
        if (($overlayRootItem.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { Fail-Pack 11 "Tombstone overlay root is a reparse point: $overlayRoot" }
        $overlayReparsePoints = @(Get-ChildItem -LiteralPath $overlayRoot -Recurse -Force | Where-Object {
            ($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0
        })
        if ($overlayReparsePoints.Count -gt 0) { Fail-Pack 11 "Reparse point in tombstone overlay: $($overlayReparsePoints[0].FullName)" }
        foreach ($file in @(Get-ChildItem -LiteralPath $overlayRoot -Recurse -File -Force | Where-Object { $_.Name -cne '.gitkeep' })) {
            $relative = Assert-SteamArchivePath -Path ($file.FullName.Substring($overlayRoot.Length).TrimStart('\').Replace('\','/')) -PackGuid $PackGuid
            if (-not $overlay.Add($relative)) { Fail-Pack 11 "Duplicate tombstone overlay path: $relative" }
        }
    }

    $safeMissing = 0
    foreach ($path in @(Get-MissingSteamPackPaths -PreviousPaths @($HistoricalIndex.Keys) -CurrentPaths @($planned))) {
        if ($overlay.Contains($path)) { $safeMissing++; continue }
        $action = Get-SteamMissingPathAction -Path $path -PackGuid $PackGuid
        if ($action -in @('XamlTombstone','Retain')) { $safeMissing++; continue }
        $expected = Join-Path $overlayRoot $path.Replace('/','\')
        Fail-Pack 11 "Historical path '$path' has no safe automatic deletion rule. Add a reviewed replacement at '$expected'. DLL removals require a verified inert assembly."
    }
    return $safeMissing
}

function Set-HostVersion {
    param([string]$Version)
    $source = Join-Path $script:Root 'SerpsModsHost\src\SerpsModsHostBootstrap.cs'
    $text = [IO.File]::ReadAllText($source)
    $text = [regex]::Replace($text, '(PluginVersion\s*=\s*")[^"]+("\s*;)', '${1}' + $Version + '${2}', 1)
    Write-Utf8CrLf -Path $source -Text $text
    $infoPath = Join-Path $script:Root 'SerpsModsHost\info.json'
    $info = Get-Content -LiteralPath $infoPath -Raw | ConvertFrom-Json
    $info.Version = $Version
    if ($info.Description -notmatch '^APIShared must') { $info.Description = 'APIShared must be subscribed separately in the Steam Workshop! / APIShared muss separat im Steam Workshop abonniert werden!' + "`n`n" + $info.Description }
    $info | Add-Member -NotePropertyName APISharedWorkshopUrl -NotePropertyValue "https://steamcommunity.com/sharedfiles/filedetails/?id=$ApiSharedWorkshopItemId" -Force
    foreach ($dependency in @($info.Dependencies)) {
        if ($dependency.GUID -eq 'APIShared_Serp') { $dependency.MinimumVersion = $apiSharedInfrastructure.Version }
    }
    if (@($info.SerpChangelog | Where-Object { [string]$_.Version -eq $Version }).Count -eq 0) {
        $entry = [pscustomobject][ordered]@{ Version = $Version; Changes = @('Updated unified Steam Workshop package contents.') }
        $info.SerpChangelog = @($entry) + @($info.SerpChangelog)
    }
    Write-JsonCrLf -Path $infoPath -Value $info
    $stubPath = Join-Path $script:Root 'SerpsModsHost\serps-modpack.json'
    $stub = Get-Content -LiteralPath $stubPath -Raw | ConvertFrom-Json
    $stub.PackVersion = $Version; $stub.HostVersion = $Version
    Write-JsonCrLf -Path $stubPath -Value $stub
}

function Get-PackReleaseState {
    param([object[]]$Releases)
    $testCandidates=@($Releases|Where-Object {$_.isDraft -and ([string]$_.tagName).StartsWith('SerpsMods/test/v',[StringComparison]::Ordinal)})
    $prefix=if($TestMode -and $testCandidates.Count){'SerpsMods/test/v'}else{'SerpsMods/v'}
    $candidate = @($Releases | Where-Object {
        ([bool]$_.isDraft -eq ($TestMode -and $testCandidates.Count -gt 0)) -and (Test-StableReleaseTag $_ $prefix)
    } | Sort-Object publishedAt -Descending | Select-Object -First 1)
    if ($candidate.Count -eq 0) { return $null }
    $tag = [string]$candidate[0].tagName
    $version = ($tag.Substring($prefix.Length) -split '-')[0]
    $cache = Join-Path $script:OutputRoot "cache\pack\v$version"
    if ($tag.StartsWith('SerpsMods/test/v', [StringComparison]::Ordinal)) { $cache = Join-Path $cache ($tag.Substring('SerpsMods/test/v'.Length)) }
    [void](New-Item -ItemType Directory -Path $cache -Force)
    $view = Invoke-Checked -FilePath 'gh' -Arguments @('release','view',$tag,'--repo',$script:Repository,'--json','tagName,isDraft,targetCommitish') -FailureCode 10
    $release = ($view.Output -join "`n") | ConvertFrom-Json
    if ($release.tagName -cne $tag -or [bool]$release.isDraft -ne [bool]$candidate[0].isDraft) { Fail-Pack 10 "Pack release identity/mode mismatch: $tag" }
    $path = Get-CachedSteamReleaseAsset $cache 'SerpsMods.provenance.json' $tag $script:Repository {
        param($asset)
        $p = [IO.File]::ReadAllText($asset) | ConvertFrom-Json
        return $p.Pack -ceq 'SerpsMods' -and $p.PackGuid -ceq $PackGuid -and $p.Version -ceq $version -and
            $p.Commit -ceq $release.targetCommitish -and $p.Map.Sha256 -match '^[a-f0-9]{64}$'
    }
    if (-not (Test-Path -LiteralPath $path)) { Fail-Pack 10 "Latest pack provenance is missing for $tag." }
    $provenance = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    $mapFileName = [string]$provenance.Map.File
    if ([string]::IsNullOrWhiteSpace($mapFileName) -or [IO.Path]::GetFileName($mapFileName) -cne $mapFileName -or
        -not $mapFileName.EndsWith('.map', [StringComparison]::OrdinalIgnoreCase)) {
        Fail-Pack 10 "Latest pack provenance contains an invalid map filename for $tag."
    }
    $expected = [string]$provenance.Map.Sha256
    $mapPath = Get-CachedSteamReleaseAsset $cache $mapFileName $tag $script:Repository {
        param($asset) (Test-ReleaseAssetHash $asset $expected) -and (Get-Item -LiteralPath $asset).Length -eq [long]$provenance.Map.Size
    }
    $mapShaPath = Get-CachedSteamReleaseAsset $cache ($mapFileName + '.sha256') $tag $script:Repository {param($asset) Test-ReleaseChecksum $asset $expected}
    foreach ($required in @($mapPath,$mapShaPath)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { Fail-Pack 10 "Latest pack release asset is missing for ${tag}: $required" }
    }
    $actualMapHash = Get-Sha256 $mapPath
    $declaredMapHash = [string]$provenance.Map.Sha256
    $shaText = [IO.File]::ReadAllText($mapShaPath)
    $shaMatch = [regex]::Match($shaText, '(?i)\b([0-9a-f]{64})\b')
    if (-not $shaMatch.Success -or
        -not [string]::Equals($actualMapHash, $declaredMapHash, [StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals($actualMapHash, $shaMatch.Groups[1].Value, [StringComparison]::OrdinalIgnoreCase) -or
        [long]$provenance.Map.Size -ne (Get-Item -LiteralPath $mapPath).Length) {
        Fail-Pack 10 "Latest pack map verification failed for $tag."
    }
    return [pscustomobject]@{ Tag = $tag; Version = $version; Provenance = $provenance; MapPath = $mapPath; MapShaPath = $mapShaPath }
}

function Get-RetiredModPlan {
    param(
        [AllowNull()]$PreviousPack,
        [Parameter(Mandatory)][array]$ActiveMods
    )

    $retiredMods = @()
    $missingTombstones = @()
    if ($null -ne $PreviousPack) {
        $activeGuids = @($ActiveMods.PluginGuid)
        foreach ($oldMod in @($PreviousPack.Provenance.Mods)) {
            if ([string]$oldMod.State -ne 'Retired' -and [string]$oldMod.Guid -in $activeGuids) {
                continue
            }

            $tombstone = Join-Path $script:Root "SerpsModsHost\Tombstones\$([string]$oldMod.Guid)"
            if (-not (Test-Path -LiteralPath $tombstone -PathType Container)) {
                $missingTombstones += [pscustomobject]@{
                    Name = [string]$oldMod.Name
                    Guid = [string]$oldMod.Guid
                    Version = [string]$oldMod.Version
                    PreviousPack = [string]$PreviousPack.Tag
                    ExpectedDirectory = $tombstone
                }
                continue
            }

            $dll = Get-CecilPluginMetadata -Directory $tombstone
            if ($dll.Guid -ne [string]$oldMod.Guid -or $dll.Version -ne [string]$oldMod.Version -or $dll.HostDependencyCount -ne 1) {
                Fail-Pack 8 "Tombstone DLL identity/dependency mismatch for $([string]$oldMod.Guid)."
            }
            $retiredMods += [pscustomobject]@{
                Name = [string]$oldMod.Name; Guid = [string]$oldMod.Guid; Version = [string]$oldMod.Version
                ReleaseUrl = [string]$oldMod.ReleaseUrl; ReleaseTag = [string]$oldMod.ReleaseTag; SourceCommit = [string]$oldMod.SourceCommit
                PackageSha256 = [string]$oldMod.PackageSha256; Directory = $tombstone; TombstoneSha256 = Get-DirectorySignature $tombstone
            }
        }
    }

    return [pscustomobject]@{
        RetiredMods = @($retiredMods)
        MissingTombstones = @($missingTombstones)
    }
}

function Get-FileRecords {
    param([string]$Directory)
    $files = @(Get-ChildItem -LiteralPath $Directory -Recurse -File | Sort-Object FullName)
    $records = @()
    foreach ($file in $files) {
        $records += [ordered]@{ Path = $file.FullName.Substring($Directory.Length).TrimStart('\').Replace('\','/'); Sha256 = Get-Sha256 $file.FullName; Size = $file.Length }
    }
    return $records
}

function Get-DirectorySignature {
    param([Parameter(Mandatory)][string]$Directory)
    $lines = @(Get-FileRecords $Directory | ForEach-Object { "$($_.Path)|$($_.Sha256)|$($_.Size)" })
    $bytes = [Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','').ToLowerInvariant() }
    finally { $sha.Dispose() }
}

function Resolve-WorkshopPackager {
    if (-not [string]::Equals($WorkshopPackagerPath, 'AUTO', [StringComparison]::OrdinalIgnoreCase)) {
        if (-not (Test-Path -LiteralPath $WorkshopPackagerPath -PathType Leaf)) { Fail-Pack 2 "Workshop packager not found: $WorkshopPackagerPath" }
        return (Resolve-Path $WorkshopPackagerPath).Path
    }
    $project = Join-Path $script:ScriptExtenderRoot 'src\SHCDESE.WorkshopPackager\src\SHCDESE.WorkshopPackager\SHCDESE.WorkshopPackager.csproj'
    if (-not (Test-Path -LiteralPath $project)) { Fail-Pack 2 "Workshop packager project not found: $project" }
    if ($Validate) { return "AUTO ($project)" }
    Invoke-Checked -FilePath 'dotnet' -Arguments @('build',$project,'--configuration','Release','--nologo') -FailureCode 9 | Out-Null
    $executables = @(Get-ChildItem -LiteralPath (Split-Path (Split-Path $project -Parent) -Parent) -Recurse -File -Filter 'SHCDESE.WorkshopPackager.exe' | Sort-Object LastWriteTimeUtc -Descending)
    if ($executables.Count -eq 0) { Fail-Pack 9 'Workshop packager build succeeded but no executable was found.' }
    return $executables[0].FullName
}

function Publish-PackRelease {
    param(
        [string]$Version,
        [string]$ZipPath,
        [string]$ZipShaPath,
        [string]$MapPath,
        [string]$MapShaPath,
        [string]$ProvenancePath,
        [string]$NotesPath,
        [string]$WorkshopZipPath
    )
    $commit = ((Invoke-Git @('rev-parse','HEAD')).Output -join '').Trim()
    $tag = if ($TestMode) { "SerpsMods/test/v$Version-$($commit.Substring(0,12))" } else { "SerpsMods/v$Version" }
    $assets = @($ZipPath,$ZipShaPath,$MapPath,$MapShaPath,$ProvenancePath)
    if ($WorkshopZipPath) { $assets += $WorkshopZipPath }
    $existing = Invoke-Checked -FilePath 'gh' -Arguments @('release','view',$tag,'--repo',$script:Repository,'--json','isDraft,targetCommitish') -FailureCode 10 -AllowFailure
    if ($existing.ExitCode -eq 0) {
        $details = ($existing.Output -join "`n") | ConvertFrom-Json
        if (-not $details.isDraft -or [string]$details.targetCommitish -ne $commit) { Fail-Pack 10 "Pack release tag already exists and cannot be resumed: $tag" }
        Invoke-Checked -FilePath 'gh' -Arguments (@('release','upload',$tag,'--repo',$script:Repository,'--clobber') + $assets) -FailureCode 10 | Out-Null
        Invoke-Checked -FilePath 'gh' -Arguments @('release','edit',$tag,'--repo',$script:Repository,'--notes-file',$NotesPath) -FailureCode 10 | Out-Null
    } else {
        Invoke-Checked -FilePath 'gh' -Arguments (@('release','create',$tag,'--repo',$script:Repository,'--draft','--target',$commit,'--title',"$PackName v$Version",'--notes-file',$NotesPath) + $assets) -FailureCode 10 | Out-Null
    }
    if (-not $TestMode) { Invoke-Checked -FilePath 'gh' -Arguments @('release','edit',$tag,'--repo',$script:Repository,'--draft=false') -FailureCode 10 | Out-Null }
    $published = Invoke-Checked -FilePath 'gh' -Arguments @('release','view',$tag,'--repo',$script:Repository,'--json','assets') -FailureCode 10
    $publishedDetails = ($published.Output -join "`n") | ConvertFrom-Json
    $zipName = [IO.Path]::GetFileName($ZipPath)
    $zipAsset = @($publishedDetails.assets | Where-Object { [string]$_.name -ceq $zipName })
    if ($zipAsset.Count -ne 1) { Fail-Pack 10 "Published pack release does not expose exactly one ZIP asset named $zipName." }
    # README release index is maintained only on an explicit documentation request.
}

try {
    Write-RunLog "Starting $PackName pack workflow; validate=$Validate."
    Initialize-WorkshopBootstrapEnvironment $script:Root
    if (-not $ApiSharedPreviewPath) { $ApiSharedPreviewPath = $PreviewPath }
    if (-not (Test-Path -LiteralPath $ApiSharedPreviewPath -PathType Leaf)) { Fail-Pack 2 'APIShared preview image is missing.' }
    $uploadHistory = Get-SteamWorkshopUploadHistory -Path $script:UploadHistoryPath -SeedPath $script:UploadHistorySeedPath -AppId $SteamAppId -StateName $SteamStateName
    Write-RunLog "Loaded $(@($uploadHistory.Uploads).Count) confirmed Steam uploads from $script:UploadHistoryPath."
    $mods = @(Get-ModNames | ForEach-Object { Get-PluginSourceMetadata $_ })
    $duplicateGuids = @($mods | Group-Object PluginGuid | Where-Object Count -gt 1)
    if ($duplicateGuids.Count -gt 0) { Fail-Pack 2 "Duplicate plugin GUIDs: $(@($duplicateGuids.Name) -join ', ')" }
    foreach ($mod in $mods) {
        if ($mod.DependencyCount -gt 1) { Fail-Pack 3 "Duplicate host dependencies in $($mod.Name)." }
        try { [void](Assert-ScriptExtenderXamlPatchContract -Directory $mod.Directory) }
        catch { Fail-Pack 3 "$($mod.Name) violates the XAML patch contract: $($_.Exception.Message)" }
    }
    if (-not (Test-Path -LiteralPath $PreviewPath -PathType Leaf)) { Fail-Pack 2 "Steam preview image not found: $PreviewPath" }
    [void](Resolve-CecilLoadPath)
    $resolvedPackager = Resolve-WorkshopPackager

    $branch = ((Invoke-Git @('branch','--show-current') 2).Output -join '').Trim()
    if ($branch -ne $script:Branch) { Fail-Pack 2 "Expected branch '$($script:Branch)', current branch is '$branch'." }
    $status = (Invoke-Git @('status','--porcelain=v1','--untracked-files=normal','--','.',':(exclude)APIShared') 2).Output
    if ($status.Count -gt 0 -and -not $Validate) { Fail-Pack 2 "Git working tree must be clean:`r`n$($status -join "`r`n")" }
    if ($status.Count -gt 0) { Write-RunLog 'Validation continues with a dirty tree; a publishing run would stop here.' 'WARN' }
    . (Join-Path $script:Root 'Shared/Tools/ApiSharedRepository/ApiShared.Common.ps1')
    $null=Assert-ApiMainSelection $script:Root $env:SHCDE_API_SHARED_MAIN_SELECTION
    Invoke-Checked -FilePath 'gh' -Arguments @('auth','status') -FailureCode 2 | Out-Null
    if (-not $Validate) {
        Invoke-Git @('fetch','--prune','origin',$script:Branch,'--tags') 5 | Out-Null
        $head = ((Invoke-Git @('rev-parse','HEAD')).Output -join '').Trim()
        $remote = ((Invoke-Git @('rev-parse',"origin/$($script:Branch)")).Output -join '').Trim()
        if ($head -ne $remote) { Fail-Pack 5 "HEAD must equal origin/$($script:Branch) before publishing." }
    }

    $releaseConfigPath = Join-Path $script:Root 'Shared\Tools\Release\release-projects.json'
    $releaseConfig = Get-Content -LiteralPath $releaseConfigPath -Raw | ConvertFrom-Json
    if ($null -eq $releaseConfig.ApiShared) { Fail-Pack 3 'Release configuration lacks the required APIShared infrastructure metadata.' }
    $apiSharedSourceInfoPath = Join-Path $script:ApiMainRoot 'info.json'
    $apiSharedDirectory = Join-Path $script:ApiMainRoot "BepInEx\plugins\$([string]$releaseConfig.ApiShared.Guid)"
    $apiSharedInfoPath = Join-Path $apiSharedDirectory 'info.json'
    $apiSharedDllPath = Join-Path $apiSharedDirectory 'APIShared.dll'
    if (-not (Test-Path -LiteralPath $apiSharedSourceInfoPath -PathType Leaf)) {
        Fail-Pack 3 "APIShared source manifest is missing: $apiSharedSourceInfoPath"
    }
    if (-not (Test-Path -LiteralPath $apiSharedInfoPath -PathType Leaf) -or -not (Test-Path -LiteralPath $apiSharedDllPath -PathType Leaf)) {
        Fail-Pack 3 "APIShared infrastructure package is incomplete: $apiSharedDirectory"
    }
    $apiSharedSourceInfo = Get-Content -LiteralPath $apiSharedSourceInfoPath -Raw | ConvertFrom-Json
    $apiSharedInfo = Get-Content -LiteralPath $apiSharedInfoPath -Raw | ConvertFrom-Json
    if ([string]$apiSharedSourceInfo.GUID -cne [string]$releaseConfig.ApiShared.Guid -or
        [string]$apiSharedSourceInfo.Version -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
        Fail-Pack 3 "APIShared source manifest identity is invalid."
    }
    if ([string]$apiSharedInfo.GUID -cne [string]$apiSharedSourceInfo.GUID -or
        [string]$apiSharedInfo.Version -cne [string]$apiSharedSourceInfo.Version) {
        Fail-Pack 3 "APIShared infrastructure identity differs from the source manifest."
    }
    $apiSharedAssembly = Get-CecilPluginMetadata -Directory $apiSharedDirectory
    if ($apiSharedAssembly.Guid -cne [string]$apiSharedSourceInfo.GUID -or
        $apiSharedAssembly.Version -cne [string]$apiSharedSourceInfo.Version) {
        Fail-Pack 3 "Built APIShared infrastructure DLL differs from the source manifest v$([string]$apiSharedSourceInfo.Version)."
    }
    foreach ($consumer in $mods) {
        $manifest = $consumer.Manifest
        foreach ($dependency in @($manifest.Dependencies | Where-Object { $_.GUID -ceq 'APIShared_Serp' })) {
            if ($dependency.MinimumVersion) { Assert-ApiSharedMinimumVersion $apiSharedSourceInfo.Version $dependency.MinimumVersion }
        }
    }
    $apiSharedChanges = @($apiSharedSourceInfo.SerpChangelog | Where-Object {
        [string]$_.Version -ceq [string]$apiSharedSourceInfo.Version
    })
    if ($apiSharedChanges.Count -ne 1 -or @($apiSharedChanges[0].Changes).Count -eq 0) {
        Fail-Pack 3 "APIShared lacks exactly one non-empty current changelog entry for v$([string]$apiSharedSourceInfo.Version)."
    }
    $apiSharedInfrastructure = [pscustomobject]@{
        Name = [string]$apiSharedInfo.Name
        Repository = [string]$releaseConfig.ApiShared.Repository
        Guid = [string]$apiSharedInfo.GUID
        Version = [string]$apiSharedInfo.Version
        ProjectDirectory = Split-Path -Parent $apiSharedSourceInfoPath
        Directory = $apiSharedDirectory
        DirectorySha256 = Get-DirectorySignature $apiSharedDirectory
        NeedsRelease = $false
        ReleaseReason = ''
        ReleaseTag = $null
        ReleaseUrl = $null
        SourceCommit = $null
        PackageSha256 = $null
        ReleasePackage = $null
    }
    try { [void](Assert-ScriptExtenderXamlPatchContract -Directory $apiSharedDirectory) }
    catch { Fail-Pack 3 "APIShared infrastructure violates the XAML patch contract: $($_.Exception.Message)" }
    $releaseResult = Invoke-Checked -FilePath 'gh' -Arguments @('release','list','--repo',$script:Repository,'--limit','1000','--json','tagName,isDraft,isPrerelease,publishedAt') -FailureCode 2
    $parsedReleases = ($releaseResult.Output -join "`n") | ConvertFrom-Json
    $script:ReleaseList = @()
    foreach ($parsedRelease in $parsedReleases) { $script:ReleaseList += $parsedRelease }
    Write-RunLog "Loaded $($script:ReleaseList.Count) published/draft release records from GitHub."
    . (Join-Path $script:Root 'Shared\Tools\Release\Release.Common.ps1')
    $headCommit = ((Invoke-Git @('rev-parse','HEAD')).Output -join '').Trim()

    # APIShared releases are prepared separately in its independent repository.
    # Consumers only accept the exact reviewed, existing artifact of this mode.
    $apiSharedInfrastructure.ReleasePackage = Get-ApiSharedReleasePackage -Infrastructure $apiSharedInfrastructure
    $apiSharedInfrastructure.NeedsRelease = $false
    $apiSharedInfrastructure.ReleaseReason = 'Use validated APIShared release artifact'
    if ($null -ne $apiSharedInfrastructure.ReleasePackage) {
        $apiPackage = $apiSharedInfrastructure.ReleasePackage
        $apiSharedInfrastructure.Directory = $apiPackage.PackageDirectory
        $apiSharedInfrastructure.DirectorySha256 = Get-DirectorySignature $apiPackage.PackageDirectory
        $apiSharedInfrastructure.ReleaseTag = $apiPackage.ReleaseTag
        $apiSharedInfrastructure.ReleaseUrl = $apiPackage.ReleaseUrl
        $apiSharedInfrastructure.SourceCommit = [string]$apiPackage.Provenance.Commit
        $apiSharedInfrastructure.PackageSha256 = $apiPackage.Sha256
    }

    foreach ($mod in $mods) {
        $mod.NeedsProjectRegistration = $mod.Name -notin @($releaseConfig.Projects)
        $latest = Get-LatestReleaseForMod -ModName $mod.Name -Releases $script:ReleaseList
        if ($null -ne $latest) {
            $mod.LatestVersion = $latest.Version; $mod.LatestTag = $latest.Tag; $mod.LatestUrl = $latest.Url; $mod.LatestCommit = $latest.Commit
            if ([version]$mod.ManifestVersion -lt [version]$latest.Version) { Fail-Pack 3 "$($mod.Name) manifest version $($mod.ManifestVersion) is older than published $($latest.Version)." }
            $comparison = Get-PackagedContentComparison -Mod $mod -BaseCommit $latest.Commit -HeadCommit $headCommit
            $mod.ReleaseContentIsCurrent = [bool]$comparison.IsCurrent
            $mod.ReleaseContentChanges = @($comparison.Paths)
            if (-not $mod.ReleaseContentIsCurrent) {
                Write-RunLog "Packaged plugin content changed for $($mod.Name): $($mod.ReleaseContentChanges -join ', ')"
            }
            if ($mod.ManifestVersion -eq $latest.Version) {
                $mod.TargetVersion = $latest.Version
                try {
                    $candidate = Get-ReleasePackage -Mod $mod
                    $dll = Get-CecilPluginMetadata -Directory $candidate.PackageDirectory
                    $mod.ReleaseArtifactValid = $dll.Guid -eq $mod.PluginGuid -and $dll.Version -eq $mod.TargetVersion -and $dll.HostDependencyCount -eq 1
                    if ($mod.ReleaseArtifactValid) { $mod.Package = $candidate }
                } catch {
                    Write-RunLog "Existing artifact validation failed for $($mod.Name): $($_.Exception.Message)" 'WARN'
                }
            }
        }
        $mod.NeedsRelease = $null -eq $latest -or -not $mod.ReleaseContentIsCurrent -or -not $mod.HasDependency -or -not $mod.ReleaseArtifactValid
        if ($mod.NeedsRelease -and $null -ne $latest -and $mod.ManifestVersion -eq $latest.Version) {
            if (-not $mod.ReleaseContentIsCurrent -and $mod.HasDependency) { Fail-Pack 3 "$($mod.Name) has changed packaged plugin content but no prepared higher version/changelog." }
            $mod.TargetVersion = Get-NextPatchVersion $latest.Version
            $mod.NeedsVersionBump = $true
        }
        $mod.NeedsSourceEdit = -not $mod.HasDependency -or $mod.PluginVersion -ne $mod.TargetVersion -or $mod.NeedsVersionBump
    }

    Write-RunLog 'Planned actions:'
    Write-RunLog ("  APIShared: infrastructure=True, release={0}, target=v{1} ({2})" -f `
        $apiSharedInfrastructure.NeedsRelease, $apiSharedInfrastructure.Version, $apiSharedInfrastructure.ReleaseReason)
    foreach ($mod in $mods) {
        Write-RunLog ("  {0}: dependency={1}, sourceEdit={2}, release={3}, target=v{4}" -f $mod.Name, $mod.HasDependency, $mod.NeedsSourceEdit, $mod.NeedsRelease, $mod.TargetVersion)
    }
    $previousPack = Get-PackReleaseState -Releases $script:ReleaseList
    $historicalSteamFiles = Get-HistoricalSteamFileIndex -History $uploadHistory -PackGuid $PackGuid
    $legacyApiSharedFiles = @(Get-LegacyApiSharedFiles -History $uploadHistory)
    . (Join-Path $script:ApiMainRoot 'tools/WorkshopMigration.ps1')
    $verifiedMigrationMaps = @($uploadHistory.Uploads | Where-Object { $_.PSObject.Properties.Name -notcontains 'Format' -or $_.Format -eq 'Map' } | ForEach-Object { Get-HistoryMapPath -Upload $_ })
    Assert-ApiWorkshopMigrationVersion -Version $apiSharedInfrastructure.Version -LegacyMapPaths $verifiedMigrationMaps -HostGuid $PackGuid
    $apiHistoricalPrefix = "BepInEx/plugins/$PackGuid/Infrastructure/APIShared_Serp/"
    foreach ($path in @($historicalSteamFiles.Keys)) {
        if ($path.StartsWith($apiHistoricalPrefix, [StringComparison]::OrdinalIgnoreCase)) { $historicalSteamFiles.Remove($path) }
    }
    Write-RunLog "Historical Steam audit indexed $($historicalSteamFiles.Count) unique paths from $(@($uploadHistory.Uploads).Count) confirmed uploads." 'OK'
    $retiredPlan = Get-RetiredModPlan -PreviousPack $previousPack -ActiveMods $mods
    $retiredMods = @($retiredPlan.RetiredMods)
    $missingTombstones = @($retiredPlan.MissingTombstones)
    Write-RunLog 'Historical archive hashes will be supplied as migration inventories; obsolete payload files are no longer replaced with retention overlays.' 'OK'
    foreach ($missing in $missingTombstones) {
        Write-RunLog ("  MISSING TOMBSTONE: {0} ({1}) v{2}, previously recorded by {3}; expected at {4}" -f `
            $missing.Name, $missing.Guid, $missing.Version, $missing.PreviousPack, $missing.ExpectedDirectory) 'WARN'
    }
    if ($missingTombstones.Count -gt 0) {
        Fail-Pack 8 'A previously delivered mod has no verified inert tombstone assembly. The Steam workflow is fail-closed and cannot omit it.'
    }
    Write-RunLog "  Workshop packager: $resolvedPackager"
    Write-RunLog "  Preview: $PreviewPath"

    if ($Validate) {
        Write-RunLog 'Validation completed before all source edits, builds, commits, pushes and releases.' 'OK'
        Write-Host "Log: $($script:LogPath)"
        exit 0
    }

    $word=if($TestMode){'TESTPAKET'}else{'VEROEFFENTLICHEN'}
    $confirmation = Read-Host "Type $word to apply reviewed source changes, commit/push and create required releases"
    if ($confirmation -cne $word) { Fail-Pack 2 'Preparation cancelled.' }
    $confirmedStatus = (Invoke-Git @('status','--porcelain=v1','--untracked-files=normal','--','.',':(exclude)APIShared')).Output
    if ($confirmedStatus.Count -gt 0) {
        Fail-Pack 2 "Git working tree changed while awaiting publishing confirmation. Commit or otherwise resolve these changes, then restart the workflow:`r`n$($confirmedStatus -join "`r`n")"
    }
    $journal = [ordered]@{ SchemaVersion = 1; RunId = $script:RunId; StartedUtc = [DateTime]::UtcNow.ToString('o'); CompletedReleases = @(); ReusedReleases = @(); Status = 'source-preparation' }
    Write-JsonCrLf -Path $script:JournalPath -Value $journal
    $apiPackage = $apiSharedInfrastructure.ReleasePackage
    Assert-ApiSharedReleaseAssetFiles (Split-Path -Parent $apiPackage.ProvenancePath) $apiPackage.ReleaseAssets
    $apiSharedInfrastructure.ReleasePackage = $apiPackage
    $apiSharedInfrastructure.Directory = $apiPackage.PackageDirectory
    $apiSharedInfrastructure.DirectorySha256 = Get-DirectorySignature $apiPackage.PackageDirectory
    $apiSharedInfrastructure.ReleaseTag = $apiPackage.ReleaseTag
    $apiSharedInfrastructure.ReleaseUrl = $apiPackage.ReleaseUrl
    $apiSharedInfrastructure.SourceCommit = [string]$apiPackage.Provenance.Commit
    $apiSharedInfrastructure.PackageSha256 = $apiPackage.Sha256
    # APIShared preparation is owned by the separate workflow target.
    $env:SHCDE_API_SHARED_DIR = $apiPackage.PackageDirectory
    $env:SHCDE_API_SHARED_RELEASE_PROOF = $apiPackage.ProvenancePath
    $env:SHCDE_API_SHARED_RELEASE_PROOF_SHA256 = $apiPackage.ProvenanceSha256
    $env:SHCDE_WORKSHOP_TEST = $(if ($TestMode) { '1' } else { '' })
    $null = Assert-ApiConsumerPackage $script:Root $apiPackage.PackageDirectory
    Write-RunLog "Consumer builds use the verified published APIShared SDK: $($apiPackage.ReleaseTag)." 'OK'
    if ($ApiSharedWorkshopItemId -notmatch '^[1-9][0-9]*$') {
        Fail-Pack 2 'Prepare and upload APIShared through the Workshop BAT drivers first. Its saved Workshop ID is used automatically when preparing SerpsMods; test mode requires its own separate APIShared item.'
    }
    Write-RunLog 'The separate APIShared package remains Extender-managed; the modpack payload uses its own receipts.' 'OK'

    foreach ($mod in @($mods | Where-Object NeedsSourceEdit)) { Set-ModCompatibility $mod }
    Add-ReleaseProjectIfNeeded $mods
    foreach ($mod in @($mods | Where-Object NeedsSourceEdit)) { Invoke-ModBuild $mod }

    $paths = @($mods | Where-Object NeedsSourceEdit | ForEach-Object { $_.Name })
    if (@($mods | Where-Object NeedsProjectRegistration).Count -gt 0) { $paths += 'Shared/Tools/Release/release-projects.json' }
    if ($paths.Count -gt 0) {
        Invoke-Git (@('add','--') + $paths) | Out-Null
        $stagedPaths = (Invoke-Git @('diff','--cached','--name-only')).Output
    } else {
        $stagedPaths = @()
    }
    if ($stagedPaths.Count -gt 0) {
        Invoke-Git @('commit','-m','Add Serps Mods Workshop pack compatibility') | Out-Null
        Invoke-Git @('push','origin',$script:Branch) | Out-Null
    }
    $unexpectedStatus = (Invoke-Git @('status','--porcelain=v1','--untracked-files=normal','--','.',':(exclude)APIShared')).Output
    if ($unexpectedStatus.Count -gt 0) {
        Fail-Pack 5 "Unexpected working-tree changes remain after source preparation. They were not committed:`r`n$($unexpectedStatus -join "`r`n")"
    }

    foreach ($mod in @($mods | Where-Object NeedsRelease)) {
        Invoke-ModRelease $mod
        $journal.CompletedReleases = @($journal.CompletedReleases) + "$($mod.Name)/v$($mod.TargetVersion)"
        Write-JsonCrLf -Path $script:JournalPath -Value $journal
        $mod.Package = Get-ReleasePackage -Mod $mod
    }
    foreach ($mod in @($mods | Where-Object { -not $_.NeedsRelease })) {
        if ($null -eq $mod.Package) { $mod.Package = Get-ReleasePackage -Mod $mod }
        Write-RunLog "Using verified existing release $($mod.LatestTag); no consumer build or publication required." 'OK'
        $journal.ReusedReleases = @($journal.ReusedReleases) + $mod.LatestTag
    }
    Write-JsonCrLf -Path $script:JournalPath -Value $journal

    $contentSignature = Get-HostContentSignature -Mods $mods -RetiredMods $retiredMods -Infrastructure $apiSharedInfrastructure -HistoricalUploads @($uploadHistory.Uploads)
    if ($null -ne $previousPack -and [string]$previousPack.Provenance.ContentSignature -eq $contentSignature -and $previousPack.Provenance.PSObject.Properties.Name -contains 'Workshop') {
        Write-RunLog "Pack content is unchanged from $($previousPack.Tag); reusing the published map." 'OK'
        $finalDir = Get-WorkshopFolder (Get-WorkshopConfiguration) (Get-WorkshopConfiguration).SerpsMods ([bool]$TestMode)
        if (-not $finalDir.StartsWith($script:Root + '\', [StringComparison]::OrdinalIgnoreCase)) { Fail-Pack 8 'Unsafe final output path.' }
        $reuseStage=Join-Path $script:OutputRoot ("reuse-"+[guid]::NewGuid().ToString("N")); [void][IO.Directory]::CreateDirectory($reuseStage)
        $workshopZipName=[string]$previousPack.Provenance.Workshop.File
        if ([IO.Path]::GetFileName($workshopZipName) -cne $workshopZipName) { Fail-Pack 10 'Invalid Workshop content ZIP name.' }
        $cache=Split-Path -Parent $previousPack.MapPath
        $workshopZip=Get-CachedSteamReleaseAsset $cache $workshopZipName $previousPack.Tag $script:Repository {
            param($asset) Test-ReleaseAssetHash $asset ([string]$previousPack.Provenance.Workshop.Sha256)
        }
        if ((Get-Sha256 $workshopZip) -cne [string]$previousPack.Provenance.Workshop.Sha256) { Fail-Pack 10 'Workshop content ZIP hash differs from release provenance.' }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        Assert-WorkshopContentZip $workshopZip
        [IO.Compression.ZipFile]::ExtractToDirectory($workshopZip,$reuseStage)
        Copy-Item -LiteralPath $PreviewPath -Destination (Join-Path $reuseStage 'preview.png')
        Assert-PreparedLooseFolder $reuseStage ([string]$previousPack.Version)
        . (Join-Path $PSScriptRoot '../Release/DiagnosticSummary.ps1')
        Write-PackageDiagnosticSummary -Directory $reuseStage -CecilPath (Resolve-CecilLoadPath)
        . (Join-Path $PSScriptRoot '../Validation/DevDiagnostics.Common.ps1')
        try { Assert-NoDevDiagnosticAssemblies -Directory $reuseStage -CecilPath (Resolve-CecilLoadPath) } catch { Fail-Pack 8 $_.Exception.Message }
        Publish-WorkshopFolder $reuseStage $finalDir $script:Root
        Save-WorkshopReleaseReference (Get-WorkshopConfiguration) ([bool]$TestMode) (Join-Path (Split-Path -Parent $previousPack.MapPath) 'SerpsMods.provenance.json')
        Write-RunLog "Reusable pack ready: $finalDir" 'OK'
        exit 0
    }

    $preparedHostInfoPath = Join-Path $script:Root 'SerpsModsHost\info.json'
    $preparedHostInfo = Get-Content -LiteralPath $preparedHostInfoPath -Raw | ConvertFrom-Json
    $previousVersion = if ($null -eq $previousPack) { $null } else { [string]$previousPack.Version }
    $packVersion = Resolve-SteamPackVersion -PreviousVersion $previousVersion -PreparedVersion ([string]$preparedHostInfo.Version)
    Set-HostVersion $packVersion
    Invoke-Git @('add','--','SerpsModsHost/src/SerpsModsHostBootstrap.cs','SerpsModsHost/info.json','SerpsModsHost/serps-modpack.json') | Out-Null
    $hostStatus = (Invoke-Git @('status','--porcelain=v1','--','SerpsModsHost')).Output
    if ($hostStatus.Count -gt 0) {
        Invoke-Git @('commit','-m',"Prepare Serps Mods v$packVersion") | Out-Null
        Invoke-Git @('push','origin',$script:Branch) | Out-Null
    }
    $hostDir = Join-Path $script:Root 'SerpsModsHost'
    $apiProof = Assert-ApiMainSelection $script:Root $env:SHCDE_API_SHARED_MAIN_SELECTION
    $null = Assert-ApiConsumerPackage $script:Root $apiSharedInfrastructure.Directory
    # Like Release-Mod: build the pinned release commit directly against the published SDK
    # instead of the branch dispatcher, which would select a personal APIShared checkout.
    $previousHostBranchPinned = $env:SHCDE_BRANCH_PINNED
    try {
        $env:SHCDE_BRANCH_PINNED = ((Invoke-Git @('rev-parse','HEAD')).Output -join '').Trim()
        Invoke-Checked -FilePath (Join-Path $hostDir 'build.bat') -Arguments @('/nopause','/noinstall') -FailureCode 8 -WorkingDirectory $hostDir | Out-Null
    } finally { $env:SHCDE_BRANCH_PINNED = $previousHostBranchPinned }

    $runRoot = Join-Path $script:OutputRoot "v$packVersion"
    $stage = Join-Path $runRoot 'stage'
    if (Test-Path -LiteralPath $runRoot) { Remove-Item -LiteralPath $runRoot -Recurse -Force }
    $hostStage = Join-Path $stage "BepInEx\plugins\$PackGuid"
    [void](New-Item -ItemType Directory -Path $hostStage -Force)
    Copy-DirectoryContents -Source (Join-Path $hostDir "BepInEx\plugins\$PackGuid") -Destination $hostStage
    $commit = ((Invoke-Git @('rev-parse','HEAD')).Output -join '').Trim()
    $infrastructureRecords = @([ordered]@{
        Name = $apiSharedInfrastructure.Name; Guid = $apiSharedInfrastructure.Guid
        Version = $apiSharedInfrastructure.Version; State = 'External'; RelativePath = ''
        ReleaseUrl = $apiSharedInfrastructure.ReleaseUrl; ReleaseTag = $apiSharedInfrastructure.ReleaseTag
        SourceCommit = $apiSharedInfrastructure.SourceCommit; PackageSha256 = $apiSharedInfrastructure.PackageSha256
        ExpectedSoftDependency = ''; Files = @()
    })
    $packRecords = @()
    foreach ($mod in $mods) {
        $childRelative = "Mods/$($mod.PluginGuid)"
        $childStage = Join-Path $hostStage $childRelative
        [void](New-Item -ItemType Directory -Path $childStage -Force)
        Copy-DirectoryContents -Source $mod.Package.PackageDirectory -Destination $childStage
        try { [void](Assert-ScriptExtenderXamlPatchContract -Directory $childStage) }
        catch { Fail-Pack 8 "Staged Steam consumer $($mod.Name) violates the XAML patch contract: $($_.Exception.Message)" }
        $privateRuntimeCopies = @(Get-ChildItem -LiteralPath $childStage -File -Recurse | Where-Object {
            $_.Name -ieq 'APIShared.dll' -or $_.Name -ieq 'SHCDESE.dll' -or $_.Name -like 'RedBird*.dll'
        })
        if ($privateRuntimeCopies.Count -ne 0) {
            Fail-Pack 8 "Steam consumer $($mod.Name) is not thin: $(@($privateRuntimeCopies.Name) -join ', ')"
        }
        $packRecords += [ordered]@{
            Name = [string]$mod.Manifest.Name; Guid = $mod.PluginGuid; Version = $mod.TargetVersion; State = 'Active'; RelativePath = $childRelative
            ReleaseUrl = $mod.LatestUrl; ReleaseTag = "$($mod.Name)/v$($mod.TargetVersion)"; SourceCommit = [string]$mod.Package.Provenance.Commit
            PackageSha256 = $mod.Package.Sha256; ExpectedSoftDependency = $PackGuid; Files = @(Get-FileRecords $childStage)
        }
    }
    foreach ($mod in $retiredMods) {
        $childRelative = "Mods/$($mod.Guid)"
        $childStage = Join-Path $hostStage $childRelative
        Copy-DirectoryContents -Source $mod.Directory -Destination $childStage
        try { [void](Assert-ScriptExtenderXamlPatchContract -Directory $childStage) }
        catch { Fail-Pack 8 "Staged retired mod $($mod.Name) violates the XAML patch contract: $($_.Exception.Message)" }
        $packRecords += [ordered]@{
            Name = $mod.Name; Guid = $mod.Guid; Version = $mod.Version; State = 'Retired'; RelativePath = $childRelative
            ReleaseUrl = $mod.ReleaseUrl; ReleaseTag = $mod.ReleaseTag; SourceCommit = $mod.SourceCommit
            PackageSha256 = $mod.PackageSha256; ExpectedSoftDependency = $PackGuid; Files = @(Get-FileRecords $childStage)
        }
    }
    $historicalReplacements = @()
    $manifest = [ordered]@{ SchemaVersion = 2; PackGuid = $PackGuid; PackVersion = $packVersion; HostVersion = $packVersion; CreatedUtc = [DateTime]::UtcNow.ToString('o'); RepositoryCommit = $commit; Infrastructure = $infrastructureRecords; Mods = $packRecords; HistoricalReplacements = $historicalReplacements }
    Write-JsonCrLf -Path (Join-Path $hostStage 'serps-modpack.json') -Value $manifest
    $inputProvenanceDir = Join-Path $hostStage 'Provenance'
    [void](New-Item -ItemType Directory -Path $inputProvenanceDir -Force)
    Write-JsonCrLf -Path (Join-Path $inputProvenanceDir 'pack-inputs.json') -Value ([ordered]@{
        SchemaVersion = 2; PackGuid = $PackGuid; PackVersion = $packVersion; Commit = $commit; ContentSignature = $contentSignature; Infrastructure = $infrastructureRecords; Mods = $packRecords; HistoricalReplacements = $historicalReplacements
    })
    Copy-Item -LiteralPath (Join-Path $hostDir 'info.json') -Destination (Join-Path $stage 'info.json')
    Copy-Item -LiteralPath $PreviewPath -Destination (Join-Path $stage 'preview.png')

    $historicalReplacements = @()

    foreach ($record in $packRecords) {
        $record['Files'] = @(Get-FileRecords (Join-Path $hostStage ([string]$record.RelativePath)))
    }
    $manifest['HistoricalReplacements'] = $historicalReplacements
    Write-JsonCrLf -Path (Join-Path $hostStage 'serps-modpack.json') -Value $manifest
    Write-JsonCrLf -Path (Join-Path $inputProvenanceDir 'pack-inputs.json') -Value ([ordered]@{
        SchemaVersion = 2; PackGuid = $PackGuid; PackVersion = $packVersion; Commit = $commit; ContentSignature = $contentSignature; Infrastructure = $infrastructureRecords; Mods = $packRecords; HistoricalReplacements = $historicalReplacements
    })
    $legacyReleases=@(foreach ($upload in @($uploadHistory.Uploads | Where-Object { $_.PSObject.Properties.Name -notcontains 'Format' -or $_.Format -eq 'Map' })) {
        $historyMap=Get-HistoryMapPath -Upload $upload
        $legacyFiles=@(Get-MapFileRecords -MapPath $historyMap -SourcePack ('SerpsMods/v'+$upload.PackVersion) | Where-Object {
            $_.Path.StartsWith("BepInEx/plugins/$PackGuid/",[StringComparison]::Ordinal) -and $_.Path -notmatch '/(config|LobbyModSettings)/'
        } | ForEach-Object { [ordered]@{Path=$_.Path;Size=$_.Size;Sha256=$_.Sha256} })
        if ($legacyFiles.Count) { [ordered]@{Version=[string]$upload.PackVersion;Files=$legacyFiles} }
    })
    Write-RunLog "Migration inventories contain $($legacyReleases.Count) verified old releases; unknown/modified obsolete files will remain untouched." 'OK'

    $null = New-ApiWorkshopInventory -Directory $hostStage -LegacyFiles $legacyApiSharedFiles
    $privateApiCopies = @(Get-ChildItem -LiteralPath $hostStage -Recurse -File -Filter 'APIShared.dll')
    if ($privateApiCopies.Count -ne 0 -or (Test-Path -LiteralPath (Join-Path $hostStage 'Infrastructure\APIShared_Serp'))) { Fail-Pack 8 'The modpack must not contain APIShared.' }
    $zipPath = Join-Path $runRoot "SerpsMods-v$packVersion.zip"
    . (Join-Path $PSScriptRoot '../Release/DiagnosticSummary.ps1')
    Write-PackageDiagnosticSummary -Directory $hostStage -CecilPath (Resolve-CecilLoadPath)
    # DEVDIAG builds are development-only; this gate blocks the pack.
    . (Join-Path $PSScriptRoot '../Validation/DevDiagnostics.Common.ps1')
    try { Assert-NoDevDiagnosticAssemblies -Directory $hostStage -CecilPath (Resolve-CecilLoadPath) } catch { Fail-Pack 8 $_.Exception.Message }
    $zipPackage = New-SteamPackReleaseZip -PackageDirectory $hostStage -DestinationPath $zipPath
    $zipShaPath = "$zipPath.sha256"
    Write-Utf8CrLf -Path $zipShaPath -Text "$($zipPackage.Sha256)  $($zipPackage.File)"

    $workshop=& (Join-Path $PSScriptRoot 'Prepare-SerpsModsWorkshop.ps1') -HostPackage $hostStage -OutputDirectory (Join-Path $runRoot 'Workshop') -Packager $resolvedPackager -APISharedVersion $apiSharedInfrastructure.Version -APISharedWorkshopId $ApiSharedWorkshopItemId -LegacyReleases $legacyReleases
    $mapPath = $workshop.MapPath
    if (-not (Test-Path -LiteralPath $mapPath -PathType Leaf) -or (Get-Item -LiteralPath $mapPath).Length -lt 1024) { Fail-Pack 9 'Workshop packager did not produce a plausible map file.' }
    Test-MapContents -MapPath $mapPath -StageDirectory ((Join-Path $runRoot 'Workshop')+'.bootstrap-stage')
    Copy-Item -LiteralPath $PreviewPath -Destination (Join-Path $workshop.Directory 'preview.png')
    $workshopZipPath=Join-Path $runRoot "SerpsMods-Workshop-v$packVersion.zip"
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($workshop.Directory,$workshopZipPath)
    $workshopZipHash=Get-Sha256 $workshopZipPath
    $mapHash = Get-Sha256 $mapPath
    $mapShaPath = Join-Path $runRoot 'SerpsMods.map.sha256'
    Write-Utf8CrLf -Path $mapShaPath -Text "$mapHash  SerpsMods.map"
    $provenancePath = Join-Path $runRoot 'SerpsMods.provenance.json'
    $omittedWithoutTombstone = @($missingTombstones | ForEach-Object { [ordered]@{
        Name = $_.Name; Guid = $_.Guid; Version = $_.Version; PreviousPack = $_.PreviousPack
    } })
    $provenance = [ordered]@{
        SchemaVersion = 3; Pack = $PackName; PackGuid = $PackGuid; Version = $packVersion; Commit = $commit; ContentSignature = $contentSignature
        ApiSharedCommit = $apiPackage.TargetCommit
        ApiSharedMainCommit = $apiProof.Commit
        CreatedUtc = [DateTime]::UtcNow.ToString('o')
        Package = [ordered]@{ File = $zipPackage.File; RootDirectory = $zipPackage.RootDirectory; Sha256 = $zipPackage.Sha256; Size = $zipPackage.Size; Files = $zipPackage.Files }
        Map = [ordered]@{ File = 'SerpsMods.map'; Sha256 = $mapHash; Size = (Get-Item $mapPath).Length }
        Workshop = [ordered]@{SchemaVersion=1;File=[IO.Path]::GetFileName($workshopZipPath);Sha256=$workshopZipHash;BootstrapGuid=$workshop.BootstrapGuid;BootstrapVersion=$workshop.BootstrapVersion}
        Packager = [ordered]@{ Path = $resolvedPackager; Sha256 = Get-Sha256 $resolvedPackager }; Infrastructure = $infrastructureRecords; Mods = $packRecords
        HistoricalReplacements = $historicalReplacements
        OmittedWithoutTombstone = $omittedWithoutTombstone
    }
    Write-JsonCrLf -Path $provenancePath -Value $provenance
    $notesPath = Join-Path $runRoot 'release-notes.md'
    $noteLines = @("# $PackName v$packVersion",'', "**APIShared must be subscribed separately / APIShared muss separat abonniert werden:** https://steamcommunity.com/sharedfiles/filedetails/?id=$ApiSharedWorkshopItemId", '', '## Included mods','') + @($mods | Sort-Object Name | ForEach-Object { "- $($_.Name) v$($_.TargetVersion)" }) + @(
        '',
        '## Download and verification',
        '',
        "ZIP SHA-256: ``$($zipPackage.Sha256)``",
        "Map SHA-256: ``$mapHash``"
    )
    if ($missingTombstones.Count -gt 0) {
        $noteLines += @('', '## Intentionally omitted without tombstone', '')
        $noteLines += @($missingTombstones | Sort-Object Guid | ForEach-Object { "- $($_.Name) ($($_.Guid)) v$($_.Version)" })
    }
    Write-Utf8CrLf -Path $notesPath -Text ($noteLines -join "`r`n")

    Copy-Item -LiteralPath $PreviewPath -Destination (Join-Path $workshop.Directory 'preview.png')
    Assert-PreparedLooseFolder $workshop.Directory $packVersion
    Publish-PackRelease -Version $packVersion -ZipPath $zipPath -ZipShaPath $zipShaPath -MapPath $mapPath -MapShaPath $mapShaPath -ProvenancePath $provenancePath -NotesPath $notesPath -WorkshopZipPath $workshopZipPath
    $finalDir = Get-WorkshopFolder (Get-WorkshopConfiguration) (Get-WorkshopConfiguration).SerpsMods ([bool]$TestMode)
    if (-not $finalDir.StartsWith($script:Root + '\', [StringComparison]::OrdinalIgnoreCase)) { Fail-Pack 8 'Unsafe final output path.' }
    Publish-WorkshopFolder $workshop.Directory $finalDir $script:Root
    Save-WorkshopReleaseReference (Get-WorkshopConfiguration) ([bool]$TestMode) $provenancePath
    $journal.Status = 'complete'; $journal.CompletedUtc = [DateTime]::UtcNow.ToString('o'); Write-JsonCrLf -Path $script:JournalPath -Value $journal
    Write-RunLog "Steam upload folder ready: $finalDir" 'OK'
    # The release worktree is temporary; keep a copy of the published files where the workspace
    # expects them (.release-output\SerpsMods\v<version>). The large staging folders are left out.
    $coordinatorRoot = Get-ApiMainWorkspace $script:Root
    if (-not $TestMode -and -not [string]::Equals($coordinatorRoot, $script:Root.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
        $copyRoot = Join-Path $coordinatorRoot ".release-output\SerpsMods\v$packVersion"
        Assert-ApiMainUnlinkedPath $copyRoot
        if (Test-Path -LiteralPath $copyRoot) { Remove-Item -LiteralPath $copyRoot -Recurse -Force }
        [void][IO.Directory]::CreateDirectory($copyRoot)
        Get-ChildItem -LiteralPath $runRoot -File | Copy-Item -Destination $copyRoot
        Copy-Item -LiteralPath $script:LogPath -Destination (Join-Path $copyRoot ('release-' + [IO.Path]::GetFileName($script:LogPath)))
        Write-RunLog "Release files copied to the workspace: $copyRoot" 'OK'
    }
    Write-RunLog "Map SHA-256: $mapHash" 'OK'
    Write-Host "Log: $($script:LogPath)"
    exit 0
} catch [SteamPackFailure] {
    Write-RunLog $_.Exception.Message 'ERROR'
    Write-Host "Log: $($script:LogPath)"
    exit $_.Exception.ExitCode
} catch {
    Write-RunLog $_.Exception.ToString() 'ERROR'
    Write-Host "Log: $($script:LogPath)"
    exit 1
} finally {
    $env:SHCDE_API_SHARED_DIR = $previousApiSharedDirectory
    $env:SHCDE_API_SHARED_RELEASE_PROOF = $previousApiSharedReleaseProof
    $env:SHCDE_API_SHARED_RELEASE_PROOF_SHA256 = $previousApiSharedReleaseProofHash
    $env:SHCDE_WORKSHOP_TEST = $previousApiSharedTestMode
    $env:SHCDE_GAME_DIR = $previousGameDirectory
    $env:SHCDESE_EXTENDER_DIR = $previousExtenderDirectory
    $env:SHCDE_MSBUILD = $previousMsBuild
    $env:SHCDE_NATIVE_BASELINE_ROOT = $previousNativeBaselineRoot
}
