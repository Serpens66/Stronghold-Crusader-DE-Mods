$ErrorActionPreference = 'Stop'
$fixtureRoot = Join-Path $PSScriptRoot 'log-fixtures\identity-tests'
$checker = Join-Path $PSScriptRoot '..\Check-Run.ps1'
$paths = @('BepInEx\patchers\DependencyBootstrapProbe.Patcher.dll','BepInEx\plugins\DependencyBootstrapProbe_Serp\DependencyBootstrapProbe.dll','BepInEx\core\BepInEx.dll','BepInEx\plugins\000shcdese\SHCDESE.dll')
$hashes = @()
foreach ($relative in $paths) {
    $target = Join-Path $fixtureRoot $relative
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    # Plain fixture bytes, deliberately not usable game assemblies.
    $bytes = [Text.Encoding]::UTF8.GetBytes('SYNTHETIC LOG TEST ONLY: ' + $relative)
    [IO.File]::WriteAllBytes($target,$bytes)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { $hashes += [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','') } finally { $sha.Dispose() }
}
$start = '[DBP:SESSION] id=synthetic'
$details = @{
    MODE='Active'; READY_OBSERVED='mode=Active; fixture'
    FINISH="mode=Active patcher=$($hashes[0])"; CORE_BINARY="sha256=$($hashes[2])"; SE_BINARY="sha256=$($hashes[3])"
    PLUGIN_AWAKE="plugin=$($hashes[1]) static render observer attached"; PLUGIN_DISABLED="plugin=$($hashes[1])"
    BEFORE_EXTENDER='snapshot=True type=fixture'; COMPATIBILITY_ENTER='count=1'; CHAIN_COMPLETE='snapshot=True extender=True checks=1'
    POST_CLEANUP_RENDER='original Unity component destroyed; distinct render frames=120'
}
$sequence = @('FINISH','MODE','LISTENER_ATTACHED','READY_OBSERVED','CHAIN_PREPARE','CORE_BINARY','CHAIN_TRANSFORMED','CHAIN_DELEGATE_BEGIN','CHAIN_DELEGATE_END','CHAIN_JIT_BEGIN','CHAIN_JIT_END','CHAIN_COPY_COMPILED','CHAIN_INSTALL','CHAIN_HOOK_INSTALLED','CHAIN_ENTER','SNAPSHOT','BEFORE_EXTENDER','SE_BINARY','SE_PREPARE','SE_TRANSFORMED','SE_DELEGATE_BEGIN','SE_DELEGATE_END','SE_JIT_BEGIN','SE_JIT_END','SE_COPY_COMPILED','SE_INSTALL','SE_HOOK_INSTALLED','COMPATIBILITY_ENTER','PLUGIN_AWAKE','CHAIN_COMPLETE','POST_CLEANUP_RENDER')
$active = @($start) + @($sequence | ForEach-Object { "[Info:fixture] [DBP:$_] $($details[$_])" })
$disabled = @($start,"[Info:fixture] [DBP:FINISH] $($details.FINISH.Replace('mode=Active','mode=Disabled'))",'[Info:fixture] [DBP:DISABLED] restart','[DBP:LISTENER_ATTACHED] fixture','[DBP:READY_OBSERVED] mode=Disabled;',"[Info:fixture] [DBP:PLUGIN_DISABLED] $($details.PLUGIN_DISABLED)",'[Info:fixture] [DBP:CHAIN_COMPLETE] snapshot=False extender=False checks=0')
function CheckFixture([string]$name,[string[]]$lines,[string]$mode,[bool]$accept) {
    $file = Join-Path $fixtureRoot ($name+'.log')
    [IO.File]::WriteAllLines($file,$lines)
    $passed = $false
    try { & $checker -Mode $mode -LogPath $file -ExpectedArtifactRoot $fixtureRoot -GameDir $fixtureRoot; $passed=$true } catch { if ($accept) { throw } }
    if ($passed -ne $accept) { throw "Unexpected log-check outcome: $name" }
    Write-Host "PASS synthetic log case: $name (accepted=$accept)"
}
CheckFixture 'active' $active Active $true
CheckFixture 'disabled' $disabled Disabled $true
CheckFixture 'missing-se-copy' @($active | Where-Object { $_ -notmatch '\[DBP:SE_COPY_COMPILED\]' }) Active $false
CheckFixture 'missing-awake' @($active | Where-Object { $_ -notmatch '\[DBP:PLUGIN_AWAKE\]' }) Active $false
CheckFixture 'duplicate-awake' @($active + ($active | Where-Object { $_ -match '\[DBP:PLUGIN_AWAKE\]' })) Active $false
CheckFixture 'duplicate-post' @($active + $active[-1]) Active $false
CheckFixture 'stale-patcher' @($active | ForEach-Object { $_.Replace($hashes[0],('0'*64)) }) Active $false
CheckFixture 'stale-plugin' @($active | ForEach-Object { $_.Replace($hashes[1],('0'*64)) }) Active $false
CheckFixture 'stale-core' @($active | ForEach-Object { $_.Replace($hashes[2],('0'*64)) }) Active $false
CheckFixture 'last-start-empty' @($active + $start) Active $false
CheckFixture 'error' @($active + '[Info:fixture] [DBP:ERROR] synthetic failure') Active $false
CheckFixture 'skip' @($active + '[Info:fixture] [DBP:SKIP] unknown binary') Active $false
CheckFixture 'wrong-count' @($active | ForEach-Object { $_.Replace('checks=1','checks=2') }) Active $false
CheckFixture 'too-few-frames' @($active | ForEach-Object { $_.Replace('frames=120','frames=119') }) Active $false
CheckFixture 'post-before-awake' (@($start,$active[-1]) + @($active[1..($active.Count-2)])) Active $false
CheckFixture 'active-in-disabled' @($disabled + '[Info:fixture] [DBP:CHAIN_HOOK_INSTALLED] unexpected') Disabled $false
CheckFixture 'disabled-as-active' $disabled Active $false
CheckFixture 'active-as-disabled' $active Disabled $false
Write-Host 'Synthetic fixtures only: this does not certify a game run.'
$observe = @($start,"[DBP:FINISH] mode=Observe patcher=$($hashes[0])",'[DBP:MODE] Observe','[DBP:LISTENER_ATTACHED] fixture','[DBP:READY_OBSERVED] mode=Observe; fixture',"[DBP:PLUGIN_AWAKE] $($details.PLUGIN_AWAKE)",'[DBP:CHAIN_COMPLETE] snapshot=False extender=False checks=0',"[DBP:POST_CLEANUP_RENDER] $($details.POST_CLEANUP_RENDER)")
CheckFixture 'observe' $observe Observe $true
CheckFixture 'observe-hook' @($observe + '[DBP:CHAIN_INSTALL] unexpected') Observe $false
CheckFixture 'observe-missing-ready' @($observe | Where-Object {$_ -notmatch 'READY_OBSERVED'}) Observe $false
CheckFixture 'observe-duplicate-ready' @($observe + '[DBP:READY_OBSERVED] mode=Observe;') Observe $false
CheckFixture 'observe-old-build' @($observe | ForEach-Object {$_.Replace($hashes[0],('0'*64))}) Observe $false