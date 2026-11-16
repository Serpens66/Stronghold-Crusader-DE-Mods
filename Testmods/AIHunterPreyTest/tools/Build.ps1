[CmdletBinding()]
param(
    [string]$GameDir = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition',
    [string]$ApiSharedDir,
    [switch]$NoInstall
)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workspace = [IO.Path]::GetFullPath((Join-Path $project '..\..'))
. (Join-Path $workspace 'Shared\Tools\ApiSharedRepository\RepositoryProcess.Common.ps1')
$extender = Join-Path $GameDir 'BepInEx\plugins\000shcdese'
if (!$ApiSharedDir) { $ApiSharedDir = Join-Path $workspace 'APIShared\BepInEx\plugins\APIShared_Serp' }
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
$package = Join-Path $project 'BepInEx\plugins\AIHunterPreyTest_Serp'
$installed = Join-Path $GameDir 'BepInEx\plugins\AIHunterPreyTest_Serp'
function Step([string]$Name, [scriptblock]$Action) {
    Write-Host "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff')] $Name"
    $global:LASTEXITCODE = 0
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name failed: $LASTEXITCODE" }
}
function Assert-ClosedGame {
    if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) {
        throw 'Close the game before replacing the test mod.'
    }
}
function Clear-Output([string]$Path, [string]$Parent) {
    $target = [IO.Path]::GetFullPath($Path)
    $root = [IO.Path]::GetFullPath($Parent).TrimEnd('\') + '\'
    if (!$target.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($target) -cne 'AIHunterPreyTest_Serp') { throw 'Unexpected output target.' }
    for ($part = $target; $part; $part = [IO.Path]::GetDirectoryName($part)) {
        if ((Test-Path -LiteralPath $part) -and ([IO.File]::GetAttributes($part) -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Linked output path: $part"
        }
    }
    if (Test-Path -LiteralPath $target) {
        if (Get-ChildItem -LiteralPath $target -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) {
            throw 'Linked output entry.'
        }
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}
try {
    if (!$NoInstall) { Assert-ClosedGame }
    Step 'APIShared package provenance' {
        & (Join-Path $workspace 'Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1') -Workspace $workspace -PackageDirectory $ApiSharedDir
    }
    Step 'Scoped runtime preflight' {
        & (Join-Path $workspace 'Shared\Tools\Validation\Test-SharedBoundaries.ps1') -Workspace $workspace -ModDirectory 'Testmods/AIHunterPreyTest' -ConsumerOnly
    }
    Step 'UI and source verification' { & (Join-Path $project 'verify.ps1') }
    $common = @('/p:Configuration=Release', '/p:ImportDirectoryBuildTargets=false', "/p:GameDir=$GameDir", "/p:ExtenderDir=$extender", "/p:ApiSharedDir=$ApiSharedDir", '/nologo', '/v:minimal')
    Clear-Output $package (Join-Path $project 'BepInEx\plugins')
    Step 'Compile test mod' { & $msbuild (Join-Path $project 'AIHunterPreyTest.csproj') /t:Rebuild @common }
    Step 'Compile prey and drain tests' { & $msbuild (Join-Path $project 'tests\AIHunterPreyTest.Tests.csproj') /t:Build @common }
    Step 'Run prey and drain tests' { & (Join-Path $project 'tests\bin\AIHunterPreyTest.Tests.exe') }
    # The harness's standard Directory.Build.targets supplies TestWorkDirectory.
    $presetCommon = @($common | Where-Object { $_ -notlike '/p:ImportDirectoryBuildTargets=*' })
    Step 'Compile preset harness' { & $msbuild (Join-Path $workspace '_inspect\HostClientPresetTests\HostClientPresetTests.csproj') /t:Build @presetCommon }
    Step 'Run focused preset integration (outside sandbox)' { & (Join-Path $workspace '_inspect\HostClientPresetTests\bin\HostClientPresetTests.exe') 'ai-hunter-prey' }
    $expected = @('AIHunterPreyTest.dll', 'AIHunterPreyTest.pdb', 'info.json', 'Override\ScriptExtenderUI\AIHunterPreyTestSettings.xaml')
    foreach ($relative in $expected) {
        if (!(Test-Path -LiteralPath (Join-Path $package $relative) -PathType Leaf)) { throw "Missing package file: $relative" }
    }
    if (@(Get-ChildItem -LiteralPath $package -Recurse -File).Count -ne $expected.Count) { throw 'Unexpected package file.' }
    if (!$NoInstall) {
        Assert-ClosedGame
        Clear-Output $installed (Join-Path $GameDir 'BepInEx\plugins')
        $null = New-Item -ItemType Directory -Path $installed -Force
        Get-ChildItem -LiteralPath $package | Copy-Item -Destination $installed -Recurse -Force
        foreach ($relative in $expected) {
            if ((Get-ApiRepositoryHash (Join-Path $package $relative)) -cne (Get-ApiRepositoryHash (Join-Path $installed $relative))) {
                throw "Installed file differs: $relative"
            }
        }
    }
    Write-Host "PASS: build, prey/drain tests, focused preset integration; installed=$(!$NoInstall). Game acceptance remains pending."
} catch { Write-Error $_; exit 1 }
