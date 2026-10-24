@echo off
setlocal EnableExtensions
rem Double-click shows a menu: 1 SerpsMods, 2 APIShared, 3 both (APIShared first), 0 cancel.
rem Scripts: /modpack, /apishared or /both; /nopause without a target uploads the modpack.
rem After a successful modpack upload the temporary main release folder (.release-output\Modpack\main\source) is removed.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\Tools\Steam\WorkshopWorkflow.ps1" -Action Upload %*
exit /b %ERRORLEVEL%
