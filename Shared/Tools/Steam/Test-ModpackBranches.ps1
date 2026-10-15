$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ModpackSource.Common.ps1')
. (Join-Path $PSScriptRoot 'WorkshopOptions.Common.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$fixture=Join-Path $workspace ('.inspect/modpack-branches-'+[guid]::NewGuid().ToString('N'))
$root=Join-Path $fixture 'author';$caller=Join-Path $fixture 'caller'
$checks=0
function Check($Ok,$Message){if(-not $Ok){throw $Message};$script:checks++}
function Reject([scriptblock]$Action){$failed=$false;try{& $Action|Out-Null}catch{$failed=$true};Check $failed 'Expected rejection'}
function Put($Relative,$Text){$p=Join-Path $root $Relative;[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($p));[IO.File]::WriteAllText($p,([regex]::Replace($Text,'\r?\n',"`r`n")),[Text.UTF8Encoding]::new($false))}
function Git([string[]]$Arguments){Invoke-ApiRepositoryGit $root $Arguments}
Check ((Get-ModpackBranchChoice '' '1') -ceq 'main') 'Main menu choice'
Check ((Get-ModpackBranchChoice '' '2') -ceq 'develop') 'Develop menu choice'
Check ((Get-ModpackBranchChoice '' '0') -ceq 'Cancel') 'Cancel menu choice'
Reject {Resolve-WorkshopOptions @('/modpack','/branch:main','/branch:develop')}
Reject {Resolve-WorkshopOptions @('/apishared','/branch:main')}
Reject {Resolve-WorkshopOptions @('/modpack','/branch:invalid')}
[void][IO.Directory]::CreateDirectory($root)
$init=Invoke-ApiRepositoryProcess 'git' @('init','--initial-branch=main',$root);Check ($init.ExitCode -eq 0) 'Init failed'
foreach($relative in @('Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1','Shared/Tools/ApiSharedRepository/MainSource.Common.ps1',
 'Shared/Tools/Steam/ModpackSource.Common.ps1','Shared/Tools/Steam/WorkshopOptions.Common.ps1','Shared/Tools/Steam/WorkshopWorkflow.ps1',
 'Shared/Tools/Steam/WorkshopWorkflow.Common.ps1','Shared/Tools/Steam/WorkshopOutputRetention.ps1','Shared/Tools/Steam/SteamWorkshopHistory.ps1',
 'CreateSteamModPack.bat','UploadWorkshop.bat')){Put $relative ([IO.File]::ReadAllText((Join-Path $workspace $relative)))}
Put 'SerpsModsHost/BepInEx/plugins/SerpsMods_Serp/SerpsModsHost.dll' 'original fixture build'
Put '.gitignore' ".release-output/`r`nAPIShared/`r`nSerpsMods/`r`nAPISharedWorkshop/`r`n*.previous-*/`r`n"
# External SDK/publisher services are inert fixtures; routing, policy, proofs, Git, BAT and PS children are real.
$apiHelper=Join-Path $root 'Shared/Tools/ApiSharedRepository/MainSource.Common.ps1'
[IO.File]::AppendAllText($apiHelper, @'

function Resolve-ApiMainSelection($Workspace){return 'fixture-published-sdk'}
function Assert-ApiMainSelection($Workspace,$Path){return @{Root=(Get-WorkshopApiRoot $Workspace);Commit='c7b210f007035ff8edaf4d359a21f7bfbcbe479d'}}
function Ensure-ApiMainBuild($Workspace){if($env:SHCDE_API_SHARED_PUBLISHED_092 -cne '1'){throw 'Missing published SDK pin'}}
function Get-WorkshopApiRoot($Workspace){$common=((Invoke-ApiRepositoryGit $Workspace @('rev-parse','--path-format=absolute','--git-common-dir'))-join '').Trim();return (Join-Path (Split-Path -Parent $common) 'APIShared')}
function Get-ApiMainDeliveryIdentity($Workspace){return @{Commit='c7b210f007035ff8edaf4d359a21f7bfbcbe479d';Selection='fixture-published-sdk'}}
function Assert-ApiMainDeliveryIdentity($Workspace,$Identity){if($Identity.Commit -cne 'c7b210f007035ff8edaf4d359a21f7bfbcbe479d'){throw 'SDK changed'};return 'fixture-published-sdk'}
'@)
Put 'APIShared/tools/WorkshopPackage.ps1' @'
function Assert-ApiWorkshopUnlinkedPath($Path){}
function Assert-ApiWorkshopUnlinkedTree($Path){}
function Get-WorkshopFileHash($Path){Get-ApiRepositoryHash $Path}
'@
Put 'APIShared/tools/WorkshopUploader/PublisherBuildProof.ps1' 'function Assert-WorkshopPublisherBuild($Root){}'
Put 'Shared/Tools/Steam/WorkshopConfiguration.ps1' @'
function Get-WorkshopConfiguration {
 $root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
 return [pscustomobject]@{Root=$root;AppId=3024040;APIShared=[pscustomobject]@{Key='APIShared';Guid='APIShared_Serp';ItemId='111';Folder=(Join-Path $root 'APISharedWorkshop')};SerpsMods=[pscustomobject]@{Key='SerpsMods';Guid='SerpsMods_Serp';ItemId='3788821961';Folder=(Join-Path $root 'SerpsMods')}}
}
'@
$prepare=@'
param([int]$TestMode,[int]$ValidateOnly,[string]$Target)
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
$config=Get-WorkshopConfiguration;$test=$TestMode -eq 1
$id=Get-WorkshopItemId $config $config.$Target $test
if($test -and $id -eq '3788821961'){throw 'Public ID leaked into test'}
Write-Host ('FIXTURE_SOURCE='+$config.Root+';branch='+$env:SHCDE_MODPACK_BRANCH+';test='+$TestMode+';id='+$id)
if($ValidateOnly){exit 0}
if($Target -eq 'SerpsMods' -and $env:MODPACK_FIXTURE_HOST_BUILD -eq '1'){
 [IO.File]::WriteAllText((Join-Path $config.Root 'SerpsModsHost/BepInEx/plugins/SerpsMods_Serp/SerpsModsHost.dll'),'built '+$env:SHCDE_MODPACK_BRANCH)
}
$folder=Get-WorkshopFolder $config $config.$Target $test
$stage=Join-Path $config.Root ('.release-output/fixture-'+$Target)
[void][IO.Directory]::CreateDirectory($stage)
[IO.File]::WriteAllText((Join-Path $stage 'payload.txt'),$env:SHCDE_MODPACK_BRANCH)
Publish-WorkshopFolder $stage $folder $config.Root
$commit=((Invoke-ApiRepositoryGit $config.Root @('rev-parse','HEAD'))-join '').Trim()
if($Target -eq 'SerpsMods'){
 $reference=Join-Path (Get-WorkshopStateRoot $config $test) 'references/SerpsMods.provenance.json'
 [void][IO.Directory]::CreateDirectory((Split-Path -Parent $reference));[IO.File]::WriteAllText($reference,'{"Commit":"'+$commit+'"}')
}
$null=Write-WorkshopPreparation $config $config.$Target $test '1.0.28' $commit 'fixture'
'@
foreach($service in @('Prepare-ModpackWorkflow.ps1','Prepare-APISharedWorkshop.ps1')){Put ('Shared/Tools/Steam/'+$service) $prepare}
Put 'Shared/Tools/Steam/Upload-WorkshopTarget.ps1' @'
param([int]$TestMode,[int]$ValidateOnly,[string]$Target)
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
$config=Get-WorkshopConfiguration
$proof=Assert-WorkshopPreparation $config $config.$Target ($TestMode -eq 1)
Write-Host ('FIXTURE_UPLOAD='+$config.Root+';commit='+$proof.Commit+';test='+$TestMode)
'@
Git @('add','-A')|Out-Null
Git @('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','-c','commit.gpgsign=false','commit','-m','fixture')|Out-Null
$cleanSource=Get-ModpackPreparationSource $root 'main' (((Git @('rev-parse','HEAD'))-join '').Trim())
Assert-ModpackPreparedOutputs $root $cleanSource.GeneratedOutputs
Check (@($cleanSource.GeneratedOutputs).Count -eq 0) 'Unchanged build outputs must produce and accept an empty snapshot'
Git @('branch','develop')|Out-Null
Git @('branch','caller')|Out-Null
Git @('worktree','add',$caller,'caller')|Out-Null
$source=Resolve-ModpackSource $caller 'develop'
Check ($source.Branch -ceq 'develop' -and $source.Root -ine $caller) 'Develop isolated worktree'
Check (((Invoke-ApiRepositoryGit $caller @('branch','--show-current'))-join '').Trim() -ceq 'caller') 'Caller branch changed'
Put '.release-output/SteamWorkshop/test/items/3024040-APIShared.item-id' '222'
Put '.release-output/SteamWorkshop/test/items/3024040-SerpsMods.item-id' '333'
$log=Join-Path $fixture 'bat.log'
$env:MODPACK_FIXTURE_HOST_BUILD='1'
& (Join-Path $caller 'CreateSteamModPack.bat') /modpack /branch:main /nopause *> $log
Check ($LASTEXITCODE -eq 0) ('Main preparation BAT: '+[IO.File]::ReadAllText($log))
$mainOutput=Join-Path $root 'SerpsModsHost/BepInEx/plugins/SerpsMods_Serp/SerpsModsHost.dll'
$mainProof=Join-Path $root '.release-output/SteamWorkshop/prepared/SerpsMods.json'
$before=Get-ApiRepositoryHash $mainProof
& (Join-Path $caller 'UploadWorkshop.bat') /modpack /validate /nopause *> $log
Check ($LASTEXITCODE -eq 0 -and (Get-ApiRepositoryHash $mainProof) -ceq $before) ('Cross-checkout upload: '+[IO.File]::ReadAllText($log))
[IO.File]::WriteAllText($mainOutput,'original fixture build')
& (Join-Path $caller 'CreateSteamModPack.bat') /modpack /branch:main /test /nopause *> $log
Check ($LASTEXITCODE -eq 0) ('Explicit private main BAT: '+[IO.File]::ReadAllText($log))
$privateMain=[IO.File]::ReadAllText((Join-Path $root '.release-output/SteamWorkshop/test/prepared/SerpsMods.json'))|ConvertFrom-Json
Check ($privateMain.Mode -ceq 'Test' -and $privateMain.ModpackSource.Branch -ceq 'main') 'Private main selection lost'
[IO.File]::WriteAllText($mainOutput,'original fixture build')
# Exercise stdin through the actual BAT entrypoint, including cancellation before any SDK action.
function Invoke-MenuBat([string]$Selection){
 $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$env:ComSpec
 $start.Arguments='/d /s /c ""'+(Join-Path $caller 'CreateSteamModPack.bat')+'" /modpack /validate /nopause"'
 $start.UseShellExecute=$false;$start.CreateNoWindow=$true
 $start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
 $process=[Diagnostics.Process]::new();$process.StartInfo=$start
 try{
  [void]$process.Start();$output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
  $process.StandardInput.WriteLine($Selection);$process.StandardInput.Close();$process.WaitForExit()
  return @{ExitCode=$process.ExitCode;Output=$output.Result;Errors=$errors.Result}
 }finally{$process.Dispose()}
}
$menu=Invoke-MenuBat '0';Check ($menu.ExitCode -eq 0 -and $menu.Output -match 'Modpack-Branch' -and $menu.Output -notmatch 'FIXTURE_SOURCE=') 'BAT menu cancellation failed'
$menu=Invoke-MenuBat '1';Check ($menu.ExitCode -eq 0 -and $menu.Output -match 'branch=main;test=0') ('BAT main menu failed: '+$menu.Output+$menu.Errors)
$menu=Invoke-MenuBat '2';Check ($menu.ExitCode -eq 0 -and $menu.Output -match 'branch=develop;test=1') ('BAT develop menu failed: '+$menu.Output+$menu.Errors)
& (Join-Path $caller 'CreateSteamModPack.bat') /both /branch:develop /nopause *> $log
Check ($LASTEXITCODE -eq 0) ('Develop test preparation BAT: '+[IO.File]::ReadAllText($log))
$testProof=Join-Path $root '.release-output/SteamWorkshop/test/prepared/SerpsMods.json'
$p=[IO.File]::ReadAllText($testProof)|ConvertFrom-Json
Check ($p.Mode -ceq 'Test' -and $p.ModpackSource.Branch -ceq 'develop') 'Develop did not force private mode'
$before=Get-ApiRepositoryHash $testProof
& (Join-Path $caller 'UploadWorkshop.bat') /modpack /test /validate /nopause *> $log
Check ($LASTEXITCODE -eq 0 -and (Get-ApiRepositoryHash $testProof) -ceq $before) ('Private cross-checkout upload: '+[IO.File]::ReadAllText($log))
$ErrorActionPreference='Continue' # Expected BAT failures may write native stderr in Windows PowerShell 5.1.
& (Join-Path $caller 'UploadWorkshop.bat') /modpack /branch:main /test /validate /nopause *> $log
Check ($LASTEXITCODE -ne 0) 'Conflicting prepared branch was accepted'
$generated=Join-Path $source.Root 'SerpsModsHost/BepInEx/plugins/SerpsMods_Serp/SerpsModsHost.dll'
$generatedBytes=[IO.File]::ReadAllText($generated);[IO.File]::WriteAllText($generated,'changed generated build')
& (Join-Path $caller 'UploadWorkshop.bat') /modpack /test /validate /nopause *> $log
Check ($LASTEXITCODE -ne 0 -and (Get-ApiRepositoryHash $testProof) -ceq $before) 'Upload accepted changed generated output or rewrote provenance'
[IO.File]::WriteAllText($generated,$generatedBytes)
$payload=Join-Path $source.Root '.release-output/SteamWorkshop/test/packages/SerpsMods/payload.txt'
$originalPayload=[IO.File]::ReadAllText($payload);[IO.File]::WriteAllText($payload,'tampered')
& (Join-Path $caller 'UploadWorkshop.bat') /modpack /test /validate /nopause *> $log
Check ($LASTEXITCODE -ne 0 -and (Get-ApiRepositoryHash $testProof) -ceq $before) 'Changed payload accepted or proof rewritten on upload'
[IO.File]::WriteAllText($payload,$originalPayload)
[IO.File]::WriteAllText($generated,'original fixture build')
Put '.release-output/SteamWorkshop/test/items/3024040-SerpsMods.item-id' '3788821961'
& (Join-Path $caller 'CreateSteamModPack.bat') /modpack /branch:develop /validate /nopause *> $log
Check ($LASTEXITCODE -ne 0) 'Public Workshop ID accepted in develop test'
Put '.release-output/SteamWorkshop/test/items/3024040-SerpsMods.item-id' '333'
[IO.File]::AppendAllText((Join-Path $source.Root '.gitignore'),"`r`nchanged")
& (Join-Path $caller 'CreateSteamModPack.bat') /modpack /branch:develop /validate /nopause *> $log
Check ($LASTEXITCODE -ne 0) 'Dirty selected source was accepted'
Check (((Invoke-ApiRepositoryGit $caller @('branch','--show-current'))-join '').Trim() -ceq 'caller') 'Caller branch was switched'
$env:MODPACK_FIXTURE_HOST_BUILD=''
Write-Host "PASS: $checks modpack branch/menu/worktree/proof/ID checks with real BAT and PowerShell children. Fixtures: $fixture"
