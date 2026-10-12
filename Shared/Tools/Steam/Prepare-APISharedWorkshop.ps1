param([int]$TestMode=0,[int]$ValidateOnly=0,[string]$Target='APIShared')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
$config=Get-WorkshopConfiguration;$test=$TestMode -eq 1;$api=$config.APIShared
$apiRoot=Join-Path $config.Root 'APIShared'
$info=[IO.File]::ReadAllText((Join-Path $apiRoot 'info.json'))|ConvertFrom-Json
if($info.GUID -cne $api.Guid){throw 'APIShared manifest identity mismatch.'}
$status=@(& git -C $apiRoot status --porcelain);if($LASTEXITCODE -ne 0){throw 'Cannot read APIShared checkout state.'};if($status.Count -and -not $ValidateOnly){throw 'Commit the reviewed APIShared sources before preparing its release.'}
. (Join-Path $apiRoot 'tools/BuildProof.ps1')
$null=Assert-ApiBuildProof $apiRoot
if($status.Count){Write-Host 'Validation uses reviewed local inputs; preparation requires a clean committed checkout.'}
$commit=(& git -C $apiRoot rev-parse HEAD).Trim();if($LASTEXITCODE -ne 0){throw 'Cannot read APIShared source commit.'}
$tag=if($test){'APIShared/test/v'+$info.Version+'-'+$commit.Substring(0,12)}else{'APIShared/v'+$info.Version}
$packager=if($config.Packager -ne 'AUTO'){$config.Packager}else{(Get-ChildItem -LiteralPath (Join-Path $config.Root 'shcde-script-extender/src/SHCDESE.WorkshopPackager') -Recurse -File -Filter 'SHCDESE.WorkshopPackager.exe'|Sort-Object LastWriteTimeUtc -Descending|Select-Object -First 1).FullName}
if(-not $packager -or -not(Test-Path -LiteralPath $packager)){throw 'Build the existing Extender Workshop packager before preparing packages.'}
if(-not(Test-Path -LiteralPath $api.Preview)){throw 'APIShared preview missing.'}
. (Join-Path $apiRoot 'tools/Release/GitHubRelease.ps1')
$release=Get-ApiGitHubRelease 'SHCDE-APIShared/APIShared' $tag
$exists=$null -ne $release
if($exists){if($release.targetCommitish -cne $commit -or [bool]$release.isDraft -ne $test){throw 'Existing APIShared release has a different source or publication mode.'}}
if($ValidateOnly){Write-Host "Validated APIShared $($info.Version), tag $tag; no release or build started.";exit 0}
if(-not $test){Assert-WorkshopPlatformProof $config}
if(-not $exists){
    $word=if($test){'TESTPAKET'}else{'VEROEFFENTLICHEN'}
    if((Read-Host "Type $word to build and create the reviewed GitHub release") -cne $word){throw 'Preparation cancelled.'}
    $arguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $apiRoot 'tools/Release/Release.ps1'),'-WorkshopPackagerPath',$packager,'-PreviewPath',$api.Preview)
    if($test){$arguments+='-TestRelease'}else{$arguments+='-Publish'}
    & powershell.exe @arguments;if($LASTEXITCODE -ne 0){throw 'APIShared release preparation failed.'}
}
$run=Join-Path (Get-WorkshopStateRoot $config $test) ('preparation/APIShared-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($run)
& gh release download $tag --repo 'SHCDE-APIShared/APIShared' --pattern 'APIShared*' --dir $run
if($LASTEXITCODE -ne 0){throw 'APIShared release assets could not be downloaded.'}
$provenance=[IO.File]::ReadAllText((Join-Path $run ('APIShared-v'+$info.Version+'.provenance.json')))|ConvertFrom-Json
if($provenance.Commit -cne $commit -or $provenance.Version -cne $info.Version -or $provenance.PluginGuid -cne $api.Guid -or $provenance.Tag -cne $tag -or $provenance.Map.Sha256 -cne (Get-WorkshopFileHash (Join-Path $run 'APIShared.map'))){throw 'Downloaded APIShared release proof mismatch.'}
$content=Join-Path $run 'content';[void][IO.Directory]::CreateDirectory($content)
Copy-Item -LiteralPath (Join-Path $run 'APIShared.map') -Destination $content
Copy-Item -LiteralPath $api.Preview -Destination (Join-Path $content 'preview.png')
Assert-PreparedWorkshopFolder $content $info.Version $api.Guid
Publish-WorkshopFolder $content (Get-WorkshopFolder $config $api $test) $config.Root
$changes=@($info.SerpChangelog|Where-Object {$_.Version -ceq $info.Version})
$releaseNotes='APIShared v'+$info.Version+': '+(@($changes.Changes) -join '; ')
$null=Write-WorkshopPreparation $config $api $test $info.Version $commit $tag $releaseNotes
Write-Host ('APIShared upload folder ready: '+(Get-WorkshopFolder $config $api $test))
