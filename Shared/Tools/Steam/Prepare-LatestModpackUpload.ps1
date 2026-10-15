param([int]$TestMode=0)
$ErrorActionPreference='Stop'
try{
    . (Join-Path $PSScriptRoot 'WorkshopConfiguration.ps1')
    . (Join-Path $PSScriptRoot 'WorkshopWorkflow.Common.ps1')
    $config=Get-WorkshopConfiguration
    if($env:SHCDE_MODPACK_BRANCH -cne 'main'){throw 'Published modpack uploads require main.'}
    $null=Assert-ApiMainSelection $config.Root $env:SHCDE_API_SHARED_MAIN_SELECTION
    $null=Prepare-LatestPublishedModpack $config ($TestMode -eq 1)
    exit 0
}catch{Write-Host $_.Exception.Message -ForegroundColor Red;exit 1}
