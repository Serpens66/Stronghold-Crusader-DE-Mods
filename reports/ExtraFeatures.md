# ExtraFeatures release status

**Status:** code newer

- Release: [v1.0.114](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtraFeatures/v1.0.114)
- Release commit: [dc1f73a](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/dc1f73aaa61a7778f40ce19c0aab91899f9f5a99)
- Current main commit: [a23aa1b](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/a23aa1b32276d670ffbb2a7b641d23b500d8e298)

## Relevant changed files

- `ExtraFeatures/build.bat`

## Diff

```diff
diff --git a/ExtraFeatures/build.bat b/ExtraFeatures/build.bat
index 2e8f78823..9c25ab224 100644
--- a/ExtraFeatures/build.bat
+++ b/ExtraFeatures/build.bat
@@ -38,6 +38,9 @@ if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_
 rem Release automation can explicitly use the validated workspace package.
 if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
 echo [%date% %time%] PowerShell checks / build step
+rem APIShared source: /apishared:main (release branch) or /apishared:dev (local checkout, default).
+call "%~dp0..\Shared\Tools\ApiSharedRepository\Select-ApiSharedBranch.cmd" %*
+if errorlevel 1 exit /b 1
 powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0.." -PackageDirectory "%API_SHARED_DIR%"
 if errorlevel 1 exit /b 1
 set "PLUGIN_NAME=ExtraFeatures_Serp"
```
