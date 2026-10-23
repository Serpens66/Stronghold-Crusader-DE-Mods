$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'MainSource.Common.ps1')
function Invoke-ApiGit([string]$Directory, [string[]]$Arguments) {
    $result = @(& git -C $Directory @Arguments)
    if ($LASTEXITCODE -ne 0) { throw "Git failed: $($Arguments -join ' ')" }
    return $result
}
function Assert-ApiCheckout([string]$Workspace) {
    $api = [IO.Path]::GetFullPath((Join-Path $Workspace 'APIShared'))
    if (-not (Test-Path -LiteralPath (Join-Path $api '.git'))) { throw 'APIShared is missing. Run Shared/Tools/ApiSharedRepository/Initialize-ApiShared.ps1 explicitly, or add your independent APIShared clone at APIShared/.' }
    $top = ((Invoke-ApiGit $api @('rev-parse','--show-toplevel')) -join '').Trim()
    if ([IO.Path]::GetFullPath($top) -ne [IO.Path]::GetFullPath($api)) { throw 'APIShared must be an independent Git checkout.' }
    return $api
}
function Assert-ApiConsumerPackage([string]$Workspace, [string]$PackageDirectory, [switch]$Release) {
    if ($env:SHCDE_BRANCH_API_ROOT -and -not $Release -and -not $env:SHCDE_API_SHARED_RELEASE_PROOF) {
        $root = [IO.Path]::GetFullPath($env:SHCDE_BRANCH_API_ROOT).TrimEnd('\','/')
        if (((Invoke-ApiGit $root @('rev-parse','HEAD')) -join '').Trim() -cne $env:SHCDE_BRANCH_API_COMMIT) { throw 'Selected SDK commit changed.' }
        $expected = [IO.Path]::GetFullPath((Join-Path $root 'BepInEx/plugins/APIShared_Serp'))
        if ($PackageDirectory -and [IO.Path]::GetFullPath($PackageDirectory).TrimEnd('\','/') -ine $expected) { throw 'SDK package differs from the selected branch build.' }
        . (Join-Path $root 'tools/BuildProof.ps1')
        return (Assert-ApiBuildProof $root)
    }
    $api=if($env:SHCDE_API_SHARED_MAIN_SELECTION){Get-WorkshopApiRoot $Workspace}else{Assert-ApiCheckout $Workspace}
    if(-not $PackageDirectory -and $env:SHCDE_API_SHARED_RELEASE_PROOF){$PackageDirectory=$env:SHCDE_API_SHARED_DIR}
    if ($PackageDirectory -and $env:SHCDE_API_SHARED_RELEASE_PROOF) {
        return Assert-ApiPublishedConsumerPackage $Workspace $PackageDirectory $env:SHCDE_API_SHARED_RELEASE_PROOF
    }
    . (Join-Path $api 'tools/BuildProof.ps1')
    $proof = Assert-ApiBuildProof $api -Release:$Release
    $localPackage = [IO.Path]::GetFullPath((Join-Path $api 'BepInEx/plugins/APIShared_Serp'))
    if ($PackageDirectory -and [IO.Path]::GetFullPath($PackageDirectory).TrimEnd('\','/') -ne $localPackage.TrimEnd('\','/')) {
        throw 'Workspace consumers must use the local APIShared package, not an installed or external DLL.'
    }
    return $proof
}
function Assert-ApiPublishedConsumerPackage([string]$Workspace, [string]$PackageDirectory, [string]$ProvenancePath) {
    # Only the release cache selected by the Workshop driver can supply a published SDK.
    # Recheck it in every child build; an environment override alone grants no trust.
    $api=if($env:SHCDE_API_SHARED_MAIN_SELECTION){Get-WorkshopApiRoot $Workspace}else{Assert-ApiCheckout $Workspace}
    $Workspace=Get-ApiMainWorkspace $Workspace
    $cacheRoot = [IO.Path]::GetFullPath((Join-Path $Workspace '.release-output/APIShared')).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    $testCacheRoot = [IO.Path]::GetFullPath((Join-Path $Workspace '.release-output/SteamWorkshop/test/SerpsMods/cache/APIShared')).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
    $proofPath = [IO.Path]::GetFullPath($ProvenancePath)
    $allowedRoot = if ($env:SHCDE_WORKSHOP_TEST -eq '1') { $testCacheRoot } else { $cacheRoot }
    if (-not $proofPath.StartsWith($allowedRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Published APIShared proof must be in the workspace release cache for the selected mode.' }
    $cache = Split-Path -Parent $proofPath
    $audit = Join-Path $cache 'audit'
    $expectedDirectory = [IO.Path]::GetFullPath((Join-Path $audit 'BepInEx/plugins/APIShared_Serp'))
    if ([IO.Path]::GetFullPath($PackageDirectory).TrimEnd('\','/') -ne $expectedDirectory.TrimEnd('\','/')) { throw 'Published APIShared SDK path does not match its release proof.' }
    $proof = Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    $info = Get-Content -LiteralPath (Join-Path $api 'info.json') -Raw | ConvertFrom-Json
    if ($proof.Commit -notmatch '^[0-9a-f]{40}$') { throw 'Invalid published APIShared source commit.' }
    if($env:SHCDE_API_SHARED_MAIN_SELECTION){
        $selected=Assert-ApiMainSelection $Workspace $env:SHCDE_API_SHARED_MAIN_SELECTION
        $ancestor=Invoke-ApiRepositoryProcess 'git' @('-C',$api,'merge-base','--is-ancestor',[string]$proof.Commit,$selected.Commit)
        if($ancestor.ExitCode -ne 0){throw 'Published APIShared SDK source is outside selected main history.'}
    }
    $tag = "APIShared/v$($info.Version)"
    if ($env:SHCDE_WORKSHOP_TEST -eq '1') { $tag = "APIShared/test/v$($info.Version)-$(([string]$proof.Commit).Substring(0,12))" }
    if ($proof.SchemaVersion -ne 1 -or $proof.Mod -cne 'APIShared' -or $proof.PluginGuid -cne 'APIShared_Serp' -or
        $proof.Version -cne $info.Version -or $proof.Tag -cne $tag) { throw 'Published APIShared SDK identity or mode mismatch.' }
    . (Join-Path $PSScriptRoot '../Steam/SteamPackPolicy.ps1')
    if($env:SHCDE_API_SHARED_MAIN_SELECTION -or $env:SHCDE_API_SHARED_RELEASE_PROOF_SHA256){
        if($env:SHCDE_API_SHARED_RELEASE_PROOF_SHA256 -cnotmatch '^[0-9a-f]{64}$' -or
           (Get-SteamPackFileSha256 $proofPath) -cne $env:SHCDE_API_SHARED_RELEASE_PROOF_SHA256){
            throw 'Published APIShared evidence changed since canonical release selection.'
        }
    }
    $paths = @(Invoke-ApiGit $api @('diff','--name-only',[string]$proof.Commit,'--'))
    $paths += @(Invoke-ApiGit $api @('ls-files','--others','--exclude-standard'))
    $changed = @(Get-SteamApiReleaseRelevantChanges $paths)
    if ($changed.Count) { throw "Published APIShared SDK differs from current package sources: $($changed -join ', ')" }
    $zip = Join-Path $cache "APIShared-v$($proof.Version).zip"
    if ((Get-SteamPackFileSha256 $zip) -cne $proof.Package.Sha256) { throw 'Published APIShared ZIP hash mismatch.' }
    $known = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($record in @($proof.Package.Files)) {
        $relative = [string]$record.Path
        if ($relative -notmatch '^BepInEx/' -or $relative -match '(^|/)\.\.?(/|$)|[:\\]|//|/$' -or -not $known.Add($relative)) { throw "Unsafe or duplicate published APIShared file: $relative" }
        $file = Join-Path $audit $relative
        if (-not (Test-Path -LiteralPath $file -PathType Leaf) -or (Get-Item -LiteralPath $file).Length -ne $record.Size -or
            (Get-SteamPackFileSha256 $file) -cne $record.Sha256) { throw "Published APIShared file hash mismatch: $relative" }
    }
    # Refuse links in any ancestor or descendant, including a redirected cache directory.
    $ancestor = Get-Item -LiteralPath $audit
    while ($ancestor -and $ancestor.FullName.StartsWith([IO.Path]::GetFullPath($Workspace), [StringComparison]::OrdinalIgnoreCase)) {
        if ($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Published APIShared SDK contains a link.' }
        $ancestor = $ancestor.Parent
    }
    foreach ($entry in @(Get-ChildItem -LiteralPath $audit -Recurse -Force)) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Published APIShared SDK contains a link.' }
        if (-not $entry.PSIsContainer) {
            $relative = $entry.FullName.Substring($audit.Length + 1).Replace('\','/')
            if (-not $known.Contains($relative)) { throw "Undeclared published APIShared file: $relative" }
        }
    }
    if (-not $known.Contains('BepInEx/plugins/APIShared_Serp/APIShared.dll')) { throw 'Published APIShared SDK has no declared DLL.' }
    return $proof
}
function Assert-ApiReleaseState([string]$Workspace) {
    if($env:SHCDE_API_SHARED_MAIN_SELECTION){
        $null=Assert-ApiMainSelection $Workspace $env:SHCDE_API_SHARED_MAIN_SELECTION
        $status=@(Invoke-ApiGit $Workspace @('status','--porcelain','--untracked-files=normal','--','.',':(exclude)APIShared'))
        if($status.Count){throw 'The mod repository must be clean for a release (APIShared development checkout is independent).'}
        return (Assert-ApiConsumerPackage $Workspace '' -Release)
    }
    $api = Assert-ApiCheckout $Workspace
    $actual = ((Invoke-ApiGit $api @('rev-parse','HEAD')) -join '').Trim()
    $status = @(Invoke-ApiGit $Workspace @('status','--porcelain','--untracked-files=normal'))
    if ($status.Count) { throw 'The mod repository must be clean for a release.' }
    $proof = Assert-ApiConsumerPackage $Workspace '' -Release
    # This read-only query accepts released tags or reviewed main history, not temporary contribution branches.
    $url = ((Invoke-ApiGit $api @('remote','get-url','origin')) -join '').Trim()
    if ($url -notmatch '^https://github\.com/SHCDE-APIShared/APIShared(?:\.git)?/?$|^git@github\.com:SHCDE-APIShared/APIShared\.git$') { throw 'APIShared origin is not the canonical public repository.' }
    $refs = @(Invoke-ApiGit $api @('ls-remote','--heads','--tags','origin'))
    $published = $false
    foreach ($row in $refs) {
        if ($row -notmatch '\srefs/heads/main$|\srefs/tags/APIShared/v\d+\.\d+\.\d+(?:\.\d+)?(?:\^\{\})?$') { continue }
        $tip = ($row -split '\s+')[0]
        & git -C $api merge-base --is-ancestor $actual $tip 2>$null
        if ($LASTEXITCODE -eq 0) { $published = $true; break }
    }
    if (-not $published) { throw 'APIShared commit publication cannot be proven from public refs and local objects. Push the commit or fetch the published refs explicitly.' }
    return $proof
}
