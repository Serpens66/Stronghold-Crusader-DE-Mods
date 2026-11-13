. (Join-Path $PSScriptRoot 'RepositoryProcess.Common.ps1')
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
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
function Test-FrozenWorkshopIdentity($Identity) {
    if(-not $Identity){return $false}
    if($Identity -is [Collections.IDictionary]){return $Identity.Contains('Kind') -and $Identity.Kind -ceq 'FrozenWorkshopDelivery'}
    return $Identity.PSObject.Properties['Kind'] -and $Identity.Kind -ceq 'FrozenWorkshopDelivery'
}
function Assert-ApiMainSelection([string]$Workspace,[string]$Path) {
    $Workspace=Get-ApiMainWorkspace $Workspace
    Assert-ApiMainUnlinkedPath $Path
    $selection=[IO.File]::ReadAllText($Path)|ConvertFrom-Json
    if($selection.PSObject.Properties['Kind'] -and $selection.Kind -ceq 'FrozenWorkshopDelivery'){
        return Assert-FrozenWorkshopDelivery $Workspace $Path $selection
    }
    if($selection.SchemaVersion -ne 1 -or $selection.Commit -cnotmatch '^[0-9a-f]{40}$' -or $selection.Branch -cne 'main'){throw 'Invalid main selection identity.'}
    $shortBase = [IO.Path]::GetFullPath((Join-Path $Workspace '.work/b/api-main'))
    $selectedRoot = [IO.Path]::GetFullPath([string]$selection.Root)
    $cacheRoot=[IO.Path]::GetFullPath((Join-Path $Workspace '.work/c/apishared-main'))
    if($selectedRoot -ieq $cacheRoot){
        $root=$cacheRoot;$expectedPath=$cacheRoot+'.selection.json'
    } elseif ($selectedRoot -match ('^' + [regex]::Escape($shortBase) + '\\[0-9]+$')) {
        $root=$selectedRoot
        $expectedPath=$root+'.selection.json'
    } else {
        # Historical pinned SDK evidence retains its exact original location.
        $base=Join-Path $Workspace ('.release-output/APIShared/main/'+$selection.Commit)
        $expectedPath=[IO.Path]::GetFullPath((Join-Path $base 'selection.json'))
        $root=[IO.Path]::GetFullPath((Join-Path $base 'source'))
    }
    if(-not [string]::Equals([IO.Path]::GetFullPath($Path),$expectedPath,[StringComparison]::OrdinalIgnoreCase) -or
        -not [string]::Equals([string]$selection.Root,$root,[StringComparison]::OrdinalIgnoreCase)){throw 'main selection is outside its commit-specific worktree.'}
    Assert-ApiRepositoryOrigin $root
    if(((Invoke-ApiRepositoryGit $root @('rev-parse','HEAD'))-join '') -cne $selection.Commit -or
        @(Invoke-ApiRepositoryGit $root @('status','--porcelain','--untracked-files=normal')).Count){throw 'main worktree changed; preserve it and inspect before rebuilding.'}
    $ancestor=Invoke-ApiRepositoryProcess 'git' @('-C',$root,'merge-base','--is-ancestor',$selection.Commit,'refs/remotes/origin/main')
    if($ancestor.ExitCode -ne 0){throw 'Selected source is not proven to belong to fetched public main history.'}
    if((Get-ApiMainSourceFingerprint $root) -cne $selection.SourceFingerprint){throw 'main source fingerprint changed.'}
    return $selection
}
function Resolve-ApiMainSelection([string]$Workspace,[switch]$RequireExisting) {
    $Workspace=Get-ApiMainWorkspace $Workspace;$api=Join-Path $Workspace 'APIShared'
    Assert-ApiRepositoryOrigin $api
    if($env:SHCDE_API_SHARED_MAIN_SELECTION){$null=Assert-ApiMainSelection $Workspace $env:SHCDE_API_SHARED_MAIN_SELECTION;return $env:SHCDE_API_SHARED_MAIN_SELECTION}
    $remote=@(Invoke-ApiRepositoryGit $api @('ls-remote','--heads','origin','refs/heads/main'))
    if($remote.Count -ne 1 -or $remote[0] -cnotmatch '^([0-9a-f]{40})\s+refs/heads/main$'){throw 'Cannot resolve public main.'}
    $commit=$Matches[1]
    # The public-main SDK is the same bounded cache used by ordinary consumers.
    # No per-release API source clones or duplicated full SDK builds are needed.
    Invoke-ApiRepositoryGit $api @('fetch','--no-tags','origin','+refs/heads/main:refs/remotes/origin/main')|Out-Null
    . (Join-Path $PSScriptRoot '../BranchBuild/BranchBuild.Common.ps1')
    $sdk=Get-CachedApiSdk $Workspace $api 'main' -Commit $commit -RequireExisting:$RequireExisting
    $root=[IO.Path]::GetFullPath($sdk.Root);$path=$root+'.selection.json'
    $selection=[ordered]@{SchemaVersion=1;Branch='main';Commit=$commit;Root=[IO.Path]::GetFullPath($root);SourceFingerprint=(Get-ApiMainSourceFingerprint $root)}
    [IO.File]::WriteAllText($path,((ConvertTo-Json -InputObject $selection -Depth 5)-replace '\r?\n',"`r`n")+"`r`n",[Text.UTF8Encoding]::new($false))
    $null=Assert-ApiMainSelection $Workspace $path
    Write-Host "APIShared release source: main / $commit (personal checkout unchanged)."
    return $path
}
# Consumer build preflights read APIShared sources at <root>\APIShared, as branch builds provide
# them. A release worktree of the modpack has no such checkout (APIShared is ignored there), so it
# gets a detached worktree of the pinned public-main commit. The coordinator keeps its own checkout.
function Ensure-ApiMainConsumerSource([string]$Root) {
    $Root=[IO.Path]::GetFullPath($Root).TrimEnd('\','/')
    $workspace=Get-ApiMainWorkspace $Root
    if($Root -ieq $workspace){return}
    $selection=Assert-ApiMainSelection $workspace $env:SHCDE_API_SHARED_MAIN_SELECTION
    $nested=Join-Path $Root 'APIShared'
    Assert-ApiMainUnlinkedPath $nested
    if(-not (Test-Path -LiteralPath $nested)){
        Invoke-ApiRepositoryGit (Join-Path $workspace 'APIShared') @('worktree','add','--detach',$nested,$selection.Commit)|Out-Null
        Write-Host "APIShared sources for consumer checks: main / $($selection.Commit) in $nested"
        return
    }
    if(@(Invoke-ApiRepositoryGit $nested @('status','--porcelain','--untracked-files=normal')).Count){throw "APIShared sources in the release worktree were changed; preserve and remove them first: $nested"}
    if(((Invoke-ApiRepositoryGit $nested @('rev-parse','HEAD'))-join '').Trim() -cne $selection.Commit){
        Invoke-ApiRepositoryGit $nested @('checkout','--quiet','--detach',$selection.Commit)|Out-Null
        Write-Host "APIShared sources for consumer checks moved to main / $($selection.Commit)."
    }
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
    try {$null=Assert-ApiBuildProof $selection.Root;return} catch {Write-Host 'Building the pinned main source; no game installation will be changed.'}
    # Recover machine paths as data only; never load development helper code.
    $localProof=Join-Path $Workspace 'APIShared/.local/build-proof.json'
    if(-not $env:SHCDE_GAME_DIR -and (Test-Path -LiteralPath $localProof)) {
        $paths=[IO.File]::ReadAllText($localProof)|ConvertFrom-Json
        $env:SHCDE_GAME_DIR=[string]$paths.GameDir
        if(-not $env:SHCDESE_EXTENDER_DIR){$env:SHCDESE_EXTENDER_DIR=[string]$paths.ExtenderDir}
        if(-not $env:SHCDE_MSBUILD){$env:SHCDE_MSBUILD=[string]$paths.MSBuild}
    }
    $previousBranchPinned = $env:SHCDE_BRANCH_PINNED
    try {
        $env:SHCDE_BRANCH_PINNED = $selection.Commit
        & (Join-Path $selection.Root 'build.bat') /nopause /noinstall
    } finally { $env:SHCDE_BRANCH_PINNED = $previousBranchPinned }
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
    $identity=Save-FrozenWorkshopDelivery $Workspace $selection $publisherProof $outputs
    $null=Assert-ApiMainSelection $Workspace $env:SHCDE_API_SHARED_MAIN_SELECTION
    return $identity
}

function Remove-SupersededWorkshopDeliveries([string]$Workspace,[string[]]$Candidates) {
    $workspace=Get-ApiMainWorkspace $Workspace
    $state=Join-Path $workspace '.release-output/SteamWorkshop'
    $referenced=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($directory in @('prepared','test/prepared')){
        foreach($file in @(Get-ChildItem -LiteralPath (Join-Path $state $directory) -File -Filter '*.json' -ErrorAction SilentlyContinue)){
            $proof=[IO.File]::ReadAllText($file.FullName)|ConvertFrom-Json
            if($proof.PSObject.Properties['MainSource'] -and (Test-FrozenWorkshopIdentity $proof.MainSource)){[void]$referenced.Add([string]$proof.MainSource.Selection)}
        }
    }
    foreach($path in @($Candidates|Sort-Object -Unique)){
        if($referenced.Contains($path) -or -not [IO.File]::Exists($path)){continue}
        $manifest=[IO.File]::ReadAllText($path)|ConvertFrom-Json
        if(-not (Test-FrozenWorkshopIdentity $manifest)){throw 'Unrecognized superseded delivery; retained.'}
        $null=Assert-FrozenWorkshopDelivery $workspace $path $manifest
        # The complete file set was checked, and the path is bound to this state root.
        Remove-Item -LiteralPath $manifest.Root -Recurse -Force -ErrorAction Stop
    }
}

# An upload needs the verified publisher and packaging helpers, not the compiler's
# source checkout. Capture their bytes only after the ordinary public-main/build
# checks above. Original source and build identities remain in the manifest.
function Save-FrozenWorkshopDelivery([string]$Workspace,$Selection,[string]$PublisherProof,$Outputs) {
    $workspace=Get-ApiMainWorkspace $Workspace
    $proofHash=Get-ApiRepositoryHash $PublisherProof
    $root=Join-Path $workspace ('.release-output/SteamWorkshop/delivery/'+$Selection.Commit.Substring(0,12)+'-'+$proofHash.Substring(0,12))
    $path=Join-Path $root 'delivery.json'
    Assert-ApiMainUnlinkedPath $root
    $files=@('tools/WorkshopPackage.ps1','tools/WorkshopUploader/PublisherBuildProof.ps1')
    foreach($helper in @('tools/DependencyBootstrapPackage.ps1','tools/PublisherPackage.ps1')){
        if([IO.File]::Exists((Join-Path $Selection.Root $helper))){$files+=$helper}
    }
    $files+=@('.local/WorkshopUploader/build-proof.json')
    $files+=@($Outputs|ForEach-Object {'.local/WorkshopUploader/'+$_.Name})
    $records=@(foreach($relative in $files){
        $source=Join-Path $Selection.Root $relative;Assert-ApiMainUnlinkedPath $source
        $destination=Join-Path $root $relative;Assert-ApiMainUnlinkedPath $destination
        $hash=Get-ApiRepositoryHash $source
        if([IO.File]::Exists($destination)){
            if((Get-ApiRepositoryHash $destination) -cne $hash){throw 'Frozen Workshop delivery differs from its verified build; inspect before replacing it.'}
        }else{
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination))
            [IO.File]::Copy($source,$destination)
        }
        [ordered]@{Path=$relative;Sha256=$hash}
    })
    $manifest=[ordered]@{SchemaVersion=1;Kind='FrozenWorkshopDelivery';Branch='main';Commit=$Selection.Commit;Root=$root;
        SourceFingerprint=$Selection.SourceFingerprint;PublisherProofSha256=(Get-ApiRepositoryHash $PublisherProof);Files=$records}
    $text=($manifest|ConvertTo-Json -Depth 8)+"`r`n"
    if([IO.File]::Exists($path)){
        if([IO.File]::ReadAllText($path) -cne $text){throw 'Frozen Workshop delivery manifest changed.'}
    }else{[IO.File]::WriteAllText($path,$text,[Text.UTF8Encoding]::new($false))}
    $null=Assert-FrozenWorkshopDelivery $workspace $path ([pscustomobject]$manifest)
    return [ordered]@{Kind='FrozenWorkshopDelivery';Selection=$path;SelectionSha256=(Get-ApiRepositoryHash $path);
        Commit=$Selection.Commit;SourceFingerprint=$Selection.SourceFingerprint;PublisherProofSha256=$manifest.PublisherProofSha256;PublisherOutputs=$Outputs}
}
function Assert-FrozenWorkshopDelivery([string]$Workspace,[string]$Path,$Manifest) {
    if($Manifest.SchemaVersion -ne 1 -or $Manifest.Branch -cne 'main' -or $Manifest.Commit -cnotmatch '^[0-9a-f]{40}$' -or
        $Manifest.SourceFingerprint -cnotmatch '^[0-9a-f]{64}$' -or $Manifest.PublisherProofSha256 -cnotmatch '^[0-9a-f]{64}$'){
        throw 'Invalid frozen Workshop delivery identity.'
    }
    $root=[IO.Path]::GetFullPath((Join-Path (Get-ApiMainWorkspace $Workspace) ('.release-output/SteamWorkshop/delivery/'+$Manifest.Commit.Substring(0,12)+'-'+$Manifest.PublisherProofSha256.Substring(0,12))))
    if([IO.Path]::GetFullPath($Path) -ine (Join-Path $root 'delivery.json') -or $Manifest.Root -ine $root){throw 'Frozen Workshop delivery escapes its state directory.'}
    Assert-ApiMainUnlinkedPath $root
    $known=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach($file in @($Manifest.Files)){
        if($file.Path -cnotmatch '^(tools/[A-Za-z0-9_./-]+\.ps1|\.local/WorkshopUploader/[A-Za-z0-9_./-]+)$' -or
            @(([string]$file.Path).Split('/')|Where-Object {$_ -in @('.','..') -or -not $_}).Count -or
            $file.Sha256 -cnotmatch '^[0-9a-f]{64}$' -or -not $known.Add([string]$file.Path)){throw 'Unsafe or duplicate frozen delivery file.'}
        $actual=Join-Path $root $file.Path;Assert-ApiMainUnlinkedPath $actual
        if((Get-ApiRepositoryHash $actual) -cne $file.Sha256){throw 'Frozen Workshop delivery file changed.'}
    }
    foreach($required in @('tools/WorkshopPackage.ps1','tools/WorkshopUploader/PublisherBuildProof.ps1','.local/WorkshopUploader/build-proof.json','.local/WorkshopUploader/WorkshopUploader.exe')){
        if(-not $known.Contains($required)){throw 'Frozen Workshop delivery is incomplete.'}
    }
    $directories=[Collections.Generic.Stack[string]]::new();$directories.Push($root)
    while($directories.Count){
        foreach($entry in [IO.Directory]::EnumerateFileSystemEntries($directories.Pop())){
            $attributes=[IO.File]::GetAttributes($entry)
            if($attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked frozen delivery entry.'}
            $relative=$entry.Substring($root.Length+1).Replace('\','/')
            if($attributes -band [IO.FileAttributes]::Directory){
                if(-not @($known|Where-Object {$_.StartsWith($relative+'/',[StringComparison]::OrdinalIgnoreCase)}).Count){throw 'Undeclared frozen delivery directory.'}
                $directories.Push($entry)
            }elseif($relative -cne 'delivery.json' -and -not $known.Contains($relative)){throw 'Undeclared frozen delivery file.'}
        }
    }
    if((Get-ApiRepositoryHash (Join-Path $root '.local/WorkshopUploader/build-proof.json')) -cne $Manifest.PublisherProofSha256){throw 'Frozen publisher build proof changed.'}
    $null=Get-ApiMainPublisherOutputs $root
    return $Manifest
}
function Get-ApiMainPublisherOutputs([string]$Root) {
    $directory=Join-Path $Root '.local/WorkshopUploader';Assert-ApiMainUnlinkedPath $directory
    $proof=[IO.File]::ReadAllText((Join-Path $directory 'build-proof.json'))|ConvertFrom-Json
    $known=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $outputs=@($proof.Outputs)
    foreach($output in $outputs){
        $name=[string]$output.Name
        # Publisher proofs use portable relative paths, including localized assets.
        # Reject aliases/traversal before hashing; duplicate identity is case-insensitive on Windows.
        if($name -cnotmatch '^[A-Za-z0-9_.-]+(?:/[A-Za-z0-9_.-]+)*$' -or
            @($name.Split('/')|Where-Object {$_ -in @('.','..') -or $_.EndsWith('.') -or
                ($_ -split '\.')[0] -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$'}).Count -or
            $output.Sha256 -cnotmatch '^[0-9a-f]{64}$' -or -not $known.Add($name)){throw 'Unsafe or duplicate publisher output identity.'}
        $file=[IO.Path]::GetFullPath((Join-Path $directory $name))
        if(-not $file.StartsWith([IO.Path]::GetFullPath($directory).TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Publisher output escapes its directory.'}
        Assert-ApiMainUnlinkedPath $file
        if((Get-ApiRepositoryHash $file) -cne $output.Sha256){throw 'Publisher output differs from prepared build.'}
    }
    if(-not $known.Contains('WorkshopUploader.exe')){throw 'Publisher proof must own WorkshopUploader.exe.'}
    foreach($file in Get-ChildItem -LiteralPath $directory -Recurse -File) {
        Assert-ApiMainUnlinkedPath $file.FullName
        $relative=$file.FullName.Substring($directory.TrimEnd('\').Length+1).Replace('\','/')
        if(($file.Extension -in @('.exe','.dll','.config','.bat','.sh','.ps1') -or
            $relative.StartsWith('Locales/',[StringComparison]::OrdinalIgnoreCase)) -and -not $known.Contains($relative)){
            throw 'Undeclared publisher runtime or localization file.'
        }
    }
    return @($outputs|Sort-Object Name|ForEach-Object {[ordered]@{Name=[string]$_.Name;Sha256=[string]$_.Sha256}})
}
function Assert-ApiMainDeliveryIdentity([string]$Workspace,$Identity) {
    if(-not $Identity){throw 'Preparation predates main isolation; prepare the package again.'}
    $selection=Assert-ApiMainSelection $Workspace ([string]$Identity.Selection)
    if((Get-ApiRepositoryHash $Identity.Selection)-cne $Identity.SelectionSha256 -or $selection.Commit -cne $Identity.Commit -or
        $selection.SourceFingerprint -cne $Identity.SourceFingerprint -or
        (Get-ApiRepositoryHash (Join-Path $selection.Root '.local/WorkshopUploader/build-proof.json')) -cne $Identity.PublisherProofSha256){throw 'Prepared main/publisher identity changed; prepare again.'}
    if(-not ($selection.PSObject.Properties['Kind'] -and $selection.Kind -ceq 'FrozenWorkshopDelivery')){
        . (Join-Path $selection.Root 'tools/WorkshopUploader/PublisherBuildProof.ps1')
        Assert-WorkshopPublisherBuild $selection.Root
    }
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
