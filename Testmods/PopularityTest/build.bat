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
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
set "ORIGINAL_DIRECTORY=%CD%"
cd /d "%~dp0"
if errorlevel 1 exit /b 1
call :main
set "RESULT=%ERRORLEVEL%"
cd /d "%ORIGINAL_DIRECTORY%"
echo [%date% %time%] PopularityTest exit code %RESULT%
if "%NO_PAUSE%"=="0" pause
exit /b %RESULT%

:main
echo [%date% %time%] PopularityTest preflight
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify.ps1"
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { throw 'Close the game before building/installing PopularityTest.' }"
if errorlevel 1 exit /b 1
"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" "%~dp0PopularityTest.csproj" /p:Configuration=Debug /nologo /verbosity:minimal
if errorlevel 1 exit /b 1
copy /Y "%~dp0info.json" "%~dp0BepInEx\plugins\PopularityTest_Serp\info.json" >nul
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1"
if errorlevel 1 exit /b 1
echo PopularityTest built, installed and verified; APIShared 0.9.2 unchanged.
exit /b 0
