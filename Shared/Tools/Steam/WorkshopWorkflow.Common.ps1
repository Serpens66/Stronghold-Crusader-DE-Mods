. (Join-Path $PSScriptRoot '../ApiSharedRepository/MainSource.Common.ps1')
$workshopApiToolsRoot=Get-WorkshopApiRoot ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')))
. (Join-Path $workshopApiToolsRoot 'tools/WorkshopPackage.ps1')
. (Join-Path $PSScriptRoot 'SteamWorkshopHistory.ps1')
. (Join-Path $workshopApiToolsRoot 'tools/WorkshopUploader/PublisherBuildProof.ps1')
. (Join-Path $PSScriptRoot 'WorkshopOptions.Common.ps1')
. (Join-Path $PSScriptRoot 'PublishedModpack.Common.ps1')
function Get-WorkshopStateRoot($Config,[bool]$Test){$stateWorkspace=Get-ApiMainWorkspace $Config.Root;$path=Join-Path $stateWorkspace '.release-output/SteamWorkshop';if($Test){$path=Join-Path $path 'test'};return $path}
function Get-WorkshopFolder($Config,$Target,[bool]$Test){if($Test){return (Join-Path $Config.Root ('.release-output/SteamWorkshop/test/packages/'+$Target.Key))};return $Target.Folder}
function Get-WorkshopItemId($Config,$Target,[bool]$Test){
    $state=Get-WorkshopStateRoot $Config $Test
    $path=Join-Path $state ('items/'+$Config.AppId+'-'+$Target.Key+'.item-id')
    $saved=if(Test-Path -LiteralPath $path){[IO.File]::ReadAllText($path).Trim()}else{''}
    $configured=if($Test){''}else{$Target.ItemId}
    $publishing=Join-Path $state ('publisher/Publishing/'+$Target.Guid+'.json')
    $retry=if(Test-Path -LiteralPath $publishing){[string]([IO.File]::ReadAllText($publishing)|ConvertFrom-Json).WorkshopId}else{''}
    $ids=@(@($saved,$configured,$retry)|Where-Object {$_}|Select-Object -Unique)
    if($ids.Count -gt 1){throw "Conflicting Workshop IDs for $($Target.Key)."}
    $id=if($ids.Count){[string]$ids[0]}elseif(-not $Test -and $Target.Key -eq 'SerpsMods'){'3788821961'}else{''}
    if($id -and $id -notmatch '^[1-9][0-9]*$'){throw 'Invalid Workshop ID.'}
    if(-not $Test -and $Target.Key -eq 'SerpsMods' -and $id -cne '3788821961'){throw 'SerpsMods must retain its existing production Workshop ID 3788821961.'}
    if($Test -and $id){foreach($production in @($Config.APIShared,$Config.SerpsMods)){if($id -eq (Get-WorkshopItemId $Config $production $false)){throw 'Production ID forbidden in tests.'}}}
    return $id
}
function Get-WorkshopRecords([string]$Directory){
    Assert-ApiWorkshopUnlinkedTree $Directory;$base=[IO.Path]::GetFullPath($Directory).TrimEnd('\')
    return @(Get-ChildItem -LiteralPath $base -File -Recurse|ForEach-Object {[ordered]@{Path=$_.FullName.Substring($base.Length+1).Replace('\','/');Size=$_.Length;Sha256=(Get-WorkshopFileHash $_.FullName)}}|Sort-Object {$_.Path})
}
function Invoke-WorkshopTargets($Targets,[scriptblock]$Execute){foreach($target in $Targets){& $Execute $target}}
function Save-WorkshopReleaseReference($Config,[bool]$Test,[string]$Source){
    $path=Join-Path (Get-WorkshopStateRoot $Config $Test) 'references/SerpsMods.provenance.json'
    Assert-ApiWorkshopUnlinkedPath $Source;Assert-ApiWorkshopUnlinkedPath $path
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $path));Copy-Item -LiteralPath $Source -Destination $path -Force
}
function Write-WorkshopPreparation($Config,$Target,[bool]$Test,[string]$Version,[string]$Commit,[string]$ReleaseTag,[string]$ReleaseNotes='',$PublishedRelease=$null,$SdkIdentity=$null){
    $proof=[ordered]@{SchemaVersion=1;Target=$Target.Key;PackageId=$Target.Guid;Version=$Version;Mode=$(if($Test){'Test'}else{'Production'});Commit=$Commit;ReleaseTag=$ReleaseTag;ReleaseNotes=$ReleaseNotes;Files=@(Get-WorkshopRecords (Get-WorkshopFolder $Config $Target $Test))}
    if($Target.Key -eq 'SerpsMods' -and $env:SHCDE_MODPACK_BRANCH -and -not $PublishedRelease){
        . (Join-Path $PSScriptRoot 'ModpackSource.Common.ps1')
        if($env:SHCDE_MODPACK_BRANCH -eq 'develop' -and -not $Test){throw 'Develop modpacks must be private tests.'}
        $source=Get-ModpackPreparationSource $Config.Root $env:SHCDE_MODPACK_BRANCH $Commit
        if($source.Commit -cne $Commit -or $source.Root -ine $Config.Root){throw 'Prepared package/source identity mismatch.'}
        $proof.ModpackSource=$source
    }
    if($Target.Key -eq 'SerpsMods'){
        $reference=Join-Path (Get-WorkshopStateRoot $Config $Test) 'references/SerpsMods.provenance.json'
        $provenance=[IO.File]::ReadAllText($reference)|ConvertFrom-Json
        $proof.PublishedSdk=Get-ModpackApiSharedIdentity $provenance ($Test -and -not $PublishedRelease)
    }
    if($Target.Key -eq 'APIShared' -and $SdkIdentity){$proof.PublishedSdk=$SdkIdentity}
    if($PublishedRelease){$proof.PublishedRelease=$PublishedRelease}
    if($env:SHCDE_API_SHARED_MAIN_SELECTION){$proof.MainSource=Get-ApiMainDeliveryIdentity $Config.Root}
    if($Target.Key -eq 'SerpsMods'){$reference=Join-Path (Get-WorkshopStateRoot $Config $Test) 'references/SerpsMods.provenance.json';$proof.ReleaseProvenance=$reference;$proof.ReleaseProvenanceSha256=Get-WorkshopFileHash $reference}
    Write-SteamWorkshopJson -Path (Join-Path (Get-WorkshopStateRoot $Config $Test) ('prepared/'+$Target.Key+'.json')) -Value $proof
    return $proof
}
function Assert-WorkshopPreparation($Config,$Target,[bool]$Test){
    $path=Join-Path (Get-WorkshopStateRoot $Config $Test) ('prepared/'+$Target.Key+'.json')
    if(-not(Test-Path -LiteralPath $path)){throw "Prepare $($Target.Key) first."}
    $proof=[IO.File]::ReadAllText($path)|ConvertFrom-Json
    if($proof.SchemaVersion -ne 1 -or $proof.Target -cne $Target.Key -or $proof.PackageId -cne $Target.Guid -or $proof.Mode -cne $(if($Test){'Test'}else{'Production'})){throw 'Preparation proof identity/mode mismatch.'}
    if($proof.PSObject.Properties['PublishedRelease']){Assert-PublishedModpackPreparation $Config $proof}
    if($proof.PSObject.Properties['ModpackSource']){
        . (Join-Path $PSScriptRoot 'ModpackSource.Common.ps1')
        $source=Get-PreparedModpackSource $Config.Root $Test $env:SHCDE_MODPACK_BRANCH
        if($source.Root -ine $Config.Root -or -not $proof.PSObject.Properties['PublishedSdk'] -or
           $proof.PublishedSdk.MainCommit -cne $proof.MainSource.Commit){throw 'Prepared modpack SDK/source identity changed.'}
    }
    $actual=@(Get-WorkshopRecords (Get-WorkshopFolder $Config $Target $Test))
    if($actual.Count -ne @($proof.Files).Count){throw 'Prepared folder contents changed.'}
    $byPath=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach($file in $actual){$byPath.Add($file.Path,$file)}
    foreach($file in $proof.Files){if(-not $byPath.ContainsKey($file.Path) -or $byPath[$file.Path].Size -ne $file.Size -or $byPath[$file.Path].Sha256 -cne $file.Sha256){throw "Prepared file changed: $($file.Path)"}}
    if($Target.Key -eq 'SerpsMods'){
        if((Get-WorkshopFileHash $proof.ReleaseProvenance) -cne $proof.ReleaseProvenanceSha256){throw 'Prepared release provenance changed.'}
        $identity=Get-ModpackApiSharedIdentity ([IO.File]::ReadAllText($proof.ReleaseProvenance)|ConvertFrom-Json) ($Test -and -not $proof.PSObject.Properties['PublishedRelease'])
        Assert-ModpackApiSharedIdentity $proof.PublishedSdk $identity
    }
    if($Target.Key -eq 'APIShared' -and $proof.PSObject.Properties['MainSource']){
        if(-not $proof.PSObject.Properties['PublishedSdk']){throw 'Prepare APIShared again to record its exact SDK artifact identity.'}
        $evidence=[pscustomobject]@{Version=$proof.Version;Commit=$proof.Commit;Tag=$proof.ReleaseTag;
            Package=[pscustomobject]@{Sha256=$proof.PublishedSdk.PackageSha256}}
        $identity=Get-ApiSharedPackageIdentity $evidence $proof.MainSource.Commit $Test
        Assert-ModpackApiSharedIdentity $proof.PublishedSdk $identity
    }
    if($proof.PSObject.Properties['MainSource']){$null=Assert-ApiMainDeliveryIdentity $Config.Root $proof.MainSource}
    return $proof
}
function Publish-WorkshopFolder([string]$Source,[string]$Destination,[string]$Workspace){
    $full=[IO.Path]::GetFullPath($Destination)
    if(-not $full.StartsWith([IO.Path]::GetFullPath($Workspace).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Output escapes workspace.'}
    Assert-ApiWorkshopUnlinkedPath $full;Assert-ApiWorkshopUnlinkedTree $Source
    $candidate=$full+'.ready-'+[guid]::NewGuid().ToString('N');[void][IO.Directory]::CreateDirectory($candidate)
    Get-ChildItem -LiteralPath $Source -Force|ForEach-Object {Copy-Item -LiteralPath $_.FullName -Destination $candidate -Recurse}
    $backup=$full+'.previous-'+[guid]::NewGuid().ToString('N')
    if(Test-Path -LiteralPath $full){Move-Item -LiteralPath $full -Destination $backup}
    try{Move-Item -LiteralPath $candidate -Destination $full}catch{if(Test-Path -LiteralPath $backup){Move-Item -LiteralPath $backup -Destination $full};throw}
}
function Assert-WorkshopContentZip([string]$ZipPath){
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead($ZipPath);$seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    try{foreach($entry in $zip.Entries){
        $name=$entry.FullName.Replace('\','/').TrimEnd('/')
        if(-not $name -or $name.Contains('\') -or $name.StartsWith('/') -or $name.Contains(':') -or @($name.Split('/')|Where-Object {$_ -in @('','.', '..') -or $_ -match '[. ]$|[\x00-\x1f<>"|?*]|^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)'}).Count -or -not $seen.Add($name) -or ((($entry.ExternalAttributes -shr 16)-band 0xF000)-eq 0xA000)){throw 'Unsafe or duplicate Workshop ZIP entry.'}
        if($name -notin @('SerpsMods.map','apishared-workshop.json','preview.png','Payload','Payload/BepInEx','Payload/BepInEx/plugins','Payload/BepInEx/plugins/SerpsMods_Serp') -and -not $name.StartsWith('Payload/BepInEx/plugins/SerpsMods_Serp/',[StringComparison]::Ordinal)){throw "Unexpected Workshop ZIP entry: $name"}
        if($name -match '/(?i:config|LobbyModSettings|APIShared_Serp|000shcdese)(/|$)'){throw 'Protected/settings target in Workshop ZIP.'}
    }
    $files=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in $zip.Entries){if(-not $entry.FullName.EndsWith('/')){[void]$files.Add($entry.FullName.Replace('\','/'))}}
    foreach($entry in $zip.Entries){$parent=$entry.FullName.Replace('\','/').TrimEnd('/');while($parent.Contains('/')){$parent=$parent.Substring(0,$parent.LastIndexOf('/'));if($files.Contains($parent)){throw 'File/directory overlap in Workshop ZIP.'}}}
    }finally{$zip.Dispose()}
}
function Assert-PreparedWorkshopFolder([string]$Directory,[string]$Version,[string]$PackageId){
    $root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
    $apiRoot=Get-WorkshopApiRoot $root
    $publisher=Join-Path $apiRoot '.local/WorkshopUploader/WorkshopUploader.exe'
    if(-not(Test-Path -LiteralPath $publisher)){throw 'Build APIShared/tools/WorkshopUploader/build.bat before preparing Workshop packages.'}
    Assert-WorkshopPublisherBuild $apiRoot
    $run=Join-Path $root ('.release-output/SteamWorkshop/validation/'+[guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($run)
    $request=Join-Path $run 'request.json';$result=Join-Path $run 'result.json'
    Write-SteamWorkshopJson $request ([ordered]@{SchemaVersion=1;Mode='Production';PackageId=$PackageId;Version=$Version;SourceDirectory=$Directory;StateRoot=$run;PreviewFile=(Join-Path $Directory 'preview.png');Title=$PackageId;Description='Package validation';ChangeNote='';Visibility='Private';WorkshopId='';PreservePresentation=$false;ForbiddenItemIds=@()})
    & $publisher --validate $request $result
    if($LASTEXITCODE -ne 0){throw "Complete package validation failed: $result"}
}
function Assert-PreparedLooseFolder([string]$Directory,[string]$Version){Assert-PreparedWorkshopFolder $Directory $Version 'SerpsMods_Serp'}
function Initialize-WorkshopBootstrapEnvironment([string]$Workspace) {
    $apiRoot=Get-WorkshopApiRoot $Workspace
    . (Join-Path $apiRoot 'tools/BuildProof.ps1')
    Initialize-ApiBuildEnvironment $apiRoot
    # The shipped standalone bootstrap compiler uses these installed references.
    # Validate them before any source edits, releases or expensive packaging.
    foreach ($relative in @('BepInEx/core/BepInEx.dll','BepInEx/plugins/000shcdese/SHCDESE.dll','Stronghold Crusader Definitive Edition_Data/Managed/UnityEngine.dll','Stronghold Crusader Definitive Edition_Data/Managed/UnityEngine.CoreModule.dll')) {
        $reference = Join-Path $env:SHCDE_GAME_DIR $relative
        if (-not (Test-Path -LiteralPath $reference -PathType Leaf)) { throw "Workshop bootstrap reference is missing: $reference" }
    }
}
