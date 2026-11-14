# ExtraFeatures release status

**Status:** code newer

- Release: [v1.0.136](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtraFeatures/v1.0.136)
- Release commit: [4859419](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/48594190e84aace0f37ab27c883750d9a0bd8552)
- Current main commit: [b6714c1](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/b6714c1671b63648f9792725d1b9c70682c5ef43)

## Relevant changed files

- `ExtraFeatures/build.bat`

## Diff

```diff
diff --git a/ExtraFeatures/build.bat b/ExtraFeatures/build.bat
index b6ef73c07..f2ed5c076 100644
--- a/ExtraFeatures/build.bat
+++ b/ExtraFeatures/build.bat
@@ -1,4 +1,5 @@
 @echo off
+rem SHCDE_BUILD_PARAMETERS: explicit SDK, installation suffix and no-install settings from the dispatcher.
 setlocal EnableExtensions
 rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
 if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
@@ -46,6 +47,7 @@ set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBu
 set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
 set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
 set "API_SHARED_DIR=%~dp0..\APIShared\BepInEx\plugins\APIShared_Serp"
+if defined SHCDE_BRANCH_API_PACKAGE set "API_SHARED_DIR=%SHCDE_BRANCH_API_PACKAGE%"
 rem The installed release is canonical; SHCDESE_EXTENDER_DIR is the explicit override.
 if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
 rem Release automation can explicitly use the validated workspace package.
@@ -57,14 +59,14 @@ if not "%ERRORLEVEL%"=="0" exit /b 1
 rem Package proof is checked at SDK selection and again at the MSBuild reference boundary.
 set "PLUGIN_NAME=ExtraFeatures_Serp"
 set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
-set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
+set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%%SHCDE_BRANCH_INSTALL_SUFFIX%"
 set "EXTENDER_DIR="
 set "NO_PAUSE=0"
 for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
 
 rem Isolated /noinstall builds only write their copied source/package tree.
 rem Installing builds still require the game to be closed before replacing any files.
-if "%NO_INSTALL%"=="1" if defined SHCDE_BRANCH_SOURCE_ROOT goto build_driver_game_ready
+if "%NO_INSTALL%"=="1" if "%SHCDE_BUILD_ISOLATED%"=="1" goto build_driver_game_ready
 echo [%date% %time%] Check that the game is closed
 powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
 if not "%ERRORLEVEL%"=="0" (
@@ -93,7 +95,7 @@ if not "%ERRORLEVEL%"=="0" goto build_failed
 
 pushd "%PROJECT_DIR%"
 echo [%date% %time%] Compile projects
-"%MSBUILD%" "%PROJECT_DIR%..\_inspect\ExtraFeaturesNativeTests\ExtraFeaturesNativeTests.csproj" /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%GAME_SCRIPT_EXTENDER_DIR%"
+"%MSBUILD%" "%PROJECT_DIR%..\_inspect\ExtraFeaturesNativeTests\ExtraFeaturesNativeTests.csproj" /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%GAME_SCRIPT_EXTENDER_DIR%" %SHCDE_BRANCH_MSBUILD_API%
 if not "%ERRORLEVEL%"=="0" goto build_failed_popd
 "%PROJECT_DIR%..\_inspect\ExtraFeaturesNativeTests\bin\ExtraFeaturesNativeTests.exe" "%GAME_DIR%"
 if not "%ERRORLEVEL%"=="0" goto build_failed_popd
```
