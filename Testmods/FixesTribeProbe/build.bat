@echo off
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "PLUGIN_NAME=FixesTribeProbe_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 goto game_running
if not exist "%MSBUILD%" goto missing_reference
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto missing_reference
if not exist "%EXTENDER_DIR%\R3.dll" goto missing_reference
if not exist "%EXTENDER_DIR%\RedBird.Core.dll" goto missing_reference
if not exist "%GAME_DIR%\BepInEx\plugins\fixes\fixes.dll" goto missing_reference

pushd "%PROJECT_DIR%"
"%MSBUILD%" FixesTribeProbe.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
set "BUILD_RESULT=%ERRORLEVEL%"
popd
if not "%BUILD_RESULT%"=="0" goto failed
if not exist "%LOCAL_PLUGIN_DIR%\FixesTribeProbe.dll" goto failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto failed
if not exist "%GAME_PLUGIN_DIR%" mkdir "%GAME_PLUGIN_DIR%"
copy /Y "%LOCAL_PLUGIN_DIR%\FixesTribeProbe.dll" "%GAME_PLUGIN_DIR%\FixesTribeProbe.dll" >nul
if errorlevel 1 goto failed
copy /Y "%LOCAL_PLUGIN_DIR%\FixesTribeProbe.pdb" "%GAME_PLUGIN_DIR%\FixesTribeProbe.pdb" >nul
if errorlevel 1 goto failed
copy /Y "%LOCAL_PLUGIN_DIR%\info.json" "%GAME_PLUGIN_DIR%\info.json" >nul
if errorlevel 1 goto failed
echo Fixes Tribe Probe built and installed on the host.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:game_running
echo Build and installation aborted: the game is running.
if "%NO_PAUSE%"=="0" pause
exit /b 1
:missing_reference
echo Required build tool or installed dependency is missing.
if "%NO_PAUSE%"=="0" pause
exit /b 1
:failed
echo Build or installation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
