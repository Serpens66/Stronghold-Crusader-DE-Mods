[CmdletBinding()]
param([Parameter(Mandatory)][string]$Workspace,[string]$PackageDirectory)
. (Join-Path $PSScriptRoot 'ApiShared.Common.ps1')
try { $null=Assert-ApiConsumerPackage $Workspace $PackageDirectory; Write-Host 'PASS: local APIShared package and build inputs.' }
catch { Write-Host $_ -ForegroundColor Red; exit 1 }
