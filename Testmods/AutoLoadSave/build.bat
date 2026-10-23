@echo off
setlocal EnableExtensions
set "AUTOLOAD_NOPAUSE=0"
set "AUTOLOAD_NOINSTALL="
for %%A in (%*) do if /I "%%~A"=="/nopause" set "AUTOLOAD_NOPAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "AUTOLOAD_NOINSTALL=-NoInstall"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %AUTOLOAD_NOINSTALL%
set "AUTOLOAD_RESULT=%ERRORLEVEL%"
if "%AUTOLOAD_NOPAUSE%"=="0" pause
exit /b %AUTOLOAD_RESULT%
