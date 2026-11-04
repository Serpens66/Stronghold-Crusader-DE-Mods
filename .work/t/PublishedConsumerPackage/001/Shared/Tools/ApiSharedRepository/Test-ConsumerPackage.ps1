[CmdletBinding()]
param([Parameter(Mandatory)][string]$Workspace,[string]$PackageDirectory)
. (Join-Path $PSScriptRoot 'ApiShared.Common.ps1')
try { $null=Assert-ApiConsumerPackage $Workspace $PackageDirectory; Write-Host 'PASS: verified APIShared consumer package and source inputs.' }
catch { Write-Host $_ -ForegroundColor Red; exit 1 }
