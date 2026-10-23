# Upload published bytes independently of the mutable source checkout used to run tools.
. (Join-Path $PSScriptRoot 'ReleaseAssetCache.ps1')
. (Join-Path $PSScriptRoot 'ApiSharedSdkPolicy.ps1')
function Invoke-ModpackReleaseGh([string[]]$Arguments){
    $result=Invoke-ApiRepositoryProcess 'gh' $Arguments
    if($result.ExitCode -ne 0){throw ('GitHub release lookup/download failed: '+$result.Diagnostic)}
    return $result.Output
}
function Get-LatestPublishedModpackRelease {
    $releases=@();$page=1
    do{
        $batch=ConvertFrom-Json -InputObject (Invoke-ModpackReleaseGh @('api',('repos/Serpens66/Stronghold-Crusader-DE-Mods/releases?per_page=100&page='+$page)))
        $batch=@($batch)
        $releases+=@($batch|Where-Object {-not $_.draft -and -not $_.prerelease -and $_.tag_name -cmatch '^SerpsMods/v\d+\.\d+\.\d+$'})
        $page++
    }while($batch.Count -eq 100)
    $latest=@($releases|Sort-Object {[version]$_.tag_name.Substring('SerpsMods/v'.Length)} -Descending|Select-Object -First 1)
    if($latest.Count -ne 1){throw 'No published stable SerpsMods release found.'}
    return $latest[0]
}
function Get-PublishedModpackCommit([string]$Workspace,[string]$Tag){
    if($Tag -cnotmatch '^SerpsMods/v\d+\.\d+\.\d+$'){throw 'Invalid public modpack release tag.'}
    # Fetch metadata only. Never switch/reset a checkout or move an existing release tag.
    Invoke-ApiRepositoryGit $Workspace @('fetch','--no-tags','origin','refs/heads/main:refs/remotes/origin/main')|Out-Null
    Invoke-ApiRepositoryGit $Workspace @('fetch','--no-tags','origin',('refs/tags/'+$Tag))|Out-Null
    $commit=((Invoke-ApiRepositoryGit $Workspace @('rev-parse','FETCH_HEAD^{commit}'))-join '').Trim()
    $ancestor=Invoke-ApiRepositoryProcess 'git' @('-C',$Workspace,'merge-base','--is-ancestor',$commit,'refs/remotes/origin/main')
    if($commit -cnotmatch '^[0-9a-f]{40}$' -or $ancestor.ExitCode -ne 0){throw 'Published release tag does not belong to main history.'}
    return $commit
}
function Get-PublishedModpackAssets($Release,[string]$Version){
    $names=@('SerpsMods.provenance.json',('SerpsMods-Workshop-v'+$Version+'.zip'),'SerpsMods.map','SerpsMods.map.sha256')
    return @(foreach($name in $names){
        $asset=@($Release.assets|Where-Object {$_.name -ceq $name})
        if($asset.Count -ne 1 -or $asset[0].state -cne 'uploaded' -or $asset[0].digest -cnotmatch '^sha256:[0-9a-f]{64}$' -or [long]$asset[0].size -le 0){throw "Missing or unverifiable published release asset: $name"}
        [pscustomobject]@{Name=$name;Sha256=([string]$asset[0].digest).Substring(7);Size=[long]$asset[0].size}
    })
}
function Assert-PublishedModpackProvenance($Provenance,$Release){
    if($Release.Branch -cne 'main' -or $Release.Tag -cne ('SerpsMods/v'+$Release.Version) -or
       $Release.Commit -cnotmatch '^[0-9a-f]{40}$' -or
       $Provenance.Pack -cne 'SerpsMods' -or $Provenance.PackGuid -cne 'SerpsMods_Serp' -or
       $Provenance.Version -cne $Release.Version -or $Provenance.Commit -cne $Release.Commit -or
       $Provenance.ApiSharedCommit -cnotmatch '^[0-9a-f]{40}$' -or
       $Provenance.ApiSharedMainCommit -cnotmatch '^[0-9a-f]{40}$'){
        throw 'Published modpack identity, main commit or APIShared provenance mismatch.'
    }
    $null=Get-ModpackApiSharedIdentity $Provenance
    $zip=@($Release.Assets|Where-Object {$_.Name -ceq ('SerpsMods-Workshop-v'+$Release.Version+'.zip')})
    $map=@($Release.Assets|Where-Object {$_.Name -ceq 'SerpsMods.map'})
    if($zip.Count -ne 1 -or $map.Count -ne 1 -or $Provenance.Workshop.File -cne $zip[0].Name -or
       $Provenance.Workshop.Sha256 -cne $zip[0].Sha256 -or $Provenance.Map.File -cne 'SerpsMods.map' -or
       $Provenance.Map.Sha256 -cne $map[0].Sha256 -or [long]$Provenance.Map.Size -ne $map[0].Size){throw 'Published ZIP/map hashes differ from release provenance.'}
}
function Assert-PublishedModpackPreparation($Config,$Proof){
    $release=$Proof.PublishedRelease
    if($Proof.Version -cnotmatch '^\d+\.\d+\.\d+$'){throw 'Invalid published version.'}
    if($Proof.Target -cne 'SerpsMods' -or $Proof.Commit -cne $release.Commit -or $Proof.Version -cne $release.Version -or
       $Proof.ReleaseTag -cne $release.Tag -or -not $Proof.MainSource -or
       $Proof.MainSource.Commit -cnotmatch '^[0-9a-f]{40}$') {throw 'Published preparation identity mismatch.'}
    $cache=Join-Path (Get-WorkshopStateRoot $Config $false) ('releases/v'+$Proof.Version)
    $required=@('SerpsMods.provenance.json',('SerpsMods-Workshop-v'+$Proof.Version+'.zip'),'SerpsMods.map','SerpsMods.map.sha256')
    if(@($release.Assets).Count -ne $required.Count){throw 'Incomplete published asset inventory.'}
    foreach($name in $required){
        $asset=@($release.Assets|Where-Object {$_.Name -ceq $name})
        $path=Join-Path $cache $name;Assert-ApiWorkshopUnlinkedPath $path
        if($asset.Count -ne 1 -or $asset[0].Sha256 -cnotmatch '^[0-9a-f]{64}$' -or
           (Get-WorkshopFileHash $path) -cne $asset[0].Sha256 -or (Get-Item -LiteralPath $path).Length -ne $asset[0].Size){throw 'Published cached asset changed.'}
    }
    $provenance=[IO.File]::ReadAllText((Join-Path $cache 'SerpsMods.provenance.json'))|ConvertFrom-Json
    Assert-PublishedModpackProvenance $provenance $release
    $identity=Get-ModpackApiSharedIdentity $provenance
    Assert-ModpackApiSharedIdentity $Proof.PublishedSdk $identity
    if((Get-WorkshopFileHash $Proof.ReleaseProvenance) -cne (Get-WorkshopFileHash (Join-Path $cache 'SerpsMods.provenance.json'))){throw 'Prepared provenance differs from published release.'}
}
function Prepare-LatestPublishedModpack($Config,[bool]$Test){
    $remote=Get-LatestPublishedModpackRelease
    $version=([string]$remote.tag_name).Substring('SerpsMods/v'.Length)
    $commit=Get-PublishedModpackCommit $Config.Root $remote.tag_name
    $release=[pscustomobject]@{Branch='main';Tag=[string]$remote.tag_name;Version=$version;Commit=$commit;Assets=@(Get-PublishedModpackAssets $remote $version)}
    Write-Host ('Latest published main release: '+$release.Tag+' / '+$commit)
    $cache=Join-Path (Get-WorkshopStateRoot $Config $false) ('releases/v'+$version)
    foreach($asset in $release.Assets){
        $local=Join-Path $Config.Root ('.release-output/SerpsMods/v'+$version+'/'+$asset.Name)
        $locals=@($local,(Join-Path $Config.Root ('.release-output/SerpsMods/v'+$version+'/Workshop/'+$asset.Name)))
        $null=Get-VerifiedReleaseAsset $cache $asset.Name {
            param($path) (Test-ReleaseAssetHash $path $asset.Sha256) -and (Get-Item -LiteralPath $path).Length -eq $asset.Size
        } {
            param($stage,$name)
            Invoke-ModpackReleaseGh @('release','download',$release.Tag,'--repo','Serpens66/Stronghold-Crusader-DE-Mods','--pattern',$name,'--dir',$stage)|Out-Null
        } $locals
    }
    $provenancePath=Join-Path $cache 'SerpsMods.provenance.json'
    $provenance=[IO.File]::ReadAllText($provenancePath)|ConvertFrom-Json
    Assert-PublishedModpackProvenance $provenance $release
    if(-not(Test-ReleaseChecksum (Join-Path $cache 'SerpsMods.map.sha256') $provenance.Map.Sha256)){throw 'Published map checksum mismatch.'}
    $proofPath=Join-Path (Get-WorkshopStateRoot $Config $Test) 'prepared/SerpsMods.json'
    if(Test-Path -LiteralPath $proofPath){
        try{
            $proof=Assert-WorkshopPreparation $Config $Config.SerpsMods $Test
            if($proof.PSObject.Properties['PublishedRelease'] -and
               (ConvertTo-Json -InputObject $proof.PublishedRelease -Depth 8 -Compress) -ceq (ConvertTo-Json -InputObject $release -Depth 8 -Compress)){
                Write-Host 'Published Workshop preparation is current and verified.'
                return $proof
            }
        }catch{Write-Host ('Refreshing Workshop preparation: '+$_.Exception.Message)}
    }
    $stage=Join-Path (Get-WorkshopStateRoot $Config $Test) ('staging/'+[guid]::NewGuid().ToString('N'))
    Assert-ApiWorkshopUnlinkedPath $stage;[void][IO.Directory]::CreateDirectory($stage)
    $zip=Join-Path $cache $provenance.Workshop.File
    Assert-WorkshopContentZip $zip
    [IO.Compression.ZipFile]::ExtractToDirectory($zip,$stage)
    if((Get-WorkshopFileHash (Join-Path $stage 'SerpsMods.map')) -cne $provenance.Map.Sha256){throw 'Workshop ZIP contains a different map.'}
    Assert-PreparedLooseFolder $stage $version
    Publish-WorkshopFolder $stage (Get-WorkshopFolder $Config $Config.SerpsMods $Test) $Config.Root
    Save-WorkshopReleaseReference $Config $Test $provenancePath
    if(Test-Path -LiteralPath $proofPath){Copy-Item -LiteralPath $proofPath -Destination ($proofPath+'.previous-'+[guid]::NewGuid().ToString('N'))}
    $proof=Write-WorkshopPreparation $Config $Config.SerpsMods $Test $version $commit $release.Tag '' $release
    Assert-PublishedModpackPreparation $Config $proof
    return $proof
}
