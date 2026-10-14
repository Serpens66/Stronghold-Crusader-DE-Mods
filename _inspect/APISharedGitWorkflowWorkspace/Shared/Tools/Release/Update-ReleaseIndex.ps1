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
foreach ($entry in @(Get-ReleaseIndexEntries -Config $config)) {
    $project = [string]$entry.Project
    $matchingReleases = @($releases | Where-Object { ([string]$_.tagName).StartsWith("$project/v", [StringComparison]::Ordinal) } | Sort-Object publishedAt -Descending)
    $version = $null
    $url = $null
    $commit = $null
    $sha256 = $null
    if ($project -eq $CurrentMod) {
        $version = $CurrentVersion
        $url = $CurrentUrl
        $commit = $CurrentCommit
        $sha256 = $CurrentSha256
    } else {
        foreach ($release in $matchingReleases) {
            $candidateVersion = ([string]$release.tagName).Substring($project.Length + 2)
            $view = Invoke-CheckedCommand -FilePath 'gh' -Arguments @('release', 'view', [string]$release.tagName, '--repo', $config.Repository, '--json', 'targetCommitish,body,url,assets')
            $details = ($view.Output -join "`n") | ConvertFrom-Json
            $assetName = Get-ReleaseIndexAssetName -Entry $entry -Version $candidateVersion
            if ([string]::IsNullOrWhiteSpace($assetName)) {
                $candidateUrl = [string]$details.url
            } else {
                $asset = @($details.assets | Where-Object { [string]$_.name -ceq $assetName })
                if ($asset.Count -ne 1) { continue }
                $candidateUrl = [string]$asset[0].url
            }
            $version = $candidateVersion
            $url = $candidateUrl
            $commit = [string]$details.targetCommitish
            $sha256 = Get-ReleaseIndexSha256 -Entry $entry -ReleaseBody ([string]$details.body)
            break
        }
    }
    if ($url) {
        $rows.Add((New-ReleaseIndexRow -Config $config -Entry $entry -Version $version -Url $url -Commit $commit -Sha256 $sha256))
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
    'Where shown, the code-status badge compares a release with the current relevant mod sources on `main`. Click it to open the mod-specific filtered diff report.',
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
