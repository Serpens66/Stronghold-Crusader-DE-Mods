@echo off
set "buildArgs=%*"
set "buildArgs=%buildArgs:/nopause=-nopause%"
set "buildArgs=%buildArgs:/noinstall=-noinstall%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %buildArgs%
exit /b %errorlevel%
