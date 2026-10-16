param([switch]$NoInstall)
$ErrorActionPreference = 'Stop'
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game before building/installing.' }
& (Join-Path $PSScriptRoot 'verify.ps1')
& (Join-Path $workspace 'Shared\Tools\Validation\Test-SharedBoundaries.ps1') -ModDirectory 'Testmods\DependencyBootstrapProbe'
& (Join-Path $workspace 'Shared\Tools\Validation\Test-DependencyMetadata.ps1') -ProjectFiles (Join-Path $PSScriptRoot 'DependencyBootstrapProbe.csproj')
& (Join-Path $workspace 'Shared\Tools\Validation\Test-PermanentNativeRuntimePatches.ps1')
& (Join-Path $PSScriptRoot 'tests\Run-Tests.ps1')
& (Join-Path $PSScriptRoot 'tests\Test-LogChecker.ps1')
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
foreach ($project in @('DependencyBootstrapProbe.Patcher.csproj','DependencyBootstrapProbe.csproj')) {
    & $msbuild (Join-Path $PSScriptRoot $project) /nologo /verbosity:minimal /p:Configuration=Release
    if ($LASTEXITCODE -ne 0) { throw "Compilation failed: $project" }
}
if (-not $NoInstall) { & (Join-Path $PSScriptRoot 'Set-ProbeMode.ps1') -Mode Observe }
$plugin = 'BepInEx\plugins\DependencyBootstrapProbe_Serp'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Destination (Join-Path $PSScriptRoot "$plugin\info.json") -Force
$files = @('BepInEx\patchers\DependencyBootstrapProbe.Patcher.dll', "$plugin\DependencyBootstrapProbe.dll", "$plugin\info.json")
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
Write-Host "Shadow probe built. Installed=$(-not $NoInstall). Game/Mono feasibility still requires a full game start."