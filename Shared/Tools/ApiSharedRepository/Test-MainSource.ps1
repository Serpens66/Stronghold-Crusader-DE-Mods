$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'MainSource.Common.ps1')
. (Join-Path $PSScriptRoot 'ApiShared.Common.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$fixture=Join-Path $workspace ('.inspect/main-workflow-'+[guid]::NewGuid().ToString('N'))
$savedSelection=$env:SHCDE_API_SHARED_MAIN_SELECTION;$savedProof=$env:SHCDE_API_SHARED_RELEASE_PROOF;$savedDirectory=$env:SHCDE_API_SHARED_DIR
$checks=0
function Check([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message};$script:checks++}
function Reject([scriptblock]$Action){$failed=$false;try{& $Action|Out-Null}catch{$failed=$true};Check $failed 'Expected rejection.'}
function Write-Fixture([string]$Path,[string]$Text){[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path));[IO.File]::WriteAllText($Path,([regex]::Replace($Text,'\r?\n',"`r`n")),[Text.UTF8Encoding]::new($false))}
function Commit-Fixture([string]$Root){Invoke-ApiRepositoryGit $Root @('add','-A')|Out-Null;Invoke-ApiRepositoryGit $Root @('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','-c','commit.gpgsign=false','commit','-m','fixture')|Out-Null}
$nativeProcess=(Get-Item Function:Invoke-ApiRepositoryProcess).ScriptBlock
# Only external repository identity/GitHub are doubled. Fetch, worktrees and merge-tree use real Git.
$shim=@'
$nativeFixtureProcess=(Get-Item Function:Invoke-ApiRepositoryProcess).ScriptBlock
function Invoke-ApiRepositoryProcess([string]$Executable,[string[]]$Arguments) {
    if($Executable -eq 'git' -and ($Arguments -join ' ') -match ' remote get-url origin$') {
        return [pscustomobject]@{ExitCode=0;Output="https://github.com/SHCDE-APIShared/APIShared.git`r`n";Diagnostic=''}
    }
    if($Executable -eq 'gh') {
        $output=if($env:APISHARED_PR_FIXTURE -eq 'existing'){'[{"url":"https://github.com/SHCDE-APIShared/APIShared/pull/123","mergeable":"MERGEABLE","mergeStateStatus":"CLEAN"}]'}else{'[]'}
        return [pscustomobject]@{ExitCode=0;Output=$output;Diagnostic=''}
    }
    & $nativeFixtureProcess $Executable $Arguments
}
'@
try {
    [void][IO.Directory]::CreateDirectory($fixture)
    $origin=Join-Path $fixture 'origin';$author=Join-Path $fixture 'author';$root=Join-Path $fixture 'workspace';$api=Join-Path $root 'APIShared'
    $null=Invoke-ApiRepositoryProcess 'git' @('init','--bare','--initial-branch=main',$origin)
    $clone=Invoke-ApiRepositoryProcess 'git' @('clone',$origin,$author);Check ($clone.ExitCode -eq 0) 'Fixture clone failed.'
    Write-Fixture (Join-Path $author '.gitignore') ".local/`r`nBepInEx/`r`n"
    Write-Fixture (Join-Path $author 'info.json') '{"GUID":"APIShared_Serp","Version":"1.0.0"}'
    Write-Fixture (Join-Path $author 'tools/WorkshopPackage.ps1') @'
function Assert-ApiWorkshopUnlinkedPath($Path) {}
function Assert-ApiWorkshopUnlinkedTree($Path) {}
function Get-WorkshopFileHash($Path) { Get-ApiRepositoryHash $Path }
'@
    Write-Fixture (Join-Path $author 'tools/BuildProof.ps1') @'
function Assert-ApiBuildProof($Root) {
    if(-not(Test-Path -LiteralPath (Join-Path $Root '.local/built'))){throw 'Missing build'}
    return @{Commit=((& git -C $Root rev-parse HEAD)-join '');Version='1.0.0'}
}
'@
    Write-Fixture (Join-Path $author 'tools/WorkshopUploader/PublisherBuildProof.ps1') @'
function Assert-WorkshopPublisherBuild($Root) {
    $proof=[IO.File]::ReadAllText((Join-Path $Root '.local/WorkshopUploader/build-proof.json'))|ConvertFrom-Json
    if((Get-ApiRepositoryHash (Join-Path $Root '.local/WorkshopUploader/WorkshopUploader.exe')) -cne $proof.Hash){throw 'Publisher tampered'}
}
'@
    Write-Fixture (Join-Path $author 'fixture-build.ps1') @'
$root=$PSScriptRoot
if($env:APISHARED_BUILD_FAIL -eq '1'){exit 1}
[void][IO.Directory]::CreateDirectory((Join-Path $root '.local/WorkshopUploader'))
[IO.File]::WriteAllText((Join-Path $root '.local/built'),'built')
$exe=Join-Path $root '.local/WorkshopUploader/WorkshopUploader.exe'
[IO.File]::WriteAllText($exe,'main publisher')
$stream=[IO.File]::OpenRead($exe);$sha=[Security.Cryptography.SHA256]::Create()
try{$hash=([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','').ToLowerInvariant()}finally{$stream.Dispose();$sha.Dispose()}
[IO.File]::WriteAllText((Join-Path $root '.local/WorkshopUploader/build-proof.json'),('{"Hash":"'+$hash+'","Outputs":[{"Name":"WorkshopUploader.exe","Sha256":"'+$hash+'"}]}'))
'@
    Write-Fixture (Join-Path $author 'build.bat') "@echo off`r`npowershell.exe -NoProfile -ExecutionPolicy Bypass -File `"%~dp0fixture-build.ps1`"`r`nexit /b %ERRORLEVEL%`r`n"
    Commit-Fixture $author
    Invoke-ApiRepositoryGit $author @('push','origin','main')|Out-Null
    Invoke-ApiRepositoryGit $author @('branch','develop')|Out-Null
    Invoke-ApiRepositoryGit $author @('push','origin','develop')|Out-Null
    Invoke-ApiRepositoryGit $author @('branch','dev/Serpens66')|Out-Null
    Invoke-ApiRepositoryGit $author @('push','origin','dev/Serpens66')|Out-Null
    [void][IO.Directory]::CreateDirectory($root)
    $clone=Invoke-ApiRepositoryProcess 'git' @('clone',$origin,$api);Check ($clone.ExitCode -eq 0) 'Workspace clone failed.'
    foreach($dir in @('Shared/Tools/ApiSharedRepository','Shared/Tools/Steam')){[void][IO.Directory]::CreateDirectory((Join-Path $root $dir))}
    foreach($file in @('RepositoryProcess.Common.ps1','MainSource.Common.ps1','Open-PullRequest.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination (Join-Path $root ('Shared/Tools/ApiSharedRepository/'+$file))}
    [IO.File]::AppendAllText((Join-Path $root 'Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1'),"`r`n"+$shim)
    foreach($bat in @('APIShared-PRCheck-develop.bat','APIShared-PRCheck-main.bat')){Copy-Item -LiteralPath (Join-Path $workspace $bat) -Destination (Join-Path $root $bat)}
    . ([scriptblock]::Create($shim))
    Invoke-ApiRepositoryGit $api @('switch','-c','dev/Serpens66')|Out-Null
    Write-Fixture (Join-Path $api 'info.json') '{"GUID":"APIShared_Serp","Version":"9.9.9"}'
    Commit-Fixture $api
    $devHead=(Invoke-ApiRepositoryGit $api @('rev-parse','HEAD'))-join ''
    Write-Fixture (Join-Path $api 'open.txt') 'unfinished development'
    $env:SHCDE_API_SHARED_MAIN_SELECTION=Resolve-ApiMainSelection $root
    $selected=Assert-ApiMainSelection $root $env:SHCDE_API_SHARED_MAIN_SELECTION
    Check (([IO.File]::ReadAllText((Join-Path $selected.Root 'info.json'))|ConvertFrom-Json).Version -ceq '1.0.0') 'Development manifest entered main selection.'
    Check (((Invoke-ApiRepositoryGit $api @('rev-parse','HEAD'))-join '') -ceq $devHead) 'Selection moved dev HEAD.'
    Check (((Invoke-ApiRepositoryGit $api @('branch','--show-current'))-join '') -ceq 'dev/Serpens66') 'Selection switched dev branch.'
    Check ([IO.File]::ReadAllText((Join-Path $api 'open.txt')) -ceq 'unfinished development') 'Selection changed open file.'
    Ensure-ApiMainBuild $root
    $cache=Join-Path $root '.release-output/APIShared/cache/v1.0.0'
    $sdk=Join-Path $cache 'audit/BepInEx/plugins/APIShared_Serp'
    [void][IO.Directory]::CreateDirectory($sdk)
    $dll=Join-Path $sdk 'APIShared.dll'
    Add-Type -TypeDefinition 'namespace APIShared { public class MainOnly {} }' -OutputAssembly $dll
    Write-Fixture (Join-Path $sdk 'info.json') '{"GUID":"APIShared_Serp","Version":"1.0.0"}'
    $archive=Join-Path $cache 'APIShared-v1.0.0.zip';Write-Fixture $archive 'archive boundary'
    $records=@(foreach($file in Get-ChildItem -LiteralPath $sdk -File){@{Path='BepInEx/plugins/APIShared_Serp/'+$file.Name;Size=$file.Length;Sha256=(Get-ApiRepositoryHash $file.FullName)}})
    $sdkProof=@{SchemaVersion=1;Mod='APIShared';PluginGuid='APIShared_Serp';Version='1.0.0';Tag='APIShared/v1.0.0';Commit=$selected.Commit;Package=@{Sha256=(Get-ApiRepositoryHash $archive);Files=$records}}
    $env:SHCDE_API_SHARED_RELEASE_PROOF=Join-Path $cache 'APIShared-v1.0.0.provenance.json'
    $env:SHCDE_API_SHARED_DIR=$sdk
    Write-Fixture $env:SHCDE_API_SHARED_RELEASE_PROOF (ConvertTo-Json -InputObject $sdkProof -Depth 8)
    $verified=Assert-ApiConsumerPackage $root $sdk
    Check ($verified.Version -ceq '1.0.0' -and $verified.Commit -ceq $selected.Commit) 'Dev manifest or source entered consumer proof.'
    Add-Type -TypeDefinition 'public class StableConsumer { public APIShared.MainOnly Value; }' -ReferencedAssemblies $dll -OutputAssembly (Join-Path $fixture 'consumer.dll')
    Reject {Add-Type -TypeDefinition 'public class NeedsUnreleasedApi { public APIShared.DevOnly Value; }' -ReferencedAssemblies $dll -OutputAssembly (Join-Path $fixture 'invalid-consumer.dll') -ErrorAction Stop}
    $env:SHCDE_API_SHARED_RELEASE_PROOF='';$env:SHCDE_API_SHARED_DIR=''
    $delivery=Get-ApiMainDeliveryIdentity $root
    $null=Assert-ApiMainDeliveryIdentity $root $delivery
    Check (-not(Test-Path -LiteralPath (Join-Path $api '.local/built'))) 'main build wrote to development output.'
    $mainPublisher=Join-Path $selected.Root '.local/WorkshopUploader/WorkshopUploader.exe'
    [IO.File]::WriteAllText($mainPublisher,'tampered');Reject {Assert-ApiMainDeliveryIdentity $root $delivery}
    [IO.File]::WriteAllText($mainPublisher,'main publisher')
    Write-Fixture (Join-Path $selected.Root 'unexpected.txt') 'preserve me'
    Reject {Resolve-ApiMainSelection $root}
    Check (Test-Path -LiteralPath (Join-Path $selected.Root 'unexpected.txt')) 'Changed worktree was overwritten.'
    [IO.File]::Delete((Join-Path $selected.Root 'unexpected.txt'))
    Invoke-ApiRepositoryGit $author @('switch','main')|Out-Null
    Write-Fixture (Join-Path $author 'next-main.txt') 'new main'
    Commit-Fixture $author;Invoke-ApiRepositoryGit $author @('push','origin','main')|Out-Null
    $later=Resolve-ApiMainSelection $root
    Check ($later -cne $delivery.Selection) 'New preparation reused old main.'
    $old=Assert-ApiMainDeliveryIdentity $root $delivery
    Check ($old -ceq $delivery.Selection) 'Upload changed to newer main.'
    $env:SHCDE_API_SHARED_MAIN_SELECTION=''
    $prCheck=Invoke-ApiRepositoryProcess 'powershell.exe' @('-NoProfile','-ExecutionPolicy','Bypass','-File',(Join-Path $root 'Shared/Tools/ApiSharedRepository/Open-PullRequest.ps1'),'-Target','Develop','-NoOpen','-NoPause')
    Check ($prCheck.ExitCode -ne 0 -and $prCheck.Output -match 'Push/pull') 'PR helper accepted unpublished local commits.'
    Invoke-ApiRepositoryGit $api @('push','origin','dev/Serpens66')|Out-Null
    $prLog=Join-Path $fixture 'pr-bat.log'
    & (Join-Path $root 'APIShared-PRCheck-develop.bat') -NoOpen -NoPause *> $prLog
    Check ($LASTEXITCODE -eq 0 -and [IO.File]::ReadAllText($prLog) -match 'compare/develop\.\.\.dev%2FSerpens66') 'PR BAT used wrong comparison.'
    $env:APISHARED_PR_FIXTURE='existing'
    & (Join-Path $root 'APIShared-PRCheck-develop.bat') -NoOpen -NoPause *> $prLog
    Check ($LASTEXITCODE -eq 0 -and [IO.File]::ReadAllText($prLog) -match '/pull/123') 'Existing PR was not opened.'
    $env:APISHARED_PR_FIXTURE=''
    Invoke-ApiRepositoryGit $author @('switch','develop')|Out-Null
    Write-Fixture (Join-Path $author 'info.json') '{"GUID":"APIShared_Serp","Version":"2.0.0"}'
    Commit-Fixture $author;Invoke-ApiRepositoryGit $author @('push','origin','develop')|Out-Null
    & (Join-Path $root 'APIShared-PRCheck-develop.bat') -NoOpen -NoPause *> $prLog
    Check ($LASTEXITCODE -eq 0 -and [IO.File]::ReadAllText($prLog) -match 'Conflicts require manual') 'Conflicts were hidden.'
    & (Join-Path $root 'APIShared-PRCheck-main.bat') -NoOpen -NoPause *> $prLog
    Check ($LASTEXITCODE -eq 0 -and [IO.File]::ReadAllText($prLog) -match 'compare/main\.\.\.develop') 'Release PR BAT used wrong direction.'
    Check (((Invoke-ApiRepositoryGit $api @('rev-parse','HEAD'))-join '') -ceq $devHead) 'PR helper changed personal commits.'
    Check ([IO.File]::ReadAllText((Join-Path $api 'open.txt')) -ceq 'unfinished development') 'PR helper changed open file.'
    # Exercise both original BAT entrypoints with real policy/worktrees and inert target services.
    $steam=Join-Path $root 'Shared/Tools/Steam'
    foreach($file in @('ModpackSource.Common.ps1','WorkshopWorkflow.ps1','WorkshopWorkflow.Common.ps1','WorkshopOutputRetention.ps1','WorkshopOptions.Common.ps1','SteamWorkshopHistory.ps1')) {
        Copy-Item -LiteralPath (Join-Path $workspace ('Shared/Tools/Steam/'+$file)) -Destination (Join-Path $steam $file)
    }
    foreach($file in @('CreateSteamModPack.bat','UploadWorkshop.bat')){Copy-Item -LiteralPath (Join-Path $workspace $file) -Destination (Join-Path $root $file)}
    Write-Fixture (Join-Path $steam 'WorkshopConfiguration.ps1') @'
function Get-WorkshopConfiguration {
    $root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
    return [pscustomobject]@{Root=$root;AppId=3024040;APIShared=[pscustomobject]@{Key='APIShared';Guid='APIShared_Serp';ItemId='111';Folder=(Join-Path $root 'APISharedWorkshop')};SerpsMods=[pscustomobject]@{Key='SerpsMods';Guid='SerpsMods_Serp';ItemId='3788821961';Folder=(Join-Path $root 'SerpsMods')}}
}
'@
    $prepareService=@'
param([int]$TestMode,[int]$ValidateOnly,[string]$Target)
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
$config=Get-WorkshopConfiguration;$selected=Assert-ApiMainSelection $config.Root $env:SHCDE_API_SHARED_MAIN_SELECTION
Write-Host ("FIXTURE_MAIN="+$selected.Commit)
if($ValidateOnly){exit 0}
if($env:APISHARED_RETENTION_FIXTURE_FAIL -ceq 'child'){exit 17}
$folder=Get-WorkshopFolder $config $config.$Target ($TestMode -eq 1)
$stage=Join-Path $config.Root ('.release-output/fixture-stage/'+$Target)
[void][IO.Directory]::CreateDirectory($stage)
if($Target -ceq 'APIShared'){
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $stream=[IO.FileStream]::new((Join-Path $stage 'APIShared.map'),[IO.FileMode]::Create)
    $zip=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
    try{
        foreach($entry in @(@('info.json','{"GUID":"APIShared_Serp","Version":"1.0.0","Manifest":1}'),@('fixture.txt',$selected.Commit))){
            $writer=[IO.StreamWriter]::new($zip.CreateEntry($entry[0]).Open());try{$writer.Write($entry[1])}finally{$writer.Dispose()}
        }
    }finally{$zip.Dispose();$stream.Dispose()}
    [IO.File]::WriteAllText((Join-Path $stage 'preview.png'),'fixture preview')
}else{[IO.File]::WriteAllText((Join-Path $stage 'payload.map'),$selected.Commit)}
Publish-WorkshopFolder $stage $folder $config.Root
if($env:APISHARED_RETENTION_FIXTURE_FAIL -ceq 'proof'){exit 18}
$null=Write-WorkshopPreparation $config $config.$Target ($TestMode -eq 1) '1.0.0' $selected.Commit 'fixture'
exit 0
'@
    # SerpsMods preparation's real provenance layout remains covered in the existing suite.
    $prepareService=$prepareService.Replace("`$null=Write-WorkshopPreparation", "if(`$Target -eq 'SerpsMods'){`$reference=Join-Path (Get-WorkshopStateRoot `$config (`$TestMode -eq 1)) 'references/SerpsMods.provenance.json';[void][IO.Directory]::CreateDirectory((Split-Path -Parent `$reference));[IO.File]::WriteAllText(`$reference,'{}')};`$null=Write-WorkshopPreparation")
    foreach($service in @('Prepare-APISharedWorkshop.ps1','Prepare-ModpackWorkflow.ps1')){Write-Fixture (Join-Path $steam $service) $prepareService}
    Write-Fixture (Join-Path $steam 'Upload-WorkshopTarget.ps1') @'
param([int]$TestMode,[int]$ValidateOnly,[string]$Target)
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
$config=Get-WorkshopConfiguration
$proof=Assert-WorkshopPreparation $config $config.$Target ($TestMode -eq 1)
Write-Host ('FIXTURE_UPLOAD_MAIN='+$proof.MainSource.Commit)
exit 0
'@
    $batLog=Join-Path $fixture 'workshop-bat.log'
    & (Join-Path $root 'CreateSteamModPack.bat') /apishared /nopause *> $batLog
    Check ($LASTEXITCODE -eq 0) ('Prepare BAT failed: '+[IO.File]::ReadAllText($batLog))
    foreach($generation in 1..3){
        & (Join-Path $root 'CreateSteamModPack.bat') /apishared /nopause *> $batLog
        Check ($LASTEXITCODE -eq 0) ('Repeated prepare BAT failed: '+[IO.File]::ReadAllText($batLog))
        Check (@(Get-ChildItem -LiteralPath $root -Directory -Filter 'APISharedWorkshop.previous-*').Count -eq 1) 'Real BAT path did not bound predecessor folders.'
    }
    $backupsBefore=@(Get-ChildItem -LiteralPath $root -Directory -Filter 'APISharedWorkshop.previous-*')
    & (Join-Path $root 'CreateSteamModPack.bat') /apishared /validate /nopause *> $batLog
    Check ($LASTEXITCODE -eq 0 -and @(Get-ChildItem -LiteralPath $root -Directory -Filter 'APISharedWorkshop.previous-*').Count -eq $backupsBefore.Count) 'Validate-only preparation changed backups.'
    $env:APISHARED_RETENTION_FIXTURE_FAIL='child'
    & (Join-Path $root 'CreateSteamModPack.bat') /apishared /nopause *> $batLog
    Check ($LASTEXITCODE -ne 0 -and @(Get-ChildItem -LiteralPath $root -Directory -Filter 'APISharedWorkshop.previous-*').Count -eq $backupsBefore.Count) 'Failed child pruned backups.'
    $env:APISHARED_RETENTION_FIXTURE_FAIL='proof'
    & (Join-Path $root 'CreateSteamModPack.bat') /apishared /nopause *> $batLog
    Check ($LASTEXITCODE -ne 0 -and @(Get-ChildItem -LiteralPath $root -Directory -Filter 'APISharedWorkshop.previous-*').Count -eq ($backupsBefore.Count+1)) 'Failed proof write pruned backups.'
    $env:APISHARED_RETENTION_FIXTURE_FAIL=''
    & (Join-Path $root 'CreateSteamModPack.bat') /apishared /nopause *> $batLog
    Check ($LASTEXITCODE -eq 0 -and @(Get-ChildItem -LiteralPath $root -Directory -Filter 'APISharedWorkshop.previous-*').Count -eq 1) 'Successful BAT retry did not prune deferred backups.'
    $productionProof=Join-Path $root '.release-output/SteamWorkshop/prepared/APIShared.json'
    $production=[IO.File]::ReadAllText($productionProof)|ConvertFrom-Json
    Invoke-ApiRepositoryGit $author @('switch','main')|Out-Null
    Write-Fixture (Join-Path $author 'after-preparation.txt') 'new public main after preparation'
    Commit-Fixture $author;Invoke-ApiRepositoryGit $author @('push','origin','main')|Out-Null
    & (Join-Path $root 'UploadWorkshop.bat') /apishared /validate /nopause *> $batLog
    Check ($LASTEXITCODE -eq 0 -and [IO.File]::ReadAllText($batLog) -match $production.MainSource.Commit) 'Upload changed the prepared main commit.'
    $testItems=Join-Path $root '.release-output/SteamWorkshop/test/items'
    Write-Fixture (Join-Path $testItems '3024040-APIShared.item-id') '222'
    Write-Fixture (Join-Path $testItems '3024040-SerpsMods.item-id') '333'
    & (Join-Path $root 'CreateSteamModPack.bat') /apishared /test /nopause *> $batLog
    Check ($LASTEXITCODE -eq 0) ('Test prepare BAT failed: '+[IO.File]::ReadAllText($batLog))
    & (Join-Path $root 'UploadWorkshop.bat') /apishared /test /validate /nopause *> $batLog
    Check ($LASTEXITCODE -eq 0) 'Test upload BAT failed.'
    [IO.File]::AppendAllText((Join-Path $root 'APISharedWorkshop/APIShared.map'),'tampered')
    $tamperRejected=$false
    try {& (Join-Path $root 'UploadWorkshop.bat') /apishared /validate /nopause *> $batLog;$tamperRejected=$LASTEXITCODE -ne 0}
    catch {$tamperRejected=$_.Exception.Message -match 'Prepared file changed'}
    Check $tamperRejected 'Upload BAT accepted modified preparation.'
    Check (((Invoke-ApiRepositoryGit $api @('rev-parse','HEAD'))-join '') -ceq $devHead) 'Workshop BATs changed development HEAD.'
    Check ([IO.File]::ReadAllText((Join-Path $api 'open.txt')) -ceq 'unfinished development') 'Workshop BATs changed development files.'
    $env:SHCDE_API_SHARED_MAIN_SELECTION=$delivery.Selection
    [IO.File]::Delete((Join-Path $selected.Root '.local/built'))
    $env:APISHARED_BUILD_FAIL='1'
    Reject {Ensure-ApiMainBuild $root}
    Check (((Invoke-ApiRepositoryGit $api @('rev-parse','HEAD'))-join '') -ceq $devHead) 'Failed build moved development HEAD.'
    $env:APISHARED_BUILD_FAIL=''
    Write-Fixture (Join-Path $selected.Root '.local/built') 'built'
    Write-Fixture (Join-Path $selected.Root '.local/WorkshopUploader/extra.dll') 'unexpected library'
    Reject {Get-ApiMainDeliveryIdentity $root}
    [IO.File]::Delete((Join-Path $selected.Root '.local/WorkshopUploader/extra.dll'))
    Write-Host "PASS: $checks pinned-main, changed-worktree, publisher, preserved Dev, PR/conflict and real BAT checks. Fixtures: $fixture"
} finally {
    $env:SHCDE_API_SHARED_MAIN_SELECTION=$savedSelection;$env:SHCDE_API_SHARED_RELEASE_PROOF=$savedProof;$env:SHCDE_API_SHARED_DIR=$savedDirectory;$env:APISHARED_PR_FIXTURE='';$env:APISHARED_BUILD_FAIL='';$env:APISHARED_RETENTION_FIXTURE_FAIL=''
}
