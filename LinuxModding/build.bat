@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "PLUGIN_NAME=LinuxModding_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

if not exist "%MSBUILD%" (
  echo MSBuild wurde nicht gefunden: !MSBUILD!
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if not exist "%GAME_DIR%\BepInEx\core\BepInEx.dll" (
  echo BepInEx.dll wurde nicht gefunden: !GAME_DIR!\BepInEx\core\BepInEx.dll
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

pushd "%PROJECT_DIR%"
"%MSBUILD%" LinuxModding.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd

if not "%BUILD_EXIT_CODE%"=="0" goto build_failed

copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if errorlevel 1 goto copy_failed
copy /Y "%PROJECT_DIR%shcde-linux-launcher.sh" "%LOCAL_PLUGIN_DIR%\shcde-linux-launcher.sh" >nul
if errorlevel 1 goto copy_failed

echo Build erfolgreich. Das lokale Linux-Paket wurde erstellt:
echo !LOCAL_PLUGIN_DIR!
echo Der Windows-Spielordner wurde nicht veraendert.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:build_failed
echo Build fehlgeschlagen. Exit Code: %BUILD_EXIT_CODE%
if "%NO_PAUSE%"=="0" pause
exit /b %BUILD_EXIT_CODE%

:copy_failed
echo Erstellen des lokalen Linux-Pakets fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1
