param([switch]$Apply,[string]$GameDir='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference='Stop'
if(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue){throw 'Close the game before finishing the local trial.'}
& (Join-Path $PSScriptRoot 'Check-UpdateTrial.ps1') -Phase After -GameDir $GameDir
. (Join-Path $PSScriptRoot 'tools/ProbeContinuation.ps1')
$report=Get-Content -LiteralPath (Join-Path $GameDir 'BepInEx/DependencyBootstrapProbe.lifecycle.json') -Raw|ConvertFrom-Json
$receipt=Join-Path $GameDir '_SE/DependencyBootstrapProbeLocalTrial.json'
$entry=@($report.LocalManifests|Where-Object {[IO.Path]::GetFullPath($_.Path) -ieq [IO.Path]::GetFullPath($receipt)})
if($entry.Count -ne 1){throw 'No unique observed test receipt.'}
$request=Join-Path $GameDir 'BepInEx/config/DependencyBootstrapProbe.local-update.json'
$expectedRequest=Get-ContinuationHash (Join-Path $PSScriptRoot 'update-trial/request.json')
$files=@(@{Path=$receipt;SHA256=$entry[0].SHA256},@{Path=$request;SHA256=$expectedRequest})
foreach($file in $files){Assert-ContinuationUnlinked $file.Path;if((Get-ContinuationHash $file.Path) -cne $file.SHA256){throw 'Changed trial file; preserve it.'}}
if(-not $Apply){$files|ForEach-Object {Write-Host "PREVIEW remove own trial state: $($_.Path)"};return}
# Remove the synthetic receipt first so an interruption cannot imply an unsubscribe.
foreach($file in $files){
    if(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue){throw 'Game started; stop cleanup.'}
    Assert-ContinuationUnlinked $file.Path
    if((Get-ContinuationHash $file.Path) -cne $file.SHA256){throw 'Trial file changed immediately before cleanup.'}
    Remove-Item -LiteralPath $file.Path
}
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'update-trial/finished.json'),(@{Removed=$files;AtUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 5))
Write-Host 'Only synthetic receipt and local trial request removed; installed B and correction switches retained.'
