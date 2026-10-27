$ErrorActionPreference = 'Stop'
$devdiag = $false; $install = $true
foreach ($argument in $args) {
    switch ($argument.ToLowerInvariant()) {
        '/devdiag' { $devdiag = $true }
        '/noinstall' { $install = $false }
        '/nopause' { }
        '/source:working' { }
        default { throw "Unsupported argument: $argument. This prototype builds its current working sources and installed SDK only." }
    }
}
$env:SHCDE_DEVDIAG = if ($devdiag) { '1' } else { '' }
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
. (Join-Path $workspace 'APIShared\tools\BuildProof.ps1')
$branch = & git -C $workspace branch --show-current
if ($devdiag -and $branch -eq 'main') { throw 'DEVDIAG builds on main are prohibited.' }
$evidence = Join-Path $PSScriptRoot '.local'
[IO.Directory]::CreateDirectory($evidence) | Out-Null
$inputs = @{}
# Read and attest every installed dependency; no SDK build/copy targets are invoked.
foreach ($folder in @('000shcdese','APIShared_Serp')) {
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $game "BepInEx\plugins\$folder") -File -Recurse) { $inputs[$file.FullName] = Get-ApiFileHash $file.FullName }
}
$project = Join-Path $PSScriptRoot 'BetterPathfindingTest.csproj'
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'native\Build-Native.ps1')
if ($LASTEXITCODE) { throw 'Private native backend build failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $workspace 'Shared\Tools\Validation\Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'Testmods/BetterPathfindingTest' -ConsumerOnly
if ($LASTEXITCODE) { throw 'Scoped boundaries failed.' }
# Test-SharedBoundaries already runs dependency metadata for the selected project.
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $workspace 'Shared\Tools\Validation\Test-DevDiagnostics.ps1') -Root $workspace -ModDirectory 'Testmods/BetterPathfindingTest'
if ($LASTEXITCODE) { throw 'Diagnostic boundaries failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests\Test-EngineGate.ps1') -GameDir $game
if ($LASTEXITCODE) { throw 'Installed engine lock contract failed.' }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests\Test-PassiveSources.ps1')
if ($LASTEXITCODE) { throw 'Passive source guards failed.' }
& dotnet run --project (Join-Path $PSScriptRoot 'tests\BetterPathfindingTest.Tests.csproj') -c Release
if ($LASTEXITCODE) { throw 'Offline behavior tests failed.' }
& $msbuild $project /nologo /t:Rebuild /p:Configuration=Release /p:SerpDevDiagnostics=$($devdiag.ToString().ToLowerInvariant()) /verbosity:minimal
if ($LASTEXITCODE) { throw 'Runtime build failed.' }
foreach ($path in $inputs.Keys) { if ((Get-ApiFileHash $path) -cne $inputs[$path]) { throw "Installed dependency changed: $path" } }
$package = Join-Path $PSScriptRoot 'BepInEx\plugins\BetterPathfindingTest_Serp'
$dll = Join-Path $package 'BetterPathfindingTest.dll'
$nativeDll = Join-Path $package 'BetterPathfindingNative.dll'
if (-not $devdiag) { Remove-Item -LiteralPath $nativeDll -Force }
& powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'tests\Test-Artifact.ps1') -AssemblyPath $dll -DiagnosticMode $($devdiag.ToString().ToLowerInvariant())
if ($LASTEXITCODE) { throw 'Artifact boundary check failed.' }
if ($install) {
    if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game before replacing the test package.' }
    $destination = Join-Path $game 'BepInEx\plugins\BetterPathfindingTest_Serp'
    [IO.Directory]::CreateDirectory($destination) | Out-Null
    $packageNames = @('BetterPathfindingTest.dll','BetterPathfindingTest.pdb','info.json'); if ($devdiag) { $packageNames += 'BetterPathfindingNative.dll' }
    foreach ($name in $packageNames) { Copy-Item -LiteralPath (Join-Path $package $name) -Destination (Join-Path $destination $name) -Force }
    foreach ($name in $packageNames) { if ((Get-ApiFileHash (Join-Path $package $name)) -cne (Get-ApiFileHash (Join-Path $destination $name))) { throw "Installed package hash mismatch: $name" } }
    if ((Get-ApiFileHash $dll) -cne (Get-ApiFileHash (Join-Path $destination 'BetterPathfindingTest.dll'))) { throw 'Installed test DLL hash mismatch.' }
}
$sources = @{}
foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src'),(Join-Path $PSScriptRoot 'tests') -Recurse -File -Filter '*.cs' | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }) { $sources[$file.FullName] = Get-ApiFileHash $file.FullName }
foreach ($relative in @('Movement\WeightedGridSearchKernel.cs','Movement\WeightedMoatRoutePlanner.cs','Moat\MoatCandidateField.cs','Runtime\UnitCommandContracts.cs')) { $path = Join-Path $workspace "BugfixesAndQoL\src\UnitCommands\$relative"; $sources[$path] = Get-ApiFileHash $path }
$nativeHash = if ($devdiag) { Get-ApiFileHash $nativeDll } else { $null }
foreach ($folder in @('native')) { foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $folder) -File) { $sources[$file.FullName] = Get-ApiFileHash $file.FullName } }
$record = @{ nativeSha256=$nativeHash; timestampUtc=[DateTime]::UtcNow.ToString('o'); branch=$branch; head=(& git -C $workspace rev-parse HEAD); devdiag=$devdiag; installed=$install; dllSha256=(Get-ApiFileHash $dll); installedDependencies=$inputs; sourceSha256=$sources } | ConvertTo-Json -Depth 6
$record | Set-Content -LiteralPath (Join-Path $evidence ('build-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '.json')) -Encoding UTF8
$record | Set-Content -LiteralPath (Join-Path $evidence 'build.json') -Encoding UTF8
Write-Host "PASS: prototype built; devdiag=$devdiag; installed=$install; dependencies unchanged."
