. (Join-Path $PSScriptRoot 'RepositoryProcess.Common.ps1')
function Assert-ApiMainUnlinkedPath([string]$Path) {
    for($part=[IO.Path]::GetFullPath($Path);$part;$part=[IO.Path]::GetDirectoryName($part)) {
        if((Test-Path -LiteralPath $part) -and ([IO.File]::GetAttributes($part)-band [IO.FileAttributes]::ReparsePoint)){throw "Linked release path: $part"}
    }
}
function Get-ApiMainSourceFingerprint([string]$Root) {
    $rows=[Collections.Generic.List[string]]::new()
    foreach($relative in @(Invoke-ApiRepositoryGit $Root @('-c','core.quotepath=false','ls-files'))) {
        $path=[IO.Path]::GetFullPath((Join-Path $Root $relative))
        if(-not $path.StartsWith($Root.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Source path escapes main worktree.'}
        Assert-ApiMainUnlinkedPath $path
        $rows.Add($relative+':'+(Get-ApiRepositoryHash $path))
    }
    $rows.Sort([StringComparer]::Ordinal);$sha=[Security.Cryptography.SHA256]::Create()
    try {return ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($rows -join "`n")))).Replace('-','').ToLowerInvariant()}
    finally {$sha.Dispose()}
}
function Get-ApiMainWorkspace([string]$Workspace) {
    if($env:SHCDE_MODPACK_COORDINATOR){
        $actual=((Invoke-ApiRepositoryGit $Workspace @('rev-parse','--path-format=absolute','--git-common-dir')) -join '').Trim()
        $expected=((Invoke-ApiRepositoryGit $env:SHCDE_MODPACK_COORDINATOR @('rev-parse','--path-format=absolute','--git-common-dir')) -join '').Trim()
        if($actual -ine $expected){throw 'SDK coordinator belongs to another modpack repository.'}
        return [IO.Path]::GetFullPath($env:SHCDE_MODPACK_COORDINATOR).TrimEnd('\','/')
    }
    return [IO.Path]::GetFullPath($Workspace).TrimEnd('\','/')
}
function Assert-PublishedApi092($Selection) {
    if($Selection.Commit -cne 'c7b210f007035ff8edaf4d359a21f7bfbcbe479d' -or
       ([IO.File]::ReadAllText((Join-Path $Selection.Root 'info.json'))|ConvertFrom-Json).Version -cne '0.9.2' -or
       (Get-ApiRepositoryHash (Join-Path $Selection.Root 'BepInEx/plugins/APIShared_Serp/APIShared.dll')) -cne '2797b06042b0634ccc97ee5d2175c96944f95edfa1924d764776d9a6bde14758'){
        throw 'Modpack requires the verified published APIShared 0.9.2 SDK (commit and DLL hash). No APIShared build is allowed.'
    }
}
function Assert-ApiMainSelection([string]$Workspace,[string]$Path) {
    $Workspace=Get-ApiMainWorkspace $Workspace
    Assert-ApiMainUnlinkedPath $Path
    $selection=[IO.File]::ReadAllText($Path)|ConvertFrom-Json
    if($selection.SchemaVersion -ne 1 -or $selection.Commit -cnotmatch '^[0-9a-f]{40}$' -or $selection.Branch -cne 'main'){throw 'Invalid main selection identity.'}
    $base=Join-Path $Workspace ('.release-output/APIShared/main/'+$selection.Commit)
    $expectedPath=[IO.Path]::GetFullPath((Join-Path $base 'selection.json'))
    $root=[IO.Path]::GetFullPath((Join-Path $base 'source'))
    if(-not [string]::Equals([IO.Path]::GetFullPath($Path),$expectedPath,[StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals([string]$selection.Root,$root,[StringComparison]::OrdinalIgnoreCase)){throw 'main selection is outside its commit-specific worktree.'}
    Assert-ApiRepositoryOrigin $root
    if(((Invoke-ApiRepositoryGit $root @('rev-parse','HEAD'))-join '') -cne $selection.Commit -or
        @(Invoke-ApiRepositoryGit $root @('status','--porcelain','--untracked-files=normal')).Count){throw 'main worktree changed; preserve it and inspect before rebuilding.'}
    $ancestor=Invoke-ApiRepositoryProcess 'git' @('-C',$root,'merge-base','--is-ancestor',$selection.Commit,'refs/remotes/origin/main')
    if($ancestor.ExitCode -ne 0){throw 'Selected source is not proven to belong to fetched public main history.'}
    if((Get-ApiMainSourceFingerprint $root) -cne $selection.SourceFingerprint){throw 'main source fingerprint changed.'}
    if($env:SHCDE_API_SHARED_PUBLISHED_092 -eq '1'){Assert-PublishedApi092 $selection}
    return $selection
}
function Resolve-ApiMainSelection([string]$Workspace) {
    $Workspace=Get-ApiMainWorkspace $Workspace;$api=Join-Path $Workspace 'APIShared'
    Assert-ApiRepositoryOrigin $api
    if($env:SHCDE_API_SHARED_PUBLISHED_092 -eq '1'){
        $path=Join-Path $Workspace '.release-output/APIShared/main/c7b210f007035ff8edaf4d359a21f7bfbcbe479d/selection.json'
        $null=Assert-ApiMainSelection $Workspace $path
        return $path
    }
    $remote=@(Invoke-ApiRepositoryGit $api @('ls-remote','--heads','origin','refs/heads/main'))
    if($remote.Count -ne 1 -or $remote[0] -cnotmatch '^([0-9a-f]{40})\s+refs/heads/main$'){throw 'Cannot resolve public main.'}
    $commit=$Matches[1];$base=Join-Path $Workspace ('.release-output/APIShared/main/'+$commit)
    $root=Join-Path $base 'source';$path=Join-Path $base 'selection.json'
    Assert-ApiMainUnlinkedPath $base
    if(Test-Path -LiteralPath $path){$null=Assert-ApiMainSelection $Workspace $path;return $path}
    Invoke-ApiRepositoryGit $api @('fetch','--no-tags','origin','+refs/heads/main:refs/remotes/origin/main')|Out-Null
    # Never adopt an unexpected pre-existing directory or reset existing work.
    if(Test-Path -LiteralPath $root){throw "Unregistered main worktree already exists: $root. Inspect it; no files were overwritten."}
    [void][IO.Directory]::CreateDirectory($base)
    Invoke-ApiRepositoryGit $api @('worktree','add','--detach',$root,$commit)|Out-Null
    $selection=[ordered]@{SchemaVersion=1;Branch='main';Commit=$commit;Root=[IO.Path]::GetFullPath($root);SourceFingerprint=(Get-ApiMainSourceFingerprint $root)}
    [IO.File]::WriteAllText($path,((ConvertTo-Json -InputObject $selection -Depth 5)-replace '\r?\n',"`r`n")+"`r`n",[Text.UTF8Encoding]::new($false))
    $null=Assert-ApiMainSelection $Workspace $path
    Write-Host "APIShared release source: main / $commit (personal checkout unchanged)."
    return $path
}
function Get-WorkshopApiRoot([string]$Workspace) {
    if($env:SHCDE_API_SHARED_MAIN_SELECTION){return (Assert-ApiMainSelection $Workspace $env:SHCDE_API_SHARED_MAIN_SELECTION).Root}
    # Ordinary local builds and explicit unit fixtures retain their development package.
    return (Join-Path $Workspace 'APIShared')
}
function Ensure-ApiMainBuild([string]$Workspace) {
    if(-not $env:SHCDE_API_SHARED_MAIN_SELECTION){throw 'Workshop builds require a pinned main selection.'}
    $selection=Assert-ApiMainSelection $Workspace $env:SHCDE_API_SHARED_MAIN_SELECTION
    . (Join-Path $selection.Root 'tools/BuildProof.ps1')
    if($env:SHCDE_API_SHARED_PUBLISHED_092 -eq '1'){
        $null=Assert-ApiBuildProof $selection.Root
        Assert-PublishedApi092 $selection
        return
    }
    try {$null=Assert-ApiBuildProof $selection.Root;return} catch {Write-Host 'Building the pinned main source; no game installation will be changed.'}
    # Recover machine paths as data only; never load development helper code.
    $localProof=Join-Path $Workspace 'APIShared/.local/build-proof.json'
    if(-not $env:SHCDE_GAME_DIR -and (Test-Path -LiteralPath $localProof)) {
        $paths=[IO.File]::ReadAllText($localProof)|ConvertFrom-Json
        $env:SHCDE_GAME_DIR=[string]$paths.GameDir
        if(-not $env:SHCDESE_EXTENDER_DIR){$env:SHCDESE_EXTENDER_DIR=[string]$paths.ExtenderDir}
        if(-not $env:SHCDE_MSBUILD){$env:SHCDE_MSBUILD=[string]$paths.MSBuild}
    }
    & (Join-Path $selection.Root 'build.bat') /nopause /noinstall
    if($LASTEXITCODE -ne 0){throw 'Pinned main build failed; consumers needing newer APIs must wait for a main release.'}
    $null=Assert-ApiBuildProof $selection.Root
    $null=Assert-ApiMainSelection $Workspace $env:SHCDE_API_SHARED_MAIN_SELECTION
}
function Get-ApiMainDeliveryIdentity([string]$Workspace) {
    $selection=Assert-ApiMainSelection $Workspace $env:SHCDE_API_SHARED_MAIN_SELECTION
    . (Join-Path $selection.Root 'tools/WorkshopUploader/PublisherBuildProof.ps1')
    Assert-WorkshopPublisherBuild $selection.Root
    $publisherProof=Join-Path $selection.Root '.local/WorkshopUploader/build-proof.json'
    $outputs=@(Get-ApiMainPublisherOutputs $selection.Root)
    return [ordered]@{Selection=$env:SHCDE_API_SHARED_MAIN_SELECTION;SelectionSha256=(Get-ApiRepositoryHash $env:SHCDE_API_SHARED_MAIN_SELECTION);Commit=$selection.Commit;SourceFingerprint=$selection.SourceFingerprint;PublisherProofSha256=(Get-ApiRepositoryHash $publisherProof);PublisherOutputs=$outputs}
}
function Get-ApiMainPublisherOutputs([string]$Root) {
    $directory=Join-Path $Root '.local/WorkshopUploader';Assert-ApiMainUnlinkedPath $directory
    $proof=[IO.File]::ReadAllText((Join-Path $directory 'build-proof.json'))|ConvertFrom-Json
    $known=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $outputs=@($proof.Outputs)
    foreach($output in $outputs){
        $name=[string]$output.Name
        if(-not $name -or [IO.Path]::GetFileName($name) -cne $name -or $name.Contains(':') -or $output.Sha256 -cnotmatch '^[0-9a-f]{64}$' -or -not $known.Add($name)){throw 'Unsafe or duplicate publisher output identity.'}
        $file=Join-Path $directory $name;Assert-ApiMainUnlinkedPath $file
        if((Get-ApiRepositoryHash $file) -cne $output.Sha256){throw 'Publisher output differs from prepared build.'}
    }
    if(-not $known.Contains('WorkshopUploader.exe')){throw 'Publisher proof must own WorkshopUploader.exe.'}
    foreach($file in Get-ChildItem -LiteralPath $directory -File | Where-Object {$_.Extension -in @('.exe','.dll','.config','.bat','.sh')}) {
        if(-not $known.Contains($file.Name)){throw 'Undeclared publisher runtime file.'}
    }
    return @($outputs|Sort-Object Name|ForEach-Object {[ordered]@{Name=[string]$_.Name;Sha256=[string]$_.Sha256}})
}
function Assert-ApiMainDeliveryIdentity([string]$Workspace,$Identity) {
    if(-not $Identity){throw 'Preparation predates main isolation; prepare the package again.'}
    $selection=Assert-ApiMainSelection $Workspace ([string]$Identity.Selection)
    if((Get-ApiRepositoryHash $Identity.Selection)-cne $Identity.SelectionSha256 -or $selection.Commit -cne $Identity.Commit -or
        $selection.SourceFingerprint -cne $Identity.SourceFingerprint -or
        (Get-ApiRepositoryHash (Join-Path $selection.Root '.local/WorkshopUploader/build-proof.json')) -cne $Identity.PublisherProofSha256){throw 'Prepared main/publisher identity changed; prepare again.'}
    . (Join-Path $selection.Root 'tools/WorkshopUploader/PublisherBuildProof.ps1')
    Assert-WorkshopPublisherBuild $selection.Root
    $outputs=@(Get-ApiMainPublisherOutputs $selection.Root)
    # PowerShell 5.1 and 7 sort punctuation differently; compare the named hashes, not JSON array order.
    $expected=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach($output in @($Identity.PublisherOutputs)){
        if($expected.ContainsKey([string]$output.Name)){throw 'Duplicate prepared publisher output.'}
        $expected.Add([string]$output.Name,[string]$output.Sha256)
    }
    if($expected.Count -ne $outputs.Count){throw 'Publisher output inventory changed since preparation.'}
    foreach($output in $outputs){
        if(-not $expected.ContainsKey([string]$output.Name) -or $expected[[string]$output.Name] -cne $output.Sha256){throw 'Publisher output inventory changed since preparation.'}
    }
    return $Identity.Selection
}
