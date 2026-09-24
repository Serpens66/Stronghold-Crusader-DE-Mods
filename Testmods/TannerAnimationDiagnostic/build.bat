@echo off
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "PROJECT_ROOT=%~dp0."
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
if defined SHCDESE_EXTENDER_DIR set "EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "PLUGIN_NAME=TannerAnimationDiagnostic_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 goto failed
if not exist "%MSBUILD%" goto failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\Test-Preflight.ps1" -ProjectDir "%PROJECT_ROOT%"
if errorlevel 1 goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\..\Shared\Test-PermanentNativeRuntimePatches.ps1"
if errorlevel 1 goto failed

pushd "%PROJECT_DIR%"
"%MSBUILD%" tests\TannerAnimationDiagnostic.Tests.csproj /p:Configuration=Release
if errorlevel 1 goto failed_popd
"%PROJECT_DIR%tests\bin\TannerAnimationDiagnostic.Tests.exe"
if errorlevel 1 goto failed_popd
"%MSBUILD%" TannerAnimationDiagnostic.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto failed_popd
popd

if not exist "%LOCAL_PLUGIN_DIR%\TannerAnimationDiagnostic.dll" goto failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto failed
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto failed
echo Tanner Animation Diagnostic built and installed successfully.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:failed_popd
popd
:failed
echo Tanner Animation Diagnostic build or installation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
