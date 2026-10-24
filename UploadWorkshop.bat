@echo off
setlocal EnableExtensions
rem Uploads the latest published main modpack release; preparation is refreshed automatically.
rem After a successful modpack upload the temporary main release folder (.release-output\Modpack\main\source) is removed.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\Tools\Steam\WorkshopWorkflow.ps1" -Action Upload %*
exit /b %ERRORLEVEL%
