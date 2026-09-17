Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ReleaseRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

function Get-ReleaseConfiguration {
    $root = Get-ReleaseRoot
    $publicConfigPath = Join-Path $PSScriptRoot 'release-projects.json'
    $config = Get-Content -LiteralPath $publicConfigPath -Raw | ConvertFrom-Json
    $localConfigPath = Join-Path $root 'release.local.json'
    $local = if (Test-Path -LiteralPath $localConfigPath) {
        Get-Content -LiteralPath $localConfigPath -Raw | ConvertFrom-Json
    } else {
        [PSCustomObject]@{}
    }

    $gameDir = if ($null -ne $local.PSObject.Properties['GameDir']) {
        [string]$local.GameDir
    } elseif (-not [string]::IsNullOrWhiteSpace($env:SHCDE_RELEASE_GAME_DIR)) {
        $env:SHCDE_RELEASE_GAME_DIR
    } else {
        'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
    }
    $msBuild = if ($null -ne $local.PSObject.Properties['MSBuild']) {
        [string]$local.MSBuild
    } elseif (-not [string]::IsNullOrWhiteSpace($env:SHCDE_RELEASE_MSBUILD)) {
        $env:SHCDE_RELEASE_MSBUILD
    } else {
        'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    }

    $apiSharedProperty = $config.PSObject.Properties['ApiShared']
    if ($null -eq $apiSharedProperty -or $null -eq $apiSharedProperty.Value) {
        throw "Release configuration is missing the required 'ApiShared' object: $publicConfigPath"
    }
    $apiShared = $apiSharedProperty.Value
    foreach ($propertyName in @('Project', 'Guid', 'Consumers')) {
        if ($null -eq $apiShared.PSObject.Properties[$propertyName]) {
            throw "Release configuration ApiShared is missing '$propertyName': $publicConfigPath"
        }
    }
    foreach ($propertyName in @('Project', 'Guid')) {
        if ([string]::IsNullOrWhiteSpace([string]$apiShared.PSObject.Properties[$propertyName].Value)) {
            throw "Release configuration ApiShared.$propertyName must not be empty: $publicConfigPath"
        }
    }
    if ($null -eq $apiShared.Consumers) {
        throw "Release configuration ApiShared.Consumers must be an object: $publicConfigPath"
    }
    foreach ($consumer in @($apiShared.Consumers.PSObject.Properties)) {
        if ([string]::IsNullOrWhiteSpace($consumer.Name) -or
            [string]$consumer.Value -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
            throw "Release configuration contains an invalid APIShared consumer minimum for '$($consumer.Name)': '$([string]$consumer.Value)'."
        }
    }
    $projects = @($config.Projects | ForEach-Object { [string]$_ })
    if ([string]$apiShared.Project -notin $projects) {
        throw "APIShared project '$([string]$apiShared.Project)' is not release-enabled in $publicConfigPath"
    }

    $additionalReleaseIndexEntries = @()
    $releaseIndexProperty = $config.PSObject.Properties['AdditionalReleaseIndexEntries']
    if ($null -ne $releaseIndexProperty -and $null -ne $releaseIndexProperty.Value) {
        $additionalReleaseIndexEntries = @($releaseIndexProperty.Value)
    }
    foreach ($entry in $additionalReleaseIndexEntries) {
        foreach ($propertyName in @('Project', 'DisplayName', 'AssetNameTemplate', 'HashLabel', 'ShowCodeStatus', 'Position')) {
            if ($null -eq $entry.PSObject.Properties[$propertyName]) {
                throw "Release index entry is missing '$propertyName': $publicConfigPath"
            }
        }
        if ([string]::IsNullOrWhiteSpace([string]$entry.Project) -or
            [string]::IsNullOrWhiteSpace([string]$entry.DisplayName) -or
            [string]::IsNullOrWhiteSpace([string]$entry.AssetNameTemplate) -or
            -not ([string]$entry.AssetNameTemplate).Contains('{version}') -or
            [string]::IsNullOrWhiteSpace([string]$entry.HashLabel) -or
            [string]$entry.Position -notin @('First', 'Last')) {
            throw "Release index entry for '$([string]$entry.Project)' is invalid: $publicConfigPath"
        }
        if ([string]$entry.Project -in $projects) {
            throw "Additional release index project '$([string]$entry.Project)' must not duplicate a release-enabled project."
        }
    }

    return [PSCustomObject]@{
        Root = $root
        Repository = [string]$config.Repository
        Branch = [string]$config.Branch
        Projects = $projects
        ProjectDirectories = $config.ProjectDirectories
        ApiShared = $apiShared
        AdditionalReleaseIndexEntries = $additionalReleaseIndexEntries
        GameDir = $gameDir
        MSBuild = $msBuild
        LocalConfigPath = $localConfigPath
    }
}

function Get-ReleaseIndexEntries {
    param([Parameter(Mandatory)]$Config)

    $entries = [System.Collections.Generic.List[object]]::new()
    foreach ($entry in @($Config.AdditionalReleaseIndexEntries | Where-Object { [string]$_.Position -ceq 'First' })) {
        $entries.Add($entry)
    }
    foreach ($project in $Config.Projects) {
        $entries.Add([PSCustomObject]@{
            Project = [string]$project
            DisplayName = [string]$project
            AssetNameTemplate = $null
            HashLabel = 'SHA-256'
            ShowCodeStatus = $true
            Position = 'Default'
        })
    }
    foreach ($entry in @($Config.AdditionalReleaseIndexEntries | Where-Object { [string]$_.Position -ceq 'Last' })) {
        $entries.Add($entry)
    }
    return @($entries)
}

function Get-ReleaseIndexAssetName {
    param(
        [Parameter(Mandatory)]$Entry,
        [Parameter(Mandatory)][string]$Version
    )

    if ([string]::IsNullOrWhiteSpace([string]$Entry.AssetNameTemplate)) { return $null }
    return ([string]$Entry.AssetNameTemplate).Replace('{version}', $Version)
}

function Get-ReleaseIndexSha256 {
    param(
        [Parameter(Mandatory)]$Entry,
        [AllowNull()][string]$ReleaseBody
    )

    $labels = [System.Collections.Generic.List[string]]::new()
    $labels.Add([string]$Entry.HashLabel)
    if ([bool]$Entry.ShowCodeStatus -and [string]$Entry.HashLabel -cne 'Thin SHA-256') {
        $labels.Add('Thin SHA-256')
    }
    foreach ($label in $labels) {
        $pattern = '(?im)^' + [regex]::Escape($label) + ':\s*`?([0-9a-f]{64})`?'
        $match = [regex]::Match([string]$ReleaseBody, $pattern)
        if ($match.Success) { return $match.Groups[1].Value.ToLowerInvariant() }
    }
    return 'see release'
}

function New-ReleaseIndexRow {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)]$Entry,
        [Parameter(Mandatory)][string]$Version,
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$Commit,
        [Parameter(Mandatory)][string]$Sha256
    )

    $shortCommit = if ($Commit.Length -ge 7) { $Commit.Substring(0, 7) } else { $Commit }
    $commitUrl = "https://github.com/$($Config.Repository)/commit/$Commit"
    $status = [char]0x2014
    if ([bool]$Entry.ShowCodeStatus) {
        $project = [string]$Entry.Project
        $badgeJsonUrl = "https://raw.githubusercontent.com/$($Config.Repository)/release-status/badges/$project.json"
        $badgeUrl = "https://img.shields.io/endpoint?url=$([Uri]::EscapeDataString($badgeJsonUrl))&cacheSeconds=300"
        $reportUrl = "https://github.com/$($Config.Repository)/blob/release-status/reports/$project.md"
        $status = "[![release status]($badgeUrl)]($reportUrl)"
    }
    return "| $([string]$Entry.DisplayName) | [$Version]($Url) | $status | [$shortCommit]($commitUrl) | ``$Sha256`` |"
}

