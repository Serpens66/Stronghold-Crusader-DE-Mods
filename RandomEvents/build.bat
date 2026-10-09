@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START RandomEvents
cd /d "%~dp0"
if errorlevel 1 goto :build_driver_directory_failed
rem The outer driver owns the pause, including failures before compilation.
call :build_driver_main %* /nopause
set "BUILD_DRIVER_RESULT=%ERRORLEVEL%"
cd /d "%BUILD_DRIVER_ORIGINAL_DIR%"
echo [%date% %time%] END: exit code %BUILD_DRIVER_RESULT%
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b %BUILD_DRIVER_RESULT%

:build_driver_directory_failed
echo ERROR: Cannot enter the build directory "%~dp0".
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b 1

:build_driver_main
echo [%date% %time%] Workspace source and runtime preflight
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-SharedBoundaries.ps1"
if errorlevel 1 exit /b 1
echo [%date% %time%] Unit access regression tests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-UnitAccess.ps1"
if errorlevel 1 exit /b 1
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
rem The installed release is canonical; SHCDESE_EXTENDER_DIR is the explicit override.
if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
set "PLUGIN_NAME=RandomEvents_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

rem Never touch build or installation output while the game has the plugin DLL loaded.
echo [%date% %time%] Check that the game is closed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 goto game_running

if not exist "%MSBUILD%" goto build_failed
if exist "%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%\SHCDESE.dll" (
  set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%"
) else if exist "%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%\SHCDESE.dll" (
  set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%"
) else if exist "%GAME_SCRIPT_EXTENDER_DIR%\SHCDESE.dll" (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
) else goto build_failed

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
pushd "%PROJECT_DIR%"
echo [%date% %time%] Compile projects
"%MSBUILD%" RandomEvents.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
popd

echo [%date% %time%] Copy package files
copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
echo [%date% %time%] Copy package files
xcopy "%PROJECT_DIR%Override" "%LOCAL_PLUGIN_DIR%\Override\" /E /I /Q /Y >nul
if not exist "%LOCAL_PLUGIN_DIR%\RandomEvents.dll" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed

rem Overlay plugin files so saved LobbyModSettings survive development builds.
echo [%date% %time%] Copy package files
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto copy_failed
if not exist "%GAME_PLUGIN_DIR%\RandomEvents.dll" goto copy_verification_failed
if not exist "%GAME_PLUGIN_DIR%\info.json" goto copy_verification_failed
if not exist "%GAME_PLUGIN_DIR%\Override\ScriptExtenderUI\RandomEventsSettings.xaml" goto copy_verification_failed
fc /B "%LOCAL_PLUGIN_DIR%\RandomEvents.dll" "%GAME_PLUGIN_DIR%\RandomEvents.dll" >nul
if errorlevel 1 goto copy_verification_failed
fc /B "%LOCAL_PLUGIN_DIR%\info.json" "%GAME_PLUGIN_DIR%\info.json" >nul
if errorlevel 1 goto copy_verification_failed
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName RandomEvents
if errorlevel 1 goto package_failed

echo Build und Installation von Random Events erfolgreich.
exit /b 0

:game_running
echo Build und Installation abgebrochen: Stronghold Crusader Definitive Edition ist noch gestartet.
echo Lokales Paket und installierter Mod wurden nicht veraendert.
exit /b 1
:copy_verification_failed
echo Installationspruefung fehlgeschlagen: DLL, info.json oder Settings-XAML fehlen oder stimmen nicht mit dem lokalen Paket ueberein.
exit /b 1
:build_failed_popd
popd
:build_failed
echo Build fehlgeschlagen.
exit /b 1
:package_failed
echo Paketpruefung fehlgeschlagen.
exit /b 1
:copy_failed
echo Installation fehlgeschlagen. Ist das Spiel noch gestartet?
exit /b 1
