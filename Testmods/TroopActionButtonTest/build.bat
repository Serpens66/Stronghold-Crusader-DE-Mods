@echo off
setlocal EnableExtensions
set "SHCDE_DEVDIAG="
for %%A in (%*) do (
  if /I not "%%~A"=="/nopause" if /I not "%%~A"=="/noinstall" goto bad_argument
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build.ps1"
exit /b %ERRORLEVEL%
:bad_argument
echo Supported arguments: /nopause /noinstall. This local testmod always builds current sources without installation.
exit /b 1