function Get-ApiSharedConsumerMinimum {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)][string]$ModName
    )
    $apiSharedProperty = $Config.PSObject.Properties['ApiShared']
    if ($null -eq $apiSharedProperty -or $null -eq $apiSharedProperty.Value) {
        throw "Resolved release configuration is missing APIShared metadata."
    }
    $consumersProperty = $apiSharedProperty.Value.PSObject.Properties['Consumers']
    if ($null -eq $consumersProperty -or $null -eq $consumersProperty.Value) {
        throw "Resolved release configuration is missing APIShared consumer metadata."
    }
    $consumer = $consumersProperty.Value.PSObject.Properties[$ModName]
    if ($null -eq $consumer) { return $null }
    return [string]$consumer.Value
}

function Get-ReleaseProjectDirectory {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)][string]$Project
    )
    if ($null -ne $Config.ProjectDirectories) {
        $mapping = $Config.ProjectDirectories.PSObject.Properties[$Project]
        if ($null -ne $mapping -and -not [string]::IsNullOrWhiteSpace([string]$mapping.Value)) {
            return ([string]$mapping.Value).Replace('/', [IO.Path]::DirectorySeparatorChar)
        }
    }
    return $Project
}

function Resolve-ReleaseTool {
    param([Parameter(Mandatory)][string]$Name)
    $resolved = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -ne $resolved) { return $resolved.Source }
    if ($Name -ieq 'gh') {
        $programFilesGh = 'C:\Program Files\GitHub CLI\gh.exe'
        $localGh = Join-Path $env:LOCALAPPDATA 'Programs\GitHub CLI\gh.exe'
        foreach ($candidate in @($programFilesGh, $localGh)) {
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
    }
    return $null
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter()][string[]]$Arguments = @(),
        [Parameter()][switch]$AllowFailure
    )
    $resolvedFilePath = Resolve-ReleaseTool -Name $FilePath
    if ([string]::IsNullOrWhiteSpace($resolvedFilePath)) { throw "Required command not found: $FilePath" }
    # Native tools such as git legitimately write progress information to stderr
    # with exit code 0. Under the script-wide Stop preference PowerShell 5.1 would
    # otherwise turn that informational stderr record into a terminating error.
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $resolvedFilePath @Arguments 2>&1)
        $exitCode = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    if (-not $AllowFailure -and $exitCode -ne 0) {
        throw "Command failed ($exitCode): $resolvedFilePath $($Arguments -join ' ')`r`n$($output -join "`r`n")"
    }
    return [PSCustomObject]@{ ExitCode = $exitCode; Output = $output }
}

