@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "PROJECT_ROOT=%~dp0."
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "PLUGIN_NAME=WaterboyTargetReservationTest_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "HOST_CLIENT_TEST_DIR=%PROJECT_DIR%..\..\_inspect\HostClientPresetTests"
set "NO_PAUSE=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"

powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build and installation aborted: Stronghold Crusader Definitive Edition is still running.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if not exist "%MSBUILD%" goto build_failed
if not exist "%GAME_SCRIPT_EXTENDER_DIR%\SHCDESE.dll" goto build_failed
if not exist "%GAME_DIR%\BepInEx\plugins\APIShared_Serp\APIShared.dll" goto build_failed

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\Test-Preflight.ps1" -ProjectDir "%PROJECT_ROOT%"
if errorlevel 1 goto build_failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\..\Shared\Test-PermanentNativeRuntimePatches.ps1"
if errorlevel 1 goto build_failed

pushd "%HOST_CLIENT_TEST_DIR%"
"%MSBUILD%" HostClientPresetTests.csproj /p:Configuration=Debug
if errorlevel 1 goto build_failed_popd
"%HOST_CLIENT_TEST_DIR%\bin\HostClientPresetTests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
popd

pushd "%PROJECT_DIR%"
"%MSBUILD%" tests\WaterboyTargetReservationTest.Tests.csproj /p:Configuration=Debug /p:ExtenderDir="%GAME_SCRIPT_EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%tests\bin\WaterboyTargetReservationTest.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
popd

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
pushd "%PROJECT_DIR%"
"%MSBUILD%" WaterboyTargetReservationTest.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%GAME_SCRIPT_EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
popd

copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if not exist "%LOCAL_PLUGIN_DIR%\WaterboyTargetReservationTest.dll" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\Override\ScriptExtenderUI\WaterboyTargetReservationTestSettings.xaml" goto package_failed
if "%NO_INSTALL%"=="1" goto built_without_install

if exist "%GAME_PLUGIN_DIR%\" (
  for /D %%D in ("%GAME_PLUGIN_DIR%\*") do rmdir /S /Q "%%~fD"
  for %%F in ("%GAME_PLUGIN_DIR%\*") do if exist "%%~fF" if not exist "%%~fF\" del /F /Q "%%~fF"
)
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto copy_failed

echo Waterboy Target Reservation Test built and installed successfully.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:built_without_install
echo Waterboy Target Reservation Test built successfully. Installation skipped.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:build_failed_popd
popd
:build_failed
echo Build failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:package_failed
echo Package validation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:copy_failed
echo Installation failed. Is the game still running?
if "%NO_PAUSE%"=="0" pause
exit /b 1
