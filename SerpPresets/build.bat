@echo off
setlocal EnableExtensions

set "PROJECT_DIR=%~dp0"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\SerpPresets_Serp"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Installation abgebrochen: Das Spiel ist noch gestartet.
  goto failed
)

if not exist "%GAME_DIR%\BepInEx\plugins\000shcdese\SHCDESE.dll" (
  echo Der installierte Script Extender wurde nicht gefunden.
  goto failed
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%verify.ps1" -PackageRoot "%PROJECT_DIR%."
if errorlevel 1 goto failed

if not exist "%GAME_PLUGIN_DIR%\" mkdir "%GAME_PLUGIN_DIR%"
if errorlevel 1 goto failed
copy /Y "%PROJECT_DIR%info.json" "%GAME_PLUGIN_DIR%\info.json" >nul
if errorlevel 1 goto failed
xcopy "%PROJECT_DIR%Override" "%GAME_PLUGIN_DIR%\Override\" /E /I /Q /Y >nul
if errorlevel 1 goto failed

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%verify.ps1" -PackageRoot "%PROJECT_DIR%." -CompareRoot "%GAME_PLUGIN_DIR%"
if errorlevel 1 goto failed

echo SerpPresets erfolgreich geprueft und installiert.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:failed
echo SerpPresets-Pruefung oder Installation fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1
