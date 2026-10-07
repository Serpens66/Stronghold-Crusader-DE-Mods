@echo off
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
if defined SHCDESE_EXTENDER_DIR set "EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "API_SHARED_DIR=%GAME_DIR%\BepInEx\plugins\APIShared_Serp"
if exist "%GAME_DIR%\BepInEx\plugins\SerpsMods_Serp\Infrastructure\APIShared_Serp\APIShared.dll" set "API_SHARED_DIR=%GAME_DIR%\BepInEx\plugins\SerpsMods_Serp\Infrastructure\APIShared_Serp"
if defined APISHARED_DIR set "API_SHARED_DIR=%APISHARED_DIR%"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }"
if errorlevel 1 goto running
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%verify.ps1" -RunTests -ExtenderDir "%EXTENDER_DIR%" -ApiSharedDll "%API_SHARED_DIR%\APIShared.dll"
if errorlevel 1 goto failed
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\..\Shared\Test-UnitAccess.ps1"
if errorlevel 1 goto failed
"%MSBUILD%" "%PROJECT_DIR%GatehouseLivingCaptureTest.csproj" /t:Rebuild /p:Configuration=Debug /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%" /nologo /verbosity:minimal
if errorlevel 1 goto failed
set "LOCAL_DIR=%PROJECT_DIR%BepInEx\plugins\GatehouseLivingCaptureTest_Serp"
set "INSTALL_DIR=%GAME_DIR%\BepInEx\plugins\GatehouseLivingCaptureTest_Serp"
if not exist "%LOCAL_DIR%\GatehouseLivingCaptureTest.dll" goto failed
if not exist "%LOCAL_DIR%\info.json" goto failed
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
copy /Y "%LOCAL_DIR%\GatehouseLivingCaptureTest.dll" "%INSTALL_DIR%\GatehouseLivingCaptureTest.dll" >nul
if errorlevel 1 goto failed
copy /Y "%LOCAL_DIR%\info.json" "%INSTALL_DIR%\info.json" >nul
if errorlevel 1 goto failed
if exist "%LOCAL_DIR%\GatehouseLivingCaptureTest.pdb" copy /Y "%LOCAL_DIR%\GatehouseLivingCaptureTest.pdb" "%INSTALL_DIR%\GatehouseLivingCaptureTest.pdb" >nul
if errorlevel 1 goto failed
echo Gatehouse Living Capture Test built, tested and installed successfully.
if "%NO_PAUSE%"=="0" pause
exit /b 0
:running
echo Build aborted: Stronghold Crusader Definitive Edition is running.
if "%NO_PAUSE%"=="0" pause
exit /b 1
:failed
echo Gatehouse Living Capture Test build, verification or installation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
