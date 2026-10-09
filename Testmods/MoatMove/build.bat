@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START MoatMove
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
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\Validation\Test-UnitCommandSplit.ps1"
if errorlevel 1 exit /b 1
echo [%date% %time%] Unit access regression tests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\Validation\Test-UnitAccess.ps1"
if errorlevel 1 exit /b 1
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
if defined SHCDESE_EXTENDER_DIR set "EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "NO_PAUSE=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
echo [%date% %time%] Check that the game is closed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 }"
if errorlevel 1 (
  echo Game is running. Build and installation aborted.
  goto failed
)
if not exist "%MSBUILD%" goto failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto failed
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\Test-Preflight.ps1"
if errorlevel 1 goto failed
echo [%date% %time%] Run tests
dotnet run --project "%PROJECT_DIR%tests\MoatMove.Tests.csproj" -- "%PROJECT_DIR%..\.."
if not "%ERRORLEVEL%"=="0" goto failed
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%MoatMove.csproj" /t:Build /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /nologo /v:minimal
if errorlevel 1 goto failed
if "%NO_INSTALL%"=="1" goto built_without_install
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\Install-Package.ps1" -GameDir "%GAME_DIR%"
if errorlevel 1 goto failed
echo MoatMove built and installed successfully.
exit /b 0
:built_without_install
echo MoatMove built successfully. Installation skipped.
exit /b 0
:failed
echo MoatMove build or installation failed.
exit /b 1
