# ExtendedData release status

**Status:** code newer

- Release: [v1.0.7](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtendedData/v1.0.7)
- Release commit: [fdeb151](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/fdeb1515a1aab78a9f08d1053c4b5ad724520a4b)
- Current main commit: [2c95ad4](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/2c95ad4c3cefecb00bdadc503704e56a025a37f7)

## Relevant changed files

- `APIShared`
- `ExtendedData/BepInEx/plugins/ExtendedData_Serp/ExtendedData.Core.dll`
- `ExtendedData/BepInEx/plugins/ExtendedData_Serp/ExtendedData.Core.pdb`
- `ExtendedData/BepInEx/plugins/ExtendedData_Serp/ExtendedData.dll`
- `ExtendedData/BepInEx/plugins/ExtendedData_Serp/ExtendedData.pdb`
- `ExtendedData/BepInEx/plugins/ExtendedData_Serp/info.json`
- `ExtendedData/BepInEx/plugins/ExtendedData_Serp/Locales/de-DE.txt`
- `ExtendedData/BepInEx/plugins/ExtendedData_Serp/Locales/en-US.txt`
- `ExtendedData/BepInEx/plugins/ExtendedData_Serp/Override/ScriptExtenderUI/ExtendedDataSettings.xaml`
- `ExtendedData/build.bat`
- `ExtendedData/ExtendedData.Core/ExtendedData.Core.csproj`
- `ExtendedData/ExtendedData.Core/TrailLordSelectionPolicy.cs`
- `ExtendedData/ExtendedData.csproj`
- `ExtendedData/info.json`
- `ExtendedData/Locales/de-DE.txt`
- `ExtendedData/Locales/en-US.txt`
- `ExtendedData/Override/ScriptExtenderUI/ExtendedDataSettings.xaml`
- `ExtendedData/src/BugfixesAndQoLTrailCustomizationBridge.cs`
- `ExtendedData/src/CoopTrailPackageExporter.cs`
- `ExtendedData/src/ExtendedDataLaunchOriginApi.cs`
- `ExtendedData/src/ExtendedDataOriginRegistration.cs`
- `ExtendedData/src/ExtendedDataPlugin.cs`
- `ExtendedData/src/ExtendedDataRuntime.cs`
- `ExtendedData/src/ExtendedDataSettingsViewModel.cs`
- `ExtendedData/src/LordDataSyncCoordinator.cs`
- `ExtendedData/src/LordUpload/CustomLordPopupPatchVerifier.cs`
- `ExtendedData/src/MapModSettingsCoordinator.cs`
- `ExtendedData/src/Properties/AssemblyInfo.cs`
- `ExtendedData/src/TrailLordPackageRuntime.cs`
- `ExtendedData/src/TrailLordSourceSelector.cs`
- `ExtendedData/src/TrailMissionSettingsCoordinator.cs`
- `ExtendedData/Test-RuntimePreflight.ps1`
- `Shared/Adapters/APIShared/MissionEventsAdapter.cs`
- `Shared/Adapters/APIShared/PlayerIdentityHelper.cs`
- `Shared/Runtime/Diagnostics/DebugLogHelper.cs`
- `Shared/Runtime/Localization/SerpLocalization.cs`
- `Shared/Runtime/Persistence/DependencyFreeJson.cs`
- `Shared/Runtime/Threading/UnityMainThreadDispatch.cs`
- `Shared/Runtime/UI/ToolTipPresentation.cs`
- `Shared/Runtime/Workshop/WorkshopContentPaths.cs`
- `Shared/Runtime/Workshop/WorkshopUploadStaging.cs`

Relevant localization keys: `ExtendedData.ExportFailedLog`, `ExtendedData.ExportFailedTitle`

The localization helper also contains a general logic change that affects every consumer.

## Diff

