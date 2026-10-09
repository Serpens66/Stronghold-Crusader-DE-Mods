@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START VirtualUnitsPrototype
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
echo [%date% %time%] Unit access regression tests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\Validation\Test-UnitAccess.ps1"
if errorlevel 1 exit /b 1
setlocal EnableExtensions EnableDelayedExpansion
set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
if defined SHCDESE_EXTENDER_DIR set "EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "NO_PAUSE=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

echo [%date% %time%] Check that the game is closed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build und Installation abgebrochen: Das Spiel ist noch gestartet.
  exit /b 1
)
if not exist "%MSBUILD%" ( echo MSBuild wurde nicht gefunden.& goto failed )
if not exist "%EXTENDER_DIR%\SHCDESE.dll" ( echo SHCDESE.dll wurde nicht gefunden: %EXTENDER_DIR%& goto failed )

pushd "%PROJECT_DIR%"
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\VirtualUnitsPrototype.Tests.csproj /p:Configuration=Release
if errorlevel 1 ( popd& goto failed )
echo [%date% %time%] Run tests
"%PROJECT_DIR%tests\bin\VirtualUnitsPrototype.Tests.exe"
if not "%ERRORLEVEL%"=="0" ( popd& goto failed )
if exist "%PROJECT_DIR%BepInEx\plugins\VirtualUnitsPrototype_Serp\" rmdir /S /Q "%PROJECT_DIR%BepInEx\plugins\VirtualUnitsPrototype_Serp"
echo [%date% %time%] Compile projects
"%MSBUILD%" VirtualUnitsPrototype.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd
if not "%BUILD_EXIT_CODE%"=="0" goto failed

set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\VirtualUnitsPrototype_Serp"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\VirtualUnitsPrototype_Serp"
if not exist "%LOCAL_PLUGIN_DIR%\VirtualUnitsPrototype.dll" goto failed
if "%NO_INSTALL%"=="1" (
  echo Build and tests successful. Installation skipped.
  exit /b 0
)
if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"
echo [%date% %time%] Copy package files
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y
if errorlevel 1 goto failed
echo Build, Tests und Installation erfolgreich.
exit /b 0

:failed
echo Build, Test oder Installation fehlgeschlagen.
exit /b 1