function Get-Sha256Hex {
    param([Parameter(Mandatory)][string]$Path)
    $stream = [IO.File]::OpenRead($Path)
    try {
        $algorithm = [Security.Cryptography.SHA256]::Create()
        try {
            $bytes = $algorithm.ComputeHash($stream)
        } finally {
            $algorithm.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
    return ([BitConverter]::ToString($bytes)).Replace('-', '').ToLowerInvariant()
}

function Get-PluginMetadata {
    param([Parameter(Mandatory)][string]$ModName)
    $config = Get-ReleaseConfiguration
    if ($ModName -notin $config.Projects) {
        throw "Project is not release-enabled: $ModName"
    }
    $modDir = Join-Path $config.Root (Get-ReleaseProjectDirectory -Config $config -Project $ModName)
    $pluginRoot = Join-Path $modDir 'BepInEx\plugins'
    $infos = @(Get-ChildItem -LiteralPath $pluginRoot -Filter info.json -File -Recurse -ErrorAction Stop)
    if ($infos.Count -ne 1) {
        throw "Expected exactly one plugin info.json below $pluginRoot, found $($infos.Count)."
    }
    $manifest = Get-Content -LiteralPath $infos[0].FullName -Raw | ConvertFrom-Json
    foreach ($property in @('GUID', 'Name', 'Version', 'SerpChangelog')) {
        if ($manifest.PSObject.Properties.Name -notcontains $property) {
            throw "Missing property '$property' in $($infos[0].FullName)."
        }
    }
    if ([string]$manifest.Version -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
        throw "Invalid release version '$($manifest.Version)' in $($infos[0].FullName)."
    }
    $matchingChanges = @($manifest.SerpChangelog | Where-Object { [string]$_.Version -eq [string]$manifest.Version })
    if ($matchingChanges.Count -ne 1 -or @($matchingChanges[0].Changes).Count -eq 0) {
        throw "Expected one non-empty SerpChangelog entry for version $($manifest.Version)."
    }
    return [PSCustomObject]@{
        Config = $config
        ModName = $ModName
        ModDir = $modDir
        BuildBat = Join-Path $modDir 'build.bat'
        PackageDir = $infos[0].Directory.FullName
        PackageFolderName = $infos[0].Directory.Name
        ManifestPath = $infos[0].FullName
        Manifest = $manifest
        Changelog = $matchingChanges[0]
        Version = [string]$manifest.Version
        Tag = "$ModName/v$($manifest.Version)"
    }
}

function Compare-SemanticVersion {
    param(
        [Parameter(Mandatory)][string]$Left,
        [Parameter(Mandatory)][string]$Right
    )
    $pattern = '^(\d+)\.(\d+)\.(\d+)(?:-([^+]+))?(?:\+.*)?$'
    $leftMatch = [regex]::Match($Left, $pattern)
    $rightMatch = [regex]::Match($Right, $pattern)
    if (-not $leftMatch.Success -or -not $rightMatch.Success) {
        throw "Cannot compare invalid semantic versions '$Left' and '$Right'."
    }
    for ($index = 1; $index -le 3; $index++) {
        $leftNumber = [uint64]$leftMatch.Groups[$index].Value
        $rightNumber = [uint64]$rightMatch.Groups[$index].Value
        if ($leftNumber -lt $rightNumber) { return -1 }
        if ($leftNumber -gt $rightNumber) { return 1 }
    }

    $leftPreRelease = $leftMatch.Groups[4].Value
    $rightPreRelease = $rightMatch.Groups[4].Value
    if ([string]::IsNullOrEmpty($leftPreRelease)) {
        return $(if ([string]::IsNullOrEmpty($rightPreRelease)) { 0 } else { 1 })
    }
    if ([string]::IsNullOrEmpty($rightPreRelease)) { return -1 }

    $leftParts = @($leftPreRelease.Split('.'))
    $rightParts = @($rightPreRelease.Split('.'))
    $partCount = [Math]::Max($leftParts.Count, $rightParts.Count)
    for ($index = 0; $index -lt $partCount; $index++) {
        if ($index -ge $leftParts.Count) { return -1 }
        if ($index -ge $rightParts.Count) { return 1 }
        $leftNumber = 0L
        $rightNumber = 0L
        $leftIsNumber = [long]::TryParse($leftParts[$index], [ref]$leftNumber)
        $rightIsNumber = [long]::TryParse($rightParts[$index], [ref]$rightNumber)
        if ($leftIsNumber -and $rightIsNumber) {
            if ($leftNumber -lt $rightNumber) { return -1 }
            if ($leftNumber -gt $rightNumber) { return 1 }
        } elseif ($leftIsNumber) {
            return -1
        } elseif ($rightIsNumber) {
            return 1
        } else {
            $comparison = [string]::CompareOrdinal($leftParts[$index], $rightParts[$index])
            if ($comparison -lt 0) { return -1 }
            if ($comparison -gt 0) { return 1 }
        }
    }
    return 0
}

function Get-ValidatedApiSharedPackage {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)][string]$MinimumVersion
    )
    $apiProject = [string]$Config.ApiShared.Project
    $apiGuid = [string]$Config.ApiShared.Guid
    $apiSourceInfoPath = Join-Path $Config.Root "$apiProject\info.json"
    $apiPackage = Join-Path $Config.Root "$apiProject\BepInEx\plugins\$apiGuid"
    $apiInfoPath = Join-Path $apiPackage 'info.json'
    $apiDllPath = Join-Path $apiPackage 'APIShared.dll'
    if (-not (Test-Path -LiteralPath $apiSourceInfoPath -PathType Leaf)) {
        throw "APIShared source manifest is missing: $apiSourceInfoPath"
    }
    if (-not (Test-Path -LiteralPath $apiInfoPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $apiDllPath -PathType Leaf)) {
        throw "APIShared package is incomplete. Build and validate $apiProject first: $apiPackage"
    }
    $apiSourceInfo = Get-Content -LiteralPath $apiSourceInfoPath -Raw | ConvertFrom-Json
    $apiInfo = Get-Content -LiteralPath $apiInfoPath -Raw | ConvertFrom-Json
    $apiVersion = [string]$apiSourceInfo.Version
    if ([string]$apiSourceInfo.GUID -cne $apiGuid) {
        throw "APIShared source manifest GUID mismatch: expected $apiGuid, found $([string]$apiSourceInfo.GUID)."
    }
    if ($apiVersion -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
        throw "APIShared source manifest contains invalid version '$apiVersion': $apiSourceInfoPath"
    }
    if ([string]$apiInfo.GUID -cne $apiGuid -or [string]$apiInfo.Version -cne $apiVersion) {
        throw "APIShared package identity mismatch: expected $apiGuid v$apiVersion from the source manifest."
    }
    if ((Compare-SemanticVersion -Left $apiVersion -Right $MinimumVersion) -lt 0) {
        throw "APIShared v$apiVersion is below the required minimum v$MinimumVersion."
    }
    return [PSCustomObject]@{
        Directory = $apiPackage
        SourceInfoPath = $apiSourceInfoPath
        InfoPath = $apiInfoPath
        DllPath = $apiDllPath
        Guid = $apiGuid
        Version = $apiVersion
    }
}

