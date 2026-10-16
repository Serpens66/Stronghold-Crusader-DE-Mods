@echo off
setlocal EnableExtensions
set "PROBE_NOPAUSE=0"
set "PROBE_NOINSTALL="
for %%A in (%*) do if /I "%%~A"=="/nopause" set "PROBE_NOPAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "PROBE_NOINSTALL=-NoInstall"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %PROBE_NOINSTALL%
set "PROBE_RESULT=%ERRORLEVEL%"
if "%PROBE_NOPAUSE%"=="0" pause
exit /b %PROBE_RESULT%