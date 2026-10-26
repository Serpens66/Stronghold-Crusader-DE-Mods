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
rem SHCDE_TEST_MODES: full quick none
setlocal EnableExtensions EnableDelayedExpansion
set "BUILD_OPTIONS_FILE=%TEMP%\shcde-bfq-options-%RANDOM%-%RANDOM%.txt"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0BuildOptions.ps1" -OutFile "%BUILD_OPTIONS_FILE%" %*
if not "%ERRORLEVEL%"=="0" (
  if exist "%BUILD_OPTIONS_FILE%" del /Q "%BUILD_OPTIONS_FILE%"
  exit /b 1
)
set /p "BUILD_TEST_MODE="<"%BUILD_OPTIONS_FILE%"
del /Q "%BUILD_OPTIONS_FILE%"
echo [%date% %time%] Test mode: %BUILD_TEST_MODE%
set "COMMAND_COMPILER_OPTION="
if "%BUILD_TEST_MODE%"=="quick" set "COMMAND_COMPILER_OPTION=-SkipCompiler"
if "%BUILD_TEST_MODE%"=="none" goto build_setup
echo [%date% %time%] Workspace source and runtime preflight
call :step_start "tests" "01-Test-SharedBoundaries.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-SharedBoundaries.ps1" -ModDirectory BugfixesAndQoL
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" exit /b 1
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "02-Verify-Interop.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\_inspect\FormationIntegration\Verify-Interop.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" exit /b 1
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "03-Test-UnitCommandSplit.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-UnitCommandSplit.ps1" -ModDirectory BugfixesAndQoL -SkipPermanentHooks -SkipCommonChecks %COMMAND_COMPILER_OPTION%
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" exit /b 1
:build_setup
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
call :step_start "sdk" "04-Select-ApiSharedBranch.cmd"
call "%~dp0..\Shared\Tools\ApiSharedRepository\Select-ApiSharedBranch.cmd" %*
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" exit /b 1
call :step_start "sdk" "05-Test-ConsumerPackage.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0.." -PackageDirectory "%API_SHARED_DIR%"
call :step_end %ERRORLEVEL%
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
call :step_start "prerequisites" "06-game-closed"
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
call :step_end %ERRORLEVEL%
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
rem Checked builds run the common source and runtime safety checks once.
set "NOTIFICATION_CHECKS=0"
set "GATEHOUSE_CHECKS=0"
set "FOREIGN_HUD_CHECKS=0"
set "SETTINGS_CHECKS=0"
set "COOP_CHECKS=0"
for %%A in (%*) do if /I "%%~A"=="/checks:notifications" set "NOTIFICATION_CHECKS=1"
for %%A in (%*) do if /I "%%~A"=="/checks:gatehouse" set "GATEHOUSE_CHECKS=1"
for %%A in (%*) do if /I "%%~A"=="/checks:foreignhud" set "FOREIGN_HUD_CHECKS=1"
for %%A in (%*) do if /I "%%~A"=="/checks:settings" set "SETTINGS_CHECKS=1"
for %%A in (%*) do if /I "%%~A"=="/checks:coop" set "COOP_CHECKS=1"
set /a CHECK_SCOPES=NOTIFICATION_CHECKS+GATEHOUSE_CHECKS+FOREIGN_HUD_CHECKS+SETTINGS_CHECKS+COOP_CHECKS
if %CHECK_SCOPES% GTR 1 goto build_failed
pushd "%PROJECT_DIR%"
if "%BUILD_TEST_MODE%"=="none" goto foreign_hud_checks_completed
rem Shared Coop selection validation is part of every checked build of both consumers.
dotnet build "%PROJECT_DIR%..\Shared\Tests\CoopAiStart.Tests\Shared.CoopAiStart.Tests.csproj" -c Release "-p:GameDir=%GAME_DIR%" "-p:ExtenderDir=%EXTENDER_DIR%"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%..\Shared\Tests\CoopAiStart.Tests\bin\Release\net481\Shared.CoopAiStart.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
if "%COOP_CHECKS%"=="1" goto coop_checks
if "%BUILD_TEST_MODE%"=="quick" goto quick_checks
if "%SETTINGS_CHECKS%"=="1" goto settings_checks
if "%FOREIGN_HUD_CHECKS%"=="1" goto foreign_hud_checks
if "%GATEHOUSE_CHECKS%"=="1" goto gatehouse_checks
if "%NOTIFICATION_CHECKS%"=="1" goto notification_checks
echo [%date% %time%] Lobby save loading and visibility contracts
call :step_start "tests" "07-Test-LobbySaveLoading.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-LobbySaveLoading.ps1" -RunTests
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Integrated gate routing migration regression
call :step_start "tests" "08-Test-GateRouting.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-GateRouting.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Gatehouse target-marker height contracts
call :step_start "tests" "09-Test-GatehouseTargetMarkerHeight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-GatehouseTargetMarkerHeight.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "10-Test-DrawbridgeDestructionFix.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-DrawbridgeDestructionFix.ps1" -RunTests
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "10b-Test-MoatTerrain.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-MoatTerrain.ps1" -RunTests
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "10c-Test-EditorRegions.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-EditorRegions.ps1" -RunTests
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "11-Test-GatehouseLivingCapturePreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-GatehouseLivingCapturePreflight.ps1" -RunTests
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "12-Test-BuildPreviewPreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-BuildPreviewPreflight.ps1" -ExtenderDir "%EXTENDER_DIR%"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "13-BuildPreview.Tests.csproj"
"%MSBUILD%" "%PROJECT_DIR%tests\BuildPreview.Tests\BuildPreview.Tests.csproj" /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%" /nologo /verbosity:minimal
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "14-BuildPreview.Tests.exe"
"%PROJECT_DIR%tests\BuildPreview.Tests\bin\BuildPreview.Tests.exe" "%GAME_DIR%\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "15-Test-RuntimePreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "16-Test-ModOptionsPerformance.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-ModOptionsPerformance.ps1" -RunTests
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "16b-Test-MinimapThrottlePreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-MinimapThrottlePreflight.ps1" -ExtenderDir "%EXTENDER_DIR%"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "17-verify.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\_inspect\AssassinGateClimb\verify.ps1" -SkipPermanentHooks -SkipCommonChecks
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "18-Test-AIKeepRangePreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-AIKeepRangePreflight.ps1" -GameDir "%GAME_DIR%" -ExtenderDir "%EXTENDER_DIR%" -ApiSharedDir "%API_SHARED_DIR%" -SkipPermanentHooks
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "19-Test-AiRaidApiContracts.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-AiRaidApiContracts.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "20-Test-AiRaidRetargetPreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-AiRaidRetargetPreflight.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "21-Run.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\AiRaidRetarget.Tests\Run.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "22-Test-NotificationLastFramePreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-NotificationLastFramePreflight.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "23-Test-SpectatorPerspectivePreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-SpectatorPerspectivePreflight.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "24-Test-DamagedHealthBarsPreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-DamagedHealthBarsPreflight.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "25-Test-WorkshopIdleDelayPreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-WorkshopIdleDelayPreflight.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "26-Test-TannerFadePreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-TannerFadePreflight.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
echo [%date% %time%] Arabian idle volume consumer tests
call :step_start "tests" "27-ArabianIdleSound.Tests.csproj"
"%MSBUILD%" "%PROJECT_DIR%tests\ArabianIdleSound.Tests\ArabianIdleSound.Tests.csproj" /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "28-ArabianIdleSoundVolume.Tests.exe"
"%PROJECT_DIR%tests\ArabianIdleSound.Tests\bin\ArabianIdleSoundVolume.Tests.exe" "%GAME_DIR%" "%PROJECT_DIR%.."
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
call :step_start "tests" "29-HostClientPresetTests.csproj"
"%MSBUILD%" "%PROJECT_DIR%..\_inspect\HostClientPresetTests\HostClientPresetTests.csproj" /p:Configuration=Debug
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "30-HostClientPresetTests.exe"
"%PROJECT_DIR%..\_inspect\HostClientPresetTests\bin\HostClientPresetTests.exe"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Run tests
call :step_start "tests" "31-Formations.Core.Tests.csproj"
dotnet test tests\Formations.Core.Tests\Formations.Core.Tests.csproj --configuration Release --logger trx --results-directory tests\Formations.Core.Tests\TestResults
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Formation speech regression tests
call :step_start "tests" "32-FormationSpeech.Tests.csproj"
dotnet run --project tests\FormationSpeech.Tests\FormationSpeech.Tests.csproj --configuration Release -p:ExtenderDir="%EXTENDER_DIR%" -- "%PROJECT_DIR%.."
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
call :step_start "tests" "33-Formations.Tests.csproj"
"%MSBUILD%" tests\Formations.Tests\Formations.Tests.csproj /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "34-Formations.Tests.exe"
"%PROJECT_DIR%tests\Formations.Tests\bin\Formations.Tests.exe"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
call :step_start "tests" "35-WaterboyTargetReservation.Tests.csproj"
"%MSBUILD%" tests\WaterboyTargetReservation.Tests\WaterboyTargetReservation.Tests.csproj /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "36-WaterboyTargetReservation.Tests.exe"
"%PROJECT_DIR%tests\WaterboyTargetReservation.Tests\bin\WaterboyTargetReservation.Tests.exe"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] PowerShell checks / build step
call :step_start "tests" "37-Test-RedBirdVersions.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\WaterboyTargetReservation.Tests\Test-RedBirdVersions.ps1" -TestExecutable "%PROJECT_DIR%tests\WaterboyTargetReservation.Tests\bin\WaterboyTargetReservation.Tests.exe" -ExtenderDir "%EXTENDER_DIR%"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
call :step_start "tests" "38-TannerFade.Tests.csproj"
"%MSBUILD%" tests\TannerFade.Tests\TannerFade.Tests.csproj /p:Configuration=Release
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "39-TannerFade.Tests.exe"
"%PROJECT_DIR%tests\TannerFade.Tests\bin\TannerFade.Tests.exe"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
call :step_start "tests" "40-WorkshopIdleDelay.Tests.csproj"
"%MSBUILD%" tests\WorkshopIdleDelay.Tests\WorkshopIdleDelay.Tests.csproj /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "41-WorkshopIdleDelay.Tests.exe"
"%PROJECT_DIR%tests\WorkshopIdleDelay.Tests\bin\WorkshopIdleDelay.Tests.exe" "%GAME_DIR%\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Tree-blight native regression
call :step_start "tests" "TreeBlightDecay.Tests build"
"%MSBUILD%" tests\TreeBlightDecay.Tests\TreeBlightDecay.Tests.csproj /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "TreeBlightDecay.Tests execution"
"%PROJECT_DIR%tests\TreeBlightDecay.Tests\bin\TreeBlightDecay.Tests.exe" "%GAME_DIR%\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
call :step_start "tests" "42-WorkerBreakPause.Tests.csproj"
"%MSBUILD%" tests\WorkerBreakPause.Tests\WorkerBreakPause.Tests.csproj /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "43-WorkerBreakPause.Tests.exe"
"%PROJECT_DIR%tests\WorkerBreakPause.Tests\bin\WorkerBreakPause.Tests.exe" "%GAME_DIR%\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
call :step_start "tests" "44-BugfixesAndQoLNativeTests.csproj"
"%MSBUILD%" "%PROJECT_DIR%..\_inspect\BugfixesAndQoLNativeTests\BugfixesAndQoLNativeTests.csproj" /p:Configuration=Release
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "45-BugfixesAndQoLNativeTests.exe"
"%PROJECT_DIR%..\_inspect\BugfixesAndQoLNativeTests\bin\BugfixesAndQoLNativeTests.exe"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
call :step_start "tests" "46-VanillaPeaceTimeNativeTests.csproj"
"%MSBUILD%" "%PROJECT_DIR%..\_inspect\VanillaPeaceTimeNativeTests\VanillaPeaceTimeNativeTests.csproj" /p:Configuration=Release
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "47-VanillaPeaceTimeNativeTests.exe"
"%PROJECT_DIR%..\_inspect\VanillaPeaceTimeNativeTests\bin\VanillaPeaceTimeNativeTests.exe"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Compile projects
call :step_start "tests" "48-ImprovedMoatFilling.Tests.csproj"
"%MSBUILD%" tests\ImprovedMoatFilling.Tests.csproj /p:Configuration=Debug
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Run tests
call :step_start "tests" "49-ImprovedMoatFilling.Tests.exe"
"%PROJECT_DIR%tests\bin\ImprovedMoatFilling.Tests.exe"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Run tests
call :step_start "tests" "50-ExtendedShiftCommandQueue.Tests.csproj"
dotnet run --project "tests\ExtendedShiftCommandQueue.Tests\ExtendedShiftCommandQueue.Tests.csproj"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Issue 6 infinite-patrol regressions
call :step_start "tests" "51-Issue6.Tests.csproj"
dotnet run --project "tests\Issue6.Tests\Issue6.Tests.csproj" -- "%PROJECT_DIR%.."
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Run tests
call :step_start "tests" "52-AssassinPathfinding.Tests.csproj"
dotnet run --project "tests\AssassinPathfinding.Tests\AssassinPathfinding.Tests.csproj" -- "%PROJECT_DIR%.."
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Captured-gate protection contracts
call :step_start "tests" "53-verify-integrated.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\_inspect\AssassinAttackControlAudit\verify-integrated.ps1" -SkipPermanentHooks -SkipCommonChecks
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
echo [%date% %time%] Captured-gate protection tests
call :step_start "tests" "54-AssassinCapturedGateProtection.Tests.csproj"
"%MSBUILD%" "tests\AssassinCapturedGateProtection.Tests\AssassinCapturedGateProtection.Tests.csproj" /p:Configuration=Release
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "55-AssassinCapturedGateProtectionTests.exe"
"%PROJECT_DIR%tests\AssassinCapturedGateProtection.Tests\bin\AssassinCapturedGateProtectionTests.exe"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
set "MOAT_TEST_API_SHARED_DLL=%API_SHARED_DIR%\APIShared.dll"
echo [%date% %time%] Run tests
call :step_start "tests" "56-FriendlyMoatMovement.Tests.csproj"
dotnet run --project "tests\FriendlyMoatMovement.Tests\FriendlyMoatMovement.Tests.csproj" -- "%PROJECT_DIR%.."
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
goto checks_completed

