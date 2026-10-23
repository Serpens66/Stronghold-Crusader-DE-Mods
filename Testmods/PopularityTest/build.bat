@echo off
setlocal EnableExtensions
rem Explicit user-selected working sources; default branch menu remains available.
for %%A in (%*) do if /I "%%~A"=="/working" goto :shcde_branch_selected
rem SHCDE_BRANCH_DISPATCH: default committed branch build.
if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
if defined SHCDE_BRANCH_PINNED goto :shcde_branch_selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0../../Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0../../." -Driver "Testmods/PopularityTest/build.bat" %*
set "SHCDE_BRANCH_RESULT=%ERRORLEVEL%"
set "SHCDE_BRANCH_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "SHCDE_BRANCH_PAUSE=0"
if "%SHCDE_BRANCH_PAUSE%"=="1" pause
exit /b %SHCDE_BRANCH_RESULT%
:shcde_branch_selected
endlocal
@echo off
setlocal EnableExtensions
set "NO_PAUSE=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/apishared:installed-dev" set "SHCDE_API_SHARED_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\APIShared_Serp_dev"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
set "ORIGINAL_DIRECTORY=%CD%"
cd /d "%~dp0"
if not "%ERRORLEVEL%"=="0" exit /b 1
call :main
set "RESULT=%ERRORLEVEL%"
cd /d "%ORIGINAL_DIRECTORY%"
echo [%date% %time%] PopularityTest exit code %RESULT%
if "%NO_PAUSE%"=="0" pause
exit /b %RESULT%

:main
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools/Build-PopularityTest.ps1" -NoInstall %NO_INSTALL%
exit /b %ERRORLEVEL%
