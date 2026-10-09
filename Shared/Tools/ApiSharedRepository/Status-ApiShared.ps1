[CmdletBinding()]
param([switch]$Release, [string]$Workspace)
if (-not $Workspace) { $Workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..')) }
. (Join-Path $PSScriptRoot 'ApiShared.Common.ps1')
try {
    $api = Assert-ApiCheckout $Workspace
    Write-Host ('Recorded commit: ' + (Get-ApiSubmodule $Workspace))
    Write-Host ('Current commit:  ' + ((Invoke-ApiGit $api @('rev-parse','HEAD')) -join ''))
    $branch = (Invoke-ApiGit $api @('branch','--show-current')) -join ''
    Write-Host ('Branch: ' + $(if($branch){$branch}else{'detached HEAD'}))
    $status = @(Invoke-ApiGit $api @('status','--short'))
    if ($status.Count) { $status | Write-Host } else { Write-Host 'APIShared checkout is clean.' }
    if ($Release) { $null=Assert-ApiReleaseState $Workspace } else { $null=Assert-ApiConsumerPackage $Workspace '' }
    Write-Host 'PASS: APIShared package matches the current build inputs.'
} catch { Write-Host $_ -ForegroundColor Red; exit 1 }
