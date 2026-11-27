[CmdletBinding(PositionalBinding=$false)]
param([switch]$nopause, [switch]$noinstall, [string]$ApiSharedDir, [string]$BugfixesDir,
    [Parameter(ValueFromRemainingArguments)][string[]]$DriverArguments)
$ErrorActionPreference = 'Stop'
# The shared dispatcher alone owns /devdiag, source selection, SDK proof and environment restoration.
if (-not $env:SHCDE_BRANCH_SOURCE_ROOT -and -not $env:SHCDE_BRANCH_PINNED) {
    $forward = @($DriverArguments)
    # This test driver's historical default is always the live working tree, including interactive calls.
    if (-not @($forward | Where-Object { $_ -match '^/(source|branch)(:|$)' -or $_ -ieq '/workspace' }).Count) {
        $forward += '/source:working'
    }
    if ($nopause) { $forward += '/nopause' }
    if ($noinstall) { $forward += '/noinstall' }
    $previousPackage = [Environment]::GetEnvironmentVariable('SHCDE_API_SHARED_DIR','Process')
    $previousBugfixes = [Environment]::GetEnvironmentVariable('SHCDE_ACCEPTANCE_BQOL_DIR','Process')
    try {
        if ($BugfixesDir) { $env:SHCDE_ACCEPTANCE_BQOL_DIR = [IO.Path]::GetFullPath($BugfixesDir) }
        if ($ApiSharedDir) {
            $env:SHCDE_API_SHARED_DIR = [IO.Path]::GetFullPath($ApiSharedDir)
            $forward += '/apishared:package'
        }
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot '../../Shared/Tools/BranchBuild/Invoke-BranchBuild.ps1') `
            -Repository (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path -Driver 'Testmods/TrappedUnitRescueTest/build.bat' @forward
        $result = $LASTEXITCODE
    } finally { [Environment]::SetEnvironmentVariable('SHCDE_API_SHARED_DIR',$previousPackage,'Process'); [Environment]::SetEnvironmentVariable('SHCDE_ACCEPTANCE_BQOL_DIR',$previousBugfixes,'Process') }
    exit $result
}
if (-not $ApiSharedDir) { $ApiSharedDir = $env:SHCDE_API_SHARED_DIR }
if (-not $BugfixesDir) { $BugfixesDir = $env:SHCDE_ACCEPTANCE_BQOL_DIR }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests/Test-PerformanceLog.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Passive performance evidence contract tests failed.' }
if ([string]::IsNullOrWhiteSpace($ApiSharedDir)) { throw 'Select an explicit APIShared plugin/bootstrap package for runtime and tests.' }
$arguments = @('msbuild', (Join-Path $PSScriptRoot 'TrappedUnitRescueTest.csproj'), '/t:Build', '/p:Configuration=Release', '/verbosity:minimal')
if ($BugfixesDir) { $arguments += '/p:BugfixesDir=' + [IO.Path]::GetFullPath($BugfixesDir) }
if ($ApiSharedDir) { $arguments += '/p:ApiSharedDir=' + [IO.Path]::GetFullPath($ApiSharedDir) }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'TrappedUnitRescueTest compilation failed.' }
$testArguments = @('msbuild', (Join-Path $PSScriptRoot 'tests/TrappedUnitRescueTest.Tests.csproj'), '/t:Build', '/p:Configuration=Release', '/verbosity:minimal')
if ($ApiSharedDir) { $testArguments += '/p:ApiSharedDir=' + [IO.Path]::GetFullPath($ApiSharedDir) }
& dotnet @testArguments
if ($LASTEXITCODE -ne 0) { throw 'Production acceptance orchestration/package tests did not compile.' }
& (Join-Path $PSScriptRoot 'tests/bin/TrappedUnitRescueTest.Tests.exe') ([IO.Path]::GetFullPath($ApiSharedDir)) (Join-Path $PSScriptRoot 'BepInEx/plugins/TrappedUnitRescueTest_Serp/TrappedUnitRescueTest.dll')
if ($LASTEXITCODE -ne 0) { throw 'Production acceptance orchestration/package tests failed.' }
Write-Output 'PASS: package built; installation and game runs use the transactional Invoke-InGameTest runner.'
