param(
    [Parameter(Mandatory)][string]$ModName,
    [switch]$NoPrompt,
    [switch]$ValidateOnly
)

. (Join-Path $PSScriptRoot 'Release.Common.ps1')

$metadata = $null
$trackedChangesAfterBuild = @()
$draftCreated = $false
try {
    $metadata = Get-PluginMetadata -ModName $ModName
    $config = $metadata.Config
    Write-Host "Preparing $($metadata.Manifest.Name) v$($metadata.Version)" -ForegroundColor Cyan

    $setup = @(Get-SetupReport -ModName $ModName)
    $setup | Format-Table -AutoSize
    $failedSetup = @($setup | Where-Object { -not $_.Ok })
    if ($failedSetup.Count -gt 0) { throw 'Release setup check failed.' }
    if (-not (Test-Path -LiteralPath $metadata.BuildBat -PathType Leaf)) { throw "Missing build.bat: $($metadata.BuildBat)" }

    [void](Invoke-CheckedCommand -FilePath 'gh' -Arguments @('auth', 'status'))
    $status = Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', $config.Root, 'status', '--porcelain=v1', '--untracked-files=normal')
    if ($status.Output.Count -ne 0) { throw "Git working tree is not clean:`r`n$($status.Output -join "`r`n")" }
    if ($ValidateOnly) {
        Write-Host 'Validation-only mode stopped before fetch, build, tag, release, or upload.' -ForegroundColor Green
        exit 0
    }
    $branch = (Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', $config.Root, 'branch', '--show-current')).Output -join ''
    if ($branch.Trim() -ne $config.Branch) { throw "Releases must be created from branch '$($config.Branch)', current branch is '$($branch.Trim())'." }
    [void](Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', $config.Root, 'fetch', '--prune', 'origin', $config.Branch, '--tags'))
    $commit = ((Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', $config.Root, 'rev-parse', 'HEAD')).Output -join '').Trim()
    $remoteCommit = ((Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', $config.Root, 'rev-parse', "origin/$($config.Branch)")).Output -join '').Trim()
    if ($commit -ne $remoteCommit) { throw "HEAD ($commit) must exactly match origin/$($config.Branch) ($remoteCommit)." }

    $existingReleaseResult = Invoke-CheckedCommand -FilePath 'gh' -Arguments @('release', 'view', $metadata.Tag, '--repo', $config.Repository, '--json', 'isDraft,targetCommitish,url') -AllowFailure
    $existingDraft = $null
    if ($existingReleaseResult.ExitCode -eq 0) {
        $existing = ($existingReleaseResult.Output -join "`n") | ConvertFrom-Json
        if (-not $existing.isDraft) { throw "Release already exists: $($metadata.Tag)" }
        if ([string]$existing.targetCommitish -ne $commit) { throw "Existing draft targets another commit: $($existing.targetCommitish)" }
        $existingDraft = $existing
        Write-Host 'A matching draft release exists and will be resumed.' -ForegroundColor Yellow
    } else {
        $tagCheck = Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', $config.Root, 'rev-parse', '-q', '--verify', "refs/tags/$($metadata.Tag)") -AllowFailure
        if ($tagCheck.ExitCode -eq 0) { throw "Tag already exists without a resumable draft: $($metadata.Tag)" }
    }
    $previousReleaseVersion = Get-PreviousPublishedReleaseVersion -Metadata $metadata
    $releaseChangeLines = @(Get-ReleaseChangeLines -Metadata $metadata -PreviousVersion $previousReleaseVersion)
    if ([string]::IsNullOrWhiteSpace($previousReleaseVersion)) {
        Write-Host 'No previous release found; release notes will say "inital release".' -ForegroundColor Yellow
    } else {
        Write-Host "Release notes include changelogs after v$previousReleaseVersion through v$($metadata.Version)." -ForegroundColor Cyan
    }

    if (-not $NoPrompt) {
        $confirmation = Read-Host "Type RELEASE to build and publish $($metadata.Manifest.Name) v$($metadata.Version)"
        if ($confirmation -cne 'RELEASE') { throw 'Release cancelled.' }
    }

    $extenderDir = Get-ExtenderDirectory -Metadata $metadata
    $dependencyRecords = @(Get-DependencyRecords -Metadata $metadata -ExtenderDir $extenderDir)
    $buildStart = [DateTime]::UtcNow
    Write-Host 'Running the mod build once...' -ForegroundColor Cyan
    $argumentString = '/d /s /c ""{0}" /nopause"' -f $metadata.BuildBat
    $process = Start-Process -FilePath $env:ComSpec -ArgumentList $argumentString -WorkingDirectory $metadata.ModDir -Wait -PassThru -NoNewWindow
    if ($process.ExitCode -ne 0) { throw "build.bat failed with exit code $($process.ExitCode)." }
    if (-not (Test-Path -LiteralPath $metadata.PackageDir -PathType Container)) { throw "Plugin package was not produced: $($metadata.PackageDir)" }

    $trackedStatus = Invoke-CheckedCommand -FilePath 'git' -Arguments @('-C', $config.Root, 'status', '--porcelain=v1', '--untracked-files=no')
    $trackedChangesAfterBuild = @($trackedStatus.Output | ForEach-Object { if ([string]$_ -match '^.. (.+)$') { $Matches[1] } })
    $outputRoot = Join-Path $config.Root ".release-output\$ModName\v$($metadata.Version)"
    if (Test-Path -LiteralPath $outputRoot) { Remove-Item -LiteralPath $outputRoot -Recurse -Force }
    [void](New-Item -ItemType Directory -Path $outputRoot -Force)
    $stageRoot = Join-Path $outputRoot 'stage'
    $stagePackage = Join-Path $stageRoot $metadata.PackageFolderName
    [void](New-Item -ItemType Directory -Path $stagePackage -Force)
    $packageChildren = @(Get-ChildItem -LiteralPath $metadata.PackageDir -Force)
    foreach ($packageChild in $packageChildren) {
        Copy-Item -LiteralPath $packageChild.FullName -Destination $stagePackage -Recurse -Force
    }

    $packageFiles = @(Get-ChildItem -LiteralPath $stagePackage -File -Recurse | Sort-Object FullName)
    if ($packageFiles.Count -eq 0) { throw 'The staged plugin package is empty.' }
    $runtimeFiles = @($packageFiles | Where-Object { $_.Extension -in @('.msgpack', '.log', '.tmp') -or $_.FullName -match '\\LobbyModSettings\\' })
    if ($runtimeFiles.Count -gt 0) {
        throw "Package contains local runtime data:`r`n$(@($runtimeFiles.FullName) -join "`r`n")"
    }
    $fileRecords = @(foreach ($file in $packageFiles) { Get-FileHashRecord -Path $file.FullName -BasePath $stageRoot })
    $baseName = "$ModName-v$($metadata.Version)"
    $zipPath = Join-Path $outputRoot "$baseName.zip"
    Compress-Archive -LiteralPath $stagePackage -DestinationPath $zipPath -CompressionLevel Optimal
    $zipHash = Get-Sha256Hex -Path $zipPath

    $auditRoot = Join-Path $outputRoot 'audit'
    Expand-Archive -LiteralPath $zipPath -DestinationPath $auditRoot
    $auditFiles = @(Get-ChildItem -LiteralPath $auditRoot -File -Recurse | Sort-Object FullName)
    $auditRecords = @(foreach ($file in $auditFiles) { Get-FileHashRecord -Path $file.FullName -BasePath $auditRoot })
    if (($fileRecords | ConvertTo-Json -Depth 5 -Compress) -cne ($auditRecords | ConvertTo-Json -Depth 5 -Compress)) {
        throw 'ZIP audit failed: archive contents differ from the staged package.'
    }

    $gitVersion = ((Invoke-CheckedCommand -FilePath 'git' -Arguments @('--version')).Output -join ' ').Trim()
    $ghVersion = ((Invoke-CheckedCommand -FilePath 'gh' -Arguments @('--version')).Output | Select-Object -First 1).Trim()
    $dotnetVersion = ((Invoke-CheckedCommand -FilePath 'dotnet' -Arguments @('--version')).Output -join '').Trim()
    $msBuildVersion = (Get-Item -LiteralPath $config.MSBuild).VersionInfo.FileVersion
    $provenance = [ordered]@{
        SchemaVersion = 1
        Repository = "https://github.com/$($config.Repository)"
        Commit = $commit
        Branch = $config.Branch
        GitCleanBeforeBuild = $true
        Mod = $ModName
        PluginGuid = [string]$metadata.Manifest.GUID
        PluginName = [string]$metadata.Manifest.Name
        Version = $metadata.Version
        Tag = $metadata.Tag
        BuildStartedUtc = $buildStart.ToString('o')
        BuildCompletedUtc = [DateTime]::UtcNow.ToString('o')
        Package = [ordered]@{ File = [IO.Path]::GetFileName($zipPath); Sha256 = $zipHash; Size = (Get-Item -LiteralPath $zipPath).Length }
        Files = $fileRecords
        Dependencies = $dependencyRecords
        Tools = [ordered]@{ Git = $gitVersion; GitHubCli = $ghVersion; DotNet = $dotnetVersion; MSBuild = $msBuildVersion }
        TrustStatement = 'This provenance is a documented statement by the repository owner, not an independently executed build.'
    }
    $provenancePath = Join-Path $outputRoot "$baseName.provenance.json"
    Write-Utf8CrLfFile -Path $provenancePath -Text ($provenance | ConvertTo-Json -Depth 10)
    $shaPath = Join-Path $outputRoot "$baseName.zip.sha256"
    Write-Utf8CrLfFile -Path $shaPath -Text "$zipHash  $baseName.zip"

    if ($trackedChangesAfterBuild.Count -gt 0) {
        [void](Invoke-CheckedCommand -FilePath 'git' -Arguments (@('-C', $config.Root, 'restore', '--worktree', '--') + $trackedChangesAfterBuild))
        $trackedChangesAfterBuild = @()
    }

    $notesPath = Join-Path $outputRoot 'release-notes.md'
    $noteLines = @(
        "# $($metadata.Manifest.Name) v$($metadata.Version)",
        '',
        '## Changes',
        ''
    ) + $releaseChangeLines + @(
        '',
        '## Source and verification',
        '',
        "Source commit: https://github.com/$($config.Repository)/commit/$commit",
        "SHA-256: ``$zipHash``",
        '',
        'Verify on Windows:',
        '',
        "    Get-FileHash $baseName.zip -Algorithm SHA256",
        '',
        'The attached provenance JSON records the package files, build tools, and dependency hashes. It is a documented statement by the repository owner, not an independently executed build.'
    )
    Write-Utf8CrLfFile -Path $notesPath -Text ($noteLines -join "`r`n")

    if ($null -eq $existingDraft) {
        [void](Invoke-CheckedCommand -FilePath 'gh' -Arguments @('release', 'create', $metadata.Tag, '--repo', $config.Repository, '--draft', '--target', $commit, '--title', "$($metadata.Manifest.Name) v$($metadata.Version)", '--notes-file', $notesPath, $zipPath, $shaPath, $provenancePath))
        $draftCreated = $true
    } else {
        [void](Invoke-CheckedCommand -FilePath 'gh' -Arguments @('release', 'edit', $metadata.Tag, '--repo', $config.Repository, '--title', "$($metadata.Manifest.Name) v$($metadata.Version)", '--notes-file', $notesPath))
        [void](Invoke-CheckedCommand -FilePath 'gh' -Arguments @('release', 'upload', $metadata.Tag, '--repo', $config.Repository, '--clobber', $zipPath, $shaPath, $provenancePath))
    }
    [void](Invoke-CheckedCommand -FilePath 'gh' -Arguments @('release', 'edit', $metadata.Tag, '--repo', $config.Repository, '--draft=false'))
    $published = ((Invoke-CheckedCommand -FilePath 'gh' -Arguments @('release', 'view', $metadata.Tag, '--repo', $config.Repository, '--json', 'url')).Output -join "`n") | ConvertFrom-Json

    & (Join-Path $PSScriptRoot 'Update-ReleaseIndex.ps1') -CurrentTag $metadata.Tag -CurrentUrl ([string]$published.url) -CurrentVersion $metadata.Version -CurrentMod $ModName -CurrentCommit $commit -CurrentSha256 $zipHash -CommitAndPush
    Write-Host "Release published: $($published.url)" -ForegroundColor Green
} catch {
    Write-Error $_
    if ($draftCreated) {
        Write-Host 'A draft release may remain on GitHub. Re-running release.bat will resume it.' -ForegroundColor Yellow
    }
    exit 1
} finally {
    if ($metadata -and $trackedChangesAfterBuild.Count -gt 0) {
        try {
            [void](Invoke-CheckedCommand -FilePath 'git' -Arguments (@('-C', $metadata.Config.Root, 'restore', '--worktree', '--') + $trackedChangesAfterBuild))
        } catch {
            Write-Warning "Could not restore build-generated tracked files: $($_.Exception.Message)"
        }
    }
}
