@echo off
setlocal EnableExtensions EnableDelayedExpansion
if "%~1"=="" (
  echo Missing release project name.
  pause
  exit /b 2
)
set "SCRIPT_DIR=%~dp0"
set "CALLED=0"
set "NO_PROMPT=0"
set "NO_PAUSE=0"
for %%A in (%*) do (
  if /I "%%~A"=="/called" set "CALLED=1"
  if /I "%%~A"=="/noprompt" set "NO_PROMPT=1"
  if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
)
if "%NO_PROMPT%"=="1" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Release-Mod.ps1" -ModName "%~1" -NoPrompt
) else (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Release-Mod.ps1" -ModName "%~1"
)
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" if "%CALLED%"=="0" if "%NO_PAUSE%"=="0" (
  echo.
  echo Release failed. Exit Code: %EXIT_CODE%
  pause
)
exit /b %EXIT_CODE%
