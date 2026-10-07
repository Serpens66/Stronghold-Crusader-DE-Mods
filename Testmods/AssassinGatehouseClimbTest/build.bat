@echo off
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "API_DIR=%PROJECT_DIR%..\..\APIShared\BepInEx\plugins\APIShared_Serp"
set "PLUGIN=AssassinGatehouseClimbTest_Serp"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 }"
if errorlevel 1 goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\..\_inspect\AssassinGateClimb\verify.ps1" -CheckTestApi
if errorlevel 1 goto failed
"%MSBUILD%" "%PROJECT_DIR%..\..\_inspect\AssassinGateClimb\tests\AssassinSharedTests.csproj" /p:Configuration=Release /verbosity:minimal
if errorlevel 1 goto failed
"%PROJECT_DIR%..\..\_inspect\AssassinGateClimb\tests\bin\AssassinSharedTests.exe"
if errorlevel 1 goto failed
"%MSBUILD%" "%PROJECT_DIR%AssassinGatehouseClimbTest.csproj" /p:Configuration=Debug /p:ApiSharedDir="%API_DIR%"
if errorlevel 1 goto failed
copy /Y "%PROJECT_DIR%info.json" "%PROJECT_DIR%BepInEx\plugins\%PLUGIN%\info.json" >nul
if errorlevel 1 goto failed
xcopy "%PROJECT_DIR%BepInEx\plugins\%PLUGIN%" "%GAME_DIR%\BepInEx\plugins\%PLUGIN%\" /E /I /Q /Y >nul
if errorlevel 1 goto failed
echo Assassin gatehouse test built and installed successfully.
if "%NO_PAUSE%"=="0" pause
exit /b 0
:failed
echo Build or installation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
