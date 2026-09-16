@echo off
setlocal EnableExtensions
set "OUTPOST_PROJECT=%~dp0"
set "NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NOPAUSE=1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%OUTPOST_PROJECT%Build-OutpostTest.ps1"
set "RESULT=%ERRORLEVEL%"
if "%NOPAUSE%"=="0" pause
exit /b %RESULT%
