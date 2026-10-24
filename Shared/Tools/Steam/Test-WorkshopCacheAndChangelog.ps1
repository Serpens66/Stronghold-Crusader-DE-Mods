Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
. (Join-Path $PSScriptRoot 'ReleaseAssetCache.ps1')
. (Join-Path $PSScriptRoot 'WorkshopChangelog.ps1')
$checks = 0
function Check([bool]$Condition, [string]$Message) { if (-not $Condition) { throw $Message }; $script:checks++ }
function Reject([scriptblock]$Action) { $failed=$false; try { & $Action | Out-Null } catch { $failed=$true }; Check $failed 'Expected rejection.' }
function Import-Function([string]$File, [string[]]$Names) {
    $ast = [Management.Automation.Language.Parser]::ParseFile($File, [ref]$null, [ref]$null)
    foreach ($name in $Names) {
        $node = $ast.Find({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst] -and $n.Name -ceq $name}, $true)
        if (-not $node) { throw "Missing function: $name" }
        . ([scriptblock]::Create($node.Extent.Text.Replace('function '+$name+' {','function script:'+$name+' {')))
    }
}
Import-Function (Join-Path $PSScriptRoot 'Upload-Workshop.ps1') @('Get-Sha256','Get-UploadedModVersions','Get-WorkshopChangeNote','Save-ChangelogState')
Import-Function (Join-Path $PSScriptRoot 'Create-SteamModPack.ps1') @('Get-HistoryMapPath','Test-StableReleaseTag','Get-PackReleaseState','Get-ReleasePackage')
function Stop-Upload($Code, $Message) { throw $Message }
# APIShared Workshop notes list every version since the last upload, newest first.
$apiLog=@([pscustomobject]@{Version='0.3.0';Changes=@('third')},[pscustomobject]@{Version='0.1.0';Changes=@('first')},[pscustomobject]@{Version='0.2.0';Changes=@('second a','second b')})
$apiNote=Get-SerpChangelogSince $apiLog '0.3.0' '0.1.0' 'APIShared'
Check ($apiNote -ceq "### v0.3.0`r`n`r`n- third`r`n`r`n### v0.2.0`r`n`r`n- second a`r`n- second b") 'APIShared note must list all versions since the last upload, newest first.'
Reject {Get-SerpChangelogSince $apiLog '0.2.0' '0.3.0' 'APIShared'}
function Fail-Pack($Code, $Message) { throw $Message }
function Write-UploadLog($Message, $Level) { }
. (Join-Path $PSScriptRoot 'SteamPackPolicy.ps1')
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
. (Join-Path $PSScriptRoot '../Paths/WorkPaths.ps1')
$fixture = New-ShortWorkDirectory -Root ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))) -Kind t -Name 'WorkshopCacheAndChangelo'
[void][IO.Directory]::CreateDirectory($fixture)
$cache = Join-Path $fixture 'cache'
$script:downloads = 0
$remoteText = 'original English text'
$source = Join-Path $fixture 'remote.zip'
[IO.File]::WriteAllText($source, $remoteText)
$expected = Get-WorkshopFileHash $source
$validator = {param($path) Test-ReleaseAssetHash $path $expected}
$download = {param($stage, $asset) $script:downloads++; Copy-Item -LiteralPath $source -Destination (Join-Path $stage $asset)}
$asset = Get-VerifiedReleaseAsset $cache 'mod.zip' $validator $download
Check ($downloads -eq 1) 'Cold cache must download exactly once.'
[void](Get-VerifiedReleaseAsset $cache 'mod.zip' $validator $download)
Check ($downloads -eq 1) 'Warm valid cache downloaded again.'
[IO.File]::WriteAllText($asset, 'damaged')
[void](Get-VerifiedReleaseAsset $cache 'mod.zip' $validator $download)
Check ($downloads -eq 2 -and (Test-ReleaseAssetHash $asset $expected)) 'Corrupt cache was not repaired.'
Reject { Get-VerifiedReleaseAsset $cache 'mod.zip' {param($p) $false} {param($stage,$name) [IO.File]::WriteAllText((Join-Path $stage $name),'bad remote')} }
Check (Test-ReleaseAssetHash $asset $expected) 'Rejected download replaced existing cache.'
Reject { Get-VerifiedReleaseAsset $cache 'mod.zip' {param($p) $false} {throw 'simulated network failure'} }
Check (Test-ReleaseAssetHash $asset $expected) 'Network failure changed existing cache.'
Reject { Get-VerifiedReleaseAsset $cache '../escape' $validator $download }
Reject { Get-VerifiedReleaseAsset $cache 'mod*' $validator $download }
$before=$downloads
[void](Get-VerifiedReleaseAsset (Join-Path $fixture 'another cache') 'mod.zip' $validator $download @($source))
Check ($downloads -eq $before) 'Existing local release asset was downloaded instead of reused.'
$shaSource = Join-Path $fixture 'remote.sha256'
[IO.File]::WriteAllText($shaSource, "$expected  mod.zip")
[void](Get-VerifiedReleaseAsset $cache 'mod.zip.sha256' {param($p) Test-ReleaseChecksum $p $expected} {
    param($stage,$name) $script:downloads++; Copy-Item -LiteralPath $shaSource -Destination (Join-Path $stage $name)
})
Check ($downloads -eq 3 -and (Test-ReleaseAssetHash $asset $expected)) 'Missing checksum downloaded the ZIP again.'

