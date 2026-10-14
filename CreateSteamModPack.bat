@echo off
setlocal EnableExtensions
rem Uses pinned public APIShared main in an isolated worktree; dev checkout stays active.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\Tools\Steam\WorkshopWorkflow.ps1" -Action Prepare %*
exit /b %ERRORLEVEL%