:quick_checks
echo [%date% %time%] Quick runtime preflight
call :step_start "tests" "57-Test-RuntimePreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1" -SkipCommonChecks
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
goto foreign_hud_checks_completed

:notification_checks
echo [%date% %time%] Notification-scoped runtime and behavior checks
call :step_start "tests" "58-Test-RuntimePreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "59-Test-NotificationLastFramePreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-NotificationLastFramePreflight.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "60-ImprovedMoatFilling.Tests.csproj"
"%MSBUILD%" tests\ImprovedMoatFilling.Tests.csproj /p:Configuration=Debug /p:ExtenderDir="%EXTENDER_DIR%" /nologo /verbosity:minimal
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "61-ImprovedMoatFilling.Tests.exe"
"%PROJECT_DIR%tests\bin\ImprovedMoatFilling.Tests.exe" --notifications
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
goto checks_completed

:settings_checks
echo [%date% %time%] Settings-scoped runtime preflight; production UI/catalog tests run after compilation
call :step_start "tests" "62-Test-RuntimePreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "63-ImprovedMoatFilling.Tests.csproj"
"%MSBUILD%" tests\ImprovedMoatFilling.Tests.csproj /p:Configuration=Debug /p:ExtenderDir="%EXTENDER_DIR%" /nologo /verbosity:minimal
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "64-ImprovedMoatFilling.Tests.exe"
"%PROJECT_DIR%tests\bin\ImprovedMoatFilling.Tests.exe" --settings-ui
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
goto foreign_hud_checks_completed

