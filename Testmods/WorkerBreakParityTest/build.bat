@echo off
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
if defined SHCDESE_EXTENDER_DIR set "EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "NATIVE_DLL=%GAME_DIR%\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"
set "PLUGIN_NAME=WorkerBreakParityTest_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "NO_PAUSE=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"

if "%NO_INSTALL%"=="0" (
  powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
  if errorlevel 1 goto failed
)
if not exist "%MSBUILD%" goto failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto failed
if not exist "%NATIVE_DLL%" goto failed

pushd "%PROJECT_DIR%"
"%MSBUILD%" tests\WorkerBreakParityTest.Tests.csproj /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto failed_popd
"%PROJECT_DIR%tests\bin\WorkerBreakParityTest.Tests.exe" "%NATIVE_DLL%"
if errorlevel 1 goto failed_popd
"%MSBUILD%" WorkerBreakParityTest.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto failed_popd
popd

if not exist "%LOCAL_PLUGIN_DIR%\WorkerBreakParityTest.dll" goto failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto failed
if "%NO_INSTALL%"=="1" goto built_only
if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto failed
:success
echo Worker Break Parity Test built and installed successfully.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:built_only
echo Worker Break Parity Test built successfully. Installation skipped.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:failed_popd
popd
:failed
echo Worker Break Parity Test build or installation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
