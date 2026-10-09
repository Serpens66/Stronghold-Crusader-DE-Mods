[CmdletBinding()]
param([string]$Revision='origin/main', [string]$Workspace)
if (-not $Workspace) { $Workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')) }
. (Join-Path $PSScriptRoot 'ApiShared.Common.ps1')
try {
    $null=Get-ApiSubmodule $Workspace
    $api=Join-Path $Workspace 'APIShared'
    if (-not (Test-Path -LiteralPath (Join-Path $api '.git'))) {
        Invoke-ApiGit $Workspace @('submodule','update','--init','--','APIShared') | Write-Host
    }
    $api=Assert-ApiCheckout $Workspace
    $status=@(Invoke-ApiGit $api @('status','--porcelain','--untracked-files=normal'))
    if ($status.Count) { throw 'APIShared has open changes. Commit or explicitly preserve them before updating.' }
    foreach ($state in @('MERGE_HEAD','CHERRY_PICK_HEAD','REVERT_HEAD','rebase-merge','rebase-apply')) {
        $path=((Invoke-ApiGit $api @('rev-parse','--git-path',$state)) -join '').Trim()
        if (-not [IO.Path]::IsPathRooted($path)) { $path=Join-Path $api $path }
        if(Test-Path -LiteralPath $path) { throw "Finish or abort the current Git operation first: $state" }
    }
    if ($Revision.StartsWith('-')) { throw 'A revision cannot be a Git option.' }
    Invoke-ApiGit $api @('fetch','origin','--tags') | Write-Host
    $target=((Invoke-ApiGit $api @('rev-parse','--verify',"$Revision^{commit}")) -join '').Trim()
    $branch=(Invoke-ApiGit $api @('branch','--show-current')) -join ''
    if (-not $branch) {
        $suffix=0; $branch='codex/api-work'
        do {
            & git -C $api show-ref --verify --quiet "refs/heads/$branch"
            if ($LASTEXITCODE -ne 0) { break }
            $suffix++; $branch="codex/api-work-$suffix"
        } while ($true)
        Invoke-ApiGit $api @('switch','-c',$branch) | Write-Host
    }
    Write-Host "Merging selected APIShared commit $target into $branch."
    & git -C $api merge --no-edit --no-overwrite-ignore $target
    if ($LASTEXITCODE -ne 0) {
        $conflicts=@(& git -C $api diff --name-only --diff-filter=U)
        $mergeHead=& git -C $api rev-parse --quiet --verify MERGE_HEAD
        if ($LASTEXITCODE -eq 0) {
            if ($conflicts.Count) {
                Write-Host 'Merge stopped. Conflicting files:'
                $conflicts | Write-Host
                Write-Host "Resolve the conflicts, then use: git -C `"$api`" add <files>"
            }
            Write-Host "Continue: git -C `"$api`" merge --continue"
            Write-Host "Cancel: git -C `"$api`" merge --abort"
        } else {
            Write-Host 'Git blocked the merge before it started. Address the error above, preserve any local files, then run this helper again.'
        }
        Write-Host 'Nothing was pushed or committed in the mod repository.'
        exit 1
    }
    Write-Host 'Update complete. Build/test APIShared; publish its commits, then commit the reviewed submodule pointer in the mod repository.'
} catch { Write-Host $_ -ForegroundColor Red; exit 1 }
