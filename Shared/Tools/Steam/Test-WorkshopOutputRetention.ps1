param([string]$ResumeFixture='', [switch]$PauseAfterDelete)
$ErrorActionPreference='Stop'
$savedSelection=$env:SHCDE_API_SHARED_MAIN_SELECTION
$env:SHCDE_API_SHARED_MAIN_SELECTION=$null
try {
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
. (Join-Path $PSScriptRoot 'WorkshopOutputRetention.ps1')
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$checks=0
function Check([bool]$Condition,[string]$Message){if(-not $Condition){throw $Message};$script:checks++}
function Put([string]$Path,[string]$Text){[void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path));[IO.File]::WriteAllText($Path,$Text,[Text.UTF8Encoding]::new($false))}
function Open-CaseLock($Case){[void][IO.Directory]::CreateDirectory($Case.State);[IO.File]::Open((Join-Path $Case.State 'workflow.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)}
if($ResumeFixture){
    $case=[IO.File]::ReadAllText($ResumeFixture)|ConvertFrom-Json
    $lock=Open-CaseLock $case
    try{
        if($PauseAfterDelete){
            function Remove-Item {
                param([string]$LiteralPath,[switch]$Force,[string]$ErrorAction)
                Microsoft.PowerShell.Management\Remove-Item -LiteralPath $LiteralPath -Force -ErrorAction Stop
                [IO.File]::WriteAllText((Join-Path $case.Root 'deleted.flag'),$LiteralPath)
                [Threading.Thread]::Sleep(300000)
            }
        }
        $before=@(Get-WorkshopOutputBackups $case.Config $case.Target $case.Test|ForEach-Object {$_.Name})
        $result=Invoke-WorkshopOutputRetention $case.Config $case.Target $case.Test $before $lock
        if($result.Deferred){exit 2}
        exit 0
    }finally{$lock.Dispose()}
}
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$fixture=Join-Path $workspace ('.inspect/workshop-output-retention-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($fixture)
function New-Case([string]$Name,[string]$Key='APIShared',[bool]$Test=$false){
    $root=Join-Path $fixture $Name
    $config=[pscustomobject]@{Root=$root;AppId=3024040;APIShared=[pscustomobject]@{Key='APIShared';Guid='APIShared_Serp';Folder=(Join-Path $root 'APISharedWorkshop')};SerpsMods=[pscustomobject]@{Key='SerpsMods';Guid='SerpsMods_Serp';Folder=(Join-Path $root 'SerpsMods')}}
    $target=$config.$Key
    $case=[pscustomobject]@{Root=$root;Config=$config;Target=$target;Test=$Test;State=(Get-WorkshopStateRoot $config $Test);Destination=(Get-WorkshopFolder $config $target $Test)}
    [void][IO.Directory]::CreateDirectory($root)
    Write-SteamWorkshopJson (Join-Path $root 'case.json') $case
    return $case
}
function New-Contents([string]$Path,$Target,[string]$Version,[switch]$Legacy){
    [void][IO.Directory]::CreateDirectory($Path)
    $map=if($Target.Key -ceq 'APIShared'){'APIShared.map'}else{'SerpsMods.map'}
    $loose=$Target.Key -ceq 'SerpsMods' -and -not $Legacy
    $guid=if($loose){'SerpsModsWorkshopBootstrap_Serp'}else{$Target.Guid}
    $stream=[IO.FileStream]::new((Join-Path $Path $map),[IO.FileMode]::Create)
    $archive=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
    try{$entry=$archive.CreateEntry('info.json');$writer=[IO.StreamWriter]::new($entry.Open());try{$writer.Write((@{GUID=$guid;Version=$Version;Manifest=1}|ConvertTo-Json -Compress))}finally{$writer.Dispose()}}finally{$archive.Dispose();$stream.Dispose()}
    Put (Join-Path $Path 'preview.png') 'synthetic preview'
    if($loose){
        $relative='BepInEx/plugins/SerpsMods_Serp/Example.dll'
        $payload=Join-Path $Path ('Payload/'+$relative);Put $payload ('synthetic payload '+$Version)
        [void][IO.Directory]::CreateDirectory((Join-Path $Path 'Payload/BepInEx/plugins/SerpsMods_Serp/Examples/BundledMission'))
        Write-SteamWorkshopJson (Join-Path $Path 'apishared-workshop.json') ([ordered]@{SchemaVersion=1;PackageId=$Target.Guid;Version=$Version;Roots=@('BepInEx/plugins/SerpsMods_Serp');Bootstrap=@{Guid=$guid;MapFile=$map;Version=$Version};Files=@(@{Path=$relative;Size=(Get-Item -LiteralPath $payload).Length;Sha256=(Get-WorkshopFileHash $payload)})})
    }
}
function Proof($Case,[string]$Version='1.0.1'){
    if($Case.Target.Key -ceq 'SerpsMods'){Put (Join-Path $Case.State 'references/SerpsMods.provenance.json') '{"Commit":"fixture"}'}
    $null=Write-WorkshopPreparation $Case.Config $Case.Target $Case.Test $Version ('a'*40) 'fixture'
}
function Backup($Case,[string]$Version='1.0.0',[switch]$Legacy){
    $path=$Case.Destination+'.previous-'+[guid]::NewGuid().ToString('N')
    New-Contents $path $Case.Target $Version -Legacy:$Legacy
    return $path
}
function Ready($Case){New-Contents $Case.Destination $Case.Target '1.0.1';Proof $Case}
function Run($Case,[string[]]$Before=$null,[string[]]$DeleteOnly=$null){
    if($null -eq $Before){$Before=@(Get-WorkshopOutputBackups $Case.Config $Case.Target $Case.Test|ForEach-Object {$_.Name})}
    $lock=Open-CaseLock $Case
    try{return Invoke-WorkshopOutputRetention $Case.Config $Case.Target $Case.Test $Before $lock $DeleteOnly}finally{$lock.Dispose()}
}
function Pending($Case,[string]$Directory){
    $inventory=Get-WorkshopOutputBackupInventory $Directory $Case.Target
    $job=[ordered]@{SchemaVersion=1;Target=$Case.Target.Key;PackageId=$Case.Target.Guid;Mode=$(if($Case.Test){'Test'}else{'Production'});Destination=$Case.Destination;DirectoryName=[IO.Path]::GetFileName($Directory);Layout=$inventory.Layout;Files=@($inventory.Files);Directories=@($inventory.Directories)}
    $job.Digest=Get-WorkshopRetentionJobHash $job
    $path=Join-Path $Case.State ('retention/'+$Case.Target.Key+'.pending.json');Write-WorkshopRetentionJob $path $job
    return $path
}
function Resume($Case){
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -ResumeFixture (Join-Path $Case.Root 'case.json') *> (Join-Path $Case.Root 'resume.log')
    Check ($LASTEXITCODE -eq 0) ('Fresh process failed: '+[IO.File]::ReadAllText((Join-Path $Case.Root 'resume.log')))
}
foreach($key in @('APIShared','SerpsMods')){
    $case=New-Case ('hundred-'+$key) $key
    $lock=Open-CaseLock $case
    try{
        for($generation=1;$generation -le 100;$generation++){
            $before=@(Get-WorkshopOutputBackups $case.Config $case.Target $false|ForEach-Object {$_.Name})
            $source=Join-Path $case.Root 'stage';New-Contents $source $case.Target ('1.0.'+$generation)
            Publish-WorkshopFolder $source $case.Destination $case.Root
            Proof $case ('1.0.'+$generation)
            $result=Invoke-WorkshopOutputRetention $case.Config $case.Target $false $before $lock
            Check ($result.Deferred -eq 0) 'Valid generation deferred.'
            $backups=@(Get-WorkshopOutputBackups $case.Config $case.Target $false)
            Check ($backups.Count -eq [Math]::Min(1,$generation-1)) 'History is not bounded to one predecessor.'
            if($generation -gt 1){$inventory=Get-WorkshopOutputBackupInventory $backups[0].FullName $case.Target;Check ($inventory.Version -ceq ('1.0.'+($generation-1))) 'Immediate predecessor was not retained.'}
        }
        $null=Assert-WorkshopPreparation $case.Config $case.Target $false
        Check (-not (Test-Path -LiteralPath (Join-Path $case.State ('retention/'+$key+'.pending.json')))) 'Completed retention left a job.'
    }finally{$lock.Dispose()}
}
$case=New-Case 'legacy' 'SerpsMods';Ready $case
$old=Backup $case -Legacy
Put (Join-Path $old 'SerpsMods.map.sha256') ((Get-WorkshopFileHash (Join-Path $old 'SerpsMods.map'))+'  SerpsMods.map')
$new=Backup $case '1.0.1'
$result=Run $case @([IO.Path]::GetFileName($old))
Check ($result.Removed -eq 1 -and -not (Test-Path -LiteralPath $old) -and (Test-Path -LiteralPath $new)) 'Legacy SerpsMods backup was not safely pruned.'
foreach($kind in @('unknown','hidden','empty-directory','serps-unknown-directory','serps-settings-directory','bad-map','checksum','wrong-owner','bad-payload','junction')){
    $key=if($kind -cin @('bad-payload','serps-unknown-directory','serps-settings-directory')){'SerpsMods'}else{'APIShared'}
    $case=New-Case $kind $key;Ready $case;$old=Backup $case;$new=Backup $case '1.0.1'
    switch($kind){
        'unknown'{Put (Join-Path $old 'personal.txt') 'keep'}
        'hidden'{$path=Join-Path $old 'personal.txt';Put $path 'keep';[IO.File]::SetAttributes($path,[IO.FileAttributes]::Hidden)}
        'empty-directory'{[void][IO.Directory]::CreateDirectory((Join-Path $old 'personal'))}
        'serps-unknown-directory'{[void][IO.Directory]::CreateDirectory((Join-Path $old 'personal'))}
        'serps-settings-directory'{[void][IO.Directory]::CreateDirectory((Join-Path $old 'Payload/BepInEx/plugins/SerpsMods_Serp/config'))}
        'bad-map'{Put (Join-Path $old 'APIShared.map') 'not a map'}
        'checksum'{Put (Join-Path $old 'APIShared.map.sha256') (('0'*64)+'  APIShared.map')}
        'wrong-owner'{$foreign=[pscustomobject]@{Key='APIShared';Guid='OtherMod'};New-Contents $old $foreign '1.0.0'}
        'bad-payload'{Put (Join-Path $old 'Payload/BepInEx/plugins/SerpsMods_Serp/Example.dll') 'modified'}
        'junction'{$outside=Join-Path $case.Root 'outside';Put (Join-Path $outside 'personal.txt') 'keep';$null=New-Item -ItemType Junction -Path (Join-Path $old 'linked') -Target $outside}
    }
    $result=Run $case @([IO.Path]::GetFileName($old))
    Check ($result.Deferred -gt 0 -and (Test-Path -LiteralPath $old) -and (Test-Path -LiteralPath $new)) ('Unsafe backup not protected: '+$kind)
}
$case=New-Case 'bad-proof';Ready $case;$old=Backup $case;$new=Backup $case
Put (Join-Path $case.Destination 'preview.png') 'changed after proof'
$result=Run $case @([IO.Path]::GetFileName($old))
Check ($result.Removed -eq 0 -and $result.Deferred -gt 0 -and (Test-Path -LiteralPath $old)) 'Changed preparation allowed pruning.'
Proof $case
$result=Run $case @([IO.Path]::GetFileName($old))
Check ($result.Removed -eq 1) 'Retry after repaired preparation failed.'
$case=New-Case 'multiple-new';Ready $case;$a=Backup $case;$b=Backup $case
$result=Run $case @()
Check ($result.Removed -eq 0 -and $result.Deferred -gt 0) 'Ambiguous simultaneous backups were deleted.'
$case=New-Case 'tie';Ready $case;$a=Backup $case;$b=Backup $case
[IO.Directory]::SetLastWriteTimeUtc($a,[DateTime]::Parse('2020-01-01T00:00:00Z').ToUniversalTime());[IO.Directory]::SetLastWriteTimeUtc($b,[DateTime]::Parse('2020-01-01T00:00:00Z').ToUniversalTime())
$keep=@($a,$b)|Sort-Object|Select-Object -First 1
$result=Run $case
Check ($result.Removed -eq 1 -and (Test-Path -LiteralPath $keep)) 'Timestamp ties are not deterministic.'
$case=New-Case 'test-isolation' 'APIShared' $true;Ready $case;$a=Backup $case;$b=Backup $case
$prod=$case.Config.APIShared.Folder+'.previous-'+[guid]::NewGuid().ToString('N');New-Contents $prod $case.Target '0.1.0'
$result=Run $case @([IO.Path]::GetFileName($a))
Check ($result.Removed -eq 1 -and (Test-Path -LiteralPath $prod)) 'Test pruning changed production outputs.'
$case=New-Case 'locked';Ready $case;$old=Backup $case;$jobPath=Pending $case $old
$stream=[IO.File]::Open((Join-Path $old 'APIShared.map'),[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try{$result=Run $case;Check ($result.Deferred -gt 0 -and (Test-Path -LiteralPath $jobPath)) 'Locked deletion lost its pending job.'}finally{$stream.Dispose()}
Resume $case
Check (-not (Test-Path -LiteralPath $old) -and -not (Test-Path -LiteralPath $jobPath)) 'Unlocked pending job did not finish.'
$case=New-Case 'abrupt-process' 'SerpsMods';Ready $case;$old=Backup $case;$jobPath=Pending $case $old
$info=[Diagnostics.ProcessStartInfo]::new('powershell.exe',('-NoProfile -ExecutionPolicy Bypass -File "'+$PSCommandPath+'" -ResumeFixture "'+(Join-Path $case.Root 'case.json')+'" -PauseAfterDelete'))
$info.UseShellExecute=$false;$info.CreateNoWindow=$true;$info.RedirectStandardOutput=$true;$info.RedirectStandardError=$true
$process=[Diagnostics.Process]::Start($info);$output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
try{$watch=[Diagnostics.Stopwatch]::StartNew();while(-not [IO.File]::Exists((Join-Path $case.Root 'deleted.flag'))){if($process.HasExited){throw ('Owned deletion child exited: '+$output.Result+$errors.Result)};if($watch.Elapsed.TotalSeconds -gt 20){throw 'Owned deletion child did not reach its checkpoint.'};Start-Sleep -Milliseconds 25};$process.Kill();[void]$process.WaitForExit(5000)}finally{if(-not $process.HasExited){$process.Kill();[void]$process.WaitForExit(5000)};$process.Dispose()}
Check (Test-Path -LiteralPath $jobPath) 'Abrupt deletion lost its job.'
Resume $case
Check (-not (Test-Path -LiteralPath $old)) 'Deletion did not resume after process termination.'
foreach($kind in @('digest','escape','active','unknown-file','changed-file')){
    $case=New-Case ('job-'+$kind);Ready $case;$old=Backup $case;$jobPath=Pending $case $old
    $job=[IO.File]::ReadAllText($jobPath)|ConvertFrom-Json
    switch($kind){
        'digest'{$job.Digest='0'*64;Write-SteamWorkshopJson $jobPath $job}
        'escape'{$job.DirectoryName='../outside';$job.Digest=Get-WorkshopRetentionJobHash $job;Write-SteamWorkshopJson $jobPath $job}
        'active'{$job.DirectoryName=[IO.Path]::GetFileName($case.Destination);$job.Digest=Get-WorkshopRetentionJobHash $job;Write-SteamWorkshopJson $jobPath $job}
        'unknown-file'{Put (Join-Path $old 'private.txt') 'keep'}
        'changed-file'{Put (Join-Path $old 'preview.png') 'changed'}
    }
    $result=Run $case
    Check ($result.Removed -eq 0 -and $result.Deferred -gt 0 -and (Test-Path -LiteralPath $old)) ('Unsafe pending deletion accepted: '+$kind)
}
$case=New-Case 'concurrency';Ready $case;$old=Backup $case
$lock=Open-CaseLock $case
try{$failed=$false;try{$other=Open-CaseLock $case;$other.Dispose()}catch [IO.IOException]{$failed=$true};Check $failed 'Concurrent workflow acquired the lock.';$result=Invoke-WorkshopOutputRetention $case.Config $case.Target $false @() $null;Check ($result.Deferred -gt 0 -and (Test-Path -LiteralPath $old)) 'Cleanup without owned lock proceeded.'}finally{$lock.Dispose()}
$case=New-Case 'unfinished';Ready $case;$a=Backup $case;$b=Backup $case
$unfinished=$case.Destination+'.ready-'+[guid]::NewGuid().ToString('N');Put (Join-Path $unfinished 'keep.txt') 'unfinished'
$unknown=$case.Destination+'.previous-manual';Put (Join-Path $unknown 'keep.txt') 'unknown'
$result=Run $case @([IO.Path]::GetFileName($a))
Check ($result.Removed -eq 1 -and $result.Deferred -eq 2 -and (Test-Path -LiteralPath $unfinished) -and (Test-Path -LiteralPath $unknown)) 'Unfinished/unrecognized output was not protected and reported.'
$case=New-Case 'changed-new';Ready $case;$a=Backup $case;$b=Backup $case
Put (Join-Path $b 'personal.txt') 'keep'
$result=Run $case @([IO.Path]::GetFileName($a))
Check ($result.Removed -eq 0 -and (Test-Path -LiteralPath $a)) 'Invalid newest predecessor caused older valid backups to be lost.'
$case=New-Case 'explicit-existing';Ready $case;$names=@();foreach($i in 1..3){$names+=@([IO.Path]::GetFileName((Backup $case ('0.0.'+$i))))}
$current=Get-WorkshopFileHash (Join-Path $case.Destination 'APIShared.map')
$result=Run $case -DeleteOnly $names
Check ($result.Removed -eq 3 -and (Get-WorkshopFileHash (Join-Path $case.Destination 'APIShared.map')) -ceq $current) 'Explicit one-time pruning changed current output.'
Write-Host ('PASS: '+$checks+' output-retention checks, 100 generations per package, legacy and loose packages, hidden/unknown files, junctions, pins, locking and real process interruption. Fixtures: '+$fixture)
} finally {$env:SHCDE_API_SHARED_MAIN_SELECTION=$savedSelection}
