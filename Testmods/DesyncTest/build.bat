@echo off
setlocal EnableExtensions
if not defined DESYNC_WORKSPACE_ROOT set "DESYNC_WORKSPACE_ROOT=%~dp0..\.."
if not defined DESYNC_OUTPUT_ROOT set "DESYNC_OUTPUT_ROOT=%~dp0BepInEx\plugins\DesyncTest_Serp"
if defined SHCDE_BRANCH_SOURCE_ROOT goto selected
if defined SHCDE_BRANCH_PINNED goto selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0..\.." -Driver "Testmods/DesyncTest/build.bat" %* /noinstall
exit /b %ERRORLEVEL%
:selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1" %*
exit /b %ERRORLEVEL%
