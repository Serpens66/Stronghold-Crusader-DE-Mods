@echo off
setlocal EnableExtensions
set "SHCDE_DEVDIAG=0"
for %%A in (%*) do if /I "%%~A"=="/devdiag" set "SHCDE_DEVDIAG=1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %*
exit /b %ERRORLEVEL%
