param([string]$Root, [switch]$Deploy)
$ErrorActionPreference = 'Stop'
$Root = [IO.Path]::GetFullPath($Root)
$body = @'
param([switch]$Deploy)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts\_release_common.ps1')
$game = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
Push-Location $PSScriptRoot
try {
    & $msbuild CrusaderDETweaker.csproj /t:Restore "/p:GameDir=$game" /p:Configuration=Release /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    & $msbuild CrusaderDETweaker.csproj /t:Build "/p:GameDir=$game" /p:Configuration=Release /verbosity:minimal
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $output = Join-Path $PSScriptRoot 'bin\Release'
    $version = Get-PluginVersion
    Set-InfoJsonVersion (Join-Path $output 'info.json') $version
    Write-UserReadme (Join-Path $output 'README.txt') $version
    Test-UserReadme (Join-Path $output 'README.txt') $version
    if ($Deploy) {
        & (Join-Path $PSScriptRoot 'scripts\deploy.ps1') -SourceDir $output -GamePath $game
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
} finally { Pop-Location }
exit 0
'@
$driver = @'
@echo off
setlocal
set "PSModulePath=%SystemRoot%\System32\WindowsPowerShell\v1.0\Modules"
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0_local_build.ps1" DEPLOY_ARGUMENT
set "TASK_RESULT=%ERRORLEVEL%"
if /i not "%~1"=="/nopause" pause
exit /b %TASK_RESULT%
'@
$argument = if ($Deploy) { '-Deploy' } else { '' }
foreach ($pair in @(@('_local_build.ps1',$body), @('build.bat',$driver.Replace('DEPLOY_ARGUMENT', $argument)))) {
    $text = $pair[1].Replace("`r`n", "`n").Replace("`n", "`r`n") + "`r`n"
    $path = Join-Path $Root $pair[0]
    [IO.File]::WriteAllText($path, $text, [Text.UTF8Encoding]::new($false))
    if ([IO.File]::ReadAllText($path) -cne $text) { throw "Write mismatch: $path" }
}
$exclude = & git -C $Root rev-parse --git-path info/exclude
if (-not [IO.Path]::IsPathRooted($exclude)) { $exclude = Join-Path $Root $exclude }
$text = [IO.File]::ReadAllText($exclude)
if (-not $text.Contains('/_local_build.ps1')) { [IO.File]::AppendAllText($exclude, "`r`n/_local_build.ps1`r`n/build.bat`r`n") }
