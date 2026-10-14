@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
set "VERIFY_ONLY=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/verifyonly" set "VERIFY_ONLY=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START CrenelatedWallTest
cd /d "%~dp0"
if errorlevel 1 exit /b 1
call :main
set "BUILD_DRIVER_RESULT=%ERRORLEVEL%"
cd /d "%BUILD_DRIVER_ORIGINAL_DIR%"
echo [%date% %time%] END CrenelatedWallTest: exit code %BUILD_DRIVER_RESULT%
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b %BUILD_DRIVER_RESULT%

:main
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify.ps1"
if errorlevel 1 exit /b 1
if "%VERIFY_ONLY%"=="1" exit /b 0
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }"
if errorlevel 1 goto :game_running
"%MSBUILD%" "%~dp0CrenelatedWallTest.csproj" /p:Configuration=Debug /nologo /verbosity:minimal
if errorlevel 1 exit /b 1
copy /Y "%~dp0info.json" "%~dp0BepInEx\plugins\CrenelatedWallTest_Serp\info.json" >nul
if errorlevel 1 exit /b 1
xcopy "%~dp0BepInEx\plugins\CrenelatedWallTest_Serp" "%GAME_DIR%\BepInEx\plugins\CrenelatedWallTest_Serp\" /E /I /Q /Y
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify-installed.ps1"
if errorlevel 1 exit /b 1
echo CrenelatedWallTest built, installed and hash-verified; APIShared unchanged.
exit /b 0

:game_running
echo ERROR: Close Stronghold Crusader Definitive Edition before building/installing CrenelatedWallTest.
exit /b 1
