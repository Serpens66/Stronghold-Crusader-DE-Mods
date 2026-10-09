@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START MainViewModelInitProbe
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
set "API_SHARED_DIR=%~dp0..\..\APIShared\BepInEx\plugins\APIShared_Serp"
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0..\.." -PackageDirectory "%API_SHARED_DIR%"
if errorlevel 1 exit /b 1
set "PLUGIN_NAME=MainViewModelInitProbe_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

echo [%date% %time%] Check that the game is closed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build and installation aborted: Stronghold Crusader Definitive Edition is still running.
  exit /b 1
)

if not exist "%MSBUILD%" goto build_failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto extender_missing
if not exist "%API_SHARED_DIR%\APIShared.dll" goto api_missing

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
pushd "%PROJECT_DIR%"
echo [%date% %time%] Compile projects
"%MSBUILD%" MainViewModelInitProbe.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%"
if errorlevel 1 goto build_failed_popd
popd

echo [%date% %time%] Copy package files
copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if not exist "%LOCAL_PLUGIN_DIR%\MainViewModelInitProbe.dll" goto package_failed
if exist "%LOCAL_PLUGIN_DIR%\APIShared.dll" goto package_failed
if exist "%LOCAL_PLUGIN_DIR%\SHCDESE.dll" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed

if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"
echo [%date% %time%] Copy package files
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto copy_failed
echo MainViewModel Init Probe built and installed successfully.
exit /b 0

:build_failed_popd
popd
:build_failed
echo Build failed.
exit /b 1
:extender_missing
echo Installed SHCDE-SE 2.3.0 was not found.
exit /b 1
:api_missing
echo APIShared.dll was not found.
exit /b 1
:package_failed
echo Package validation failed.
exit /b 1
:copy_failed
echo Installation failed. Is the game still running?
exit /b 1
