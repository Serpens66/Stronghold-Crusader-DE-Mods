@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START StockpileAccessFixTest
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
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
rem The installed release is canonical; SHCDESE_EXTENDER_DIR is the explicit override.
if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
set "PLUGIN_NAME=StockpileAccessFixTest_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

rem Never replace plugin files while the game has loaded them.
echo [%date% %time%] Check that the game is closed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build and installation aborted: Stronghold Crusader Definitive Edition is still running.
  echo The local package and installed mod were not changed.
  exit /b 1
)

if not exist "%MSBUILD%" goto build_failed
if exist "%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%\SHCDESE.dll" (
  set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%"
) else if exist "%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%\SHCDESE.dll" (
  set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%"
) else if exist "%GAME_SCRIPT_EXTENDER_DIR%\SHCDESE.dll" (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
) else goto build_failed

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
pushd "%PROJECT_DIR%"
echo [%date% %time%] Compile projects
"%MSBUILD%" StockpileAccessFixTest.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
popd

echo [%date% %time%] Copy package files
copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if not exist "%LOCAL_PLUGIN_DIR%\StockpileAccessFixTest.dll" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed

if "%NO_INSTALL%"=="1" (
  echo Build successful. Installation skipped.
  exit /b 0
)

if exist "%GAME_PLUGIN_DIR%\" (
  for /D %%D in ("%GAME_PLUGIN_DIR%\*") do (
    rmdir /S /Q "%%~fD"
    if errorlevel 1 goto copy_failed
  )
  for %%F in ("%GAME_PLUGIN_DIR%\*") do (
    if exist "%%~fF" if not exist "%%~fF\" (
      del /F /Q "%%~fF"
      if errorlevel 1 goto copy_failed
    )
  )
)
echo [%date% %time%] Copy package files
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto copy_failed

echo Stockpile Access Fix Test built and installed successfully.
exit /b 0

:build_failed_popd
popd
:build_failed
echo Build failed.
exit /b 1

:package_failed
echo Package validation failed.
exit /b 1

:copy_failed
echo Installation failed. Is the game still running?
exit /b 1
