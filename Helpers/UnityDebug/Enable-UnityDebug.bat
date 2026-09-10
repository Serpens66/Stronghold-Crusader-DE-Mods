@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "if (([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { exit 0 }; try { Start-Process -FilePath '%~f0' -Verb RunAs -ErrorAction Stop; exit 42 } catch { exit 43 }"
if "%errorlevel%"=="42" exit /b 0
if not "%errorlevel%"=="0" (
    echo Administrator elevation was not granted.
    pause
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Switch-UnityRuntime.ps1" -Mode Debug
set "switchExitCode=%errorlevel%"

echo.
if not "%switchExitCode%"=="0" echo Switching failed. No further changes will be made.
pause
exit /b %switchExitCode%
