param([int]$TestMode=0,[int]$ValidateOnly=0,[string]$Target='SerpsMods')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
$config=Get-WorkshopConfiguration;$test=$TestMode -eq 1
$apiId=Get-WorkshopItemId $config $config.APIShared $test
if(-not $apiId){throw 'Prepare/upload APIShared first.'}
$env:SERPS_STEAM_MODS=$config.Mods -join '|'
$arguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'Create-SteamModPack.ps1'),'-PackName','SerpsMods','-PackGuid','SerpsMods_Serp','-SteamAppId','3024040','-SteamStateName','SerpsMods','-WorkshopPackagerPath',$config.Packager,'-PreviewPath',$config.SerpsMods.Preview,'-ApiSharedPreviewPath',$config.APIShared.Preview,'-ApiSharedWorkshopItemId',$apiId,'-NoPause')
if($env:SHCDE_MODPACK_BRANCH){$arguments+=@('-ModpackBranch',$env:SHCDE_MODPACK_BRANCH)}
if($test){$arguments+='-TestMode'}
if($ValidateOnly){$arguments+='-Validate'}
& powershell.exe @arguments;if($LASTEXITCODE -ne 0){exit $LASTEXITCODE}
if(-not $ValidateOnly){
    $folder=Get-WorkshopFolder $config $config.SerpsMods $test
    $manifest=[IO.File]::ReadAllText((Join-Path $folder 'apishared-workshop.json'))|ConvertFrom-Json
    $reference=[IO.File]::ReadAllText((Join-Path (Get-WorkshopStateRoot $config $test) 'references/SerpsMods.provenance.json'))|ConvertFrom-Json
    $releaseCommit=[string]$reference.Commit
    # A verified release can be reused after tooling-only commits. Freeze the selected
    # checkout separately; the immutable release provenance retains its original commit.
    $commit=if($env:SHCDE_MODPACK_BRANCH){((Invoke-ApiRepositoryGit $config.Root @('rev-parse','HEAD')) -join '').Trim()}else{$releaseCommit}
    $null=Write-WorkshopPreparation $config $config.SerpsMods $test $manifest.Version $commit $(if($test){'SerpsMods/test/v'+$manifest.Version+'-'+$releaseCommit.Substring(0,12)}else{'SerpsMods/v'+$manifest.Version})
}
