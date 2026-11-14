# StartConditions release status

**Status:** code newer

- Release: [v1.0.35](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/StartConditions/v1.0.35)
- Release commit: [4859419](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/48594190e84aace0f37ab27c883750d9a0bd8552)
- Current main commit: [b6714c1](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/b6714c1671b63648f9792725d1b9c70682c5ef43)

## Relevant changed files

- `StartConditions/build.bat`

## Diff

```diff
diff --git a/StartConditions/build.bat b/StartConditions/build.bat
index 29adcda49..7e9433de6 100644
--- a/StartConditions/build.bat
+++ b/StartConditions/build.bat
@@ -1,4 +1,5 @@
 @echo off
+rem SHCDE_BUILD_PARAMETERS: explicit SDK, installation suffix and no-install settings from the dispatcher.
 setlocal EnableExtensions
 rem SHCDE_BRANCH_DISPATCH: select committed sources; the selected child owns its build.
 if defined SHCDE_BRANCH_SOURCE_ROOT goto :shcde_branch_selected
@@ -49,7 +50,9 @@ set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\00
 set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
 set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
 set "API_SHARED_DIR=%~dp0..\APIShared\BepInEx\plugins\APIShared_Serp"
+if defined SHCDE_BRANCH_API_PACKAGE set "API_SHARED_DIR=%SHCDE_BRANCH_API_PACKAGE%"
 set "LOCAL_API_SHARED_DIR=%PROJECT_DIR%..\APIShared\BepInEx\plugins\APIShared_Serp"
+if defined SHCDE_BRANCH_API_PACKAGE set "LOCAL_API_SHARED_DIR=%SHCDE_BRANCH_API_PACKAGE%"
 rem The installed release is canonical; SHCDESE_EXTENDER_DIR is the explicit override.
 if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
 rem Release automation can explicitly use the validated workspace package.
@@ -131,7 +134,7 @@ dotnet run --project "%PROJECT_DIR%tests\StartSettings.Tests\StartSettings.Tests
 if not "%ERRORLEVEL%"=="0" exit /b 1
 
 echo [%date% %time%] AI start-troop save and restore regressions
-"%MSBUILD%" "%PROJECT_DIR%..\_inspect\StartConditionsAIIsolationTests\StartConditionsAIIsolationTests.csproj" /nologo /v:minimal /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%"
+"%MSBUILD%" "%PROJECT_DIR%..\_inspect\StartConditionsAIIsolationTests\StartConditionsAIIsolationTests.csproj" /nologo /v:minimal /p:Configuration=Release /p:ExtenderDir="%EXTENDER_DIR%" %SHCDE_BRANCH_MSBUILD_API%
 if not "%ERRORLEVEL%"=="0" exit /b 1
 "%PROJECT_DIR%..\_inspect\StartConditionsAIIsolationTests\bin\StartConditionsAIIsolationTests.exe" --behavior-only
 if not "%ERRORLEVEL%"=="0" exit /b 1
@@ -150,8 +153,8 @@ if "%NO_INSTALL%"=="1" goto prepared_no_install
   set "PLUGIN_NAME=StartConditions_Serp"
   set "OLD_PLUGIN_NAME=StartConditions"
   set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\!PLUGIN_NAME!"
-  set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!PLUGIN_NAME!"
-  set "OLD_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!OLD_PLUGIN_NAME!"
+  set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!PLUGIN_NAME!%SHCDE_BRANCH_INSTALL_SUFFIX%"
+  set "OLD_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\!OLD_PLUGIN_NAME!%SHCDE_BRANCH_INSTALL_SUFFIX%"
   echo Installationsziel: !GAME_PLUGIN_DIR!
 
   if not exist "!LOCAL_PLUGIN_DIR!\" (
```
