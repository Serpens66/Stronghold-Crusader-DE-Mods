@echo off
setlocal EnableExtensions

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "GAME_API_DIR=%GAME_DIR%\BepInEx\plugins\APIShared_Serp"
set "LOCAL_API_DIR=%PROJECT_DIR%..\..\APIShared\BepInEx\plugins\APIShared_Serp"
set "API_SHARED_DIR="
set "PLUGIN_NAME=MainViewModelInitProbe_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build and installation aborted: Stronghold Crusader Definitive Edition is still running.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if not exist "%MSBUILD%" goto build_failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto extender_missing
if exist "%GAME_API_DIR%\APIShared.dll" (
  set "API_SHARED_DIR=%GAME_API_DIR%"
) else if exist "%LOCAL_API_DIR%\APIShared.dll" (
  set "API_SHARED_DIR=%LOCAL_API_DIR%"
) else goto api_missing

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
pushd "%PROJECT_DIR%"
"%MSBUILD%" MainViewModelInitProbe.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%"
if errorlevel 1 goto build_failed_popd
popd

copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if not exist "%LOCAL_PLUGIN_DIR%\MainViewModelInitProbe.dll" goto package_failed
if exist "%LOCAL_PLUGIN_DIR%\APIShared.dll" goto package_failed
if exist "%LOCAL_PLUGIN_DIR%\SHCDESE.dll" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed

if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto copy_failed
echo MainViewModel Init Probe built and installed successfully.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:build_failed_popd
popd
:build_failed
echo Build failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
:extender_missing
echo Installed SHCDE-SE 2.3.0 was not found.
if "%NO_PAUSE%"=="0" pause
exit /b 1
:api_missing
echo APIShared.dll was not found.
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
