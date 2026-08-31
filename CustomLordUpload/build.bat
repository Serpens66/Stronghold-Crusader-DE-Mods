@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "PLUGIN_NAME=CustomLordUpload_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

rem Never touch build or installation output while the game has plugin DLLs loaded.
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build und Installation abgebrochen: Stronghold Crusader Definitive Edition ist noch gestartet.
  echo Lokales Paket und installierter Mod wurden nicht veraendert.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

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
"%MSBUILD%" CustomLordUpload.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
popd

copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
xcopy "%PROJECT_DIR%Patches" "%LOCAL_PLUGIN_DIR%\Patches\" /E /I /Q /Y >nul
xcopy "%PROJECT_DIR%Locales" "%LOCAL_PLUGIN_DIR%\Locales\" /E /I /Q /Y >nul
if not exist "%LOCAL_PLUGIN_DIR%\CustomLordUpload.dll" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\Patches\Assets\GUI\XAMLResources\HUD_ConfirmationPopup.xaml" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\Patches\Assets\GUI\XAMLResources\FRONT_EditorSetup.xaml" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\Locales\en-US.txt" goto package_failed
if exist "%LOCAL_PLUGIN_DIR%\Override\Assets\GUI\XAMLResources\HUD_ConfirmationPopup.xaml" goto package_failed

if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto copy_failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Release\Write-LocalBuildManifest.ps1" -ModName CustomLordUpload
if errorlevel 1 goto package_failed
echo Build und Installation von Custom Lord Upload erfolgreich.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:build_failed_popd
popd
:build_failed
echo Build fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1
:package_failed
echo Paketpruefung fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1
:copy_failed
echo Installation fehlgeschlagen. Ist das Spiel noch gestartet?
if "%NO_PAUSE%"=="0" pause
exit /b 1
