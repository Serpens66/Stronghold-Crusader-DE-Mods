@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "PLUGIN_NAME=BugfixesAndQoL_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

rem Never touch build or installation output while the game has plugin DLLs loaded.
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build und Installation abgebrochen: Stronghold Crusader Definitive Edition ist noch gestartet.
  echo Lokales Paket und installierter Mod wurden nicht veraendert.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if not exist "%MSBUILD%" goto build_failed
rem Prefer the validated release package; the source bin folder may contain a stale Debug build.
if exist "%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%\SHCDESE.dll" (
  set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%"
) else if exist "%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%\SHCDESE.dll" (
  set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%"
) else if exist "%GAME_SCRIPT_EXTENDER_DIR%\SHCDESE.dll" (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
) else goto build_failed

pushd "%PROJECT_DIR%"
"%MSBUILD%" tests\ImprovedMoatFilling.Tests.csproj /p:Configuration=Debug
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%tests\bin\ImprovedMoatFilling.Tests.exe"
if errorlevel 1 goto build_failed_popd
dotnet run --project "tests\ExtendedShiftCommandQueue.Tests\ExtendedShiftCommandQueue.Tests.csproj"
if errorlevel 1 goto build_failed_popd
dotnet run --project "tests\AssassinPathfinding.Tests\AssassinPathfinding.Tests.csproj" -- "%PROJECT_DIR%.."
if errorlevel 1 goto build_failed_popd
dotnet run --project "tests\FriendlyMoatMovement.Tests\FriendlyMoatMovement.Tests.csproj" -- "%PROJECT_DIR%.."
if errorlevel 1 goto build_failed_popd
popd

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
pushd "%PROJECT_DIR%"
"%MSBUILD%" BugfixesAndQoL.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
popd

copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
xcopy "%PROJECT_DIR%Override" "%LOCAL_PLUGIN_DIR%\Override\" /E /I /Q /Y >nul
xcopy "%PROJECT_DIR%Patches" "%LOCAL_PLUGIN_DIR%\Patches\" /E /I /Q /Y >nul
if not exist "%LOCAL_PLUGIN_DIR%\BugfixesAndQoL.dll" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed

if exist "%GAME_PLUGIN_DIR%\" (
  rem Keep player-created lobby settings while replacing all packaged files.
  for /D %%D in ("%GAME_PLUGIN_DIR%\*") do (
    if /I not "%%~nxD"=="LobbyModSettings" (
      rmdir /S /Q "%%~fD"
      if errorlevel 1 goto copy_failed
    )
  )
  for %%F in ("%GAME_PLUGIN_DIR%\*") do (
    if exist "%%~fF" if not exist "%%~fF\" (
      del /F /Q "%%~fF"
      if errorlevel 1 goto copy_failed
    )
  )
)
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto copy_failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Release\Write-LocalBuildManifest.ps1" -ModName BugfixesAndQoL
if errorlevel 1 goto package_failed
echo Build und Installation von Bugfixes and QoL erfolgreich.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:build_failed_popd
popd
:build_failed
echo Build fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1
:package_failed
echo Paketpruefung fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1
:copy_failed
echo Installation fehlgeschlagen. Ist das Spiel noch gestartet?
if "%NO_PAUSE%"=="0" pause
exit /b 1
