@echo off
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "SOURCE_DIR=%PROJECT_DIR%source\testlord_serp_fixesprobe"
set "TARGET_DIR=C:\Users\Serpens66\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\CustomLords\testlord_serp_fixesprobe"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 goto game_running
if not exist "%SOURCE_DIR%\info.json" goto failed
if not exist "%SOURCE_DIR%\lordmeta.json" goto failed
if not exist "%SOURCE_DIR%\Override\Fixes\preferences.json" goto failed
if exist "%TARGET_DIR%\" goto existing_target
xcopy "%SOURCE_DIR%\*" "%TARGET_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto failed
echo Fixes Override Probe Lord installed successfully.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:game_running
echo Installation aborted: the game is running.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:existing_target
echo Installation aborted: target lord already exists.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:failed
echo Installation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
