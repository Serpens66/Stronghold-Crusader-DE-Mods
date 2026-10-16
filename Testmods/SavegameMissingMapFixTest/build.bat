@echo off
setlocal EnableExtensions
set "NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NOPAUSE=1"
pushd "%~dp0"
call :main
set "RESULT=%ERRORLEVEL%"
popd
if "%NOPAUSE%"=="0" pause
exit /b %RESULT%
:main
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "SHCDE_API_SHARED_DIR=%GAME_DIR%\BepInEx\plugins\APIShared_Serp"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify.ps1"
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { Write-Error 'Close the game before installation.'; exit 1 }"
if errorlevel 1 exit /b 1
"%MSBUILD%" "%~dp0SavegameMissingMapFixTest.csproj" /nologo /verbosity:minimal "/p:ApiSharedDir=%SHCDE_API_SHARED_DIR%"
if errorlevel 1 exit /b 1
copy /Y "%~dp0info.json" "%~dp0BepInEx\plugins\SavegameMissingMapFixTest_Serp\info.json" >nul
if errorlevel 1 exit /b 1
xcopy "%~dp0BepInEx\plugins\SavegameMissingMapFixTest_Serp" "%GAME_DIR%\BepInEx\plugins\SavegameMissingMapFixTest_Serp\" /E /I /Q /Y
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify-installed.ps1"
exit /b %ERRORLEVEL%
