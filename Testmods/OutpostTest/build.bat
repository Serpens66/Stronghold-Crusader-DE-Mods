@echo off
setlocal EnableExtensions
rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
if defined SHCDE_BRANCH_PINNED goto :shcde_branch_selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0../../Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0../../." -Driver "Testmods/OutpostTest/build.bat" %*
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
echo [%date% %time%] START OutpostTest
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
setlocal EnableExtensions
set "OUTPOST_PROJECT=%~dp0"
set "NOPAUSE=0"
set "INSTALL_ARGUMENT="
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "INSTALL_ARGUMENT=-NoInstall"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NOPAUSE=1"
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%OUTPOST_PROJECT%Build-OutpostTest.ps1" %INSTALL_ARGUMENT%
set "RESULT=%ERRORLEVEL%"
exit /b %RESULT%
