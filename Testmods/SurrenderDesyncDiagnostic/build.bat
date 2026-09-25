@echo off
setlocal EnableExtensions

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "API_SHARED_DIR=%GAME_DIR%\BepInEx\plugins\APIShared_Serp"
set "BUGFIXES_DIR=%GAME_DIR%\BepInEx\plugins\BugfixesAndQoL_Serp"
set "PLUGIN_NAME=SurrenderDesyncDiagnostic_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 goto game_running
if not exist "%MSBUILD%" goto failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto failed
if not exist "%API_SHARED_DIR%\APIShared.dll" goto failed
if not exist "%BUGFIXES_DIR%\BugfixesAndQoL.dll" goto failed

pushd "%PROJECT_DIR%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-Preflight.ps1"
if errorlevel 1 goto failed_popd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\..\Shared\Test-PermanentNativeRuntimePatches.ps1"
if errorlevel 1 goto failed_popd
"%MSBUILD%" SurrenderDesyncDiagnostic.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%" /p:BugfixesDir="%BUGFIXES_DIR%"
if errorlevel 1 goto failed_popd
popd

copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if errorlevel 1 goto failed
if not exist "%LOCAL_PLUGIN_DIR%\SurrenderDesyncDiagnostic.dll" goto failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto failed
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto failed
echo Surrender Desync Diagnostic built and installed.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:failed_popd
popd
:failed
echo Surrender Desync Diagnostic build or installation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
:game_running
echo Game is running; build and installation aborted.
if "%NO_PAUSE%"=="0" pause
exit /b 1
