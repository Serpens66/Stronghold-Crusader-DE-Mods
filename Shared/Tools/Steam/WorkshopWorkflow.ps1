param([ValidateSet('Prepare','Upload')][string]$Action,[Parameter(ValueFromRemainingArguments=$true)][string[]]$Options)
$ErrorActionPreference='Stop'
if($null -eq $Options){$Options=@()}
. (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
. (Join-Path $PSScriptRoot 'WorkshopOptions.Common.ps1')
. (Join-Path $PSScriptRoot '../ApiSharedRepository/MainSource.Common.ps1')
. (Join-Path $PSScriptRoot 'ModpackSource.Common.ps1')
$exitCode=0;$settings=$null;$lock=$null
$previousMainSelection=$env:SHCDE_API_SHARED_MAIN_SELECTION
$noPauseRequested=@($Options|Where-Object {$_ -ieq '/nopause'}).Count -gt 0
# Leave delegated work through this block so only the original caller owns the pause.
:workflow do {
try{
    if($Action -eq 'Upload' -and -not @($Options|Where-Object {$_ -in @('/modpack','/apishared','/both')}).Count){$Options=@('/modpack')+$Options}
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
            $source=Get-PreparedModpackUploadSource $config.Root $settings.Test $settings.Branch
            $settings.Branch=$source.Branch
        }
        $env:SHCDE_MODPACK_COORDINATOR=Get-ModpackCoordinator $config.Root
        $env:SHCDE_MODPACK_BRANCH=$source.Branch
        Write-Host ('Modpack-Branch: '+$source.Branch+' / '+$source.Commit+' / APIShared: validated main release')
        if([IO.Path]::GetFullPath($source.Root).TrimEnd('\','/') -ine $config.Root.TrimEnd('\','/')){
            $argsForChild=@('/branch:'+$source.Branch) + @('/nopause')
            $argsForChild+=switch($settings.Target){'Both'{'/both'} default{'/modpack'}}
            if($settings.Test){$argsForChild+='/test'}
            if($settings.Validate){$argsForChild+='/validate'}
            & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $source.Root 'Shared/Tools/Steam/WorkshopWorkflow.ps1') -Action $Action @argsForChild
            $exitCode=$LASTEXITCODE
            # The temporary release folder is only needed until its package is uploaded.
            if($Action -eq 'Upload' -and $exitCode -eq 0 -and -not $settings.Validate -and -not $settings.Test){
                try{Remove-ModpackReleaseWorktree $config.Root $source}catch{Write-Host ('WARNING: temporary release folder was kept: '+$_.Exception.Message)}
            }
            break workflow
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
    if($Action -eq 'Prepare' -and -not $settings.Validate){Ensure-ApiMainBuild $config.Root}
    if($Action -eq 'Upload'){
        # Freeze the selected SDK and package across preparation and upload.
        # A new latest release requires a separate preparation; never replace the proof here.
        $firstSdk=$null
        foreach($target in $targets){
            $prepared=Assert-WorkshopPreparation $config $target $settings.Test
            $selected=Assert-ApiMainDeliveryIdentity $config.Root $prepared.MainSource
            if($selected -cne $env:SHCDE_API_SHARED_MAIN_SELECTION){throw 'Both uploads must use the same pinned main source.'}
            if($firstSdk){Assert-ModpackApiSharedIdentity $firstSdk $prepared.PublishedSdk}
            $firstSdk=$prepared.PublishedSdk
        }
    }
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
    if($Action -eq 'Prepare' -and $settings.Target -eq 'Both' -and -not $settings.Validate){
        $apiProof=Assert-WorkshopPreparation $config $config.APIShared $settings.Test
        $modpackProof=Assert-WorkshopPreparation $config $config.SerpsMods $settings.Test
        Assert-ModpackApiSharedIdentity $apiProof.PublishedSdk $modpackProof.PublishedSdk
    }
}catch{Write-Host $_.Exception.Message -ForegroundColor Red;$exitCode=1}finally{if($lock){$lock.Dispose()};$env:SHCDE_API_SHARED_MAIN_SELECTION=$previousMainSelection}
}while($false)
if(-not $noPauseRequested){[void](Read-Host 'Enter zum Schliessen')}
exit $exitCode
