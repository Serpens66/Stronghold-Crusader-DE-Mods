@echo off
setlocal
set "NO_PAUSE=0"
set "REVISION=origin/main"
for %%A in (%*) do (
  if /I "%%~A"=="/nopause" (set "NO_PAUSE=1") else (set "REVISION=%%~A")
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\Tools\ApiSharedRepository\Update-ApiShared.ps1" -Revision "%REVISION%"
set "RESULT=%ERRORLEVEL%"
if "%NO_PAUSE%"=="0" pause
exit /b %RESULT%
