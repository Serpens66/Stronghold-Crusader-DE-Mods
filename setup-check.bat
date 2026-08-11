@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%Shared\Release\Test-ReleaseSetup.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"
echo.
if "%EXIT_CODE%"=="0" (
  echo Setup-Pruefung erfolgreich.
) else (
  echo Setup-Pruefung fehlgeschlagen. Exit Code: %EXIT_CODE%
)
pause
exit /b %EXIT_CODE%
