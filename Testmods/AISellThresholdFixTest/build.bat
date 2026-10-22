@echo off
setlocal EnableExtensions
set "SHCDE_DEVDIAG="
set "BUILD_DRIVER_NOPAUSE=0"
set "NO_INSTALL=0"
set "VERIFY_ONLY=0"
for %%A in (%*) do (
  if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
  if /I "%%~A"=="/devdiag" set "SHCDE_DEVDIAG=1"
  if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
  if /I "%%~A"=="/verifyonly" set "VERIFY_ONLY=1"
  if /I not "%%~A"=="/nopause" if /I not "%%~A"=="/devdiag" if /I not "%%~A"=="/noinstall" if /I not "%%~A"=="/verifyonly" if /I not "%%~A"=="/source:working" goto :bad_argument
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" -NoInstall %NO_INSTALL% -VerifyOnly %VERIFY_ONLY%
set "BUILD_RESULT=%ERRORLEVEL%"
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b %BUILD_RESULT%
:bad_argument
echo ERROR: Supported arguments: /source:working /nopause /devdiag /noinstall /verifyonly
exit /b 1
