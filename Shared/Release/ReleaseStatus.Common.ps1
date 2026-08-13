Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-StatusGit {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)][string[]]$Arguments,
        [switch]$AllowFailure
    )
    return Invoke-CheckedCommand -FilePath 'git' -Arguments (@('-C', $Config.Root) + $Arguments) -AllowFailure:$AllowFailure
}

function Get-GitText {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)][string]$Revision,
        [Parameter(Mandatory)][string]$Path,
        [switch]$AllowMissing
    )
    $result = Invoke-StatusGit -Config $Config -Arguments @('show', "${Revision}:$Path") -AllowFailure:$AllowMissing
    if ($result.ExitCode -ne 0) { return $null }
    return ($result.Output -join "`n")
}

function Get-ChangedRepositoryPaths {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)][string]$BaseCommit,
        [Parameter(Mandatory)][string]$HeadCommit
    )
    $result = Invoke-StatusGit -Config $Config -Arguments @(
        'diff', '--name-only', '--diff-filter=ACDMRTUXB', $BaseCommit, $HeadCommit, '--'
    )
    return @($result.Output | ForEach-Object { ([string]$_).Replace('\', '/') } | Where-Object { $_ })
}

function Test-RelevantProjectPath {
    param(
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$Path
    )
    $prefix = "$Project/"
    if (-not $Path.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) { return $false }
    $relative = $Path.Substring($prefix.Length)
    if ($relative -match '(^|/)(?:bin|obj|\.inspect|\.tools|Docs?|Reference|packaging)(/|$)') { return $false }
    if ($relative -match '(^|/)[^/]*\.Tests?(/|$)') { return $false }
    if ($relative -ieq 'release.bat') { return $false }
    if ($relative -match '(?i)(^|/)UpdateToNewDLL\.md$') { return $false }
    if ($relative -match '(?i)\.md$') {
        return ($Project -eq 'CustomCustomTrail' -and $relative -ieq 'README.md')
    }
    if ($relative -match '(?i)\.(?:log|tmp|msgpack)$') { return $false }
    if ($relative -match '(?i)(^|/)LobbyModSettings(/|$)') { return $false }
    return $true
}

function ConvertTo-RepositoryPath {
    param(
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$Include
    )
    $value = $Include.Replace('\', '/')
    if ($value -match '^\$\(' -or [IO.Path]::IsPathRooted($value)) { return $null }
    $parts = [System.Collections.Generic.List[string]]::new()
    foreach ($part in @($Project.Split('/') + $value.Split('/'))) {
        if ([string]::IsNullOrWhiteSpace($part) -or $part -eq '.') { continue }
        if ($part -eq '..') {
            if ($parts.Count -eq 0) { return $null }
            $parts.RemoveAt($parts.Count - 1)
        } else {
            $parts.Add($part)
        }
    }
    return ($parts -join '/')
}

function Get-ExternalProjectInputs {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$HeadCommit,
        [Parameter(Mandatory)][string[]]$TrackedHeadPaths
    )
    $projectPath = "$Project/$Project.csproj"
    $projectText = Get-GitText -Config $Config -Revision $HeadCommit -Path $projectPath -AllowMissing
    if ([string]::IsNullOrWhiteSpace($projectText)) { return @() }
    [xml]$xml = $projectText
    $paths = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $nodes = @($xml.SelectNodes('//*[local-name()="Compile" or local-name()="Content" or local-name()="None" or local-name()="EmbeddedResource" or local-name()="AdditionalFiles"][@Include]'))
    foreach ($node in $nodes) {
        $normalized = ConvertTo-RepositoryPath -Project $Project -Include ([string]$node.Include)
        if ([string]::IsNullOrWhiteSpace($normalized) -or $normalized.StartsWith("$Project/", [StringComparison]::OrdinalIgnoreCase)) { continue }
        if ($normalized.IndexOfAny([char[]]@('*', '?', '[')) -ge 0) {
            $pattern = [WildcardPattern]::new($normalized, [Management.Automation.WildcardOptions]::IgnoreCase)
            foreach ($candidate in $TrackedHeadPaths) {
                if ($pattern.IsMatch($candidate)) { [void]$paths.Add($candidate) }
            }
        } else {
            [void]$paths.Add($normalized)
        }
    }
    return @($paths | Sort-Object)
}

function Get-LocalizationConstantMap {
    param([AllowNull()][string]$Text)
    $map = @{}
    if ([string]::IsNullOrWhiteSpace($Text)) { return $map }
    foreach ($match in [regex]::Matches($Text, '(?m)^\s*public\s+const\s+string\s+([A-Za-z_]\w*)\s*=\s*"([A-Za-z][A-Za-z0-9_-]*(?:\.[A-Za-z0-9_.-]+)+)"\s*;')) {
        $map[$match.Groups[1].Value] = $match.Groups[2].Value
    }
    return $map
}

function Add-LocalizationKeysFromText {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][Collections.Generic.HashSet[string]]$Keys,
        [AllowNull()][string]$Text,
        [Parameter(Mandatory)][hashtable]$ConstantMap
    )
    if ([string]::IsNullOrWhiteSpace($Text)) { return }
    foreach ($match in [regex]::Matches($Text, '(?m)^\s*([A-Za-z][A-Za-z0-9_-]*(?:\.[A-Za-z0-9_.-]+)+)\s*=')) {
        [void]$Keys.Add($match.Groups[1].Value)
    }
    foreach ($match in [regex]::Matches($Text, '\bSerpLocalization\.([A-Za-z_]\w*)\b')) {
        $name = $match.Groups[1].Value
        if ($ConstantMap.ContainsKey($name)) { [void]$Keys.Add([string]$ConstantMap[$name]) }
    }
    foreach ($match in [regex]::Matches($Text, '"([A-Za-z][A-Za-z0-9_-]*(?:\.[A-Za-z0-9_.-]+)+)"')) {
        [void]$Keys.Add($match.Groups[1].Value)
    }
}

