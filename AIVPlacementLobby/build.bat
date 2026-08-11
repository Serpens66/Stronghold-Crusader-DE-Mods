@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "POWERSHELL=%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "EDITOR_VANILLA_AIV_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition - Castle & CPU Lord Editor\CrusaderCastleEditorUnity_Data\StreamingAssets\Villages"
set "PACKAGE_MANIFEST=%PROJECT_DIR%info.json"
set "PACKAGE_VERIFIER=%PROJECT_DIR%Verify-Package.ps1"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\AIVPlacementLobby_Serp"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\AIVPlacementLobby_Serp"
set "STAGED_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\.AIVPlacementLobby_Serp.build"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

if not exist "%MSBUILD%" (
  echo MSBuild wurde nicht gefunden:
  echo !MSBUILD!
  goto build_failed
)

if not exist "%POWERSHELL%" (
  echo Windows PowerShell wurde nicht gefunden:
  echo !POWERSHELL!
  goto build_failed
)

if not exist "%GAME_DIR%\BepInEx\core\BepInEx.dll" (
  echo BepInEx.dll wurde im Spielordner nicht gefunden:
  echo !GAME_DIR!\BepInEx\core\BepInEx.dll
  goto build_failed
)

if not exist "%PACKAGE_MANIFEST%" (
  echo Das kanonische Plugin-Manifest wurde nicht gefunden:
  echo !PACKAGE_MANIFEST!
  goto build_failed
)

if not exist "%PACKAGE_VERIFIER%" (
  echo Die Paketpruefung wurde nicht gefunden:
  echo !PACKAGE_VERIFIER!
  goto build_failed
)

if not exist "%EDITOR_VANILLA_AIV_DIR%\rat1.aivjson" (
  echo Die offiziellen Vanilla-AIV-Dateien wurden nicht gefunden:
  echo !EDITOR_VANILLA_AIV_DIR!
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

rem Recreate the local package so stale files can never leak into an installation.
if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
if errorlevel 1 goto package_failed_popd

"%MSBUILD%" AIVPlacementLobby.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd

if not "%BUILD_EXIT_CODE%"=="0" goto build_failed

copy /Y "%PACKAGE_MANIFEST%" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if errorlevel 1 goto package_failed

echo Aktualisiere Vanilla-AIV-Dateien aus dem offiziellen Editor...
xcopy "%EDITOR_VANILLA_AIV_DIR%\*.aivjson" "%LOCAL_PLUGIN_DIR%\VanillaAIV\" /I /Q /Y
if errorlevel 1 goto package_failed

"%POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -File "%PACKAGE_VERIFIER%" -PackageRoot "%LOCAL_PLUGIN_DIR%"
if errorlevel 1 goto package_failed

echo Erzeuge und pruefe vollstaendige Installation...
if exist "%STAGED_GAME_PLUGIN_DIR%\" rmdir /S /Q "%STAGED_GAME_PLUGIN_DIR%"
if errorlevel 1 goto copy_failed
xcopy "%LOCAL_PLUGIN_DIR%" "%STAGED_GAME_PLUGIN_DIR%\" /E /I /Q /Y
if errorlevel 1 goto copy_failed
"%POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -File "%PACKAGE_VERIFIER%" -PackageRoot "%LOCAL_PLUGIN_DIR%" -InstalledRoot "%STAGED_GAME_PLUGIN_DIR%"
if errorlevel 1 goto copy_failed

rem Carry player-created lobby settings into the verified replacement package.
if exist "%GAME_PLUGIN_DIR%\LobbyModSettings\" (
  xcopy "%GAME_PLUGIN_DIR%\LobbyModSettings" "%STAGED_GAME_PLUGIN_DIR%\LobbyModSettings\" /E /I /Q /Y
  if errorlevel 1 goto copy_failed
)
if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"
if errorlevel 1 goto copy_failed
move /Y "%STAGED_GAME_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%" >nul
if errorlevel 1 goto copy_failed
"%POWERSHELL%" -NoProfile -ExecutionPolicy Bypass -File "%PACKAGE_VERIFIER%" -PackageRoot "%LOCAL_PLUGIN_DIR%" -InstalledRoot "%GAME_PLUGIN_DIR%"
if errorlevel 1 goto copy_failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Release\Write-LocalBuildManifest.ps1" -ModName AIVPlacementLobby
if errorlevel 1 goto package_failed

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

:package_failed_popd
popd

:package_failed
echo.
echo Das lokale Plugin-Paket konnte nicht vollstaendig erzeugt werden.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:copy_failed
echo.
echo Kopieren fehlgeschlagen. Ist das Spiel noch gestartet?
if "%NO_PAUSE%"=="0" pause
exit /b 1
