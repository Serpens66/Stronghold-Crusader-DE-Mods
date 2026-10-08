@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\Validation\Test-SharedBoundaries.ps1"
if errorlevel 1 exit /b 1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\Validation\Test-UnitAccess.ps1"
if errorlevel 1 exit /b 1
setlocal EnableExtensions
set "OUTPOST_PROJECT=%~dp0"
set "NOPAUSE=0"
set "INSTALL_ARGUMENT="
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "INSTALL_ARGUMENT=-NoInstall"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NOPAUSE=1"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%OUTPOST_PROJECT%Build-OutpostTest.ps1" %INSTALL_ARGUMENT%
set "RESULT=%ERRORLEVEL%"
if "%NOPAUSE%"=="0" pause
exit /b %RESULT%
