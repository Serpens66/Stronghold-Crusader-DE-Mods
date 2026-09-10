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
if not exist "%PROJECT_DIR%Assets\CrusaderSwordsman\atlas.png" ( echo Private Atlas-Assets fehlen.& goto failed )
if not exist "%PROJECT_DIR%Assets\CrusaderRoundTower\atlas.png" ( echo Private Rundturm-Assets fehlen.& goto failed )
if not exist "%PROJECT_DIR%Assets\CrusaderUI\UIBuildingsO011_colour1.png" ( echo Private HUD-Assets fehlen.& goto failed )

pushd "%PROJECT_DIR%"
"%MSBUILD%" tests\SkinTest.Tests.csproj /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 ( popd& goto failed )
"%PROJECT_DIR%tests\bin\SkinTest.Tests.exe"
if errorlevel 1 ( popd& goto failed )
"%MSBUILD%" SkinTest.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
if "%BUILD_EXIT_CODE%"=="0" "%PROJECT_DIR%tests\bin\SkinTest.Tests.exe" --runtime-assembly "%PROJECT_DIR%BepInEx\plugins\SkinTest_Serp\SkinTest.dll"
if errorlevel 1 set "BUILD_EXIT_CODE=1"
popd
if not "%BUILD_EXIT_CODE%"=="0" goto failed

set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\SkinTest_Serp"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\SkinTest_Serp"
if not exist "%LOCAL_PLUGIN_DIR%\SkinTest.dll" goto failed
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y
if errorlevel 1 goto failed
echo Build, Tests und Installation erfolgreich.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:failed
echo Build, Test oder Installation fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1
