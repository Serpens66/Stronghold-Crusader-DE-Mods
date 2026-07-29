@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "GAME_EXE=%GAME_DIR%\Stronghold Crusader Definitive Edition.exe"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\VanillaAICExporter_Serp"
set "MARKER=%GAME_PLUGIN_DIR%\last_export.txt"
set "REQUEST=%GAME_PLUGIN_DIR%\export_requested.txt"
set "WORKSPACE_EXPORTS=%PROJECT_DIR%Exports"
set "NO_PAUSE=0"
if /I "%~1"=="/nopause" set "NO_PAUSE=1"

call "%PROJECT_DIR%build.bat" /nopause
if errorlevel 1 goto failed

if exist "%MARKER%" del /Q "%MARKER%"
if errorlevel 1 goto failed
> "%REQUEST%" echo requested
if errorlevel 1 goto failed

echo.
echo Starte Stronghold Crusader Definitive Edition...
start "" "%GAME_EXE%"

echo Warte ohne Zeitlimit auf den Vanilla-AIC-Export.
echo Das Spiel darf geschlossen werden, sobald diese BAT Erfolg meldet.

:wait_for_export
if exist "%MARKER%" goto export_ready
ping -n 2 127.0.0.1 >nul 2>&1
goto wait_for_export

:export_ready
set /P "SOURCE_EXPORT="<"%MARKER%"
if not exist "!SOURCE_EXPORT!\" (
  echo Der Export-Marker verweist auf einen nicht vorhandenen Ordner:
  echo !SOURCE_EXPORT!
  goto failed
)

for %%D in ("!SOURCE_EXPORT!") do set "EXPORT_NAME=%%~nxD"
set "TARGET_EXPORT=%WORKSPACE_EXPORTS%\!EXPORT_NAME!"
if not exist "%WORKSPACE_EXPORTS%\" mkdir "%WORKSPACE_EXPORTS%"
xcopy "!SOURCE_EXPORT!" "!TARGET_EXPORT!\" /E /I /Y
if errorlevel 1 goto failed

echo.
echo Vanilla-lordjson erfolgreich erstellt und in den Workspace kopiert:
echo !TARGET_EXPORT!
echo.
echo Bitte schliesse jetzt Stronghold Crusader Definitive Edition.
echo Danach wird das installierte Export-Plugin automatisch entfernt.

:wait_for_game_exit
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }"
if errorlevel 1 goto remove_installed_plugin
ping -n 2 127.0.0.1 >nul 2>&1
goto wait_for_game_exit

:remove_installed_plugin
if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"
if exist "%GAME_PLUGIN_DIR%\" (
  echo Das installierte Plugin konnte nicht entfernt werden:
  echo %GAME_PLUGIN_DIR%
  goto failed
)

echo Installiertes Export-Plugin wurde entfernt.
echo.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:failed
echo.
echo Export fehlgeschlagen. Details stehen gegebenenfalls in:
echo %GAME_DIR%\BepInEx\LogOutput.log
echo.
if "%NO_PAUSE%"=="0" pause
exit /b 1
