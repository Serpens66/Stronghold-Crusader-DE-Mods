@echo off
set "SHCDE_DEVDIAG=0"
set "buildArgs=%*"
set "buildArgs=%buildArgs:/nopause=-NoPause%"
set "buildArgs=%buildArgs:/devdiag=-DevDiag%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %buildArgs%
exit /b %errorlevel%
