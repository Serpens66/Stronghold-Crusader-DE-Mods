@echo off
setlocal EnableExtensions
set "INSTALL_ARGUMENT="
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "INSTALL_ARGUMENT=-NoInstall"
for %%A in (%*) do if /I not "%%~A"=="/nopause" if /I not "%%~A"=="/noinstall" goto bad_argument
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build.ps1" %INSTALL_ARGUMENT%
set "BUILD_RESULT=%ERRORLEVEL%"
for %%A in (%*) do if /I "%%~A"=="/nopause" exit /b %BUILD_RESULT%
pause
exit /b %BUILD_RESULT%
:bad_argument
echo Builds working sources against the local APIShared SDK and installed game/Extender. Supported: /nopause /noinstall.
exit /b 1
