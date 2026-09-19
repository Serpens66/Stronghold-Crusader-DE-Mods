@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
if defined SHCDESE_EXTENDER_DIR set "EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "API_SHARED_DIR=%GAME_DIR%\BepInEx\plugins\APIShared_Serp"
set "PLUGIN_NAME=PreplacedTest_Serp"
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

if not exist "%MSBUILD%" goto build_failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto build_failed
if not exist "%API_SHARED_DIR%\APIShared.dll" goto api_shared_missing
if not exist "%API_SHARED_DIR%\info.json" goto api_shared_missing
powershell.exe -NoProfile -Command "$manifest = Get-Content -LiteralPath '%API_SHARED_DIR%\info.json' -Raw | ConvertFrom-Json; if ([version]$manifest.Version -lt [version]'0.3.6') { exit 1 }"
if errorlevel 1 goto api_shared_too_old

pushd "%PROJECT_DIR%"
"%MSBUILD%" tests\PreplacedTest.Tests.csproj /p:Configuration=Debug
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%tests\bin\PreplacedTest.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
popd

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
pushd "%PROJECT_DIR%"
"%MSBUILD%" PreplacedTest.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
popd

copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if not exist "%LOCAL_PLUGIN_DIR%\PreplacedTest.dll" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\PreplacedTest.pdb" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed

if "%NO_INSTALL%"=="1" (
  echo Build and tests successful. Installation skipped.
  if "%NO_PAUSE%"=="0" pause
  exit /b 0
)

if exist "%GAME_PLUGIN_DIR%\" (
  for /D %%D in ("%GAME_PLUGIN_DIR%\*") do rmdir /S /Q "%%~fD"
  for %%F in ("%GAME_PLUGIN_DIR%\*") do if exist "%%~fF" if not exist "%%~fF\" del /F /Q "%%~fF"
)
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto copy_failed

echo Preplaced Test built and installed successfully.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:build_failed_popd
popd
:build_failed
echo Build failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:api_shared_missing
echo Build aborted: APIShared_Serp 0.3.6 or newer is required. Build and install APIShared\build.bat first.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:api_shared_too_old
echo Build aborted: installed APIShared_Serp is older than 0.3.6. Build and install APIShared\build.bat first.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:package_failed
echo Package validation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:copy_failed
echo Installation failed. Is the game still running?
if "%NO_PAUSE%"=="0" pause
exit /b 1