function Get-ProjectLocalizationKeys {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$BaseCommit,
        [Parameter(Mandatory)][string]$HeadCommit,
        [Parameter(Mandatory)][hashtable]$BaseConstantMap,
        [Parameter(Mandatory)][hashtable]$HeadConstantMap
    )
    $keys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($revisionRecord in @(
        [PSCustomObject]@{ Revision = $BaseCommit; Map = $BaseConstantMap },
        [PSCustomObject]@{ Revision = $HeadCommit; Map = $HeadConstantMap }
    )) {
        $grep = Invoke-StatusGit -Config $Config -Arguments @(
            'grep', '-I', '-h', '-E', 'SerpLocalization\.|^[[:space:]]*[A-Za-z][A-Za-z0-9_.-]+[[:space:]]*=',
            $revisionRecord.Revision, '--', $Project
        ) -AllowFailure
        if ($grep.ExitCode -le 1) {
            Add-LocalizationKeysFromText -Keys $keys -Text ($grep.Output -join "`n") -ConstantMap $revisionRecord.Map
        }
    }
    return ,$keys
}

function Get-KeysFromLocalizationLines {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Lines,
        [Parameter(Mandatory)][hashtable]$BaseConstantMap,
        [Parameter(Mandatory)][hashtable]$HeadConstantMap
    )
    $keys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in $Lines) {
        foreach ($match in [regex]::Matches($line, '"([A-Za-z][A-Za-z0-9_-]*(?:\.[A-Za-z0-9_.-]+)+)"')) {
            [void]$keys.Add($match.Groups[1].Value)
        }
        foreach ($match in [regex]::Matches($line, '\b([A-Za-z_]\w*)\b')) {
            $name = $match.Groups[1].Value
            if ($BaseConstantMap.ContainsKey($name)) { [void]$keys.Add([string]$BaseConstantMap[$name]) }
            if ($HeadConstantMap.ContainsKey($name)) { [void]$keys.Add([string]$HeadConstantMap[$name]) }
        }
    }
    return ,$keys
}

function Get-LocalizationHunkDecision {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$ChangedLines,
        [Parameter(Mandatory)][AllowEmptyCollection()][Collections.Generic.HashSet[string]]$ProjectKeys,
        [Parameter(Mandatory)][hashtable]$BaseConstantMap,
        [Parameter(Mandatory)][hashtable]$HeadConstantMap
    )
    $keys = Get-KeysFromLocalizationLines -Lines $ChangedLines -BaseConstantMap $BaseConstantMap -HeadConstantMap $HeadConstantMap
    $isGlobal = $keys.Count -eq 0
    $isRelevant = $isGlobal
    $relevantKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($key in $keys) {
        if ($ProjectKeys.Contains($key)) {
            $isRelevant = $true
            [void]$relevantKeys.Add($key)
        }
    }
    return [PSCustomObject]@{
        IsRelevant = $isRelevant
        IsGlobal = $isGlobal
        RelevantKeys = @($relevantKeys | Sort-Object)
    }
}

