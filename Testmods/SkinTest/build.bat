@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START SkinTest
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
set "API_SHARED_DIR=%~dp0..\..\APIShared\BepInEx\plugins\APIShared_Serp"
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0..\.." -PackageDirectory "%API_SHARED_DIR%"
if errorlevel 1 exit /b 1
if defined SHCDESE_EXTENDER_DIR set "EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "NO_PAUSE=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"

echo [%date% %time%] Check that the game is closed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build und Installation abgebrochen: Das Spiel ist noch gestartet.
  exit /b 1
)
if not exist "%MSBUILD%" ( echo MSBuild wurde nicht gefunden.& goto failed )
if not exist "%EXTENDER_DIR%\SHCDESE.dll" ( echo SHCDESE.dll wurde nicht gefunden: %EXTENDER_DIR%& goto failed )
if not exist "%API_SHARED_DIR%\APIShared.dll" ( echo APIShared.dll 0.3.6 wurde nicht gefunden: %API_SHARED_DIR%& goto failed )
if not exist "%PROJECT_DIR%Assets\CrusaderSwordsman\atlas.png" ( echo Private Atlas-Assets fehlen.& goto failed )
if not exist "%PROJECT_DIR%Assets\CrusaderRoundTower\atlas.png" ( echo Private Rundturm-Assets fehlen.& goto failed )
if not exist "%PROJECT_DIR%Assets\CrusaderRoundTowerAnimations\atlas.png" ( echo Private Rundturm-Animationsassets fehlen.& goto failed )
if not exist "%PROJECT_DIR%Assets\CrusaderUI\UIBuildingsO011_colour1.png" ( echo Private HUD-Assets fehlen.& goto failed )
if not exist "%PROJECT_DIR%Assets\CrusaderUI\UIButtonsO018_colour8.png" ( echo Private Kasernen-HUD-Assets fehlen.& goto failed )

pushd "%PROJECT_DIR%"
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\SkinTest.Tests.csproj /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 ( popd& goto failed )
echo [%date% %time%] Run tests
"%PROJECT_DIR%tests\bin\SkinTest.Tests.exe"
if not "%ERRORLEVEL%"=="0" ( popd& goto failed )
echo [%date% %time%] Compile projects
"%MSBUILD%" SkinTest.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
if "%BUILD_EXIT_CODE%"=="0" "%PROJECT_DIR%tests\bin\SkinTest.Tests.exe" --runtime-assembly "%PROJECT_DIR%BepInEx\plugins\SkinTest_Serp\SkinTest.dll"
if not "%ERRORLEVEL%"=="0" set "BUILD_EXIT_CODE=1"
popd
if not "%BUILD_EXIT_CODE%"=="0" goto failed

set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\SkinTest_Serp"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\SkinTest_Serp"
if not exist "%LOCAL_PLUGIN_DIR%\SkinTest.dll" goto failed
if "%NO_INSTALL%"=="1" goto built_without_install
echo [%date% %time%] Copy package files
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y
if errorlevel 1 goto failed
echo Build, Tests und Installation erfolgreich.
exit /b 0

:built_without_install
echo Build und Tests erfolgreich. Installation uebersprungen.
exit /b 0

:failed
echo Build, Test oder Installation fehlgeschlagen.
exit /b 1
