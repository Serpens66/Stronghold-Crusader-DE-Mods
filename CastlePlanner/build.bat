@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
if /I "%~1"=="/nopause" set "NO_PAUSE=1"

rem Never touch build or installation output while the game has plugin DLLs loaded.
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build und Installation abgebrochen: Stronghold Crusader Definitive Edition ist noch gestartet.
  echo Lokales Paket und installierter Mod wurden nicht veraendert.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if not exist "%MSBUILD%" (
  echo MSBuild wurde nicht gefunden:
  echo !MSBUILD!
  echo.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if not exist "%GAME_DIR%\BepInEx\core\BepInEx.dll" (
  echo BepInEx.dll wurde im Spielordner nicht gefunden:
  echo !GAME_DIR!\BepInEx\core\BepInEx.dll
  echo.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if exist "%LOCAL_SCRIPT_EXTENDER_ROOT%\" (
  if exist "%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%\SHCDESE.dll" (
    set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%"
  ) else if exist "%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%\SHCDESE.dll" (
    set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%"
  ) else (
    echo Lokaler Script Extender Nebenordner wurde gefunden:
    echo !LOCAL_SCRIPT_EXTENDER_ROOT!
    echo.
    echo Aber es wurde keine lokale SHCDESE.dll gefunden.
    echo Baue zuerst ..\shcde-script-extender\build.bat oder entferne den Nebenordner,
    echo wenn gegen die installierte Spiel-DLL kompiliert werden soll.
    echo.
    if "%NO_PAUSE%"=="0" pause
    exit /b 1
  )
) else (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
)

if not exist "%EXTENDER_DIR%\SHCDESE.dll" (
  echo SHCDESE.dll wurde nicht gefunden:
  echo !EXTENDER_DIR!\SHCDESE.dll
  echo.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

echo Verwende Script Extender Referenzen:
echo !EXTENDER_DIR!
echo.

pushd "%PROJECT_DIR%"
dotnet restore AIVPlacement.Tests\CastlePlanner.AIVPlacement.Tests.csproj -m:1 -p:BuildInParallel=false
if errorlevel 1 (
  set "BUILD_EXIT_CODE=1"
  popd
  goto build_failed
)
dotnet build AIVPlacement.Tests\CastlePlanner.AIVPlacement.Tests.csproj -c Release --no-restore -m:1 -p:BuildInParallel=false
if errorlevel 1 (
  set "BUILD_EXIT_CODE=1"
  popd
  goto build_failed
)
"%PROJECT_DIR%AIVPlacement.Tests\bin\Release\net10.0\CastlePlanner.AIVPlacement.Tests.exe"
if errorlevel 1 (
  set "BUILD_EXIT_CODE=1"
  popd
  goto build_failed
)

"%MSBUILD%" CastlePlanner.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd

echo.
if "%BUILD_EXIT_CODE%"=="0" (
  echo Build erfolgreich.
  echo Kopiere Plugin in den Spielordner...
  set "PLUGIN_NAME=CastlePlanner_Serp"
  set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\!PLUGIN_NAME!"
  set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!PLUGIN_NAME!"

  if not exist "!LOCAL_PLUGIN_DIR!\" (
    echo Lokaler Plugin-Ordner wurde nicht gefunden:
    echo !LOCAL_PLUGIN_DIR!
    goto copy_failed
  )

  rem Remove the superseded patch so upgrades cannot leave a second HUD behind.
  set "LEGACY_MAIN_HUD_PATCH=!GAME_PLUGIN_DIR!\Patches\Assets\GUI\XAML\MainHUD.xaml"
  if exist "!LEGACY_MAIN_HUD_PATCH!" (
    del /Q "!LEGACY_MAIN_HUD_PATCH!"
    if exist "!LEGACY_MAIN_HUD_PATCH!" goto copy_failed
  )

  rem Overlay managed files so Script Extender Msgpack settings survive rebuilds.
  xcopy "!LOCAL_PLUGIN_DIR!" "!GAME_PLUGIN_DIR!\" /E /I /Q /Y
  if errorlevel 1 goto copy_failed
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Release\Write-LocalBuildManifest.ps1" -ModName CastlePlanner
  if errorlevel 1 goto copy_failed
  echo Plugin kopiert; vorhandene Laufzeitdaten wurden beibehalten.
) else (
  echo Build fehlgeschlagen. Exit Code: %BUILD_EXIT_CODE%
)
echo.
if "%NO_PAUSE%"=="0" pause
exit /b %BUILD_EXIT_CODE%

:build_failed
echo.
echo Build oder AIV-Placement-Tests fehlgeschlagen.
echo.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:copy_failed
echo.
echo Kopieren fehlgeschlagen. Ist das Spiel noch gestartet?
echo Beende Stronghold Crusader Definitive Edition und starte build.bat erneut.
echo.
if "%NO_PAUSE%"=="0" pause
exit /b 1
