@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Shared\Tools\ApiSharedRepository\Open-PullRequest.ps1" -Target Develop %*
exit /b %ERRORLEVEL%
