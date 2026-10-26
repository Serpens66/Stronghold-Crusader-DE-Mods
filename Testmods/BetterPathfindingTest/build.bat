@echo off
setlocal
set "SHCDE_DEVDIAG="
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %*
set "BUILD_RESULT=%ERRORLEVEL%"
if /i not "%~1"=="/nopause" if /i not "%~2"=="/nopause" if /i not "%~3"=="/nopause" if /i not "%~4"=="/nopause" pause
exit /b %BUILD_RESULT%
