. (Join-Path $PSScriptRoot '../ApiSharedRepository/MainSource.Common.ps1')
function Get-ModpackCoordinator([string]$Workspace) {
    $common=((Invoke-ApiRepositoryGit $Workspace @('rev-parse','--path-format=absolute','--git-common-dir')) -join '').Trim()
    if([IO.Path]::GetFileName($common) -cne '.git'){throw 'A normal shared modpack Git repository is required.'}
    return [IO.Path]::GetDirectoryName($common)
}
function Get-ModpackBranchChoice([string]$Branch,[string]$Selection) {
    if($Branch){if($Branch -notin @('main','develop')){throw 'Unsupported modpack branch.'};return $Branch}
    if(-not $Selection){
        Write-Host "Modpack-Branch (APIShared aus geprueftem main-Release):`r`n1. main - oeffentlicher Release`r`n2. develop - privates Testpaket`r`n0. Abbrechen"
        $Selection=Read-Host 'Modpack-Branch'
    }
    switch($Selection){'1'{return 'main'} '2'{return 'develop'} '0'{return 'Cancel'} default{throw 'Invalid modpack branch selection.'}}
}
function Assert-ModpackSource([string]$Workspace,$Source,[switch]$Clean) {
    if(-not $Source -or $Source.Branch -cnotin @('main','develop') -or $Source.Commit -cnotmatch '^[0-9a-f]{40}$'){throw 'Invalid modpack source identity.'}
    $root=[IO.Path]::GetFullPath([string]$Source.Root).TrimEnd('\','/')
    Assert-ApiMainUnlinkedPath $root
    if((Get-ModpackCoordinator $Workspace) -ine (Get-ModpackCoordinator $root)){throw 'Modpack source belongs to a different repository.'}
    $top=((Invoke-ApiRepositoryGit $root @('rev-parse','--show-toplevel')) -join '').Trim()
    if([IO.Path]::GetFullPath($top).TrimEnd('\','/') -ine $root){throw 'Modpack source is not a worktree root.'}
    $branch=((Invoke-ApiRepositoryGit $root @('branch','--show-current')) -join '').Trim()
    $head=((Invoke-ApiRepositoryGit $root @('rev-parse','HEAD')) -join '').Trim()
    if($branch -cne $Source.Branch -or $head -cne $Source.Commit){throw 'Selected modpack worktree changed branch or commit; prepare again.'}
    if($Clean -and @(Invoke-ApiRepositoryGit $root @('status','--porcelain','--untracked-files=normal')).Count){throw 'Selected modpack source must be clean; commit or preserve its changes first.'}
    return $root
}
# A merge on GitHub must reach the selected local worktree, because publishing requires
# HEAD == origin/<branch>. Only a clean fast-forward is applied; divergence is never reset.
function Sync-ModpackBranchFromOrigin([string]$Root,[string]$Branch) {
    if((Invoke-ApiRepositoryProcess 'git' @('-C',$Root,'remote','get-url','origin')).ExitCode -ne 0){return}
    Invoke-ApiRepositoryGit $Root @('fetch','--no-tags','origin',("+refs/heads/{0}:refs/remotes/origin/{0}" -f $Branch))|Out-Null
    $local=((Invoke-ApiRepositoryGit $Root @('rev-parse','HEAD')) -join '').Trim()
    $upstream=((Invoke-ApiRepositoryGit $Root @('rev-parse',"refs/remotes/origin/$Branch")) -join '').Trim()
    if($local -ceq $upstream){return}
    if((Invoke-ApiRepositoryProcess 'git' @('-C',$Root,'merge-base','--is-ancestor',$local,$upstream)).ExitCode -ne 0){
        # Local commits ahead of origin are left for the publishing check, which requires a push.
        if((Invoke-ApiRepositoryProcess 'git' @('-C',$Root,'merge-base','--is-ancestor',$upstream,$local)).ExitCode -eq 0){return}
        throw "Local $Branch diverged from origin/$Branch; resolve it manually in $Root."
    }
    if(@(Invoke-ApiRepositoryGit $Root @('status','--porcelain','--untracked-files=normal')).Count){
        Write-Host "WARNING: local $Branch is behind origin/$Branch, but $Root has changes; it was not updated."
        return
    }
    Invoke-ApiRepositoryGit $Root @('merge','--ff-only',$upstream)|Out-Null
    Write-Host "Fast-forwarded local $Branch to origin/$Branch ($upstream)."
}
function Resolve-ModpackSource([string]$Workspace,[string]$Branch,[switch]$AllowGeneratedOutputs) {
    if($Branch -cnotin @('main','develop')){throw 'Unsupported modpack branch.'}
    $root=$null;$candidate=$null
    foreach($line in @(Invoke-ApiRepositoryGit $Workspace @('-c','core.quotepath=false','worktree','list','--porcelain'))){
        if($line.StartsWith('worktree ')){$candidate=$line.Substring(9)}
        if($line -ceq ('branch refs/heads/'+$Branch)){$root=$candidate}
    }
    if(-not $root){
        $root=New-ShortWorkDirectory -Root (Get-ModpackCoordinator $Workspace) -Kind b -Name ('modpack-'+$Branch) -Identity (((Invoke-ApiRepositoryGit $Workspace @('rev-parse',$Branch))-join '').Trim())
        Assert-ApiMainUnlinkedPath $root
        if(@(Get-ChildItem -LiteralPath $root -Force).Count){throw 'Allocated modpack worktree is not empty.'}
        Invoke-ApiRepositoryGit $Workspace @('worktree','add',$root,$Branch)|Out-Null
    }
    # Only a new preparation follows origin; uploads keep the worktree of their proof.
    if(-not $AllowGeneratedOutputs){Sync-ModpackBranchFromOrigin $root $Branch}
    $source=[ordered]@{Branch=$Branch;Root=[IO.Path]::GetFullPath($root);Commit=((Invoke-ApiRepositoryGit $root @('rev-parse','HEAD')) -join '').Trim()}
    if($AllowGeneratedOutputs){
        $null=Assert-ModpackSource $Workspace $source
        $null=Get-ModpackDirtyOutputs $source.Root
    }else{$null=Assert-ModpackSource $Workspace $source -Clean}
    return $source
}
function Get-ModpackStateRoot([string]$Workspace,[bool]$Test) {
    $root=Join-Path (Get-ModpackCoordinator $Workspace) '.release-output/SteamWorkshop'
    if($Test){$root=Join-Path $root 'test'}
    Assert-ApiMainUnlinkedPath $root
    return $root
}
function Get-PreparedModpackSource([string]$Workspace,[bool]$Test,[string]$Branch) {
    $path=Join-Path (Get-ModpackStateRoot $Workspace $Test) 'prepared/SerpsMods.json'
    $proof=[IO.File]::ReadAllText($path)|ConvertFrom-Json
    if(-not $proof.PSObject.Properties['ModpackSource']){throw 'Prepare the modpack with branch selection before uploading.'}
    $source=$proof.ModpackSource
    if(($Branch -and $source.Branch -cne $Branch) -or ($source.Branch -eq 'develop' -and -not $Test) -or
        $proof.Mode -cne $(if($Test){'Test'}else{'Production'}) -or $proof.Commit -cne $source.Commit){throw 'Prepared modpack branch/mode/commit mismatch.'}
    $root=Assert-ModpackSource $Workspace $source
    Assert-ModpackPreparedOutputs $root $source.GeneratedOutputs
    return $source
}

# Upload consumes a preparation, never a newly selected release/SDK. Historical released
# packages remain uploadable through their own immutable PublishedRelease proof.
function Get-PreparedModpackUploadSource([string]$Workspace,[bool]$Test,[string]$Branch) {
    $path=Join-Path (Get-ModpackStateRoot $Workspace $Test) 'prepared/SerpsMods.json'
    if(-not(Test-Path -LiteralPath $path)){throw 'Prepare the modpack before uploading.'}
    $proof=[IO.File]::ReadAllText($path)|ConvertFrom-Json
    if($proof.Mode -cne $(if($Test){'Test'}else{'Production'})){throw 'Prepared modpack mode mismatch.'}
    if($proof.PSObject.Properties['ModpackSource']){return Get-PreparedModpackSource $Workspace $Test $Branch}
    if(-not $proof.PSObject.Properties['PublishedRelease'] -or ($Branch -and $Branch -cne 'main')){
        throw 'Prepared modpack has no matching source or historical release proof.'
    }
    return Resolve-ModpackSource $Workspace 'main' -AllowGeneratedOutputs
}

# The host driver refreshes tracked output after the source commit. Freeze only these
# known build products; every source edit or unrelated dirty path remains forbidden.
function Get-ModpackGeneratedOutputPaths {
    return @('SerpsModsHost/BepInEx/plugins/SerpsMods_Serp/SerpsModsHost.dll',
        'SerpsModsHost/BepInEx/plugins/SerpsMods_Serp/SerpsModsHost.pdb',
        'SerpsModsHost/BepInEx/plugins/SerpsMods_Serp/info.json',
        'SerpsModsHost/BepInEx/plugins/SerpsMods_Serp/serps-modpack.json')
}
function Get-ModpackDirtyOutputs([string]$Root) {
    if(@(Invoke-ApiRepositoryGit $Root @('diff','--cached','--name-only')).Count -or
       @(Invoke-ApiRepositoryGit $Root @('ls-files','--others','--exclude-standard')).Count){throw 'Unexpected staged/untracked changes after preparation.'}
    $allowed=@(Get-ModpackGeneratedOutputPaths)
    $records=@(foreach($path in @(Invoke-ApiRepositoryGit $Root @('-c','core.quotepath=false','diff','--no-renames','--name-only'))){
        if($path -cnotin $allowed){throw "Unexpected source change after preparation: $path"}
        $file=Join-Path $Root $path;Assert-ApiMainUnlinkedPath $file
        [ordered]@{Path=$path;Sha256=(Get-ApiRepositoryHash $file)}
    })
    return @($records|Sort-Object {$_.Path})
}
function Get-ModpackPreparationSource([string]$Root,[string]$Branch,[string]$Commit) {
    $source=[ordered]@{Root=[IO.Path]::GetFullPath($Root);Branch=$Branch;Commit=$Commit}
    $null=Assert-ModpackSource $Root $source
    $source.GeneratedOutputs=@(Get-ModpackDirtyOutputs $Root)
    return $source
}
function Assert-ModpackPreparedOutputs([string]$Root,$Outputs) {
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach($output in @($Outputs)){
        if($output.Path -cnotin @(Get-ModpackGeneratedOutputPaths) -or $output.Sha256 -cnotmatch '^[0-9a-f]{64}$' -or
           -not $seen.Add([string]$output.Path)){throw 'Invalid generated-output identity.'}
    }
    $actual=@(Get-ModpackDirtyOutputs $Root)
    if((ConvertTo-Json -InputObject $actual -Compress) -cne (ConvertTo-Json -InputObject @($Outputs) -Compress)){throw 'Generated outputs changed since preparation; prepare again.'}
}
