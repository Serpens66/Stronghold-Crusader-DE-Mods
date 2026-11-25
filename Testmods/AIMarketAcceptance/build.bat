@echo off
set "acceptArgs=%*"
set "acceptArgs=%acceptArgs:/nopause=-nopause%"
set "acceptArgs=%acceptArgs:/noinstall=-noinstall%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %acceptArgs%
exit /b %errorlevel%
