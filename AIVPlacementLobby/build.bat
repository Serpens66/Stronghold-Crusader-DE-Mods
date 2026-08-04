@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "EDITOR_VANILLA_AIV_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition - Castle & CPU Lord Editor\CrusaderCastleEditorUnity_Data\StreamingAssets\Villages"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

if not exist "%MSBUILD%" (
  echo MSBuild wurde nicht gefunden:
  echo !MSBUILD!
  goto build_failed
)

if not exist "%GAME_DIR%\BepInEx\core\BepInEx.dll" (
  echo BepInEx.dll wurde im Spielordner nicht gefunden:
  echo !GAME_DIR!\BepInEx\core\BepInEx.dll
  goto build_failed
)

if exist "%LOCAL_SCRIPT_EXTENDER_ROOT%\" (
  if exist "%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%\SHCDESE.dll" (
    set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%"
  ) else if exist "%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%\SHCDESE.dll" (
    set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%"
  ) else (
    echo Der lokale Script Extender wurde gefunden, aber SHCDESE.dll fehlt.
    goto build_failed
  )
) else (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
)

echo Verwende Script Extender Referenzen:
echo !EXTENDER_DIR!
echo.

pushd "%PROJECT_DIR%"
dotnet run --project AIVPlacementLobby.Tests -c Release
if errorlevel 1 goto build_failed_popd

"%MSBUILD%" AIVPlacementLobby.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd

if not "%BUILD_EXIT_CODE%"=="0" goto build_failed

set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\AIVPlacementLobby_Serp"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\AIVPlacementLobby_Serp"

if not exist "%EDITOR_VANILLA_AIV_DIR%\rat1.aivjson" (
  echo Die offiziellen Vanilla-AIV-Dateien wurden nicht gefunden:
  echo !EDITOR_VANILLA_AIV_DIR!
  goto build_failed
)

echo Aktualisiere Vanilla-AIV-Dateien aus dem offiziellen Editor...
if exist "%LOCAL_PLUGIN_DIR%\VanillaAIV\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%\VanillaAIV"
if errorlevel 1 goto copy_failed
xcopy "%EDITOR_VANILLA_AIV_DIR%\*.aivjson" "%LOCAL_PLUGIN_DIR%\VanillaAIV\" /I /Q /Y
if errorlevel 1 goto copy_failed

echo Kopiere Plugin in den Spielordner...
if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"
if errorlevel 1 goto copy_failed
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y
if errorlevel 1 goto copy_failed

echo.
echo Build, Tests und Installation erfolgreich.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:build_failed_popd
popd

:build_failed
echo.
echo Build oder Tests fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:copy_failed
echo.
echo Kopieren fehlgeschlagen. Ist das Spiel noch gestartet?
if "%NO_PAUSE%"=="0" pause
exit /b 1
