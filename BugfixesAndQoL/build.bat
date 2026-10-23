@echo off
setlocal EnableExtensions
rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
if defined SHCDE_BRANCH_PINNED goto :shcde_branch_selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0../Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0../." -Driver "BugfixesAndQoL/build.bat" %*
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
echo [%date% %time%] START BugfixesAndQoL
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
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-SharedBoundaries.ps1" -ModDirectory BugfixesAndQoL
if not "%ERRORLEVEL%"=="0" exit /b 1
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\_inspect\FormationIntegration\Verify-Interop.ps1"
if not "%ERRORLEVEL%"=="0" exit /b 1
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-UnitCommandSplit.ps1" -ModDirectory BugfixesAndQoL -SkipPermanentHooks
if not "%ERRORLEVEL%"=="0" exit /b 1
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "NO_INSTALL=0"
if defined SHCDE_PREPARE_ONLY set "NO_INSTALL=%SHCDE_PREPARE_ONLY%"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
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
rem APIShared source: /apishared:main (release branch) or /apishared:dev (local checkout, default).
call "%~dp0..\Shared\Tools\ApiSharedRepository\Select-ApiSharedBranch.cmd" %*
if not "%ERRORLEVEL%"=="0" exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0.." -PackageDirectory "%API_SHARED_DIR%"
if not "%ERRORLEVEL%"=="0" exit /b 1
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
if not "%ERRORLEVEL%"=="0" (
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

rem /checks:notifications selects the relevant behavior suite for a notification-only change.
rem Common source, dependency and runtime safety checks remain mandatory.
set "NOTIFICATION_CHECKS=0"
set "GATEHOUSE_CHECKS=0"
set "FOREIGN_HUD_CHECKS=0"
set "SETTINGS_CHECKS=0"
for %%A in (%*) do if /I "%%~A"=="/checks:notifications" set "NOTIFICATION_CHECKS=1"
for %%A in (%*) do if /I "%%~A"=="/checks:gatehouse" set "GATEHOUSE_CHECKS=1"
for %%A in (%*) do if /I "%%~A"=="/checks:foreignhud" set "FOREIGN_HUD_CHECKS=1"
for %%A in (%*) do if /I "%%~A"=="/checks:settings" set "SETTINGS_CHECKS=1"
set /a CHECK_SCOPES=NOTIFICATION_CHECKS+GATEHOUSE_CHECKS+FOREIGN_HUD_CHECKS+SETTINGS_CHECKS
if %CHECK_SCOPES% GTR 1 goto build_failed
pushd "%PROJECT_DIR%"
if "%SETTINGS_CHECKS%"=="1" goto settings_checks
if "%FOREIGN_HUD_CHECKS%"=="1" goto foreign_hud_checks
if "%GATEHOUSE_CHECKS%"=="1" goto gatehouse_checks
if "%NOTIFICATION_CHECKS%"=="1" goto notification_checks
echo [%date% %time%] Lobby save loading and visibility contracts
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-LobbySaveLoading.ps1" -RunTests
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Integrated gate routing migration regression
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-GateRouting.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Gatehouse target-marker height contracts
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-GatehouseTargetMarkerHeight.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-DrawbridgeDestructionFix.ps1" -RunTests
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-GatehouseLivingCapturePreflight.ps1" -RunTests
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-BuildPreviewPreflight.ps1" -ExtenderDir "%EXTENDER_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%MSBUILD%" "%PROJECT_DIR%tests\BuildPreview.Tests\BuildPreview.Tests.csproj" /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%" /nologo /verbosity:minimal
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%tests\BuildPreview.Tests\bin\BuildPreview.Tests.exe" "%GAME_DIR%\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-ModOptionsPerformance.ps1" -RunTests
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\_inspect\AssassinGateClimb\verify.ps1" -SkipPermanentHooks
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-AIKeepRangePreflight.ps1" -GameDir "%GAME_DIR%" -ExtenderDir "%EXTENDER_DIR%" -ApiSharedDir "%API_SHARED_DIR%" -SkipPermanentHooks
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-AiRaidApiContracts.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-AiRaidRetargetPreflight.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\AiRaidRetarget.Tests\Run.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-NotificationLastFramePreflight.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-SpectatorPerspectivePreflight.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-DamagedHealthBarsPreflight.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-WorkshopIdleDelayPreflight.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-TannerFadePreflight.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
echo [%date% %time%] Arabian idle volume consumer tests
"%MSBUILD%" "%PROJECT_DIR%tests\ArabianIdleSound.Tests\ArabianIdleSound.Tests.csproj" /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%tests\ArabianIdleSound.Tests\bin\ArabianIdleSoundVolume.Tests.exe" "%GAME_DIR%" "%PROJECT_DIR%.."
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%..\_inspect\HostClientPresetTests\HostClientPresetTests.csproj" /p:Configuration=Debug
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%..\_inspect\HostClientPresetTests\bin\HostClientPresetTests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Run tests
dotnet test tests\Formations.Core.Tests\Formations.Core.Tests.csproj --configuration Release --logger trx --results-directory tests\Formations.Core.Tests\TestResults
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Formation speech regression tests
dotnet run --project tests\FormationSpeech.Tests\FormationSpeech.Tests.csproj --configuration Release -p:ExtenderDir="%EXTENDER_DIR%" -- "%PROJECT_DIR%.."
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\Formations.Tests\Formations.Tests.csproj /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%tests\Formations.Tests\bin\Formations.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\WaterboyTargetReservation.Tests\WaterboyTargetReservation.Tests.csproj /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%tests\WaterboyTargetReservation.Tests\bin\WaterboyTargetReservation.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\WaterboyTargetReservation.Tests\Test-RedBirdVersions.ps1" -TestExecutable "%PROJECT_DIR%tests\WaterboyTargetReservation.Tests\bin\WaterboyTargetReservation.Tests.exe" -ExtenderDir "%EXTENDER_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\TannerFade.Tests\TannerFade.Tests.csproj /p:Configuration=Release
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%tests\TannerFade.Tests\bin\TannerFade.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\WorkshopIdleDelay.Tests\WorkshopIdleDelay.Tests.csproj /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%tests\WorkshopIdleDelay.Tests\bin\WorkshopIdleDelay.Tests.exe" "%GAME_DIR%\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\WorkerBreakPause.Tests\WorkerBreakPause.Tests.csproj /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%tests\WorkerBreakPause.Tests\bin\WorkerBreakPause.Tests.exe" "%GAME_DIR%\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%..\_inspect\BugfixesAndQoLNativeTests\BugfixesAndQoLNativeTests.csproj" /p:Configuration=Release
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%..\_inspect\BugfixesAndQoLNativeTests\bin\BugfixesAndQoLNativeTests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%..\_inspect\VanillaPeaceTimeNativeTests\VanillaPeaceTimeNativeTests.csproj" /p:Configuration=Release
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%..\_inspect\VanillaPeaceTimeNativeTests\bin\VanillaPeaceTimeNativeTests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
"%MSBUILD%" tests\ImprovedMoatFilling.Tests.csproj /p:Configuration=Debug
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Run tests
"%PROJECT_DIR%tests\bin\ImprovedMoatFilling.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Run tests
dotnet run --project "tests\ExtendedShiftCommandQueue.Tests\ExtendedShiftCommandQueue.Tests.csproj"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Issue 6 infinite-patrol regressions
dotnet run --project "tests\Issue6.Tests\Issue6.Tests.csproj" -- "%PROJECT_DIR%.."
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Run tests
dotnet run --project "tests\AssassinPathfinding.Tests\AssassinPathfinding.Tests.csproj" -- "%PROJECT_DIR%.."
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Captured-gate protection contracts
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\_inspect\AssassinAttackControlAudit\verify-integrated.ps1" -SkipPermanentHooks
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Captured-gate protection tests
"%MSBUILD%" "tests\AssassinCapturedGateProtection.Tests\AssassinCapturedGateProtection.Tests.csproj" /p:Configuration=Release
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%tests\AssassinCapturedGateProtection.Tests\bin\AssassinCapturedGateProtectionTests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
set "MOAT_TEST_API_SHARED_DLL=%API_SHARED_DIR%\APIShared.dll"
echo [%date% %time%] Run tests
dotnet run --project "tests\FriendlyMoatMovement.Tests\FriendlyMoatMovement.Tests.csproj" -- "%PROJECT_DIR%.."
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
goto checks_completed

:notification_checks
echo [%date% %time%] Notification-scoped runtime and behavior checks
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-NotificationLastFramePreflight.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%MSBUILD%" tests\ImprovedMoatFilling.Tests.csproj /p:Configuration=Debug /p:ExtenderDir="%EXTENDER_DIR%" /nologo /verbosity:minimal
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%tests\bin\ImprovedMoatFilling.Tests.exe" --notifications
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
goto checks_completed

:settings_checks
echo [%date% %time%] Settings-scoped runtime preflight; production UI/catalog tests run after compilation
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%MSBUILD%" tests\ImprovedMoatFilling.Tests.csproj /p:Configuration=Debug /p:ExtenderDir="%EXTENDER_DIR%" /nologo /verbosity:minimal
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%tests\bin\ImprovedMoatFilling.Tests.exe" --settings-ui
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
goto foreign_hud_checks_completed

:gatehouse_checks
echo [%date% %time%] Gatehouse geometry, live topology and diagnostic behavior checks
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
pwsh.exe -NoProfile -File "%PROJECT_DIR%tests\GatehouseDiagnostics\Test-GatehouseDiagnostics.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
pwsh.exe -NoProfile -File "%PROJECT_DIR%tests\GatehouseDiagnostics\Test-GatehouseDiagnostics.ps1" -Diagnostic
if not "%ERRORLEVEL%"=="0" goto build_failed_popd

goto checks_completed

:foreign_hud_checks
echo [%date% %time%] Foreign troop HUD runtime, skin and player-color checks
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-ModOptionsPerformance.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
goto foreign_hud_behavior_checks

:checks_completed
rem Keep foreign HUD regressions in the ordinary suite as well as the scoped build.
if "%NOTIFICATION_CHECKS%%GATEHOUSE_CHECKS%"=="00" goto foreign_hud_behavior_checks
goto foreign_hud_checks_completed

:foreign_hud_behavior_checks
pwsh.exe -NoProfile -File "%PROJECT_DIR%tests\ForeignTroopHud\Test-PlayerColors.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
pwsh.exe -NoProfile -File "%PROJECT_DIR%tests\ForeignTroopHud\Test-Portraits.ps1"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd

:foreign_hud_checks_completed
popd

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
pushd "%PROJECT_DIR%"
echo [%date% %time%] Compile projects
"%MSBUILD%" BugfixesAndQoL.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-Modsettings.ps1" -AssemblyPath "%LOCAL_PLUGIN_DIR%\BugfixesAndQoL.dll" -ApiSharedDir "%API_SHARED_DIR%" -ExtenderDir "%EXTENDER_DIR%" -RunNative
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
set "NOTIFICATION_DIAG_ARG="
if "%SHCDE_DEVDIAG%"=="1" set "NOTIFICATION_DIAG_ARG=-DevDiag"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-NotificationDiagnosticArtifact.ps1" -AssemblyPath "%LOCAL_PLUGIN_DIR%\BugfixesAndQoL.dll" %NOTIFICATION_DIAG_ARG%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
popd

echo [%date% %time%] Copy package files
copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
echo [%date% %time%] Copy package files
xcopy "%PROJECT_DIR%Override" "%LOCAL_PLUGIN_DIR%\Override\" /E /I /Q /Y >nul
echo [%date% %time%] Copy package files
xcopy "%PROJECT_DIR%Patches" "%LOCAL_PLUGIN_DIR%\Patches\" /E /I /Q /Y >nul
if not exist "%LOCAL_PLUGIN_DIR%\BugfixesAndQoL.dll" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed

if "%NO_INSTALL%"=="1" goto prepared_no_install
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Disable-LegacyGateTest.ps1" -GameDirectory "%GAME_DIR%"
if not "%ERRORLEVEL%"=="0" goto copy_failed
if exist "%GAME_PLUGIN_DIR%\" (
  rem Keep player-created lobby settings while replacing all packaged files.
  for /D %%D in ("%GAME_PLUGIN_DIR%\*") do (
    if /I not "%%~nxD"=="LobbyModSettings" (
      rmdir /S /Q "%%~fD"
      if not errorlevel 0 cmd /c exit 1
      if errorlevel 1 goto copy_failed
    )
  )
  for %%F in ("%GAME_PLUGIN_DIR%\*") do (
    if exist "%%~fF" if not exist "%%~fF\" (
      del /F /Q "%%~fF"
      if not errorlevel 0 cmd /c exit 1
      if errorlevel 1 goto copy_failed
    )
  )
)
echo [%date% %time%] Copy package files
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if not "%ERRORLEVEL%"=="0" goto copy_failed
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName BugfixesAndQoL
if not "%ERRORLEVEL%"=="0" goto package_failed
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

:prepared_no_install
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName BugfixesAndQoL
if not "%ERRORLEVEL%"=="0" exit /b 1
echo Paket vorbereitet; Installation uebersprungen.
exit /b 0