function Get-PublishedApiSharedRelease {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)]$Package
    )
    $tag = "APIShared/v$([string]$Package.Version)"
    $assetName = "APIShared-v$([string]$Package.Version).zip"
    $result = Invoke-CheckedCommand -FilePath 'gh' -Arguments @(
        'release', 'view', $tag, '--repo', [string]$Config.Repository,
        '--json', 'tagName,isDraft,url,assets'
    ) -AllowFailure
    if ($result.ExitCode -ne 0) {
        throw "Required APIShared release $tag is not published. Publish APIShared first."
    }
    $release = ($result.Output -join "`n") | ConvertFrom-Json
    if ([string]$release.tagName -cne $tag) {
        throw "Required APIShared release resolved to unexpected tag '$([string]$release.tagName)' instead of '$tag'."
    }
    if ([bool]$release.isDraft) {
        throw "Required APIShared release $tag is still a draft. Publish APIShared first."
    }
    $assets = @($release.assets | Where-Object { [string]$_.name -ceq $assetName })
    if ($assets.Count -ne 1) {
        throw "Required APIShared release $tag must contain exactly one $assetName asset."
    }
    return [PSCustomObject]@{
        Tag = $tag
        Url = [string]$release.url
        AssetName = $assetName
        AssetUrl = [string]$assets[0].url
    }
}

