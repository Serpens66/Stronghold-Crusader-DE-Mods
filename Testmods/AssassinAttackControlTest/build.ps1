[CmdletBinding()]
param([switch]$NoPause)
$ErrorActionPreference='Stop'
function Get-Sha256([string]$Path) {
    $stream=[IO.File]::OpenRead($Path)
    $algorithm=[Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-','') }
    finally { $algorithm.Dispose(); $stream.Dispose() }
}

$exitCode=1
try {
    if(Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Game is running; build and install postponed.' }
    $modRoot=$PSScriptRoot
    $workspace=(Resolve-Path (Join-Path $modRoot '..\..')).Path
    $game='E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
    $extender=Join-Path $game 'BepInEx\plugins\000shcdese'
    $api=Join-Path $workspace 'APIShared\BepInEx\plugins\APIShared_Serp'
    $msbuild='C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    & (Join-Path $modRoot 'verify.ps1')
    if(-not $?) { throw 'Preflight failed' }
    & $msbuild (Join-Path $modRoot 'tests\AssassinAttackControlTests.csproj') /p:Configuration=Release /nologo /verbosity:minimal
    if($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
    & (Join-Path $modRoot 'tests\bin\AssassinAttackControlTests.exe')
    if($LASTEXITCODE -ne 0) { throw 'Tests failed' }
    & $msbuild (Join-Path $modRoot 'AssassinAttackControlTest.csproj') /p:Configuration=Release "/p:GameDir=$game" "/p:ExtenderDir=$extender" "/p:ApiSharedDir=$api" /nologo /verbosity:minimal
    if($LASTEXITCODE -ne 0) { throw 'Runtime build failed' }
    $package=Join-Path $modRoot 'BepInEx\plugins\AssassinAttackControlTest_Serp'
    Copy-Item -LiteralPath (Join-Path $modRoot 'info.json') -Destination $package -Force
    Copy-Item -LiteralPath (Join-Path $modRoot 'Override') -Destination $package -Recurse -Force
    $installed=Join-Path $game 'BepInEx\plugins\AssassinAttackControlTest_Serp'
    [IO.Directory]::CreateDirectory($installed) | Out-Null
    foreach($file in Get-ChildItem -LiteralPath $package -Recurse -File) {
        $relative=$file.FullName.Substring($package.Length+1)
        $destination=Join-Path $installed $relative
        [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
        if((Get-Sha256 $file.FullName) -ne (Get-Sha256 $destination)) { throw "Package mismatch: $relative" }
    }
    Write-Host 'Assassin Attack Control Test built, installed and package equality verified.'
    $exitCode=0
}
catch { Write-Error -Message $_ -ErrorAction Continue }
if(-not $NoPause) { Read-Host 'Press Enter' | Out-Null }
exit $exitCode
