@echo off
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "OUTPUT=%PROJECT_DIR%BepInEx\plugins\AivLobbyPresetTest_Serp"
set "INSTALL=%GAME_DIR%\BepInEx\plugins\AivLobbyPresetTest_Serp"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 goto failed
if not exist "%MSBUILD%" goto failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\Test-Preflight.ps1" -ProjectDir "%PROJECT_DIR%."
if errorlevel 1 goto failed
"%MSBUILD%" "%PROJECT_DIR%tests\AivLobbyPresetTest.Tests.csproj" /p:Configuration=Debug
if errorlevel 1 goto failed
"%PROJECT_DIR%tests\bin\AivLobbyPresetTest.Tests.exe" "%PROJECT_DIR%CraterLakePreset.json" "%PROJECT_DIR%AivLobbyTestSeries.json" "%PROJECT_DIR%AivLobbyFullMapProbeSeries.json" "%PROJECT_DIR%AivLobbyCrater180ProbeSeries.json" "%PROJECT_DIR%AivLobbySixMatchSeries.json"
if errorlevel 1 goto failed
"%MSBUILD%" "%PROJECT_DIR%AivLobbyPresetTest.csproj" /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto failed
if not exist "%OUTPUT%\AivLobbyPresetTest.dll" goto failed
if not exist "%INSTALL%" mkdir "%INSTALL%"
copy /Y "%OUTPUT%\AivLobbyPresetTest.dll" "%INSTALL%\AivLobbyPresetTest.dll" >nul
if errorlevel 1 goto failed
copy /Y "%OUTPUT%\CraterLakePreset.json" "%INSTALL%\CraterLakePreset.json" >nul
if errorlevel 1 goto failed
copy /Y "%OUTPUT%\AivLobbyTestSeries.json" "%INSTALL%\AivLobbyTestSeries.json" >nul
if errorlevel 1 goto failed
copy /Y "%OUTPUT%\info.json" "%INSTALL%\info.json" >nul
if errorlevel 1 goto failed
echo AIV Lobby Preset Test built and installed.
if "%NO_PAUSE%"=="0" pause
exit /b 0
:failed
echo AIV Lobby Preset Test build or installation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
