@echo off
setlocal EnableExtensions
set "SHCDE_DEVDIAG="
set "AREA_DIAG="
set "AREA_INSTALL="
for %%A in (%*) do (
  if /I "%%~A"=="/devdiag" set "AREA_DIAG=-DevDiag"
  if /I "%%~A"=="/noinstall" set "AREA_INSTALL=-NoInstall"
  if /I not "%%~A"=="/nopause" if /I not "%%~A"=="/devdiag" if /I not "%%~A"=="/noinstall" goto bad_argument
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\Build.ps1" %AREA_DIAG% %AREA_INSTALL%
exit /b %ERRORLEVEL%
:bad_argument
echo Supported arguments: /nopause /noinstall /devdiag
exit /b 1
