@echo off
setlocal EnableExtensions
rem Selects the Modpack branch: main (public) or develop (private test).
rem Scripts: /branch:main or /branch:develop. Uses the validated APIShared main release; APIShared is published separately.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\Tools\Steam\WorkshopWorkflow.ps1" -Action Prepare %*
exit /b %ERRORLEVEL%
