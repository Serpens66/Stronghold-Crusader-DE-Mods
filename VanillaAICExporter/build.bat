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

if not exist "%MSBUILD%" (
  echo MSBuild wurde nicht gefunden:
  echo !MSBUILD!
  goto failed
)

if not exist "%GAME_DIR%\BepInEx\core\BepInEx.dll" (
  echo BepInEx.dll wurde im Spielordner nicht gefunden:
  echo !GAME_DIR!\BepInEx\core\BepInEx.dll
  goto failed
)

if exist "%LOCAL_SCRIPT_EXTENDER_ROOT%\" (
  if exist "%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%\SHCDESE.dll" (
    set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%"
  ) else if exist "%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%\SHCDESE.dll" (
    set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%"
  ) else (
    echo Der lokale Script Extender wurde gefunden, aber seine SHCDESE.dll fehlt.
    goto failed
  )
) else (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
)

echo Verwende Script Extender Referenzen:
echo !EXTENDER_DIR!
echo.

pushd "%PROJECT_DIR%"
"%MSBUILD%" VanillaAICExporter.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd
if not "%BUILD_EXIT_CODE%"=="0" goto failed

set "PLUGIN_NAME=VanillaAICExporter_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"

if exist "%GAME_PLUGIN_DIR%\" (
  rem Keep player-created lobby settings while replacing all packaged files.
  for /D %%D in ("%GAME_PLUGIN_DIR%\*") do (
    if /I not "%%~nxD"=="LobbyModSettings" (
      rmdir /S /Q "%%~fD"
      if errorlevel 1 goto copy_failed
    )
  )
  for %%F in ("%GAME_PLUGIN_DIR%\*") do (
    if exist "%%~fF" if not exist "%%~fF\" (
      del /F /Q "%%~fF"
      if errorlevel 1 goto copy_failed
    )
  )
)
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Y
if errorlevel 1 goto copy_failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Release\Write-LocalBuildManifest.ps1" -ModName VanillaAICExporter
if errorlevel 1 goto copy_failed

echo.
echo Build und Installation erfolgreich.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:copy_failed
echo.
echo Kopieren fehlgeschlagen. Ist das Spiel noch gestartet?
if "%NO_PAUSE%"=="0" pause
exit /b 1

:failed
echo.
echo Build fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1
