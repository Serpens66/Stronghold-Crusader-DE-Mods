# ExtendedData release status

**Status:** code newer

- Release: [v1.0.12](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtendedData/v1.0.12)
- Release commit: [685e441](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/685e441edfe59cd91bf5e7270e340fbc60ca60ba)
- Current main commit: [a23aa1b](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/a23aa1b32276d670ffbb2a7b641d23b500d8e298)

## Relevant changed files

- `ExtendedData/build.bat`

## Diff

```diff
diff --git a/ExtendedData/build.bat b/ExtendedData/build.bat
index e6798d7ba..3423d53fd 100644
--- a/ExtendedData/build.bat
+++ b/ExtendedData/build.bat
@@ -58,6 +58,9 @@ if exist "%PACK_PLUGIN_ROOT%\Mods\ExtendedData_Serp\ExtendedData.dll" (
 )
 if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
 echo [%date% %time%] PowerShell checks / build step
+rem APIShared source: /apishared:main (release branch) or /apishared:dev (local checkout, default).
+call "%~dp0..\Shared\Tools\ApiSharedRepository\Select-ApiSharedBranch.cmd" %*
+if errorlevel 1 exit /b 1
 powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0.." -PackageDirectory "%API_SHARED_DIR%"
 if errorlevel 1 exit /b 1
 set "EXTENDER_DIR="
```
