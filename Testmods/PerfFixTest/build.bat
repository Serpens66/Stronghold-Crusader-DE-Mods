@echo off
setlocal EnableExtensions
rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
if defined SHCDE_BRANCH_PINNED goto :shcde_branch_selected
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0../../Shared\Tools\BranchBuild\Invoke-BranchBuild.ps1" -Repository "%~dp0../../." -Driver "Testmods/PerfFixTest/build.bat" %*
set "SHCDE_BRANCH_RESULT=%ERRORLEVEL%"
set "SHCDE_BRANCH_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "SHCDE_BRANCH_PAUSE=0"
if "%SHCDE_BRANCH_PAUSE%"=="1" pause
exit /b %SHCDE_BRANCH_RESULT%
:shcde_branch_selected
endlocal
@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
set "VERIFY_ONLY=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/verifyonly" set "VERIFY_ONLY=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START PerfFixTest
cd /d "%~dp0"
if errorlevel 1 exit /b 1
call :main
set "BUILD_DRIVER_RESULT=%ERRORLEVEL%"
cd /d "%BUILD_DRIVER_ORIGINAL_DIR%"
echo [%date% %time%] END PerfFixTest: exit code %BUILD_DRIVER_RESULT%
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b %BUILD_DRIVER_RESULT%

:main
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify.ps1"
if errorlevel 1 exit /b 1
if "%VERIFY_ONLY%"=="1" exit /b 0
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
"%MSBUILD%" "%~dp0PerfFixTest.csproj" /p:Configuration=Debug /nologo /verbosity:minimal
if errorlevel 1 exit /b 1
copy /Y "%~dp0info.json" "%~dp0BepInEx\plugins\PerfFixTest_Serp\info.json" >nul
if errorlevel 1 exit /b 1
if "%SHCDE_BRANCH_NOINSTALL%"=="1" goto :built
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }"
if errorlevel 1 goto :game_running
xcopy "%~dp0BepInEx\plugins\PerfFixTest_Serp" "%GAME_DIR%\BepInEx\plugins\PerfFixTest_Serp\" /E /I /Q /Y
if errorlevel 1 exit /b 1
echo PerfFixTest built and installed.
exit /b 0

:built
echo PerfFixTest built (not installed).
exit /b 0

:game_running
echo ERROR: Close Stronghold Crusader Definitive Edition before installing PerfFixTest.
exit /b 1
