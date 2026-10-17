param([switch]$NoInstall, [switch]$CorrectionTrial, [switch]$LifecycleObservation)
$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game before building/installing.' }
if($LifecycleObservation -and $CorrectionTrial){throw 'Lifecycle observation does not reinstall control errors.'}
$backup=$null
if($LifecycleObservation -and -not $NoInstall){
    . (Join-Path $PSScriptRoot 'tools/ProbeContinuation.ps1')
    $backup=Join-Path $workspace ('_inspect/DependencyBootstrapProbe/lifecycle-runtime/backup-'+[guid]::NewGuid().ToString('N'))
    New-ProbeContinuation $PSScriptRoot $game (Join-Path $workspace '_inspect/DependencyBootstrapProbe/Correction-accepted-20261016-2154') $backup
    Write-Host "Probe backup: $backup"
}
if ($CorrectionTrial) {
    & (Join-Path $PSScriptRoot 'Test-CorrectionControl.ps1')
    & (Join-Path $PSScriptRoot 'tests\Test-ControlLifecycle.ps1')
    if (-not $NoInstall -and (Test-Path -LiteralPath (Join-Path $game 'BepInEx\plugins\DependencyBootstrapProbeControl_Serp'))) { throw 'Control target already exists; preserve it.' }
}
& (Join-Path $PSScriptRoot 'verify.ps1')
& (Join-Path $workspace 'Shared\Tools\Validation\Test-SharedBoundaries.ps1') -ModDirectory 'Testmods\DependencyBootstrapProbe'
& (Join-Path $workspace 'Shared\Tools\Validation\Test-DependencyMetadata.ps1') -ProjectFiles (Join-Path $PSScriptRoot 'DependencyBootstrapProbe.csproj')
& (Join-Path $workspace 'Shared\Tools\Validation\Test-PermanentNativeRuntimePatches.ps1')
& (Join-Path $PSScriptRoot 'tests\Run-Tests.ps1')
& (Join-Path $PSScriptRoot 'tests\Test-LogChecker.ps1')
& (Join-Path $PSScriptRoot 'tests\Test-Continuation.ps1')
& (Join-Path $PSScriptRoot 'tests\Test-PackageLifecycle.ps1')
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
foreach ($project in @('DependencyBootstrapProbe.Patcher.csproj','DependencyBootstrapProbe.csproj')) {
    & $msbuild (Join-Path $PSScriptRoot $project) /nologo /verbosity:minimal /p:Configuration=Release
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $project" }
}
if (-not $NoInstall -and -not $LifecycleObservation) { & (Join-Path $PSScriptRoot 'Set-ProbeMode.ps1') -Mode Observe }
$plugin = 'BepInEx\plugins\DependencyBootstrapProbe_Serp'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Destination (Join-Path $PSScriptRoot "$plugin\info.json") -Force
$files = @('BepInEx\patchers\DependencyBootstrapProbe.Patcher.dll', "$plugin\DependencyBootstrapProbe.dll", "$plugin\info.json")
if ($CorrectionTrial) {
    $control = 'BepInEx\plugins\DependencyBootstrapProbeControl_Serp\info.json'
    $files += $control
}
if(-not $NoInstall){
    if(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue){throw 'Game started during build; installation refused.'}
    if($backup){$null=Assert-ProbeContinuation $game $backup -BeforeCopy}
}
foreach ($relative in $files) {
    $source = Join-Path $PSScriptRoot $relative
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Missing output: $relative" }
    if (-not $NoInstall) {
        $target = Join-Path $game $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
        Copy-Item -LiteralPath $source -Destination $target -Force
        if ([Convert]::ToBase64String([IO.File]::ReadAllBytes($source)) -cne [Convert]::ToBase64String([IO.File]::ReadAllBytes($target))) { throw "Copy verification failed: $relative" }
    }
}
if($backup){& (Join-Path $PSScriptRoot 'Set-ProbeMode.ps1') -Mode Active -Correction -ContinuationRoot $backup}
Write-Host "Correction-capable probe built. Installed=$(-not $NoInstall). Game/Mono feasibility still requires a full game start."
