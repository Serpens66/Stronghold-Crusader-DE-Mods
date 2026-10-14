param(
    [string]$OutputDirectory,
    [string]$HeadCommit = 'origin/main'
)

. (Join-Path $PSScriptRoot 'Release.Common.ps1')
. (Join-Path $PSScriptRoot 'ReleaseStatus.Common.ps1')

$config = Get-ReleaseConfiguration
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $config.Root '.release-output\release-status'
}
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
if ($outputRoot.TrimEnd('\', '/') -eq $config.Root.TrimEnd('\', '/')) {
    throw 'The generated status output directory must not be the repository root.'
}
$badgeRoot = Join-Path $outputRoot 'badges'
$reportRoot = Join-Path $outputRoot 'reports'
$diffRoot = Join-Path $outputRoot 'diffs'
foreach ($generatedDirectory in @($badgeRoot, $reportRoot, $diffRoot)) {
    if (Test-Path -LiteralPath $generatedDirectory) { Remove-Item -LiteralPath $generatedDirectory -Recurse -Force }
}
foreach ($directory in @($outputRoot, $badgeRoot, $reportRoot, $diffRoot)) {
    if (-not (Test-Path -LiteralPath $directory)) { [void](New-Item -ItemType Directory -Path $directory -Force) }
}

$resolvedHead = ((Invoke-StatusGit -Config $config -Arguments @('rev-parse', "$HeadCommit^{commit}")).Output -join '').Trim()
$releaseListResult = Invoke-CheckedCommand -FilePath 'gh' -Arguments @(
    'release', 'list', '--repo', $config.Repository, '--limit', '1000',
    '--json', 'tagName,isDraft,publishedAt'
) -AllowFailure
$releases = @()
$releaseListError = $null
if ($releaseListResult.ExitCode -eq 0) {
    $releases = @((($releaseListResult.Output -join "`n") | ConvertFrom-Json) | Where-Object { -not $_.isDraft })
} else {
    $releaseListError = "GitHub release list failed with exit code $($releaseListResult.ExitCode)."
}

foreach ($project in $config.Projects) {
    $status = 'unknown'
    $version = $null
    $releaseUrl = $null
    $baseCommit = $null
    $comparison = $null
    $errorMessage = $releaseListError
    try {
        if ([string]::IsNullOrWhiteSpace($errorMessage)) {
            $release = @($releases | Where-Object { ([string]$_.tagName).StartsWith("$project/v", [StringComparison]::Ordinal) } | Sort-Object { [DateTimeOffset]$_.publishedAt } -Descending | Select-Object -First 1)
            if ($release.Count -eq 0) {
                $errorMessage = 'No published release exists for this mod.'
            } else {
                $tag = [string]$release[0].tagName
                $version = $tag.Substring($project.Length + 2)
                $releaseUrl = "https://github.com/$($config.Repository)/releases/tag/$tag"
                $tagCommitResult = Invoke-StatusGit -Config $config -Arguments @('rev-parse', "$tag^{commit}") -AllowFailure
                if ($tagCommitResult.ExitCode -ne 0) { throw "Release tag could not be resolved: $tag" }
                $baseCommit = ($tagCommitResult.Output -join '').Trim()
                $comparison = Get-ModStatusComparison -Config $config -Project $project -BaseCommit $baseCommit -HeadCommit $resolvedHead
                $status = if ($comparison.IsCurrent) { 'current' } else { 'code newer' }
            }
        }
    } catch {
        $status = 'unknown'
        $errorMessage = $_.Exception.Message
    }
    Write-StatusBadge -Path (Join-Path $badgeRoot "$project.json") -Status $status
    Write-StatusReport -Config $config -OutputRoot $outputRoot -Project $project -Status $status -Version $version -ReleaseUrl $releaseUrl -BaseCommit $baseCommit -HeadCommit $resolvedHead -Comparison $comparison -ErrorMessage $errorMessage
    Write-Host "$project`: $status"
}
