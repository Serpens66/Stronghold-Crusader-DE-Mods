@echo off
setlocal EnableExtensions
set "RELAY_NOPAUSE=0"
set "RELAY_DEVDIAG="
set "SHCDE_DEVDIAG="
for %%A in (%*) do (
  if /I "%%~A"=="/nopause" set "RELAY_NOPAUSE=1"
  if /I "%%~A"=="/devdiag" set "RELAY_DEVDIAG=-DevDiag"
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %RELAY_DEVDIAG%
set "RELAY_RESULT=%ERRORLEVEL%"
if "%RELAY_NOPAUSE%"=="0" pause
exit /b %RELAY_RESULT%
