@echo off
setlocal EnableExtensions
rem Uploads the verified preparation and its pinned main publisher, without switching dev.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\Tools\Steam\WorkshopWorkflow.ps1" -Action Upload %*
exit /b %ERRORLEVEL%
