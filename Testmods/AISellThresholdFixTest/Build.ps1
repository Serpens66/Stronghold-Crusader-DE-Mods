[CmdletBinding()]
param([int]$NoInstall=0,[int]$VerifyOnly=0)
$ErrorActionPreference='Stop'
$workspace=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$game='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$msbuild='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] START AISellThresholdFixTest: current working sources' -f (Get-Date))
& (Join-Path $PSScriptRoot 'verify.ps1')
if (-not $?) { throw 'Verification failed.' }
if ($VerifyOnly) { exit 0 }
if (-not $NoInstall -and (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue)) { throw 'Close the game before installation.' }
& $msbuild (Join-Path $PSScriptRoot 'AISellThresholdFixTest.csproj') /t:Rebuild /p:Configuration=Debug /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Runtime build failed.' }
$output=Join-Path $PSScriptRoot 'BepInEx\plugins\AISellThresholdFixTest_Serp'
& dotnet (Join-Path $PSScriptRoot 'tests\bin\Debug\net10.0\AISellThresholdFixTest.Tests.dll') --artifact (Join-Path $output 'AISellThresholdFixTest.dll') ([string]($env:SHCDE_DEVDIAG -eq '1'))
if ($LASTEXITCODE -ne 0) { throw 'Compiled runtime artifact verification failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'info.json') -Destination (Join-Path $output 'info.json') -Force
. (Join-Path $workspace 'Shared\Tools\Release\Release.Common.ps1')
if (-not $NoInstall) {
    $target=Join-Path $game 'BepInEx\plugins\AISellThresholdFixTest_Serp'
    [void][IO.Directory]::CreateDirectory($target)
    foreach ($name in @('AISellThresholdFixTest.dll','AISellThresholdFixTest.pdb','info.json')) {
        $source=Join-Path $output $name
        Copy-Item -LiteralPath $source -Destination (Join-Path $target $name) -Force
        if ((Get-Sha256Hex $source) -cne (Get-Sha256Hex (Join-Path $target $name))) { throw "Installed hash mismatch: $name" }
        Write-Host ("Verified installed {0}: {1}" -f $name,(Get-Sha256Hex $source))
    }
}
Write-Host ('[{0:yyyy-MM-dd HH:mm:ss.fff}] PASS AISellThresholdFixTest built; installed={1}; devdiag={2}' -f (Get-Date),(-not $NoInstall),($env:SHCDE_DEVDIAG -eq '1'))
