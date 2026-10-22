@echo off
setlocal EnableExtensions
rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
if defined SHCDE_BRANCH_PINNED goto :shcde_branch_selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0../Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0../." -Driver "SerpsModsHost/build.bat" %*
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
echo [%date% %time%] START SerpsModsHost
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
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-SharedBoundaries.ps1" -ModDirectory SerpsModsHost -ConsumerOnly
if not "%ERRORLEVEL%"=="0" exit /b 1
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "API_SHARED_DIR=%~dp0..\APIShared\BepInEx\plugins\APIShared_Serp"
if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
echo [%date% %time%] PowerShell checks / build step
rem APIShared source: /apishared:main (release branch) or /apishared:dev (local checkout, default).
call "%~dp0..\Shared\Tools\ApiSharedRepository\Select-ApiSharedBranch.cmd" %*
if not "%ERRORLEVEL%"=="0" exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0.." -PackageDirectory "%API_SHARED_DIR%"
if not "%ERRORLEVEL%"=="0" exit /b 1
rem The installed release is canonical; SHCDESE_EXTENDER_DIR is the explicit override.
if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
set "PLUGIN_NAME=SerpsMods_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"

rem Never touch build or installation output while the game has plugin DLLs loaded.
echo [%date% %time%] Check that the game is closed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if not "%ERRORLEVEL%"=="0" (
  echo Build und Installation abgebrochen: Stronghold Crusader Definitive Edition ist noch gestartet.
  echo Lokales Paket und installierter Mod wurden nicht veraendert.
  exit /b 1
)

if not exist "%MSBUILD%" goto build_failed
if not exist "%API_SHARED_DIR%\APIShared.dll" (
  echo APIShared.dll wurde nicht gefunden: !API_SHARED_DIR!\APIShared.dll
  goto build_failed
)
if exist "%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%\SHCDESE.dll" (
  set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%"
) else if exist "%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%\SHCDESE.dll" (
  set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%"
) else if exist "%GAME_SCRIPT_EXTENDER_DIR%\SHCDESE.dll" (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
) else goto build_failed

echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tools\Test-HostInstallation.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
pushd "%PROJECT_DIR%"
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%..\_inspect\SerpsModsHostDuplicateTests\SerpsModsHostDuplicateTests.csproj" /p:Configuration=Release
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%..\_inspect\SerpsModsHostDuplicateTests\bin\SerpsModsHostDuplicateTests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%..\_inspect\ModSettingsSearchTests\ModSettingsSearchTests.csproj" /p:Configuration=Release
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%..\_inspect\ModSettingsSearchTests\bin\ModSettingsSearchTests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%MSBUILD%" SerpsModsHost.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%..\_inspect\SerpsModsHostDuplicateTests\bin\SerpsModsHostDuplicateTests.exe" "%LOCAL_PLUGIN_DIR%\SerpsModsHost.dll" "%GAME_DIR%" "%PROJECT_DIR%info.json" "%EXTENDER_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
popd

echo [%date% %time%] Copy package files
copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
echo [%date% %time%] Copy package files
copy /Y "%PROJECT_DIR%serps-modpack.json" "%LOCAL_PLUGIN_DIR%\serps-modpack.json" >nul
echo [%date% %time%] Copy package files
xcopy "%PROJECT_DIR%Override" "%LOCAL_PLUGIN_DIR%\Override\" /E /I /Q /Y >nul
echo [%date% %time%] Copy package files
xcopy "%PROJECT_DIR%Locales" "%LOCAL_PLUGIN_DIR%\Locales\" /E /I /Q /Y >nul
if not exist "%LOCAL_PLUGIN_DIR%\SerpsModsHost.dll" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\serps-modpack.json" goto package_failed

if "%NO_INSTALL%"=="1" (
  echo Serps Mods Host vorbereitet; Installation uebersprungen.
  exit /b 0
)
echo [%date% %time%] Copy package files
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tools\Install-HostPackage.ps1" -PackageDirectory "%LOCAL_PLUGIN_DIR%" -PluginRoot "%GAME_DIR%\BepInEx\plugins" -Destination "%GAME_PLUGIN_DIR%"
if not "%ERRORLEVEL%"=="0" goto copy_failed
echo Build und Installation von Serps Mods Host erfolgreich.
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
