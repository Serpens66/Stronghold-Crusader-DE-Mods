@echo off
setlocal EnableExtensions
rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
if defined SHCDE_BRANCH_PINNED goto :shcde_branch_selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0../Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0../." -Driver "ExtraFeatures/build.bat" %*
set "SHCDE_BRANCH_RESULT=%ERRORLEVEL%"
set "SHCDE_BRANCH_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "SHCDE_BRANCH_PAUSE=0"
if "%SHCDE_BRANCH_PAUSE%"=="1" pause
exit /b %SHCDE_BRANCH_RESULT%
:shcde_branch_selected
endlocal
@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START ExtraFeatures
cd /d "%~dp0"
if not "%ERRORLEVEL%"=="0" goto :build_driver_directory_failed
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
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-SharedBoundaries.ps1" -ModDirectory "ExtraFeatures"
if not "%ERRORLEVEL%"=="0" exit /b 1
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "NO_INSTALL=0"
if defined SHCDE_PREPARE_ONLY set "NO_INSTALL=%SHCDE_PREPARE_ONLY%"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "API_SHARED_DIR=%~dp0..\APIShared\BepInEx\plugins\APIShared_Serp"
rem The installed release is canonical; SHCDESE_EXTENDER_DIR is the explicit override.
if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
rem Release automation can explicitly use the validated workspace package.
if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
echo [%date% %time%] PowerShell checks / build step
rem APIShared source: /apishared:main (release branch) or /apishared:dev (local checkout, default).
call "%~dp0..\Shared\Tools\ApiSharedRepository\Select-ApiSharedBranch.cmd" %*
if not "%ERRORLEVEL%"=="0" exit /b 1
rem Package proof is checked at SDK selection and again at the MSBuild reference boundary.
set "PLUGIN_NAME=ExtraFeatures_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

rem Never touch build or installation output while the game has plugin DLLs loaded.
echo [%date% %time%] Check that the game is closed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if not "%ERRORLEVEL%"=="0" (
  echo Build und Installation abgebrochen: Stronghold Crusader Definitive Edition ist noch gestartet.
  echo Lokales Paket und installierter Mod wurden nicht veraendert.
  exit /b 1
)

echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%verify-repair.ps1" -GameDir "%GAME_DIR%" -ExtenderDir "%GAME_SCRIPT_EXTENDER_DIR%" -SkipSharedPreflight
if not "%ERRORLEVEL%"=="0" goto build_failed

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%verify-popularity.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed

if not exist "%MSBUILD%" goto build_failed
if exist "%GAME_SCRIPT_EXTENDER_DIR%\SHCDESE.dll" (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
) else goto build_failed
if not exist "%API_SHARED_DIR%\APIShared.dll" goto build_failed

echo [%date% %time%] Cost ownership regression tests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared.Tests\CostOverrides\Run-Tests.ps1" -GameDir "%GAME_DIR%" -MSBuild "%MSBUILD%" -ExtenderDir "%GAME_SCRIPT_EXTENDER_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed

pushd "%PROJECT_DIR%"
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%..\_inspect\ExtraFeaturesNativeTests\ExtraFeaturesNativeTests.csproj" /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%GAME_SCRIPT_EXTENDER_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%..\_inspect\ExtraFeaturesNativeTests\bin\ExtraFeaturesNativeTests.exe" "%GAME_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
popd

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
pushd "%PROJECT_DIR%"
echo [%date% %time%] Compile projects
"%MSBUILD%" ExtraFeatures.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
popd

echo [%date% %time%] Compiled classification and dependency contracts
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-CompiledContracts.ps1" -AssemblyPath "%LOCAL_PLUGIN_DIR%\ExtraFeatures.dll" -GameDir "%GAME_DIR%" -ExtenderDir "%EXTENDER_DIR%" -ApiSharedDir "%API_SHARED_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed

echo [%date% %time%] Validate source and package hashes
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tools/Verify-Package.ps1"
if not "%ERRORLEVEL%"=="0" goto package_failed

if "%NO_INSTALL%"=="1" goto prepared_no_install
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tools/Verify-Package.ps1" -Install -InstalledDirectory "%GAME_PLUGIN_DIR%"
if not "%ERRORLEVEL%"=="0" goto copy_failed
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName ExtraFeatures
if not "%ERRORLEVEL%"=="0" goto package_failed

echo Build und Installation von Extra Features erfolgreich.
exit /b 0

:build_failed_popd
popd
:build_failed
echo Build fehlgeschlagen.
exit /b 1
:package_failed
echo Paketpruefung fehlgeschlagen.
exit /b 1
:copy_failed
echo Installation fehlgeschlagen. Ist das Spiel noch gestartet?
exit /b 1

:prepared_no_install
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName ExtraFeatures
if not "%ERRORLEVEL%"=="0" exit /b 1
echo Paket vorbereitet; Installation uebersprungen.
exit /b 0
