$ErrorActionPreference='Stop'
$env:SHCDE_API_SHARED_MAIN_SELECTION=$null
$env:SHCDE_MODPACK_COORDINATOR=$null
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
. (Join-Path $PSScriptRoot 'ModpackSource.Common.ps1')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$checks=0
function Check($Ok,$Message){if(-not $Ok){throw $Message};$script:checks++}
function Reject([scriptblock]$Action){$failed=$false;try{& $Action|Out-Null}catch{$failed=$true};Check $failed 'Expected rejection'}
function Put($Path,$Text){[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path));[IO.File]::WriteAllText($Path,$Text,[Text.UTF8Encoding]::new($false))}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
$fixture = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -Kind t -Name 'PublishedModpack'
$root=Join-Path $fixture 'repository';[void][IO.Directory]::CreateDirectory($root)
$init=Invoke-ApiRepositoryProcess 'git' @('init','--initial-branch=main',$root);Check ($init.ExitCode -eq 0) 'Init'
function Git([string[]]$Arguments){Invoke-ApiRepositoryGit $root $Arguments}
Put (Join-Path $root '.gitignore') ".release-output/`r`nSerpsMods/`r`nSerpsMods.previous-*/`r`n"
Put (Join-Path $root 'source.txt') 'released source'
Git @('add','-A')|Out-Null
Git @('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','-c','commit.gpgsign=false','commit','-m','release')|Out-Null
Git @('tag','SerpsMods/v1.0.28')|Out-Null
Git @('remote','add','origin',$root)|Out-Null
$commit=((Git @('rev-parse','HEAD'))-join '').Trim()
$config=[pscustomobject]@{Root=$root;SerpsMods=[pscustomobject]@{Key='SerpsMods';Guid='SerpsMods_Serp';Folder=(Join-Path $root 'SerpsMods')}}
$remote=Join-Path $fixture 'remote';[void][IO.Directory]::CreateDirectory($remote)
Put (Join-Path $remote 'SerpsMods.map') 'published map bytes'
$mapHash=Get-WorkshopFileHash (Join-Path $remote 'SerpsMods.map')
Put (Join-Path $remote 'SerpsMods.map.sha256') "$mapHash  SerpsMods.map"
$zipPath=Join-Path $remote 'SerpsMods-Workshop-v1.0.28.zip'
$zip=[IO.Compression.ZipFile]::Open($zipPath,[IO.Compression.ZipArchiveMode]::Create)
try{
    $contents=[ordered]@{'SerpsMods.map'='published map bytes';'preview.png'='preview';'apishared-workshop.json'='{}';'Payload\BepInEx\plugins\SerpsMods_Serp\info.json'='{"GUID":"SerpsMods_Serp","Version":"1.0.28"}'}
    foreach($entry in $contents.GetEnumerator()){
        $writer=[IO.StreamWriter]::new($zip.CreateEntry($entry.Key).Open(),[Text.UTF8Encoding]::new($false))
        try{$writer.Write($entry.Value)}finally{$writer.Dispose()}
    }
}finally{$zip.Dispose()}
$apiCommit='c7b210f007035ff8edaf4d359a21f7bfbcbe479d'
$provenance=[pscustomobject]@{Pack='SerpsMods';PackGuid='SerpsMods_Serp';Version='1.0.28';Commit=$commit;ApiSharedCommit=$apiCommit;ApiSharedMainCommit=$apiCommit;
    Map=@{File='SerpsMods.map';Sha256=$mapHash;Size=(Get-Item (Join-Path $remote 'SerpsMods.map')).Length};
    Workshop=@{File='SerpsMods-Workshop-v1.0.28.zip';Sha256=(Get-WorkshopFileHash $zipPath)}}
Write-SteamWorkshopJson (Join-Path $remote 'SerpsMods.provenance.json') $provenance
$assets=@(Get-ChildItem -LiteralPath $remote -File|ForEach-Object {[pscustomobject]@{name=$_.Name;size=$_.Length;state='uploaded';digest=('sha256:'+(Get-WorkshopFileHash $_.FullName))}})
$script:releases=@(
    [pscustomobject]@{tag_name='APIShared/v99.0.0';draft=$false;prerelease=$false;assets=@()},
    [pscustomobject]@{tag_name='SerpsMods/v99.0.0';draft=$true;prerelease=$false;assets=@()},
    [pscustomobject]@{tag_name='SerpsMods/v88.0.0';draft=$false;prerelease=$true;assets=@()},
    [pscustomobject]@{tag_name='SerpsMods/v1.0.9';draft=$false;prerelease=$false;assets=@()},
    [pscustomobject]@{tag_name='SerpsMods/v1.0.28';draft=$false;prerelease=$false;assets=$assets})
