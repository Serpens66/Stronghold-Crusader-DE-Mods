@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START AivLobbyPresetTest
cd /d "%~dp0"
if errorlevel 1 goto :build_driver_directory_failed
rem The outer driver owns the pause, including failures before compilation.
call :build_driver_main %* /nopause
set "BUILD_DRIVER_RESULT=%ERRORLEVEL%"
cd /d "%BUILD_DRIVER_ORIGINAL_DIR%"
echo [%date% %time%] END: exit code %BUILD_DRIVER_RESULT%
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b %BUILD_DRIVER_RESULT%

:build_driver_directory_failed
echo ERROR: Cannot enter the build directory "%~dp0".
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b 1

:build_driver_main
echo [%date% %time%] Workspace source and runtime preflight
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\Validation\Test-SharedBoundaries.ps1"
if errorlevel 1 exit /b 1
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "OUTPUT=%PROJECT_DIR%BepInEx\plugins\AivLobbyPresetTest_Serp"
set "INSTALL=%GAME_DIR%\BepInEx\plugins\AivLobbyPresetTest_Serp"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
echo [%date% %time%] Check that the game is closed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 goto failed
if not exist "%MSBUILD%" goto failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto failed
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\Test-Preflight.ps1" -ProjectDir "%PROJECT_DIR%."
if errorlevel 1 goto failed
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%tests\AivLobbyPresetTest.Tests.csproj" /p:Configuration=Debug
if errorlevel 1 goto failed
echo [%date% %time%] Run tests
"%PROJECT_DIR%tests\bin\AivLobbyPresetTest.Tests.exe" "%PROJECT_DIR%CraterLakePreset.json" "%PROJECT_DIR%AivLobbyTestSeries.json" "%PROJECT_DIR%AivLobbyFullMapProbeSeries.json" "%PROJECT_DIR%AivLobbyCrater180ProbeSeries.json" "%PROJECT_DIR%AivLobbySixMatchSeries.json" "%PROJECT_DIR%AivLobbyMultiAivSeries.json" "%PROJECT_DIR%AivLobbyConnectedRecordProbeSeries.json" "%PROJECT_DIR%AivLobbyStartRebuildRegressionSeries.json"
if errorlevel 1 goto failed
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%AivLobbyPresetTest.csproj" /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto failed
if not exist "%OUTPUT%\AivLobbyPresetTest.dll" goto failed
if not exist "%INSTALL%" mkdir "%INSTALL%"
echo [%date% %time%] Copy package files
copy /Y "%OUTPUT%\AivLobbyPresetTest.dll" "%INSTALL%\AivLobbyPresetTest.dll" >nul
if errorlevel 1 goto failed
echo [%date% %time%] Copy package files
copy /Y "%OUTPUT%\CraterLakePreset.json" "%INSTALL%\CraterLakePreset.json" >nul
if errorlevel 1 goto failed
echo [%date% %time%] Copy package files
copy /Y "%OUTPUT%\AivLobbyTestSeries.json" "%INSTALL%\AivLobbyTestSeries.json" >nul
if errorlevel 1 goto failed
echo [%date% %time%] Copy package files
copy /Y "%OUTPUT%\info.json" "%INSTALL%\info.json" >nul
if errorlevel 1 goto failed
echo AIV Lobby Preset Test built and installed.
exit /b 0
:failed
echo AIV Lobby Preset Test build or installation failed.
exit /b 1
