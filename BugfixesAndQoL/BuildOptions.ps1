[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutFile,
    [Parameter(ValueFromRemainingArguments)][string[]]$DriverArguments)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../Shared/Tools/BranchBuild/BranchBuild.Common.ps1')
try {
    $selection = Get-BranchTestSelection 'BugfixesAndQoL/build.bat' $DriverArguments
    [IO.File]::WriteAllText($OutFile, ($selection.Mode + "`r`n"), [Text.Encoding]::ASCII)
} catch { Write-Host $_.Exception.Message; exit 1 }
