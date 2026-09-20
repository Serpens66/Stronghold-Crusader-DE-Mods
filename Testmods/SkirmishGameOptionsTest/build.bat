@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "EXTRAFEATURES_DIR=%GAME_DIR%\BepInEx\plugins\ExtraFeatures_Serp"
if not exist "%EXTRAFEATURES_DIR%\ExtraFeatures.dll" set "EXTRAFEATURES_DIR=%PROJECT_DIR%..\..\ExtraFeatures\BepInEx\plugins\ExtraFeatures_Serp"
if defined SHCDESE_EXTENDER_DIR set "EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "PLUGIN_NAME=SkirmishGameOptionsTest_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "NO_PAUSE=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build and installation aborted: Stronghold Crusader Definitive Edition is still running.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if not exist "%MSBUILD%" goto failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto failed
if not exist "%EXTRAFEATURES_DIR%\ExtraFeatures.dll" goto failed

pushd "%PROJECT_DIR%"
"%MSBUILD%" tests\SkirmishGameOptionsTest.Tests.csproj /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 ( popd& goto failed )
"%PROJECT_DIR%tests\bin\SkirmishGameOptionsTest.Tests.exe"
if errorlevel 1 ( popd& goto failed )

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
"%MSBUILD%" SkirmishGameOptionsTest.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ExtraFeaturesDir="%EXTRAFEATURES_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd
if not "%BUILD_EXIT_CODE%"=="0" goto failed

if not exist "%LOCAL_PLUGIN_DIR%\SkirmishGameOptionsTest.dll" goto failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto failed
if not exist "%LOCAL_PLUGIN_DIR%\Patches\Assets\GUI\XAMLResources\FRONT_Multiplayer.xaml" goto failed

if "%NO_INSTALL%"=="1" (
  echo Build and tests successful. Installation skipped.
  if "%NO_PAUSE%"=="0" pause
  exit /b 0
)

if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto failed

echo Skirmish Game Options Test built, tested and installed successfully.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:failed
echo Build, test or installation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
