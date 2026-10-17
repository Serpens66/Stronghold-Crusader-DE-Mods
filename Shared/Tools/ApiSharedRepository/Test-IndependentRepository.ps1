$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'RepositoryProcess.Common.ps1')
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
$temp = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -Kind t -Name 'IndependentRepository'
$env:GIT_ALLOW_PROTOCOL='file'
function G([string]$Root,[string[]]$Arguments){return Invoke-ApiRepositoryGit $Root $Arguments}
function Check([bool]$Value,[string]$Message){if(-not $Value){throw $Message}}
function Write-Fixture([string]$Path,[string]$Text){[IO.File]::WriteAllText($Path,$Text,[Text.UTF8Encoding]::new($false))}
[void][IO.Directory]::CreateDirectory($temp)
foreach($case in @('rollback','success')) {
    $parent=Join-Path $temp $case; $origin=Join-Path $temp ($case+'-origin')
    G $temp @('init','--initial-branch=main',$origin)|Out-Null
    G $origin @('config','user.name','Fixture');G $origin @('config','user.email','fixture@example.invalid')
    Write-Fixture (Join-Path $origin 'file.txt') 'base';G $origin @('add','.');G $origin @('commit','-m','base')|Out-Null
    G $temp @('init','--initial-branch=main',$parent)|Out-Null
    G $parent @('config','user.name','Fixture');G $parent @('config','user.email','fixture@example.invalid')
    G $parent @('submodule','add',$origin,'APIShared')|Out-Null
    Write-Fixture (Join-Path $parent '.gitignore') ".inspect/`r`n"
    Write-Fixture (Join-Path $parent 'parent.txt') 'base'
    G $parent @('add','.');G $parent @('commit','-m','submodule')|Out-Null
    $api=Join-Path $parent 'APIShared'
    G $api @('config','user.name','Fixture');G $api @('config','user.email','fixture@example.invalid')
    G $api @('switch','-c','dev/Serpens66')|Out-Null
    Write-Fixture (Join-Path $api 'local.txt') 'unpublished';G $api @('add','.');G $api @('commit','-m','unpublished')|Out-Null
    $linked=Join-Path $temp ($case+'-linked');G $api @('worktree','add','--detach',$linked,'HEAD')|Out-Null
    Write-Fixture (Join-Path $api 'file.txt') 'staged';G $api @('add','file.txt');Write-Fixture (Join-Path $api 'file.txt') 'open'
    Write-Fixture (Join-Path $api 'unknown.txt') 'unknown';Write-Fixture (Join-Path $linked 'file.txt') 'linked open'
    Write-Fixture (Join-Path $parent 'parent.txt') 'parent staged';G $parent @('add','parent.txt');Write-Fixture (Join-Path $parent 'parent.txt') 'parent open'
    $indexBefore=(G $parent @('ls-files','--stage','--','parent.txt')) -join ''
    $head=(G $api @('rev-parse','HEAD')) -join ''
    $apiStatus=(G $api @('status','--porcelain')) -join "`n"
    $arguments=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'Convert-ToIndependent.ps1'),'-Workspace',$parent)
    if($case -eq 'rollback'){$arguments+='-SimulateFailure'}
    $run=Invoke-ApiRepositoryProcess 'powershell.exe' $arguments
    Write-Host $run.Output
    if($case -eq 'success' -and $run.ExitCode -ne 0){throw $run.Diagnostic}
    Check (($run.ExitCode -eq 0) -eq ($case -eq 'success')) 'Unexpected migration result'
    Check (((G $api @('rev-parse','HEAD')) -join '') -eq $head) 'HEAD changed'
    Check (((G $api @('status','--porcelain')) -join "`n") -ceq $apiStatus) 'Open or staged API files changed'
    Check ([IO.File]::ReadAllText((Join-Path $linked 'file.txt')) -ceq 'linked open') 'Linked files changed'
    Check (((G $parent @('ls-files','--stage','--','parent.txt')) -join '') -ceq $indexBefore) 'Parent staging changed'
    Check ([IO.File]::ReadAllText((Join-Path $parent 'parent.txt')) -ceq 'parent open') 'Parent file changed'
    if($case -eq 'success') {
        Check (Test-Path -LiteralPath (Join-Path $api '.git') -PathType Container) 'No independent metadata'
        Check (@(G $parent @('ls-files','--','APIShared')).Count -eq 0) 'APIShared still indexed'
        Check (@(G $parent @('status','--porcelain','--untracked-files=all','--','APIShared/')).Count -eq 1) 'Expected only staged gitlink deletion'
        Check (-not (Test-Path -LiteralPath (Join-Path $parent '.gitmodules'))) 'Empty gitmodules retained'
        $common=(G $linked @('rev-parse','--path-format=absolute','--git-common-dir')) -join ''
        Check ([IO.Path]::GetFullPath($common) -eq (Join-Path $api '.git')) 'Linked worktree uses old metadata'
    } else {
        Check (Test-Path -LiteralPath (Join-Path $api '.git') -PathType Leaf) 'Rollback lost submodule pointer'
        Check (@(G $parent @('ls-files','--stage','--','APIShared')).Count -eq 1) 'Rollback lost gitlink'
    }
}
G $origin @('branch','dev/Serpens66')
$fresh=Join-Path $temp 'fresh';[void][IO.Directory]::CreateDirectory($fresh)
$env:GIT_CONFIG_COUNT='1';$env:GIT_CONFIG_KEY_0=('url.'+$origin.Replace('\','/')+'.insteadOf');$env:GIT_CONFIG_VALUE_0='https://github.com/SHCDE-APIShared/APIShared.git'
$setupArgs=@('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $PSScriptRoot 'Initialize-ApiShared.ps1'),'-Workspace',$fresh)
$setup=Invoke-ApiRepositoryProcess 'powershell.exe' $setupArgs
Check ($setup.ExitCode -eq 0) ('Explicit initialization failed: '+$setup.Diagnostic)
Check (((G (Join-Path $fresh 'APIShared') @('branch','--show-current')) -join '') -ceq 'dev/Serpens66') 'Initializer selected wrong branch'
Write-Fixture (Join-Path $fresh 'APIShared/file.txt') 'keep local'
$setup=Invoke-ApiRepositoryProcess 'powershell.exe' $setupArgs
Check ($setup.ExitCode -ne 0) 'Initializer replaced existing checkout'
Check ([IO.File]::ReadAllText((Join-Path $fresh 'APIShared/file.txt')) -ceq 'keep local') 'Initializer changed local files'
Write-Host "PASS: migration, rollback, unpublished commits, independent metadata, linked worktree, open/staged files and unrelated parent staging. Fixtures: $temp"
