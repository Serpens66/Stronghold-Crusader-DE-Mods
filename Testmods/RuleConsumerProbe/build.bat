@echo off
setlocal EnableExtensions
set "RCP_NOPAUSE=0"
set "RCP_NOINSTALL="
for %%A in (%*) do if /I "%%~A"=="/nopause" set "RCP_NOPAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "RCP_NOINSTALL=-NoInstall"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %RCP_NOINSTALL%
set "RCP_RESULT=%ERRORLEVEL%"
if "%RCP_NOPAUSE%"=="0" pause
exit /b %RCP_RESULT%
