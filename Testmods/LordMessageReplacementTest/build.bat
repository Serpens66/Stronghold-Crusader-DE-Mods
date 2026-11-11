@echo off
setlocal EnableExtensions
rem The central driver selects isolated working or branch sources and preserves its SDK selection.
if defined SHCDE_BRANCH_SOURCE_ROOT goto shcde_branch_selected
if defined SHCDE_BRANCH_PINNED goto shcde_branch_selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0../../Shared/Tools/BranchBuild/Invoke-BranchBuild.ps1" -Repository "%~dp0../../." -Driver "Testmods/LordMessageReplacementTest/build.bat" %*
set "LORD_BUILD_RESULT=%ERRORLEVEL%"
for %%A in (%*) do if /I "%%~A"=="/nopause" exit /b %LORD_BUILD_RESULT%
pause
exit /b %LORD_BUILD_RESULT%

:shcde_branch_selected
set "SHCDE_DEVDIAG="
set "LORD_BUILD_NOINSTALL="
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "LORD_BUILD_NOINSTALL=-NoInstall"
if defined SHCDE_API_SHARED_DIR (
  set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
) else (
  call "%~dp0../../Shared/Tools/ApiSharedRepository/Select-ApiSharedBranch.cmd" %*
  if errorlevel 1 exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools/Build.ps1" -ApiSharedDir "%API_SHARED_DIR%" %LORD_BUILD_NOINSTALL%
exit /b %ERRORLEVEL%