function Get-LocalizationDiffForProject {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$BaseCommit,
        [Parameter(Mandatory)][string]$HeadCommit
    )
    $path = 'Shared/SerpLocalization.cs'
    $diffResult = Invoke-StatusGit -Config $Config -Arguments @(
        'diff', '--no-ext-diff', '--no-color', '--unified=3', $BaseCommit, $HeadCommit, '--', $path
    )
    $diffLines = @($diffResult.Output | ForEach-Object { [string]$_ })
    if ($diffLines.Count -eq 0) {
        return [PSCustomObject]@{ Relevant = $false; Patch = ''; Keys = @(); HasGlobalChange = $false }
    }

    $baseText = Get-GitText -Config $Config -Revision $BaseCommit -Path $path -AllowMissing
    $headText = Get-GitText -Config $Config -Revision $HeadCommit -Path $path -AllowMissing
    $baseMap = Get-LocalizationConstantMap -Text $baseText
    $headMap = Get-LocalizationConstantMap -Text $headText
    $projectKeys = Get-ProjectLocalizationKeys -Config $Config -Project $Project -BaseCommit $BaseCommit -HeadCommit $HeadCommit -BaseConstantMap $baseMap -HeadConstantMap $headMap

    $headers = [System.Collections.Generic.List[string]]::new()
    $hunks = [System.Collections.Generic.List[object]]::new()
    $currentHunk = $null
    foreach ($line in $diffLines) {
        if ($line.StartsWith('@@')) {
            $currentHunk = [System.Collections.Generic.List[string]]::new()
            $currentHunk.Add($line)
            $hunks.Add($currentHunk)
        } elseif ($null -eq $currentHunk) {
            $headers.Add($line)
        } else {
            $currentHunk.Add($line)
        }
    }

    $selected = [System.Collections.Generic.List[object]]::new()
    $selectedKeys = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $hasGlobal = $false
    foreach ($hunk in $hunks) {
        $changedLines = @($hunk | Where-Object { ($_ -match '^\+(?!\+)' -or $_ -match '^-(?!--)') })
        $decision = Get-LocalizationHunkDecision -ChangedLines $changedLines -ProjectKeys $projectKeys -BaseConstantMap $baseMap -HeadConstantMap $headMap
        if ($decision.IsRelevant) {
            $selected.Add($hunk)
            if ($decision.IsGlobal) { $hasGlobal = $true }
            foreach ($key in $decision.RelevantKeys) { [void]$selectedKeys.Add($key) }
        }
    }
    if ($selected.Count -eq 0) {
        return [PSCustomObject]@{ Relevant = $false; Patch = ''; Keys = @(); HasGlobalChange = $false }
    }
    $patchLines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $headers) { $patchLines.Add($line) }
    foreach ($hunk in $selected) {
        foreach ($line in $hunk) { $patchLines.Add([string]$line) }
    }
    return [PSCustomObject]@{
        Relevant = $true
        Patch = ($patchLines -join "`n")
        Keys = @($selectedKeys | Sort-Object)
        HasGlobalChange = $hasGlobal
    }
}

function Get-FileDiffText {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)][string]$BaseCommit,
        [Parameter(Mandatory)][string]$HeadCommit,
        [Parameter(Mandatory)][string]$Path
    )
    $numStat = Invoke-StatusGit -Config $Config -Arguments @(
        'diff', '--numstat', $BaseCommit, $HeadCommit, '--', $Path
    )
    if (@($numStat.Output | Where-Object { [string]$_ -match '^-\s+-\s+' }).Count -gt 0) {
        return ''
    }
    $result = Invoke-StatusGit -Config $Config -Arguments @(
        'diff', '--no-ext-diff', '--no-color', '--unified=3', $BaseCommit, $HeadCommit, '--', $Path
    )
    return ($result.Output -join "`n")
}

function Get-ModStatusComparison {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$BaseCommit,
        [Parameter(Mandatory)][string]$HeadCommit
    )
    $allChanged = @(Get-ChangedRepositoryPaths -Config $Config -BaseCommit $BaseCommit -HeadCommit $HeadCommit)
    $trackedHead = @((Invoke-StatusGit -Config $Config -Arguments @('ls-tree', '-r', '--name-only', $HeadCommit)).Output | ForEach-Object { ([string]$_).Replace('\', '/') })
    $externalInputs = @(Get-ExternalProjectInputs -Config $Config -Project $Project -HeadCommit $HeadCommit -TrackedHeadPaths $trackedHead)
    $externalSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($path in $externalInputs) { [void]$externalSet.Add($path) }

    $relevantPaths = [System.Collections.Generic.List[string]]::new()
    foreach ($path in $allChanged) {
        if (Test-RelevantProjectPath -Project $Project -Path $path) {
            $relevantPaths.Add($path)
        } elseif ($path -ne 'Shared/SerpLocalization.cs' -and $externalSet.Contains($path)) {
            $relevantPaths.Add($path)
        }
    }

    $localization = [PSCustomObject]@{ Relevant = $false; Patch = ''; Keys = @(); HasGlobalChange = $false }
    if ($externalSet.Contains('Shared/SerpLocalization.cs') -and $allChanged -contains 'Shared/SerpLocalization.cs') {
        $localization = Get-LocalizationDiffForProject -Config $Config -Project $Project -BaseCommit $BaseCommit -HeadCommit $HeadCommit
        if ($localization.Relevant) { $relevantPaths.Add('Shared/SerpLocalization.cs') }
    }

    $uniquePaths = @($relevantPaths | Sort-Object -Unique)
    $patches = [System.Collections.Generic.List[string]]::new()
    foreach ($path in $uniquePaths) {
        $patch = if ($path -eq 'Shared/SerpLocalization.cs') {
            [string]$localization.Patch
        } else {
            Get-FileDiffText -Config $Config -BaseCommit $BaseCommit -HeadCommit $HeadCommit -Path $path
        }
        if (-not [string]::IsNullOrWhiteSpace($patch)) { $patches.Add($patch) }
    }
    return [PSCustomObject]@{
        IsCurrent = $uniquePaths.Count -eq 0
        Paths = $uniquePaths
        Patch = ($patches -join "`n`n")
        LocalizationKeys = @($localization.Keys)
        HasGlobalLocalizationChange = [bool]$localization.HasGlobalChange
    }
}

function Write-StatusBadge {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][ValidateSet('current', 'code newer', 'unknown')][string]$Status
    )
    $color = switch ($Status) {
        'current' { 'brightgreen' }
        'code newer' { 'orange' }
        default { 'lightgrey' }
    }
    $badge = [ordered]@{ schemaVersion = 1; label = 'release'; message = $Status; color = $color }
    Write-Utf8CrLfFile -Path $Path -Text ($badge | ConvertTo-Json -Compress)
}