function Get-PreviousPublishedReleaseVersion {
    param([Parameter(Mandatory)]$Metadata)
    $result = Invoke-CheckedCommand -FilePath 'gh' -Arguments @(
        'release', 'list', '--repo', $Metadata.Config.Repository,
        '--limit', '1000', '--json', 'tagName,isDraft,publishedAt'
    )
    $releases = @(($result.Output -join "`n") | ConvertFrom-Json)
    $tagPrefix = "$($Metadata.ModName)/v"
    $matchingReleases = @($releases | Where-Object {
        -not $_.isDraft -and
        ([string]$_.tagName).StartsWith($tagPrefix, [StringComparison]::Ordinal) -and
        [string]$_.tagName -ne $Metadata.Tag
    } | Sort-Object { [DateTimeOffset]$_.publishedAt } -Descending)
    if ($matchingReleases.Count -gt 0) {
        $version = ([string]$matchingReleases[0].tagName).Substring($tagPrefix.Length)
        if ($version -notmatch '^\d+\.\d+\.\d+(?:[-+][0-9A-Za-z.-]+)?$') {
            throw "The previous published release has an invalid version tag: $($matchingReleases[0].tagName)"
        }
        return $version
    }

    # An exact lookup is a safety net if GitHub's release-list response is incomplete.
    $remainingVersions = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in @($Metadata.Manifest.SerpChangelog)) {
        $entryVersion = [string]$entry.Version
        if ((Compare-SemanticVersion -Left $entryVersion -Right $Metadata.Version) -lt 0) {
            $remainingVersions.Add($entryVersion)
        }
    }
    while ($remainingVersions.Count -gt 0) {
        $newestIndex = 0
        for ($index = 1; $index -lt $remainingVersions.Count; $index++) {
            if ((Compare-SemanticVersion -Left $remainingVersions[$index] -Right $remainingVersions[$newestIndex]) -gt 0) {
                $newestIndex = $index
            }
        }
        $candidateVersion = $remainingVersions[$newestIndex]
        $remainingVersions.RemoveAt($newestIndex)
        $candidateTag = "$tagPrefix$candidateVersion"
        $candidateResult = Invoke-CheckedCommand -FilePath 'gh' -Arguments @(
            'release', 'view', $candidateTag, '--repo', $Metadata.Config.Repository,
            '--json', 'tagName,isDraft,publishedAt'
        ) -AllowFailure
        if ($candidateResult.ExitCode -ne 0) { continue }
        $candidate = ($candidateResult.Output -join "`n") | ConvertFrom-Json
        if (-not $candidate.isDraft -and [string]$candidate.tagName -ceq $candidateTag) {
            return $candidateVersion
        }
    }
    return $null
}

