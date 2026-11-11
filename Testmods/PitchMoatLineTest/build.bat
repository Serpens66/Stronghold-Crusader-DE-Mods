@echo off
setlocal EnableExtensions
set "SHCDE_DEVDIAG="
set "PITCH_MOAT_DEVDIAG="
for %%A in (%*) do (
  if /I "%%~A"=="/devdiag" set "PITCH_MOAT_DEVDIAG=-DevDiag"
  if /I not "%%~A"=="/nopause" if /I not "%%~A"=="/noinstall" if /I not "%%~A"=="/devdiag" goto bad_argument
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build.ps1" %PITCH_MOAT_DEVDIAG%
exit /b %ERRORLEVEL%
:bad_argument
echo Supported arguments: /nopause /noinstall /devdiag. This testmod driver prepares a package without installation.
exit /b 1
