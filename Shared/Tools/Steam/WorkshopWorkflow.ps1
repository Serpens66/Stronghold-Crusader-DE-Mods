param([ValidateSet('Prepare','Upload')][string]$Action,[Parameter(ValueFromRemainingArguments=$true)][string[]]$Options)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
$exitCode=0;$settings=$null;$lock=$null
$noPauseRequested=@($Options|Where-Object {$_ -ieq '/nopause'}).Count -gt 0
try{
    $settings=Resolve-WorkshopOptions $Options
    if($settings.Target -eq 'Cancel'){exit 0}
    $config=Get-WorkshopConfiguration
    $targets=if($settings.Target -eq 'Both'){@($config.APIShared,$config.SerpsMods)}else{@($config.($settings.Target))}
    Write-Host ('Workshop '+$Action+' / '+$(if($settings.Test){'TEST: PRIVATE, SEPARATE IDS'}else{'PRODUCTION'})+' / '+$settings.Target)
    if($Action -eq 'Prepare' -and $settings.Target -in @('SerpsMods','Both') -and -not(Get-WorkshopItemId $config $config.APIShared $settings.Test)){throw 'APIShared Workshop ID missing. Prepare and upload APIShared first. No release changes were made.'}
    $state=Get-WorkshopStateRoot $config $settings.Test;Assert-ApiWorkshopUnlinkedPath $state;[void][IO.Directory]::CreateDirectory($state)
    $lock=[IO.File]::Open((Join-Path $state 'workflow.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    Invoke-WorkshopTargets $targets {param($target)
        Write-Host ('--- '+$target.Key+' ---')
        $script=if($Action -eq 'Upload'){'Upload-WorkshopTarget.ps1'}elseif($target.Key -eq 'APIShared'){'Prepare-APISharedWorkshop.ps1'}else{'Prepare-ModpackWorkflow.ps1'}
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $script) -TestMode ([int]$settings.Test) -ValidateOnly ([int]$settings.Validate) -Target $target.Key
        if($LASTEXITCODE -ne 0){throw "$($target.Key) failed with exit code $LASTEXITCODE; following targets were not started."}
        Write-Host ($target.Key+' completed.')
    }
}catch{Write-Host $_.Exception.Message -ForegroundColor Red;$exitCode=1}finally{if($lock){$lock.Dispose()}}
if(-not $noPauseRequested){[void](Read-Host 'Enter zum Schliessen')}
exit $exitCode
