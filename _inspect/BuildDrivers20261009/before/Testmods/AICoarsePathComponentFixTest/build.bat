@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\Validation\Test-SharedBoundaries.ps1"
if errorlevel 1 exit /b 1
setlocal EnableExtensions
set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
if defined SHCDESE_EXTENDER_DIR set "EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "API_SHARED_DIR=%~dp0..\..\APIShared\BepInEx\plugins\APIShared_Serp"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0..\.." -PackageDirectory "%API_SHARED_DIR%"
if errorlevel 1 exit /b 1
set "BUGFIX_DIR=%GAME_DIR%\BepInEx\plugins\BugfixesAndQoL_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\AICoarsePathComponentFixTest_Serp"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\AICoarsePathComponentFixTest_Serp"
set "NO_PAUSE=0"
set "NO_INSTALL=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
for %%A in (%*) do if /I "%%~A"=="/noinstall" set "NO_INSTALL=1"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%verify.ps1"
if errorlevel 1 goto failed
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 goto failed
if not exist "%MSBUILD%" goto failed
if not exist "%EXTENDER_DIR%\SHCDESE.dll" goto failed
if not exist "%API_SHARED_DIR%\APIShared.dll" goto failed
if not exist "%BUGFIX_DIR%\BugfixesAndQoL.dll" goto failed

pushd "%PROJECT_DIR%"
"%MSBUILD%" AICoarsePathComponentFixTest.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%" /p:BugfixDir="%BUGFIX_DIR%"
if errorlevel 1 goto failed_popd
popd
copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if errorlevel 1 goto failed
if "%NO_INSTALL%"=="1" (
  echo AI Coarse Path Component Fix Test built locally; installation skipped.
  if "%NO_PAUSE%"=="0" pause
  exit /b 0
)
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto failed
echo AI Coarse Path Component Fix Test built and installed.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:failed_popd
popd
:failed
echo AI Coarse Path Component Fix Test build or installation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
