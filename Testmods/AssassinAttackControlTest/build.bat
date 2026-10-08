@echo off
setlocal
set "NO_PAUSE="
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=-NoPause"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %NO_PAUSE%
exit /b %ERRORLEVEL%
