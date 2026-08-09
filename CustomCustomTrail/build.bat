@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\CustomCustomTrail_Serp"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\CustomCustomTrail_Serp"
set "STAGED_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\.CustomCustomTrail_Serp.build"
set "LEGACY_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\CoopTrailReplacer_Serp"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

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
dotnet run --project CustomCustomTrail.Tests -c Release
if errorlevel 1 goto build_failed_popd

rem Recreate the exact package so removed assets cannot survive an update.
if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
if errorlevel 1 goto package_failed_popd

"%MSBUILD%" CustomCustomTrail.csproj /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
popd

copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if errorlevel 1 goto package_failed
copy /Y "%PROJECT_DIR%README.md" "%LOCAL_PLUGIN_DIR%\README.md" >nul
if errorlevel 1 goto package_failed
xcopy "%PROJECT_DIR%CoopTrails" "%LOCAL_PLUGIN_DIR%\CoopTrails\" /E /I /Q /Y
if errorlevel 1 goto package_failed
xcopy "%PROJECT_DIR%Examples" "%LOCAL_PLUGIN_DIR%\Examples\" /E /I /Q /Y
if errorlevel 1 goto package_failed

for %%F in (CustomCustomTrail.dll CustomCustomTrail.Core.dll info.json README.md) do (
  if not exist "%LOCAL_PLUGIN_DIR%\%%F" (
    echo Paketdatei fehlt: %%F
    goto package_failed
  )
)
for %%T in (Trail1 Trail2 Trail3 Trail4) do (
  if not exist "%LOCAL_PLUGIN_DIR%\CoopTrails\%%T\" (
    echo Paketordner fehlt: CoopTrails\%%T
    goto package_failed
  )
)

echo Installiere geprueftes Paket...
if exist "%STAGED_GAME_PLUGIN_DIR%\" rmdir /S /Q "%STAGED_GAME_PLUGIN_DIR%"
if errorlevel 1 goto copy_failed
xcopy "%LOCAL_PLUGIN_DIR%" "%STAGED_GAME_PLUGIN_DIR%\" /E /I /Q /Y
if errorlevel 1 goto copy_failed
if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"
if errorlevel 1 goto copy_failed
move /Y "%STAGED_GAME_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%" >nul
if errorlevel 1 goto copy_failed
rem Remove the former installation so BepInEx cannot load both plugin identities.
if exist "%LEGACY_GAME_PLUGIN_DIR%\" rmdir /S /Q "%LEGACY_GAME_PLUGIN_DIR%"
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
