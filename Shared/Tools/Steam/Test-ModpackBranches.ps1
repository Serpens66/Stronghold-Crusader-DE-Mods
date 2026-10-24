$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ModpackSource.Common.ps1')
. (Join-Path $PSScriptRoot 'WorkshopOptions.Common.ps1')
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
$fixture = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -Kind t -Name 'ModpackBranches'
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
foreach($relative in @('Shared/Tools/Paths/WorkPaths.ps1','Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1','Shared/Tools/ApiSharedRepository/MainSource.Common.ps1',
 'Shared/Tools/Steam/ModpackSource.Common.ps1','Shared/Tools/Steam/WorkshopOptions.Common.ps1','Shared/Tools/Steam/WorkshopWorkflow.ps1',
 'Shared/Tools/Steam/WorkshopWorkflow.Common.ps1','Shared/Tools/Steam/WorkshopOutputRetention.ps1','Shared/Tools/Steam/SteamWorkshopHistory.ps1',
 'Shared/Tools/Steam/PublishedModpack.Common.ps1','Shared/Tools/Steam/ReleaseAssetCache.ps1','Shared/Tools/Steam/ApiSharedSdkPolicy.ps1',
 'CreateSteamModPack.bat','UploadWorkshop.bat')){Put $relative ([IO.File]::ReadAllText((Join-Path $workspace $relative)))}
