@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START BugfixesAndQoL
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
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-SharedBoundaries.ps1"
if errorlevel 1 exit /b 1
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\_inspect\FormationIntegration\Verify-Interop.ps1"
if errorlevel 1 exit /b 1
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-UnitCommandSplit.ps1"
if errorlevel 1 exit /b 1
echo [%date% %time%] Unit access regression tests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-UnitAccess.ps1"
if errorlevel 1 exit /b 1
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "API_SHARED_DIR=%~dp0..\APIShared\BepInEx\plugins\APIShared_Serp"
set "LOCAL_API_SHARED_DIR=%PROJECT_DIR%..\APIShared\BepInEx\plugins\APIShared_Serp"
rem The installed release is canonical; SHCDESE_EXTENDER_DIR is the explicit override.
if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
rem Release automation can explicitly use the validated workspace package.
if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0.." -PackageDirectory "%API_SHARED_DIR%"
if errorlevel 1 exit /b 1
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
set "PLUGIN_NAME=BugfixesAndQoL_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

rem Never touch build or installation output while the game has plugin DLLs loaded.
echo [%date% %time%] Check that the game is closed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build und Installation abgebrochen: Stronghold Crusader Definitive Edition ist noch gestartet.
  echo Lokales Paket und installierter Mod wurden nicht veraendert.
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
if not exist "%API_SHARED_DIR%\APIShared.dll" goto build_failed

pushd "%PROJECT_DIR%"
echo [%date% %time%] Gatehouse target-marker height contracts
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-GatehouseTargetMarkerHeight.ps1"
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-GatehouseLivingCapturePreflight.ps1" -RunTests
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1"
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-ModOptionsPerformance.ps1" -RunTests
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\_inspect\AssassinGateClimb\verify.ps1"
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-AIKeepRangePreflight.ps1" -GameDir "%GAME_DIR%" -ExtenderDir "%EXTENDER_DIR%" -ApiSharedDir "%API_SHARED_DIR%"
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-AiRaidApiContracts.ps1"
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-AiRaidRetargetPreflight.ps1"
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\AiRaidRetarget.Tests\Run.ps1"
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-NotificationLastFramePreflight.ps1"
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-SpectatorPerspectivePreflight.ps1"
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-DamagedHealthBarsPreflight.ps1"
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-WorkshopIdleDelayPreflight.ps1"
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-TannerFadePreflight.ps1"
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Validation\Test-PermanentNativeRuntimePatches.ps1"
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%..\_inspect\HostClientPresetTests\HostClientPresetTests.csproj" /p:Configuration=Debug
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%..\_inspect\HostClientPresetTests\bin\HostClientPresetTests.exe" market-goods-order
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%..\_inspect\HostClientPresetTests\bin\HostClientPresetTests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Run tests
dotnet test tests\Formations.Core.Tests\Formations.Core.Tests.csproj --configuration Release --logger trx --results-directory tests\Formations.Core.Tests\TestResults
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\Formations.Tests\Formations.Tests.csproj /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%tests\Formations.Tests\bin\Formations.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\WaterboyTargetReservation.Tests\WaterboyTargetReservation.Tests.csproj /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%tests\WaterboyTargetReservation.Tests\bin\WaterboyTargetReservation.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\WaterboyTargetReservation.Tests\Test-RedBirdVersions.ps1" -TestExecutable "%PROJECT_DIR%tests\WaterboyTargetReservation.Tests\bin\WaterboyTargetReservation.Tests.exe" -ExtenderDir "%EXTENDER_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\TannerFade.Tests\TannerFade.Tests.csproj /p:Configuration=Release
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%tests\TannerFade.Tests\bin\TannerFade.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\WorkshopIdleDelay.Tests\WorkshopIdleDelay.Tests.csproj /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%tests\WorkshopIdleDelay.Tests\bin\WorkshopIdleDelay.Tests.exe" "%GAME_DIR%\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\WorkerBreakPause.Tests\WorkerBreakPause.Tests.csproj /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%tests\WorkerBreakPause.Tests\bin\WorkerBreakPause.Tests.exe" "%GAME_DIR%\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%..\_inspect\BugfixesAndQoLNativeTests\BugfixesAndQoLNativeTests.csproj" /p:Configuration=Release
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%..\_inspect\BugfixesAndQoLNativeTests\bin\BugfixesAndQoLNativeTests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%..\_inspect\VanillaPeaceTimeNativeTests\VanillaPeaceTimeNativeTests.csproj" /p:Configuration=Release
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%..\_inspect\VanillaPeaceTimeNativeTests\bin\VanillaPeaceTimeNativeTests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\ImprovedMoatFilling.Tests.csproj /p:Configuration=Debug
if errorlevel 1 goto build_failed_popd
echo [%date% %time%] Run tests
"%PROJECT_DIR%tests\bin\ImprovedMoatFilling.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Run tests
dotnet run --project "tests\ExtendedShiftCommandQueue.Tests\ExtendedShiftCommandQueue.Tests.csproj"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Run tests
dotnet run --project "tests\AssassinPathfinding.Tests\AssassinPathfinding.Tests.csproj" -- "%PROJECT_DIR%.."
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
set "MOAT_TEST_API_SHARED_DLL=%API_SHARED_DIR%\APIShared.dll"
echo [%date% %time%] Run tests
dotnet run --project "tests\FriendlyMoatMovement.Tests\FriendlyMoatMovement.Tests.csproj" -- "%PROJECT_DIR%.."
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
popd

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
pushd "%PROJECT_DIR%"
echo [%date% %time%] Compile projects
"%MSBUILD%" BugfixesAndQoL.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%"
if errorlevel 1 goto build_failed_popd
popd

echo [%date% %time%] Copy package files
copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
echo [%date% %time%] Copy package files
xcopy "%PROJECT_DIR%Override" "%LOCAL_PLUGIN_DIR%\Override\" /E /I /Q /Y >nul
echo [%date% %time%] Copy package files
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
echo [%date% %time%] Copy package files
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto copy_failed
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName BugfixesAndQoL
if errorlevel 1 goto package_failed
echo Build und Installation von Bugfixes and QoL erfolgreich.
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
