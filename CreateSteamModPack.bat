@echo off
setlocal EnableExtensions
rem Selects the Modpack branch: main (public) or develop (private test).
rem main is built in a temporary release folder (.release-output\Modpack\main\source); the published
rem release files are copied to .release-output\SerpsMods\v<version> of this workspace.
rem Scripts: /branch:main or /branch:develop. APIShared (or Beide) releases APIShared from public main first; the modpack uses that release.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\Tools\Steam\WorkshopWorkflow.ps1" -Action Prepare %*
exit /b %ERRORLEVEL%
