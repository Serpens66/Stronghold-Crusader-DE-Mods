[CmdletBinding()]
param([string]$GameDir, [string]$ExtenderDir)
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
if (-not $GameDir) { $GameDir = $env:SHCDE_GAME_DIR }
if (-not $GameDir) { $GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition' }
if (-not $ExtenderDir) { $ExtenderDir = Join-Path $GameDir 'BepInEx\plugins\000shcdese' }
foreach ($script in @('Test-SharedBoundaries.ps1','Test-UnitCommandSplit.ps1','Test-UnitAccess.ps1')) {
    & (Join-Path $PSScriptRoot $script)
    if (-not $?) { throw "Consumer preflight failed: $script" }
}
foreach ($script in @('_inspect\Fixes124Implementation\Verify-Implementation.ps1','_inspect\AssassinGateClimb\verify.ps1')) {
    & (Join-Path $workspace $script)
    if (-not $?) { throw "Consumer compatibility check failed: $script" }
}
foreach ($suite in @('APISharedTests','LobbyModSettingsPresetTests')) {
    & dotnet run --project (Join-Path $workspace "_inspect\$suite\$suite.csproj") --configuration Release
    if ($LASTEXITCODE -ne 0) { throw "Workspace consumer tests failed: $suite" }
}
Write-Host 'PASS: APIShared workspace consumers and additional compatibility checks.'