function Get-ReleaseChangeLines {
    param(
        [Parameter(Mandatory)]$Metadata,
        [AllowNull()][string]$PreviousVersion
    )
    if ([string]::IsNullOrWhiteSpace($PreviousVersion)) {
        return @('inital release')
    }
    if ((Compare-SemanticVersion -Left $Metadata.Version -Right $PreviousVersion) -le 0) {
        throw "New version $($Metadata.Version) must be newer than the previous published version $PreviousVersion."
    }

    $entries = @($Metadata.Manifest.SerpChangelog | Where-Object {
        $entryVersion = [string]$_.Version
        (Compare-SemanticVersion -Left $entryVersion -Right $PreviousVersion) -gt 0 -and
        (Compare-SemanticVersion -Left $entryVersion -Right $Metadata.Version) -le 0
    })
    if ($entries.Count -eq 0) {
        throw "No changelog entries found after v$PreviousVersion through v$($Metadata.Version)."
    }

    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in $entries) {
        if ($lines.Count -gt 0) { $lines.Add('') }
        $lines.Add("### v$([string]$entry.Version)")
        $lines.Add('')
        foreach ($change in @($entry.Changes)) {
            $lines.Add("- $change")
        }
    }
    return @($lines)
}

function Get-ExtenderDirectory {
    param([Parameter(Mandatory)]$Metadata)
    $localRoot = [IO.Path]::Combine($Metadata.Config.Root, 'shcde-script-extender')
    $candidates = @(
        ([IO.Path]::Combine($Metadata.Config.GameDir, 'BepInEx\plugins\000shcdese')),
        ([IO.Path]::Combine($localRoot, 'mod_output\000shcdese')),
        ([IO.Path]::Combine($localRoot, 'src\SHCDESE.BepInEx\bin\net481'))
    )
    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath ([IO.Path]::Combine($candidate, 'SHCDESE.dll'))) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    throw 'SHCDESE.dll was not found in the local Script Extender or installed game.'
}

