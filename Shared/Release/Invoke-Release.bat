@echo off
setlocal EnableExtensions
if "%~1"=="" (
  echo Missing release project name.
  pause
  exit /b 2
)
set "SCRIPT_DIR=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Release-Mod.ps1" -ModName "%~1"
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" if /I not "%~2"=="/called" (
  echo.
  echo Release failed. Exit Code: %EXIT_CODE%
  pause
)
exit /b %EXIT_CODE%
