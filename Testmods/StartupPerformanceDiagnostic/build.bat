@echo off
setlocal EnableExtensions

set "PROJECT_DIR=%~dp0"
set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
set "PATCHER_NAME=StartupPerformanceDiagnostic.Patcher.dll"
set "PLUGIN_NAME=StartupPerformanceDiagnostic_Serp"
set "LOCAL_PATCHER_DIR=%PROJECT_DIR%BepInEx\patchers"
set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
set "GAME_PATCHER=%GAME_DIR%\BepInEx\patchers\%PATCHER_NAME%"
set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
set "NO_PAUSE=0"
for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"

powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
if errorlevel 1 (
  echo Build and installation aborted: Stronghold Crusader Definitive Edition is still running.
  if "%NO_PAUSE%"=="0" pause
  exit /b 1
)

if not exist "%MSBUILD%" goto build_failed
if not exist "%GAME_DIR%\BepInEx\core\BepInEx.dll" goto references_missing
if not exist "%GAME_DIR%\BepInEx\core\Mono.Cecil.dll" goto references_missing
if not exist "%GAME_DIR%\Stronghold Crusader Definitive Edition_Data\Managed\Noesis.NoesisGUI.dll" goto references_missing
if not exist "%PROJECT_DIR%..\..\shcde-script-extender\deps\Assembly-CSharp-publicized.dll" goto references_missing

powershell.exe -NoProfile -Command "$codeFiles = Get-ChildItem -LiteralPath '%PROJECT_DIR%src','%PROJECT_DIR%patcher' -Recurse -File -Include *.cs; $projectFiles = Get-ChildItem -LiteralPath '%PROJECT_DIR%' -File | Where-Object { $_.Extension -eq '.csproj' }; $runtimeFiles = @($codeFiles) + @($projectFiles); if ($runtimeFiles | Select-String -Pattern 'System\.Text\.Json|Newtonsoft|JavaScriptSerializer|System\.Web\.Extensions|DataContractJsonSerializer|JsonUtility') { exit 1 }; if ($codeFiles | Select-String -Pattern '\b(OnDestroy|OnDisable|OnApplicationQuit)\s*\(') { exit 1 }; $pluginSource = Get-Content -LiteralPath '%PROJECT_DIR%src\StartupPerformanceDiagnosticPlugin.cs' -Raw; if ($pluginSource -match 'MainViewModel\.instance|FrontendMenus\.loadingStill|\bvoid\s+Update\s*\(') { exit 1 }; exit 0"
if errorlevel 1 goto preflight_failed

if exist "%LOCAL_PATCHER_DIR%\" rmdir /S /Q "%LOCAL_PATCHER_DIR%"
if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
if exist "%PROJECT_DIR%tests\bin\" rmdir /S /Q "%PROJECT_DIR%tests\bin"

pushd "%PROJECT_DIR%"
"%MSBUILD%" StartupPerformanceDiagnostic.Patcher.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%"
if errorlevel 1 goto build_failed_popd
"%MSBUILD%" StartupPerformanceDiagnostic.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%"
if errorlevel 1 goto build_failed_popd
"%MSBUILD%" tests\StartupPerformanceDiagnostic.Tests.csproj /p:Configuration=Debug
if errorlevel 1 goto build_failed_popd
"%PROJECT_DIR%tests\bin\StartupPerformanceDiagnostic.Tests.exe"
if errorlevel 1 goto tests_failed_popd
popd

copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
if not exist "%LOCAL_PATCHER_DIR%\%PATCHER_NAME%" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\StartupPerformanceDiagnostic.dll" goto package_failed
if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed
if exist "%LOCAL_PLUGIN_DIR%\StartupPerformanceDiagnostic.Patcher.dll" goto package_failed

if not exist "%GAME_DIR%\BepInEx\patchers\" mkdir "%GAME_DIR%\BepInEx\patchers"
copy /Y "%LOCAL_PATCHER_DIR%\%PATCHER_NAME%" "%GAME_PATCHER%" >nul
if errorlevel 1 goto copy_failed
if exist "%GAME_PLUGIN_DIR%\" rmdir /S /Q "%GAME_PLUGIN_DIR%"
xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
if errorlevel 1 goto copy_failed

echo Startup Performance Diagnostic built, tested, and installed successfully.
if "%NO_PAUSE%"=="0" pause
exit /b 0

:build_failed_popd
popd
:build_failed
echo Build failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
:tests_failed_popd
popd
echo Tests failed.
if "%NO_PAUSE%"=="0" pause
exit /b 1
:references_missing
echo Required BepInEx or game reference was not found.
if "%NO_PAUSE%"=="0" pause
exit /b 1
:preflight_failed
echo Static preflight failed: forbidden dependency, lifecycle method, or private direct field access found.
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
