[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$DesktopHostSource,
    [Parameter(Mandatory)][string]$MakeDesyncPackage,
    [Parameter(Mandatory)][string]$UiAcceptancePackage,
    [string]$ClientRoot = '\\LENOVO_SERP\Stronghold Crusader Definitive Edition'
)

$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $workspace 'Shared/Tools/Paths/WorkPaths.ps1')
. (Join-Path $workspace 'Shared/Tools/ApiSharedRepository/RepositoryProcess.Common.ps1')
. (Join-Path $workspace 'Shared/Tools/Automation/InGameTest.Transaction.ps1')
Add-Type -Path (Resolve-Path -LiteralPath $DesktopHostSource).Path

$hostRoot = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$gameName = 'Stronghold Crusader Definitive Edition'
$exe = Join-Path $hostRoot ($gameName + '.exe')
$desktop = [IntPtr]::Zero
$instance = $null
$label = 'paired-' + [Guid]::NewGuid().ToString('N').Substring(0,12)
$crlf = [string][char]13 + [char]10
$utf8 = [Text.UTF8Encoding]::new($false)
$run = New-ShortWorkDirectory -Root $workspace -Kind a -Name paired-resync -Identity $label
$backup = [IO.Directory]::CreateDirectory((Join-Path $run 'rollback')).FullName
$snapshots = [Collections.Generic.List[object]]::new()
$installs = [Collections.Generic.List[object]]::new()
$installed = $false
$testPaths = [Collections.Generic.List[object]]::new()
$mutex = [Threading.Mutex]::new($false,'Local\SHCDE.InGameTest.3024040')
$locked = $false
$hostSummary = $null
$clientSummary = $null
$clientExited = $false
$errorMessage = $null

function Write-ProgressLine([string]$Message) {
    Write-Host ('[{0:HH:mm:ss.fff}] {1}' -f [DateTime]::Now,$Message)
}

function Save-TestPath([string]$Path, [string]$Name, [switch]$Directory, [switch]$Client) {
    $snapshot = Save-InGameTestState $Path (Join-Path $backup $Name) -Directory:$Directory
    $snapshots.Add([pscustomobject]@{State=$snapshot; Client=[bool]$Client})
}

function Publish-Checkpoint([string]$Name, [string[]]$Lines, [switch]$Client) {
    $folder = if($Client){$clientUi}else{$hostUi}
    $path = Join-Path $folder ($label+'-'+$Name+'.ok')
    $temporary = $path+'.tmp'
    foreach($file in @($path,$temporary)){$testPaths.Add([pscustomobject]@{Path=$file; Client=[bool]$Client})}
    [IO.File]::WriteAllLines($temporary,$Lines,$utf8)
    [IO.File]::Move($temporary,$path)
}

function Assert-GameClosed([string]$Root) {
    $stream = [IO.File]::Open((Join-Path $Root 'BepInEx/LogOutput.log'),'Open','Read','Read')
    $stream.Dispose()
}

function Install-TestPackage([string]$Source, [string]$Root) {
    $manifest = Get-Content -LiteralPath (Join-Path $Source 'info.json') -Raw | ConvertFrom-Json
    if($manifest.GUID -notin @('MakeDesync_Serp','UiAcceptance_Serp')){throw 'Unexpected test package.'}
    $plugins = Join-Path $Root 'BepInEx/plugins'
    $target = Join-Path $plugins $manifest.GUID
    Assert-InGameTestUnlinked $target
    $null = [IO.Directory]::CreateDirectory($target)
    foreach($file in Get-ChildItem -LiteralPath $Source -File){
        if($file.Name -notin @('info.json','MakeDesync.dll','MakeDesync.pdb','UiAcceptance.dll','UiAcceptance.pdb')){continue}
        $path = Join-Path $target $file.Name
        $expected = (Get-FileHash -LiteralPath $file.FullName).Hash
        $old = [IO.File]::Exists($path)
        if($old -and (Get-FileHash -LiteralPath $path).Hash -eq $expected){continue}
        $item = [pscustomobject]@{Path=$path; Existed=$old; Temporary=$path+'.'+$label+'.tmp'; Backup=$path+'.'+$label+'.bak'; Applied=$false}
        $installs.Add($item)
        [IO.File]::Copy($file.FullName,$item.Temporary)
        if((Get-FileHash -LiteralPath $item.Temporary).Hash -ne $expected){throw ('Transfer failed: '+$path)}
        if($old){[IO.File]::Replace($item.Temporary,$path,$item.Backup,$true)}
        else{[IO.File]::Move($item.Temporary,$path)}
        $item.Applied = $true
        if((Get-FileHash -LiteralPath $path).Hash -ne $expected){throw ('Verification failed: '+$path)}
    }
}

