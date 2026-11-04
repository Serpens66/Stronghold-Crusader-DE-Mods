@echo off
setlocal EnableExtensions
rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
if defined SHCDE_BRANCH_PINNED goto :shcde_branch_selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0../../Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0../../." -Driver "Testmods/MoatMove/build.bat" %*
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
echo [%date% %time%] START MoatMove
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
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\Validation\Test-SharedBoundaries.ps1"
if not "%ERRORLEVEL%"=="0" exit /b 1
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\Validation\Test-UnitCommandSplit.ps1" -ModDirectory Testmods/MoatMove
if not "%ERRORLEVEL%"=="0" exit /b 1
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
if defined SHCDESE_EXTENDER_DIR set "EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "API_SHARED_DIR=%~dp0..\..\APIShared\BepInEx\plugins\APIShared_Serp"
rem Release automation can explicitly use the validated workspace package.
if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
rem APIShared source: /apishared:main (release branch) or /apishared:dev (local checkout, default).
call "%~dp0..\..\Shared\Tools\ApiSharedRepository\Select-ApiSharedBranch.cmd" %*
if not "%ERRORLEVEL%"=="0" goto failed
set "NO_PAUSE=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
echo [%date% %time%] Check that the game is closed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 }"
if not "%ERRORLEVEL%"=="0" (
  echo Game is running. Build and installation aborted.
  goto failed
)
if not exist "%MSBUILD%" goto failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto failed
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\Test-Preflight.ps1"
if not "%ERRORLEVEL%"=="0" goto failed
if not exist "%API_SHARED_DIR%\APIShared.dll" goto failed
rem A branch build copy has no built APIShared next to it; the regression needs the selected package.
set "MOAT_TEST_API_SHARED_DLL=%API_SHARED_DIR%\APIShared.dll"
rem The anchor integration test loads the real native core, so it is compiled first.
echo [%date% %time%] Compile native anchor core
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%native\Build-MoatMoveAnchor.ps1"
if not "%ERRORLEVEL%"=="0" goto failed
set "MOAT_TEST_ANCHOR_DIR=%PROJECT_DIR%.local\anchor"
echo [%date% %time%] Run tests
dotnet run --project "%PROJECT_DIR%tests\MoatMove.Tests.csproj" -- "%PROJECT_DIR%..\.."
if not "%ERRORLEVEL%"=="0" goto failed
echo [%date% %time%] Compile projects
"%MSBUILD%" "%PROJECT_DIR%MoatMove.csproj" /t:Build /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%" /nologo /v:minimal
if not "%ERRORLEVEL%"=="0" goto failed
echo [%date% %time%] Package native anchor core
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%native\Build-MoatMoveAnchor.ps1" -CopyOnly -OutputDirectory "%PROJECT_DIR%BepInEx\plugins\MoatMove_Serp"
if not "%ERRORLEVEL%"=="0" goto failed
if "%NO_INSTALL%"=="1" goto built_without_install
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%tests\Install-Package.ps1" -GameDir "%GAME_DIR%"
if not "%ERRORLEVEL%"=="0" goto failed
echo MoatMove built and installed successfully.
exit /b 0
:built_without_install
echo MoatMove built successfully. Installation skipped.
exit /b 0
:failed
echo MoatMove build or installation failed.
exit /b 1
