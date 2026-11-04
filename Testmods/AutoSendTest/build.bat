@echo off
setlocal EnableExtensions
for %%A in (%*) do if /I not "%%~A"=="/nopause" if /I not "%%~A"=="/noinstall" goto bad_argument
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build.ps1"
exit /b %ERRORLEVEL%
:bad_argument
echo Supported arguments: /nopause /noinstall. This prototype driver prepares a package without installation.
exit /b 1