:gatehouse_checks
echo [%date% %time%] Gatehouse geometry, live topology and diagnostic behavior checks
call :step_start "tests" "65-Test-RuntimePreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "66-Test-GatehouseDiagnostics.ps1"
pwsh.exe -NoProfile -File "%PROJECT_DIR%tests\GatehouseDiagnostics\Test-GatehouseDiagnostics.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "67-Test-GatehouseDiagnostics.ps1"
pwsh.exe -NoProfile -File "%PROJECT_DIR%tests\GatehouseDiagnostics\Test-GatehouseDiagnostics.ps1" -Diagnostic
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd

goto checks_completed

:foreign_hud_checks
echo [%date% %time%] Foreign troop HUD runtime, skin and player-color checks
call :step_start "tests" "68-Test-RuntimePreflight.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "69-Test-ModOptionsPerformance.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-ModOptionsPerformance.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
goto foreign_hud_behavior_checks

:coop_checks
"%MSBUILD%" "%PROJECT_DIR%tests\ImprovedMoatFilling.Tests.csproj" /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /nologo /verbosity:minimal
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%PROJECT_DIR%tests\bin\ImprovedMoatFilling.Tests.exe" --coop-ai
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
goto foreign_hud_checks_completed

:checks_completed
rem Keep foreign HUD regressions in the ordinary suite as well as the scoped build.
if "%NOTIFICATION_CHECKS%%GATEHOUSE_CHECKS%"=="00" goto foreign_hud_behavior_checks
goto foreign_hud_checks_completed