function Read-Recent([string]$Path) {
    if([IO.File]::Exists($Path)){return @(Get-Content -LiteralPath $Path -Tail 1500)}
    return @()
}

function Save-LogExcerpt([string]$Path, [string]$Name) {
    if(-not [IO.File]::Exists($Path)){return}
    $all = @(Get-Content -LiteralPath $Path)
    $begin = $all.Length
    for($i=$all.Length-1;$i -ge 0;$i--){
        if($all[$i] -match ('run='+[regex]::Escape($label)+' ')){$begin=$i}
        if($begin -lt $all.Length -and $all[$i] -match '\[Message:.*BepInEx.*- Stronghold Crusader Definitive Edition'){break}
    }
    if($begin -ge $all.Length){return}
    $excerpt = @($all[$begin..($all.Length-1)] | Where-Object {$_ -match 'ACCEPT run=|MP_HOST|MAKE_DESYNC|RESYNC_DATA|MARKET_STALE_NATIVE|MARKET_PENDING|PAIRED_|MP_RESTART_TEST|\[Error'})
    [IO.File]::WriteAllLines((Join-Path $run $Name),[string[]]$excerpt,$utf8)
}

try {
    try{$locked=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$locked=$true}
    if(-not $locked){throw 'Another test is using the host game.'}
    if(Get-Process -Name $gameName -ErrorAction SilentlyContinue){throw 'Host game is already running.'}
    if(-not (Get-Process -Name steam -ErrorAction SilentlyContinue)){throw 'Host Steam is not running in the interactive session.'}
    Assert-GameClosed $hostRoot
    Assert-GameClosed $ClientRoot

    $hostConfig = Join-Path $hostRoot 'BepInEx/config'
    $clientConfig = Join-Path $ClientRoot 'BepInEx/config'
    $hostUi = Join-Path $hostConfig 'UiAcceptance'
    $clientUi = Join-Path $clientConfig 'UiAcceptance'
    $hostLog = Join-Path $hostRoot 'BepInEx/LogOutput.log'
    $clientLog = Join-Path $ClientRoot 'BepInEx/LogOutput.log'
    $steamAppId = Join-Path $hostRoot 'steam_appid.txt'
    $hostSettings = Join-Path $env:USERPROFILE ('AppData/LocalLow/Firefly Studios/'+$gameName+'/settings.cfg')
    $apiSettings = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'SHCDE/APIShared/BugfixesAndQoL_Serp'
    $lordSource = Join-Path $workspace 'Testmods/AIMarketAcceptance/Fixtures/SerpMarketRest4'
    $lordTarget = Join-Path ([IO.Path]::GetDirectoryName($hostSettings)) 'CustomLords/SerpMarketRest4'
    $lordInventory = Get-InGameTestTree $lordSource
    if($lordInventory.Files.Count -ne 2 -or $lordInventory.Directories.Count -ne 0 -or
        $lordInventory.Files['SerpMarketRest4.lordjson'] -ne '17b809e9ea015a5181efd5f76cc2908dca50166bd68787db3c7588dbc08c4a5b' -or
        $lordInventory.Files['SerpMarketRest4.aivjson'] -ne '84bd02e51d2da4abb5c6ae5fbe76820a96b99ac4dfedf8760a6bcd26c8951806'){
        throw 'The trading AI AIC or AIV differs from the reviewed fixture.'
    }
    $installedLord = Get-InGameTestTree $lordTarget
    if($installedLord.Exists){Assert-InGameTestTree $lordTarget $lordInventory}

    Save-TestPath $hostSettings 'host-settings.cfg'
    Save-TestPath $steamAppId 'host-appid.txt'
    Save-TestPath $apiSettings 'host-bfq-settings' -Directory
    Save-TestPath (Join-Path $hostConfig 'UiAcceptance_Serp.cfg') 'host-ui.cfg'
    Save-TestPath (Join-Path $clientConfig 'UiAcceptance_Serp.cfg') 'client-ui.cfg' -Client
    Save-TestPath (Join-Path $clientUi 'join-lobby.txt') 'client-join-lobby.txt' -Client
    if(-not $installedLord.Exists){Save-TestPath $lordTarget 'host-custom-trading-ai' -Directory}
    [IO.File]::WriteAllText((Join-Path $run 'recovery.json'),(($snapshots | ConvertTo-Json -Depth 7) -replace '(?<!\r)\n',$crlf)+$crlf,$utf8)

    if(-not $installedLord.Exists){Copy-InGameTestTree $lordSource $lordTarget $lordInventory}

    foreach($root in @($hostRoot,$ClientRoot)){
        Install-TestPackage $MakeDesyncPackage $root
        Install-TestPackage $UiAcceptancePackage $root
    }
    $installed = $true
    Write-ProgressLine "Installed and verified both test mods on host and client. Run $label."

    foreach($side in @('host','client')){
        $scenarioName = 'paired-resync-'+$side+'.json'
        $config = if($side -eq 'host'){$hostConfig}else{$clientConfig}
        $scenarioDir = Join-Path $config 'UiAcceptance'
        $null = [IO.Directory]::CreateDirectory($scenarioDir)
        $scenarioPath = Join-Path $scenarioDir ($label+'-'+$side+'.json')
        $testPaths.Add([pscustomobject]@{Path=$scenarioPath; Client=($side -eq 'client')})
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('../scenarios/'+$scenarioName)) -Destination $scenarioPath
        $cfgLines = @('[Run]','Enabled = true',('Scenario = '+[IO.Path]::GetFileName($scenarioPath)),('RunLabel = '+$label),'')
        [IO.File]::WriteAllText((Join-Path $config 'UiAcceptance_Serp.cfg'),($cfgLines -join $crlf),$utf8)
    }
    $joinFile = Join-Path $clientUi 'join-lobby.txt'
    if([IO.File]::Exists($joinFile)){[IO.File]::Delete($joinFile)}
    if([IO.File]::Exists($steamAppId)){
        if([IO.File]::ReadAllText($steamAppId).Trim() -ne '3024040'){throw 'Host steam_appid.txt does not match the game.'}
    }else{[IO.File]::WriteAllText($steamAppId,('3024040'+$crlf),$utf8)}
    $text = [IO.File]::ReadAllText($hostSettings)
    foreach($key in @('MasterVolume','SFXVolume','SpeechVolume','UnitSpeechVolume','MusicVolume')){
        if($text -notmatch ('(?m)^'+$key+':')){throw ('Missing audio setting: '+$key)}
        $text = [regex]::Replace($text,('(?m)^'+$key+':[^\r\n]*'),($key+':0'))
    }
    [IO.File]::WriteAllText($hostSettings,$text,$utf8)

    $inputDesktop = [InGameTestDesktopHost]::InputDesktop()
    $desktopName = 'SerpTest_'+$label
    $desktop = [InGameTestDesktopHost]::CreateDesktopW($desktopName,[IntPtr]::Zero,[IntPtr]::Zero,0,0xC3,[IntPtr]::Zero)
    if($desktop -eq [IntPtr]::Zero){throw [ComponentModel.Win32Exception]::new()}
    $launchArguments = '-screen-fullscreen 0 -screen-width 1280 -screen-height 720 -logFile "'+(Join-Path $run 'Player.log')+'"'
    $instance = [InGameTestDesktopHost]::Start($exe,$launchArguments,$hostRoot,$desktopName)
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $seenWindow = $false
    $published = $false
    $readySent = $false
    $fixtureSent = $false
    $terrainAborted = $false
    $sent = @{}
    $runRegex = 'ACCEPT run='+[regex]::Escape($label)+' step='
    Write-ProgressLine ('Started host on separate desktop, PID '+$instance.Id)
    while(-not $instance.HasExited -and $watch.Elapsed.TotalMinutes -lt 26){
        if([InGameTestDesktopHost]::InputDesktop() -cne $inputDesktop -or [InGameTestDesktopHost]::HasInputWindow($instance.Id)){
            throw 'Host reached the input desktop or foreground.'
        }
        if(-not $seenWindow){
            $seenWindow = @([InGameTestDesktopHost]::Windows($desktop) | Where-Object {$_.Pid -eq $instance.Id}).Count -gt 0
            if(-not $seenWindow -and $watch.Elapsed.TotalSeconds -gt 80){throw 'Host window did not appear on the isolated desktop.'}
        }
        $hostLines = @(Read-Recent $hostLog)
        if(-not $published -and $seenWindow -and $hostLines -match ($runRegex+'map PASS ')){
            $hosted = @($hostLines | Select-String -Pattern ('MP_HOST lobby=(\d+) run='+[regex]::Escape($label)+'\b')) | Select-Object -Last 1
            if($hosted){
                [IO.File]::WriteAllText($joinFile,$hosted.Matches[0].Groups[1].Value,$utf8)
                $published = $true
                Write-ProgressLine 'PRIVATE_LOBBY_READY. Start the client game now. The client scenario joins automatically.'
            }
        }
        if($published -and -not $readySent -and $hostLines -match ($runRegex+'peace-readback PASS ')){
            $profile = $hostLines | Select-String -Pattern ('PAIRED_AI_PROFILE run='+[regex]::Escape($label)+' .*digest=([A-F0-9]{64})\b') | Select-Object -Last 1
            $digest = if($profile){$profile.Matches[0].Groups[1].Value}else{'unavailable'}
            Publish-Checkpoint 'host-setup' @($label,$digest) -Client
            $readySent = $true
            Write-ProgressLine 'Client joined. The host published the test setup.'
        }
        if($published){
            $clientLines = @(Read-Recent $clientLog)
            if(-not $fixtureSent){
                $point = '(none|\d+,\d+)'
                $fixture = $hostLines | Select-String -Pattern ('PAIRED_FIXTURE run='+[regex]::Escape($label)+' moat='+$point+' hutA='+$point+' hutB='+$point) | Select-Object -Last 1
                if($fixture){
                    $fields = $fixture.Matches[0].Groups
                    Publish-Checkpoint 'fixture' @($label,$fields[1].Value,$fields[2].Value,$fields[3].Value) -Client
                    $fixtureSent = $true
                }elseif($hostLines -match ($runRegex+'fixture FAIL ')){
                    Publish-Checkpoint 'fixture' @($label,'none','none','none') -Client
                    $fixtureSent = $true
                }
            }
            if(-not $terrainAborted -and ($hostLines -match ($runRegex+'(?:dig|client-moat|fill|client-soil) FAIL ') -or
                $clientLines -match ($runRegex+'(?:dug|moat|restored-terrain|soil) FAIL '))){
                Publish-Checkpoint 'terrain-aborted' @($label) -Client
                Publish-Checkpoint 'terrain-aborted' @($label)
                $terrainAborted = $true
            }
            foreach($checkpoint in @('moat','soil')){
                if(-not $sent[$checkpoint] -and $clientLines -match ($runRegex+$checkpoint+' PASS ')){
                    Publish-Checkpoint $checkpoint @($label)
                    $sent[$checkpoint] = $true
                    Write-ProgressLine ('Confirmed client checkpoint: '+$checkpoint)
                }
            }
            $clientSummary = $clientLines -match ($runRegex+'SUMMARY ') | Select-Object -Last 1
        }
        $hostSummary = $hostLines -match ($runRegex+'SUMMARY ') | Select-Object -Last 1
        if($hostSummary -and $clientSummary){break}
        Start-Sleep -Milliseconds 800
    }
    if(-not $hostSummary){$hostSummary = @(Read-Recent $hostLog) -match ($runRegex+'SUMMARY ') | Select-Object -Last 1}
    if($hostSummary){ Write-ProgressLine $hostSummary }
    if($clientSummary){ Write-ProgressLine $clientSummary }
    if(-not $hostSummary -or -not $clientSummary){throw 'A paired scenario did not reach its final summary.'}
    if($hostSummary -notmatch 'SUMMARY PASS ' -or $clientSummary -notmatch 'SUMMARY PASS '){throw 'A paired scenario reported a failure.'}
} catch {
    $errorMessage = $_.Exception.Message
    Write-ProgressLine ('FAILED: '+$errorMessage)
} finally {
    for($i=$installs.Count-1; $i -ge 0; $i--){
        $item = $installs[$i]
        if([IO.File]::Exists($item.Backup)){
            if($installed){[IO.File]::Delete($item.Backup)}
            else{[IO.File]::Move($item.Backup,$item.Path,$true)}
        }elseif(-not $installed -and $item.Applied -and -not $item.Existed){[IO.File]::Delete($item.Path)}
        if([IO.File]::Exists($item.Temporary)){[IO.File]::Delete($item.Temporary)}
    }
    if($instance){
        if(-not $instance.HasExited -and -not $instance.WaitForExit(20000)){$instance.Kill();$null=$instance.WaitForExit(10000)}
        $instance.Dispose()
    }
    if($desktop -ne [IntPtr]::Zero){$null=[InGameTestDesktopHost]::CloseDesktop($desktop)}
    if($hostLog){try{Save-LogExcerpt $hostLog 'host.log'}catch{Write-Warning $_.Exception.Message}}
    if($clientLog){
        try{Save-LogExcerpt $clientLog 'client.log'}catch{Write-Warning $_.Exception.Message}
        $clientWait = [Diagnostics.Stopwatch]::StartNew()
        do{
            try{Assert-GameClosed $ClientRoot; $clientExited=$true}catch{Start-Sleep -Milliseconds 800}
        }while(-not $clientExited -and $clientWait.Elapsed.TotalSeconds -lt 25)
    }
    $hostExited = -not (Get-Process -Name $gameName -ErrorAction SilentlyContinue)
    try{
        foreach($snapshot in $snapshots){
            if(($snapshot.Client -and $clientExited) -or (-not $snapshot.Client -and $hostExited)){Restore-InGameTestState $snapshot.State}
        }
        foreach($file in $testPaths){
            if(($file.Client -and $clientExited) -or (-not $file.Client -and $hostExited)){[IO.File]::Delete($file.Path)}
        }
        if(-not $hostExited -or ($clientLog -and -not $clientExited)){Write-Warning ('Game still holds its log. Deferred recovery: '+$backup)}
    } finally {
        [IO.File]::WriteAllText((Join-Path $run 'result.json'),(([ordered]@{
            Run=$label; Error=$errorMessage; HostSummary=$hostSummary; ClientSummary=$clientSummary; HostExited=$hostExited
            ClientExited=$clientExited; PackagesInstalled=$installed
        } | ConvertTo-Json) -replace '(?<!\r)\n',$crlf)+$crlf,$utf8)
        if($locked){$mutex.ReleaseMutex()}
        $mutex.Dispose()
    }
    Write-ProgressLine ('Evidence: '+$run)
}
if($errorMessage){throw $errorMessage}