function Get-SetupReport {
    param([string]$ModName)
    $config = Get-ReleaseConfiguration
    $checks = [System.Collections.Generic.List[object]]::new()
    function Add-Check([string]$Name, [bool]$Ok, [string]$Detail) {
        $checks.Add([PSCustomObject]@{ Check = $Name; Ok = $Ok; Detail = $Detail })
    }

    foreach ($command in @('git', 'gh', 'dotnet')) {
        $resolved = Resolve-ReleaseTool -Name $command
        Add-Check $command ($null -ne $resolved) $(if ($resolved) { $resolved } else { 'not found' })
    }
    if ($null -ne (Resolve-ReleaseTool -Name 'gh')) {
        $auth = Invoke-CheckedCommand -FilePath 'gh' -Arguments @('auth', 'status') -AllowFailure
        Add-Check 'GitHub authentication' ($auth.ExitCode -eq 0) $(if ($auth.ExitCode -eq 0) { 'authenticated' } else { ($auth.Output -join ' ') })
    }
    Add-Check 'MSBuild' (Test-Path -LiteralPath $config.MSBuild -PathType Leaf) $config.MSBuild
    Add-Check 'Game directory' (Test-Path -LiteralPath $config.GameDir -PathType Container) $config.GameDir
    $bepInEx = [IO.Path]::Combine($config.GameDir, 'BepInEx\core\BepInEx.dll')
    Add-Check 'BepInEx.dll' (Test-Path -LiteralPath $bepInEx -PathType Leaf) $bepInEx
    $crusader = [IO.Path]::Combine($config.GameDir, 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll')
    Add-Check 'CrusaderDE.dll' (Test-Path -LiteralPath $crusader -PathType Leaf) $crusader
    if (-not [string]::IsNullOrWhiteSpace($ModName)) {
        try {
            $metadata = Get-PluginMetadata -ModName $ModName
            Add-Check 'Release whitelist' $true $ModName
            Add-Check 'build.bat' (Test-Path -LiteralPath $metadata.BuildBat -PathType Leaf) $metadata.BuildBat
            $extender = Get-ExtenderDirectory -Metadata $metadata
            Add-Check 'SHCDESE.dll' $true (Join-Path $extender 'SHCDESE.dll')
        } catch {
            Add-Check 'Mod metadata' $false $_.Exception.Message
        }
    }
    return @($checks)
}

function Get-FileHashRecord {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$BasePath)
    $resolvedBase = (Resolve-Path -LiteralPath $BasePath).Path.TrimEnd('\') + '\'
    $resolvedPath = (Resolve-Path -LiteralPath $Path).Path
    if (-not $resolvedPath.StartsWith($resolvedBase, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Path is outside the expected base directory: $resolvedPath"
    }
    $relative = $resolvedPath.Substring($resolvedBase.Length).Replace('\', '/')
    return [PSCustomObject]@{
        Path = $relative
        Sha256 = Get-Sha256Hex -Path $Path
        Size = (Get-Item -LiteralPath $Path).Length
    }
}

function Get-DependencyRecords {
    param(
        [Parameter(Mandatory)]$Metadata,
        [Parameter(Mandatory)][string]$ExtenderDir,
        [string]$ApiSharedDir
    )
    if ([string]::IsNullOrWhiteSpace($ApiSharedDir)) {
        $ApiSharedDir = Join-Path $Metadata.Config.GameDir 'BepInEx\plugins\APIShared_Serp'
    }
    $paths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [void]$paths.Add((Join-Path $Metadata.Config.GameDir 'Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'))
    $projectFiles = @(Get-ChildItem -LiteralPath $Metadata.ModDir -Filter *.csproj -File -Recurse)
    foreach ($projectFile in $projectFiles) {
        [xml]$xml = Get-Content -LiteralPath $projectFile.FullName -Raw
        $hintNodes = @($xml.SelectNodes('//*[local-name()="HintPath"]'))
        foreach ($node in $hintNodes) {
            $candidate = [string]$node.InnerText
            $candidate = $candidate.Replace('$(GameDir)', $Metadata.Config.GameDir)
            $candidate = $candidate.Replace('$(ExtenderDir)', $ExtenderDir)
            $candidate = $candidate.Replace('$(ApiSharedDir)', $ApiSharedDir)
            $candidate = $candidate.Replace('$(MSBuildThisFileDirectory)', $projectFile.DirectoryName + '\')
            $candidate = $candidate.Replace('$(LocalScriptExtenderBuildOutput)', (Join-Path $Metadata.Config.Root 'shcde-script-extender\src\SHCDESE.BepInEx\bin\net481'))
            $candidate = $candidate.Replace('$(LocalScriptExtenderModOutput)', (Join-Path $Metadata.Config.Root 'shcde-script-extender\mod_output\000shcdese'))
            if (-not [IO.Path]::IsPathRooted($candidate)) {
                $candidate = Join-Path $projectFile.DirectoryName $candidate
            }
            if (Test-Path -LiteralPath $candidate -PathType Leaf) {
                [void]$paths.Add((Resolve-Path -LiteralPath $candidate).Path)
            }
        }
    }
    $records = @(foreach ($path in $paths) {
        $item = Get-Item -LiteralPath $path
        $displayPath = if ($item.FullName.StartsWith($Metadata.Config.GameDir + '\', [StringComparison]::OrdinalIgnoreCase)) {
            '$GameDir/' + $item.FullName.Substring($Metadata.Config.GameDir.Length + 1).Replace('\', '/')
        } elseif ($item.FullName.StartsWith($ExtenderDir + '\', [StringComparison]::OrdinalIgnoreCase)) {
            '$ExtenderDir/' + $item.FullName.Substring($ExtenderDir.Length + 1).Replace('\', '/')
        } elseif ($item.FullName.StartsWith($Metadata.Config.Root + '\', [StringComparison]::OrdinalIgnoreCase)) {
            '$Repository/' + $item.FullName.Substring($Metadata.Config.Root.Length + 1).Replace('\', '/')
        } else {
            '$External/' + $item.Name
        }
        [PSCustomObject]@{
            Name = $item.Name
            Path = $displayPath
            Sha256 = Get-Sha256Hex -Path $item.FullName
            Size = $item.Length
        }
    })
    return @($records | Sort-Object Path)
}

function Write-Utf8CrLfFile {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$Text)
    $normalized = ($Text -replace "`r?`n", "`r`n").TrimEnd("`r", "`n") + "`r`n"
    $encoding = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($Path, $normalized, $encoding)
    $actual = [IO.File]::ReadAllText($Path, $encoding)
    if (-not [string]::Equals($normalized, $actual, [StringComparison]::Ordinal)) {
        throw "CRLF write verification failed: $Path"
    }
}
