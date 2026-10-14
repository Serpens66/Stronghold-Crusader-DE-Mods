[CmdletBinding()]
param([string]$Workspace)
$ErrorActionPreference='Stop'
if (-not $Workspace) { $Workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')) }
. (Join-Path $PSScriptRoot 'RepositoryProcess.Common.ps1')
$target=Join-Path ([IO.Path]::GetFullPath($Workspace)) 'APIShared'
if(Test-Path -LiteralPath $target){throw "APIShared already exists: $target. No files were changed; use GitHub Desktop to manage this repository."}
$result=Invoke-ApiRepositoryProcess 'git' @('clone','--branch','dev/Serpens66','--','https://github.com/SHCDE-APIShared/APIShared.git',$target)
if($result.ExitCode -ne 0){throw "APIShared clone failed. Inspect the destination before retrying: $($result.Diagnostic)"}
Write-Host 'APIShared cloned on dev/Serpens66. Add this folder through File > Add local repository in GitHub Desktop.'