# Exercise real preparation functions with a simulated gh, not an alternative implementation.
$script:OutputRoot = Join-Path $fixture 'SerpsMods'
$script:Repository = 'Serpens66/Stronghold-Crusader-DE-Mods'
$PackGuid = 'SerpsMods_Serp'; $TestMode = $false
$script:remoteDirectory = Join-Path $fixture 'release'; [void][IO.Directory]::CreateDirectory($remoteDirectory)
$script:remoteRelease = [pscustomobject]@{tagName='SerpsMods/v1.2.3';isDraft=$false;targetCommitish=('a'*40)}
function Invoke-Checked {
    param($FilePath, $Arguments, $FailureCode)
    if ($FilePath -cne 'gh') { throw 'Unexpected external command.' }
    if ($Arguments[1] -ceq 'view') { return [pscustomobject]@{Output=@(($script:remoteRelease | ConvertTo-Json -Compress));ExitCode=0} }
    if ($Arguments[1] -cne 'download') { throw 'Unexpected gh mutation.' }
    $script:downloads++
    $name = $Arguments[[Array]::IndexOf($Arguments,'--pattern')+1]
    $stage = $Arguments[[Array]::IndexOf($Arguments,'--dir')+1]
    Copy-Item -LiteralPath (Join-Path $script:remoteDirectory $name) -Destination (Join-Path $stage $name)
    return [pscustomobject]@{Output=@();ExitCode=0}
}
$mapSource = Join-Path $remoteDirectory 'SerpsMods.map'; [IO.File]::WriteAllText($mapSource,'historical map')
$mapHash = Get-WorkshopFileHash $mapSource
$upload = [pscustomobject]@{PackVersion='1.2.3';MapSha256=$mapHash}
$first = Get-HistoryMapPath $upload; $before=$downloads
Check ((Get-HistoryMapPath $upload) -ceq $first -and $downloads -eq $before) 'Historical cache was downloaded again.'
$local = Join-Path $OutputRoot 'v1.2.3/SerpsMods.map'; [void][IO.Directory]::CreateDirectory((Split-Path $local)); [IO.File]::WriteAllText($local,'locally modified')
Check ((Get-HistoryMapPath $upload) -ceq $first -and [IO.File]::ReadAllText($local) -ceq 'locally modified') 'Modified local map was overwritten.'
[IO.File]::WriteAllText((Join-Path $remoteDirectory 'SerpsMods.map.sha256'),"$mapHash  SerpsMods.map")
$packProof = [ordered]@{Pack='SerpsMods';PackGuid=$PackGuid;Version='1.2.3';Commit=('a'*40);Map=@{File='SerpsMods.map';Sha256=$mapHash;Size=(Get-Item $mapSource).Length}}
Write-SteamWorkshopJson (Join-Path $remoteDirectory 'SerpsMods.provenance.json') $packProof
$releases = @([pscustomobject]@{tagName='SerpsMods/v1.2.3';isDraft=$false;publishedAt='2026-10-01'})
$pack = Get-PackReleaseState $releases; $before=$downloads
$null = Get-PackReleaseState $releases
Check ($downloads -eq $before) 'Previous modpack assets downloaded again.'
# Manually published prereleases (e.g. v1.0.29BETA) never become the previous pack baseline.
$beta=[pscustomobject]@{tagName='SerpsMods/v1.2.4BETA';isDraft=$false;isPrerelease=$true;publishedAt='2026-10-02'}
Check ((Get-PackReleaseState (@($releases)+$beta)).Version -ceq '1.2.3' -and $downloads -eq $before) 'A manual prerelease replaced the stable pack baseline.'
[IO.File]::WriteAllText($pack.MapShaPath,'corrupt checksum')
$null = Get-PackReleaseState $releases
Check ($downloads -eq $before+1) 'Checksum repair redownloaded other pack files.'
$remoteRelease.targetCommitish='b'*40
Reject {Get-PackReleaseState $releases}
Check ((Get-WorkshopFileHash (Join-Path (Split-Path $pack.MapPath) 'SerpsMods.provenance.json')) -ceq
    (Get-WorkshopFileHash (Join-Path $remoteDirectory 'SerpsMods.provenance.json'))) 'Wrong commit replaced cached provenance.'
