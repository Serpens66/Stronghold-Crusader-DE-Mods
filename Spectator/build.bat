@echo off
setlocal EnableExtensions
rem Working sources are the development default. Explicit branch selection uses the common driver.
set "SELECT_BRANCH=0"
for %%A in (%*) do for /f "tokens=1 delims=:" %%B in ("%%~A") do if /I "%%B"=="/branch" set "SELECT_BRANCH=1"
if defined SHCDE_BRANCH_SOURCE_ROOT goto selected
if "%SELECT_BRANCH%"=="0" goto selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0.." -Driver "Spectator/build.bat" %*
exit /b %ERRORLEVEL%
:selected
if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
call "%~dp0..\Shared\Tools\ApiSharedRepository\Select-ApiSharedBranch.cmd" %*
if not "%ERRORLEVEL%"=="0" exit /b 1
set "NO_INSTALL=0"
set "NO_PAUSE=0"
for %%A in (%*) do (
 if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
 if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" -ApiSharedDir "%API_SHARED_DIR%" -NoInstall %NO_INSTALL%
set "RESULT=%ERRORLEVEL%"
if "%NO_PAUSE%"=="0" pause
exit /b %RESULT%
