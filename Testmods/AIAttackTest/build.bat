@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
rem The installed release is canonical; SHCDESE_EXTENDER_DIR is the only explicit override.
if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
set "PLUGIN_NAME=AIAttackTest_Serp"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "EXTENDER_DIR="
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

rem Never replace plugin files while the game has loaded them.
powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build and installation aborted: Stronghold Crusader Definitive Edition is still running.
  echo The local package and installed mod were not changed.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if not exist "%MSBUILD%" goto build_failed
if exist "%GAME_SCRIPT_EXTENDER_DIR%\SHCDESE.dll" (
  set "EXTENDER_DIR=%GAME_SCRIPT_EXTENDER_DIR%"
) else goto build_failed

rem Runtime dependency and Unity lifecycle preflight required for every build.
powershell.exe -NoProfile -Command "$codeFiles = Get-ChildItem -LiteralPath '%PROJECT_DIR%src' -Recurse -File -Include *.cs; $projectFiles = Get-ChildItem -LiteralPath '%PROJECT_DIR%' -File | Where-Object { $_.Extension -eq '.csproj' }; $runtimeFiles = @($codeFiles) + @($projectFiles); if ($runtimeFiles | Select-String -Pattern 'System\.Text\.Json|Newtonsoft\.Json|JavaScriptSerializer|System\.Web\.Extensions|DataContractJsonSerializer|JsonUtility') { exit 1 }; if ($codeFiles | Select-String -Pattern '\b(OnDestroy|OnDisable|OnApplicationQuit)\s*\(') { exit 1 }; exit 0"
if errorlevel 1 goto preflight_failed

if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
pushd "%PROJECT_DIR%"
"%MSBUILD%" tests\AIAttackTest.Tests.csproj /p:Configuration=Debug /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%tests\bin\AIAttackTest.Tests.exe"
if errorlevel 1 goto test_failed_popd
"%MSBUILD%" AIAttackTest.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
if errorlevel 1 goto build_failed_popd
popd

copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
xcopy "%PROJECT_DIR%Override" "%LOCAL_PLUGIN_DIR%\Override\" /E /I /Q /Y >nul
if errorlevel 1 goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\AIAttackTest.dll" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\Override\ScriptExtenderUI\AIAttackTestSettings.xaml" goto package_failed

if exist "%GAME_PLUGIN_DIR%\" (
  for /D %%D in ("%GAME_PLUGIN_DIR%\*") do (
    rmdir /S /Q "%%~fD"
    if errorlevel 1 goto copy_failed
  )
  for %%F in ("%GAME_PLUGIN_DIR%\*") do (
    if exist "%%~fF" if not exist "%%~fF\" (
      del /F /Q "%%~fF"
      if errorlevel 1 goto copy_failed
    )
  )
)
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto copy_failed

echo AI Attack Test checks passed, mod built and installed successfully.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:preflight_failed
echo Runtime JSON or Unity lifecycle preflight failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:test_failed_popd
popd
echo Tests failed. The mod was not built or installed.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:build_failed_popd
popd
:build_failed
echo Build failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:package_failed
echo Package validation failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1

:copy_failed
echo Installation failed. Is the game still running?
if "%NO_PAUSE%"=="0" pause
exit /b 1