$remoteRelease.targetCommitish='a'*40

# Current individual-release cache: exercise ZIP, identity and checksum selection.
$existing = Join-Path $root '.release-output/SerpsMods/cache/BugfixesAndQoL'
if (Test-Path -LiteralPath $existing) {
    $proofFile = Get-ChildItem -LiteralPath $existing -Filter '*.provenance.json' -Recurse | Select-Object -First 1
    if ($proofFile) {
        $individual = [IO.File]::ReadAllText($proofFile.FullName) | ConvertFrom-Json
        $base = 'BugfixesAndQoL-v'+$individual.Version
        foreach($name in @("$base.zip","$base.zip.sha256","$base.provenance.json")) {
            Copy-Item -LiteralPath (Join-Path $proofFile.DirectoryName $name) -Destination (Join-Path $remoteDirectory $name)
        }
        $remoteRelease.tagName=$individual.Tag; $remoteRelease.targetCommitish=$individual.Commit
        $consumer=[pscustomobject]@{Name='BugfixesAndQoL';TargetVersion=$individual.Version;PluginGuid=$individual.PluginGuid;LatestTag=$individual.Tag}
        $package=Get-ReleasePackage $consumer; $before=$downloads
        $null=Get-ReleasePackage $consumer
        Check ($downloads -eq $before) 'Valid individual release downloaded again.'
        $checksum=Join-Path (Split-Path $package.Zip) "$base.zip.sha256"
        [IO.File]::WriteAllText($checksum,'invalid checksum')
        $null=Get-ReleasePackage $consumer
        Check ($downloads -le $before+1 -and (Test-ReleaseChecksum $checksum $individual.Package.Sha256)) 'Individual checksum repair redownloaded ZIP/provenance.'
        $before=$downloads
        $remoteRelease.isDraft=$true
        Reject {Get-ReleasePackage $consumer}
        $remoteRelease.isDraft=$false
        Check ($downloads -eq $before) 'Wrong release mode downloaded assets.'
        $productionZip=$package.Zip
        $productionRoot=$OutputRoot
        $TestMode=$true; $script:OutputRoot=Join-Path $fixture 'test/SerpsMods'
        $reusedPublic=Get-ReleasePackage $consumer
        Check ($reusedPublic.Zip.StartsWith($OutputRoot+'\',[StringComparison]::OrdinalIgnoreCase) -and $reusedPublic.Zip -cne $productionZip) 'Private test failed to reuse an unchanged public child release in its isolated cache.'
        $consumer.LatestTag='BugfixesAndQoL/test/v'+$individual.Version+'-'+$individual.Commit.Substring(0,12)
        $remoteRelease.tagName=$consumer.LatestTag; $remoteRelease.isDraft=$true
        $individual.Tag=$consumer.LatestTag
        Write-SteamWorkshopJson (Join-Path $remoteDirectory "$base.provenance.json") $individual
        $testPackage=Get-ReleasePackage $consumer
        Check ($testPackage.Zip.StartsWith($OutputRoot+'\',[StringComparison]::OrdinalIgnoreCase) -and $testPackage.Zip -cne $productionZip) 'Test cache mixed with production cache.'
        $before=$downloads; $null=Get-ReleasePackage $consumer
        Check ($downloads -eq $before) 'Valid draft-release cache downloaded again.'
        $script:OutputRoot=$productionRoot; $TestMode=$false
    }
}

# Exact English strings, unordered input, version gaps, first inclusion and downgrade.
$payload = Join-Path $fixture 'package'
$infoPath = Join-Path $payload 'Payload/BepInEx/plugins/SerpsMods_Serp/Mods/Test_Serp/info.json'
$info = [ordered]@{GUID='Test_Serp';Version='1.0.4';SerpChangelog=@(
    @{Version='1.0.2';Changes=@('Keep punctuation, casing and  double spaces.')},
    @{Version='1.0.4';Changes=@('version bump')}, @{Version='1.0.1';Changes=@('Old baseline.')})}
Write-SteamWorkshopJson $infoPath $info
$mod = [pscustomobject]@{Guid='Test_Serp';Name='Test';Version='1.0.4';RelativePath='Mods/Test_Serp';Files=@(@{Path='info.json';Size=(Get-Item $infoPath).Length;Sha256=(Get-WorkshopFileHash $infoPath)})}
$body = Get-PayloadChangelogBody $payload $mod '1.0.1'
Check ($body.Text -ceq "### v1.0.4`r`n`r`n- version bump`r`n`r`n### v1.0.2`r`n`r`n- Keep punctuation, casing and  double spaces.") 'Original English text/order changed.'
Check ([regex]::Matches((Get-PayloadChangelogBody $payload $mod '').Text,'### v').Count -eq 3) 'New mod lost historical sections.'
Check ((Get-PayloadChangelogBody $payload $mod '1.0.4').Text -ceq '') 'Unchanged version emitted duplicate changes.'
Reject { Get-PayloadChangelogBody $payload $mod '1.0.5' }
[IO.File]::AppendAllText($infoPath,' '); Reject { Get-PayloadChangelogBody $payload $mod '1.0.1' }
$info.SerpChangelog += @{Version='1.0.2';Changes=@('Duplicate')}; Write-SteamWorkshopJson $infoPath $info
$mod.Files[0].Size=(Get-Item $infoPath).Length; $mod.Files[0].Sha256=Get-WorkshopFileHash $infoPath
Reject { Get-PayloadChangelogBody $payload $mod '1.0.1' }

# Recover only confirmed baselines, keeping validation/cancelled attempts read-only.
$historyPath=Join-Path $fixture 'history.json'; $seedPath=Join-Path $fixture 'seed.json'
$packProof.Mods=@(@{Guid='Test_Serp';Name='Test';Version='1.0.1';State='Active'})
$reference=Join-Path $fixture 'proof.json'; Write-SteamWorkshopJson $reference $packProof
$history=[ordered]@{SchemaVersion=1;AppId=3024040;StateName='SerpsMods';Uploads=@(@{ItemId='123';PackVersion='1.2.3';MapSha256=$mapHash;UploadedUtc='2026-10-01T00:00:00Z'})}
Write-SteamWorkshopJson $historyPath $history
$baseline=Get-ConfirmedWorkshopChangelogState $null '123' $historyPath $seedPath 3024040 $fixture $reference
Check ($baseline.Mods[0].Version -ceq '1.0.1') 'Confirmed baseline was not recovered.'
Check ($null -eq (Get-ConfirmedWorkshopChangelogState $null '456' $historyPath $seedPath 3024040 $fixture $reference)) 'Foreign item baseline reused.'
$baseline.Mods[0].Version='1.0.2'
Reject {Get-ConfirmedWorkshopChangelogState $baseline '123' $historyPath $seedPath 3024040 $fixture $reference}
$baseline.Mods[0].Version='1.0.1'
$baseline.MapSha256='b'*64; Reject {Get-ConfirmedWorkshopChangelogState $baseline '123' $historyPath $seedPath 3024040 $fixture $reference}
$packProof.Map.Sha256='c'*64; Write-SteamWorkshopJson $reference $packProof
Reject {Get-ConfirmedWorkshopChangelogState $null '123' $historyPath $seedPath 3024040 $fixture $reference}
$statePath=Join-Path $fixture 'changelog-state.json'
$savedNote=[pscustomobject]@{PackVersion='1.2.3';MapSha256=$mapHash;Mods=@();ContentFingerprint=('d'*64)}
Save-ChangelogState $statePath $savedNote '123'
$persisted=[IO.File]::ReadAllText($statePath)|ConvertFrom-Json
Check ($persisted.ContentFingerprint -ceq $savedNote.ContentFingerprint -and $persisted.ItemId -ceq '123') 'Saved upload identity/fingerprint differs from the confirmed result.'

# Current prepared production package: compare every emitted line to its actual manifest.
$config=Get-WorkshopConfiguration
$preparation=Join-Path $root '.release-output/SteamWorkshop/prepared/SerpsMods.json'
# UploadWorkshop removes the temporary release folder after a successful upload; its consumed
# preparation then has no local package left to compare.
$preparedRoot=if(Test-Path -LiteralPath $preparation){$p0=[IO.File]::ReadAllText($preparation)|ConvertFrom-Json;if($p0.PSObject.Properties['ModpackSource']){[string]$p0.ModpackSource.Root}else{''}}else{''}
if($preparedRoot -and -not (Test-Path -LiteralPath $preparedRoot)){Write-Host "SKIP: prepared package folder was removed after its upload: $preparedRoot"}
elseif (Test-Path -LiteralPath $preparation) {
    # Prepared files belong to their selected checkout, even when this test starts on develop.
    $prepared=[IO.File]::ReadAllText($preparation)|ConvertFrom-Json
    if($prepared.PSObject.Properties['PublishedRelease']){
        . (Join-Path $PSScriptRoot 'ModpackSource.Common.ps1')
        $config.Root=(Resolve-ModpackSource $root 'main' -AllowGeneratedOutputs).Root
    }elseif($prepared.PSObject.Properties['ModpackSource']){$config.Root=$prepared.ModpackSource.Root}
    $config.SerpsMods.Folder=Join-Path $config.Root 'SerpsMods'
    $env:SHCDE_MODPACK_COORDINATOR=$root
    $env:SHCDE_MODPACK_BRANCH='main'
    if($prepared.PSObject.Properties['MainSource']){$env:SHCDE_API_SHARED_MAIN_SELECTION=$prepared.MainSource.Selection}
    $proof=Assert-WorkshopPreparation $config $config.SerpsMods $false
    $provenance=[IO.File]::ReadAllText($proof.ReleaseProvenance)|ConvertFrom-Json
    $statePath=Join-Path $root '.release-output/SteamWorkshop/items/3024040-SerpsMods.changelog-state.json'
    $previous=if(Test-Path $statePath){[IO.File]::ReadAllText($statePath)|ConvertFrom-Json}else{$null}
    $note=Get-WorkshopChangeNote (Join-Path $root '.release-output') (Get-Item (Join-Path $config.SerpsMods.Folder 'SerpsMods.map')) $previous $proof.ReleaseProvenance $config.SerpsMods.Folder
    $versions=Get-UploadedModVersions $previous
    $expectedSections=0
    foreach($active in @($provenance.Mods|Where-Object {$_.State -ceq 'Active'})) {
        $path=Join-Path $config.SerpsMods.Folder ('Payload/BepInEx/plugins/SerpsMods_Serp/'+$active.RelativePath+'/info.json')
        $manifest=[IO.File]::ReadAllText($path)|ConvertFrom-Json
        $old=if($versions.ContainsKey($active.Guid)){$versions[$active.Guid]}else{''}
        foreach($entry in @($manifest.SerpChangelog|Where-Object {(-not $old -or [version]$_.Version -gt [version]$old) -and [version]$_.Version -le [version]$active.Version})) {
            $expectedSections++
            foreach($line in $entry.Changes){Check ($note.Text.Contains('- '+$line)) "Original changelog line absent: $line"}
        }
    }
    Check ([regex]::Matches($note.Text,'(?m)^### v').Count -eq $expectedSections) 'Incorrect total version-section count.'
    [IO.File]::WriteAllText((Join-Path $fixture 'SerpsMods-changelog.txt'),$note.Text+"`r`n",[Text.UTF8Encoding]::new($false))
    Write-Host "Current package: $expectedSections original version sections; changelog: $fixture/SerpsMods-changelog.txt"
}
Write-Host "PASS: $checks cache/changelog checks. Fixtures: $fixture"
