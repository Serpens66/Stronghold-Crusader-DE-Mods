param([switch]$PublisherScopeOnly)
try {
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
$checks=0
function Check([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message};$script:checks++}
function Reject([scriptblock]$Action){$failed=$false;try{& $Action}catch{$failed=$true};Check $failed 'Expected rejection.'}
foreach($entry in @(@('/modpack','SerpsMods'),@('/apishared','APIShared'),@('/both','Both'))){
    $options=Resolve-WorkshopOptions @($entry[0],'/validate','/test','/nopause')
    Check ($options.Target -ceq $entry[1] -and $options.Validate -and $options.Test -and $options.NoPause) 'Selection/flags mismatch.'
}
foreach($entry in @(@('1','SerpsMods'),@('2','APIShared'),@('3','Both'),@('0','Cancel'))){Check ((Resolve-WorkshopOptions @() $entry[0]).Target -ceq $entry[1]) 'Menu mismatch.'}
Reject {Resolve-WorkshopOptions @('/modpack','/both')}
Reject {Resolve-WorkshopOptions @('/apishared','/apishared')}
Reject {Resolve-WorkshopOptions @('/unknown')}
$completed=[Collections.Generic.List[string]]::new()
Reject {Invoke-WorkshopTargets @('APIShared','SerpsMods') {param($target) $completed.Add($target);throw 'First step failed'}}
Check (($completed -join ',') -ceq 'APIShared') 'Second target started after first failure.'
$completed.Clear()
Reject {Invoke-WorkshopTargets @('APIShared','SerpsMods') {param($target) $completed.Add($target);if($target -eq 'SerpsMods'){throw 'Second step failed'}}}
Check (($completed -join ',') -ceq 'APIShared,SerpsMods') 'First success was not retained before second failure.'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
$fixture = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -Kind t -Name 'WorkshopWorkflow' -Transient
[void][IO.Directory]::CreateDirectory($fixture)
. (Join-Path $PSScriptRoot 'WorkshopPublisherScope.ps1')
# The real frozen publisher is immutable. Exercise its external process boundary
# with a fake CLI, including failure after Steam allocated an item ID.
$scopeRoot=Join-Path $fixture 'scoped-publisher';[void][IO.Directory]::CreateDirectory($scopeRoot)
$fakePublisher=Join-Path $scopeRoot 'FakePublisher.exe'
Add-Type -OutputAssembly $fakePublisher -OutputType ConsoleApplication -ReferencedAssemblies @('System.dll','System.Core.dll','System.Web.Extensions.dll') -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
public static class FakePublisher {
 public static int Main(string[] args) {
  if(args[0]=="--hold") {System.Threading.Thread.Sleep(5000);return 0;}
  var json=new JavaScriptSerializer();var request=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(args[1]));
  string state=(string)request["StateRoot"], id=(string)request["PackageId"];
  string content=Path.Combine(state,"Uploads",Guid.NewGuid().ToString("N"),"Content");Directory.CreateDirectory(content);
  File.WriteAllBytes(Path.Combine(content,"payload.bin"),new byte[1024*1024]);
  if(request.ContainsKey("SaveRecoveryId")) {
   string publishing=Path.Combine(state,"Publishing");Directory.CreateDirectory(publishing);
   File.WriteAllText(Path.Combine(publishing,id+".json"),"{\"WorkshopId\":\"987\",\"CreatedByPublisher\":true}");
  }
  File.WriteAllText(args[2],json.Serialize(new {StateRoot=state,Action=args[0]}));
  if(request.ContainsKey("SpawnDescendant")) {
   var info=new System.Diagnostics.ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--hold");
   info.UseShellExecute=false;info.CreateNoWindow=true;
   var child=System.Diagnostics.Process.Start(info);File.WriteAllText((string)request["DescendantPidFile"],child.Id.ToString());
  }
  Console.WriteLine("Fake publisher completed "+args[0]);
  return request.ContainsKey("Fail")?9:0;
 }
}
'@
$publisherState=Join-Path $scopeRoot '.release-output/SteamWorkshop/publisher'
$requestFile=Join-Path $scopeRoot 'request.json';$resultFile=Join-Path $scopeRoot 'result.json'
$publisherRequest=[ordered]@{StateRoot=$publisherState;PackageId='SerpsMods_Serp'}
[void][IO.Directory]::CreateDirectory((Join-Path $publisherState 'Publishing'))
$centralId=Join-Path $publisherState 'Publishing/SerpsMods_Serp.json'
[IO.File]::WriteAllText($centralId,'{"WorkshopId":"321","Version":"1.0.0","Fingerprint":"'+('a'*64)+'"}')
foreach($action in @('validate','inspect','publish','validate')){
 [IO.File]::WriteAllText($requestFile,($publisherRequest|ConvertTo-Json))
 Check ((Invoke-ScopedWorkshopPublisher $fakePublisher $action $requestFile $resultFile) -eq 0) 'Scoped publisher exit code changed.'
 Check (@(Get-ChildItem -LiteralPath (Join-Path $publisherState 'calls') -Directory).Count -eq 0) 'Completed publisher snapshot retained.'
 Check ((Get-Content -LiteralPath $requestFile -Raw|ConvertFrom-Json).StateRoot -ceq $publisherState) 'Original replay request changed.'
 Check ((Get-Content -LiteralPath $centralId -Raw|ConvertFrom-Json).WorkshopId -ceq '321') 'Unchanged recovery state overwritten.'
}
$publisherRequest.SaveRecoveryId=$true;$publisherRequest.Fail=$true
[IO.File]::WriteAllText($requestFile,($publisherRequest|ConvertTo-Json))
Check ((Invoke-ScopedWorkshopPublisher $fakePublisher publish $requestFile $resultFile) -eq 9) 'Publisher failure hidden.'
Check ((Get-Content -LiteralPath $centralId -Raw|ConvertFrom-Json).WorkshopId -ceq '987') 'Allocated item ID lost after failure.'
Check (@(Get-ChildItem -LiteralPath (Join-Path $publisherState 'calls') -Directory).Count -eq 0) 'Failed publisher payload retained.'
function New-ScopeFixture {
 $path=Join-Path $publisherState ('calls/'+[guid]::NewGuid().ToString('N'));[void][IO.Directory]::CreateDirectory($path)
 $owner=[ordered]@{SchemaVersion=1;Protocol='WorkspacePublisherCall';Root=$path;StateRoot=$publisherState;PackageId='SerpsMods_Serp';InitialStateHash=(Get-WorkshopPublisherScopeHash $centralId);Phase='Complete';ChildPid=0;ChildStartedUtc=''}
 [IO.File]::WriteAllText((Join-Path $path 'owner.json'),($owner|ConvertTo-Json));return [pscustomobject]@{Path=$path;Owner=$owner}
}
$owned=New-ScopeFixture;$owned.Owner.ChildPid=$PID;$owned.Owner.ChildStartedUtc=(Get-Process -Id $PID).StartTime.ToUniversalTime().ToString('o')
[IO.File]::WriteAllText((Join-Path $owned.Path 'owner.json'),($owned.Owner|ConvertTo-Json))
Reject {Complete-WorkshopPublisherScope $publisherState $owned.Path};Check ([IO.Directory]::Exists($owned.Path)) 'Live child scope removed.'
[IO.File]::WriteAllText((Join-Path $owned.Path 'lease'),'');[IO.File]::WriteAllText($requestFile,($publisherRequest|ConvertTo-Json));Reject {Invoke-ScopedWorkshopPublisher $fakePublisher publish $requestFile $resultFile}
Check (@(Get-ChildItem -LiteralPath (Join-Path $publisherState 'calls') -Directory).Count -eq 1) 'Retry created another Steam operation while a previous child is alive.'
$owned.Owner.ChildPid=0;[IO.File]::WriteAllText((Join-Path $owned.Path 'owner.json'),($owned.Owner|ConvertTo-Json));Complete-WorkshopPublisherScope $publisherState $owned.Path
$owned=New-ScopeFixture;[IO.File]::WriteAllText((Join-Path $owned.Path 'authored.txt'),'preserve')
Reject {Complete-WorkshopPublisherScope $publisherState $owned.Path};Check ([IO.File]::Exists((Join-Path $owned.Path 'authored.txt'))) 'Unknown data discarded.'
[IO.File]::Delete((Join-Path $owned.Path 'authored.txt'));Complete-WorkshopPublisherScope $publisherState $owned.Path
$owned=New-ScopeFixture;$owned.Owner.Root=$scopeRoot;[IO.File]::WriteAllText((Join-Path $owned.Path 'owner.json'),($owned.Owner|ConvertTo-Json))
Reject {Complete-WorkshopPublisherScope $publisherState $owned.Path};$owned.Owner.Root=$owned.Path;[IO.File]::WriteAllText((Join-Path $owned.Path 'owner.json'),($owned.Owner|ConvertTo-Json));Complete-WorkshopPublisherScope $publisherState $owned.Path
$owned=New-ScopeFixture;$outside=Join-Path $scopeRoot 'outside';[void][IO.Directory]::CreateDirectory($outside);[IO.File]::WriteAllText((Join-Path $outside 'keep.txt'),'keep')
$link=Join-Path $owned.Path 'Uploads';New-Item -ItemType Junction -Path $link -Target $outside|Out-Null
Reject {Complete-WorkshopPublisherScope $publisherState $owned.Path};Check ([IO.File]::ReadAllText((Join-Path $outside 'keep.txt')) -ceq 'keep') 'Junction target changed.'
[IO.Directory]::Delete($link,$false);Complete-WorkshopPublisherScope $publisherState $owned.Path
$owned=New-ScopeFixture;[IO.File]::WriteAllText((Join-Path $owned.Path 'lease'),'')
[void][IO.Directory]::CreateDirectory((Join-Path $owned.Path 'Publishing'));[IO.File]::WriteAllText((Join-Path $owned.Path 'Publishing/SerpsMods_Serp.json'),'{"WorkshopId":"654"}')
$publisherRequest.Remove('SaveRecoveryId');$publisherRequest.Remove('Fail');[IO.File]::WriteAllText($requestFile,($publisherRequest|ConvertTo-Json))
Check ((Invoke-ScopedWorkshopPublisher $fakePublisher validate $requestFile $resultFile) -eq 0) 'Abandoned scope recovery failed.'
Check ((Get-Content -LiteralPath $centralId -Raw|ConvertFrom-Json).WorkshopId -ceq '654') 'Abandoned allocated item ID not recovered.'
Check (-not [IO.Directory]::Exists($owned.Path)) 'Abandoned payload scope retained.'
$owned=New-ScopeFixture;[void][IO.Directory]::CreateDirectory((Join-Path $owned.Path 'Publishing'))
[IO.File]::WriteAllText((Join-Path $owned.Path 'Publishing/SerpsMods_Serp.json.tmp'),'{"WorkshopId":"765","CreatedByPublisher":true}')
Complete-WorkshopPublisherScope $publisherState $owned.Path
Check ((Get-Content -LiteralPath $centralId -Raw|ConvertFrom-Json).WorkshopId -ceq '765') 'Pending atomic-write recovery ID lost.'
$owned=New-ScopeFixture;[void][IO.Directory]::CreateDirectory((Join-Path $owned.Path 'Publishing'))
[IO.File]::WriteAllText((Join-Path $owned.Path 'Publishing/SerpsMods_Serp.json.tmp'),'{"WorkshopId":')
$bulk=Join-Path $owned.Path ('Uploads/'+[guid]::NewGuid().ToString('N')+'/Content');[void][IO.Directory]::CreateDirectory($bulk);[IO.File]::WriteAllBytes((Join-Path $bulk 'payload.bin'),[byte[]]::new(1048576))
Reject {Complete-WorkshopPublisherScope $publisherState $owned.Path}
Check (-not [IO.Directory]::Exists((Join-Path $owned.Path 'Uploads'))) 'Invalid compact recovery retained bulky payload.'
Check ([IO.File]::ReadAllText((Join-Path $owned.Path 'Publishing/SerpsMods_Serp.json.tmp')) -ceq '{"WorkshopId":') 'Partial recovery bytes discarded.'
[IO.File]::Delete((Join-Path $owned.Path 'Publishing/SerpsMods_Serp.json.tmp'));Complete-WorkshopPublisherScope $publisherState $owned.Path
$owned=New-ScopeFixture;[void][IO.Directory]::CreateDirectory((Join-Path $owned.Path 'Publishing'));[IO.File]::WriteAllText((Join-Path $owned.Path 'Publishing/SerpsMods_Serp.json'),'{"WorkshopId":"876"}')
$bulk=Join-Path $owned.Path ('Uploads/'+[guid]::NewGuid().ToString('N')+'/Content');[void][IO.Directory]::CreateDirectory($bulk);[IO.File]::WriteAllBytes((Join-Path $bulk 'payload.bin'),[byte[]]::new(1048576))
[IO.File]::WriteAllText($centralId,'{"WorkshopId":"999"}');Reject {Complete-WorkshopPublisherScope $publisherState $owned.Path}
Check (-not [IO.Directory]::Exists((Join-Path $owned.Path 'Uploads'))) 'Conflicting compact recovery retained bulky payload.'
Check ((Get-Content -LiteralPath $centralId -Raw|ConvertFrom-Json).WorkshopId -ceq '999') 'Recovery conflict overwrote current item ID.'
[IO.File]::WriteAllText($centralId,'{"WorkshopId":"765","CreatedByPublisher":true}');Complete-WorkshopPublisherScope $publisherState $owned.Path
$publisherRequest.SpawnDescendant=$true;$publisherRequest.DescendantPidFile=Join-Path $scopeRoot 'descendant.pid';[IO.File]::WriteAllText($requestFile,($publisherRequest|ConvertTo-Json))
$drainWatch=[Diagnostics.Stopwatch]::StartNew();Reject {Invoke-ScopedWorkshopPublisher $fakePublisher validate $requestFile $resultFile};$drainWatch.Stop()
Check ($drainWatch.Elapsed.TotalSeconds -lt 4.5) 'Inherited output pipe kept wrapper waiting for descendant.'
$held=@(Get-ChildItem -LiteralPath (Join-Path $publisherState 'calls') -Directory);Check ($held.Count -eq 1) 'Unresolved descendant scope removed.'
$descendant=Get-Process -Id ([int][IO.File]::ReadAllText($publisherRequest.DescendantPidFile)) -ErrorAction SilentlyContinue
if($descendant){[void]$descendant.WaitForExit(10000);$descendant.Dispose()}
$record=Get-Content -LiteralPath (Join-Path $held[0].FullName 'owner.json') -Raw|ConvertFrom-Json;$record.Phase='Complete';[IO.File]::WriteAllText((Join-Path $held[0].FullName 'owner.json'),($record|ConvertTo-Json));Complete-WorkshopPublisherScope $publisherState $held[0].FullName
. (Join-Path $PSScriptRoot 'WorkspaceReleaseRetention.ps1')
$nativeProcessSnapshot=${function:Get-WorkspaceReleaseProcesses}
function Get-WorkspaceReleaseProcesses {@()}
$pruneRoot=Join-Path $fixture 'prune';$pruneOutput=Join-Path $pruneRoot '.release-output'
function Write-PruneFixture([string]$Relative,[string]$Data='fixture'){$path=Join-Path $pruneOutput $Relative;[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path));[IO.File]::WriteAllText($path,$Data);return $path}
$reference=Write-PruneFixture 'SerpsMods/v1.0.7/proof.json'
$sdk=Write-PruneFixture 'SerpsMods/cache/APIShared/v0.1.0/sdk.dll'
$manifest=Write-PruneFixture 'SteamWorkshop/delivery/current/delivery.json'
$null=Write-PruneFixture 'SteamWorkshop/releases/v1.0.2/current.zip'
$null=Write-PruneFixture 'SteamWorkshop/prepared/SerpsMods.json' ((@{Version='1.0.2';ReleaseProvenance=$reference;MainSource=@{Selection=$manifest};Sdk=@{Path=$sdk};PublishedRelease=@{Version='1.0.2'}})|ConvertTo-Json -Depth 5)
$null=Write-PruneFixture 'SteamWorkshop/items/3024040-SerpsMods.item-id' '123'
foreach($relative in @('SerpsMods/v1.0.7/stage/old.bin','SerpsMods/cache/old.bin','SteamWorkshop/validation/old/Uploads/a.bin','SteamWorkshop/preparation/old/payload.bin','SteamWorkshop/publisher/Uploads/old/payload.bin','SteamWorkshop/releases/v1.0.1/old.zip')){$null=Write-PruneFixture $relative}
$cleanup=Clear-WorkspaceReleaseOutputs $pruneRoot
Check (-not $cleanup.Skipped -and $cleanup.RemovedBytes -gt 0) 'Generated output cleanup did not run.'
foreach($pin in @($reference,$sdk,$manifest,(Join-Path $pruneOutput 'SteamWorkshop/releases/v1.0.2/current.zip'),(Join-Path $pruneOutput 'SteamWorkshop/items/3024040-SerpsMods.item-id'))){Check ([IO.File]::Exists($pin)) 'Current prepared identity/state deleted.'}
Check (-not [IO.Directory]::Exists((Join-Path $pruneOutput 'SteamWorkshop/validation'))) 'Validation snapshot archive retained.'
for($generation=1;$generation -le 5;$generation++){
 $path=Write-PruneFixture ('CastlePlanner/v0.1.'+$generation+'/stage/output.dll');$null=Clear-WorkspaceReleaseOutputs $pruneRoot
 Check (-not [IO.File]::Exists($path)) 'Versioned release outputs accumulate after repeated preparation.'
}
$git=Write-PruneFixture 'CastlePlanner/v0.2.0/source/.git' 'unclassified'
$cleanup=Clear-WorkspaceReleaseOutputs $pruneRoot;Check ([IO.File]::Exists($git)) 'Unknown Git source removed by release cleanup.';[IO.File]::Delete($git)
$git=Write-PruneFixture 'CastlePlanner/v0.2.1/.git' 'worktree pointer'
$pin=Write-PruneFixture 'CastlePlanner/v0.2.1/pinned.map'
$untracked=Write-PruneFixture 'CastlePlanner/v0.2.1/untracked.txt' 'unique source'
$null=Write-PruneFixture 'SteamWorkshop/prepared/linked.json' ((@{PackagePath=$pin})|ConvertTo-Json)
$cleanup=Clear-WorkspaceReleaseOutputs $pruneRoot
foreach($path in @($git,$pin,$untracked)){Check ([IO.File]::Exists($path)) 'Referenced Git root partially removed by release cleanup.'}
[IO.File]::Delete($git)
Set-Item Function:Get-WorkspaceReleaseProcesses $nativeProcessSnapshot
if($PublisherScopeOnly){Write-Host "PASS: $checks Workshop flags, publisher scope and output lifecycle checks.";return}
$config=Get-WorkshopConfiguration;$config.Root=$fixture;$config.APIShared.Folder=Join-Path $fixture 'APISharedWorkshop';$config.SerpsMods.Folder=Join-Path $fixture 'SerpsMods'
Check ((Get-WorkshopItemId $config $config.SerpsMods $false) -ceq '3788821961') 'SerpsMods identity changed.'
Check (-not(Get-WorkshopItemId $config $config.APIShared $false)) 'APIShared identity must be independent.'
Check (-not(Get-WorkshopItemId $config $config.SerpsMods $true)) 'Tests must not inherit production ID.'
$config.APIShared.ItemId='123';Check ((Get-WorkshopItemId $config $config.APIShared $false) -ceq '123') 'Configured ID ignored.'
$state=Get-WorkshopStateRoot $config $true;$items=Join-Path $state 'items';[void][IO.Directory]::CreateDirectory($items)
[IO.File]::WriteAllText((Join-Path $items '3024040-APIShared.item-id'),'123');Reject {Get-WorkshopItemId $config $config.APIShared $true}
[void][IO.Directory]::CreateDirectory($config.APIShared.Folder)
[IO.File]::WriteAllText((Join-Path $config.APIShared.Folder 'APIShared.map'),'fixture')
$null=Write-WorkshopPreparation $config $config.APIShared $false '0.7.1' ('a'*40) 'APIShared/v0.7.1'
Check ((Assert-WorkshopPreparation $config $config.APIShared $false).Version -ceq '0.7.1') 'Proof rejected unchanged folder.'
[IO.File]::WriteAllText((Join-Path $config.APIShared.Folder 'APIShared.map'),'modified');Reject {Assert-WorkshopPreparation $config $config.APIShared $false}
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
function MakeZip([string[]]$Names){$path=Join-Path $fixture ([guid]::NewGuid().ToString('N')+'.zip');$zip=[IO.Compression.ZipFile]::Open($path,[IO.Compression.ZipArchiveMode]::Create);try{foreach($name in $Names){[void]$zip.CreateEntry($name)}}finally{$zip.Dispose()};return $path}
Assert-WorkshopContentZip (MakeZip @('SerpsMods.map','apishared-workshop.json','Payload/BepInEx/plugins/SerpsMods_Serp/Mods/Example.dll'));$checks++
Assert-WorkshopContentZip (MakeZip @('SerpsMods.map','Payload\BepInEx\plugins\SerpsMods_Serp\info.json'));$checks++
Reject {Assert-WorkshopContentZip (MakeZip @('Payload/BepInEx/plugins/SerpsMods_Serp/info.json','Payload\BepInEx\plugins\SerpsMods_Serp\info.json'))}
foreach($names in @(@('../escape'),@('SerpsMods.map','serpsmods.map'),@('Payload/BepInEx/plugins/SerpsMods_Serp/CON.dll'),@('Payload/BepInEx/plugins/SerpsMods_Serp/config/settings.json'),@('Payload/BepInEx/plugins/SerpsMods_Serp/A','Payload/BepInEx/plugins/SerpsMods_Serp/A/b.dll'))){Reject {Assert-WorkshopContentZip (MakeZip $names)}}
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Create-SteamModPack.ps1'),[ref]$null,[ref]$null)
$function=$ast.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Get-HostContentSignature'},$true)
. ([scriptblock]::Create($function.Extent.Text))
function Get-Sha256([string]$Path){Get-WorkshopFileHash $Path}
$script:Root=$fixture;$ApiSharedWorkshopItemId='123'
$script:ApiMainRoot=Join-Path $fixture 'APIShared'
foreach($relative in @('SerpsModsHost/src/Host.cs','SerpsModsHost/obj/generated.cs','Shared/Tools/Steam/Prepare-SerpsModsWorkshop.ps1','APIShared/tools/Prepare-WorkshopPackage.ps1','APIShared/tools/New-WorkshopBootstrap.ps1','APIShared/tools/WorkshopPackage.ps1')){$path=Join-Path $fixture $relative;[void][IO.Directory]::CreateDirectory((Split-Path -Parent $path));[IO.File]::WriteAllText($path,'fixture')}
$history=@([pscustomobject]@{PackVersion='1.0.1';MapSha256=('a'*64)})
$before=Get-HostContentSignature -Mods @() -HistoricalUploads $history
$loose=[pscustomobject]@{PackVersion='1.0.2';MapSha256=('b'*64);Format='Loose';ContentFingerprint=('c'*64)}
Check ((Get-HostContentSignature -Mods @() -HistoricalUploads @($history+$loose)) -ceq $before) 'A confirmed loose upload must not change package signature.'
[IO.File]::WriteAllText((Join-Path $fixture 'SerpsModsHost/obj/generated.cs'),'generated change')
Check ((Get-HostContentSignature -Mods @() -HistoricalUploads $history) -ceq $before) 'Generated build files must not change package signature.'
[IO.File]::WriteAllText((Join-Path $fixture 'SerpsModsHost/src/Host.cs'),'source change')
Check ((Get-HostContentSignature -Mods @() -HistoricalUploads $history) -cne $before) 'Host source changes must change package signature.'
$before=Get-HostContentSignature -Mods @() -HistoricalUploads $history
[IO.File]::WriteAllText((Join-Path $fixture 'APIShared/tools/WorkshopPackage.ps1'),'packager change')
Check ((Get-HostContentSignature -Mods @() -HistoricalUploads $history) -cne $before) 'Packaging changes must change package signature.'
Write-Host "PASS: $checks Workshop workflow checks. Fixtures: $fixture"
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-WorkshopOutputRetention.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Workshop output retention regression failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-WorkshopOutputGit.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Workshop output Git regression failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-BootstrapEnvironment.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Bootstrap environment regression failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Test-WorkshopCacheAndChangelog.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Workshop cache/changelog regression failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot '../ApiSharedRepository/Test-MainSource.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Pinned main/PR/Workshop BAT regression failed.' }
foreach($test in @('Test-ModpackBranches.ps1','Test-PublishedSdkPolicy.ps1','Test-PublishedModpack.ps1')){
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $test)
    if($LASTEXITCODE -ne 0){throw "$test failed."}
}

} finally { if(Get-Command Complete-TransientWorkDirectories -ErrorAction SilentlyContinue){Complete-TransientWorkDirectories -Owner $PSCommandPath} }
