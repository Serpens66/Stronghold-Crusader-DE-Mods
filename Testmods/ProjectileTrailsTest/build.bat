@echo off
set "buildArgs=%*"
set "buildArgs=%buildArgs:/nopause=-NoPause%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %buildArgs%
exit /b %errorlevel%
