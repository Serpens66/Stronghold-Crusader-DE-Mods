@echo off
setlocal EnableExtensions

set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "GAME_PATCHER=%GAME_DIR%\BepInEx\patchers\StartupPerformanceDiagnostic.Patcher.dll"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\StartupPerformanceDiagnostic_Serp"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Uninstall aborted: Stronghold Crusader Definitive Edition is still running.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if exist "%GAME_PATCHER%" del /F /Q "%GAME_PATCHER%"
if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"

if exist "%GAME_PATCHER%" goto uninstall_failed
if exist "%GAME_PLUGIN_DIR%\" goto uninstall_failed

echo Startup Performance Diagnostic was completely removed from the game installation.
echo Existing entries in BepInEx\LogOutput.log were intentionally preserved.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:uninstall_failed
echo Uninstall failed. Run this file with sufficient permissions and ensure the game is closed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
