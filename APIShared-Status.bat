@echo off
setlocal
set "NO_PAUSE=0"
set "RELEASE="
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/release" set "RELEASE=-Release"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\Tools\ApiSharedRepository\Status-ApiShared.ps1" %RELEASE%
set "RESULT=%ERRORLEVEL%"
if "%NO_PAUSE%"=="0" pause
exit /b %RESULT%
