@echo off
setlocal EnableExtensions
set "RESTART_NOPAUSE=0"
set "SHCDE_DEVDIAG="
for %%A in (%*) do if /I "%%~A"=="/nopause" set "RESTART_NOPAUSE=1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
set "RESTART_RESULT=%ERRORLEVEL%"
if "%RESTART_NOPAUSE%"=="0" pause
exit /b %RESTART_RESULT%
