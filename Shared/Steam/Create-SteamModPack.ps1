param(
    [Parameter(Mandatory)][string]$PackName,
    [Parameter(Mandatory)][string]$PackGuid,
    [Parameter(Mandatory)][string]$WorkshopPackagerPath,
    [Parameter(Mandatory)][string]$PreviewPath,
    [switch]$Validate,
    [switch]$NoPause
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

class SteamPackFailure : System.Exception {
    [int]$ExitCode
    SteamPackFailure([int]$exitCode, [string]$message) : base($message) { $this.ExitCode = $exitCode }
}

$script:Root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$script:OutputRoot = Join-Path $script:Root '.release-output\SerpsMods'
$script:RunId = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$script:LogDir = Join-Path $script:OutputRoot 'logs'
$script:LogPath = Join-Path $script:LogDir "$($script:RunId).log"
$script:JournalPath = Join-Path $script:OutputRoot 'journal.json'
$script:Repository = 'Serpens66/Stronghold-Crusader-DE-Mods'
$script:Branch = 'main'
$script:CompatibilityChange = 'Added optional Serps Mods Host dependency for Workshop mod-pack load ordering.'
$script:CecilSourcePath = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\core\Mono.Cecil.dll'
$script:CecilLoadPath = $null
$script:BepInExCorePath = Split-Path -Parent $script:CecilSourcePath
$script:ScriptExtenderOutputPath = Join-Path $script:Root 'shcde-script-extender\src\SHCDESE.BepInEx\bin\net481'
$script:ReleaseList = @()

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
    $clientClassificationPattern = '\[(?:(?:Shared\.)?PresetLocal(?:Attribute)?|(?:Shared\.)?SyncPerPlayer(?:Attribute)?)\]'
    $usesSharedActivationProxy = $false

    foreach ($contract in $contracts) {
        $isSharedProxy =
            ($contract.Role -eq 'Host' -and $contract.Property -eq 'HostSettingsEnabled') -or
            ($contract.Role -eq 'Client' -and $contract.Property -eq 'ClientSettingsEnabled')
        if ($isSharedProxy) {
            # These UI properties are non-persisted facades. PresetController resolves
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
    $manifest = $canonicalRows[0].Json
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
        CodeIsCurrent = $false
        ReleaseArtifactValid = $false
        NeedsRelease = $false
        TargetVersion = [string]$manifest.Version
        NeedsVersionBump = $false
        NeedsSourceEdit = $false
        Package = $null
    }
}

function Get-LatestReleaseForMod {
    param([string]$ModName, [object[]]$Releases)
    $candidate = @($Releases | Where-Object {
        (-not [bool]$_.isDraft) -and ([string]$_.tagName).StartsWith($ModName + '/v', [StringComparison]::Ordinal)
    } | Sort-Object publishedAt -Descending | Select-Object -First 1)
    Write-RunLog "Release lookup for ${ModName}: inputs=$($Releases.Count), matches=$($candidate.Count)."
    if ($candidate.Count -eq 0) { return $null }
    $view = Invoke-Checked -FilePath 'gh' -Arguments @('release','view',[string]$candidate[0].tagName,'--repo',$script:Repository,'--json','targetCommitish,url') -FailureCode 2
    $details = ($view.Output -join "`n") | ConvertFrom-Json
    return [pscustomobject]@{
        Tag = [string]$candidate[0].tagName
        Version = ([string]$candidate[0].tagName).Substring($ModName.Length + 2)
        Commit = [string]$details.targetCommitish
        Url = [string]$details.url
    }
}

function Get-ReleasePackage {
    param([Parameter(Mandatory)]$Mod, [switch]$ForceDownload)
    $cache = Join-Path $script:OutputRoot "cache\$($Mod.Name)\v$($Mod.TargetVersion)"
    [void](New-Item -ItemType Directory -Path $cache -Force)
    $base = "$($Mod.Name)-v$($Mod.TargetVersion)"
    $zip = Join-Path $cache "$base.zip"
    $sha = Join-Path $cache "$base.zip.sha256"
    $provenance = Join-Path $cache "$base.provenance.json"
    if ($ForceDownload -or -not (Test-Path -LiteralPath $zip) -or -not (Test-Path -LiteralPath $sha) -or -not (Test-Path -LiteralPath $provenance)) {
        $tag = "$($Mod.Name)/v$($Mod.TargetVersion)"
        Invoke-Checked -FilePath 'gh' -Arguments @('release','download',$tag,'--repo',$script:Repository,'--pattern',"$base.*",'--dir',$cache,'--clobber') -FailureCode 7 | Out-Null
    }
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
    if (Test-Path -LiteralPath $audit) { Remove-Item -LiteralPath $audit -Recurse -Force }
    Expand-Archive -LiteralPath $zip -DestinationPath $audit
    $roots = @(Get-ChildItem -LiteralPath $audit -Directory)
    if ($roots.Count -ne 1) { Fail-Pack 7 "Release ZIP for $($Mod.Name) must contain exactly one root directory." }
    return [pscustomobject]@{ Zip = $zip; Sha256 = $actual; Provenance = $prov; PackageDirectory = $roots[0].FullName }
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
    $path = Join-Path $script:Root 'Shared\Release\release-projects.json'
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
    Invoke-Checked -FilePath $Mod.BuildBat -Arguments @('/nopause') -FailureCode 4 -WorkingDirectory $Mod.Directory | Out-Null
    $built = Get-CecilPluginMetadata -Directory $Mod.PackageDirectory
    if ($built.Guid -ne $Mod.PluginGuid -or $built.Version -ne $Mod.TargetVersion -or $built.HostDependencyCount -ne 1) {
        Fail-Pack 4 "Built DLL audit failed for $($Mod.Name): GUID=$($built.Guid), version=$($built.Version), host dependencies=$($built.HostDependencyCount)."
    }
}

function Invoke-ModRelease {
    param([Parameter(Mandatory)]$Mod)
    Write-RunLog "Publishing required individual release $($Mod.Name) v$($Mod.TargetVersion)."
    Invoke-Checked -FilePath $Mod.ReleaseBat -Arguments @('/noprompt','/nopause') -FailureCode 6 -WorkingDirectory $Mod.Directory | Out-Null
    $Mod.LatestTag = "$($Mod.Name)/v$($Mod.TargetVersion)"
    $Mod.LatestUrl = "https://github.com/$($script:Repository)/releases/tag/$([Uri]::EscapeDataString($Mod.LatestTag))"
}

function Get-NextPatchVersion {
    param([string]$Version)
    $value = [version]$Version
    return '{0}.{1}.{2}' -f $value.Major, $value.Minor, ($value.Build + 1)
}

function Get-HostContentSignature {
    param([object[]]$Mods, [object[]]$RetiredMods = @())
    $lines = [Collections.Generic.List[string]]::new()
    foreach ($mod in @($Mods | Sort-Object PluginGuid)) { $lines.Add("MOD|$($mod.PluginGuid)|$($mod.TargetVersion)|$($mod.Package.Sha256)") }
    foreach ($mod in @($RetiredMods | Sort-Object Guid)) { $lines.Add("RETIRED|$($mod.Guid)|$($mod.Version)|$($mod.TombstoneSha256)") }
    $hostRoot = Join-Path $script:Root 'SerpsModsHost'
    $files = @(Get-ChildItem -LiteralPath $hostRoot -Recurse -File | Where-Object {
        $_.FullName -notmatch '\\BepInEx\\plugins\\' -and $_.Name -notin @('info.json','serps-modpack.json','steam-preview.png')
    } | Sort-Object FullName)
    foreach ($file in $files) {
        $relative = $file.FullName.Substring($hostRoot.Length).TrimStart('\').Replace('\','/')
        $text = if ($file.Extension -in @('.cs','.csproj','.xaml','.txt','.bat')) { [IO.File]::ReadAllText($file.FullName) } else { Get-Sha256 $file.FullName }
        if ($file.Name -eq 'SerpsModsHostPlugin.cs') { $text = [regex]::Replace($text, 'PluginVersion\s*=\s*"[^"]+"', 'PluginVersion="<PACK_VERSION>"') }
        $bytes = [Text.Encoding]::UTF8.GetBytes("$relative`n$text")
        $fileSha = [Security.Cryptography.SHA256]::Create()
        try { $lines.Add("FILE|$relative|" + ([BitConverter]::ToString($fileSha.ComputeHash($bytes)).Replace('-','').ToLowerInvariant())) }
        finally { $fileSha.Dispose() }
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
    [byte[]]$signature = @(0x50, 0x4B, 0x03, 0x04)
    $stream = [IO.File]::Open($MapPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        $matched = 0
        $position = 0L
        while (($value = $stream.ReadByte()) -ne -1) {
            if ($value -eq $signature[$matched]) {
                $matched++
                if ($matched -eq $signature.Length) { return $position - ($signature.Length - 1) }
            } else {
                $matched = if ($value -eq $signature[0]) { 1 } else { 0 }
            }
            $position++
        }
    } finally {
        $stream.Dispose()
    }
    Fail-Pack 9 "Map audit failed: ZIP payload header not found in $MapPath."
}

function Test-MapContents {
    param([Parameter(Mandatory)][string]$MapPath, [Parameter(Mandatory)][string]$StageDirectory)
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

function Set-HostVersion {
    param([string]$Version)
    $source = Join-Path $script:Root 'SerpsModsHost\src\SerpsModsHostPlugin.cs'
    $text = [IO.File]::ReadAllText($source)
    $text = [regex]::Replace($text, '(PluginVersion\s*=\s*")[^"]+("\s*;)', '${1}' + $Version + '${2}', 1)
    Write-Utf8CrLf -Path $source -Text $text
    $infoPath = Join-Path $script:Root 'SerpsModsHost\info.json'
    $info = Get-Content -LiteralPath $infoPath -Raw | ConvertFrom-Json
    $info.Version = $Version
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
    $candidate = @($Releases | Where-Object {
        (-not [bool]$_.isDraft) -and ([string]$_.tagName).StartsWith('SerpsMods/v', [StringComparison]::Ordinal)
    } | Sort-Object publishedAt -Descending | Select-Object -First 1)
    if ($candidate.Count -eq 0) { return $null }
    $tag = [string]$candidate[0].tagName
    $version = $tag.Substring('SerpsMods/v'.Length)
    $cache = Join-Path $script:OutputRoot "cache\pack\v$version"
    [void](New-Item -ItemType Directory -Path $cache -Force)
    Invoke-Checked -FilePath 'gh' -Arguments @('release','download',$tag,'--repo',$script:Repository,'--pattern','SerpsMods.provenance.json','--dir',$cache,'--clobber') -FailureCode 10 | Out-Null
    $path = Join-Path $cache 'SerpsMods.provenance.json'
    if (-not (Test-Path -LiteralPath $path)) { Fail-Pack 10 "Latest pack provenance is missing for $tag." }
    return [pscustomobject]@{ Tag = $tag; Version = $version; Provenance = (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json) }
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
    $project = Join-Path $script:Root 'shcde-script-extender\src\SHCDESE.WorkshopPackager\src\SHCDESE.WorkshopPackager\SHCDESE.WorkshopPackager.csproj'
    if (-not (Test-Path -LiteralPath $project)) { Fail-Pack 2 "Workshop packager project not found: $project" }
    if ($Validate) { return "AUTO ($project)" }
    Invoke-Checked -FilePath 'dotnet' -Arguments @('build',$project,'--configuration','Release','--nologo') -FailureCode 9 | Out-Null
    $executables = @(Get-ChildItem -LiteralPath (Split-Path (Split-Path $project -Parent) -Parent) -Recurse -File -Filter 'SHCDESE.WorkshopPackager.exe' | Sort-Object LastWriteTimeUtc -Descending)
    if ($executables.Count -eq 0) { Fail-Pack 9 'Workshop packager build succeeded but no executable was found.' }
    return $executables[0].FullName
}

function Publish-PackRelease {
    param([string]$Version, [string]$MapPath, [string]$ShaPath, [string]$ProvenancePath, [string]$NotesPath)
    $tag = "SerpsMods/v$Version"
    $commit = ((Invoke-Git @('rev-parse','HEAD')).Output -join '').Trim()
    $existing = Invoke-Checked -FilePath 'gh' -Arguments @('release','view',$tag,'--repo',$script:Repository,'--json','isDraft,targetCommitish') -FailureCode 10 -AllowFailure
    if ($existing.ExitCode -eq 0) {
        $details = ($existing.Output -join "`n") | ConvertFrom-Json
        if (-not $details.isDraft -or [string]$details.targetCommitish -ne $commit) { Fail-Pack 10 "Pack release tag already exists and cannot be resumed: $tag" }
        Invoke-Checked -FilePath 'gh' -Arguments @('release','upload',$tag,'--repo',$script:Repository,'--clobber',$MapPath,$ShaPath,$ProvenancePath) -FailureCode 10 | Out-Null
        Invoke-Checked -FilePath 'gh' -Arguments @('release','edit',$tag,'--repo',$script:Repository,'--notes-file',$NotesPath) -FailureCode 10 | Out-Null
    } else {
        Invoke-Checked -FilePath 'gh' -Arguments @('release','create',$tag,'--repo',$script:Repository,'--draft','--target',$commit,'--title',"$PackName v$Version",'--notes-file',$NotesPath,$MapPath,$ShaPath,$ProvenancePath) -FailureCode 10 | Out-Null
    }
    Invoke-Checked -FilePath 'gh' -Arguments @('release','edit',$tag,'--repo',$script:Repository,'--draft=false') -FailureCode 10 | Out-Null
}

try {
    Write-RunLog "Starting $PackName pack workflow; validate=$Validate."
    $mods = @(Get-ModNames | ForEach-Object { Get-PluginSourceMetadata $_ })
    $duplicateGuids = @($mods | Group-Object PluginGuid | Where-Object Count -gt 1)
    if ($duplicateGuids.Count -gt 0) { Fail-Pack 2 "Duplicate plugin GUIDs: $(@($duplicateGuids.Name) -join ', ')" }
    foreach ($mod in $mods) { if ($mod.DependencyCount -gt 1) { Fail-Pack 3 "Duplicate host dependencies in $($mod.Name)." } }
    if (-not (Test-Path -LiteralPath $PreviewPath -PathType Leaf)) { Fail-Pack 2 "Steam preview image not found: $PreviewPath" }
    [void](Resolve-CecilLoadPath)
    $resolvedPackager = Resolve-WorkshopPackager

    $branch = ((Invoke-Git @('branch','--show-current') 2).Output -join '').Trim()
    if ($branch -ne $script:Branch) { Fail-Pack 2 "Expected branch '$($script:Branch)', current branch is '$branch'." }
    $status = (Invoke-Git @('status','--porcelain=v1','--untracked-files=normal') 2).Output
    if ($status.Count -gt 0 -and -not $Validate) { Fail-Pack 2 "Git working tree must be clean:`r`n$($status -join "`r`n")" }
    if ($status.Count -gt 0) { Write-RunLog 'Validation continues with a dirty tree; a publishing run would stop here.' 'WARN' }
    Invoke-Checked -FilePath 'gh' -Arguments @('auth','status') -FailureCode 2 | Out-Null
    if (-not $Validate) {
        Invoke-Git @('fetch','--prune','origin',$script:Branch,'--tags') 5 | Out-Null
        $head = ((Invoke-Git @('rev-parse','HEAD')).Output -join '').Trim()
        $remote = ((Invoke-Git @('rev-parse',"origin/$($script:Branch)")).Output -join '').Trim()
        if ($head -ne $remote) { Fail-Pack 5 "HEAD must equal origin/$($script:Branch) before publishing." }
    }

    $releaseConfigPath = Join-Path $script:Root 'Shared\Release\release-projects.json'
    $releaseConfig = Get-Content -LiteralPath $releaseConfigPath -Raw | ConvertFrom-Json
    $releaseResult = Invoke-Checked -FilePath 'gh' -Arguments @('release','list','--repo',$script:Repository,'--limit','1000','--json','tagName,isDraft,publishedAt') -FailureCode 2
    $parsedReleases = ($releaseResult.Output -join "`n") | ConvertFrom-Json
    $script:ReleaseList = @()
    foreach ($parsedRelease in $parsedReleases) { $script:ReleaseList += $parsedRelease }
    Write-RunLog "Loaded $($script:ReleaseList.Count) published/draft release records from GitHub."
    . (Join-Path $script:Root 'Shared\Release\Release.Common.ps1')
    . (Join-Path $script:Root 'Shared\Release\ReleaseStatus.Common.ps1')
    $headCommit = ((Invoke-Git @('rev-parse','HEAD')).Output -join '').Trim()

    foreach ($mod in $mods) {
        $mod.NeedsProjectRegistration = $mod.Name -notin @($releaseConfig.Projects)
        $latest = Get-LatestReleaseForMod -ModName $mod.Name -Releases $script:ReleaseList
        if ($null -ne $latest) {
            $mod.LatestVersion = $latest.Version; $mod.LatestTag = $latest.Tag; $mod.LatestUrl = $latest.Url; $mod.LatestCommit = $latest.Commit
            if ([version]$mod.ManifestVersion -lt [version]$latest.Version) { Fail-Pack 3 "$($mod.Name) manifest version $($mod.ManifestVersion) is older than published $($latest.Version)." }
            $comparison = Get-ModStatusComparison -Config (Get-ReleaseConfiguration) -Project $mod.Name -BaseCommit $latest.Commit -HeadCommit $headCommit
            $mod.CodeIsCurrent = [bool]$comparison.IsCurrent
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
        $mod.NeedsRelease = $null -eq $latest -or -not $mod.CodeIsCurrent -or -not $mod.HasDependency -or -not $mod.ReleaseArtifactValid
        if ($mod.NeedsRelease -and $null -ne $latest -and $mod.ManifestVersion -eq $latest.Version) {
            if (-not $mod.CodeIsCurrent -and $mod.HasDependency) { Fail-Pack 3 "$($mod.Name) has unreleased relevant code but no prepared higher version/changelog." }
            $mod.TargetVersion = Get-NextPatchVersion $latest.Version
            $mod.NeedsVersionBump = $true
        }
        $mod.NeedsSourceEdit = -not $mod.HasDependency -or $mod.PluginVersion -ne $mod.TargetVersion -or $mod.NeedsVersionBump
    }

    Write-RunLog 'Planned actions:'
    foreach ($mod in $mods) {
        Write-RunLog ("  {0}: dependency={1}, sourceEdit={2}, release={3}, target=v{4}" -f $mod.Name, $mod.HasDependency, $mod.NeedsSourceEdit, $mod.NeedsRelease, $mod.TargetVersion)
    }
    $previousPack = Get-PackReleaseState -Releases $script:ReleaseList
    $retiredPlan = Get-RetiredModPlan -PreviousPack $previousPack -ActiveMods $mods
    $retiredMods = @($retiredPlan.RetiredMods)
    $missingTombstones = @($retiredPlan.MissingTombstones)
    foreach ($missing in $missingTombstones) {
        Write-RunLog ("  MISSING TOMBSTONE: {0} ({1}) v{2}, previously recorded by {3}; expected at {4}" -f `
            $missing.Name, $missing.Guid, $missing.Version, $missing.PreviousPack, $missing.ExpectedDirectory) 'WARN'
    }
    Write-RunLog "  Workshop packager: $resolvedPackager"
    Write-RunLog "  Preview: $PreviewPath"

    if ($Validate) {
        if ($missingTombstones.Count -gt 0) {
            Write-RunLog 'Validation found missing tombstones. Publishing will require the explicit TROTZDEM_BAUEN confirmation.' 'WARN'
        }
        Write-RunLog 'Validation completed before all source edits, builds, commits, pushes and releases.' 'OK'
        Write-Host "Log: $($script:LogPath)"
        exit 0
    }

    $confirmation = Read-Host 'Type VEROEFFENTLICHEN to apply source changes, commit, push and publish required releases'
    if ($confirmation -cne 'VEROEFFENTLICHEN') { Fail-Pack 2 'Publishing cancelled.' }

    $confirmedStatus = (Invoke-Git @('status','--porcelain=v1','--untracked-files=normal')).Output
    if ($confirmedStatus.Count -gt 0) {
        Fail-Pack 2 "Git working tree changed while awaiting publishing confirmation. Commit or otherwise resolve these changes, then restart the workflow:`r`n$($confirmedStatus -join "`r`n")"
    }
    if ($missingTombstones.Count -gt 0) {
        $missingSummary = @($missingTombstones | ForEach-Object { "$($_.Name) ($($_.Guid)) v$($_.Version)" }) -join ', '
        Write-RunLog "Publishing without tombstones would intentionally omit: $missingSummary" 'WARN'
        $tombstoneConfirmation = Read-Host 'Type TROTZDEM_BAUEN to intentionally omit these previously recorded mods without tombstones'
        if ($tombstoneConfirmation -cne 'TROTZDEM_BAUEN') {
            Fail-Pack 8 'Publishing cancelled because the missing-tombstone override was not confirmed.'
        }
        Write-RunLog "Missing-tombstone override confirmed; intentionally omitting: $missingSummary" 'WARN'
    }

    $journal = [ordered]@{ SchemaVersion = 1; RunId = $script:RunId; StartedUtc = [DateTime]::UtcNow.ToString('o'); CompletedReleases = @(); Status = 'source-preparation' }
    Write-JsonCrLf -Path $script:JournalPath -Value $journal
    foreach ($mod in @($mods | Where-Object NeedsSourceEdit)) { Set-ModCompatibility $mod }
    Add-ReleaseProjectIfNeeded $mods
    foreach ($mod in @($mods | Where-Object NeedsSourceEdit)) { Invoke-ModBuild $mod }

    $paths = @($mods | Where-Object NeedsSourceEdit | ForEach-Object { $_.Name })
    if (@($mods | Where-Object NeedsProjectRegistration).Count -gt 0) { $paths += 'Shared/Release/release-projects.json' }
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
    $unexpectedStatus = (Invoke-Git @('status','--porcelain=v1','--untracked-files=normal')).Output
    if ($unexpectedStatus.Count -gt 0) {
        Fail-Pack 5 "Unexpected working-tree changes remain after source preparation. They were not committed:`r`n$($unexpectedStatus -join "`r`n")"
    }

    foreach ($mod in @($mods | Where-Object NeedsRelease)) {
        Invoke-ModRelease $mod
        $journal.CompletedReleases = @($journal.CompletedReleases) + "$($mod.Name)/v$($mod.TargetVersion)"
        Write-JsonCrLf -Path $script:JournalPath -Value $journal
        $mod.Package = Get-ReleasePackage -Mod $mod -ForceDownload
    }
    foreach ($mod in @($mods | Where-Object { -not $_.NeedsRelease })) {
        if ($null -eq $mod.Package) { $mod.Package = Get-ReleasePackage -Mod $mod }
    }

    $contentSignature = Get-HostContentSignature -Mods $mods -RetiredMods $retiredMods
    if ($null -ne $previousPack -and [string]$previousPack.Provenance.ContentSignature -eq $contentSignature) {
        Write-RunLog "Pack content is unchanged from $($previousPack.Tag); reusing the published map." 'OK'
        $reuseDir = Join-Path $script:OutputRoot "cache\pack\v$($previousPack.Version)"
        Invoke-Checked -FilePath 'gh' -Arguments @('release','download',$previousPack.Tag,'--repo',$script:Repository,'--pattern','SerpsMods.map*','--dir',$reuseDir,'--clobber') -FailureCode 10 | Out-Null
        $finalDir = Join-Path $script:Root $PackName
        if (-not $finalDir.StartsWith($script:Root + '\', [StringComparison]::OrdinalIgnoreCase)) { Fail-Pack 8 'Unsafe final output path.' }
        if (Test-Path -LiteralPath $finalDir) { Remove-Item -LiteralPath $finalDir -Recurse -Force }
        [void](New-Item -ItemType Directory -Path $finalDir)
        Copy-Item -LiteralPath (Join-Path $reuseDir 'SerpsMods.map') -Destination $finalDir
        Copy-Item -LiteralPath $PreviewPath -Destination (Join-Path $finalDir 'preview.png')
        Write-RunLog "Reusable pack ready: $finalDir" 'OK'
        exit 0
    }

    $packVersion = if ($null -eq $previousPack) { '1.0.0' } else { Get-NextPatchVersion $previousPack.Version }
    Set-HostVersion $packVersion
    Invoke-Git @('add','--','SerpsModsHost/src/SerpsModsHostPlugin.cs','SerpsModsHost/info.json','SerpsModsHost/serps-modpack.json') | Out-Null
    $hostStatus = (Invoke-Git @('status','--porcelain=v1','--','SerpsModsHost')).Output
    if ($hostStatus.Count -gt 0) {
        Invoke-Git @('commit','-m',"Prepare Serps Mods v$packVersion") | Out-Null
        Invoke-Git @('push','origin',$script:Branch) | Out-Null
    }
    $hostDir = Join-Path $script:Root 'SerpsModsHost'
    Invoke-Checked -FilePath (Join-Path $hostDir 'build.bat') -Arguments @('/nopause') -FailureCode 8 -WorkingDirectory $hostDir | Out-Null

    $runRoot = Join-Path $script:OutputRoot "v$packVersion"
    $stage = Join-Path $runRoot 'stage'
    if (Test-Path -LiteralPath $runRoot) { Remove-Item -LiteralPath $runRoot -Recurse -Force }
    $hostStage = Join-Path $stage "BepInEx\plugins\$PackGuid"
    [void](New-Item -ItemType Directory -Path $hostStage -Force)
    Copy-DirectoryContents -Source (Join-Path $hostDir "BepInEx\plugins\$PackGuid") -Destination $hostStage
    $packRecords = @()
    foreach ($mod in $mods) {
        $childRelative = "Mods/$($mod.PluginGuid)"
        $childStage = Join-Path $hostStage $childRelative
        [void](New-Item -ItemType Directory -Path $childStage -Force)
        Copy-DirectoryContents -Source $mod.Package.PackageDirectory -Destination $childStage
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
        $packRecords += [ordered]@{
            Name = $mod.Name; Guid = $mod.Guid; Version = $mod.Version; State = 'Retired'; RelativePath = $childRelative
            ReleaseUrl = $mod.ReleaseUrl; ReleaseTag = $mod.ReleaseTag; SourceCommit = $mod.SourceCommit
            PackageSha256 = $mod.PackageSha256; ExpectedSoftDependency = $PackGuid; Files = @(Get-FileRecords $childStage)
        }
    }
    $commit = ((Invoke-Git @('rev-parse','HEAD')).Output -join '').Trim()
    $manifest = [ordered]@{ SchemaVersion = 1; PackGuid = $PackGuid; PackVersion = $packVersion; HostVersion = $packVersion; CreatedUtc = [DateTime]::UtcNow.ToString('o'); RepositoryCommit = $commit; Mods = $packRecords }
    Write-JsonCrLf -Path (Join-Path $hostStage 'serps-modpack.json') -Value $manifest
    $inputProvenanceDir = Join-Path $hostStage 'Provenance'
    [void](New-Item -ItemType Directory -Path $inputProvenanceDir -Force)
    Write-JsonCrLf -Path (Join-Path $inputProvenanceDir 'pack-inputs.json') -Value ([ordered]@{
        SchemaVersion = 1; PackGuid = $PackGuid; PackVersion = $packVersion; Commit = $commit; ContentSignature = $contentSignature; Mods = $packRecords
    })
    Copy-Item -LiteralPath (Join-Path $hostDir 'info.json') -Destination (Join-Path $stage 'info.json')
    Copy-Item -LiteralPath $PreviewPath -Destination (Join-Path $stage 'preview.png')

    $mapPath = Join-Path $runRoot 'SerpsMods.map'
    Invoke-Checked -FilePath $resolvedPackager -Arguments @('-s',$stage,'-o',$mapPath) -FailureCode 9 | Out-Null
    if (-not (Test-Path -LiteralPath $mapPath -PathType Leaf) -or (Get-Item -LiteralPath $mapPath).Length -lt 1024) { Fail-Pack 9 'Workshop packager did not produce a plausible map file.' }
    Test-MapContents -MapPath $mapPath -StageDirectory $stage
    $mapHash = Get-Sha256 $mapPath
    $shaPath = Join-Path $runRoot 'SerpsMods.map.sha256'
    Write-Utf8CrLf -Path $shaPath -Text "$mapHash  SerpsMods.map"
    $provenancePath = Join-Path $runRoot 'SerpsMods.provenance.json'
    $omittedWithoutTombstone = @($missingTombstones | ForEach-Object { [ordered]@{
        Name = $_.Name; Guid = $_.Guid; Version = $_.Version; PreviousPack = $_.PreviousPack
    } })
    $provenance = [ordered]@{
        SchemaVersion = 1; Pack = $PackName; PackGuid = $PackGuid; Version = $packVersion; Commit = $commit; ContentSignature = $contentSignature
        CreatedUtc = [DateTime]::UtcNow.ToString('o'); Map = [ordered]@{ File = 'SerpsMods.map'; Sha256 = $mapHash; Size = (Get-Item $mapPath).Length }
        Packager = [ordered]@{ Path = $resolvedPackager; Sha256 = Get-Sha256 $resolvedPackager }; Mods = $packRecords
        OmittedWithoutTombstone = $omittedWithoutTombstone
    }
    Write-JsonCrLf -Path $provenancePath -Value $provenance
    $notesPath = Join-Path $runRoot 'release-notes.md'
    $noteLines = @("# $PackName v$packVersion",'', '## Included mods','') + @($mods | Sort-Object Name | ForEach-Object { "- $($_.Name) v$($_.TargetVersion)" }) + @('',"SHA-256: ``$mapHash``")
    if ($missingTombstones.Count -gt 0) {
        $noteLines += @('', '## Intentionally omitted without tombstone', '')
        $noteLines += @($missingTombstones | Sort-Object Guid | ForEach-Object { "- $($_.Name) ($($_.Guid)) v$($_.Version)" })
    }
    Write-Utf8CrLf -Path $notesPath -Text ($noteLines -join "`r`n")

    Publish-PackRelease -Version $packVersion -MapPath $mapPath -ShaPath $shaPath -ProvenancePath $provenancePath -NotesPath $notesPath
    $finalDir = Join-Path $script:Root $PackName
    if (-not $finalDir.StartsWith($script:Root + '\', [StringComparison]::OrdinalIgnoreCase)) { Fail-Pack 8 'Unsafe final output path.' }
    if (Test-Path -LiteralPath $finalDir) { Remove-Item -LiteralPath $finalDir -Recurse -Force }
    [void](New-Item -ItemType Directory -Path $finalDir)
    Copy-Item -LiteralPath $mapPath -Destination $finalDir
    Copy-Item -LiteralPath $shaPath -Destination $finalDir
    Copy-Item -LiteralPath $PreviewPath -Destination (Join-Path $finalDir 'preview.png')
    $journal.Status = 'complete'; $journal.CompletedUtc = [DateTime]::UtcNow.ToString('o'); Write-JsonCrLf -Path $script:JournalPath -Value $journal
    Write-RunLog "Steam upload folder ready: $finalDir" 'OK'
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
}
