@echo off
rem Build.ps1 delegates source/SDK/DEVDIAG selection to the shared BranchBuild dispatcher.
set "buildArgs=%*"
set "buildArgs=%buildArgs:/nopause=-nopause%"
set "buildArgs=%buildArgs:/noinstall=-noinstall%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %buildArgs%
exit /b %errorlevel%
