@echo off
setlocal EnableExtensions
set "AREA_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "AREA_NOPAUSE=1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
set "AREA_RESULT=%ERRORLEVEL%"
if "%AREA_NOPAUSE%"=="0" pause
exit /b %AREA_RESULT%
