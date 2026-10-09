# Integration fixtures exercise Git itself; package fixtures double only external files.
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ApiShared.Common.ps1')
$temp=Join-Path ([IO.Path]::GetTempPath()) ('ApiSharedWorkflow-'+[Guid]::NewGuid().ToString('N'))
$oldProtocol=$env:GIT_ALLOW_PROTOCOL
$env:GIT_ALLOW_PROTOCOL='file'
function Check([bool]$value,[string]$message) { if(-not$value){throw $message} }
function Write-Fixture([string]$Path,[string]$Text) { [IO.Directory]::CreateDirectory((Split-Path -Parent $Path)) | Out-Null; [IO.File]::WriteAllText($Path,$Text,[Text.UTF8Encoding]::new($false)) }
function Commit-Fixture([string]$Directory,[string]$Message) {
    Invoke-ApiGit $Directory @('add','-A') | Out-Null
    Invoke-ApiGit $Directory @('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','commit','-m',$Message) | Out-Null
}
function Run-Update([string]$Workspace,[bool]$Success) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Update-ApiShared.ps1') -Workspace $Workspace
    Check (($LASTEXITCODE -eq 0) -eq $Success) 'Unexpected updater result'
}
try {
    [IO.Directory]::CreateDirectory($temp) | Out-Null
    $origin=Join-Path $temp 'origin'; $author=Join-Path $temp 'author'; $workspace=Join-Path $temp 'workspace'
    & git init --bare --initial-branch=main $origin | Out-Null; if($LASTEXITCODE){throw 'Bare init failed'}
    & git clone $origin $author | Out-Null; if($LASTEXITCODE){throw 'Author clone failed'}
    Write-Fixture (Join-Path $author 'a.txt') "baseline`n"; Commit-Fixture $author baseline
    Invoke-ApiGit $author @('push','origin','main') | Out-Null
    & git init --initial-branch=main $workspace | Out-Null; if($LASTEXITCODE){throw 'Workspace init failed'}
    Invoke-ApiGit $workspace @('submodule','add',$origin,'APIShared') | Out-Null
    Commit-Fixture $workspace pin
    $api=Join-Path $workspace 'APIShared'
    Invoke-ApiGit $api @('config','user.name','Fixture') | Out-Null
    Invoke-ApiGit $api @('config','user.email','fixture@example.invalid') | Out-Null
    $pin=Get-ApiSubmodule $workspace
    Write-Fixture (Join-Path $author 'community.txt') "community`n"; Commit-Fixture $author community
    Invoke-ApiGit $author @('push','origin','main') | Out-Null
    Invoke-ApiGit $api @('checkout','--detach',$pin) | Out-Null
    Run-Update $workspace $true
    Check (Test-Path -LiteralPath (Join-Path $api 'community.txt')) 'Community change lost'
    Check ((Get-ApiSubmodule $workspace) -eq $pin) 'Updater changed the parent pointer'
    Check (((Invoke-ApiGit $api @('branch','--show-current')) -join '').StartsWith('codex/api-work')) 'Detached HEAD was not preserved on a work branch'
    Write-Fixture (Join-Path $api 'local.txt') "local`n"; Commit-Fixture $api local
    Write-Fixture (Join-Path $author 'remote.txt') "remote`n"; Commit-Fixture $author remote
    Invoke-ApiGit $author @('push','origin','main') | Out-Null
    Run-Update $workspace $true
    Check ((Test-Path (Join-Path $api 'local.txt')) -and (Test-Path (Join-Path $api 'remote.txt'))) 'Diverged changes were lost'
    Write-Fixture (Join-Path $api 'open.txt') 'open'
    $before=((Invoke-ApiGit $api @('rev-parse','HEAD')) -join '')
    Run-Update $workspace $false
    Check (((Invoke-ApiGit $api @('rev-parse','HEAD')) -join '') -eq $before) 'Open changes changed HEAD'
    Check ([IO.File]::ReadAllText((Join-Path $api 'open.txt')) -eq 'open') 'Open file was lost'
    Commit-Fixture $api preserve
    Write-Fixture (Join-Path $api 'a.txt') "local conflict`n"; Commit-Fixture $api local-conflict
    Write-Fixture (Join-Path $author 'a.txt') "remote conflict`n"; Commit-Fixture $author remote-conflict
    Invoke-ApiGit $author @('push','origin','main') | Out-Null
    Run-Update $workspace $false
    Check (@(Invoke-ApiGit $api @('diff','--name-only','--diff-filter=U')).Count -eq 1) 'Conflict not preserved'
    Run-Update $workspace $false
    Invoke-ApiGit $api @('merge','--abort') | Out-Null
    Check ([IO.File]::ReadAllText((Join-Path $api 'a.txt')).Trim() -eq 'local conflict') 'Abort lost the local commit'
    $fresh=Join-Path $temp 'fresh'
    & git clone $workspace $fresh | Out-Null; if($LASTEXITCODE){throw 'Fresh clone failed'}
    & git -C $fresh config user.name Fixture; & git -C $fresh config user.email fixture@example.invalid
    Run-Update $fresh $true
    Check (Test-Path (Join-Path $fresh 'APIShared/community.txt')) 'Uninitialized submodule did not initialize/update'

    & (Join-Path $PSScriptRoot '../../../APIShared/tools/Validation/Test-BuildProof.ps1')
    . (Join-Path $PSScriptRoot '../Release/Release.Common.ps1')
    . (Join-Path $PSScriptRoot '../Release/ReleaseStatus.Common.ps1')
    $fixtureConfig=[pscustomobject]@{Root=$workspace}
    $historical=Get-GitText -Config $fixtureConfig -Revision HEAD -Path 'APIShared/a.txt'
    Check ($historical -eq 'baseline') 'Gitlink source read did not use the recorded commit'
    $historyRoot=Join-Path $temp 'embedded-history'
    & git init --initial-branch=main $historyRoot | Out-Null
    Write-Fixture (Join-Path $historyRoot 'APIShared/a.txt') "baseline`n"
    Commit-Fixture $historyRoot embedded
    $embeddedCommit=((Invoke-ApiGit $historyRoot @('rev-parse','HEAD')) -join '')
    Invoke-ApiGit $historyRoot @('rm','APIShared/a.txt') | Out-Null
    Invoke-ApiGit $historyRoot @('submodule','add',$origin,'APIShared') | Out-Null
    Invoke-ApiGit (Join-Path $historyRoot 'APIShared') @('checkout','--detach',$pin) | Out-Null
    Commit-Fixture $historyRoot submodule
    $historyConfig=[pscustomobject]@{Root=$historyRoot}
    $changed=@(Get-ChangedRepositoryPaths -Config $historyConfig -BaseCommit $embeddedCommit -HeadCommit HEAD)
    Check ($changed -notcontains 'APIShared/a.txt') 'Identical embedded/submodule source was reported deleted'
    Check ((Get-FileDiffText -Config $historyConfig -BaseCommit $embeddedCommit -HeadCommit HEAD -Path 'APIShared/a.txt') -eq '') 'Identical source produced a migration diff'

    # Use real local repositories and evidence; double only the external public-ref query.
    $releaseRoot=Join-Path $temp 'release-workspace'
    & git init --initial-branch=main $releaseRoot | Out-Null
    Invoke-ApiGit $releaseRoot @('submodule','add',$origin,'APIShared') | Out-Null
    $releaseApi=Join-Path $releaseRoot 'APIShared'
    foreach($dir in @('src','Properties','Patches','tools')) { [IO.Directory]::CreateDirectory((Join-Path $releaseApi $dir)) | Out-Null }
    Write-Fixture (Join-Path $releaseApi 'APIShared.csproj') '<Project />'
    Write-Fixture (Join-Path $releaseApi 'src/example.cs') 'production input boundary'
    Write-Fixture (Join-Path $releaseApi '.gitignore') ".local/`nBepInEx/`n"
    Write-Fixture (Join-Path $releaseApi 'tools/Build.ps1') 'build boundary'
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../../../APIShared/tools/BuildProof.ps1') -Destination (Join-Path $releaseApi 'tools/BuildProof.ps1')
    Commit-Fixture $releaseApi release-inputs
    Commit-Fixture $releaseRoot pin
    $compiler=Join-Path $temp 'release-compiler'; Write-Fixture $compiler 'external compiler boundary'
    $dll=Join-Path $releaseApi 'BepInEx/plugins/APIShared_Serp/APIShared.dll'; Write-Fixture $dll 'external binary boundary'
    . (Join-Path $releaseApi 'tools/BuildProof.ps1')
    $state=Get-ApiInputState $releaseApi '' '' $compiler
    Write-ApiBuildProof $releaseApi '' '' $compiler $state
    $publishedTip=((Invoke-ApiGit $releaseApi @('rev-parse','HEAD')) -join '')
    $gitImplementation=(Get-Item Function:Invoke-ApiGit).ScriptBlock
    function Invoke-ApiGit([string]$Directory,[string[]]$Arguments) {
        if (($Arguments -join ' ') -eq 'remote get-url origin') { return 'https://github.com/SHCDE-APIShared/APIShared.git' }
        if (($Arguments -join ' ') -eq 'ls-remote --heads --tags origin') { return "$publishedTip`trefs/heads/main" }
        & $gitImplementation $Directory $Arguments
    }
    $null=Assert-ApiReleaseState $releaseRoot
    Write-Fixture (Join-Path $releaseApi 'unpublished.txt') 'local commit'
    Commit-Fixture $releaseApi unpublished
    $failed=$false; try { $null=Assert-ApiReleaseState $releaseRoot } catch { $failed=$true }
    Check $failed 'Different submodule pointer accepted for release'
    Commit-Fixture $releaseRoot pin-local
    Write-ApiBuildProof $releaseApi '' '' $compiler $state
    $failed=$false; try { $null=Assert-ApiReleaseState $releaseRoot } catch { $failed=$true }
    Check $failed 'Unpublished commit accepted for release'
    $publishedTip=((Invoke-ApiGit $releaseApi @('rev-parse','HEAD')) -join '')
    $null=Assert-ApiReleaseState $releaseRoot
    Write-Fixture (Join-Path $releaseApi 'BepInEx/plugins/APIShared_Serp/extra.dll') 'unexpected package file'
    $failed=$false; try { $null=Assert-ApiConsumerPackage $releaseRoot '' } catch { $failed=$true }
    Check $failed 'Unexpected package file accepted'
    Remove-Item -LiteralPath (Join-Path $releaseApi 'BepInEx/plugins/APIShared_Serp/extra.dll')
    Remove-Item -LiteralPath $dll
    $failed=$false; try { $null=Assert-ApiConsumerPackage $releaseRoot '' } catch { $failed=$true }
    Check $failed 'Missing package file accepted'
    Set-Item Function:Invoke-ApiGit $gitImplementation
    # Git normally overwrites ignored files when an incoming commit tracks the same path.
    # Local configuration must survive that collision, too.
    $collisionOrigin=Join-Path $temp 'collision-origin'
    $collisionAuthor=Join-Path $temp 'collision-author'
    $collisionRoot=Join-Path $temp 'collision-workspace'
    & git init --bare --initial-branch=main $collisionOrigin | Out-Null
    & git clone $collisionOrigin $collisionAuthor | Out-Null
    Write-Fixture (Join-Path $collisionAuthor '.gitignore') "private.cfg`n"
    Commit-Fixture $collisionAuthor baseline
    Invoke-ApiGit $collisionAuthor @('push','origin','main') | Out-Null
    & git init --initial-branch=main $collisionRoot | Out-Null
    Invoke-ApiGit $collisionRoot @('submodule','add',$collisionOrigin,'APIShared') | Out-Null
    Commit-Fixture $collisionRoot pin
    $collisionApi=Join-Path $collisionRoot 'APIShared'
    Write-Fixture (Join-Path $collisionApi 'private.cfg') 'user configuration'
    Write-Fixture (Join-Path $collisionAuthor 'private.cfg') 'incoming configuration'
    Invoke-ApiGit $collisionAuthor @('add','--force','private.cfg') | Out-Null
    Commit-Fixture $collisionAuthor incoming
    Invoke-ApiGit $collisionAuthor @('push','origin','main') | Out-Null
    Run-Update $collisionRoot $false
    Check ([IO.File]::ReadAllText((Join-Path $collisionApi 'private.cfg')) -ceq 'user configuration') 'Ignored local configuration overwritten'
    Write-Host 'PASS: initialized/fresh submodules, detached HEAD, divergent changes, open files, conflicts/abort, missing/stale inputs, package tampering, dirty release rejection, cross-shell fingerprint and gitlink source reads.'
} finally {
    $env:GIT_ALLOW_PROTOCOL=$oldProtocol
    # Keep failed fixture contents inspectable; successful fixtures are ordinary disposable test data.
    Write-Host "Git fixtures: $temp"
}
