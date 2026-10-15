param([ValidateSet('Prepare','Upload')][string]$Action,[Parameter(ValueFromRemainingArguments=$true)][string[]]$Options)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot 'WorkshopOptions.Common.ps1')
. (Join-Path $PSScriptRoot '../ApiSharedRepository/MainSource.Common.ps1')
. (Join-Path $PSScriptRoot 'ModpackSource.Common.ps1')
$exitCode=0;$settings=$null;$lock=$null
$previousMainSelection=$env:SHCDE_API_SHARED_MAIN_SELECTION
$noPauseRequested=@($Options|Where-Object {$_ -ieq '/nopause'}).Count -gt 0
try{
    $settings=Resolve-WorkshopOptions $Options
    if($settings.Target -eq 'Cancel'){exit 0}
    $config=Get-WorkshopConfiguration
    if($settings.Target -in @('SerpsMods','Both')){
        if($Action -eq 'Prepare'){
            $settings.Branch=Get-ModpackBranchChoice $settings.Branch
            if($settings.Branch -eq 'Cancel'){exit 0}
            if($settings.Branch -eq 'develop'){$settings.Test=$true}
            $source=Resolve-ModpackSource $config.Root $settings.Branch
        }else{
            if($settings.Branch -eq 'develop'){$settings.Test=$true}
            $source=Get-PreparedModpackSource $config.Root $settings.Test $settings.Branch
            $settings.Branch=$source.Branch
        }
        $env:SHCDE_MODPACK_COORDINATOR=Get-ModpackCoordinator $config.Root
        $env:SHCDE_MODPACK_BRANCH=$source.Branch
        $env:SHCDE_API_SHARED_PUBLISHED_092='1'
        Write-Host ('Modpack-Branch: '+$source.Branch+' / '+$source.Commit+' / APIShared: published 0.9.2')
        if([IO.Path]::GetFullPath($source.Root).TrimEnd('\','/') -ine $config.Root.TrimEnd('\','/')){
            $argsForChild=@('/branch:'+$source.Branch) + @('/nopause')
            $argsForChild+=switch($settings.Target){'Both'{'/both'} default{'/modpack'}}
            if($settings.Test){$argsForChild+='/test'}
            if($settings.Validate){$argsForChild+='/validate'}
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $source.Root 'Shared/Tools/Steam/WorkshopWorkflow.ps1') -Action $Action @argsForChild
            exit $LASTEXITCODE
        }
    }
    $targets=if($settings.Target -eq 'Both'){@($config.APIShared,$config.SerpsMods)}else{@($config.($settings.Target))}
    if($Action -eq 'Prepare'){
        $env:SHCDE_API_SHARED_MAIN_SELECTION=Resolve-ApiMainSelection $config.Root
    }else{
        $preparedState=if($env:SHCDE_MODPACK_COORDINATOR){Get-ModpackStateRoot $config.Root $settings.Test}else{Join-Path $config.Root '.release-output/SteamWorkshop'}
        if($settings.Test -and -not $env:SHCDE_MODPACK_COORDINATOR){$preparedState=Join-Path $preparedState 'test'}
        $firstSelection=$null
        foreach($target in $targets){
            $prepared=[IO.File]::ReadAllText((Join-Path $preparedState ('prepared/'+$target.Key+'.json')))|ConvertFrom-Json
            if(-not $prepared.PSObject.Properties['MainSource']){throw 'Prepare the package again with main isolation before uploading.'}
            $selected=Assert-ApiMainDeliveryIdentity $config.Root $prepared.MainSource
            if($firstSelection -and $firstSelection -cne $selected){throw 'Both packages must have the same pinned main preparation.'}
            $firstSelection=$selected
        }
        $env:SHCDE_API_SHARED_MAIN_SELECTION=$firstSelection
    }
    . (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
    . (Join-Path $PSScriptRoot 'WorkshopOutputRetention.ps1')
    Write-Host ('Workshop '+$Action+' / '+$(if($settings.Test){'TEST: PRIVATE, SEPARATE IDS'}else{'PRODUCTION'})+' / '+$settings.Target)
    if($Action -eq 'Prepare' -and $settings.Target -in @('SerpsMods','Both') -and -not(Get-WorkshopItemId $config $config.APIShared $settings.Test)){throw 'APIShared Workshop ID missing. Prepare and upload APIShared first. No release changes were made.'}
    $state=Get-WorkshopStateRoot $config $settings.Test;Assert-ApiWorkshopUnlinkedPath $state;[void][IO.Directory]::CreateDirectory($state)
    $lock=[IO.File]::Open((Join-Path $state 'workflow.lock'),[IO.FileMode]::OpenOrCreate,[IO.FileAccess]::ReadWrite,[IO.FileShare]::None)
    if($Action -eq 'Prepare' -and (-not $settings.Validate -or $env:SHCDE_API_SHARED_PUBLISHED_092 -eq '1')){Ensure-ApiMainBuild $config.Root}
    Invoke-WorkshopTargets $targets {param($target)
        Write-Host ('--- '+$target.Key+' ---')
        $previousBackups=@()
        $retentionReady=$true
        if($Action -eq 'Prepare' -and -not $settings.Validate){
            try{$previousBackups=@(Get-WorkshopOutputBackups $config $target $settings.Test|ForEach-Object {$_.Name})}
            catch{$retentionReady=$false;Write-Host ('WARNING: Workshop output retention skipped: '+$_.Exception.Message)}
        }
        $script=if($Action -eq 'Upload'){'Upload-WorkshopTarget.ps1'}elseif($target.Key -eq 'APIShared'){'Prepare-APISharedWorkshop.ps1'}else{'Prepare-ModpackWorkflow.ps1'}
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $script) -TestMode ([int]$settings.Test) -ValidateOnly ([int]$settings.Validate) -Target $target.Key
        if($LASTEXITCODE -ne 0){throw "$($target.Key) failed with exit code $LASTEXITCODE; following targets were not started."}
        if($Action -eq 'Prepare' -and -not $settings.Validate -and $retentionReady){$null=Invoke-WorkshopOutputRetention $config $target $settings.Test $previousBackups $lock}
        Write-Host ($target.Key+' completed.')
    }
}catch{Write-Host $_.Exception.Message -ForegroundColor Red;$exitCode=1}finally{if($lock){$lock.Dispose()};$env:SHCDE_API_SHARED_MAIN_SELECTION=$previousMainSelection}
if(-not $noPauseRequested){[void](Read-Host 'Enter zum Schliessen')}
exit $exitCode
