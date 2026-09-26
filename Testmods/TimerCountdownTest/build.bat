@echo off
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "OUTPUT=%PROJECT_DIR%BepInEx\plugins\TimerCountdownTest_Serp"
set "INSTALL=%GAME_DIR%\BepInEx\plugins\TimerCountdownTest_Serp"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 goto failed
if not exist "%MSBUILD%" goto failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\Test-Preflight.ps1" -ProjectDir "%PROJECT_DIR%."
if errorlevel 1 goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\..\Shared\Test-PermanentNativeRuntimePatches.ps1"
if errorlevel 1 goto failed
"%MSBUILD%" "%PROJECT_DIR%tests\TimerCountdownTest.Tests.csproj" /p:Configuration=Debug
if errorlevel 1 goto failed
"%PROJECT_DIR%tests\bin\TimerCountdownTest.Tests.exe"
if errorlevel 1 goto failed
"%MSBUILD%" "%PROJECT_DIR%TimerCountdownTest.csproj" /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto failed
if not exist "%OUTPUT%\TimerCountdownTest.dll" goto failed
if not exist "%INSTALL%" mkdir "%INSTALL%"
copy /Y "%OUTPUT%\TimerCountdownTest.dll" "%INSTALL%\TimerCountdownTest.dll" >nul
if errorlevel 1 goto failed
copy /Y "%OUTPUT%\info.json" "%INSTALL%\info.json" >nul
if errorlevel 1 goto failed
xcopy /E /I /Y "%OUTPUT%\Patches" "%INSTALL%\Patches" >nul
if errorlevel 1 goto failed
echo Timer Countdown Test built and installed.
if "%NO_PAUSE%"=="0" pause
exit /b 0
:failed
echo Timer Countdown Test build or installation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
