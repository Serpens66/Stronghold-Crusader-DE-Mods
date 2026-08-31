@echo off
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%PROJECT_DIR%..\shcde-script-extender\src\SHCDESE.BepInEx\bin\net481"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
    echo Build abgebrochen: Das Spiel ist noch gestartet.
    if "%NO_PAUSE%"=="0" pause
    exit /b 1
)
if not exist "%MSBUILD%" (
    echo MSBuild wurde nicht gefunden.
    if "%NO_PAUSE%"=="0" pause
    exit /b 1
)
if not exist "%EXTENDER_DIR%\SHCDESE.dll" set "EXTENDER_DIR=%PROJECT_DIR%..\shcde-script-extender\mod_output\000shcdese"
if not exist "%EXTENDER_DIR%\SHCDESE.dll" (
    echo SHCDESE.dll wurde nicht gefunden.
    if "%NO_PAUSE%"=="0" pause
    exit /b 1
)
pushd "%PROJECT_DIR%"
"%MSBUILD%" ExtremePowers.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
set "BUILD_EXIT_CODE=%ERRORLEVEL%"
popd
if not "%BUILD_EXIT_CODE%"=="0" goto done
set "LOCAL_BUILD_DIR=%PROJECT_DIR%bin\Debug"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\ExtremePowers_Serp"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\ExtremePowers_Serp"
if not exist "%LOCAL_PLUGIN_DIR%" mkdir "%LOCAL_PLUGIN_DIR%"
copy /Y "%LOCAL_BUILD_DIR%\ExtremePowers.API.dll" "%LOCAL_PLUGIN_DIR%\ExtremePowers.API.dll" >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
copy /Y "%LOCAL_BUILD_DIR%\ExtremePowers.API.pdb" "%LOCAL_PLUGIN_DIR%\ExtremePowers.API.pdb" >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
copy /Y "%LOCAL_BUILD_DIR%\ExtremePowers.dll" "%LOCAL_PLUGIN_DIR%\ExtremePowers.dll" >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
copy /Y "%LOCAL_BUILD_DIR%\ExtremePowers.pdb" "%LOCAL_PLUGIN_DIR%\ExtremePowers.pdb" >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
copy /Y "%LOCAL_BUILD_DIR%\info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
xcopy "%LOCAL_BUILD_DIR%\Locales" "%LOCAL_PLUGIN_DIR%\Locales\" /E /I /Y >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
xcopy "%LOCAL_BUILD_DIR%\Override" "%LOCAL_PLUGIN_DIR%\Override\" /E /I /Y >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
xcopy "%LOCAL_BUILD_DIR%\Patches" "%LOCAL_PLUGIN_DIR%\Patches\" /E /I /Y >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
if not exist "%GAME_PLUGIN_DIR%" mkdir "%GAME_PLUGIN_DIR%"
copy /Y "%LOCAL_PLUGIN_DIR%\ExtremePowers.API.dll" "%GAME_PLUGIN_DIR%\ExtremePowers.API.dll" >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
copy /Y "%LOCAL_PLUGIN_DIR%\ExtremePowers.API.pdb" "%GAME_PLUGIN_DIR%\ExtremePowers.API.pdb" >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
copy /Y "%LOCAL_PLUGIN_DIR%\ExtremePowers.dll" "%GAME_PLUGIN_DIR%\ExtremePowers.dll" >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
copy /Y "%LOCAL_PLUGIN_DIR%\ExtremePowers.pdb" "%GAME_PLUGIN_DIR%\ExtremePowers.pdb" >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
copy /Y "%LOCAL_PLUGIN_DIR%\info.json" "%GAME_PLUGIN_DIR%\info.json" >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
xcopy "%LOCAL_PLUGIN_DIR%\Locales" "%GAME_PLUGIN_DIR%\Locales\" /E /I /Y >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
xcopy "%LOCAL_PLUGIN_DIR%\Override" "%GAME_PLUGIN_DIR%\Override\" /E /I /Y >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
xcopy "%LOCAL_PLUGIN_DIR%\Patches" "%GAME_PLUGIN_DIR%\Patches\" /E /I /Y >nul
if errorlevel 1 set "BUILD_EXIT_CODE=1"
if not exist "%GAME_PLUGIN_DIR%\ExtremePowers.API.dll" set "BUILD_EXIT_CODE=1"
if not exist "%GAME_PLUGIN_DIR%\ExtremePowers.dll" set "BUILD_EXIT_CODE=1"
:done
if "%BUILD_EXIT_CODE%"=="0" echo Build und Installation erfolgreich.
if "%NO_PAUSE%"=="0" pause
exit /b %BUILD_EXIT_CODE%
