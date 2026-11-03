@echo off
setlocal EnableExtensions
set "UIACCEPT_NOPAUSE=0"
set "UIACCEPT_NOINSTALL="
for %%A in (%*) do if /I "%%~A"=="/nopause" set "UIACCEPT_NOPAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "UIACCEPT_NOINSTALL=-NoInstall"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %UIACCEPT_NOINSTALL%
set "UIACCEPT_RESULT=%ERRORLEVEL%"
if "%UIACCEPT_NOPAUSE%"=="0" pause
exit /b %UIACCEPT_RESULT%
