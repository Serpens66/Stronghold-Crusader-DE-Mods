# BuildingCosts release status

**Status:** code newer

- Release: [v1.0.118](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingCosts/v1.0.118)
- Release commit: [4859419](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/48594190e84aace0f37ab27c883750d9a0bd8552)
- Current main commit: [b6714c1](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/b6714c1671b63648f9792725d1b9c70682c5ef43)

## Relevant changed files

- `BuildingCosts/build.bat`

## Diff

```diff
diff --git a/BuildingCosts/build.bat b/BuildingCosts/build.bat
index bad9e2aad..d11ce3df1 100644
--- a/BuildingCosts/build.bat
+++ b/BuildingCosts/build.bat
@@ -1,4 +1,5 @@
 @echo off
+rem SHCDE_BUILD_PARAMETERS: explicit SDK, installation suffix and no-install settings from the dispatcher.
 setlocal EnableExtensions
 rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
 if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
@@ -111,7 +112,7 @@ echo.
 
 pushd "%PROJECT_DIR%"
 echo [%date% %time%] Compile projects
-"%MSBUILD%" BuildingCosts.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
+"%MSBUILD%" BuildingCosts.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" %SHCDE_BRANCH_MSBUILD_API%
 set "BUILD_EXIT_CODE=%ERRORLEVEL%"
 popd
 
@@ -123,8 +124,8 @@ if "%NO_INSTALL%"=="1" goto prepared_no_install
   set "PLUGIN_NAME=BuildingCosts_Serp"
   set "OLD_PLUGIN_NAME=BuildingCosts"
   set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\!PLUGIN_NAME!"
-  set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!PLUGIN_NAME!"
-  set "OLD_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!OLD_PLUGIN_NAME!"
+  set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!PLUGIN_NAME!%SHCDE_BRANCH_INSTALL_SUFFIX%"
+  set "OLD_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!OLD_PLUGIN_NAME!%SHCDE_BRANCH_INSTALL_SUFFIX%"
 
   if not exist "!LOCAL_PLUGIN_DIR!\" (
     echo Lokaler Plugin-Ordner wurde nicht gefunden:
```
