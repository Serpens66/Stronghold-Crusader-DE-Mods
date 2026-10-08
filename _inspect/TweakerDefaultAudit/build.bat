@echo off
setlocal
set "PSModulePath=%SystemRoot%\System32\WindowsPowerShell\v1.0\Modules"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
set "BUILD_RESULT=%ERRORLEVEL%"
if /i not "%~1"=="/nopause" pause
exit /b %BUILD_RESULT%
