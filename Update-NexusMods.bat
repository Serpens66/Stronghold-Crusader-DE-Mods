@echo off
setlocal EnableExtensions
set "SCRIPT=%~dp0Shared\Release\Update-NexusMods.ps1"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" %*
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if "%EXIT_CODE%"=="0" (
  echo Nexus-Mods-Aktualisierung beendet.
) else (
  echo Nexus-Mods-Aktualisierung fehlgeschlagen. Exit Code: %EXIT_CODE%
)
pause
exit /b %EXIT_CODE%
