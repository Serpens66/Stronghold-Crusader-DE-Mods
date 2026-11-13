@echo off
setlocal EnableExtensions
set "SHCDE_DEVDIAG="
set "AUTOSEND_DEVDIAG="
for %%A in (%*) do (
  if /I "%%~A"=="/devdiag" set "AUTOSEND_DEVDIAG=-DevDiag"
  if /I not "%%~A"=="/nopause" if /I not "%%~A"=="/noinstall" if /I not "%%~A"=="/devdiag" goto bad_argument
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build.ps1" %AUTOSEND_DEVDIAG%
exit /b %ERRORLEVEL%
:bad_argument
echo Supported arguments: /nopause /noinstall /devdiag. This prototype driver prepares a package without installation.
exit /b 1
