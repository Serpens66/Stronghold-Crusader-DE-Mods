@echo off
setlocal EnableExtensions
rem Explicit development driver: current working sources, selected installed SDK, no installation.
for %%A in (%*) do if /I not "%%~A"=="/nopause" if /I not "%%~A"=="/noinstall" goto bad_argument
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build.ps1"
set "BUILD_RESULT=%ERRORLEVEL%"
for %%A in (%*) do if /I "%%~A"=="/nopause" exit /b %BUILD_RESULT%
pause
exit /b %BUILD_RESULT%
:bad_argument
echo Unsupported argument. This test driver builds working sources without installation; /nopause and /noinstall are supported.
exit /b 1