```diff
diff --git a/ExtendedData/BepInEx/plugins/ExtendedData_Serp/info.json b/ExtendedData/BepInEx/plugins/ExtendedData_Serp/info.json
index 5dc97ecd7..2f22328f9 100644
--- a/ExtendedData/BepInEx/plugins/ExtendedData_Serp/info.json
+++ b/ExtendedData/BepInEx/plugins/ExtendedData_Serp/info.json
@@ -3,16 +3,42 @@
   "Author": "Serpens66",
   "Name": "Extended Data",
   "Description": "Adds portable Custom and Coop Trail data plus complete Script Extender Custom Lord Workshop packages.",
-  "Version": "1.0.7",
+  "Version": "1.0.10",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "MinimumScriptExtenderVersion": "2.4.0",
   "MaximumScriptExtenderVersion": "",
+  "Dependencies": [
+    {
+      "GUID": "APIShared_Serp",
+      "MinimumVersion": "0.5.0"
+    }
+  ],
   "Manifest": 1,
   "NetworkMode": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.10",
+      "Changes": [
+        "Expose source package paths and Workshop subscription status for Coop Trail import rows, enabling safe local Trail deletion in Bugfixes and QoL."
+      ]
+    },
+    {
+      "Version": "1.0.9",
+      "Changes": [
+        "Migrate to APIShared 0.5.0 shared services and updated mod-owned adapters while preserving existing features."
+      ]
+    },
+    {
+      "Version": "1.0.8",
+      "Changes": [
+        "Move routine diagnostics to Debug; preserve warnings, errors and explicit file-operation results."
+      ]
+    },
     {
       "Version": "1.0.7",
-      "Changes": ["Preserve restart safety when an optional settings provider is removed or unavailable."]
+      "Changes": [
+        "Preserve restart safety when an optional settings provider is removed or unavailable."
+      ]
     },
     {
       "Version": "1.0.6",

diff --git a/ExtendedData/BepInEx/plugins/ExtendedData_Serp/Locales/de-DE.txt b/ExtendedData/BepInEx/plugins/ExtendedData_Serp/Locales/de-DE.txt
index 88985c768..69be57a27 100644
--- a/ExtendedData/BepInEx/plugins/ExtendedData_Serp/Locales/de-DE.txt
+++ b/ExtendedData/BepInEx/plugins/ExtendedData_Serp/Locales/de-DE.txt
@@ -86,7 +86,8 @@ ExtendedData.ErrorParticipantsNotReady=Paketstatus noch nicht bereit bei:
 ExtendedData.StartBlockedTitle=Koop-Trail kann nicht gestartet werden
 ExtendedData.TrailMakerCoop=Koop-Trail
 ExtendedData.TrailMakerCoopHelp=Exportiert die ersten 40 vorhandenen Missionen als portables Koop-Paket. Missionen 1–10 werden Koop-Trail 1, 11–20 Trail 2, 21–30 Trail 3 und 31–40 Trail 4. Spätere Missionen bleiben normale Custom-Trail-Missionen. Die ersten beiden belegten Spielerslots werden Host und Gast.
-ExtendedData.ExportFailedTitle=Koop-Trail-Export fehlgeschlagen
+ExtendedData.ExportFailedTitle=Trail-Export fehlgeschlagen
+ExtendedData.ExportFailedLog=Beim Export ist ein Fehler aufgetreten. Details findest du im BepInEx-Log (LogOutput.log).
 ExtendedData.ExportFailed=Der normale Trail wurde nicht exportiert, weil das Koop-Paket nicht erstellt werden konnte.
 ExtendedData.MapModSettingsErrorTitle=Map-Modsettings nicht verfügbar
 ExtendedData.MapModSettingsUnavailable=Die ausgewählte Map enthält keine gültigen eingebetteten Modsettings.

diff --git a/ExtendedData/BepInEx/plugins/ExtendedData_Serp/Locales/en-US.txt b/ExtendedData/BepInEx/plugins/ExtendedData_Serp/Locales/en-US.txt
index ceaaaad89..f3a1c4ed2 100644
--- a/ExtendedData/BepInEx/plugins/ExtendedData_Serp/Locales/en-US.txt
+++ b/ExtendedData/BepInEx/plugins/ExtendedData_Serp/Locales/en-US.txt
@@ -86,7 +86,8 @@ ExtendedData.ErrorParticipantsNotReady=Package status not ready for:
 ExtendedData.StartBlockedTitle=Coop Trail cannot start
 ExtendedData.TrailMakerCoop=Coop Trail
 ExtendedData.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.
-ExtendedData.ExportFailedTitle=Coop Trail export failed
+ExtendedData.ExportFailedTitle=Trail export failed
+ExtendedData.ExportFailedLog=An error occurred during export. See the BepInEx log (LogOutput.log) for details.
 ExtendedData.ExportFailed=The normal Trail was not exported because the Coop package could not be created.
 ExtendedData.MapModSettingsErrorTitle=Map mod settings unavailable
 ExtendedData.MapModSettingsUnavailable=The selected Map contains no valid embedded mod settings.

diff --git a/ExtendedData/BepInEx/plugins/ExtendedData_Serp/Override/ScriptExtenderUI/ExtendedDataSettings.xaml b/ExtendedData/BepInEx/plugins/ExtendedData_Serp/Override/ScriptExtenderUI/ExtendedDataSettings.xaml
index 6dfdadcee..b000c0987 100644
--- a/ExtendedData/BepInEx/plugins/ExtendedData_Serp/Override/ScriptExtenderUI/ExtendedDataSettings.xaml
+++ b/ExtendedData/BepInEx/plugins/ExtendedData_Serp/Override/ScriptExtenderUI/ExtendedDataSettings.xaml
@@ -2,7 +2,7 @@
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       xmlns:ui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
       xmlns:sys="clr-namespace:System;assembly=mscorlib"
-      xmlns:shared="clr-namespace:Shared;assembly=APIShared"
+      xmlns:shared="clr-namespace:APIShared.ModSettings;assembly=APIShared"
       xmlns:seui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
       shared:ModSettingsSearch.FilterText="{Binding System_ModSettingsSearchText}"
       shared:ModSettingsSearch.IncludeToolTips="{Binding System_ModSettingsSearchIncludeToolTips}"

diff --git a/ExtendedData/build.bat b/ExtendedData/build.bat
index 2e7a7de4f..b9aa6624f 100644
--- a/ExtendedData/build.bat
+++ b/ExtendedData/build.bat
@@ -1,4 +1,28 @@
 @echo off
+setlocal EnableExtensions
+set "BUILD_DRIVER_NOPAUSE=0"
+for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
+set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
+echo [%date% %time%] START ExtendedData
+cd /d "%~dp0"
+if errorlevel 1 goto :build_driver_directory_failed
+rem The outer driver owns the pause, including failures before compilation.
+call :build_driver_main %* /nopause
+set "BUILD_DRIVER_RESULT=%ERRORLEVEL%"
+cd /d "%BUILD_DRIVER_ORIGINAL_DIR%"
+echo [%date% %time%] END: exit code %BUILD_DRIVER_RESULT%
+if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
+exit /b %BUILD_DRIVER_RESULT%
+
+:build_driver_directory_failed
+echo ERROR: Cannot enter the build directory "%~dp0".
+if "%BUILD_DRIVER_NOPAUSE%"=="0" pause
+exit /b 1
+
+:build_driver_main
+echo [%date% %time%] Workspace source and runtime preflight
+powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-SharedBoundaries.ps1"
+if errorlevel 1 exit /b 1
 setlocal EnableExtensions EnableDelayedExpansion
 
 set "PROJECT_DIR=%~dp0"
@@ -17,7 +41,7 @@ set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\ExtendedData_Serp"
 set "STAGED_GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\.ExtendedData_Serp.build"
 set "LEGACY_TRAIL_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\CustomCustomTrail_Serp"
 set "LEGACY_LORD_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\CustomLordUpload_Serp"
-set "API_SHARED_DIR=%GAME_DIR%\BepInEx\plugins\APIShared_Serp"
+set "API_SHARED_DIR=%~dp0..\APIShared\BepInEx\plugins\APIShared_Serp"
 set "NO_PAUSE=0"
 for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
 set "PACK_PLUGIN_ROOT=%GAME_DIR%\BepInEx\plugins\SerpsMods_Serp"
@@ -28,17 +52,19 @@ if exist "%PACK_PLUGIN_ROOT%\Mods\ExtendedData_Serp\ExtendedData.dll" (
   )
   set "GAME_PLUGIN_DIR=%PACK_PLUGIN_ROOT%\Mods\ExtendedData_Serp"
   set "STAGED_GAME_PLUGIN_DIR=%PACK_PLUGIN_ROOT%\Mods\.ExtendedData_Serp.build"
-  set "API_SHARED_DIR=%PACK_PLUGIN_ROOT%\Infrastructure\APIShared_Serp"
 )
 if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
+echo [%date% %time%] PowerShell checks / build step
+powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0.." -PackageDirectory "%API_SHARED_DIR%"
+if errorlevel 1 exit /b 1
 set "EXTENDER_DIR="
 
 rem Never touch build or installation output while the game has plugin DLLs loaded.
+echo [%date% %time%] Check that the game is closed
 powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
 if errorlevel 1 (
   echo Build und Installation abgebrochen: Stronghold Crusader Definitive Edition ist noch gestartet.
   echo Lokales Paket und installierter Mod wurden nicht veraendert.
-  if "%NO_PAUSE%"=="0" pause
   exit /b 1
 )
 
@@ -74,18 +100,23 @@ echo Verwende Script Extender Referenzen:
 echo !EXTENDER_DIR!
 echo.
 
+echo [%date% %time%] PowerShell checks / build step
 powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\_inspect\Fixes124Implementation\Verify-Implementation.ps1"
 if errorlevel 1 goto build_failed
 
 pushd "%PROJECT_DIR%"
+echo [%date% %time%] PowerShell checks / build step
 powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Test-RuntimePreflight.ps1" -GameDir "%GAME_DIR%"
 if errorlevel 1 goto forbidden_source_popd
+echo [%date% %time%] Run tests
 dotnet run --project ExtendedData.Tests -c Release
 if not "%ERRORLEVEL%"=="0" goto build_failed_popd
+echo [%date% %time%] Compile projects
 "%MSBUILD%" ExtendedData.Upload.Tests\ExtendedData.Upload.Tests.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%"
 if errorlevel 1 goto build_failed_popd
 "%PROJECT_DIR%ExtendedData.Upload.Tests\bin\Debug\ExtendedData.Upload.Tests.exe"
 if not "%ERRORLEVEL%"=="0" goto build_failed_popd
+echo [%date% %time%] Compile projects
 "%MSBUILD%" ExtendedData.JsonUpload.Tests\ExtendedData.JsonUpload.Tests.csproj /p:Configuration=Debug
 if errorlevel 1 goto build_failed_popd
 "%PROJECT_DIR%ExtendedData.JsonUpload.Tests\bin\Debug\ExtendedData.JsonUpload.Tests.exe"
@@ -95,12 +126,15 @@ rem Recreate the exact package so removed assets cannot survive an update.
 if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
 if errorlevel 1 goto package_failed_popd
 
+echo [%date% %time%] Compile projects
 "%MSBUILD%" ExtendedData.csproj /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%"
 if errorlevel 1 goto build_failed_popd
 popd
 
+echo [%date% %time%] Copy package files
 copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
 if errorlevel 1 goto package_failed
+echo [%date% %time%] Copy package files
 xcopy "%PROJECT_DIR%Examples" "%LOCAL_PLUGIN_DIR%\Examples\" /E /I /Q /Y
 if errorlevel 1 goto package_failed
 
@@ -116,10 +150,12 @@ if not exist "%LOCAL_PLUGIN_DIR%\Locales\en-US.txt" goto package_failed
 echo Installiere geprueftes Paket...
 if exist "%STAGED_GAME_PLUGIN_DIR%\" rmdir /S /Q "%STAGED_GAME_PLUGIN_DIR%"
 if errorlevel 1 goto copy_failed
+echo [%date% %time%] Copy package files
 xcopy "%LOCAL_PLUGIN_DIR%" "%STAGED_GAME_PLUGIN_DIR%\" /E /I /Q /Y
 if errorlevel 1 goto copy_failed
 rem Carry player-created lobby settings into the staged replacement package.
 if exist "%GAME_PLUGIN_DIR%\LobbyModSettings\" (
+echo [%date% %time%] Copy package files
   xcopy "%GAME_PLUGIN_DIR%\LobbyModSettings" "%STAGED_GAME_PLUGIN_DIR%\LobbyModSettings\" /E /I /Q /Y
   if errorlevel 1 goto copy_failed
 )
@@ -131,11 +167,11 @@ if exist "%LEGACY_TRAIL_PLUGIN_DIR%\" rmdir /S /Q "%LEGACY_TRAIL_PLUGIN_DIR%"
 if errorlevel 1 goto copy_failed
 if exist "%LEGACY_LORD_PLUGIN_DIR%\" rmdir /S /Q "%LEGACY_LORD_PLUGIN_DIR%"
 if errorlevel 1 goto copy_failed
-powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Release\Write-LocalBuildManifest.ps1" -ModName ExtendedData
+echo [%date% %time%] PowerShell checks / build step
+powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName ExtendedData
 if errorlevel 1 goto package_failed
 echo.
 echo Build, Tests und Installation erfolgreich.
-if "%NO_PAUSE%"=="0" pause
 exit /b 0
 
 :build_failed_popd
@@ -143,14 +179,12 @@ popd
 :build_failed
 echo.
 echo Build oder Tests fehlgeschlagen.
-if "%NO_PAUSE%"=="0" pause
 exit /b 1
 
 :forbidden_source_popd
 popd
 echo.
 echo ExtendedData Runtime-Preflight fehlgeschlagen.
-if "%NO_PAUSE%"=="0" pause
 exit /b 1
 
 :package_failed_popd
@@ -158,11 +192,9 @@ popd
 :package_failed
 echo.
 echo Das lokale Plugin-Paket konnte nicht erzeugt werden.
-if "%NO_PAUSE%"=="0" pause
 exit /b 1
 
 :copy_failed
 echo.
 echo Installation fehlgeschlagen. Ist das Spiel noch gestartet?
-if "%NO_PAUSE%"=="0" pause
 exit /b 1

diff --git a/ExtendedData/ExtendedData.Core/ExtendedData.Core.csproj b/ExtendedData/ExtendedData.Core/ExtendedData.Core.csproj
index b68cfa54e..a0ff8d0b7 100644
--- a/ExtendedData/ExtendedData.Core/ExtendedData.Core.csproj
+++ b/ExtendedData/ExtendedData.Core/ExtendedData.Core.csproj
@@ -3,13 +3,13 @@
     <TargetFramework>netstandard2.0</TargetFramework>
     <RootNamespace>ExtendedData.Core</RootNamespace>
     <AssemblyName>ExtendedData.Core</AssemblyName>
-    <AssemblyVersion>1.0.7.0</AssemblyVersion>
-    <FileVersion>1.0.7.0</FileVersion>
+    <AssemblyVersion>1.0.10.0</AssemblyVersion>
+    <FileVersion>1.0.10.0</FileVersion>
     <LangVersion>latest</LangVersion>
     <Nullable>disable</Nullable>
   </PropertyGroup>
   <ItemGroup>
-    <Compile Include="..\..\Shared\AtomicFileReplacement.cs" Link="Shared\AtomicFileReplacement.cs" />
-    <Compile Include="..\..\Shared\DependencyFreeJson.cs" Link="Shared\DependencyFreeJson.cs" />
+    <Compile Include="..\..\Shared\Runtime\Persistence\AtomicFileReplacement.cs" Link="Shared\Runtime\Persistence\AtomicFileReplacement.cs" />
+    <Compile Include="..\..\Shared\Runtime\Persistence\DependencyFreeJson.cs" Link="Shared\Runtime\Persistence\DependencyFreeJson.cs" />
   </ItemGroup>
 </Project>

diff --git a/ExtendedData/ExtendedData.Core/TrailLordSelectionPolicy.cs b/ExtendedData/ExtendedData.Core/TrailLordSelectionPolicy.cs
index 030ba0e8c..a14de2171 100644
--- a/ExtendedData/ExtendedData.Core/TrailLordSelectionPolicy.cs
+++ b/ExtendedData/ExtendedData.Core/TrailLordSelectionPolicy.cs
@@ -6,9 +6,17 @@ namespace ExtendedData
     {
         public static bool UsesEmbeddedLord(TrailLordSlot slot, bool builtInLord,
             string selectedLordName, string mediaAlias = null)
+        {
+            return UsesEmbeddedLord(slot, builtInLord, selectedLordName, mediaAlias, null);
+        }
+
+        public static bool UsesEmbeddedLord(TrailLordSlot slot, bool builtInLord,
+            string selectedLordName, string mediaAlias, int? selectedLordType)
         {
             return slot != null && !builtInLord &&
-                (string.Equals(slot.LordName, selectedLordName, StringComparison.Ordinal) ||
+                (string.IsNullOrWhiteSpace(selectedLordName) && selectedLordType.HasValue &&
+                 selectedLordType.Value >= 0 && slot.LordType == selectedLordType ||
+                 string.Equals(slot.LordName, selectedLordName, StringComparison.Ordinal) ||
                  !string.IsNullOrEmpty(mediaAlias) &&
                  string.Equals(mediaAlias, selectedLordName, StringComparison.Ordinal));
         }

diff --git a/ExtendedData/ExtendedData.csproj b/ExtendedData/ExtendedData.csproj
index 66eb6abfd..78115dc47 100644
--- a/ExtendedData/ExtendedData.csproj
+++ b/ExtendedData/ExtendedData.csproj
@@ -8,8 +8,8 @@
     <OutputType>Library</OutputType>
     <RootNamespace>ExtendedData</RootNamespace>
     <AssemblyName>ExtendedData</AssemblyName>
-    <AssemblyVersion>1.0.7.0</AssemblyVersion>
-    <FileVersion>1.0.7.0</FileVersion>
+    <AssemblyVersion>1.0.10.0</AssemblyVersion>
+    <FileVersion>1.0.10.0</FileVersion>
     <TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion>
     <LangVersion>latest</LangVersion>
     <Nullable>annotations</Nullable>
@@ -58,21 +58,22 @@
     <Reference Include="ExtendedData.Core"><HintPath>$(CoreOutput)\ExtendedData.Core.dll</HintPath><Private>true</Private></Reference>
   </ItemGroup>
   <ItemGroup>
-    <Compile Include="..\Shared\DebugLogHelper.cs"><Link>Shared\DebugLogHelper.cs</Link></Compile>
-    <Compile Include="..\Shared\DependencyFreeJson.cs"><Link>Shared\DependencyFreeJson.cs</Link></Compile>
-    <Compile Include="..\Shared\UnityMainThreadDispatch.cs"><Link>Shared\UnityMainThreadDispatch.cs</Link></Compile>
-    <Compile Include="..\Shared\GameModeHelper.cs"><Link>Shared\GameModeHelper.cs</Link></Compile>
-    <Compile Include="..\Shared\GameplaySessionLifecycle.cs"><Link>Shared\GameplaySessionLifecycle.cs</Link></Compile>
-    <Compile Include="..\Shared\SerpLocalization.cs"><Link>Shared\SerpLocalization.cs</Link></Compile>
-    <Compile Include="..\Shared\ToolTipPresentation.cs"><Link>Shared\ToolTipPresentation.cs</Link></Compile>
-    <Compile Include="..\Shared\WorkshopContentPaths.cs"><Link>Shared\WorkshopContentPaths.cs</Link></Compile>
-    <Compile Include="..\Shared\WorkshopUploadStaging.cs"><Link>Shared\WorkshopUploadStaging.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Diagnostics\DebugLogHelper.cs"><Link>Shared\Runtime\Diagnostics\DebugLogHelper.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Persistence\DependencyFreeJson.cs"><Link>Shared\Runtime\Persistence\DependencyFreeJson.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Threading\UnityMainThreadDispatch.cs"><Link>Shared\Runtime\Threading\UnityMainThreadDispatch.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\PlayerIdentityHelper.cs"><Link>Shared\Adapters\APIShared\PlayerIdentityHelper.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\MissionEventsAdapter.cs"><Link>Shared\Adapters\APIShared\MissionEventsAdapter.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Localization\SerpLocalization.cs"><Link>Shared\Runtime\Localization\SerpLocalization.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\UI\ToolTipPresentation.cs"><Link>Shared\Runtime\UI\ToolTipPresentation.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Workshop\WorkshopContentPaths.cs"><Link>Shared\Runtime\Workshop\WorkshopContentPaths.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Workshop\WorkshopUploadStaging.cs"><Link>Shared\Runtime\Workshop\WorkshopUploadStaging.cs</Link></Compile>
     <Compile Include="src\ExtendedDataPlugin.cs" />
     <Compile Include="src\Properties\AssemblyInfo.cs" />
     <Compile Include="src\ExtendedDataSettingsViewModel.cs" />
     <Compile Include="src\TrailModSelectionItem.cs" />
     <Compile Include="src\ExtendedDataRuntime.cs" />
     <Compile Include="src\ExtendedDataLaunchOriginApi.cs" />
+    <Compile Include="src\ExtendedDataOriginRegistration.cs" />
     <Compile Include="src\ExtendedDataModDataApi.cs" />
     <Compile Include="src\FixesLordPreferencesBridge.cs" />
     <Compile Include="src\TypedPreferenceSnapshotCodec.cs" />

diff --git a/ExtendedData/info.json b/ExtendedData/info.json
index 5dc97ecd7..2f22328f9 100644
--- a/ExtendedData/info.json
+++ b/ExtendedData/info.json
@@ -3,16 +3,42 @@
   "Author": "Serpens66",
   "Name": "Extended Data",
   "Description": "Adds portable Custom and Coop Trail data plus complete Script Extender Custom Lord Workshop packages.",
-  "Version": "1.0.7",
+  "Version": "1.0.10",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "MinimumScriptExtenderVersion": "2.4.0",
   "MaximumScriptExtenderVersion": "",
+  "Dependencies": [
+    {
+      "GUID": "APIShared_Serp",
+      "MinimumVersion": "0.5.0"
+    }
+  ],
   "Manifest": 1,
   "NetworkMode": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.10",
+      "Changes": [
+        "Expose source package paths and Workshop subscription status for Coop Trail import rows, enabling safe local Trail deletion in Bugfixes and QoL."
+      ]
+    },
+    {
+      "Version": "1.0.9",
+      "Changes": [
+        "Migrate to APIShared 0.5.0 shared services and updated mod-owned adapters while preserving existing features."
+      ]
+    },
+    {
+      "Version": "1.0.8",
+      "Changes": [
+        "Move routine diagnostics to Debug; preserve warnings, errors and explicit file-operation results."
+      ]
+    },
     {
       "Version": "1.0.7",
-      "Changes": ["Preserve restart safety when an optional settings provider is removed or unavailable."]
+      "Changes": [
+        "Preserve restart safety when an optional settings provider is removed or unavailable."
+      ]
     },
     {
       "Version": "1.0.6",

diff --git a/ExtendedData/Locales/de-DE.txt b/ExtendedData/Locales/de-DE.txt
index 88985c768..69be57a27 100644
--- a/ExtendedData/Locales/de-DE.txt
+++ b/ExtendedData/Locales/de-DE.txt
@@ -86,7 +86,8 @@ ExtendedData.ErrorParticipantsNotReady=Paketstatus noch nicht bereit bei:
 ExtendedData.StartBlockedTitle=Koop-Trail kann nicht gestartet werden
 ExtendedData.TrailMakerCoop=Koop-Trail
 ExtendedData.TrailMakerCoopHelp=Exportiert die ersten 40 vorhandenen Missionen als portables Koop-Paket. Missionen 1–10 werden Koop-Trail 1, 11–20 Trail 2, 21–30 Trail 3 und 31–40 Trail 4. Spätere Missionen bleiben normale Custom-Trail-Missionen. Die ersten beiden belegten Spielerslots werden Host und Gast.
-ExtendedData.ExportFailedTitle=Koop-Trail-Export fehlgeschlagen
+ExtendedData.ExportFailedTitle=Trail-Export fehlgeschlagen
+ExtendedData.ExportFailedLog=Beim Export ist ein Fehler aufgetreten. Details findest du im BepInEx-Log (LogOutput.log).
 ExtendedData.ExportFailed=Der normale Trail wurde nicht exportiert, weil das Koop-Paket nicht erstellt werden konnte.
 ExtendedData.MapModSettingsErrorTitle=Map-Modsettings nicht verfügbar
 ExtendedData.MapModSettingsUnavailable=Die ausgewählte Map enthält keine gültigen eingebetteten Modsettings.

diff --git a/ExtendedData/Locales/en-US.txt b/ExtendedData/Locales/en-US.txt
index ceaaaad89..f3a1c4ed2 100644
--- a/ExtendedData/Locales/en-US.txt
+++ b/ExtendedData/Locales/en-US.txt
@@ -86,7 +86,8 @@ ExtendedData.ErrorParticipantsNotReady=Package status not ready for:
 ExtendedData.StartBlockedTitle=Coop Trail cannot start
 ExtendedData.TrailMakerCoop=Coop Trail
 ExtendedData.TrailMakerCoopHelp=Exports the first 40 existing missions as a portable Coop package. Missions 1-10 become Coop Trail 1, 11-20 Trail 2, 21-30 Trail 3 and 31-40 Trail 4. Later missions remain normal Custom Trail missions. The first two occupied player slots become host and guest.
-ExtendedData.ExportFailedTitle=Coop Trail export failed
+ExtendedData.ExportFailedTitle=Trail export failed
+ExtendedData.ExportFailedLog=An error occurred during export. See the BepInEx log (LogOutput.log) for details.
 ExtendedData.ExportFailed=The normal Trail was not exported because the Coop package could not be created.
 ExtendedData.MapModSettingsErrorTitle=Map mod settings unavailable
 ExtendedData.MapModSettingsUnavailable=The selected Map contains no valid embedded mod settings.

diff --git a/ExtendedData/Override/ScriptExtenderUI/ExtendedDataSettings.xaml b/ExtendedData/Override/ScriptExtenderUI/ExtendedDataSettings.xaml
index 6dfdadcee..b000c0987 100644
--- a/ExtendedData/Override/ScriptExtenderUI/ExtendedDataSettings.xaml
+++ b/ExtendedData/Override/ScriptExtenderUI/ExtendedDataSettings.xaml
@@ -2,7 +2,7 @@
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       xmlns:ui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
       xmlns:sys="clr-namespace:System;assembly=mscorlib"
-      xmlns:shared="clr-namespace:Shared;assembly=APIShared"
+      xmlns:shared="clr-namespace:APIShared.ModSettings;assembly=APIShared"
       xmlns:seui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
       shared:ModSettingsSearch.FilterText="{Binding System_ModSettingsSearchText}"
       shared:ModSettingsSearch.IncludeToolTips="{Binding System_ModSettingsSearchIncludeToolTips}"

diff --git a/ExtendedData/src/BugfixesAndQoLTrailCustomizationBridge.cs b/ExtendedData/src/BugfixesAndQoLTrailCustomizationBridge.cs
index 0753dc833..882aa963d 100644
--- a/ExtendedData/src/BugfixesAndQoLTrailCustomizationBridge.cs
+++ b/ExtendedData/src/BugfixesAndQoLTrailCustomizationBridge.cs
@@ -45,7 +45,7 @@ namespace ExtendedData
                     });
                 if (registered)
                 {
-                    Shared.DebugLogHelper.LogInfo(
+                    Shared.DebugLogHelper.LogDebug(
                         log,
                         "Registered ExtendedData as the BugfixesAndQoL Trail customization provider.");
                 }

diff --git a/ExtendedData/src/CoopTrailPackageExporter.cs b/ExtendedData/src/CoopTrailPackageExporter.cs
index 731006f6b..3595facb8 100644
--- a/ExtendedData/src/CoopTrailPackageExporter.cs
+++ b/ExtendedData/src/CoopTrailPackageExporter.cs
@@ -90,11 +90,17 @@ namespace ExtendedData
                     if (!File.Exists(trailPath))
                         continue;
                     ordinal++;
-                    CoopMissionDefinition definition = CreateDefinition(
-                        trailPath,
-                        ordinal,
-                        missionsRoot,
-                        out IReadOnlyList<TrailLordSlot> lordSlots);
+                    CoopMissionDefinition definition;
+                    IReadOnlyList<TrailLordSlot> lordSlots;
+                    try
+                    {
+                        definition = CreateDefinition(trailPath, ordinal, missionsRoot, out lordSlots);
+                    }
+                    catch (Exception exception)
+                    {
+                        throw new InvalidDataException("Coop mission " + ordinal + " [" + trailPath +
+                            "] could not be exported. " + exception.Message, exception);
+                    }
                     string jsonPath = Path.Combine(missionsRoot, ordinal.ToString("00") + ".coopmission.json");
                     MissionLoader.WriteAtomic(jsonPath, definition);
                     TrailLordRequirements.Create(jsonPath, lordSlots).Write(jsonPath);
@@ -156,7 +162,8 @@ namespace ExtendedData
             HUD_IngameMenu.RestartSkirmishMapInfo restart = container?.restartSkirmishInfo;
             if (restart?.selectedHeader == null || restart.MPsetupData == null)
                 throw new InvalidDataException("Mission " + ordinal + " has no complete saved skirmish setup.");
-            lordSlots = TrailLordPackageRuntime.Capture(restart);
+            var lordSources = new Dictionary<int, TrailLordPackageRuntime.Candidate>();
+            lordSlots = TrailLordPackageRuntime.Capture(restart, lordSources);
 
             List<int> activeSlots = Enumerable.Range(0, Math.Min(8, restart.lordTypes?.Count ?? 0))
                 .Where(index => restart.lordTypes[index] != -9999)
@@ -198,7 +205,8 @@ namespace ExtendedData
                     Colour = Math.Max(1, Math.Min(8, colour)),
                 };
                 if (activeIndex >= 2)
-                    PopulateAi(player, restart, slot, assetRoot, missionsRoot, activeIndex + 1);
+                    PopulateAi(player, restart, slot, assetRoot, missionsRoot, activeIndex + 1,
+                        lordSources.TryGetValue(slot + 1, out var source) ? source : null);
                 definition.Players.Add(player);
             }
             MissionProjection projection = MissionProjection.Create(definition);
@@ -283,7 +291,8 @@ namespace ExtendedData
             int slot,
             string assetRoot,
             string missionsRoot,
-            int playerNumber)
+            int playerNumber,
+            TrailLordPackageRuntime.Candidate authorSource)
         {
             FRONT_Multiplayer.MPAIVInfo info = restart.aivs != null && slot < restart.aivs.Length ? restart.aivs[slot] : null;
             if (info == null)
@@ -296,7 +305,9 @@ namespace ExtendedData
             }
             else
             {
-                string lordSource = ResolveLordConfigFile(info);
+                string lordSource = RequireFile(ResolveNamedFile(authorSource?.Config.path,
+                    authorSource?.Config.name, ".lordjson"), ".lordjson",
+                    "author Lord for player " + playerNumber);
                 string playerAssetRoot = Path.Combine(assetRoot, "AI-" + playerNumber.ToString("00"));
                 Directory.CreateDirectory(playerAssetRoot);
                 string lordTarget = Path.Combine(playerAssetRoot, Path.GetFileName(lordSource));
@@ -304,7 +315,7 @@ namespace ExtendedData
                 player.Lord = new LordReference
                 {
                     Source = "bundled",
-                    Name = info.lordName,
+                    Name = authorSource.Name,
                     File = ToMissionRelative(lordTarget, missionsRoot),
                     BaseLordId = baseLordId,
                 };
@@ -327,7 +338,7 @@ namespace ExtendedData
                     }
                     else
                     {
-                        string aivSource = ResolveAivFile(info, aiv, baseLordId);
+                        string aivSource = ResolveAivFile(info, aiv, baseLordId, authorSource);
                         string playerAssetRoot = Path.Combine(assetRoot, "AI-" + playerNumber.ToString("00"));
                         Directory.CreateDirectory(playerAssetRoot);
                         string aivTarget = Path.Combine(playerAssetRoot, Path.GetFileName(aivSource));
@@ -350,30 +361,19 @@ namespace ExtendedData
             player.NativePreferredAiv = restart.MPsetupData.preferredAIVs[slot];
         }
 
-        private static string ResolveLordConfigFile(FRONT_Multiplayer.MPAIVInfo info)
-        {
-            CustomisationFileManager.CustomLordConfig config = info.lordConfig;
-            string direct = ResolveNamedFile(config?.path, config?.name, ".lordjson");
-            if (direct != null)
-                return direct;
-            List<CustomisationFileManager.CustomLordConfig> installed =
-                CustomisationFileManager.Instance.getLordLordList(-1, info.lordName);
-            CustomisationFileManager.CustomLordConfig match = installed?.FirstOrDefault(candidate =>
-                (config != null && candidate.checksum == config.checksum) ||
-                string.Equals(candidate.name, config?.name, StringComparison.OrdinalIgnoreCase));
-            return RequireFile(ResolveNamedFile(match?.path, match?.name, ".lordjson"), ".lordjson", "custom lord " + info.lordName);
-        }
-
-        private static string ResolveAivFile(FRONT_Multiplayer.MPAIVInfo info, CustomisationFileManager.CustomAIV aiv, int baseLordId)
+        private static string ResolveAivFile(FRONT_Multiplayer.MPAIVInfo info, CustomisationFileManager.CustomAIV aiv, int baseLordId,
+            TrailLordPackageRuntime.Candidate authorSource)
         {
             string direct = ResolveNamedFile(aiv.path, aiv.AIVName, ".aivjson");
             if (direct != null)
                 return direct;
-            List<CustomisationFileManager.CustomAIV> installed = info.builtInLord
+            List<CustomisationFileManager.CustomAIV> installed = authorSource != null
+                ? authorSource.Aivs
+                : info.builtInLord || string.IsNullOrWhiteSpace(info.lordName)
                 ? CustomisationFileManager.Instance.getLordAIVList(baseLordId)
                 : CustomisationFileManager.Instance.getLordAIVList(-1, info.lordName);
             CustomisationFileManager.CustomAIV match = installed?.FirstOrDefault(candidate =>
-                candidate.checksum == aiv.checksum || string.Equals(candidate.AIVName, aiv.AIVName, StringComparison.OrdinalIgnoreCase));
+                candidate.checksum == aiv.checksum && string.Equals(candidate.AIVName, aiv.AIVName, StringComparison.OrdinalIgnoreCase));
             return RequireFile(ResolveNamedFile(match?.path, match?.AIVName, ".aivjson"), ".aivjson", "AIV " + aiv.AIVName);
         }
 

diff --git a/ExtendedData/src/ExtendedDataLaunchOriginApi.cs b/ExtendedData/src/ExtendedDataLaunchOriginApi.cs
index 97aaceaae..cb2a3b164 100644
--- a/ExtendedData/src/ExtendedDataLaunchOriginApi.cs
+++ b/ExtendedData/src/ExtendedDataLaunchOriginApi.cs
@@ -18,8 +18,8 @@ namespace ExtendedData
         CustomizedSandsOfTime,
     }
 
-    /// <summary>Optional, dependency-free reflection surface for shared game-mode classification.</summary>
-    public static class ExtendedDataLaunchOriginApi
+    /// <summary>Consumer-owned launch-origin state and save-data protocol.</summary>
+    public static partial class ExtendedDataLaunchOriginApi
     {
         private const int CurrentApiVersion = 2;
         private const int LegacyApiVersion = 1;
@@ -157,7 +157,7 @@ namespace ExtendedData
                 launchPending = false;
             }
             if (previous != ExtendedDataLaunchOriginKind.None)
-                DebugLogHelper.LogInfo(log, $"Cleared customized launch origin: previous={previous}.");
+                DebugLogHelper.LogDebug(log, $"Cleared customized launch origin: previous={previous}.");
         }
 
         internal static void MarkMapStarted()
@@ -224,7 +224,7 @@ namespace ExtendedData
                     data.TrailId,
                     data.MissionId,
                     restored: true);
-                DebugLogHelper.LogInfo(
+                DebugLogHelper.LogDebug(
                     log,
                     $"Restored customized launch origin from save: origin={data.Origin}, trailId={data.TrailId}, missionId={data.MissionId}.");
             }
@@ -275,7 +275,7 @@ namespace ExtendedData
                 restoredFromSave = restored;
                 launchPending = true;
             }
-            DebugLogHelper.LogInfo(
+            DebugLogHelper.LogDebug(
                 log,
                 $"Set customized launch origin: origin={originValue}, trailType={trailTypeValue}, " +
                 $"trailId={trailIdValue}, missionId={missionIdValue}, restored={restored}.");

diff --git a/ExtendedData/src/ExtendedDataOriginRegistration.cs b/ExtendedData/src/ExtendedDataOriginRegistration.cs
new file mode 100644
index 000000000..7589c98bc
--- /dev/null
+++ b/ExtendedData/src/ExtendedDataOriginRegistration.cs
@@ -0,0 +1,18 @@
+using APIShared.GameModes;
+
+namespace ExtendedData
+{
+    public static partial class ExtendedDataLaunchOriginApi
+    {
+        internal static void RegisterModeProvider() =>
+            CustomizedLaunchOrigins.Register(ExtendedDataPlugin.PluginGuid, CaptureModeOrigin);
+
+        private static CustomizedLaunchOrigin CaptureModeOrigin()
+        {
+            lock (Sync)
+                return new CustomizedLaunchOrigin((CustomizedLaunchOriginKind)origin,
+                    trailType, trailId, missionId, restoredFromSave, launchPending,
+                    supportsBuiltInOrigins: true);
+        }
+    }
+}

diff --git a/ExtendedData/src/ExtendedDataPlugin.cs b/ExtendedData/src/ExtendedDataPlugin.cs
index 3891c023b..ea18970c6 100644
--- a/ExtendedData/src/ExtendedDataPlugin.cs
+++ b/ExtendedData/src/ExtendedDataPlugin.cs
@@ -1,3 +1,4 @@
+using APIShared.ModSettings;
 using BepInEx;
 using SHCDESE.API.LowLevel;
 using SHCDESE.API;
@@ -9,16 +10,17 @@ using UnityEngine;
 namespace ExtendedData
 {
     [BepInDependency("000shcdese", "2.4.0")]
-    [BepInDependency("APIShared_Serp", "0.4.9")]
+    [BepInDependency("APIShared_Serp", "0.5.0")]
     [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
     [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
     [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
     [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
+    [ExcludeFromSavegameModSettings]
     public sealed class ExtendedDataPlugin : BaseUnityPlugin
     {
         public const string PluginGuid = "ExtendedData_Serp";
         public const string PluginName = "Extended Data";
-        public const string PluginVersion = "1.0.7";
+        public const string PluginVersion = "1.0.10";
         public const bool ExtendedDataModSettingsOptOut = true;
 
         private static ExtendedDataRuntime runtime;
@@ -29,7 +31,7 @@ namespace ExtendedData
         private void Awake()
         {
             Shared.UnityMainThreadDispatch.InitializeForCurrentThread();
-            Shared.DebugLogHelper.LogInfo(Logger, PluginName + " " + PluginVersion + " loaded.");
+            ExtendedDataLaunchOriginApi.RegisterModeProvider();
             CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
         }
 
@@ -40,7 +42,7 @@ namespace ExtendedData
                 CrusaderLibrary.Instance.LibraryLoaded -= OnLibraryLoaded;
                 Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName);
                 Settings = new ExtendedDataSettingsViewModel();
-                Shared.LobbyModSettingsPresetRegistration.Register(
+                APIShared.ModSettings.LobbyModSettingsPresetRegistration.Register(
                     this,
                     Logger,
                     PluginGuid,

diff --git a/ExtendedData/src/ExtendedDataRuntime.cs b/ExtendedData/src/ExtendedDataRuntime.cs
index eeab43ae9..4dcd606e0 100644
--- a/ExtendedData/src/ExtendedDataRuntime.cs
+++ b/ExtendedData/src/ExtendedDataRuntime.cs
@@ -1802,7 +1802,7 @@ namespace ExtendedData
         private static bool ReadLobbyFlag(FRONT_Multiplayer self, FieldInfo field) =>
             self != null && (bool)field.GetValue(self);
 
-        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
+        private void LogInfo(string message) => Shared.DebugLogHelper.LogDebug(log, message);
         private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);
     }
 }

diff --git a/ExtendedData/src/ExtendedDataSettingsViewModel.cs b/ExtendedData/src/ExtendedDataSettingsViewModel.cs
index 57a64f0f8..a3f616016 100644
--- a/ExtendedData/src/ExtendedDataSettingsViewModel.cs
+++ b/ExtendedData/src/ExtendedDataSettingsViewModel.cs
@@ -1,3 +1,4 @@
+using APIShared.ModSettings;
 using ExtendedData.Core;
 using Noesis;
 using SHCDESE.API;
@@ -15,7 +16,7 @@ using System.Windows.Input;
 
 namespace ExtendedData
 {
-    public sealed class ExtendedDataSettingsViewModel : Shared.PresetLobbyModSettingsViewModel
+    public sealed class ExtendedDataSettingsViewModel : APIShared.ModSettings.PresetLobbyModSettingsViewModel
     {
         internal const string ErrorStatusPrefix = "ERROR|";
         internal const string MissingStatus = "ERROR|MISSING";
@@ -62,7 +63,7 @@ namespace ExtendedData
             SerpLocalization.Get(key);
 
         protected override void ConfigurePerPlayerLobbySettings(
-            Shared.PerPlayerLobbySettingsBuilder settings)
+            APIShared.ModSettings.PerPlayerLobbySettingsBuilder settings)
         {
             settings
                 .ResetSlotsWith(nameof(CoopPackageStatus), () => null)
@@ -88,7 +89,7 @@ namespace ExtendedData
         public event Action ActiveCoopPackageChanged;
         public event Action<string> LordDataSnapshotChanged;
         public event Action<string> LordPackageManifestChanged;
-        internal event Action<Shared.PerPlayerLobbySnapshot> LordDataLobbyChanged;
+        internal event Action<APIShared.ModSettings.PerPlayerLobbySnapshot> LordDataLobbyChanged;
         internal event Action CoopPackageRemoteStatusChanged;
         internal event Action LordDataRemoteStatusChanged;
         internal event Action<int> LordDataSnapshotMutationRejected;
@@ -139,7 +140,7 @@ namespace ExtendedData
             }
         }
 
-        [Shared.PresetLocal]
+        [APIShared.ModSettings.PresetLocal]
         public bool EnableClientFeatures
         {
             get => enableClientFeatures;
@@ -170,7 +171,7 @@ namespace ExtendedData
             }
         }
 
-        [Shared.PresetLocal]
+        [APIShared.ModSettings.PresetLocal]
         public string[] PlayerTrailPropertyIds
         {
             get => playerTrailPropertyIds;
@@ -183,7 +184,7 @@ namespace ExtendedData
             }
         }
 
-        [Shared.PresetLocal]
+        [APIShared.ModSettings.PresetLocal]
         public string[] FixedTrailPropertyIds
         {
             get => fixedTrailPropertyIds;
@@ -490,7 +491,7 @@ namespace ExtendedData
                         SerpLocalization.Get("ExtendedData.Mode.Fixed") + "\n" +
                         SerpLocalization.Get("ExtendedData.Mode.Mixed") + "\n" +
                         System.Globalization.CultureInfo.CurrentCulture.Name + "\n" +
-                        string.Join("\n", entry.Properties.Select(p => p.Name + ":" + p.PropertyType.AssemblyQualifiedName + ":" + p.IsDefined(typeof(Shared.RequiresRestartAttribute), true)));
+                        string.Join("\n", entry.Properties.Select(p => p.Name + ":" + p.PropertyType.AssemblyQualifiedName + ":" + p.IsDefined(typeof(APIShared.ModSettings.RequiresRestartAttribute), true)));
                     if (selectionCache.TryGetValue(entry.ModId, out var cached) &&
                         ReferenceEquals(cached.Item1, entry.Endpoint) && cached.Item2 == signature)
                         return cached.Item3;

diff --git a/ExtendedData/src/LordDataSyncCoordinator.cs b/ExtendedData/src/LordDataSyncCoordinator.cs
index 2f5bde9ac..c2b4a9516 100644
--- a/ExtendedData/src/LordDataSyncCoordinator.cs
+++ b/ExtendedData/src/LordDataSyncCoordinator.cs
@@ -1,3 +1,5 @@
+using APIShared.GameModes;
+using APIShared.ModSettings;
 using APIShared;
 using BepInEx.Logging;
 using CrusaderDE;
@@ -82,7 +84,7 @@ namespace ExtendedData
                     fixes.Apply(pendingTrail);
                     ExtendedDataModDataApi.SetNetworkSnapshot(pendingTrail, true);
                     trailApplied = true;
-                    DebugLogHelper.LogInfo(log, "Trail Lord values applied at mission initialization: " +
+                    DebugLogHelper.LogDebug(log, "Trail Lord values applied at mission initialization: " +
                         pendingTrail.Digest);
                     return;
                 }
@@ -95,7 +97,7 @@ namespace ExtendedData
                     packageManifest = null;
                     localLordPaths.Clear();
                     ExtendedDataModDataApi.SetNetworkSnapshot(null, false);
-                    DebugLogHelper.LogInfo(log, "Lord-data session cleared for single-player map initialization.");
+                    DebugLogHelper.LogDebug(log, "Lord-data session cleared for single-player map initialization.");
                     return;
                 }
                 if (packageManifest?.UseLocalValues == true)
@@ -126,7 +128,7 @@ namespace ExtendedData
                     if (!string.Equals(lastMapAppliedDiagnostic, active.Digest, StringComparison.Ordinal))
                     {
                         lastMapAppliedDiagnostic = active.Digest;
-                        DebugLogHelper.LogInfo(log, "Lord-data map initialization applied: " +
+                        DebugLogHelper.LogDebug(log, "Lord-data map initialization applied: " +
                             LordDataSyncDiagnostics.DescribeSnapshot(active));
                     }
                 }
@@ -158,7 +160,7 @@ namespace ExtendedData
                 lastMapTransitionDiagnostic = null;
                 lastMapAppliedDiagnostic = null;
                 if (hadLordSession)
-                    DebugLogHelper.LogInfo(log, "Lord-data map ended; session Fixes preferences restored.");
+                    DebugLogHelper.LogDebug(log, "Lord-data map ended; session Fixes preferences restored.");
             }));
         }
 
@@ -195,7 +197,7 @@ namespace ExtendedData
                 }
             }
             ExtendedDataModDataApi.SetSinglePlayerLords(paths, unresolved);
-            DebugLogHelper.LogInfo(log, "Single-player Lord selection published: " +
+            DebugLogHelper.LogDebug(log, "Single-player Lord selection published: " +
                 "selected=" + (selected == null ? "unavailable" : selected.Length.ToString()) +
                 ",required=" + selectedRosterRequired + ",trail=" + selectedTrail +
                 ",custom=" + paths.Count +
@@ -360,7 +362,7 @@ namespace ExtendedData
                     if (!string.IsNullOrWhiteSpace(mediaName))
                     {
                         names[slot.PlayerId] = mediaName;
-                        DebugLogHelper.LogInfo(log, "Custom Trail Lord media provider for slot " +
+                        DebugLogHelper.LogDebug(log, "Custom Trail Lord media provider for slot " +
                             slot.PlayerId + ": " + mediaName);
                     }
                     else if (!string.IsNullOrWhiteSpace(error))
@@ -391,7 +393,7 @@ namespace ExtendedData
                 ActiveLobbyMatches(lobby) && ObservedLobbyMatches(lobby) &&
                 !string.IsNullOrEmpty(settings.LordDataSnapshot))
             {
-                DebugLogHelper.LogInfo(log, "Lord-data existing host setting observed on lobby open: source=" +
+                DebugLogHelper.LogDebug(log, "Lord-data existing host setting observed on lobby open: source=" +
                     source + ",wire=" + LordDataSyncDiagnostics.DescribeJson(settings.LordDataSnapshot, false));
                 OnSnapshotChanged(settings.LordDataSnapshot);
             }
@@ -418,7 +420,7 @@ namespace ExtendedData
                 string signature = next.SessionId + ":" + next.Digest;
                 if (!string.Equals(signature, lastHostCaptureDiagnostic, StringComparison.Ordinal) ||
                     string.Equals(source, "start-attempt", StringComparison.Ordinal))
-                    DebugLogHelper.LogInfo(log, "Lord-data host capture: source=" + source + "," +
+                    DebugLogHelper.LogDebug(log, "Lord-data host capture: source=" + source + "," +
                         LordDataSyncDiagnostics.DescribeSnapshot(next) +
                         ",selection=" + DescribeLobbySelection(lobby));
                 lastHostCaptureDiagnostic = signature;
@@ -532,7 +534,7 @@ namespace ExtendedData
                                 "Override", "Fixes", "preferences.json"))),
                         FixesDigest = fixesJson == null ? null : LordDataSyncDiagnostics.Hash(fixesJson),
                     });
-                    DebugLogHelper.LogInfo(log, "Lord package inspected: source=" + source +
+                    DebugLogHelper.LogDebug(log, "Lord package inspected: source=" + source +
                         ",slot=" + (index + 1) + ",lord=" + LordDataSyncDiagnostics.SafeLabel(info.lordName) +
                         ",fingerprintedFiles=" + files.GameplayPaths.Count +
                         ",requiresLocalFiles=" + files.HasUnsupportedGameplayFiles +
@@ -572,7 +574,7 @@ namespace ExtendedData
             packageManifest = next;
             SetLocalPackageStatus(next);
             if (changed)
-                DebugLogHelper.LogInfo(log, "Lord package manifest published: source=" + source +
+                DebugLogHelper.LogDebug(log, "Lord package manifest published: source=" + source +
                     ",session=" + next.SessionId + "," +
                     LordDataSyncDiagnostics.DescribePackageMode(next) +
                     ",slots=" + next.Slots.Count);
@@ -599,7 +601,7 @@ namespace ExtendedData
                         received.Digest, wire, settings.LordPackageStatus))
                         throw new InvalidOperationException(
                             "The Lord package confirmation could not be queued for publication.");
-                    DebugLogHelper.LogInfo(log, "Lord package manifest received: session=" +
+                    DebugLogHelper.LogDebug(log, "Lord package manifest received: session=" +
                         received.SessionId + "," +
                         LordDataSyncDiagnostics.DescribePackageMode(received) +
                         ",localStatus=" + LordDataSyncDiagnostics.DescribePackageStatus(
@@ -644,7 +646,7 @@ namespace ExtendedData
                     if (required && !matched)
                         DebugLogHelper.LogWarning(log, message);
                     else
-                        DebugLogHelper.LogInfo(log, message);
+                        DebugLogHelper.LogDebug(log, message);
                 }
                 if (!matched)
                     continue;
@@ -1047,7 +1049,7 @@ namespace ExtendedData
 
         internal void LogStartAttempt(string command, bool runtimeEnabled, FRONT_Multiplayer lobby)
         {
-            DebugLogHelper.LogInfo(log, "Lord-data start hook reached: command=" +
+            DebugLogHelper.LogDebug(log, "Lord-data start hook reached: command=" +
                 LordDataSyncDiagnostics.SafeLabel(command) +
                 ",runtimeEnabled=" + runtimeEnabled + "," + DescribeHostGate(lobby) +
                 "," + DescribeAcknowledgements(lobbyHumanSlots) +
@@ -1057,7 +1059,7 @@ namespace ExtendedData
 
         internal void LogStartDecision(bool captured, bool ready, string reason)
         {
-            DebugLogHelper.LogInfo(log, "Lord-data start decision: captureSucceeded=" + captured +
+            DebugLogHelper.LogDebug(log, "Lord-data start decision: captureSucceeded=" + captured +
                 ",ready=" + ready + ",reason=" + (string.IsNullOrEmpty(reason) ? "none" : reason) +
                 "," + DescribeAcknowledgements(lobbyHumanSlots) +
                 "," + DescribePackageAcknowledgements(lobbyHumanSlots));
@@ -1154,7 +1156,7 @@ namespace ExtendedData
                 changed || string.Equals(source, "start-attempt", StringComparison.Ordinal));
             if (changed || string.Equals(source, "start-attempt", StringComparison.Ordinal))
             {
-                DebugLogHelper.LogInfo(log, "Lord-data host Fixes values applied: source=" + source +
+                DebugLogHelper.LogDebug(log, "Lord-data host Fixes values applied: source=" + source +
                     ",session=" + snapshot.SessionId + ",digest=" + snapshot.Digest +
                     ",selectedLords=" + snapshot.Slots.Count);
             }
@@ -1169,7 +1171,7 @@ namespace ExtendedData
             ExtendedDataModDataApi.SetNetworkSnapshot(snapshot, true);
             settings.LordDataStatus = "READY|" + snapshot.Digest;
             if (changed || string.Equals(source, "start-attempt", StringComparison.Ordinal))
-                DebugLogHelper.LogInfo(log, "Lord-data host publication: source=" + source +
+                DebugLogHelper.LogDebug(log, "Lord-data host publication: source=" + source +
                     ",session=" + snapshot.SessionId + ",digest=" + snapshot.Digest +
                     ",snapshotSettingMatches=" + string.Equals(settings.LordDataSnapshot,
                         snapshot.WireJson, StringComparison.Ordinal) +
@@ -1186,7 +1188,7 @@ namespace ExtendedData
                 if (!string.Equals(signature, lastHostEchoDiagnostic, StringComparison.Ordinal))
                 {
                     lastHostEchoDiagnostic = signature;
-                    DebugLogHelper.LogInfo(log, "Lord-data setting callback ignored as local host echo: lobby=" +
+                    DebugLogHelper.LogDebug(log, "Lord-data setting callback ignored as local host echo: lobby=" +
                         lobbyId + ",wire=" + LordDataSyncDiagnostics.DescribeJson(wireJson, false));
                 }
                 return;
@@ -1195,7 +1197,7 @@ namespace ExtendedData
             if (!string.Equals(receivedSignature, lastClientReceivedDiagnostic, StringComparison.Ordinal))
             {
                 lastClientReceivedDiagnostic = receivedSignature;
-                DebugLogHelper.LogInfo(log, "Lord-data host setting received: lobby=" + lobbyId +
+                DebugLogHelper.LogDebug(log, "Lord-data host setting received: lobby=" + lobbyId +
                     ",wire=" + LordDataSyncDiagnostics.DescribeJson(wireJson, false));
             }
             UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
@@ -1210,7 +1212,7 @@ namespace ExtendedData
                     bool newlyAccepted = !string.Equals(snapshot.Digest, lastClientAcceptedDiagnostic,
                         StringComparison.Ordinal);
                     if (newlyAccepted)
-                        DebugLogHelper.LogInfo(log, "Lord-data snapshot parsed: " +
+                        DebugLogHelper.LogDebug(log, "Lord-data snapshot parsed: " +
                             LordDataSyncDiagnostics.DescribeSnapshot(snapshot));
                     stage = "session-check";
                     FRONT_Multiplayer lobby = ExtendedDataRuntime.GetExistingMainViewModel()?.FRONTMultiplayer;
@@ -1222,7 +1224,7 @@ namespace ExtendedData
                     VerifyEffectiveFixes(snapshot, "client-receive", newlyAccepted);
                     if (newlyAccepted)
                     {
-                        DebugLogHelper.LogInfo(log, "Lord-data client Fixes values applied: session=" +
+                        DebugLogHelper.LogDebug(log, "Lord-data client Fixes values applied: session=" +
                             snapshot.SessionId + ",digest=" + snapshot.Digest +
                             ",selectedLords=" + snapshot.Slots.Count);
                     }
@@ -1237,7 +1239,7 @@ namespace ExtendedData
                         throw new InvalidOperationException(
                             "The Lord-data confirmation could not be queued for publication.");
                     if (newlyAccepted)
-                        DebugLogHelper.LogInfo(log, "Lord-data client values applied; confirmation queued: session=" +
+                        DebugLogHelper.LogDebug(log, "Lord-data client values applied; confirmation queued: session=" +
                             snapshot.SessionId + ",digest=" + snapshot.Digest + ",localStatus=" +
                             LordDataSyncDiagnostics.DescribeStatus(settings.LordDataStatus, snapshot.Digest));
                     lastClientAcceptedDiagnostic = snapshot.Digest;
@@ -1310,7 +1312,7 @@ namespace ExtendedData
         private void OnLobbyChanged(PerPlayerLobbySnapshot snapshot)
         {
             int[] players = snapshot?.Players?.Keys.OrderBy(id => id).ToArray() ?? Array.Empty<int>();
-            DebugLogHelper.LogInfo(log, "Lord-data lobby observation: previous=" + lobbyId +
+            DebugLogHelper.LogDebug(log, "Lord-data lobby observation: previous=" + lobbyId +
                 ",current=" + snapshot?.LobbyId + ",players=[" + string.Join(",", players) +
                 "],unresolved=" + snapshot?.HasUnresolvedPlayers + ",localPlayer=" +
                 snapshot?.LocalPlayerId + "," + DescribeAcknowledgements(players));
@@ -1339,7 +1341,7 @@ namespace ExtendedData
             lastHostEchoDiagnostic = null;
             lastAssetDiagnostics.Clear();
             lastPackageAvailabilityDiagnostics.Clear();
-            DebugLogHelper.LogInfo(log, "Lord-data session reset: lobby=" + lobbyId +
+            DebugLogHelper.LogDebug(log, "Lord-data session reset: lobby=" + lobbyId +
                 ",fixesRestored=true,activeSnapshot=absent.");
             if (snapshot?.LobbyId != null)
                 OnLobbyOpened(ExtendedDataRuntime.GetExistingMainViewModel()?.FRONTMultiplayer, "lobby-change");
@@ -1351,7 +1353,7 @@ namespace ExtendedData
             if (string.Equals(summary, lastRemoteStatusDiagnostic, StringComparison.Ordinal))
                 return;
             lastRemoteStatusDiagnostic = summary;
-            DebugLogHelper.LogInfo(log, "Lord-data remote status changed: " + summary);
+            DebugLogHelper.LogDebug(log, "Lord-data remote status changed: " + summary);
         }
 
         private void VerifyEffectiveFixes(LordDataSnapshot snapshot, string source, bool logSuccess)
@@ -1359,7 +1361,7 @@ namespace ExtendedData
             if (!snapshot.FixesInstalled)
             {
                 if (logSuccess)
-                    DebugLogHelper.LogInfo(log, "Lord-data Fixes verification: source=" + source +
+                    DebugLogHelper.LogDebug(log, "Lord-data Fixes verification: source=" + source +
                         ",digest=" + snapshot.Digest + ",state=not-installed.");
                 return;
             }
@@ -1376,7 +1378,7 @@ namespace ExtendedData
                         ",actual=" + LordDataSyncDiagnostics.DescribeJson(actual, true) +
                         ",matches=" + matches + ",mapOverride=" + fixes.HasMapOverride(slot.PlayerId) + ",scope=lord-defaults";
                     if (matches && logSuccess)
-                        DebugLogHelper.LogInfo(log, message);
+                        DebugLogHelper.LogDebug(log, message);
                     else if (!matches)
                     {
                         DebugLogHelper.LogError(log, message);
@@ -1395,7 +1397,7 @@ namespace ExtendedData
         }
 
         private void OnLocalStatusChanged(string status) =>
-            DebugLogHelper.LogInfo(log, "Lord-data local status changed: lobby=" + lobbyId +
+            DebugLogHelper.LogDebug(log, "Lord-data local status changed: lobby=" + lobbyId +
                 ",digest=" + (active?.Digest ?? "none") + ",status=" +
                 LordDataSyncDiagnostics.DescribeStatus(status, active?.Digest));
 
@@ -1421,7 +1423,7 @@ namespace ExtendedData
                                   settings.LordPackageStatus));
                     if (!current)
                     {
-                        DebugLogHelper.LogInfo(log, "Lord-data deferred confirmation skipped: source=" +
+                        DebugLogHelper.LogDebug(log, "Lord-data deferred confirmation skipped: source=" +
                             source + ",session=" + sessionId + ",digest=" + digest +
                             ",reason=selection-or-lobby-changed.");
                         return;
@@ -1435,7 +1437,7 @@ namespace ExtendedData
                     // an incoming host setting. APIShared republishes the current personal values
                     // after that incoming update has returned.
                     settings.System_RequestPerPlayerSettingsPublish();
-                    DebugLogHelper.LogInfo(log, "Lord-data deferred confirmation publication requested: source=" +
+                    DebugLogHelper.LogDebug(log, "Lord-data deferred confirmation publication requested: source=" +
                         source + ",session=" + sessionId + ",digest=" + digest +
                         ",origin=" + origin +
                         ",status=" + (isSnapshot
@@ -1479,7 +1481,7 @@ namespace ExtendedData
                 packageManifest?.UseLocalValues != true && !AllKnownPlayersAcknowledged())
                 DebugLogHelper.LogError(log, message + ",problem=one or more known player acknowledgements are missing or stale.");
             else
-                DebugLogHelper.LogInfo(log, message);
+                DebugLogHelper.LogDebug(log, message);
         }
 
         private bool AllKnownPlayersAcknowledged()
@@ -1528,7 +1530,7 @@ namespace ExtendedData
             if (string.Equals(signature, lastHostGateDiagnostic, StringComparison.Ordinal))
                 return;
             lastHostGateDiagnostic = signature;
-            DebugLogHelper.LogInfo(log, "Lord-data host capture skipped: source=" + source + "," + gate);
+            DebugLogHelper.LogDebug(log, "Lord-data host capture skipped: source=" + source + "," + gate);
         }
 
         private string DescribeHostGate(FRONT_Multiplayer lobby) =>
@@ -1539,7 +1541,7 @@ namespace ExtendedData
                 ActiveLobbyMatches(lobby),
                 ObservedLobbyMatches(lobby),
                 HasRealLobbyMember(lobby),
-                Shared.GameModeHelper.IsRealMultiplayer());
+                APIShared.GameModes.GameModeHelper.IsRealMultiplayer());
 
         private bool IsHostLobby(FRONT_Multiplayer lobby) =>
             LordDataSyncDiagnostics.IsHostLobby(
@@ -1626,7 +1628,7 @@ namespace ExtendedData
                     ",problem=asset-present-but-effective-entry-missing.");
                 throw new InvalidDataException("Fixes did not load the available preferences for selected Lord " + lordName + ".");
             }
-            DebugLogHelper.LogInfo(log, "Lord-data Fixes source: " + diagnostic);
+            DebugLogHelper.LogDebug(log, "Lord-data Fixes source: " + diagnostic);
         }
 
         private void Invalidate(string message)

diff --git a/ExtendedData/src/LordUpload/CustomLordPopupPatchVerifier.cs b/ExtendedData/src/LordUpload/CustomLordPopupPatchVerifier.cs
index 79f2caaf7..2b8712dcd 100644
--- a/ExtendedData/src/LordUpload/CustomLordPopupPatchVerifier.cs
+++ b/ExtendedData/src/LordUpload/CustomLordPopupPatchVerifier.cs
@@ -52,7 +52,7 @@ namespace ExtendedData
                     : Interlocked.Exchange(ref uploadPageVerificationLogged, 1) == 0;
                 if (firstSuccess)
                 {
-                    Shared.DebugLogHelper.LogInfo(
+                    Shared.DebugLogHelper.LogDebug(
                         log,
                         description + " XAML patch matched exactly once.");
                 }

diff --git a/ExtendedData/src/MapModSettingsCoordinator.cs b/ExtendedData/src/MapModSettingsCoordinator.cs
index 3cb572fb2..2b7db1170 100644
--- a/ExtendedData/src/MapModSettingsCoordinator.cs
+++ b/ExtendedData/src/MapModSettingsCoordinator.cs
@@ -1,3 +1,4 @@
+using APIShared.ModSettings;
 using APIShared;
 using BepInEx.Logging;
 using CrusaderDE;
@@ -176,7 +177,7 @@ namespace ExtendedData
             }));
 
             RegisterLobbyObserver();
-            DebugLogHelper.LogInfo(log, "Map mod-settings coordinator initialized.");
+            DebugLogHelper.LogDebug(log, "Map mod-settings coordinator initialized.");
         }
 
         internal void SetEnabled(bool value)
@@ -453,7 +454,7 @@ namespace ExtendedData
                         throw new InvalidDataException("Captured Map mod settings exceed the supported payload size.");
                     pendingMapSavePayload = payload;
                     pendingMapSavePath = NormalizePath(path);
-                    DebugLogHelper.LogInfo(
+                    DebugLogHelper.LogDebug(
                         log,
                         "Captured Map mod settings before save; mentioned=[" +
                         string.Join(", ", document.Mods.Keys) + "].");
@@ -708,7 +709,7 @@ namespace ExtendedData
             activeMapPath = NormalizePath(selected.filePath);
             activeMapCrc = selected.crc;
             activeJson = json;
-            DebugLogHelper.LogInfo(
+            DebugLogHelper.LogDebug(
                 log,
                 "Applied Map mod settings from [" + selected.filePath + "]; mentioned=[" +
                 string.Join(", ", document.Mods.Keys) + "].");
@@ -780,7 +781,7 @@ namespace ExtendedData
                     apply: false,
                     mapFileName: clearedMapFileName,
                     mapCrc: clearedMapCrc);
-            DebugLogHelper.LogInfo(log, "Cleared Map mod-settings context: " + reason + ".");
+            DebugLogHelper.LogDebug(log, "Cleared Map mod-settings context: " + reason + ".");
         }
 
         private void OnMapSettingsPacket(ReceiveCustomPacketEventArgs<MapModSettingsPacket> args)
@@ -876,7 +877,7 @@ namespace ExtendedData
                         SerpLocalization.Get("ExtendedData.MapModSettingsMissingTitle"),
                         SerpLocalization.Get("ExtendedData.MapModSettingsMissing") + " " + string.Join(", ", missing));
                 }
-                DebugLogHelper.LogInfo(log, "Applied authenticated host Map mod settings.");
+                DebugLogHelper.LogDebug(log, "Applied authenticated host Map mod settings.");
             }
             catch (Exception exception)
             {
@@ -912,7 +913,7 @@ namespace ExtendedData
                 dataLength = bytes.Length,
                 dataOffset = 0,
             });
-            DebugLogHelper.LogInfo(log, "Broadcast Map mod-settings " + (apply ? "apply" : "clear") + ".");
+            DebugLogHelper.LogDebug(log, "Broadcast Map mod-settings " + (apply ? "apply" : "clear") + ".");
         }
 
         private void RegisterLobbyObserver()

diff --git a/ExtendedData/src/Properties/AssemblyInfo.cs b/ExtendedData/src/Properties/AssemblyInfo.cs
index 948f14bfc..eb1f6140e 100644
--- a/ExtendedData/src/Properties/AssemblyInfo.cs
+++ b/ExtendedData/src/Properties/AssemblyInfo.cs
@@ -1,5 +1,5 @@
 using System.Reflection;
 
-[assembly: AssemblyVersion("1.0.7.0")]
-[assembly: AssemblyFileVersion("1.0.7.0")]
-[assembly: AssemblyInformationalVersion("1.0.7")]
+[assembly: AssemblyVersion("1.0.10.0")]
+[assembly: AssemblyFileVersion("1.0.10.0")]
+[assembly: AssemblyInformationalVersion("1.0.10")]

diff --git a/ExtendedData/src/TrailLordPackageRuntime.cs b/ExtendedData/src/TrailLordPackageRuntime.cs
index e07320856..02a963fd9 100644
--- a/ExtendedData/src/TrailLordPackageRuntime.cs
+++ b/ExtendedData/src/TrailLordPackageRuntime.cs
@@ -11,7 +11,7 @@ namespace ExtendedData
 {
     internal static class TrailLordPackageRuntime
     {
-        private sealed class Candidate
+        internal sealed class Candidate
         {
             internal string Name;
             internal CustomisationFileManager.CustomLordConfig Config;
@@ -19,7 +19,8 @@ namespace ExtendedData
         }
 
         internal static IReadOnlyList<TrailLordSlot> Capture(
-            HUD_IngameMenu.RestartSkirmishMapInfo restart)
+            HUD_IngameMenu.RestartSkirmishMapInfo restart,
+            IDictionary<int, Candidate> resolvedSources = null)
         {
             if (restart?.aivs == null)
                 throw new InvalidDataException("The Trail has no saved Lord data.");
@@ -34,16 +35,8 @@ namespace ExtendedData
                 if (info == null || info.builtInLord || info.lordConfig == null)
                     continue;
                 string checksum = info.lordConfig.checksum.ToString();
-                Candidate[] sources = FindCandidates(checksum,
-                    (info.aivs ?? new List<CustomisationFileManager.CustomAIV>())
-                        .Select(aiv => aiv.checksum.ToString()).ToArray());
-                Candidate[] matching = sources.Where(item =>
-                    string.Equals(item.Name, info.lordName, StringComparison.OrdinalIgnoreCase) &&
-                    string.Equals(item.Config.name, info.lordConfig.name, StringComparison.OrdinalIgnoreCase)).ToArray();
-                int sourceIndex = TrailLordSourceSelector.SelectIndex(
-                    matching.Select(item => item.Config.path).ToArray(),
-                    info.lordConfig.path, info.lordName);
-                Candidate source = matching[sourceIndex];
+                Candidate source = ResolveAuthorSource(info, index + 1);
+                if (resolvedSources != null) resolvedSources[index + 1] = source;
                 LordPackageFileState package = LordPackageFingerprint.Capture(source.Config.path, source.Config.name);
                 string sidecar = Path.Combine(source.Config.path, source.Config.name + ".modlord.json");
                 string modJson = File.Exists(sidecar) ? LordDataSnapshot.ReadModLordFile(sidecar) : null;
@@ -51,7 +44,7 @@ namespace ExtendedData
                 {
                     PlayerId = index + 1,
                     LordType = source.Config.lordType,
-                    LordName = info.lordName,
+                    LordName = source.Name,
                     ConfigName = info.lordConfig.name,
                     ConfigChecksum = checksum,
                     AivChecksums = (info.aivs ?? new List<CustomisationFileManager.CustomAIV>())
@@ -59,12 +52,38 @@ namespace ExtendedData
                     RequiresInstalledPackage = package.HasUnsupportedGameplayFiles,
                     PackageDigest = package.HasUnsupportedGameplayFiles ? package.Digest : null,
                     ModLordJson = modJson,
-                    FixesJson = fixes.Capture(info.lordName, source.Config.lordType),
+                    FixesJson = fixes.Capture(source.Name, source.Config.lordType),
                 });
             }
             return result;
         }
 
+        internal static Candidate ResolveAuthorSource(FRONT_Multiplayer.MPAIVInfo info, int playerId)
+        {
+            try
+            {
+                if (info?.lordConfig == null)
+                    throw new InvalidDataException("The author Lord has no saved configuration.");
+                string name = TrailLordSourceSelector.ResolveLordName(info.lordName,
+                    info.lordType, ConfigSettings.extendedLordPaths);
+                Candidate[] matching = FindCandidates(info.lordConfig.checksum.ToString(),
+                    (info.aivs ?? new List<CustomisationFileManager.CustomAIV>())
+                        .Select(aiv => aiv.checksum.ToString()).ToArray())
+                    .Where(item => TrailLordSourceSelector.MatchesIdentity(item.Name,
+                        item.Config.lordType, item.Config.name, name, info.lordType,
+                        info.lordConfig.name, string.IsNullOrWhiteSpace(info.lordName))).ToArray();
+                int sourceIndex = TrailLordSourceSelector.SelectIndex(
+                    matching.Select(item => item.Config.path).ToArray(), info.lordConfig.path, name);
+                return matching[sourceIndex];
+            }
+            catch (Exception exception)
+            {
+                throw new InvalidDataException("Author Lord resolution failed: player=" + playerId +
+                    ", lordType=" + info?.lordType + ", lordName=[" + info?.lordName +
+                    "], config=[" + info?.lordConfig?.name + "], checksum=" + info?.lordConfig?.checksum +
+                    ", source=[" + info?.lordConfig?.path + "]. " + exception.Message, exception);
+            }
+        }
         internal static bool TryResolve(TrailLordSlot slot, out string internalName,
             out string requiredLordPath, out string reason)
         {

diff --git a/ExtendedData/src/TrailLordSourceSelector.cs b/ExtendedData/src/TrailLordSourceSelector.cs
index 01202e62c..a8c1d7fe2 100644
--- a/ExtendedData/src/TrailLordSourceSelector.cs
+++ b/ExtendedData/src/TrailLordSourceSelector.cs
@@ -6,6 +6,24 @@ namespace ExtendedData
 {
     internal static class TrailLordSourceSelector
     {
+        internal static string ResolveLordName(string savedName, int lordType,
+            IReadOnlyList<string> extendedNames)
+        {
+            if (!string.IsNullOrWhiteSpace(savedName)) return savedName;
+            if (extendedNames == null || lordType < 0 || lordType >= extendedNames.Count ||
+                string.IsNullOrWhiteSpace(extendedNames[lordType]))
+                throw new InvalidDataException("An unnamed author Lord has no valid Extended Lord type: " + lordType);
+            return extendedNames[lordType];
+        }
+
+        internal static bool MatchesIdentity(string candidateName, int candidateType,
+            string candidateConfigName, string selectedName, int selectedType,
+            string selectedConfigName, bool unnamedExtendedLord)
+        {
+            return string.Equals(candidateName, selectedName, StringComparison.OrdinalIgnoreCase) &&
+                string.Equals(candidateConfigName, selectedConfigName, StringComparison.OrdinalIgnoreCase) &&
+                (!unnamedExtendedLord || candidateType == selectedType);
+        }
         internal static int SelectIndex(IReadOnlyList<string> candidateDirectories,
             string selectedDirectory, string lordName)
         {

diff --git a/ExtendedData/src/TrailMissionSettingsCoordinator.cs b/ExtendedData/src/TrailMissionSettingsCoordinator.cs
index f68719682..25621b069 100644
--- a/ExtendedData/src/TrailMissionSettingsCoordinator.cs
+++ b/ExtendedData/src/TrailMissionSettingsCoordinator.cs
@@ -1,3 +1,5 @@
+using APIShared.GameModes;
+using APIShared.ModSettings;
 using APIShared;
 using BepInEx.Bootstrap;
 using BepInEx.Logging;
@@ -252,7 +254,7 @@ namespace ExtendedData
                     if (!lastCompatibilityFailures.TryGetValue(item.ModId, out string previous) ||
                         !string.Equals(previous, item.IncompatibilityReason, StringComparison.Ordinal))
                     {
-                        DebugLogHelper.LogInfo(
+                        DebugLogHelper.LogDebug(
                             log,
                             $"Map/Trail mod settings [{item.DisplayName}] ({item.ModId}) are not included in creator presets: " +
                             item.IncompatibilityReason + ".");
@@ -354,7 +356,7 @@ namespace ExtendedData
                     new CustomLordUploadStager(),
                     new CustomLordUploadConfirmation(),
                     lordRules);
-                DebugLogHelper.LogInfo(
+                DebugLogHelper.LogDebug(
                     log,
                     "Custom Lord preflight rules: Script Extender=" + lordRules.ExtenderIdentity +
                     ", reflectedLordInfoFields=" + lordRules.LordInfoFields.Count +
@@ -463,7 +465,7 @@ namespace ExtendedData
 
                 EnsureCoopCustomizeButtons();
                 EnsureTrailMakerCoopCheckbox(FRONT_ManageTrail.Instance);
-                DebugLogHelper.LogInfo(log, "Trail mission-settings coordinator initialized.");
+                DebugLogHelper.LogDebug(log, "Trail mission-settings coordinator initialized.");
             }
 
             public void Dispose()
@@ -492,7 +494,7 @@ namespace ExtendedData
                     ModSettingsApplication.EnterContext(BuildRestartContext(source, document));
                     ApplyDocument(document, editable, presetLabel);
                     activeCreatorDocument = CloneDocument(document);
-                    DebugLogHelper.LogInfo(log, $"Loaded {source} mod settings; editable={editable}.");
+                    DebugLogHelper.LogDebug(log, $"Loaded {source} mod settings; editable={editable}.");
                     return GetMissingMentionedMods(document);
                 }
                 catch (Exception exception)
@@ -516,7 +518,7 @@ namespace ExtendedData
                 if (string.IsNullOrEmpty(ModSettingsApplication.ContextId)) ModSettingsApplication.EnterContext(BuildRestartContext(source, document));
                 ApplyDocument(document, editable, presetLabel, materializeCurrentValues: materializeCurrentValues);
                 activeCreatorDocument = CloneDocument(document);
-                DebugLogHelper.LogInfo(log, $"Loaded {source} mod settings; editable={editable}.");
+                DebugLogHelper.LogDebug(log, $"Loaded {source} mod settings; editable={editable}.");
                 return GetMissingMentionedMods(document);
             }
 
@@ -700,7 +702,7 @@ namespace ExtendedData
                     if (!cleanupDeferralLogged)
                     {
                         cleanupDeferralLogged = true;
-                        DebugLogHelper.LogInfo(log, "Deferred Trail mod-settings cleanup while Custom Trail setup/mission is active.");
+                        DebugLogHelper.LogDebug(log, "Deferred Trail mod-settings cleanup while Custom Trail setup/mission is active.");
                     }
                     return;
                 }
@@ -743,7 +745,7 @@ namespace ExtendedData
                 mapSourceDocument = null;
                 workingSourceContextId = string.Empty;
                 SourcesChanged?.Invoke();
-                DebugLogHelper.LogInfo(log, "Left " + activeContextLabel + " mod-settings context.");
+                DebugLogHelper.LogDebug(log, "Left " + activeContextLabel + " mod-settings context.");
                 activeContextLabel = "Trail";
             }
 
@@ -755,7 +757,7 @@ namespace ExtendedData
                 MissionPresetEndAction action = missionPresetLifecycle.End(MapEndKind(reason));
                 if (action == MissionPresetEndAction.Preserve)
                 {
-                    DebugLogHelper.LogInfo(
+                    DebugLogHelper.LogDebug(
                         log,
                         "Retained the active Map/Trail mod-settings preset across an expected mission replacement.");
                     return true;
@@ -769,7 +771,7 @@ namespace ExtendedData
                         trailContext = false;
                         workingContextEditable = false;
                     }
-                    DebugLogHelper.LogInfo(
+                    DebugLogHelper.LogDebug(
                         log,
                         "Suspended the Trail Maker mission preset until the authoring lobby returns.");
                     preserveContextForLaunch = false;
@@ -814,7 +816,7 @@ namespace ExtendedData
                 }
 
                 ModSettingsApplication.ConfirmStarted();
-                DebugLogHelper.LogInfo(log, "Confirmed active mission preset: " + pending + ".");
+                DebugLogHelper.LogDebug(log, "Confirmed active mission preset: " + pending + ".");
                 return true;
             }
 
@@ -911,7 +913,7 @@ namespace ExtendedData
                     // Capture synchronously before invoking it so every save uses its own visible values.
                     document = CaptureDocument();
                     string[] mentionedMods = document.Mods.Keys.ToArray();
-                    DebugLogHelper.LogInfo(
+                    DebugLogHelper.LogDebug(
                         log,
                         "Captured Trail mod settings before save; mentioned=[" + string.Join(", ", mentionedMods) + "].");
                     if (editorSaveOptions.IncludeTrailModSettings)
@@ -1186,7 +1188,7 @@ namespace ExtendedData
                             InvokeUploadFailure(mapTitle, terminalFailure);
                             return;
                         }
-                        DebugLogHelper.LogInfo(
+                        DebugLogHelper.LogDebug(
                             log,
                             $"Extended CPU Lord staging ready for [{mapTitle}]: {copiedFiles} copied, {existingFiles} already present, {packageFiles} package files, {packageBytes} bytes.");
                     }
@@ -1222,7 +1224,7 @@ namespace ExtendedData
                             InvokeUploadFailure(mapTitle, terminalFailure);
                             return;
                         }
-                        DebugLogHelper.LogInfo(log,
+                        DebugLogHelper.LogDebug(log,
                             $"Added {copiedFiles} Custom Trail JSON file(s) and {requiredFiles} required Lord file(s) to Workshop staging for [{mapTitle}].");
                     }
                 }
@@ -1450,7 +1452,7 @@ namespace ExtendedData
                     trail.Name + ".data",
                     includeModSettings,
                     out int copiedModSettings);
-                DebugLogHelper.LogInfo(
+                DebugLogHelper.LogDebug(
                     log,
                     $"Prepared Coop Trail Workshop package [{trail.Name}] with " +
                     $"mod-settings included={includeModSettings}, sidecars={copiedModSettings}, " +
@@ -1539,6 +1541,14 @@ namespace ExtendedData
                     {
                         Text1 = source.SelectionName,
                         Text2 = source.MissionCount.ToString(CultureInfo.InvariantCulture),
+                        // Public Vanilla metadata lets UI consumers distinguish local packages
+                        // from subscriptions without relying on names or Steam icons.
+                        trail = new MapFileManager.CustomTrailInfo
+                        {
+                            Name = source.SelectionName,
+                            FullPath = source.PackageRoot,
+                            workshop = !IsDirectChildOf(source.PackageRoot, ConfigSettings.GetUserCustomTrailsPath()),
+                        },
                     });
                 }
             }
@@ -1651,9 +1661,7 @@ namespace ExtendedData
                     catch (Exception exception)
                     {
                         DebugLogHelper.LogError(log, "Could not prepare Coop Trail export: " + exception);
-                        ShowInformation(
-                            SerpLocalization.Get("ExtendedData.ExportFailedTitle"),
-                            SerpLocalization.Get("ExtendedData.ExportFailed") + "\r\n" + exception.Message);
+                        ShowExportFailure();
                         return;
                     }
                 }
@@ -1685,9 +1693,7 @@ namespace ExtendedData
                 catch (Exception exception)
                 {
                     DebugLogHelper.LogError(log, "Could not finish Trail export: " + exception);
-                    ShowInformation(
-                        SerpLocalization.Get("ExtendedData.ExportFailedTitle"),
-                        SerpLocalization.Get("ExtendedData.ExportFailed") + "\r\n" + exception.Message);
+                    ShowExportFailure();
                 }
                 finally
                 {
@@ -1751,7 +1757,7 @@ namespace ExtendedData
                 string fullTrailPath = IOPath.GetFullPath(trailPath);
                 if (capturedDocumentsByTrailPath.TryGetValue(fullTrailPath, out document))
                 {
-                    DebugLogHelper.LogInfo(log, $"Using synchronously captured Trail mod settings for export [{fullTrailPath}].");
+                    DebugLogHelper.LogDebug(log, $"Using synchronously captured Trail mod settings for export [{fullTrailPath}].");
                     return true;
                 }
                 string sidecar = MissionLoader.GetTrailModSettingsPath(fullTrailPath);
@@ -1919,6 +1925,22 @@ namespace ExtendedData
                     Directory.Delete(trailMakerSource, true);
             }
 
+            private void ShowExportFailure()
+            {
+                try
+                {
+                    HUD_ConfirmationPopup.ShowConfirmationOKMessage(
+                        SerpLocalization.Get("ExtendedData.ExportFailedTitle"), delegate { },
+                        SerpLocalization.Get("ExtendedData.ExportFailedLog"));
+                    MainViewModel.Instance.Show_HUD_Confirmation = false;
+                    MainViewModel.Instance.Show_HUD_ConfirmationMP = true;
+                    MainViewModel.Instance.FrontEndMenu.UpdateFrontMenuPopupScale();
+                }
+                catch (Exception exception)
+                {
+                    DebugLogHelper.LogError(log, "Could not display the Trail export error popup: " + exception);
+                }
+            }
             private static void ShowInformation(string title, string message)
             {
                 HUD_ConfirmationPopup.ShowConfirmationOKMessage(title, delegate { }, message);
@@ -2147,7 +2169,7 @@ namespace ExtendedData
                     // The lobby uses the original map header, while Vanilla's Custom Trail
                     // launch path requires the .trail container header in the restart payload.
                     customTrailRestartInfo.selectedHeader = customTrailSetupHeader;
-                    DebugLogHelper.LogInfo(
+                    DebugLogHelper.LogDebug(
                         log,
                         $"Starting customized Custom Trail [{customTrailRestartInfo.customTrailName}] " +
                         $"mission {customTrailRestartInfo.customTrailLevel}.");
@@ -2176,7 +2198,7 @@ namespace ExtendedData
                                     TrailLordSelectionPolicy.UsesEmbeddedLord(slot,
                                         info.builtInLord, info.lordName,
                                         LordDataCoordinator.PreparedTrailMediaAlias(
-                                            requirements, slot.PlayerId)))
+                                            requirements, slot.PlayerId), info.lordType))
                                 .Select(slot => slot.PlayerId));
                             PrepareCustomTrailLords(requirements, infos, true);
                             LordDataCoordinator.RemapTrailMedia(infos);
@@ -2572,7 +2594,7 @@ namespace ExtendedData
                 // doOpen can trigger unrelated context cleanup; apply the selected mission again
                 // after all lobby view models exist so Trail is visible and selected immediately.
                 EnterSidecar(header.filePath, editable: true);
-                DebugLogHelper.LogInfo(
+                DebugLogHelper.LogDebug(
                     log,
                     $"Opened Custom Trail setup [{menus.CustomTrailName}] mission {missionId}; " +
                     $"map=[{lobbyMapHeader.display_filename}], path=[{lobbyMapHeader.filePath}].");
@@ -2944,7 +2966,7 @@ namespace ExtendedData
                 // ShowSetupScreen rebuilds lobby settings. Reapply the selected mission only
                 // after that transition, matching the working Custom Trail Customize path.
                 CoopSetupOpened?.Invoke();
-                DebugLogHelper.LogInfo(log, $"Opened Coop Trail setup trail={trailId + 1}, mission={mission}, source={source}.");
+                DebugLogHelper.LogDebug(log, $"Opened Coop Trail setup trail={trailId + 1}, mission={mission}, source={source}.");
             }
 
             private void BroadcastCoopCustomize(int trailId, int missionId)
@@ -2984,7 +3006,7 @@ namespace ExtendedData
                     dataLength = bytes.Length,
                     dataOffset = 0,
                 });
-                DebugLogHelper.LogInfo(
+                DebugLogHelper.LogDebug(
                     log,
                     $"Broadcast Built-in Customize launch origin: trailType={packet.TrailType}, " +
                     $"missionIndex={packet.MissionId}, packetId={builtInCustomizeOriginPacketId}.");
@@ -3012,7 +3034,7 @@ namespace ExtendedData
                     dataLength = bytes.Length,
                     dataOffset = 0,
                 });
-                DebugLogHelper.LogInfo(
+                DebugLogHelper.LogDebug(
                     log,
                     $"Broadcast Coop Trail {(launch ? "launch" : "setup")} transition " +
                     $"trail={trailId + 1}, mission={missionId}, packetId={coopCustomizePacketId}.");
@@ -3132,7 +3154,7 @@ namespace ExtendedData
                     }
 
                     CaptureBuiltInCustomizeOrigin(packet.TrailType, packet.MissionId);
-                    DebugLogHelper.LogInfo(
+                    DebugLogHelper.LogDebug(
                         log,
                         $"Accepted Built-in Customize launch origin from host: trailType={packet.TrailType}, " +
                         $"missionIndex={packet.MissionId}.");
@@ -3197,7 +3219,7 @@ namespace ExtendedData
                         source = "new mission defaults";
                     }
                     missionPresetLifecycle.CompleteTrailMakerReturn();
-                    DebugLogHelper.LogInfo(
+                    DebugLogHelper.LogDebug(
                         log,
                         "Activated editable Trail Maker mod-settings context from " + source + ".");
                 }
@@ -3213,7 +3235,7 @@ namespace ExtendedData
                             preserveCurrentValues: true);
                         UpdateTrailMakerWorkingDocument(CaptureDocument(), null);
                         missionPresetLifecycle.CompleteTrailMakerReturn();
-                        DebugLogHelper.LogInfo(
+                        DebugLogHelper.LogDebug(
                             log,
                             "Activated editable Trail Maker mod-settings context from fail-closed defaults.");
                     }
@@ -3246,7 +3268,7 @@ namespace ExtendedData
                         ? CaptureDocument()
                         : trailMakerWorkingDocument ?? ModSettingsDefinition.CreateModDefaults();
                     UpdateTrailMakerWorkingDocument(document, trailMakerTrailPath);
-                    DebugLogHelper.LogInfo(
+                    DebugLogHelper.LogDebug(
                         log,
                         "Captured editable Trail Maker mod-settings draft before " + transition + ".");
                 }
@@ -3318,7 +3340,7 @@ namespace ExtendedData
                 ApplyDocument(document, editable, useFixedDefaults: !exists && editable,
                     previewOnly: previewOnly, preserveCurrentValues: editable);
                 string[] mentionedMods = document.Mods.Keys.ToArray();
-                DebugLogHelper.LogInfo(
+                DebugLogHelper.LogDebug(
                     log,
                     $"Loaded Trail sidecar [{sidecar}]; exists={exists}, editable={editable}, " +
                     "mentioned=[" + string.Join(", ", mentionedMods) + "].");
@@ -3394,7 +3416,7 @@ namespace ExtendedData
                         properties.Keys);
                     if (removedSettings.Length != 0)
                     {
-                        DebugLogHelper.LogInfo(
+                        DebugLogHelper.LogDebug(
                             log,
                             $"Ignored obsolete Map/Trail settings for [{participant.Key}]: " +
                             string.Join(", ", removedSettings) + ". They will be omitted on the next save.");

diff --git a/ExtendedData/Test-RuntimePreflight.ps1 b/ExtendedData/Test-RuntimePreflight.ps1
index b57cb245e..f37847cfe 100644
--- a/ExtendedData/Test-RuntimePreflight.ps1
+++ b/ExtendedData/Test-RuntimePreflight.ps1
@@ -5,9 +5,9 @@ param(
 
 $ErrorActionPreference = 'Stop'
 $workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
-. (Join-Path $workspace 'Shared\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')
+. (Join-Path $workspace 'Shared\Tools\ScriptExtenderUpdate\ScriptExtenderUpdate.Common.ps1')
 
-$mods = Get-Content -Raw -LiteralPath (Join-Path $workspace 'Shared\ScriptExtenderUpdate\mods.json') | ConvertFrom-Json
+$mods = Get-Content -Raw -LiteralPath (Join-Path $workspace 'Shared\Tools\ScriptExtenderUpdate\mods.json') | ConvertFrom-Json
 # Windows PowerShell 5.1 preserves a top-level JSON array as one pipeline object,
 # while newer PowerShell versions enumerate it. A foreach statement handles both forms.
 $mod = @(foreach ($entry in $mods) {
@@ -138,6 +138,11 @@ try {
         @('CrusaderDE.FRONT_Multiplayer/MPAIVInfo', 'lordConfig', 'CustomisationFileManager/CustomLordConfig'),
         @('CustomisationFileManager/CustomLordConfig', 'lordType', 'System.Int32'),
         @('CustomisationFileManager/CustomLordConfig', 'name', 'System.String'),
+        @('CustomisationFileManager/CustomLordConfig', 'path', 'System.String'),
+        @('CrusaderDE.FRONT_Multiplayer/MPAIVInfo', 'lordType', 'System.Int32'),
+        @('CrusaderDE.FRONT_Multiplayer/MPAIVInfo', 'lordName', 'System.String'),
+        @('ConfigSettings', 'extendedLordPaths', 'System.String[]'),
+        @('CrusaderDE.MainViewModel', 'FrontEndMenu', 'CrusaderDE.FrontendMenus'),
         @('CustomisationFileManager/CustomLordConfig', 'checksum', 'System.UInt64')
     )) {
         $parts = [string[]]$contract
@@ -161,6 +166,15 @@ try {
         $_.Parameters[1].ParameterType.FullName -ceq 'System.String'
     })
     if ($lookup.Count -ne 1) { throw 'Managed Custom Lord lookup contract changed.' }
+    $viewModel = $managedAssembly.MainModule.Types | Where-Object FullName -CEQ 'CrusaderDE.MainViewModel'
+    foreach ($name in @('Show_HUD_Confirmation', 'Show_HUD_ConfirmationMP')) {
+        $property = @($viewModel.Properties | Where-Object Name -CEQ $name)
+        if ($property.Count -ne 1 -or $property[0].PropertyType.FullName -cne 'System.Boolean' -or
+            -not $property[0].SetMethod.IsPublic -or $property[0].SetMethod.IsStatic) {
+            throw "Trail export popup property contract changed: $name"
+        }
+    }
+    Assert-ManagedMethodContract $managedAssembly 'CrusaderDE.FrontendMenus' 'UpdateFrontMenuPopupScale' @() 'Public'
     Assert-ManagedMethodContract $managedAssembly 'EditorDirector' 'SaveSaveGameOrMap' @(
         'System.String', 'System.String', 'System.Boolean', 'System.Boolean', 'System.Boolean') 'Public'
 }

diff --git a/Shared/Adapters/APIShared/MissionEventsAdapter.cs b/Shared/Adapters/APIShared/MissionEventsAdapter.cs
new file mode 100644
index 000000000..ab1eb5f82
--- /dev/null
+++ b/Shared/Adapters/APIShared/MissionEventsAdapter.cs
@@ -0,0 +1,154 @@
+using APIShared.GameModes;
+using APIShared;
+using BepInEx;
+using BepInEx.Logging;
+using R3;
+using System;
+using System.Collections.Generic;
+using System.Linq;
+
+namespace Shared
+{
+    internal enum GameplaySessionStartKind { NewMap, LoadedSave, EditorCreated, EditorLoaded }
+    internal sealed class GameplaySessionStartedContext
+    {
+        internal GameplaySessionStartedContext(MissionLifecycleNotification notification) { Notification = notification; }
+        internal MissionLifecycleNotification Notification { get; }
+        internal GameplaySessionStartKind Kind => (GameplaySessionStartKind)Notification.Context.StartKind;
+        internal GameModeSnapshot Mode => Notification.Context.Mode;
+        internal bool IsLoadedSave => Notification.Context.IsSave;
+        internal bool IsEditor => Notification.Context.IsEditor;
+        internal bool LoadingEditorMap => Kind == GameplaySessionStartKind.EditorLoaded;
+        internal long SessionId => Notification.Context.SessionId;
+        internal bool IsReplay => Notification.IsReplay;
+        internal string SaveFileName => Notification.Context.FilePath;
+    }
+
+    // Per-assembly delivery only. Detection and mode decisions live in APIShared.
+    internal static class MissionEvents
+    {
+        private static readonly List<Action<MissionLifecycleNotification>> observers = new List<Action<MissionLifecycleNotification>>();
+        private static IMissionLifecycleCapability capability;
+        private static Action<MissionLifecycleNotification> priority;
+        private static MissionLifecycleNotification latest;
+        private static string ownerGuid;
+#if API_SHARED_PRESET_TESTS
+        private static readonly ManualLogSource log = null;
+#else
+        private static readonly ManualLogSource log = BepInEx.Logging.Logger.CreateLogSource("Mission adapter");
+#endif
+        internal static void SetOwner(string owner) { ownerGuid = owner; }
+        internal static void SetGate(Action<MissionLifecycleNotification> gate)
+        {
+            bool connected = capability != null;
+            priority = gate;
+            EnsureConnected();
+            if (connected && latest != null) gate(latest);
+        }
+        private static void EnsureConnected()
+        {
+#if !API_SHARED_PRESET_TESTS
+            if (capability != null) return;
+            string owner = ownerGuid ?? typeof(MissionEvents).Assembly.GetTypes()
+                .SelectMany(t => t.GetCustomAttributes(typeof(BepInPlugin), false).Cast<BepInPlugin>())
+                .Select(a => a.GUID).First();
+            if (!ApiShared.Current.TryGetMissionLifecycle(owner, out var service, out var diagnostic))
+                throw new InvalidOperationException("Mission lifecycle unavailable: " + diagnostic?.Reason);
+            capability = service;
+            if (!service.TryRegisterObserver("Shared.MissionEvents." + typeof(MissionEvents).Assembly.GetName().Name, Deliver, Deliver, Deliver, out diagnostic))
+            { capability = null; throw new InvalidOperationException("Mission lifecycle registration failed: " + diagnostic?.Reason); }
+#endif
+        }
+#if API_SHARED_PRESET_TESTS
+        internal static void ResetForTests() { observers.Clear(); latest = null; priority = null; capability = null; }
+        internal static void PublishForTests(MissionLifecycleNotification e) => Deliver(e);
+#endif
+        private static void Deliver(MissionLifecycleNotification notification)
+        {
+            latest = notification.Kind == MissionLifecycleKind.End ? null : notification;
+            try { priority?.Invoke(notification); }
+            catch (Exception ex) { Report("Mission gate callback failed: ", ex); }
+            foreach (var callback in observers.ToArray())
+            {
+                try { callback(notification); }
+                catch (Exception ex) { Report("Mission subscriber failed: ", ex); }
+            }
+        }
+        private static void Report(string message, Exception exception)
+        {
+            try { DebugLogHelper.LogError(log, message + exception); } catch { }
+        }
+        private static Observable<MissionLifecycleNotification> Observe(Func<MissionLifecycleNotification, bool> predicate, bool replay = false) =>
+            new RelayObservable(predicate, replay);
+        private sealed class RelayObservable : Observable<MissionLifecycleNotification>
+        {
+            private readonly Func<MissionLifecycleNotification, bool> predicate;
+            private readonly bool replay;
+            internal RelayObservable(Func<MissionLifecycleNotification, bool> predicate, bool replay)
+            { this.predicate = predicate; this.replay = replay; }
+            protected override IDisposable SubscribeCore(Observer<MissionLifecycleNotification> observer)
+            {
+                long deliveredStart = 0;
+                Action<MissionLifecycleNotification> callback = e =>
+                {
+                    if (!predicate(e)) return;
+                    if (e.Kind == MissionLifecycleKind.Start)
+                    {
+                        if (deliveredStart == e.Context.SessionId) return;
+                        deliveredStart = e.Context.SessionId;
+                    }
+                    observer.OnNext(e);
+                };
+                observers.Add(callback);
+                try
+                {
+                    EnsureConnected();
+                    if (replay && latest?.Kind == MissionLifecycleKind.Start)
+                        callback(latest.AsReplay());
+                    return new LocalSubscription(callback);
+                }
+                catch { observers.Remove(callback); throw; }
+            }
+        }
+        internal static Observable<MissionLifecycleNotification> Started => Observe(e => e.Kind == MissionLifecycleKind.Start, true);
+        internal static Observable<MissionLifecycleNotification> Ended => Observe(e => e.Kind == MissionLifecycleKind.End);
+        internal static Observable<MissionLifecycleNotification> Initialization => Observe(e => e.Kind == MissionLifecycleKind.Initialization);
+        internal static Observable<MissionLifecycleNotification> NativeStart => Observe(e => e.Kind == MissionLifecycleKind.Initialization &&
+            (e.Phase == MissionInitializationPhase.BeforeNativeStart || e.Phase == MissionInitializationPhase.AfterNativeStart));
+        internal static Observable<MissionLifecycleNotification> Loading => Observe(e => e.Kind == MissionLifecycleKind.Initialization &&
+            (e.Phase == MissionInitializationPhase.BeforeLoad || e.Phase == MissionInitializationPhase.NativeLoaded));
+        internal static Observable<MissionLifecycleNotification> SaveLoading => Loading.Where(e => e.Context.IsSave);
+        private sealed class LocalSubscription : IDisposable
+        {
+            private Action<MissionLifecycleNotification> callback;
+            internal LocalSubscription(Action<MissionLifecycleNotification> callback) { this.callback = callback; }
+            public void Dispose() { observers.Remove(callback); callback = null; }
+        }
+    }
+
+    internal static class GameplaySessionLifecycle
+    {
+        internal static IDisposable SubscribeStarted(ManualLogSource log, Action<GameplaySessionStartedContext> callback,
+            Action onEnded = null)
+        {
+            var start = MissionEvents.Started.Subscribe(e =>
+            {
+                try { callback(new GameplaySessionStartedContext(e)); }
+                catch (Exception ex) { DebugLogHelper.LogError(log, "Mission start subscriber failed: " + ex); }
+            });
+            if (onEnded == null) return start;
+            var end = MissionEvents.Ended.Subscribe(_ =>
+            {
+                try { onEnded?.Invoke(); }
+                catch (Exception ex) { DebugLogHelper.LogError(log, "Mission end subscriber failed: " + ex); }
+            });
+            return new Pair(start, end);
+        }
+        private sealed class Pair : IDisposable
+        {
+            private readonly IDisposable start, end;
+            internal Pair(IDisposable start, IDisposable end) { this.start = start; this.end = end; }
+            public void Dispose() { start.Dispose(); end.Dispose(); }
+        }
+    }
+}

diff --git a/Shared/Adapters/APIShared/PlayerIdentityHelper.cs b/Shared/Adapters/APIShared/PlayerIdentityHelper.cs
new file mode 100644
index 000000000..cec12fa15
--- /dev/null
+++ b/Shared/Adapters/APIShared/PlayerIdentityHelper.cs
@@ -0,0 +1,431 @@
+using APIShared.GameModes;
+using SHCDESE.API;
+using SHCDESE.EventAPI.MapLoader;
+using CrusaderDE;
+using System;
+using System.Collections.Generic;
+using System.Linq;
+using System.Reflection;
+#if !API_SHARED_PRESET_TESTS
+using Steamworks;
+#endif
+
+namespace Shared
+{
+    internal readonly struct PlayerIdentityResolution
+    {
+        internal PlayerIdentityResolution(int playerId, bool isResolved, string error, string diagnostic)
+        {
+            PlayerId = playerId;
+            IsResolved = isResolved;
+            Error = error ?? string.Empty;
+            Diagnostic = diagnostic ?? string.Empty;
+        }
+
+        internal int PlayerId { get; }
+        internal bool IsResolved { get; }
+        internal string Error { get; }
+        internal string Diagnostic { get; }
+    }
+
+    internal static class PlayerIdentityHelper
+    {
+        // Resolve the local player only from sources whose slot semantics are known.
+        // The GameNetworkAPI local-player getter reads the same managed rosters and logs a
+        // warning whenever they are still transitional, so it must not be used as an
+        // additional fallback from persistent lobby observers.
+        private const int FirstPlayerId = 1;
+        private const int LastPlayerId = 8;
+
+        internal static PlayerIdentityResolution ResolveLocalPlayerId(
+            bool realMultiplayer,
+            bool hasInGameHumanRoster,
+            int nativePlayerId,
+            int gameMemberPlayerId,
+            int lobbyPlayerId)
+        {
+            bool nativeValid = IsValidPlayerId(nativePlayerId);
+            bool gameMemberValid = IsValidPlayerId(gameMemberPlayerId);
+            bool lobbyValid = IsValidPlayerId(lobbyPlayerId);
+
+            if (realMultiplayer && hasInGameHumanRoster)
+            {
+                if (nativeValid && gameMemberValid && nativePlayerId != gameMemberPlayerId)
+                {
+                    return Failure(
+                        $"Authoritative local player ID mismatch: native={nativePlayerId}, gameMember={gameMemberPlayerId}, " +
+                        $"lobby={lobbyPlayerId}.");
+                }
+
+                int authoritative = nativeValid ? nativePlayerId : gameMemberPlayerId;
+                if (!IsValidPlayerId(authoritative))
+                {
+                    return Failure(
+                        $"No authoritative local player ID is available in the active multiplayer roster: " +
+                        $"native={nativePlayerId}, gameMember={gameMemberPlayerId}, lobby={lobbyPlayerId}.");
+                }
+                if (lobbyValid && lobbyPlayerId != authoritative)
+                {
+                    return Failure(
+                        $"Final lobby mapping disagrees with the authoritative local player ID: " +
+                        $"authoritative={authoritative}, lobby={lobbyPlayerId}.");
+                }
+
+                return Success(authoritative, string.Empty);
+            }
+
+            if (realMultiplayer)
+            {
+                if (lobbyValid)
+                    return Success(lobbyPlayerId, string.Empty);
+                return Failure($"No local multiplayer player ID is available yet: lobby={lobbyPlayerId}.");
+            }
+
+            if (nativeValid)
+                return Success(nativePlayerId, string.Empty);
+            if (gameMemberValid)
+                return Success(gameMemberPlayerId, string.Empty);
+            if (lobbyValid)
+                return Success(lobbyPlayerId, string.Empty);
+            return Failure("No valid local player ID is available.");
+        }
+
+        internal static PlayerIdentityResolution ResolvePlayerIdForSteamId(
+            ulong steamId,
+            IReadOnlyDictionary<int, ulong> playersById)
+        {
+            if (steamId == 0)
+                return Failure("The requested Steam identity is invalid.");
+
+            var normalized = new Dictionary<int, ulong>();
+            foreach (KeyValuePair<int, ulong> player in
+                playersById ?? new Dictionary<int, ulong>())
+            {
+                if (!TryAddPlayer(normalized, player.Key, player.Value, out string error))
+                    return Failure(error);
+            }
+
+            int[] matches = normalized
+                .Where(player => player.Value == steamId)
+                .Select(player => player.Key)
+                .ToArray();
+            if (matches.Length != 1)
+            {
+                return Failure(
+                    matches.Length == 0
+                        ? $"Steam identity {steamId} is not part of the resolved human roster."
+                        : $"Steam identity {steamId} is assigned to multiple player slots.");
+            }
+            return Success(matches[0], string.Empty);
+        }
+
+        internal static PlayerIdentityResolution ResolveAuthenticatedPerPlayerTarget(
+            ulong senderSteamId,
+            int payloadPlayerId,
+            IReadOnlyDictionary<int, ulong> playersById)
+        {
+            PlayerIdentityResolution resolution = ResolvePlayerIdForSteamId(
+                senderSteamId,
+                playersById);
+            if (!resolution.IsResolved || resolution.PlayerId == payloadPlayerId)
+                return resolution;
+            return Success(
+                resolution.PlayerId,
+                $"The per-player payload claimed slot {payloadPlayerId}, but authenticated " +
+                $"Steam identity {senderSteamId} belongs to final slot {resolution.PlayerId}.");
+        }
+
+#if !API_SHARED_PRESET_TESTS
+        internal static PlayerIdentityResolution CaptureLocalPlayerId(
+            bool preferInGameRoster) =>
+            CaptureLocalPlayerId(
+                GameModeHelper.IsRealMultiplayer(),
+                preferInGameRoster);
+
+        internal static PlayerIdentityResolution CaptureLocalPlayerId(
+            bool realMultiplayer,
+            bool preferInGameRoster)
+        {
+            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
+            ulong localSteamId = 0;
+            try
+            {
+                localSteamId = SteamUser.GetSteamID().m_SteamID;
+            }
+            catch
+            {
+                // Steam can be unavailable during early singleplayer initialization.
+            }
+
+            Platform_Multiplayer.MPGameMember[] humanGameMembers = platform?.gameMembers?
+                .Where(member => member != null && !member.kicked && !member.skirmishAI)
+                .ToArray() ?? Array.Empty<Platform_Multiplayer.MPGameMember>();
+            int gameMemberPlayerId = humanGameMembers
+                .Where(member => localSteamId != 0 && member.steamID == localSteamId)
+                .Select(member => member.playerID)
+                .FirstOrDefault();
+
+            int nativePlayerId = 0;
+            try
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/ExtendedData.diff).
