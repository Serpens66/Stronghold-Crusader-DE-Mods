@echo off
setlocal EnableExtensions EnableDelayedExpansion
set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
if defined SHCDESE_EXTENDER_DIR set "EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build und Installation abgebrochen: Das Spiel ist noch gestartet.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)
if not exist "%MSBUILD%" ( echo MSBuild wurde nicht gefunden.& goto failed )
if not exist "%EXTENDER_DIR%\SHCDESE.dll" ( echo SHCDESE.dll wurde nicht gefunden: %EXTENDER_DIR%& goto failed )

pushd "%PROJECT_DIR%"
"%MSBUILD%" tests\VirtualUnitsPrototype.Tests.csproj /p:Configuration=Release
if errorlevel 1 ( popd& goto failed )
"%PROJECT_DIR%tests\bin\VirtualUnitsPrototype.Tests.exe"
if errorlevel 1 ( popd& goto failed )
if exist "%PROJECT_DIR%BepInEx\plugins\VirtualUnitsPrototype_Serp\" rmdir /S /Q "%PROJECT_DIR%BepInEx\plugins\VirtualUnitsPrototype_Serp"
"%MSBUILD%" VirtualUnitsPrototype.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd
if not "%BUILD_EXIT_CODE%"=="0" goto failed

set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\VirtualUnitsPrototype_Serp"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\VirtualUnitsPrototype_Serp"
if not exist "%LOCAL_PLUGIN_DIR%\VirtualUnitsPrototype.dll" goto failed
if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y
if errorlevel 1 goto failed
echo Build, Tests und Installation erfolgreich.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:failed
echo Build, Test oder Installation fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1
