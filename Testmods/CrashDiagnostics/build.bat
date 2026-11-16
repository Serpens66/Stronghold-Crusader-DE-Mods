@echo off
setlocal EnableExtensions
rem Diagnostic support package; never installs or rebuilds the released mods/SDK.
set "SHCDE_DEVDIAG="
set "CD_DEVDIAG="
set "CD_VERIFY="
for %%A in (%*) do (
  if /I "%%~A"=="/devdiag" set "CD_DEVDIAG=-DevDiag"
  if /I "%%~A"=="/verifyonly" set "CD_VERIFY=-VerifyOnly"
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %CD_DEVDIAG% %CD_VERIFY%
exit /b %ERRORLEVEL%
