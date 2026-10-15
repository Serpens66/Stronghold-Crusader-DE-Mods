. (Join-Path $PSScriptRoot '../ApiSharedRepository/MainSource.Common.ps1')
function Get-ModpackCoordinator([string]$Workspace) {
    $common=((Invoke-ApiRepositoryGit $Workspace @('rev-parse','--path-format=absolute','--git-common-dir')) -join '').Trim()
    if([IO.Path]::GetFileName($common) -cne '.git'){throw 'A normal shared modpack Git repository is required.'}
    return [IO.Path]::GetDirectoryName($common)
}
function Get-ModpackBranchChoice([string]$Branch,[string]$Selection) {
    if($Branch){if($Branch -notin @('main','develop')){throw 'Unsupported modpack branch.'};return $Branch}
    if(-not $Selection){
        Write-Host "Modpack-Branch (APIShared bleibt veroeffentlichtes 0.9.2):`r`n1. main - oeffentlicher Release`r`n2. develop - privates Testpaket`r`n0. Abbrechen"
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
function Resolve-ModpackSource([string]$Workspace,[string]$Branch) {
    if($Branch -cnotin @('main','develop')){throw 'Unsupported modpack branch.'}
    $root=$null;$candidate=$null
    foreach($line in @(Invoke-ApiRepositoryGit $Workspace @('-c','core.quotepath=false','worktree','list','--porcelain'))){
        if($line.StartsWith('worktree ')){$candidate=$line.Substring(9)}
        if($line -ceq ('branch refs/heads/'+$Branch)){$root=$candidate}
    }
    if(-not $root){
        $root=Join-Path (Get-ModpackCoordinator $Workspace) ('.release-output/Modpack/'+$Branch+'/source')
        Assert-ApiMainUnlinkedPath $root
        if(Test-Path -LiteralPath $root){throw 'Unregistered modpack worktree exists; inspect it before continuing.'}
        Invoke-ApiRepositoryGit $Workspace @('worktree','add',$root,$Branch)|Out-Null
    }
    $source=[ordered]@{Branch=$Branch;Root=[IO.Path]::GetFullPath($root);Commit=((Invoke-ApiRepositoryGit $root @('rev-parse','HEAD')) -join '').Trim()}
    $null=Assert-ModpackSource $Workspace $source -Clean
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
    $null=Assert-ModpackSource $Workspace $source -Clean
    return $source
}
