@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START BuildingCosts
cd /d "%~dp0"
if errorlevel 1 goto :build_driver_directory_failed
rem The outer driver owns the pause, including failures before compilation.
call :build_driver_main %* /nopause
set "BUILD_DRIVER_RESULT=%ERRORLEVEL%"
cd /d "%BUILD_DRIVER_ORIGINAL_DIR%"
echo [%date% %time%] END: exit code %BUILD_DRIVER_RESULT%
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b %BUILD_DRIVER_RESULT%

:build_driver_directory_failed
echo ERROR: Cannot enter the build directory "%~dp0".
if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
exit /b 1

:build_driver_main
echo [%date% %time%] Workspace source and runtime preflight
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-SharedBoundaries.ps1"
if errorlevel 1 exit /b 1
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
rem The installed release is canonical; SHCDESE_EXTENDER_DIR is the explicit override.
if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

rem Never touch build or installation output while the game has plugin DLLs loaded.
echo [%date% %time%] Check that the game is closed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build und Installation abgebrochen: Stronghold Crusader Definitive Edition ist noch gestartet.
  echo Lokales Paket und installierter Mod wurden nicht veraendert.
  exit /b 1
)

if not exist "%MSBUILD%" (
  echo MSBuild wurde nicht gefunden:
  echo !MSBUILD!
  echo.
  exit /b 1
)

if not exist "%GAME_DIR%\BepInEx\core\BepInEx.dll" (
  echo BepInEx.dll wurde im Spielordner nicht gefunden:
  echo !GAME_DIR!\BepInEx\core\BepInEx.dll
  echo.
  exit /b 1
)

if exist "%LOCAL_SCRIPT_EXTENDER_ROOT%\" (
  if exist "%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%\SHCDESE.dll" (
    set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%"
  ) else if exist "%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%\SHCDESE.dll" (
    set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%"
  ) else (
    echo Lokaler Script Extender Nebenordner wurde gefunden:
    echo !LOCAL_SCRIPT_EXTENDER_ROOT!
    echo.
    echo Aber es wurde keine lokale SHCDESE.dll gefunden.
    echo Baue zuerst ..\shcde-script-extender\build.bat oder entferne den Nebenordner,
    echo wenn gegen die installierte Spiel-DLL kompiliert werden soll.
    echo.
    exit /b 1
  )
) else (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
)

if not exist "%EXTENDER_DIR%\SHCDESE.dll" (
  echo SHCDESE.dll wurde nicht gefunden:
  echo !EXTENDER_DIR!\SHCDESE.dll
  echo.
  exit /b 1
)

echo Verwende Script Extender Referenzen:
echo !EXTENDER_DIR!
echo.

pushd "%PROJECT_DIR%"
echo [%date% %time%] Compile projects
"%MSBUILD%" BuildingCosts.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd

echo.
if "%BUILD_EXIT_CODE%"=="0" (
  echo Build erfolgreich.
  echo Kopiere Plugin in den Spielordner...
  set "PLUGIN_NAME=BuildingCosts_Serp"
  set "OLD_PLUGIN_NAME=BuildingCosts"
  set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\!PLUGIN_NAME!"
  set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!PLUGIN_NAME!"
  set "OLD_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!OLD_PLUGIN_NAME!"

  if not exist "!LOCAL_PLUGIN_DIR!\" (
    echo Lokaler Plugin-Ordner wurde nicht gefunden:
    echo !LOCAL_PLUGIN_DIR!
    goto copy_failed
  )

  if exist "!GAME_PLUGIN_DIR!\" (
    for /D %%D in ("!GAME_PLUGIN_DIR!\*") do (
      if /I not "%%~nxD"=="LobbyModSettings" (
        rmdir /S /Q "%%~fD"
        if errorlevel 1 goto copy_failed
      )
    )
    for %%F in ("!GAME_PLUGIN_DIR!\*") do (
      if exist "%%~fF" (
        if not exist "%%~fF\" (
          del /F /Q "%%~fF"
          if errorlevel 1 goto copy_failed
        )
      )
    )
  )
echo [%date% %time%] Copy package files
  xcopy "!LOCAL_PLUGIN_DIR!" "!GAME_PLUGIN_DIR!\" /E /I /Y
  if errorlevel 1 goto copy_failed
  if exist "!OLD_GAME_PLUGIN_DIR!\" (
    rem Remove legacy packaged files but retain any player-created lobby settings.
    for /D %%D in ("!OLD_GAME_PLUGIN_DIR!\*") do (
      if /I not "%%~nxD"=="LobbyModSettings" (
        rmdir /S /Q "%%~fD"
        if errorlevel 1 goto copy_failed
      )
    )
    for %%F in ("!OLD_GAME_PLUGIN_DIR!\*") do (
      if exist "%%~fF" if not exist "%%~fF\" (
        del /F /Q "%%~fF"
        if errorlevel 1 goto copy_failed
      )
    )
  )

echo [%date% %time%] PowerShell checks / build step
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName BuildingCosts
  if errorlevel 1 goto copy_failed

  echo Plugin kopiert.
) else (
  echo Build fehlgeschlagen. Exit Code: %BUILD_EXIT_CODE%
)
echo.
exit /b %BUILD_EXIT_CODE%

:copy_failed
echo.
echo Kopieren fehlgeschlagen. Ist das Spiel noch gestartet?
echo Beende Stronghold Crusader Definitive Edition und starte build.bat erneut.
echo.
exit /b 1
