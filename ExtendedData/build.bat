@echo off
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
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\ExtendedData_Serp"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\ExtendedData_Serp"
set "STAGED_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\.ExtendedData_Serp.build"
set "LEGACY_TRAIL_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\CustomCustomTrail_Serp"
set "LEGACY_LORD_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\CustomLordUpload_Serp"
set "API_SHARED_DIR=%GAME_DIR%\BepInEx\plugins\APIShared_Serp"
if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

rem Never touch build or installation output while the game has plugin DLLs loaded.
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build und Installation abgebrochen: Stronghold Crusader Definitive Edition ist noch gestartet.
  echo Lokales Paket und installierter Mod wurden nicht veraendert.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if not exist "%MSBUILD%" (
  echo MSBuild wurde nicht gefunden: !MSBUILD!
  goto build_failed
)
if not exist "%GAME_DIR%\BepInEx\core\BepInEx.dll" (
  echo BepInEx wurde nicht gefunden: !GAME_DIR!\BepInEx\core\BepInEx.dll
  goto build_failed
)
if not exist "%PROJECT_DIR%info.json" (
  echo info.json wurde nicht gefunden.
  goto build_failed
)
if not exist "%API_SHARED_DIR%\APIShared.dll" (
  echo APIShared.dll wurde nicht gefunden: !API_SHARED_DIR!\APIShared.dll
  goto build_failed
)

if exist "%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%\SHCDESE.dll" (
  set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%"
) else if exist "%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%\SHCDESE.dll" (
  set "EXTENDER_DIR=%LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT%"
) else if exist "%GAME_SCRIPT_EXTENDER_DIR%\SHCDESE.dll" (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
) else (
  echo SHCDESE.dll wurde weder im lokalen Fork noch im Spiel gefunden.
  goto build_failed
)

echo Verwende Script Extender Referenzen:
echo !EXTENDER_DIR!
echo.

pushd "%PROJECT_DIR%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1" -GameDir "%GAME_DIR%"
if errorlevel 1 goto forbidden_source_popd
dotnet run --project ExtendedData.Tests -c Release
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%MSBUILD%" ExtendedData.Upload.Tests\ExtendedData.Upload.Tests.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%"
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%ExtendedData.Upload.Tests\bin\Debug\ExtendedData.Upload.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd
"%MSBUILD%" ExtendedData.JsonUpload.Tests\ExtendedData.JsonUpload.Tests.csproj /p:Configuration=Debug
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%ExtendedData.JsonUpload.Tests\bin\Debug\ExtendedData.JsonUpload.Tests.exe"
if not "%ERRORLEVEL%"=="0" goto build_failed_popd

rem Recreate the exact package so removed assets cannot survive an update.
if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
if errorlevel 1 goto package_failed_popd

"%MSBUILD%" ExtendedData.csproj /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%"
if errorlevel 1 goto build_failed_popd
popd

copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if errorlevel 1 goto package_failed
xcopy "%PROJECT_DIR%Examples" "%LOCAL_PLUGIN_DIR%\Examples\" /E /I /Q /Y
if errorlevel 1 goto package_failed

for %%F in (ExtendedData.dll ExtendedData.Core.dll info.json) do (
  if not exist "%LOCAL_PLUGIN_DIR%\%%F" (
    echo Paketdatei fehlt: %%F
    goto package_failed
  )
)
if not exist "%LOCAL_PLUGIN_DIR%\Patches\Assets\GUI\XAMLResources\HUD_ConfirmationPopup.xaml" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\Patches\Assets\GUI\XAMLResources\FRONT_EditorSetup.xaml" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\Locales\en-US.txt" goto package_failed
echo Installiere geprueftes Paket...
if exist "%STAGED_GAME_PLUGIN_DIR%\" rmdir /S /Q "%STAGED_GAME_PLUGIN_DIR%"
if errorlevel 1 goto copy_failed
xcopy "%LOCAL_PLUGIN_DIR%" "%STAGED_GAME_PLUGIN_DIR%\" /E /I /Q /Y
if errorlevel 1 goto copy_failed
rem Carry player-created lobby settings into the staged replacement package.
if exist "%GAME_PLUGIN_DIR%\LobbyModSettings\" (
  xcopy "%GAME_PLUGIN_DIR%\LobbyModSettings" "%STAGED_GAME_PLUGIN_DIR%\LobbyModSettings\" /E /I /Q /Y
  if errorlevel 1 goto copy_failed
)
if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"
if errorlevel 1 goto copy_failed
move /Y "%STAGED_GAME_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%" >nul
if errorlevel 1 goto copy_failed
if exist "%LEGACY_TRAIL_PLUGIN_DIR%\" rmdir /S /Q "%LEGACY_TRAIL_PLUGIN_DIR%"
if errorlevel 1 goto copy_failed
if exist "%LEGACY_LORD_PLUGIN_DIR%\" rmdir /S /Q "%LEGACY_LORD_PLUGIN_DIR%"
if errorlevel 1 goto copy_failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Release\Write-LocalBuildManifest.ps1" -ModName ExtendedData
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

:forbidden_source_popd
popd
echo.
echo ExtendedData Runtime-Preflight fehlgeschlagen.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:package_failed_popd
popd
:package_failed
echo.
echo Das lokale Plugin-Paket konnte nicht erzeugt werden.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:copy_failed
echo.
echo Installation fehlgeschlagen. Ist das Spiel noch gestartet?
if "%NO_PAUSE%"=="0" pause
exit /b 1
