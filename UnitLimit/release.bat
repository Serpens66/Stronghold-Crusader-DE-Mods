@echo off
call "%~dp0..\Shared\Release\Invoke-Release.bat" UnitLimit /called
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" (
  echo.
  echo Release fehlgeschlagen. Exit Code: %EXIT_CODE%
  pause
)
exit /b %EXIT_CODE%
