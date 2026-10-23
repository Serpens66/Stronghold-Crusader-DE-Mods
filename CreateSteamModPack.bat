@echo off
setlocal EnableExtensions
rem Selects the Modpack branch: main (public) or develop (private test).
rem Scripts: /branch:main or /branch:develop. APIShared (or Beide) releases APIShared from public main first; the modpack uses that release.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\Tools\Steam\WorkshopWorkflow.ps1" -Action Prepare %*
exit /b %ERRORLEVEL%
