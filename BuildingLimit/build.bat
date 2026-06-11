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
"%MSBUILD%" BuildingLimit.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd

echo.
if "%BUILD_EXIT_CODE%"=="0" (
  echo Build erfolgreich.
  echo Kopiere Plugin in den Spielordner...
  if not exist "%GAME_DIR%\BepInEx\plugins\BuildingLimit" mkdir "%GAME_DIR%\BepInEx\plugins\BuildingLimit"
  if not exist "%GAME_DIR%\BepInEx\plugins\BuildingLimit\Override\ScriptExtenderUI" mkdir "%GAME_DIR%\BepInEx\plugins\BuildingLimit\Override\ScriptExtenderUI"
  if not exist "%GAME_DIR%\BepInEx\plugins\BuildingLimit\Patches\Assets\GUI\XAML" mkdir "%GAME_DIR%\BepInEx\plugins\BuildingLimit\Patches\Assets\GUI\XAML"

  copy /Y "%PROJECT_DIR%BepInEx\plugins\BuildingLimit\BuildingLimit.dll" "%GAME_DIR%\BepInEx\plugins\BuildingLimit\BuildingLimit.dll"
  if errorlevel 1 goto copy_failed
  copy /Y "%PROJECT_DIR%BepInEx\plugins\BuildingLimit\BuildingLimit.pdb" "%GAME_DIR%\BepInEx\plugins\BuildingLimit\BuildingLimit.pdb"
  if errorlevel 1 goto copy_failed
  copy /Y "%PROJECT_DIR%BepInEx\plugins\BuildingLimit\info.json" "%GAME_DIR%\BepInEx\plugins\BuildingLimit\info.json"
  if errorlevel 1 goto copy_failed
  copy /Y "%PROJECT_DIR%BepInEx\plugins\BuildingLimit\Override\ScriptExtenderUI\BuildingLimitSettings.xaml" "%GAME_DIR%\BepInEx\plugins\BuildingLimit\Override\ScriptExtenderUI\BuildingLimitSettings.xaml"
  if errorlevel 1 goto copy_failed
  copy /Y "%PROJECT_DIR%BepInEx\plugins\BuildingLimit\Patches\Assets\GUI\XAML\MainHUD.xaml" "%GAME_DIR%\BepInEx\plugins\BuildingLimit\Patches\Assets\GUI\XAML\MainHUD.xaml"
  if errorlevel 1 goto copy_failed

  if exist "%GAME_DIR%\BepInEx\plugins\BuildingLimit\ScriptExtenderUI\BuildingLimitSettings.xaml" del "%GAME_DIR%\BepInEx\plugins\BuildingLimit\ScriptExtenderUI\BuildingLimitSettings.xaml"
  if exist "%GAME_DIR%\BepInEx\plugins\BuildingLimit\Patches\Assets\GUI\XAMLResources\HUD_Buildings.xaml" del "%GAME_DIR%\BepInEx\plugins\BuildingLimit\Patches\Assets\GUI\XAMLResources\HUD_Buildings.xaml"
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

