@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "VANILLA_EXPORT_ROOT=%PROJECT_DIR%..\VanillaAICExporter\Exports"
set "EDITOR_VANILLA_AIV_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition - Castle & CPU Lord Editor\CrusaderCastleEditorUnity_Data\StreamingAssets\Villages"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
if /I "%~1"=="/nopause" set "NO_PAUSE=1"

if not exist "%MSBUILD%" (
  echo MSBuild wurde nicht gefunden:
  echo !MSBUILD!
  echo.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if not exist "%GAME_DIR%\BepInEx\core\BepInEx.dll" (
  echo BepInEx.dll wurde im Spielordner nicht gefunden:
  echo !GAME_DIR!\BepInEx\core\BepInEx.dll
  echo.
  if "%NO_PAUSE%"=="0" pause
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
    if "%NO_PAUSE%"=="0" pause
    exit /b 1
  )
) else (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
)

if not exist "%EXTENDER_DIR%\SHCDESE.dll" (
  echo SHCDESE.dll wurde nicht gefunden:
  echo !EXTENDER_DIR!\SHCDESE.dll
  echo.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

echo Verwende Script Extender Referenzen:
echo !EXTENDER_DIR!
echo.

pushd "%PROJECT_DIR%"
"%MSBUILD%" ActiveAIVDetector.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd

echo.
if "%BUILD_EXIT_CODE%"=="0" (
  echo Build erfolgreich.
  set "PLUGIN_NAME=ActiveAIVDetector_Serp"
  set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\!PLUGIN_NAME!"
  set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!PLUGIN_NAME!"
  set "VANILLA_EXPORT_DIR="

  if not exist "!LOCAL_PLUGIN_DIR!\" (
    echo Lokaler Plugin-Ordner wurde nicht gefunden:
    echo !LOCAL_PLUGIN_DIR!
    goto copy_failed
  )

  if not exist "!VANILLA_EXPORT_ROOT!\" (
    echo Vanilla-AIC-Exportordner wurde nicht gefunden:
    echo !VANILLA_EXPORT_ROOT!
    goto copy_failed
  )
  for /F "delims=" %%D in ('dir /B /AD /O-D "!VANILLA_EXPORT_ROOT!"') do (
    if not defined VANILLA_EXPORT_DIR set "VANILLA_EXPORT_DIR=!VANILLA_EXPORT_ROOT!\%%D"
  )
  if not defined VANILLA_EXPORT_DIR (
    echo Kein Vanilla-AIC-Export wurde unter !VANILLA_EXPORT_ROOT! gefunden.
    goto copy_failed
  )
  if not exist "!VANILLA_EXPORT_DIR!\manifest.json" (
    echo Der neueste Vanilla-AIC-Export besitzt kein manifest.json:
    echo !VANILLA_EXPORT_DIR!
    goto copy_failed
  )

  echo Buendle Vanilla-AIC-Export:
  echo !VANILLA_EXPORT_DIR!
  if exist "!LOCAL_PLUGIN_DIR!\VanillaAIC\" rmdir /S /Q "!LOCAL_PLUGIN_DIR!\VanillaAIC"
  if errorlevel 1 goto copy_failed
  xcopy "!VANILLA_EXPORT_DIR!" "!LOCAL_PLUGIN_DIR!\VanillaAIC\" /E /I /Q /Y
  if errorlevel 1 goto copy_failed

  if exist "!EDITOR_VANILLA_AIV_DIR!\rat1.aivjson" (
    echo Aktualisiere Vanilla-AIV-Dateien aus dem offiziellen Editor:
    echo !EDITOR_VANILLA_AIV_DIR!
    if exist "!LOCAL_PLUGIN_DIR!\VanillaAIV\" rmdir /S /Q "!LOCAL_PLUGIN_DIR!\VanillaAIV"
    if errorlevel 1 goto copy_failed
    xcopy "!EDITOR_VANILLA_AIV_DIR!\*.aivjson" "!LOCAL_PLUGIN_DIR!\VanillaAIV\" /I /Q /Y
    if errorlevel 1 goto copy_failed
  ) else (
    echo Offizieller Editor nicht gefunden; verwende bereits gebuendelte Vanilla-AIV-Dateien.
    if not exist "!LOCAL_PLUGIN_DIR!\VanillaAIV\rat1.aivjson" (
      echo Weder Editor-Quelle noch gebuendelte Vanilla-AIV-Dateien wurden gefunden.
      goto copy_failed
    )
  )

  echo Kopiere Plugin in den Spielordner...
  if exist "!GAME_PLUGIN_DIR!\" (
    rmdir /S /Q "!GAME_PLUGIN_DIR!"
    if errorlevel 1 goto copy_failed
  )
  xcopy "!LOCAL_PLUGIN_DIR!" "!GAME_PLUGIN_DIR!\" /E /I /Q /Y
  if errorlevel 1 goto copy_failed
  echo Plugin kopiert.
) else (
  echo Build fehlgeschlagen. Exit Code: %BUILD_EXIT_CODE%
)
echo.
if "%NO_PAUSE%"=="0" pause
exit /b %BUILD_EXIT_CODE%

:copy_failed
echo.
echo Kopieren fehlgeschlagen. Ist das Spiel noch gestartet?
echo Beende Stronghold Crusader Definitive Edition und starte build.bat erneut.
echo.
if "%NO_PAUSE%"=="0" pause
exit /b 1
