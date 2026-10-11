. (Join-Path $PSScriptRoot '../../../APIShared/tools/WorkshopPackage.ps1')
. (Join-Path $PSScriptRoot 'SteamWorkshopHistory.ps1')
. (Join-Path $PSScriptRoot '../../../APIShared/tools/WorkshopUploader/PublisherBuildProof.ps1')
function Resolve-WorkshopOptions([string[]]$Options, [string]$Selection) {
    $targets=@();$validate=$false;$test=$false;$noPause=$false
    foreach($option in $Options){switch($option.ToLowerInvariant()){
        '/modpack'{$targets+='SerpsMods'} '/apishared'{$targets+='APIShared'} '/both'{$targets+='Both'}
        '/validate'{$validate=$true} '/test'{$test=$true} '/nopause'{$noPause=$true}
        default{throw "Unknown option: $option"}
    }}
    if($targets.Count -gt 1){throw 'Specify exactly one target option.'}
    if($targets.Count -eq 0){
        if(-not $Selection){Write-Host "1. SerpsMods`r`n2. APIShared`r`n3. Beide`r`n0. Abbrechen";$Selection=Read-Host 'Auswahl'}
        $targets=@(switch($Selection){'1'{'SerpsMods'} '2'{'APIShared'} '3'{'Both'} '0'{'Cancel'} default{throw 'Invalid target selection.'}})
    }
    [pscustomobject]@{Target=$targets[0];Validate=$validate;Test=$test;NoPause=$noPause}
}
function Get-WorkshopStateRoot($Config,[bool]$Test){$path=Join-Path $Config.Root '.release-output/SteamWorkshop';if($Test){$path=Join-Path $path 'test'};return $path}
function Get-WorkshopFolder($Config,$Target,[bool]$Test){if($Test){return (Join-Path (Get-WorkshopStateRoot $Config $true) ('packages/'+$Target.Key))};return $Target.Folder}
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
function Assert-WorkshopPlatformProof($Config,[string]$PackageId,[string]$Fingerprint){
    $path=Join-Path $Config.Root 'APIShared/.local/workshop-platform-tests.json'
    if(-not(Test-Path -LiteralPath $path)){throw 'Production requires reviewed platform evidence. Use /test first.'}
    $proof=[IO.File]::ReadAllText($path)|ConvertFrom-Json
    $checks=@{APISharedSha256='APIShared/BepInEx/plugins/APIShared_Serp/APIShared.dll';UpdaterWindowsSha256='APIShared/Workshop/workshop-updater.ps1';UpdaterProtonSha256='APIShared/Workshop/workshop-updater.sh';PublisherSha256='APIShared/.local/WorkshopUploader/WorkshopUploader.exe'}
    if($proof.SchemaVersion -ne 2){throw 'Platform evidence schema 2 is required.'}
    foreach($field in $checks.Keys){if($proof.$field -cne (Get-WorkshopFileHash (Join-Path $Config.Root $checks[$field]))){throw "Platform evidence belongs to another build: $field"}}
    foreach($field in @('Windows','Proton','SteamUploads','ModpackMigration','ExternalManager','ExtenderAndFixes')){if($proof.$field -ne $true){throw "Missing platform approval: $field"}}
    if($PackageId -and @($proof.Packages|Where-Object {$_.PackageId -ceq $PackageId -and $_.Fingerprint -ceq $Fingerprint}).Count -ne 1){throw 'Platform approval does not cover this exact prepared package.'}
}
function Invoke-WorkshopTargets($Targets,[scriptblock]$Execute){foreach($target in $Targets){& $Execute $target}}
function Save-WorkshopReleaseReference($Config,[bool]$Test,[string]$Source){
    $path=Join-Path (Get-WorkshopStateRoot $Config $Test) 'references/SerpsMods.provenance.json'
    Assert-ApiWorkshopUnlinkedPath $Source;Assert-ApiWorkshopUnlinkedPath $path
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $path));Copy-Item -LiteralPath $Source -Destination $path -Force
}
function Write-WorkshopPreparation($Config,$Target,[bool]$Test,[string]$Version,[string]$Commit,[string]$ReleaseTag,[string]$ReleaseNotes=''){
    $proof=[ordered]@{SchemaVersion=1;Target=$Target.Key;PackageId=$Target.Guid;Version=$Version;Mode=$(if($Test){'Test'}else{'Production'});Commit=$Commit;ReleaseTag=$ReleaseTag;ReleaseNotes=$ReleaseNotes;Files=@(Get-WorkshopRecords (Get-WorkshopFolder $Config $Target $Test))}
    if($Target.Key -eq 'SerpsMods'){$reference=Join-Path (Get-WorkshopStateRoot $Config $Test) 'references/SerpsMods.provenance.json';$proof.ReleaseProvenance=$reference;$proof.ReleaseProvenanceSha256=Get-WorkshopFileHash $reference}
    Write-SteamWorkshopJson -Path (Join-Path (Get-WorkshopStateRoot $Config $Test) ('prepared/'+$Target.Key+'.json')) -Value $proof
    return $proof
}
function Assert-WorkshopPreparation($Config,$Target,[bool]$Test){
    $path=Join-Path (Get-WorkshopStateRoot $Config $Test) ('prepared/'+$Target.Key+'.json')
    if(-not(Test-Path -LiteralPath $path)){throw "Prepare $($Target.Key) first."}
    $proof=[IO.File]::ReadAllText($path)|ConvertFrom-Json
    if($proof.SchemaVersion -ne 1 -or $proof.Target -cne $Target.Key -or $proof.PackageId -cne $Target.Guid -or $proof.Mode -cne $(if($Test){'Test'}else{'Production'})){throw 'Preparation proof identity/mode mismatch.'}
    $actual=@(Get-WorkshopRecords (Get-WorkshopFolder $Config $Target $Test))
    if($actual.Count -ne @($proof.Files).Count){throw 'Prepared folder contents changed.'}
    $byPath=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
    foreach($file in $actual){$byPath.Add($file.Path,$file)}
    foreach($file in $proof.Files){if(-not $byPath.ContainsKey($file.Path) -or $byPath[$file.Path].Size -ne $file.Size -or $byPath[$file.Path].Sha256 -cne $file.Sha256){throw "Prepared file changed: $($file.Path)"}}
    if($Target.Key -eq 'SerpsMods' -and (Get-WorkshopFileHash $proof.ReleaseProvenance) -cne $proof.ReleaseProvenanceSha256){throw 'Prepared release provenance changed.'}
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
        $name=$entry.FullName.TrimEnd('/')
        if(-not $name -or $name.Contains('\') -or $name.StartsWith('/') -or $name.Contains(':') -or @($name.Split('/')|Where-Object {$_ -in @('','.', '..') -or $_ -match '[. ]$|[\x00-\x1f<>"|?*]|^(?i:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)'}).Count -or -not $seen.Add($name) -or ((($entry.ExternalAttributes -shr 16)-band 0xF000)-eq 0xA000)){throw 'Unsafe or duplicate Workshop ZIP entry.'}
        if($name -notin @('SerpsMods.map','apishared-workshop.json','preview.png','Payload','Payload/BepInEx','Payload/BepInEx/plugins','Payload/BepInEx/plugins/SerpsMods_Serp') -and -not $name.StartsWith('Payload/BepInEx/plugins/SerpsMods_Serp/',[StringComparison]::Ordinal)){throw "Unexpected Workshop ZIP entry: $name"}
        if($name -match '/(?i:config|LobbyModSettings|APIShared_Serp|000shcdese)(/|$)'){throw 'Protected/settings target in Workshop ZIP.'}
    }
    $files=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($entry in $zip.Entries){if(-not $entry.FullName.EndsWith('/')){[void]$files.Add($entry.FullName)}}
    foreach($entry in $zip.Entries){$parent=$entry.FullName.TrimEnd('/');while($parent.Contains('/')){$parent=$parent.Substring(0,$parent.LastIndexOf('/'));if($files.Contains($parent)){throw 'File/directory overlap in Workshop ZIP.'}}}
    }finally{$zip.Dispose()}
}
function Assert-PreparedWorkshopFolder([string]$Directory,[string]$Version,[string]$PackageId){
    $root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
    $publisher=Join-Path $root 'APIShared/.local/WorkshopUploader/WorkshopUploader.exe'
    if(-not(Test-Path -LiteralPath $publisher)){throw 'Build APIShared/tools/WorkshopUploader/build.bat before preparing Workshop packages.'}
    Assert-WorkshopPublisherBuild (Join-Path $root 'APIShared')
    $run=Join-Path $root ('.release-output/SteamWorkshop/validation/'+[guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($run)
    $request=Join-Path $run 'request.json';$result=Join-Path $run 'result.json'
    Write-SteamWorkshopJson $request ([ordered]@{SchemaVersion=1;Mode='Production';PackageId=$PackageId;Version=$Version;SourceDirectory=$Directory;StateRoot=$run;PreviewFile=(Join-Path $Directory 'preview.png');Title=$PackageId;Description='Package validation';ChangeNote='';Visibility='Private';WorkshopId='';PreservePresentation=$false;ForbiddenItemIds=@()})
    & $publisher --validate $request $result
    if($LASTEXITCODE -ne 0){throw "Complete package validation failed: $result"}
}
function Assert-PreparedLooseFolder([string]$Directory,[string]$Version){Assert-PreparedWorkshopFolder $Directory $Version 'SerpsMods_Serp'}