Put 'SerpsModsHost/BepInEx/plugins/SerpsMods_Serp/SerpsModsHost.dll' 'original fixture build'
Put '.gitignore' ".work/`r`n.release-output/`r`nAPIShared/`r`nSerpsMods/`r`nAPISharedWorkshop/`r`n*.previous-*/`r`n"
# External SDK/publisher services are inert fixtures; routing, policy, proofs, Git, BAT and PS children are real.
$apiHelper=Join-Path $root 'Shared/Tools/ApiSharedRepository/MainSource.Common.ps1'
[IO.File]::AppendAllText($apiHelper, @'

function Resolve-ApiMainSelection($Workspace){return 'fixture-published-sdk'}
function Assert-ApiMainSelection($Workspace,$Path){return @{Root=(Get-WorkshopApiRoot $Workspace);Commit='c7b210f007035ff8edaf4d359a21f7bfbcbe479d'}}
function Ensure-ApiMainBuild($Workspace){if(-not $env:SHCDE_API_SHARED_MAIN_SELECTION){throw 'Missing fixed SDK selection'}}
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
# Observe the real host prompt, which Windows PowerShell can omit from redirected output.
function Read-Host([string]$Prompt){Write-Host ('FIXTURE_PROMPT='+$Prompt);Microsoft.PowerShell.Utility\Read-Host -Prompt $Prompt}
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
if(-not $Target){$Target='SerpsMods';$ValidateOnly=0}

$id=Get-WorkshopItemId $config $config.$Target $test
if($test -and $id -eq '3788821961'){throw 'Public ID leaked into test'}
if($PSCommandPath -notlike '*Prepare-LatestModpackUpload.ps1'){Write-Host ('FIXTURE_SOURCE='+$config.Root+';branch='+$env:SHCDE_MODPACK_BRANCH+';test='+$TestMode+';id='+$id)}
if($env:MODPACK_FIXTURE_FAIL -eq '1'){Write-Host 'FIXTURE_FAILURE';exit 7}
if($ValidateOnly){exit 0}
if($Target -eq 'SerpsMods' -and $env:MODPACK_FIXTURE_HOST_BUILD -eq '1' -and $PSCommandPath -notlike '*Prepare-LatestModpackUpload.ps1'){
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
 [void][IO.Directory]::CreateDirectory((Split-Path -Parent $reference))
 $apiCommit='c7b210f007035ff8edaf4d359a21f7bfbcbe479d'
 $apiTag=if($test){'APIShared/test/v0.9.2-'+$apiCommit.Substring(0,12)}else{'APIShared/v0.9.2'}
 $proof=[ordered]@{Commit=$commit;ApiSharedCommit=$apiCommit;ApiSharedMainCommit=$apiCommit;Infrastructure=@(@{Guid='APIShared_Serp';Version='0.9.2';SourceCommit=$apiCommit;ReleaseTag=$apiTag;PackageSha256=('a'*64)})}
 [IO.File]::WriteAllText($reference,($proof|ConvertTo-Json -Depth 5))
}
if($Target -eq 'APIShared'){
 $apiCommit='c7b210f007035ff8edaf4d359a21f7bfbcbe479d'
 $apiTag=if($test){'APIShared/test/v0.9.2-'+$apiCommit.Substring(0,12)}else{'APIShared/v0.9.2'}
 $evidence=[pscustomobject]@{Commit=$apiCommit;Version='0.9.2';Tag=$apiTag;Package=@{Sha256=('a'*64)}}
 $sdkIdentity=Get-ApiSharedPackageIdentity $evidence $apiCommit $test
 $null=Write-WorkshopPreparation $config $config.$Target $test '0.9.2' $apiCommit $apiTag -SdkIdentity $sdkIdentity
}else{$null=Write-WorkshopPreparation $config $config.$Target $test '1.0.28' $commit 'fixture'}
'@
foreach($service in @('Prepare-LatestModpackUpload.ps1','Prepare-ModpackWorkflow.ps1','Prepare-APISharedWorkshop.ps1')){Put ('Shared/Tools/Steam/'+$service) $prepare}
Put 'Shared/Tools/Steam/Upload-WorkshopTarget.ps1' @'
param([int]$TestMode,[int]$ValidateOnly,[string]$Target)
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
$config=Get-WorkshopConfiguration
if($env:MODPACK_FIXTURE_FAIL -eq '1'){Write-Host 'FIXTURE_FAILURE';exit 7}
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
# Real BAT/Windows PowerShell children must wait for Enter only in the original caller.
function Test-CompletionMatrix([string]$Driver,[switch]$WithoutArguments){
 $entryRoots=if($WithoutArguments){@($root,$source.Root)}else{@($root,$caller)}
 $pauseCases=if($WithoutArguments){@($false)}else{@($false,$true)}
 foreach($entryRoot in $entryRoots){
  foreach($fail in @($false,$true)){
   foreach($noPause in $pauseCases){
    $env:MODPACK_FIXTURE_FAIL=if($fail){'1'}else{'0'}
    $start=[Diagnostics.ProcessStartInfo]::new();$start.FileName=$env:ComSpec
    $flags=if($WithoutArguments){''}else{'/modpack /branch:main /validate'};if($noPause){$flags+=' /nopause'}
    $start.Arguments='/d /s /c ""'+(Join-Path $entryRoot $Driver)+'" '+$flags+'"'
    $start.WorkingDirectory=$entryRoot;$start.UseShellExecute=$false;$start.CreateNoWindow=$true
    $start.RedirectStandardInput=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $process=[Diagnostics.Process]::new();$process.StartInfo=$start
    $result=[Text.StringBuilder]::new();$sentEnter=$false
    try{
     [void]$process.Start();$errors=$process.StandardError.ReadToEndAsync()
     if($noPause){$process.StandardInput.Close()}
     # Read one character so a prompt without a newline cannot remain buffered.
     $buffer=New-Object char[] 1;$deadline=[DateTime]::UtcNow.AddSeconds(45)
     while($true){
      $read=$process.StandardOutput.ReadAsync($buffer,0,$buffer.Length)
      while(-not $read.Wait(100)){
       if([DateTime]::UtcNow -gt $deadline){throw ("BAT completion timed out: $Driver root=$entryRoot fail=$fail nopause=$noPause / "+$result.ToString())}
      }
      $count=$read.Result;if($count -eq 0){break}
      [void]$result.Append($buffer,0,$count)
      if(-not $noPause -and -not $sentEnter -and $result.ToString().Contains('FIXTURE_PROMPT=Enter zum Schliessen')){
       Check (-not $process.HasExited) 'BAT exited before receiving Enter'
       $process.StandardInput.WriteLine();$process.StandardInput.Close();$sentEnter=$true
      }
     }
     if(-not $process.WaitForExit(5000)){throw 'BAT did not exit after completion'}
     $output=$result.ToString();$description="$Driver root=$entryRoot fail=$fail nopause=$noPause noargs=$WithoutArguments"
     Check ($process.ExitCode -eq $(if($fail){1}else{0})) ("Wrong exit code: $description / $output / "+$errors.Result)
     if($WithoutArguments){
      Check ($output -match 'Modpack-Branch: main /' -and $output -notmatch 'Unknown option:|FIXTURE_PROMPT=Auswahl|FIXTURE_PROMPT=Modpack-Branch') ("Argumentless upload must select main without an option error/menu: $description / $output")
     }
     $prompts=[regex]::Matches($output,'FIXTURE_PROMPT=Enter zum Schliessen').Count
     Check ($prompts -eq $(if($noPause){0}else{1}) -and ($noPause -or $sentEnter)) ("Wrong pause count: $description / $output")
     $marker=if($fail){'FIXTURE_FAILURE'}elseif($Driver -eq 'CreateSteamModPack.bat'){'FIXTURE_SOURCE='}else{'FIXTURE_UPLOAD='}
     Check ([regex]::Matches($output,$marker).Count -eq 1) ("Workflow executed more or less than once: $description / $output")
    }finally{if(-not $process.HasExited){$process.Kill();$process.WaitForExit()};$process.Dispose()}
   }
  }
 }
 $env:MODPACK_FIXTURE_FAIL=''
}
Test-CompletionMatrix 'CreateSteamModPack.bat'
$env:MODPACK_FIXTURE_HOST_BUILD='1'
& (Join-Path $caller 'CreateSteamModPack.bat') /modpack /branch:main /nopause *> $log
Check ($LASTEXITCODE -eq 0) ('Main preparation BAT: '+[IO.File]::ReadAllText($log))
Test-CompletionMatrix 'UploadWorkshop.bat'
Test-CompletionMatrix 'UploadWorkshop.bat' -WithoutArguments
& (Join-Path $caller 'UploadWorkshop.bat') /validate /nopause *> $log
Check ($LASTEXITCODE -eq 0 -and [IO.File]::ReadAllText($log) -match 'FIXTURE_UPLOAD=.*author' -and [IO.File]::ReadAllText($log) -notmatch 'Auswahl') 'Upload without target must select the main modpack without a menu'
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
 # With a UTF-8 console the .NET Framework stdin writer emits a BOM that the hidden child
 # decodes as visible OEM characters before the answer; typed input never carries it.
 $inputEncoding=[Console]::InputEncoding
 try{
  if($inputEncoding.GetPreamble().Length){[Console]::InputEncoding=[Text.UTF8Encoding]::new($false)}
  [void]$process.Start();$output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
  $process.StandardInput.WriteLine($Selection);$process.StandardInput.Close();$process.WaitForExit()
  return @{ExitCode=$process.ExitCode;Output=$output.Result;Errors=$errors.Result}
 }finally{$process.Dispose();if([Console]::InputEncoding -ne $inputEncoding){[Console]::InputEncoding=$inputEncoding}}
}
$menu=Invoke-MenuBat '0';Check ($menu.ExitCode -eq 0 -and $menu.Output -match 'Modpack-Branch' -and $menu.Output -notmatch 'FIXTURE_SOURCE=') ('BAT menu cancellation failed: '+$menu.ExitCode+' / '+$menu.Output+$menu.Errors)
$menu=Invoke-MenuBat '1';Check ($menu.ExitCode -eq 0 -and $menu.Output -match 'branch=main;test=0') ('BAT main menu failed: '+$menu.Output+$menu.Errors)
$menu=Invoke-MenuBat '2';Check ($menu.ExitCode -eq 0 -and $menu.Output -match 'branch=develop;test=1') ('BAT develop menu failed: '+$menu.Output+$menu.Errors)
& (Join-Path $caller 'CreateSteamModPack.bat') /both /branch:develop /nopause *> $log
Check ($LASTEXITCODE -eq 0) ('Develop test preparation BAT: '+[IO.File]::ReadAllText($log))
$testProof=Join-Path $root '.release-output/SteamWorkshop/test/prepared/SerpsMods.json'
$p=[IO.File]::ReadAllText($testProof)|ConvertFrom-Json
Check ($p.Mode -ceq 'Test' -and $p.ModpackSource.Branch -ceq 'develop') 'Develop did not force private mode'
$before=Get-ApiRepositoryHash $testProof
& (Join-Path $caller 'UploadWorkshop.bat') /modpack /test /validate /nopause *> $log
Check ($LASTEXITCODE -eq 0 -and (Get-ApiRepositoryHash $testProof) -ceq $before -and ([IO.File]::ReadAllText($testProof)|ConvertFrom-Json).ModpackSource.Branch -ceq 'develop') ('Private upload must retain its prepared source and SDK: '+[IO.File]::ReadAllText($log))
$before=Get-ApiRepositoryHash $testProof
$ErrorActionPreference='Continue' # Expected BAT failures may write native stderr in Windows PowerShell 5.1.
& (Join-Path $caller 'UploadWorkshop.bat') /modpack /branch:develop /test /validate /nopause *> $log
Check ($LASTEXITCODE -eq 0 -and (Get-ApiRepositoryHash $testProof) -ceq $before) 'Matching prepared private develop upload was rejected or changed'
& (Join-Path $caller 'UploadWorkshop.bat') /modpack /branch:develop /validate /nopause *> $log
Check ($LASTEXITCODE -ne 0) 'Public upload accepted a develop source'
$generated=Join-Path $root 'SerpsModsHost/BepInEx/plugins/SerpsMods_Serp/SerpsModsHost.dll'
$generatedBytes=[IO.File]::ReadAllText($generated);[IO.File]::WriteAllText($generated,'changed generated build')
& (Join-Path $caller 'UploadWorkshop.bat') /modpack /test /validate /nopause *> $log
Check ($LASTEXITCODE -eq 0 -and (Get-ApiRepositoryHash $testProof) -ceq $before) 'Unrelated main outputs must not replace the prepared develop upload'
[IO.File]::WriteAllText($generated,$generatedBytes)
$payload=Join-Path $p.ModpackSource.Root '.release-output/SteamWorkshop/test/packages/SerpsMods/payload.txt'
$originalPayload=[IO.File]::ReadAllText($payload);[IO.File]::WriteAllText($payload,'tampered')
& (Join-Path $caller 'UploadWorkshop.bat') /modpack /test /validate /nopause *> $log
Check ($LASTEXITCODE -ne 0 -and [IO.File]::ReadAllText($payload) -ceq 'tampered') 'Changed prepared payload must be rejected without overwriting the preparation'
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
# A PR merged on GitHub must fast-forward the clean local release worktree; divergence must stop.
$originRepo=Join-Path $fixture 'origin.git';$pusher=Join-Path $fixture 'pusher'
Check ((Invoke-ApiRepositoryProcess 'git' @('clone','--bare','--quiet',$root,$originRepo)).ExitCode -eq 0) 'Bare origin clone failed'
Git @('remote','add','origin',$originRepo)|Out-Null
Check ((Invoke-ApiRepositoryProcess 'git' @('clone','--quiet','--branch','main',$originRepo,$pusher)).ExitCode -eq 0) 'Pusher clone failed'
function Push-OriginCommit([string]$Name){
 [IO.File]::WriteAllText((Join-Path $pusher ($Name+'.txt')),$Name)
 Invoke-ApiRepositoryGit $pusher @('add','-A')|Out-Null
 Invoke-ApiRepositoryGit $pusher @('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','-c','commit.gpgsign=false','commit','-m',$Name)|Out-Null
 Invoke-ApiRepositoryGit $pusher @('push','--quiet','origin','main')|Out-Null
 return ((Invoke-ApiRepositoryGit $pusher @('rev-parse','HEAD'))-join '').Trim()
}
$merged=Push-OriginCommit 'merged-pr'
$synced=Resolve-ModpackSource $caller 'main'
Check ($synced.Root -ieq [IO.Path]::GetFullPath($root) -and $synced.Commit -ceq $merged) 'Clean local main was not fast-forwarded to origin/main'
Git @('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','-c','commit.gpgsign=false','commit','--allow-empty','-m','local-only')|Out-Null
$null=Push-OriginCommit 'remote-only'
Reject {Resolve-ModpackSource $caller 'main'}
# After an upload only a pushed release folder whose sole changes are the frozen host outputs is removed.
Invoke-ApiRepositoryGit $pusher @('push','--quiet','origin','main:release')|Out-Null
$releaseFolder=Join-Path $root '.release-output\Modpack\release\source'
function New-ReleaseFolder{Git @('fetch','--quiet','origin','+refs/heads/release:refs/heads/release')|Out-Null;Git @('worktree','add','--quiet',$releaseFolder,'release')|Out-Null}
New-ReleaseFolder
[IO.File]::WriteAllText((Join-Path $releaseFolder 'SerpsModsHost/BepInEx/plugins/SerpsMods_Serp/SerpsModsHost.dll'),'release host build')
Remove-ModpackReleaseWorktree $root ([pscustomobject]@{Root=$releaseFolder;Branch='release'})
Check (-not (Test-Path -LiteralPath $releaseFolder)) 'Uploaded release folder with only frozen outputs was kept'
New-ReleaseFolder
[IO.File]::AppendAllText((Join-Path $releaseFolder '.gitignore'),"`r`nchanged")
Reject {Remove-ModpackReleaseWorktree $root ([pscustomobject]@{Root=$releaseFolder;Branch='release'})}
Check (Test-Path -LiteralPath $releaseFolder) 'Release folder with a source change was removed'
Invoke-ApiRepositoryGit $releaseFolder @('checkout','--','.gitignore')|Out-Null
Invoke-ApiRepositoryGit $releaseFolder @('-c','user.name=Fixture','-c','user.email=fixture@example.invalid','-c','commit.gpgsign=false','commit','--quiet','--allow-empty','-m','unpushed')|Out-Null
Reject {Remove-ModpackReleaseWorktree $root ([pscustomobject]@{Root=$releaseFolder;Branch='release'})}
Check (Test-Path -LiteralPath $releaseFolder) 'Release folder with unpushed commits was removed'
Write-Host "PASS: $checks modpack branch/menu/worktree/proof/ID checks with real BAT and PowerShell children. Fixtures: $fixture"
