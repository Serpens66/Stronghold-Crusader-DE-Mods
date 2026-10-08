@echo off
setlocal EnableExtensions
set "NO_PAUSE=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build.ps1" -NoInstall %NO_INSTALL%
set "RESULT=%ERRORLEVEL%"
if "%NO_PAUSE%"=="0" pause
exit /b %RESULT%
