@echo off
setlocal EnableExtensions
set "MOD_DIR=%~dp0"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build und Installation abgebrochen: Das Spiel ist noch gestartet.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%MOD_DIR%Verify-SpectatorPerspective.ps1"
if errorlevel 1 goto failed
if not exist "%MSBUILD%" goto failed
"%MSBUILD%" "%MOD_DIR%SpectatorPerspectiveTest.csproj" /p:Configuration=Debug /p:GameDir="%GAME_DIR%"
if errorlevel 1 goto failed
if not exist "%MOD_DIR%BepInEx\plugins\SpectatorPerspectiveTest_Serp\SpectatorPerspectiveTest.dll" goto failed
xcopy "%MOD_DIR%BepInEx\plugins\SpectatorPerspectiveTest_Serp\*" "%GAME_DIR%\BepInEx\plugins\SpectatorPerspectiveTest_Serp\" /E /I /Q /Y
if errorlevel 1 goto failed
echo Build und Installation erfolgreich.
if "%NO_PAUSE%"=="0" pause
exit /b 0
:failed
echo Build oder Installation fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1
