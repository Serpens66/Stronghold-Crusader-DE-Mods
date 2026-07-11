@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "EXTENDER_DIR="

if not exist "%MSBUILD%" (
  echo MSBuild wurde nicht gefunden:
  echo !MSBUILD!
  echo.
  pause
  exit /b 1
)

if not exist "%GAME_DIR%\BepInEx\core\BepInEx.dll" (
  echo BepInEx.dll wurde im Spielordner nicht gefunden:
  echo !GAME_DIR!\BepInEx\core\BepInEx.dll
  echo.
  pause
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
    pause
    exit /b 1
  )
) else (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
)

if not exist "%EXTENDER_DIR%\SHCDESE.dll" (
  echo SHCDESE.dll wurde nicht gefunden:
  echo !EXTENDER_DIR!\SHCDESE.dll
  echo.
  pause
  exit /b 1
)

echo Verwende Script Extender Referenzen:
echo !EXTENDER_DIR!
echo.

pushd "%PROJECT_DIR%"
"%MSBUILD%" ImprovedHunters.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd

echo.
if "%BUILD_EXIT_CODE%"=="0" (
  echo Build erfolgreich.
  echo Kopiere Plugin in den Spielordner...
  set "PLUGIN_NAME=ImprovedHunters_Serp"
  set "OLD_PLUGIN_NAME=HuntRabbits_Serp"
  set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\!PLUGIN_NAME!"
  set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!PLUGIN_NAME!"
  set "OLD_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!OLD_PLUGIN_NAME!"

  if not exist "!LOCAL_PLUGIN_DIR!\" (
    echo Lokaler Plugin-Ordner wurde nicht gefunden:
    echo !LOCAL_PLUGIN_DIR!
    goto copy_failed
  )

  if exist "!GAME_PLUGIN_DIR!\" (
    rmdir /S /Q "!GAME_PLUGIN_DIR!"
    if errorlevel 1 goto copy_failed
  )

  if exist "!OLD_GAME_PLUGIN_DIR!\" (
    rmdir /S /Q "!OLD_GAME_PLUGIN_DIR!"
    if errorlevel 1 goto copy_failed
  )

  xcopy "!LOCAL_PLUGIN_DIR!" "!GAME_PLUGIN_DIR!\" /E /I /Y
  if errorlevel 1 goto copy_failed
  echo Plugin kopiert.
) else (
  echo Build fehlgeschlagen. Exit Code: %BUILD_EXIT_CODE%
)
echo.
pause
exit /b %BUILD_EXIT_CODE%

:copy_failed
echo.
echo Kopieren fehlgeschlagen. Ist das Spiel noch gestartet?
echo Beende Stronghold Crusader Definitive Edition und starte build.bat erneut.
echo.
pause
exit /b 1
