[CmdletBinding()]
param([Parameter(Mandatory)][ValidateSet('Develop','Main')][string]$Target,[switch]$NoOpen,[switch]$NoPause)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'RepositoryProcess.Common.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$api=Join-Path $workspace 'APIShared';$exitCode=0
try {
    Assert-ApiRepositoryOrigin $api
    $source=if($Target -eq 'Develop'){'dev/Serpens66'}else{'develop'}
    $base=if($Target -eq 'Develop'){'develop'}else{'main'}
    foreach($branch in @($source,$base)) {
        Invoke-ApiRepositoryGit $api @('fetch','--no-tags','origin',("+refs/heads/{0}:refs/remotes/origin/{0}" -f $branch))|Out-Null
    }
    $sourceCommit=(Invoke-ApiRepositoryGit $api @('rev-parse',"refs/remotes/origin/$source"))-join ''
    $baseCommit=(Invoke-ApiRepositoryGit $api @('rev-parse',"refs/remotes/origin/$base"))-join ''
    if($Target -eq 'Develop') {
        $local=(Invoke-ApiRepositoryGit $api @('rev-parse','refs/heads/dev/Serpens66'))-join ''
        if($local -cne $sourceCommit){throw 'Push/pull dev/Serpens66 in GitHub Desktop first. This helper never publishes local commits.'}
        if(@(Invoke-ApiRepositoryGit $api @('status','--porcelain')).Count){Write-Host 'Open local files are not included; only pushed commits are compared.'}
    }
    Write-Host "APIShared PR: $source -> $base"
    Write-Host 'The PR includes all source commits not yet integrated. Use Create a merge commit; keep both long-lived branches.'
    $pending=(Invoke-ApiRepositoryGit $api @('rev-list','--count',"$baseCommit..$sourceCommit"))-join ''
    if($pending -eq '0'){Write-Host 'No new commits to integrate.';return}
    $merge=Invoke-ApiRepositoryProcess 'git' @('-C',$api,'merge-tree','--write-tree',$baseCommit,$sourceCommit)
    if($merge.ExitCode -eq 1){
        Write-Host 'Conflicts require manual resolution. Merge the target into your source branch in GitHub Desktop, edit the listed files, commit and push there; reopen this helper.'
        ($merge.Output -split '\r?\n' | Select-Object -Skip 1)|Write-Host
        if($Target -eq 'Main'){Write-Host 'Resolve develop/main conflicts through a separate PR into develop; do not push directly to main.'}
    }elseif($merge.ExitCode -ne 0){throw "Could not inspect conflicts: $($merge.Diagnostic)"}
    else {Write-Host 'No textual conflict found. CI and gameplay readiness still require review.'}
    $prs=Invoke-ApiRepositoryProcess 'gh' @('pr','list','--repo','SHCDE-APIShared/APIShared','--state','open','--head',$source,'--base',$base,'--json','url,mergeable,mergeStateStatus')
    if($prs.ExitCode){throw "Cannot query PRs: $($prs.Diagnostic)"}
    $parsedPrs=$prs.Output|ConvertFrom-Json
    $existing=@($parsedPrs)
    if($existing.Count -gt 1){throw 'Multiple open PRs match this branch pair.'}
    $url=if($existing.Count){[string]$existing[0].url}else{'https://github.com/SHCDE-APIShared/APIShared/compare/'+$base+'...'+[Uri]::EscapeDataString($source)+'?expand=1'}
    if($existing.Count -and $url -cnotmatch '^https://github\.com/SHCDE-APIShared/APIShared/pull/\d+$'){throw 'Unexpected PR URL; no browser was opened.'}
    Write-Host $url
    if(-not $NoOpen){Start-Process -FilePath $url}
}catch{Write-Host $_.Exception.Message -ForegroundColor Red;$exitCode=1}
finally{if(-not $NoPause){[void](Read-Host 'Enter zum Schliessen')}}
exit $exitCode
