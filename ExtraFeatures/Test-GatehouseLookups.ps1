[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSVersion.Major -lt 7) {
    & pwsh.exe -NoProfile -File $PSCommandPath
    if ($LASTEXITCODE -ne 0) { throw 'Gatehouse lookup tests failed.' }
    return
}
$files = @(
    (Join-Path $PSScriptRoot 'src/GatehouseAutomationRuntime.Lookups.cs')
    (Join-Path $PSScriptRoot 'tests/GatehouseLookupFixture.cs')
    (Join-Path $PSScriptRoot '../APIShared/src/Buildings/BuildingAccess.cs')
)
Add-Type -Path $files -CompilerOptions @('/unsafe')
[ExtraFeatures.GatehouseLookupFixture]::Run()
