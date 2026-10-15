@echo off
setlocal EnableExtensions
rem Configuration and target selection are shared by both Workshop drivers.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\Tools\Steam\WorkshopWorkflow.ps1" -Action Upload %*
exit /b %ERRORLEVEL%
