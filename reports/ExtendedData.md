# ExtendedData release status

**Status:** code newer

- Release: [v1.0.26](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtendedData/v1.0.26)
- Release commit: [4859419](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/48594190e84aace0f37ab27c883750d9a0bd8552)
- Current main commit: [b6714c1](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/b6714c1671b63648f9792725d1b9c70682c5ef43)

## Relevant changed files

- `ExtendedData/build.bat`

## Diff

```diff
diff --git a/ExtendedData/build.bat b/ExtendedData/build.bat
index e3bd08b3f..71e8014da 100644
--- a/ExtendedData/build.bat
+++ b/ExtendedData/build.bat
@@ -1,4 +1,5 @@
 @echo off
+rem SHCDE_BUILD_PARAMETERS: explicit SDK, installation suffix and no-install settings from the dispatcher.
 setlocal EnableExtensions
 rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
 if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
@@ -53,14 +54,15 @@ if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_
 set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
 set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
 set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\ExtendedData_Serp"
-set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\ExtendedData_Serp"
-set "STAGED_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\.ExtendedData_Serp.build"
-set "LEGACY_TRAIL_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\CustomCustomTrail_Serp"
-set "LEGACY_LORD_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\CustomLordUpload_Serp"
+set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\ExtendedData_Serp%SHCDE_BRANCH_INSTALL_SUFFIX%"
+set "STAGED_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\.ExtendedData_Serp.build%SHCDE_BRANCH_INSTALL_SUFFIX%"
+set "LEGACY_TRAIL_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\CustomCustomTrail_Serp%SHCDE_BRANCH_INSTALL_SUFFIX%"
+set "LEGACY_LORD_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\CustomLordUpload_Serp%SHCDE_BRANCH_INSTALL_SUFFIX%"
 set "API_SHARED_DIR=%~dp0..\APIShared\BepInEx\plugins\APIShared_Serp"
+if defined SHCDE_BRANCH_API_PACKAGE set "API_SHARED_DIR=%SHCDE_BRANCH_API_PACKAGE%"
 set "NO_PAUSE=0"
 for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
-set "PACK_PLUGIN_ROOT=%GAME_DIR%\BepInEx\plugins\SerpsMods_Serp"
+set "PACK_PLUGIN_ROOT=%GAME_DIR%\BepInEx\plugins\SerpsMods_Serp%SHCDE_BRANCH_INSTALL_SUFFIX%"
 if exist "%PACK_PLUGIN_ROOT%\Mods\ExtendedData_Serp\ExtendedData.dll" (
   if exist "%GAME_PLUGIN_DIR%\ExtendedData.dll" (
     echo Zwei installierte ExtendedData-Kopien gefunden. Bitte Installation bereinigen.
@@ -135,7 +137,7 @@ if not "%ERRORLEVEL%"=="0" goto build_failed_popd
 "%PROJECT_DIR%ExtendedData.Upload.Tests\bin\Debug\ExtendedData.Upload.Tests.exe"
 if not "%ERRORLEVEL%"=="0" goto build_failed_popd
 echo [%date% %time%] Compile projects
-"%MSBUILD%" ExtendedData.JsonUpload.Tests\ExtendedData.JsonUpload.Tests.csproj /p:Configuration=Debug
+"%MSBUILD%" ExtendedData.JsonUpload.Tests\ExtendedData.JsonUpload.Tests.csproj /p:Configuration=Debug %SHCDE_BRANCH_MSBUILD_API%
 if not "%ERRORLEVEL%"=="0" goto build_failed_popd
 "%PROJECT_DIR%ExtendedData.JsonUpload.Tests\bin\Debug\ExtendedData.JsonUpload.Tests.exe"
 if not "%ERRORLEVEL%"=="0" goto build_failed_popd
```
