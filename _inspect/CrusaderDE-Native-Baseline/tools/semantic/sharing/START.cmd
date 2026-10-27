@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Create-GhidraProject.ps1"
if errorlevel 1 echo Project creation failed. See the message and logs above.
pause
