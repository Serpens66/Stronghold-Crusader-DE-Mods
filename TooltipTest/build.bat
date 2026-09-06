@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build und Installation abgebrochen: Das Spiel ist noch gestartet.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if not exist "%MSBUILD%" (
  echo MSBuild wurde nicht gefunden: !MSBUILD!
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if exist "%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%\SHCDESE.dll" (
  set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%"
) else if exist "%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%\SHCDESE.dll" (
  set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%"
) else (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
)

if not exist "!EXTENDER_DIR!\SHCDESE.dll" (
  echo SHCDESE.dll wurde nicht gefunden: !EXTENDER_DIR!\SHCDESE.dll
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

pushd "%PROJECT_DIR%"
"%MSBUILD%" TooltipTest.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="!EXTENDER_DIR!"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd

if not "%BUILD_EXIT_CODE%"=="0" goto build_failed

set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\TooltipTest_Serp"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\TooltipTest_Serp"
if exist "!GAME_PLUGIN_DIR!\" rmdir /S /Q "!GAME_PLUGIN_DIR!"
xcopy "!LOCAL_PLUGIN_DIR!" "!GAME_PLUGIN_DIR!\" /E /I /Y
if errorlevel 1 goto copy_failed

echo Build und Installation erfolgreich.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:build_failed
echo Build fehlgeschlagen. Exit Code: %BUILD_EXIT_CODE%
if "%NO_PAUSE%"=="0" pause
exit /b %BUILD_EXIT_CODE%

:copy_failed
echo Installation fehlgeschlagen. Ist das Spiel noch gestartet?
if "%NO_PAUSE%"=="0" pause
exit /b 1
