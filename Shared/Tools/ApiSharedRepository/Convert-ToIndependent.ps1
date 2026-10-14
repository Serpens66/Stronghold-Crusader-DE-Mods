[CmdletBinding()]
param([string]$Workspace, [switch]$SimulateFailure)
$ErrorActionPreference = 'Stop'
if (-not $Workspace) { $Workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')) }
. (Join-Path $PSScriptRoot 'RepositoryProcess.Common.ps1')
function Git([string]$Root, [string[]]$Arguments) { return Invoke-ApiRepositoryGit $Root $Arguments }
function Write-Text([string]$Path, [string]$Text) {
    $value = [regex]::Replace($Text, '\r?\n', "`r`n")
    [IO.File]::WriteAllText($Path, $value, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($Path) -cne $value) { throw "Write verification failed: $Path" }
}
$Workspace = [IO.Path]::GetFullPath($Workspace)
$api = Join-Path $Workspace 'APIShared'; $pointer = Join-Path $api '.git'
if (Test-Path -LiteralPath $pointer -PathType Container) { throw 'APIShared already has its own Git directory; no migration performed.' }
$entry = (Git $Workspace @('ls-files','--stage','--','APIShared')) -join ''
if ($entry -notmatch '^160000 [0-9a-f]{40} 0\s+APIShared$') { throw 'Expected one non-conflicted APIShared gitlink in the index.' }
$gitDir = ((Git $api @('rev-parse','--absolute-git-dir')) -join '').Trim()
$parentGit = ((Git $Workspace @('rev-parse','--absolute-git-dir')) -join '').Trim()
if (Test-Path -LiteralPath (Join-Path $parentGit 'index.lock')) { throw 'The mod repository index is locked; finish the active Git operation first.' }
if (-not [IO.Path]::GetFullPath($gitDir).StartsWith([IO.Path]::GetFullPath($parentGit).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Submodule metadata is outside the workspace Git directory.' }
function Assert-ApiMainPath([string]$Path) {
    for ($current=$Path; $current; $current=[IO.Path]::GetDirectoryName($current)) {
        if ((Test-Path -LiteralPath $current) -and ((Get-Item -LiteralPath $current -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked migration path: $current" }
    }
}

function Snapshot([string]$Root) {
    $files=[ordered]@{}
    foreach($relative in @(Git $Root @('-c','core.quotepath=false','ls-files','--cached','--others','--exclude-standard'))){
        $path=Join-Path $Root $relative
        $files[$relative]=if(Test-Path -LiteralPath $path -PathType Leaf){Get-ApiRepositoryHash $path}else{'missing'}
    }
    return [ordered]@{Head=((Git $Root @('rev-parse','HEAD')) -join ''); Branch=((Git $Root @('branch','--show-current')) -join ''); Status=@(Git $Root @('status','--porcelain=v1','--untracked-files=all')); Index=@(Git $Root @('ls-files','--stage')); Refs=@(Git $Root @('for-each-ref','--format=%(refname) %(objectname)')); Stash=@(Git $Root @('stash','list')); Files=$files}
}
foreach ($path in @($Workspace,$api,$gitDir)) { Assert-ApiMainPath $path }
$worktrees = @(); $rows = @(Git $api @('worktree','list','--porcelain'))
foreach ($row in $rows) {
    if ($row.StartsWith('worktree ')) {
        $root = $row.Substring(9)
        if ([IO.Path]::GetFullPath($root) -eq [IO.Path]::GetFullPath($gitDir)) { $root=$api }
        Assert-ApiMainPath $root
        foreach ($operation in @('index.lock','MERGE_HEAD','CHERRY_PICK_HEAD','REVERT_HEAD','rebase-merge','rebase-apply')) {
            $state = ((Git $root @('rev-parse','--path-format=absolute','--git-path',$operation)) -join '').Trim()
            if (Test-Path -LiteralPath $state) { throw "Finish the Git operation before migrating: $state" }
        }
        $worktrees += [pscustomobject]@{Root=$root; State=(Snapshot $root); Pointer=[IO.File]::ReadAllBytes((Join-Path $root '.git'))}
    }
}
$backup = Join-Path $Workspace ('.inspect/APISharedIndependent-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
[void][IO.Directory]::CreateDirectory($backup)
$savedFiles = @{}
foreach ($file in @((Join-Path $parentGit 'index'),(Join-Path $parentGit 'config'),(Join-Path $Workspace '.gitmodules'),(Join-Path $Workspace '.gitignore'))) {
    $savedFiles[$file] = if (Test-Path -LiteralPath $file) { [IO.File]::ReadAllBytes($file) } else { $null }
}
Copy-Item -LiteralPath $gitDir -Destination (Join-Path $backup 'git') -Recurse
foreach ($file in $savedFiles.Keys) { if ($null -ne $savedFiles[$file]) { [IO.File]::WriteAllBytes((Join-Path $backup ([IO.Path]::GetFileName($file))),$savedFiles[$file]) } }
[IO.File]::WriteAllText((Join-Path $backup 'state.json'),(ConvertTo-Json -InputObject $worktrees -Depth 12),[Text.UTF8Encoding]::new($false))
Write-Host "Recovery backup: $backup"
$activated=$false
try {
    # Preserve the old metadata as an inactive backup. Never deinit or delete working files.
    Move-Item -LiteralPath $pointer -Destination (Join-Path $backup 'original-git-pointer')
    $activated=$true
    Copy-Item -LiteralPath (Join-Path $backup 'git') -Destination $pointer -Recurse
    Git $Workspace @('config','-f',(Join-Path $pointer 'config'),'--unset','core.worktree') | Out-Null
    Git $api @('worktree','repair') | Out-Null
    foreach ($worktree in $worktrees) {
        if ((ConvertTo-Json -InputObject (Snapshot $worktree.Root) -Depth 10 -Compress) -cne (ConvertTo-Json -InputObject $worktree.State -Depth 10 -Compress)) { throw "Worktree state changed: $($worktree.Root)" }
        $common=((Git $worktree.Root @('rev-parse','--path-format=absolute','--git-common-dir')) -join '').Trim()
        if ([IO.Path]::GetFullPath($common) -ne [IO.Path]::GetFullPath($pointer)) { throw 'Worktree still depends on old metadata.' }
    }
    if ($SimulateFailure) { throw 'Simulated interruption after worktree repair.' }
    Git $Workspace @('rm','--cached','--','APIShared') | Out-Null
    $modules=Join-Path $Workspace '.gitmodules'
    Git $Workspace @('config','-f',$modules,'--remove-section','submodule.APIShared') | Out-Null
    if ([string]::IsNullOrWhiteSpace([IO.File]::ReadAllText($modules))) { Remove-Item -LiteralPath $modules } else { Write-Text $modules ([IO.File]::ReadAllText($modules)) }
    Git $Workspace @('config','--local','--remove-section','submodule.APIShared') | Out-Null
    $ignore=Join-Path $Workspace '.gitignore'
    $text=if(Test-Path -LiteralPath $ignore){[IO.File]::ReadAllText($ignore)}else{''}
    if ($text -notmatch '(?m)^/APIShared/\r?$') { Write-Text $ignore ($text.TrimEnd()+"`r`n`r`n# Independent local APIShared repository`r`n/APIShared/`r`n") }
    if (@(Git $Workspace @('ls-files','--','APIShared')).Count) { throw 'APIShared is still tracked by the workspace.' }
    Git $Workspace @('check-ignore','APIShared/') | Out-Null
    Write-Host 'PASS: independent APIShared repository; all worktree states and refs preserved. No commit or push performed.'
} catch {
    $failure=$_
    Write-Host "Migration failed; restoring original links: $failure"
    foreach ($worktree in $worktrees) {
        $file=Join-Path $worktree.Root '.git'
        if ($worktree.Root -eq $api) {
            if ($activated -and (Test-Path -LiteralPath $file)) { Move-Item -LiteralPath $file -Destination (Join-Path $backup 'failed-independent-git') }
        }
        $stream=[IO.File]::Open($file,[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::Write)
        try { $stream.SetLength(0); $stream.Write($worktree.Pointer,0,$worktree.Pointer.Length) } finally { $stream.Dispose() }
    }
    foreach ($file in $savedFiles.Keys) {
        if ($null -eq $savedFiles[$file]) { if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file} }
        else { [IO.File]::WriteAllBytes($file,$savedFiles[$file]) }
    }
    throw $failure
}
