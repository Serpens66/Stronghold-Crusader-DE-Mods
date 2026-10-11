@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START ArabianIdleSoundTest
cd /d "%~dp0"
if errorlevel 1 exit /b 1
call :build_main
set "BUILD_DRIVER_RESULT=%ERRORLEVEL%"
cd /d "%BUILD_DRIVER_ORIGINAL_DIR%"
echo [%date% %time%] END: exit code %BUILD_DRIVER_RESULT%
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b %BUILD_DRIVER_RESULT%

:build_main
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0..\.."
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify.ps1"
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }"
if errorlevel 1 exit /b 1
"%MSBUILD%" "%~dp0ArabianIdleSoundTest.csproj" /nologo /verbosity:minimal /p:Configuration=Debug
if errorlevel 1 exit /b 1
set "LOCAL_PLUGIN_DIR=%~dp0BepInEx\plugins\ArabianIdleSoundTest_Serp"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\ArabianIdleSoundTest_Serp"
copy /Y "%~dp0info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if errorlevel 1 exit /b 1
if not exist "%LOCAL_PLUGIN_DIR%\ArabianIdleSoundTest.dll" exit /b 1
if not exist "%GAME_PLUGIN_DIR%" mkdir "%GAME_PLUGIN_DIR%"
if errorlevel 1 exit /b 1
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 exit /b 1
echo Arabian Idle Sound Test built and installed; factor=0.25, Enabled=true by default.
exit /b 0
