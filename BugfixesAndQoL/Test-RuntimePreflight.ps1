[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
. (Join-Path $workspace 'Shared\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')

$modsDocument = Get-Content -Raw -LiteralPath (Join-Path $workspace 'Shared\ScriptExtenderUpdate\mods.json') | ConvertFrom-Json
$mods = @(foreach ($entry in $modsDocument) { $entry })
$mod = @($mods | Where-Object { [string]$_.Name -ceq 'BugfixesAndQoL' })
if ($mod.Count -ne 1) {
    throw "Expected exactly one BugfixesAndQoL Script Extender inventory entry; found $($mod.Count)."
}

Assert-SERuntimeModPreflight $mod[0] $workspace
Write-Output 'BugfixesAndQoL runtime JSON/lifecycle preflight succeeded.'
