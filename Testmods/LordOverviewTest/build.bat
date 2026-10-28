@echo off
setlocal
set "BUILD_SCRIPT=%~dp0Build.ps1"
set "SHCDE_DEVDIAG="
set "BUILD_DEVDIAG=0"
set "BUILD_NOINSTALL=0"
:args
if "%~1"=="" goto build
if /I "%~1"=="/devdiag" (set "BUILD_DEVDIAG=1"& shift& goto args)
if /I "%~1"=="/noinstall" (set "BUILD_NOINSTALL=1"& shift& goto args)
if /I "%~1"=="/nopause" (shift& goto args)
echo Unknown argument: %~1
exit /b 2
:build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%BUILD_SCRIPT%" -DevDiag %BUILD_DEVDIAG% -NoInstall %BUILD_NOINSTALL%
exit /b %ERRORLEVEL%
