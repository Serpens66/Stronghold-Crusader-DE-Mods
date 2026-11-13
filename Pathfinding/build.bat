@echo off
setlocal EnableExtensions
rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
if defined SHCDE_BRANCH_PINNED goto :shcde_branch_selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0../Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0../." -Driver "Pathfinding/build.bat" %*
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
echo [%date% %time%] START Pathfinding
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
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-SharedBoundaries.ps1" -ModDirectory Pathfinding
if not "%ERRORLEVEL%"=="0" exit /b 1
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
if defined SHCDESE_EXTENDER_DIR set "EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "API_SHARED_DIR=%~dp0..\APIShared\BepInEx\plugins\APIShared_Serp"
rem Release automation can explicitly use the validated workspace package.
if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
rem APIShared source: /apishared:main (release branch) or /apishared:dev (local checkout, default).
call "%~dp0..\Shared\Tools\ApiSharedRepository\Select-ApiSharedBranch.cmd" %*
if not "%ERRORLEVEL%"=="0" goto failed
set "NO_INSTALL=0"
set "NO_TESTS=0"
set "CONFIG_ARGS="
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
for %%A in (%*) do if /I "%%~A"=="/notests" set "NO_TESTS=1"
for %%A in (%*) do if /I "%%~A"=="/devdiag" set "CONFIG_ARGS=/p:SerpDevDiagnostics=true"
echo [%date% %time%] Check that the game is closed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 }"
if not "%ERRORLEVEL%"=="0" (
  echo Game is running. Build and installation aborted.
  goto failed
)
if not exist "%MSBUILD%" goto failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto failed
if not exist "%API_SHARED_DIR%\APIShared.dll" goto failed
echo [%date% %time%] Verify the selected APIShared package before anything is replaced
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0.." -PackageDirectory "%API_SHARED_DIR%"
if not "%ERRORLEVEL%"=="0" goto failed
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\Pathfinding_Serp"
echo [%date% %time%] Preflight
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\Test-Preflight.ps1"
if not "%ERRORLEVEL%"=="0" goto failed
echo [%date% %time%] Compile native cores
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%native\Build-PathfindingAnchor.ps1"
if not "%ERRORLEVEL%"=="0" goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%native\Build-PathfindingAssassin.ps1"
if not "%ERRORLEVEL%"=="0" goto failed
if "%NO_TESTS%"=="1" goto compile_projects
rem The anchor integration test loads the real native core.
set "MOAT_TEST_API_SHARED_DLL=%API_SHARED_DIR%\APIShared.dll"
set "MOAT_TEST_ANCHOR_DIR=%PROJECT_DIR%obj\native-anchor"
echo [%date% %time%] Run tests
dotnet run --project "%PROJECT_DIR%tests\Engine\Pathfinding.Engine.Tests.csproj" -- "%PROJECT_DIR%.."
if not "%ERRORLEVEL%"=="0" goto failed
dotnet test "%PROJECT_DIR%tests\Formations.Core.Tests\Formations.Core.Tests.csproj" --configuration Release
if not "%ERRORLEVEL%"=="0" goto failed
dotnet run --project "%PROJECT_DIR%tests\FormationSpeech.Tests\FormationSpeech.Tests.csproj" --configuration Release -p:ExtenderDir="%EXTENDER_DIR%" -- "%PROJECT_DIR%.."
if not "%ERRORLEVEL%"=="0" goto failed
"%MSBUILD%" "%PROJECT_DIR%tests\Formations.Tests\Formations.Tests.csproj" /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%" /nologo /v:minimal
if not "%ERRORLEVEL%"=="0" goto failed
"%PROJECT_DIR%tests\Formations.Tests\bin\Formations.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto failed
dotnet run --project "%PROJECT_DIR%tests\ExtendedShiftCommandQueue.Tests\ExtendedShiftCommandQueue.Tests.csproj"
if not "%ERRORLEVEL%"=="0" goto failed
dotnet run --project "%PROJECT_DIR%tests\Issue6.Tests\Issue6.Tests.csproj" -- "%PROJECT_DIR%.."
if not "%ERRORLEVEL%"=="0" goto failed
dotnet run --project "%PROJECT_DIR%tests\AssassinPathfinding.Tests\AssassinPathfinding.Tests.csproj" -- "%PROJECT_DIR%.."
if not "%ERRORLEVEL%"=="0" goto failed
dotnet run --project "%PROJECT_DIR%tests\FriendlyMoatMovement.Tests\FriendlyMoatMovement.Tests.csproj" -- "%PROJECT_DIR%.."
if not "%ERRORLEVEL%"=="0" goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\_inspect\FormationIntegration\Verify-Interop.ps1"
if not "%ERRORLEVEL%"=="0" goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-UnitCommandSplit.ps1" -ModDirectory Pathfinding -SkipPermanentHooks -SkipCommonChecks
if not "%ERRORLEVEL%"=="0" goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\_inspect\AssassinGateClimb\verify.ps1" -SkipPermanentHooks -SkipCommonChecks
if not "%ERRORLEVEL%"=="0" goto failed
"%MSBUILD%" "%PROJECT_DIR%tests\Movement.Tests\Movement.Tests.csproj" /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%" /nologo /v:minimal
if not "%ERRORLEVEL%"=="0" goto failed
"%PROJECT_DIR%tests\Movement.Tests\bin\Movement.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto failed
pwsh.exe -NoProfile -File "%PROJECT_DIR%tests\GatehouseDiagnostics\Test-GatehouseDiagnostics.ps1"
if not "%ERRORLEVEL%"=="0" goto failed
pwsh.exe -NoProfile -File "%PROJECT_DIR%tests\GatehouseDiagnostics\Test-GatehouseDiagnostics.ps1" -Diagnostic
if not "%ERRORLEVEL%"=="0" goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-GateRouting.ps1"
if not "%ERRORLEVEL%"=="0" goto failed
:compile_projects
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%Pathfinding.csproj" /t:Build /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%" %CONFIG_ARGS% /nologo /v:minimal
if not "%ERRORLEVEL%"=="0" goto failed
echo [%date% %time%] Package native cores
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%native\Build-PathfindingAnchor.ps1" -CopyOnly -OutputDirectory "%LOCAL_PLUGIN_DIR%"
if not "%ERRORLEVEL%"=="0" goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%native\Build-PathfindingAssassin.ps1" -CopyOnly -OutputDirectory "%LOCAL_PLUGIN_DIR%"
if not "%ERRORLEVEL%"=="0" goto failed
if "%NO_INSTALL%"=="1" goto built_without_install
echo [%date% %time%] Install package
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\Install-Package.ps1" -GameDir "%GAME_DIR%"
if not "%ERRORLEVEL%"=="0" goto failed
echo Pathfinding built and installed successfully.
exit /b 0
:built_without_install
echo Pathfinding built successfully. Installation skipped.
exit /b 0
:failed
echo Pathfinding build or installation failed.
exit /b 1
