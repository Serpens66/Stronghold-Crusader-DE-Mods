$ErrorActionPreference = 'Stop'
function Invoke-ApiGit([string]$Directory, [string[]]$Arguments) {
    $result = @(& git -C $Directory @Arguments)
    if ($LASTEXITCODE -ne 0) { throw "Git failed: $($Arguments -join ' ')" }
    return $result
}
function Get-ApiSubmodule([string]$Workspace) {
    $row = (Invoke-ApiGit $Workspace @('ls-tree','HEAD','--','APIShared')) -join ''
    if ($row -notmatch '^160000 commit ([0-9a-f]{40})\s+APIShared$') { throw 'APIShared is not recorded as a submodule in the workspace HEAD.' }
    return $Matches[1]
}
function Assert-ApiCheckout([string]$Workspace) {
    $api = [IO.Path]::GetFullPath((Join-Path $Workspace 'APIShared'))
    if (-not (Test-Path -LiteralPath (Join-Path $api '.git'))) { throw 'APIShared is not initialized. Run APIShared-Aktualisieren.bat or git submodule update --init APIShared.' }
    $top = ((Invoke-ApiGit $api @('rev-parse','--show-toplevel')) -join '').Trim()
    if ([IO.Path]::GetFullPath($top) -ne [IO.Path]::GetFullPath($api)) { throw 'APIShared must be an independent Git checkout.' }
    return $api
}
function Assert-ApiConsumerPackage([string]$Workspace, [string]$PackageDirectory, [switch]$Release) {
    $api = Assert-ApiCheckout $Workspace
    . (Join-Path $api 'tools/BuildProof.ps1')
    $proof = Assert-ApiBuildProof $api -Release:$Release
    $localPackage = [IO.Path]::GetFullPath((Join-Path $api 'BepInEx/plugins/APIShared_Serp'))
    if ($PackageDirectory -and [IO.Path]::GetFullPath($PackageDirectory).TrimEnd('\','/') -ne $localPackage.TrimEnd('\','/')) {
        throw 'Workspace consumers must use the local APIShared package, not an installed or external DLL.'
    }
    return $proof
}
function Assert-ApiReleaseState([string]$Workspace) {
    $api = Assert-ApiCheckout $Workspace
    $expected = Get-ApiSubmodule $Workspace
    $actual = ((Invoke-ApiGit $api @('rev-parse','HEAD')) -join '').Trim()
    if ($actual -ne $expected) { throw 'Commit the reviewed APIShared submodule pointer in the mod repository first.' }
    $status = @(Invoke-ApiGit $Workspace @('status','--porcelain','--untracked-files=normal'))
    if ($status.Count) { throw 'The mod repository must be clean for a release.' }
    $proof = Assert-ApiConsumerPackage $Workspace '' -Release
    # This read-only query accepts released tags or reviewed main history, not temporary contribution branches.
    $url = ((Invoke-ApiGit $api @('remote','get-url','origin')) -join '').Trim()
    if ($url -notmatch '^https://github\.com/SHCDE-APIShared/APIShared(?:\.git)?/?$|^git@github\.com:SHCDE-APIShared/APIShared\.git$') { throw 'APIShared origin is not the canonical public repository.' }
    $refs = @(Invoke-ApiGit $api @('ls-remote','--heads','--tags','origin'))
    $published = $false
    foreach ($row in $refs) {
        if ($row -notmatch '\srefs/heads/main$|\srefs/tags/') { continue }
        $tip = ($row -split '\s+')[0]
        & git -C $api merge-base --is-ancestor $actual $tip 2>$null
        if ($LASTEXITCODE -eq 0) { $published = $true; break }
    }
    if (-not $published) { throw 'APIShared commit publication cannot be proven from public refs and local objects. Push the commit or fetch the published refs explicitly.' }
    return $proof
}
