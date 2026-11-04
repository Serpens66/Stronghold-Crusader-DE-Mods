param([string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$workspace = [IO.Path]::GetFullPath((Join-Path $project '..\..'))
. (Join-Path $workspace 'Shared\Tools\Paths\WorkPaths.ps1')
$run = New-ShortWorkDirectory -Root $workspace -Kind a -Name 'wood-safety-build' -Identity 'working sources; installed dependencies; no install'
Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')] Evidence/package: $run"
$env:SHCDE_GAME_DIR = $GameDir
$sdk = Join-Path $GameDir 'BepInEx\plugins\APIShared_Serp'
$extender = Join-Path $GameDir 'BepInEx\plugins\000shcdese'
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
$common = @('/p:ImportDirectoryBuildTargets=false', '/p:NuGetAudit=false', "/p:GameDir=$GameDir", "/p:ApiSharedDir=$sdk", "/p:ExtenderDir=$extender")
function Step([string]$Name, [scriptblock]$Action) {
    Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')] $Name"
    $global:LASTEXITCODE = 0
    & $Action *> (Join-Path $run ($Name + '.log'))
    if ($LASTEXITCODE -ne 0) { throw "$Name failed; see $run" }
}
function Get-Sha256Path([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $sha = [Security.Cryptography.SHA256]::Create()
        try { [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $sha.Dispose() }
    } finally { $stream.Dispose() }
}
function Snapshot {
    @(Get-ChildItem -LiteralPath (Join-Path $GameDir 'BepInEx\plugins'),(Join-Path $GameDir 'BepInEx\patchers') -File -Recurse |
        Sort-Object FullName | ForEach-Object { [ordered]@{Path=$_.FullName; Hash=(Get-Sha256Path $_.FullName)} })
}
$before = Snapshot
try {
    $before | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'installed-before.json') -Encoding UTF8
    foreach ($mod in @('AIBuildDiagnoseTest','AIWoodSafetyTest')) {
        Step "preflight-$mod" { & (Join-Path $workspace 'Shared\Tools\Validation\Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory "Testmods\$mod" -ConsumerOnly }
        Step "verify-$mod" { & (Join-Path $workspace "Testmods\$mod\verify.ps1") }
    }
    $general = Join-Path $run 'package\BepInEx\plugins\AIBuildDiagnoseTest_Serp'
    $safety = Join-Path $run 'package\BepInEx\plugins\AIWoodSafetyTest_Serp'
    Step 'compile-general' { & $msbuild (Join-Path $workspace 'Testmods\AIBuildDiagnoseTest\AIBuildDiagnoseTest.csproj') /t:Rebuild /nologo /v:minimal @common "/p:OutputPath=$general" }
    Step 'compile-safety' { & $msbuild (Join-Path $project 'AIWoodSafetyTest.csproj') /t:Rebuild /nologo /v:minimal @common "/p:GeneralDiagnosticDir=$general" "/p:OutputPath=$safety" }
    foreach ($suite in @(
        @('general-tests','Testmods\AIBuildDiagnoseTest\tests\Diagnostics.Tests\Diagnostics.Tests.csproj'),
        @('safety-tests','Testmods\AIWoodSafetyTest\tests\Safety.Tests.csproj'),
        @('companion-tests','Testmods\CanarariWoodTest\tests\Companion.Tests\Companion.Tests.csproj'))) {
        $name = $suite[0]; $testProject = Join-Path $workspace $suite[1]
        Step $name { & dotnet test $testProject -c Release @common "/p:GeneralDiagnosticDir=$general" --logger trx --results-directory (Join-Path $run $name) }
    }
    Copy-Item -LiteralPath (Join-Path $workspace 'Testmods\AIBuildDiagnoseTest\info.json') -Destination $general
    Copy-Item -LiteralPath (Join-Path $project 'info.json') -Destination $safety
    $zip = Join-Path $run 'AIWoodSafetyTest-paired.zip'
    Compress-Archive -LiteralPath (Join-Path $run 'package\BepInEx') -DestinationPath $zip
    [ordered]@{TimeUtc=[DateTime]::UtcNow.ToString('o');Head=(& git -C $workspace rev-parse HEAD);Installed=$false;Package=$zip;
        Artifacts=@(Get-ChildItem -LiteralPath (Join-Path $run 'package') -Recurse -File | ForEach-Object { [ordered]@{Path=$_.FullName;Hash=(Get-Sha256Path $_.FullName)} });
        GameAcceptance='Pending: affected-save A/B comparison; no installation in this driver'} | ConvertTo-Json -Depth 6 |
        Set-Content -LiteralPath (Join-Path $run 'result.json') -Encoding UTF8
    Write-Host "PASS: paired build and tests; NO INSTALLATION. Package: $zip"
}
finally {
    $after = Snapshot
    $after | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run 'installed-after.json') -Encoding UTF8
    if (Compare-Object ($before | ConvertTo-Json -Depth 5) ($after | ConvertTo-Json -Depth 5)) { throw 'Installed files changed during build; inspect snapshots.' }
}
