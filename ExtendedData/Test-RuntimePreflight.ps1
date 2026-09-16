[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $workspace 'Shared\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')

$mods = Get-Content -Raw -LiteralPath (Join-Path $workspace 'Shared\ScriptExtenderUpdate\mods.json') | ConvertFrom-Json
# Windows PowerShell 5.1 preserves a top-level JSON array as one pipeline object,
# while newer PowerShell versions enumerate it. A foreach statement handles both forms.
$mod = @(foreach ($entry in $mods) {
    if ([string]$entry.Name -ceq 'ExtendedData') {
        $entry
    }
})
if ($mod.Count -ne 1) {
    throw "Expected exactly one ExtendedData Script Extender inventory entry; found $($mod.Count)."
}

Assert-SERuntimeModPreflight $mod[0] $workspace
Write-Output 'ExtendedData runtime JSON/lifecycle preflight succeeded.'