$downloads=0
$script:remoteDirectory=$remote
# Only network/publisher services are simulated; selection, Git fetches, ZIPs, cache and proofs are real.
function Invoke-ModpackReleaseGh([string[]]$Arguments){
    if($Arguments[0] -ceq 'api'){
        if($script:pagination -and $Arguments[1] -like '*page=1'){
            $page=@(1..100|ForEach-Object {[pscustomobject]@{tag_name='Other/v1.0.0';draft=$false;prerelease=$false;assets=@()}})
            return ConvertTo-Json -InputObject $page -Depth 8
        }
        return ConvertTo-Json -InputObject $script:releases -Depth 8
    }
    if($Arguments[0] -cne 'release' -or $Arguments[1] -cne 'download'){throw 'Unexpected publication/mutation'}
    $script:downloads++
    $name=$Arguments[[Array]::IndexOf($Arguments,'--pattern')+1]
    $stage=$Arguments[[Array]::IndexOf($Arguments,'--dir')+1]
    Copy-Item -LiteralPath (Join-Path $script:remoteDirectory $name) -Destination (Join-Path $stage $name)
}
function Assert-PreparedLooseFolder([string]$Directory,[string]$Version){
    $info=Get-Content -LiteralPath (Join-Path $Directory 'Payload/BepInEx/plugins/SerpsMods_Serp/info.json') -Raw|ConvertFrom-Json
    if($info.GUID -cne 'SerpsMods_Serp' -or $info.Version -cne $Version){throw 'Invalid payload identity'}
}
function Get-ApiMainDeliveryIdentity($Workspace){return [pscustomobject]@{Commit=$apiCommit;Selection='fixture-published-sdk'}}
function Assert-ApiMainDeliveryIdentity($Workspace,$Identity){if($Identity.Commit -cne $apiCommit){throw 'Changed SDK'};return 'fixture-published-sdk'}
$env:SHCDE_API_SHARED_MAIN_SELECTION='fixture-published-sdk'
$env:SHCDE_MODPACK_BRANCH='main'
$script:pagination=$false
Check ((Get-LatestPublishedModpackRelease).tag_name -ceq 'SerpsMods/v1.0.28') 'Wrong stable latest release'
$script:pagination=$true
Check ((Get-LatestPublishedModpackRelease).tag_name -ceq 'SerpsMods/v1.0.28') 'Latest release beyond the first API page was missed'
$script:pagination=$false
$proof=Prepare-LatestPublishedModpack $config $false
Check ($proof.Version -ceq '1.0.28' -and $proof.Commit -ceq $commit -and $proof.PublishedRelease.Branch -ceq 'main') 'Published source identity'
Check (-not $proof.PSObject.Properties['ModpackSource']) 'Published bytes must not bind to mutable build output'
$proofPath=Join-Path $root '.release-output/SteamWorkshop/prepared/SerpsMods.json'
$proofHash=Get-WorkshopFileHash $proofPath;$downloadCount=$downloads
$null=Prepare-LatestPublishedModpack $config $false
Check ($downloads -eq $downloadCount -and (Get-WorkshopFileHash $proofPath) -ceq $proofHash) 'Warm upload rewrote proof or downloaded bytes'
Put (Join-Path $root 'source.txt') 'later tooling fix on main'
Git @('add','source.txt')|Out-Null
Git @('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','-c','commit.gpgsign=false','commit','-m','later fix')|Out-Null
$null=Prepare-LatestPublishedModpack $config $false
Check ((Get-WorkshopFileHash $proofPath) -ceq $proofHash) 'Later main commit invalidated published preparation'
$null=Assert-WorkshopPreparation $config $config.SerpsMods $false;$checks++
Put (Join-Path $root 'SerpsMods/SerpsMods.map') 'tampered prepared map'
Reject {Assert-WorkshopPreparation $config $config.SerpsMods $false}
$null=Prepare-LatestPublishedModpack $config $false
Check ((Get-WorkshopFileHash (Join-Path $root 'SerpsMods/SerpsMods.map')) -ceq $mapHash) 'Damaged preparation not repaired'
Check (@(Get-ChildItem -LiteralPath (Split-Path $proofPath) -Filter '*.previous-*').Count -gt 0) 'Previous proof not retained'
$cache=Join-Path $root '.release-output/SteamWorkshop/releases/v1.0.28'
Put (Join-Path $cache 'SerpsMods-Workshop-v1.0.28.zip') 'damaged cached archive'
$null=Prepare-LatestPublishedModpack $config $false
Check ($downloads -eq $downloadCount+1) 'Corrupt cache not repaired with only one download'
$private=Prepare-LatestPublishedModpack $config $true
Check ($private.Mode -ceq 'Test' -and $private.PublishedRelease.Branch -ceq 'main') 'Private upload did not use public main release'
$missing=[pscustomobject]@{assets=@($assets|Where-Object {$_.name -cne 'SerpsMods.map'})}
Reject {Get-PublishedModpackAssets $missing '1.0.28'}
$badProof=$provenance|ConvertTo-Json -Depth 8|ConvertFrom-Json;$badProof.Commit='a'*40
Reject {Assert-PublishedModpackProvenance $badProof $proof.PublishedRelease}
$badProof.Commit=$commit;$badProof.ApiSharedCommit='b'*40
Reject {Assert-PublishedModpackProvenance $badProof $proof.PublishedRelease}
# Invalid remote bytes must not replace a prepared public upload.
$remoteZipBytes=[IO.File]::ReadAllBytes($zipPath)
$preparedHash=Get-WorkshopFileHash $proofPath
Put $zipPath 'invalid download'
Put (Join-Path $cache 'SerpsMods-Workshop-v1.0.28.zip') 'invalid cache'
Reject {Prepare-LatestPublishedModpack $config $false}
Check ((Get-WorkshopFileHash $proofPath) -ceq $preparedHash) 'Rejected download changed prepared proof'
[IO.File]::WriteAllBytes($zipPath,$remoteZipBytes)
$null=Prepare-LatestPublishedModpack $config $false
# A newly published main release replaces the previously prepared version automatically.
$newRemote=Join-Path $fixture 'remote-1.0.29';[void][IO.Directory]::CreateDirectory($newRemote)
foreach($file in Get-ChildItem -LiteralPath $remote -File){Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $newRemote $file.Name)}
$newZip=Join-Path $newRemote 'SerpsMods-Workshop-v1.0.29.zip'
Move-Item -LiteralPath (Join-Path $newRemote 'SerpsMods-Workshop-v1.0.28.zip') -Destination $newZip
$archive=[IO.Compression.ZipFile]::Open($newZip,[IO.Compression.ZipArchiveMode]::Update)
try{
    $archive.GetEntry('Payload\BepInEx\plugins\SerpsMods_Serp\info.json').Delete()
    $writer=[IO.StreamWriter]::new($archive.CreateEntry('Payload\BepInEx\plugins\SerpsMods_Serp\info.json').Open(),[Text.UTF8Encoding]::new($false))
    try{$writer.Write('{"GUID":"SerpsMods_Serp","Version":"1.0.29"}')}finally{$writer.Dispose()}
}finally{$archive.Dispose()}
Git @('tag','SerpsMods/v1.0.29')|Out-Null
$newCommit=((Git @('rev-parse','HEAD'))-join '').Trim()
$provenance.Version='1.0.29';$provenance.Commit=$newCommit
$provenance.Workshop.File='SerpsMods-Workshop-v1.0.29.zip';$provenance.Workshop.Sha256=Get-WorkshopFileHash $newZip
Write-SteamWorkshopJson (Join-Path $newRemote 'SerpsMods.provenance.json') $provenance
$newAssets=@(Get-ChildItem -LiteralPath $newRemote -File|ForEach-Object {[pscustomobject]@{name=$_.Name;size=$_.Length;state='uploaded';digest=('sha256:'+(Get-WorkshopFileHash $_.FullName))}})
$script:releases+=[pscustomobject]@{tag_name='SerpsMods/v1.0.29';draft=$false;prerelease=$false;assets=$newAssets}
$script:remoteDirectory=$newRemote
$newProof=Prepare-LatestPublishedModpack $config $false
Check ($newProof.Version -ceq '1.0.29' -and $newProof.Commit -ceq $newCommit) 'Newer main release was not selected'
Check ((Get-Content (Join-Path $root 'SerpsMods/Payload/BepInEx/plugins/SerpsMods_Serp/info.json') -Raw|ConvertFrom-Json).Version -ceq '1.0.29') 'New release payload not restored'
$script:releases=@();Reject {Get-LatestPublishedModpackRelease}
Git @('checkout','--orphan','foreign')|Out-Null
Git @('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','-c','commit.gpgsign=false','commit','-m','foreign')|Out-Null
Git @('tag','SerpsMods/v9.0.0')|Out-Null
Git @('checkout','main')|Out-Null
Reject {Get-PublishedModpackCommit $root 'SerpsMods/v9.0.0'}
Write-Host "PASS: $checks published-release/latest/main-history/cache/repair/private/provenance checks. Fixtures: $fixture"