:foreign_hud_behavior_checks
call :step_start "tests" "70-Test-PlayerColors.ps1"
pwsh.exe -NoProfile -File "%PROJECT_DIR%tests\ForeignTroopHud\Test-PlayerColors.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
call :step_start "tests" "71-Test-Portraits.ps1"
pwsh.exe -NoProfile -File "%PROJECT_DIR%tests\ForeignTroopHud\Test-Portraits.ps1"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd

:foreign_hud_checks_completed
popd

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
pushd "%PROJECT_DIR%"
echo [%date% %time%] Compile projects
call :step_start "compile" "72-BugfixesAndQoL.csproj"
"%MSBUILD%" BugfixesAndQoL.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%"
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
if "%BUILD_TEST_MODE%"=="none" goto artifact_tests_completed
set "MODSETTINGS_NATIVE_OPTION="
if "%BUILD_TEST_MODE%"=="full" set "MODSETTINGS_NATIVE_OPTION=-RunNative"
call :step_start "tests" "73-Test-Modsettings.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-Modsettings.ps1" -AssemblyPath "%LOCAL_PLUGIN_DIR%\BugfixesAndQoL.dll" -ApiSharedDir "%API_SHARED_DIR%" -ExtenderDir "%EXTENDER_DIR%" %MODSETTINGS_NATIVE_OPTION%
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
:artifact_tests_completed
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
call :step_start "package" "74-Disable-LegacyGateTest.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Disable-LegacyGateTest.ps1" -GameDirectory "%GAME_DIR%"
call :step_end %ERRORLEVEL%
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
call :step_start "package" "75-Write-LocalBuildManifest.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName BugfixesAndQoL
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" goto package_failed
echo Build und Installation von Bugfixes and QoL erfolgreich. Testmodus: %BUILD_TEST_MODE%.
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
call :step_start "package" "76-Write-LocalBuildManifest.ps1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName BugfixesAndQoL
call :step_end %ERRORLEVEL%
if not "%ERRORLEVEL%"=="0" exit /b 1
echo Paket vorbereitet; Installation uebersprungen. Testmodus: %BUILD_TEST_MODE%.
exit /b 0

:step_start
set "STEP_CATEGORY=%~1"
set "STEP_NAME=%~2"
call :step_clock
set "STEP_STARTED=!STEP_CLOCK!"
echo [%date% %time%] START !STEP_CATEGORY!: !STEP_NAME!
exit /b 0

:step_end
set "STEP_RESULT=%~1"
call :step_clock
set /a "STEP_ELAPSED=(STEP_CLOCK-STEP_STARTED+8640000)%%8640000*10" >nul
echo [%date% %time%] [BuildTiming] Category=!STEP_CATEGORY! Step=!STEP_NAME! DurationMs=!STEP_ELAPSED! ExitCode=!STEP_RESULT!
exit /b %STEP_RESULT%

:step_clock
set "STEP_TIME=!time: =0!"
set /a "STEP_CLOCK=(1!STEP_TIME:~0,2!-100)*360000+(1!STEP_TIME:~3,2!-100)*6000+(1!STEP_TIME:~6,2!-100)*100+(1!STEP_TIME:~9,2!-100)" >nul
exit /b 0
