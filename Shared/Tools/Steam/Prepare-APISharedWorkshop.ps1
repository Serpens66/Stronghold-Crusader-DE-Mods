param([int]$TestMode=0,[int]$ValidateOnly=0,[string]$Target='APIShared')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot '../ApiSharedRepository/MainSource.Common.ps1')
$config=Get-WorkshopConfiguration
if(-not $env:SHCDE_API_SHARED_MAIN_SELECTION){$env:SHCDE_API_SHARED_MAIN_SELECTION=Resolve-ApiMainSelection $config.Root}
if(-not $ValidateOnly){Ensure-ApiMainBuild $config.Root}
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
$config=Get-WorkshopConfiguration;$test=$TestMode -eq 1;$api=$config.APIShared
$apiRoot=Get-WorkshopApiRoot $config.Root
$info=[IO.File]::ReadAllText((Join-Path $apiRoot 'info.json'))|ConvertFrom-Json
if($info.GUID -cne $api.Guid){throw 'APIShared manifest identity mismatch.'}
$status=@(& git -C $apiRoot status --porcelain);if($LASTEXITCODE -ne 0){throw 'Cannot read APIShared checkout state.'};if($status.Count -and -not $ValidateOnly){throw 'Commit the reviewed APIShared sources before preparing its release.'}
. (Join-Path $apiRoot 'tools/BuildProof.ps1')
$null=Assert-ApiBuildProof $apiRoot
if($status.Count){throw 'Pinned main worktree changed.'}
$commit=(& git -C $apiRoot rev-parse HEAD).Trim();if($LASTEXITCODE -ne 0){throw 'Cannot read APIShared source commit.'}
$tag=if($test){'APIShared/test/v'+$info.Version+'-'+$commit.Substring(0,12)}else{'APIShared/v'+$info.Version}
# The Git-ignored local Extender checkout exists only in the coordinator workspace, not in release worktrees.
$packagerRoot=@($config.Root,(Get-ApiMainWorkspace $config.Root))|ForEach-Object{Join-Path $_ 'shcde-script-extender/src/SHCDESE.WorkshopPackager'}|Where-Object{Test-Path -LiteralPath $_ -PathType Container}|Select-Object -First 1
$packager=if($config.Packager -ne 'AUTO'){$config.Packager}elseif($packagerRoot){(Get-ChildItem -LiteralPath $packagerRoot -Recurse -File -Filter 'SHCDESE.WorkshopPackager.exe'|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1).FullName}
if(-not $packager -or -not(Test-Path -LiteralPath $packager)){throw 'Build the existing Extender Workshop packager before preparing packages.'}
if(-not(Test-Path -LiteralPath $api.Preview)){throw 'APIShared preview missing.'}
. (Join-Path $apiRoot 'tools/Release/GitHubRelease.ps1')
$release=Get-ApiGitHubRelease 'SHCDE-APIShared/APIShared' $tag
$exists=$null -ne $release
if($exists){
    if([string]$release.targetCommitish -cnotmatch '^[0-9a-f]{40}$'){throw 'Existing APIShared release has a different source or publication mode.'}
    if([bool]$release.isDraft -ne ($test)){throw 'Existing APIShared release has a different source or publication mode.'}
    if($release.targetCommitish -cne $commit){
        if($test){throw 'Existing APIShared release has a different source or publication mode.'}
        . (Join-Path $PSScriptRoot 'SteamPackPolicy.ps1')
        $ancestor=Invoke-ApiRepositoryProcess 'git' @('-C',$apiRoot,'merge-base','--is-ancestor',[string]$release.targetCommitish,$commit)
        $paths=@(Invoke-ApiRepositoryGit $apiRoot @('diff','--name-only',[string]$release.targetCommitish,$commit))
        if($ancestor.ExitCode -ne 0 -or @(Get-SteamApiReleaseRelevantChanges $paths).Count){throw 'Existing APIShared release has a different source or publication mode.'}
        Write-Host 'Reusing published APIShared: release-relevant main sources are unchanged.'
    }
}
# Validation never publishes, so a missing release stays a failure there.
if($ValidateOnly -and -not $exists){throw "APIShared release $tag is unavailable. Run the preparation without /validate to create it from pinned main $commit."}
if($ValidateOnly){Write-Host "Validated APIShared $($info.Version), tag $tag; no release or build started.";exit 0}
# The source is always the pinned public-main worktree (Assert-ApiMainSelection), so only
# main can be released; Release.ps1 additionally requires a pushed, clean commit.
if(-not $exists){
    $word=if($test){'TESTPAKET'}else{'VEROEFFENTLICHEN'}
    if((Read-Host "Type $word to build and create the reviewed APIShared GitHub release $tag from main $commit") -cne $word){throw 'Preparation cancelled.'}
    $arguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $apiRoot 'tools/Release/Release.ps1'),'-WorkshopPackagerPath',$packager,'-PreviewPath',$api.Preview)
    if($test){$arguments+='-TestRelease'}else{$arguments+='-Publish'}
    & powershell.exe @arguments;if($LASTEXITCODE -ne 0){throw 'APIShared release preparation failed.'}
    $release=Get-ApiGitHubRelease 'SHCDE-APIShared/APIShared' $tag
    if(-not $release -or [string]$release.targetCommitish -cne $commit -or [bool]$release.isDraft -ne $test){throw 'Created APIShared release has an unexpected source or publication mode.'}
    $exists=$true
}
$run=Join-Path (Get-WorkshopStateRoot $config $test) ('preparation/APIShared-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($run)
& gh release download $tag --repo 'SHCDE-APIShared/APIShared' --pattern 'APIShared*' --dir $run
if($LASTEXITCODE -ne 0){throw 'APIShared release assets could not be downloaded.'}
$provenance=[IO.File]::ReadAllText((Join-Path $run ('APIShared-v'+$info.Version+'.provenance.json')))|ConvertFrom-Json
$deliveredCommit=if($exists){[string]$release.targetCommitish}else{$commit}
if($provenance.Commit -cne $deliveredCommit -or $provenance.Version -cne $info.Version -or $provenance.PluginGuid -cne $api.Guid -or $provenance.Tag -cne $tag -or $provenance.Map.Sha256 -cne (Get-WorkshopFileHash (Join-Path $run 'APIShared.map'))){throw 'Downloaded APIShared release proof mismatch.'}
$content=Join-Path $run 'content';[void][IO.Directory]::CreateDirectory($content)
Copy-Item -LiteralPath (Join-Path $run 'APIShared.map') -Destination $content
Copy-Item -LiteralPath $api.Preview -Destination (Join-Path $content 'preview.png')
Assert-PreparedWorkshopFolder $content $info.Version $api.Guid
Publish-WorkshopFolder $content (Get-WorkshopFolder $config $api $test) $config.Root
# Like the modpack: every changelog version since the last confirmed Workshop upload.
. (Join-Path $PSScriptRoot 'WorkshopChangelog.ps1')
$uploadedState=Join-Path (Get-WorkshopStateRoot $config $test) ('items/'+$config.AppId+'-APIShared.changelog-state.json')
$uploadedVersion=if(Test-Path -LiteralPath $uploadedState){[string]([IO.File]::ReadAllText($uploadedState)|ConvertFrom-Json).PackVersion}else{''}
# A repeated upload of the same version lists only its own entry.
if($uploadedVersion -and [version]$uploadedVersion -ge [version]$info.Version){
    $uploadedVersion=[string](@($info.SerpChangelog|Where-Object {[version]$_.Version -lt [version]$info.Version}|Sort-Object {[version]$_.Version} -Descending|Select-Object -First 1).Version)
}
$releaseNotes='## APIShared v'+$info.Version+"`r`n`r`n"+(Get-SerpChangelogSince $info.SerpChangelog $info.Version $uploadedVersion 'APIShared')
$sdkIdentity=Get-ApiSharedPackageIdentity $provenance (Get-ApiMainDeliveryIdentity $config.Root).Commit $test
$null=Write-WorkshopPreparation $config $api $test $info.Version $deliveredCommit $tag $releaseNotes -SdkIdentity $sdkIdentity
Write-Host ('APIShared upload folder ready: '+(Get-WorkshopFolder $config $api $test))
