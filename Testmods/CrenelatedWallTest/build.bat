@echo off
setlocal EnableExtensions
rem Explicit working-source builds use this testmod's existing, validated SDK.
rem They do not create or rebuild an APIShared checkout.
for %%A in (%*) do if /I "%%~A"=="/source:working" goto :shcde_branch_selected
rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
if defined SHCDE_BRANCH_PINNED goto :shcde_branch_selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0../../Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0../../." -Driver "Testmods/CrenelatedWallTest/build.bat" %*
set "SHCDE_BRANCH_RESULT=%ERRORLEVEL%"
set "SHCDE_BRANCH_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "SHCDE_BRANCH_PAUSE=0"
if "%SHCDE_BRANCH_PAUSE%"=="1" pause
exit /b %SHCDE_BRANCH_RESULT%
:shcde_branch_selected
endlocal
@echo off
setlocal EnableExtensions
set "SHCDE_DEVDIAG="
set "BUILD_DRIVER_NOPAUSE=0"
set "VERIFY_ONLY=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/verifyonly" set "VERIFY_ONLY=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START CrenelatedWallTest
cd /d "%~dp0"
if not "%ERRORLEVEL%"=="0" exit /b 1
call :main
set "BUILD_DRIVER_RESULT=%ERRORLEVEL%"
cd /d "%BUILD_DRIVER_ORIGINAL_DIR%"
echo [%date% %time%] END CrenelatedWallTest: exit code %BUILD_DRIVER_RESULT%
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b %BUILD_DRIVER_RESULT%

:main
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" -NoInstall %NO_INSTALL% -VerifyOnly %VERIFY_ONLY%
exit /b %ERRORLEVEL%
