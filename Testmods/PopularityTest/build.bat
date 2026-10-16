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