function Write-StatusReport {
    param(
        [Parameter(Mandatory)]$Config,
        [Parameter(Mandatory)][string]$OutputRoot,
        [Parameter(Mandatory)][string]$Project,
        [Parameter(Mandatory)][string]$Status,
        [AllowNull()][string]$Version,
        [AllowNull()][string]$ReleaseUrl,
        [AllowNull()][string]$BaseCommit,
        [Parameter(Mandatory)][string]$HeadCommit,
        [AllowNull()]$Comparison,
        [AllowNull()][string]$ErrorMessage
    )
    $repositoryUrl = "https://github.com/$($Config.Repository)"
    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add("# $Project release status")
    $lines.Add('')
    $lines.Add("**Status:** $Status")
    $lines.Add('')
    if (-not [string]::IsNullOrWhiteSpace($Version)) { $lines.Add("- Release: [v$Version]($ReleaseUrl)") }
    if (-not [string]::IsNullOrWhiteSpace($BaseCommit)) { $lines.Add("- Release commit: [$($BaseCommit.Substring(0, [Math]::Min(7, $BaseCommit.Length)))]($repositoryUrl/commit/$BaseCommit)") }
    $lines.Add("- Current main commit: [$($HeadCommit.Substring(0, [Math]::Min(7, $HeadCommit.Length)))]($repositoryUrl/commit/$HeadCommit)")
    if (-not [string]::IsNullOrWhiteSpace($ErrorMessage)) {
        $lines.Add('')
        $lines.Add("Status generation failed: $ErrorMessage")
    } elseif ($null -ne $Comparison -and $Comparison.IsCurrent) {
        $lines.Add('')
        $lines.Add('No release-relevant changes were found.')
    } elseif ($null -ne $Comparison) {
        $lines.Add('')
        $lines.Add('## Relevant changed files')
        $lines.Add('')
        foreach ($path in $Comparison.Paths) { $lines.Add("- ``$path``") }
        if (@($Comparison.LocalizationKeys).Count -gt 0) {
            $lines.Add('')
            $lines.Add('Relevant localization keys: ' + (@($Comparison.LocalizationKeys | ForEach-Object { "``$_``" }) -join ', '))
        }
        if ($Comparison.HasGlobalLocalizationChange) {
            $lines.Add('')
            $lines.Add('The localization helper also contains a general logic change that affects every consumer.')
        }
        if (-not [string]::IsNullOrWhiteSpace([string]$Comparison.Patch)) {
            $patchLines = @(([string]$Comparison.Patch) -split "`r?`n")
            $maxReportLines = 2000
            $truncated = $patchLines.Count -gt $maxReportLines
            if ($truncated) { $patchLines = @($patchLines | Select-Object -First $maxReportLines) }
            $lines.Add('')
            $lines.Add('## Diff')
            $lines.Add('')
            $lines.Add('```diff')
            foreach ($patchLine in $patchLines) { $lines.Add($patchLine) }
            $lines.Add('```')
            if ($truncated) {
                $lines.Add('')
                $lines.Add("The embedded diff was limited to $maxReportLines lines. [Open the complete filtered patch](../diffs/$Project.diff).")
            }
            Write-Utf8CrLfFile -Path (Join-Path $OutputRoot "diffs\$Project.diff") -Text ([string]$Comparison.Patch)
        }
    }
    Write-Utf8CrLfFile -Path (Join-Path $OutputRoot "reports\$Project.md") -Text ($lines -join "`r`n")
}
