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
$fixture=Join-Path $root ('.inspect/workshop-workflow-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($fixture)
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
foreach($names in @(@('../escape'),@('SerpsMods.map','serpsmods.map'),@('Payload/BepInEx/plugins/SerpsMods_Serp/CON.dll'),@('Payload/BepInEx/plugins/SerpsMods_Serp/config/settings.json'),@('Payload/BepInEx/plugins/SerpsMods_Serp/A','Payload/BepInEx/plugins/SerpsMods_Serp/A/b.dll'))){Reject {Assert-WorkshopContentZip (MakeZip $names)}}
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'Create-SteamModPack.ps1'),[ref]$null,[ref]$null)
$function=$ast.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq 'Get-HostContentSignature'},$true)
. ([scriptblock]::Create($function.Extent.Text))
function Get-Sha256([string]$Path){Get-WorkshopFileHash $Path}
$script:Root=$fixture;$ApiSharedWorkshopItemId='123'
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
