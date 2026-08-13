param(
    [string]$CurrentTag,
    [string]$CurrentUrl,
    [string]$CurrentVersion,
    [string]$CurrentMod,
    [string]$CurrentCommit,
    [string]$CurrentSha256,
    [switch]$CommitAndPush
)

. (Join-Path $PSScriptRoot 'Release.Common.ps1')

$config = Get-ReleaseConfiguration
$result = Invoke-CheckedCommand -FilePath 'gh' -Arguments @('release', 'list', '--repo', $config.Repository, '--limit', '1000', '--json', 'tagName,name,isDraft,publishedAt')
$releases = @((($result.Output -join "`n") | ConvertFrom-Json) | Where-Object { -not $_.isDraft })
$rows = [System.Collections.Generic.List[string]]::new()
foreach ($project in $config.Projects) {
    $release = @($releases | Where-Object { [string]$_.tagName -like "$project/v*" } | Sort-Object publishedAt -Descending | Select-Object -First 1)
    $version = $null
    $url = $null
    $commit = $null
    $sha256 = $null
    if ($project -eq $CurrentMod) {
        $version = $CurrentVersion
        $url = $CurrentUrl
        $commit = $CurrentCommit
        $sha256 = $CurrentSha256
    } elseif ($release.Count -eq 1) {
        $version = ([string]$release[0].tagName).Substring($project.Length + 2)
        $view = Invoke-CheckedCommand -FilePath 'gh' -Arguments @('release', 'view', [string]$release[0].tagName, '--repo', $config.Repository, '--json', 'targetCommitish,body,url')
        $details = ($view.Output -join "`n") | ConvertFrom-Json
        $url = [string]$details.url
        $commit = [string]$details.targetCommitish
        $hashMatch = [regex]::Match([string]$details.body, '(?im)^SHA-256:\s*`?([0-9a-f]{64})`?')
        $sha256 = if ($hashMatch.Success) { $hashMatch.Groups[1].Value.ToLowerInvariant() } else { 'see release' }
    }
    if ($url) {
        $shortCommit = if ($commit.Length -ge 7) { $commit.Substring(0, 7) } else { $commit }
        $commitUrl = "https://github.com/$($config.Repository)/commit/$commit"
        $badgeJsonUrl = "https://raw.githubusercontent.com/$($config.Repository)/release-status/badges/$project.json"
        $badgeUrl = "https://img.shields.io/endpoint?url=$([Uri]::EscapeDataString($badgeJsonUrl))&cacheSeconds=300"
        $reportUrl = "https://github.com/$($config.Repository)/blob/release-status/reports/$project.md"
        $statusBadge = "[![release status]($badgeUrl)]($reportUrl)"
        $rows.Add("| $project | [$version]($url) | $statusBadge | [$shortCommit]($commitUrl) | ``$sha256`` |")
    }
}

$readmePath = Join-Path $config.Root 'README.md'
$utf8 = [Text.UTF8Encoding]::new($false)
$readme = [IO.File]::ReadAllText($readmePath, $utf8)
$startMarker = '<!-- RELEASE-INDEX:START -->'
$endMarker = '<!-- RELEASE-INDEX:END -->'
$sectionLines = @(
    $startMarker,
    '## Latest Mod Releases',
    '',
    'These archives are produced by the repository release scripts from the linked public commit. The provenance file records the exact package, tool, and dependency hashes. This is a documented statement by the repository owner, not an independently executed build.',
    '',
    'The code-status badge compares each release with the current relevant mod sources on `main`. Click it to open the mod-specific filtered diff report.',
    '',
    '| Mod | Latest release | Code status | Source commit | ZIP SHA-256 |',
    '| --- | --- | --- | --- | --- |'
) + @($rows) + @(
    '',
    'Verify a downloaded archive with `Get-FileHash <archive.zip> -Algorithm SHA256` and compare it with the release asset and table above.',
    $endMarker
)
$section = $sectionLines -join "`r`n"
if ($readme.Contains($startMarker) -and $readme.Contains($endMarker)) {
    $pattern = [regex]::Escape($startMarker) + '.*?' + [regex]::Escape($endMarker)
    $updated = [regex]::Replace($readme, $pattern, $section, [Text.RegularExpressions.RegexOptions]::Singleline)
} else {
    $updated = $readme.TrimEnd("`r", "`n") + "`r`n`r`n" + $section + "`r`n"
}
Write-Utf8CrLfFile -Path $readmePath -Text $updated

if ($CommitAndPush) {
    $status = Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', $config.Root, 'status', '--short', '--', 'README.md')
    if ($status.Output.Count -gt 0) {
        [void](Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', $config.Root, 'add', '--', 'README.md'))
        [void](Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', $config.Root, 'commit', '-m', "Update release index for $CurrentMod v$CurrentVersion"))
        [void](Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', $config.Root, 'push', 'origin', $config.Branch))
    }
}
