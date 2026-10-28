@echo off
setlocal EnableExtensions
rem This standalone test driver builds the current working sources and never rebuilds or installs dependencies.
set "SHCDE_DEVDIAG="
set "LORD_NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/devdiag" set "SHCDE_DEVDIAG=1"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "LORD_NO_PAUSE=1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %*
set "LORD_BUILD_RESULT=%ERRORLEVEL%"
if "%LORD_NO_PAUSE%"=="0" pause
exit /b %LORD_BUILD_RESULT%
