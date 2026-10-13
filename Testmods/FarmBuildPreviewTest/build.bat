@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START FarmBuildPreviewTest
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
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
if defined SHCDESE_EXTENDER_DIR set "EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify.ps1" -ExtenderDir "%EXTENDER_DIR%"
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\Validation\Test-SharedBoundaries.ps1" -ModDirectory "Testmods\FarmBuildPreviewTest"
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }"
if errorlevel 1 exit /b 1
if not exist "%MSBUILD%" exit /b 1
echo [%date% %time%] Compile and run isolated regressions
"%MSBUILD%" "%~dp0tests\FarmBuildPreviewTest.Tests.csproj" /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%" /nologo /verbosity:minimal
if errorlevel 1 exit /b 1
"%~dp0tests\bin\FarmBuildPreviewTest.Tests.exe" "%GAME_DIR%\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"
if errorlevel 1 exit /b 1
echo [%date% %time%] Compile runtime
"%MSBUILD%" "%~dp0FarmBuildPreviewTest.csproj" /p:Configuration=Debug /p:ExtenderDir="%EXTENDER_DIR%" /nologo /verbosity:minimal
if errorlevel 1 exit /b 1
echo [%date% %time%] Verify and install package
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" -GameDir "%GAME_DIR%"
if errorlevel 1 exit /b 1
echo Farm Build Preview Test built, checked and installed.
exit /b 0
