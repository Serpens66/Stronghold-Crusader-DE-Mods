@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "PROJECT_ROOT=%~dp0."
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "GIT_BASH=C:\Program Files\Git\bin\bash.exe"
set "PLUGIN_DLL=%PROJECT_DIR%BepInEx\plugins\LinuxModding_Serp\LinuxModding.dll"
set "PROBE_PROJECT=%PROJECT_DIR%tests\DetourProbe\DetourProbe.csproj"
set "PROBE_EXE=%PROJECT_DIR%tests\DetourProbe\bin\Debug\LinuxModding.DetourProbe.exe"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

if not exist "%MSBUILD%" (
  echo MSBuild wurde nicht gefunden: !MSBUILD!
  goto failed
)
if not exist "%GIT_BASH%" (
  echo Git Bash wurde nicht gefunden: !GIT_BASH!
  goto failed
)

call "%PROJECT_DIR%build.bat" /nopause
if errorlevel 1 goto failed

"%MSBUILD%" "%PROBE_PROJECT%" /p:Configuration=Debug
if errorlevel 1 goto failed

"%PROBE_EXE%" "%GAME_DIR%" "%PLUGIN_DLL%"
if errorlevel 1 goto failed

"%GIT_BASH%" "%PROJECT_DIR%tests\windows-launcher-tests.sh" "%PROJECT_ROOT%"
if errorlevel 1 goto failed

echo.
echo Alle Windows-Tests waren erfolgreich.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:failed
echo.
echo Windows-Testlauf fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1
