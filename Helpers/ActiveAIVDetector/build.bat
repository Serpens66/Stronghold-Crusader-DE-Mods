@echo off
setlocal EnableExtensions
set "BUILD_DRIVER_NOPAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
echo [%date% %time%] START ActiveAIVDetector
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
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\Validation\Test-SharedBoundaries.ps1"
if errorlevel 1 exit /b 1
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "API_SHARED_DIR=%~dp0..\..\APIShared\BepInEx\plugins\APIShared_Serp"
rem The installed release is canonical; SHCDESE_EXTENDER_DIR is the explicit override.
if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
echo [%date% %time%] PowerShell checks / build step
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0..\.." -PackageDirectory "%API_SHARED_DIR%"
if errorlevel 1 exit /b 1
set "VANILLA_EXPORT_ROOT=%PROJECT_DIR%..\VanillaAICExporter\Exports"
set "EDITOR_VANILLA_AIV_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition - Castle & CPU Lord Editor\CrusaderCastleEditorUnity_Data\StreamingAssets\Villages"
set "CHAT10_TRACE_CONFIG=%PROJECT_DIR%Diagnostics\Chat10-Bow-Ridge-Trace.cfg"
set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
set "NO_PAUSE=0"
set "INSTALL_CHAT10_TRACE=0"
for %%A in (%*) do (
  if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
  if /I "%%~A"=="/trace" set "INSTALL_CHAT10_TRACE=1"
)

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

if not exist "%EXTENDER_DIR%\SHCDESE.dll" (
  echo SHCDESE.dll wurde nicht gefunden:
  echo !EXTENDER_DIR!\SHCDESE.dll
  echo.
  exit /b 1
)

if not exist "%API_SHARED_DIR%\APIShared.dll" (
  echo APIShared.dll wurde nicht gefunden:
  echo !API_SHARED_DIR!\APIShared.dll
  echo.
  exit /b 1
)

echo Verwende Script Extender Referenzen:
echo !EXTENDER_DIR!
echo.

pushd "%PROJECT_DIR%"
echo [%date% %time%] Compile projects
"%MSBUILD%" ActiveAIVDetector.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%"
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
echo [%date% %time%] Copy package files
  xcopy "!VANILLA_EXPORT_DIR!" "!LOCAL_PLUGIN_DIR!\VanillaAIC\" /E /I /Q /Y
  if errorlevel 1 goto copy_failed

  if exist "!EDITOR_VANILLA_AIV_DIR!\rat1.aivjson" (
    echo Aktualisiere Vanilla-AIV-Dateien aus dem offiziellen Editor:
    echo !EDITOR_VANILLA_AIV_DIR!
    if exist "!LOCAL_PLUGIN_DIR!\VanillaAIV\" rmdir /S /Q "!LOCAL_PLUGIN_DIR!\VanillaAIV"
    if errorlevel 1 goto copy_failed
echo [%date% %time%] Copy package files
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
    rem Replace only packaged asset directories; preserve captures and player data.
    for /D %%D in ("!GAME_PLUGIN_DIR!\*") do (
      if /I "%%~nxD"=="VanillaAIC" (
        rmdir /S /Q "%%~fD"
        if errorlevel 1 goto copy_failed
      )
      if /I "%%~nxD"=="VanillaAIV" (
        rmdir /S /Q "%%~fD"
        if errorlevel 1 goto copy_failed
      )
    )
    for %%F in ("!GAME_PLUGIN_DIR!\*") do (
      if exist "%%~fF" if not exist "%%~fF\" (
        del /F /Q "%%~fF"
        if errorlevel 1 goto copy_failed
      )
    )
  )
echo [%date% %time%] Copy package files
  xcopy "!LOCAL_PLUGIN_DIR!" "!GAME_PLUGIN_DIR!\" /E /I /Q /Y
  if errorlevel 1 goto copy_failed
  echo Plugin kopiert.

  if "!INSTALL_CHAT10_TRACE!"=="1" (
    if not exist "!CHAT10_TRACE_CONFIG!" (
      echo Chat-10-Trace-Konfiguration wurde nicht gefunden:
      echo !CHAT10_TRACE_CONFIG!
      goto copy_failed
    )
    echo Installiere explizit aktivierte Chat-10-Prebuild-Trace-Konfiguration...
echo [%date% %time%] Copy package files
    copy /Y "!CHAT10_TRACE_CONFIG!" "%GAME_DIR%\BepInEx\config\ActiveAIVDetector_Serp.cfg" >nul
    if errorlevel 1 goto copy_failed
    echo Chat-10-Prebuild-Trace aktiviert.
  )
  rem Internal helper projects are deliberately not release-enabled and therefore have no release provenance manifest.
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
