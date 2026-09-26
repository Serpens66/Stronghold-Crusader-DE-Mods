@echo off
setlocal
set "PRESERVE_PROJECT=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PRESERVE_PROJECT%Build-KeepCampfireGroundPreserveTest.ps1"
set "RESULT=%ERRORLEVEL%"
if /i not "%~1"=="/nopause" pause
exit /b %RESULT%
