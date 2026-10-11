# CastlePlanner release status

**Status:** code newer

- Release: [v0.8.36](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/CastlePlanner/v0.8.36)
- Release commit: [a1ab5dc](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/a1ab5dc8ccce17b7c2b7c6ce754dc92503f40a5a)
- Current main commit: [2c95ad4](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/2c95ad4c3cefecb00bdadc503704e56a025a37f7)

## Relevant changed files

- `APIShared`
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/AIVParser.Core.dll`
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/AIVParser.Core.pdb`
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/AIVPlacement.Core.dll`
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/AIVPlacement.Core.pdb`
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/CastlePlanner.AIVPlacement.Core.dll`
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/CastlePlanner.AIVPlacement.Core.pdb`
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/CastlePlanner.dll`
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/CastlePlanner.pdb`
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/info.json`
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/MapParser.Core.dll`
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/MapParser.Core.pdb`
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Override/ScriptExtenderUI/CastlePlannerSettings.xaml`
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Patches/Assets/GUI/XAML/IngameUIScreens.xaml`
- `CastlePlanner/build.bat`
- `CastlePlanner/CastlePlanner.csproj`
- `CastlePlanner/src/AIVPlacement/AivPlacementRuntime.cs`
- `CastlePlanner/src/AIVPlacement/AivSelectionDialogRuntime.cs`
- `CastlePlanner/src/BlueprintBuildingCaptureCatalog.cs`
- `CastlePlanner/src/BlueprintBuildingIconCatalog.cs`
- `CastlePlanner/src/BlueprintBuildingImageLibrary.cs`
- `CastlePlanner/src/BlueprintBuildingSizeCalibration.cs`
- `CastlePlanner/src/BlueprintHudViewModel.cs`
- `CastlePlanner/src/BlueprintRenderer.cs`
- `CastlePlanner/src/BlueprintRuntimeController.cs`
- `CastlePlanner/src/CastleDropDownHeightController.cs`
- `CastlePlanner/src/CastlePlannerPlugin.cs`
- `CastlePlanner/src/CastlePlannerRuntime.cs`
- `CastlePlanner/src/CastlePlannerSettingsViewModel.cs`
- `CastlePlanner/src/FixesKeepRotationCompatibility.cs`
- `CastlePlanner/src/FreeCastlePreviewRuntime.cs`
- `Shared/Adapters/APIShared/DirectLaunchSettingsNotice.cs`
- `Shared/Adapters/APIShared/GameplayFeatureModePolicy.cs`
- `Shared/Adapters/APIShared/GameplayModActivationGate.cs`
- `Shared/Adapters/APIShared/MissionEventsAdapter.cs`
- `Shared/Adapters/APIShared/PlayerIdentityHelper.cs`
- `Shared/Adapters/APIShared/SerpsModProfiles.cs`
- `Shared/Runtime/Diagnostics/DebugLogHelper.cs`
- `Shared/Runtime/Gameplay/ActivePlayerHelper.cs`
- `Shared/Runtime/Localization/SerpLocalization.cs`
- `Shared/Runtime/Native/GameBuildingFootprint.cs`
- `Shared/Runtime/Native/NativePatternResolver.cs`
- `Shared/Runtime/Persistence/DependencyFreeJson.cs`
- `Shared/Runtime/Threading/UnityMainThreadDispatch.cs`
- `Shared/Runtime/UI/AiSettingsHelpHover.cs`
- `Shared/Runtime/UI/ToolTipPresentation.cs`
- `Shared/Runtime/Workshop/WorkshopContentPaths.cs`

The localization helper also contains a general logic change that affects every consumer.

## Diff

```diff
diff --git a/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/info.json b/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/info.json
index 1a9ef7cab..6ee1272ac 100644
--- a/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/info.json
+++ b/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/info.json
@@ -3,13 +3,38 @@
   "Author": "Serpens66",
   "Name": "CastlePlanner",
   "Description": "Displays AIVJSON blueprints, spawns personal human castles, and contains the gated AI lobby placement workflow.",
-  "Version": "0.8.36",
+  "Version": "0.8.40",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "MinimumScriptExtenderVersion": "2.10.4",
   "MaximumScriptExtenderVersion": "",
   "Manifest": 1,
   "NetworkMode": 1,
   "SerpChangelog": [
+    {
+      "Version": "0.8.40",
+      "Changes": [
+        "Use APIShared 0.6.0 coordinated hooks and event contracts to improve compatibility with other mods; preserve existing gameplay behavior."
+      ]
+    },
+    {
+      "Version": "0.8.39",
+      "Changes": [
+        "Migrate to APIShared 0.5.0 shared services and updated mod-owned adapters while preserving existing features."
+      ]
+    },
+    {
+      "Version": "0.8.38",
+      "Changes": [
+        "Move routine diagnostics to Debug; preserve warnings, errors and explicit file-operation results."
+      ]
+    },
+    {
+      "Version": "0.8.37",
+      "Changes": [
+        "Corrected drawbridge blueprint mirroring across composite, depth-atlas and fallback images.",
+        "Made drawbridge view selection independent of terrain and flattened-view heights."
+      ]
+    },
     {
       "Version": "0.8.36",
       "Changes": [
@@ -890,5 +915,11 @@
         "Replaced CastlePlanner's duplicate multiplayer and skirmish classification with the shared GameModeHelper."
       ]
     }
+  ],
+  "Dependencies": [
+    {
+      "GUID": "APIShared_Serp",
+      "MinimumVersion": "0.6.0"
+    }
   ]
 }

diff --git a/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Override/ScriptExtenderUI/CastlePlannerSettings.xaml b/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Override/ScriptExtenderUI/CastlePlannerSettings.xaml
index 788186d0e..3cef53933 100644
--- a/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Override/ScriptExtenderUI/CastlePlannerSettings.xaml
+++ b/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Override/ScriptExtenderUI/CastlePlannerSettings.xaml
@@ -5,6 +5,7 @@
       xmlns:sys="clr-namespace:System;assembly=mscorlib"
       xmlns:shared="clr-namespace:Shared;assembly=APIShared"
       xmlns:seui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
+      shared:ModSettingsMode.Availability="{Binding System_ModeAvailability}"
       shared:ModSettingsSearch.FilterText="{Binding System_ModSettingsSearchText}"
       shared:ModSettingsSearch.IncludeToolTips="{Binding System_ModSettingsSearchIncludeToolTips}"
       shared:ModSettingsSearch.ExactKey="{Binding System_ModSettingsSearchExactKey}">
@@ -16,7 +17,7 @@
     <Style x:Key="HostActivationBorder" TargetType="{x:Type Border}"><Setter Property="Background" Value="#443B6EA5"/><Setter Property="BorderBrush" Value="#FF77AAFF"/><Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="3"/><Setter Property="Padding" Value="8,4"/></Style>
     <Style x:Key="ClientActivationBorder" TargetType="{x:Type Border}"><Setter Property="Background" Value="#44306950"/><Setter Property="BorderBrush" Value="#FF66CC99"/><Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="3"/><Setter Property="Padding" Value="8,4"/></Style>
     <shared:ModSettingsSearchVisibilityConverter x:Key="ModSettingsSearchVisibilityConverter"/>
-    <Style x:Key="ModSettingsSearchTargetGrid" TargetType="{x:Type Grid}"><Setter Property="Visibility"><Setter.Value><MultiBinding Converter="{StaticResource ModSettingsSearchVisibilityConverter}"><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.FilterText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.IncludeToolTips)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ExactKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.Key)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.Title)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ToolTipText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/></MultiBinding></Setter.Value></Setter></Style>
+    <Style x:Key="ModSettingsSearchTargetGrid" TargetType="{x:Type Grid}"><Setter Property="shared:ModSettingsMode.Key" Value="{Binding RelativeSource={RelativeSource Self}, Path=(shared:ModSettingsSearch.Key)}"/><Setter Property="Visibility"><Setter.Value><MultiBinding Converter="{StaticResource ModSettingsSearchVisibilityConverter}"><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.FilterText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.IncludeToolTips)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ExactKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.Key)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.Title)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ToolTipText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/></MultiBinding></Setter.Value></Setter></Style>
     <Style x:Key="ModSettingsSearchSectionGrid" TargetType="{x:Type Grid}"><Setter Property="Visibility"><Setter.Value><MultiBinding Converter="{StaticResource ModSettingsSearchVisibilityConverter}"><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.FilterText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.IncludeToolTips)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ExactKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/></MultiBinding></Setter.Value></Setter></Style>
     <Style x:Key="ModSettingsSearchIcon" TargetType="{x:Type Path}"><Setter Property="Stroke" Value="#FF77AAFF"/><Style.Triggers><DataTrigger Binding="{Binding System_ModSettingsSearchHasActiveFilter}" Value="True"><Setter Property="Stroke" Value="#FFF2D48A"/></DataTrigger></Style.Triggers></Style>
   </Grid.Resources>
@@ -125,7 +126,7 @@
         <TextBlock Text="{Binding System_ModSettingsSearchNoResultsText}" Visibility="{Binding System_ModSettingsSearchNoResultsVisibility}" Foreground="#FFCC66" HorizontalAlignment="Left" Margin="0,4,0,0"/>
       </StackPanel>
     <TextBlock Text="{Binding ActionsScopeNoticeText}" Visibility="{Binding ActionsScopeNoticeVisibility}" Foreground="#BBBBBB" TextWrapping="Wrap" Margin="0,0,0,8"/>
-    <TextBlock Text="{Binding System_DirectLaunchNoticeText}" Visibility="{Binding System_DirectLaunchNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
+    <TextBlock Text="{Binding System_ConsumerModeNoticeText}" Visibility="{Binding System_ConsumerModeNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
     <TextBlock Text="{Binding System_TrailSourceNoticeText}" Visibility="{Binding System_TrailSourceNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
     <TextBlock Text="{Binding HostReadOnlyNoticeText}"
                Visibility="{Binding HostReadOnlyNoticeVisibility}"

diff --git a/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Patches/Assets/GUI/XAML/IngameUIScreens.xaml b/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Patches/Assets/GUI/XAML/IngameUIScreens.xaml
index dc54258fc..1e74e744f 100644
--- a/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Patches/Assets/GUI/XAML/IngameUIScreens.xaml
+++ b/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/Patches/Assets/GUI/XAML/IngameUIScreens.xaml
@@ -282,25 +282,4 @@
     </Content>
   </Operation>
 
-  <!-- Sharing Vanilla's extras container makes HUD hiding and report screens
-       affect this trigger exactly like the button directly below it. -->
-  <Operation Type="Add" XPath="/n:UserControl/n:Grid/n:Grid[@Name='HUD_ObjectivesPanel']">
-    <Content>
-      <Button x:Name="CastlePlannerBlueprintSettingsButton"
-              Width="36" Height="34" Margin="0,0,0,34"
-              HorizontalAlignment="Left" VerticalAlignment="Bottom"
-              Visibility="{Binding HudVisible, Converter={StaticResource booleanToVisibilityConverter}}"
-              local:PropEx.Sprite1="{StaticResource UI-Buildings A001}"
-              local:PropEx.Sprite2="{StaticResource UI-Buildings A002}"
-              local:PropEx.Sprite3="{StaticResource UI-Buildings A002}"
-              local:PropEx.Sprite4="{StaticResource UI-Buildings A001}"
-              Command="{Binding ToggleSettingsPanelCommand}"
-              CommandParameter="{Binding RelativeSource={RelativeSource Self}}"
-              Style="{StaticResource BTN_Building}">
-        <Button.RenderTransform>
-          <TranslateTransform Y="{Binding TriggerVerticalOffset}"/>
-        </Button.RenderTransform>
-      </Button>
-    </Content>
-  </Operation>
 </Patch>

diff --git a/CastlePlanner/build.bat b/CastlePlanner/build.bat
index 983d5dc70..7451c4833 100644
--- a/CastlePlanner/build.bat
+++ b/CastlePlanner/build.bat
@@ -1,4 +1,28 @@
 @echo off
+setlocal EnableExtensions
+set "BUILD_DRIVER_NOPAUSE=0"
+for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
+set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
+echo [%date% %time%] START CastlePlanner
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
@@ -8,13 +32,15 @@ set "LOCAL_SCRIPT_EXTENDER_ROOT=%PROJECT_DIR%..\shcde-script-extender"
 set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\mod_output\000shcdese"
 set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%LOCAL_SCRIPT_EXTENDER_ROOT%\src\SHCDESE.BepInEx\bin\net481"
 set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
-set "API_SHARED_DIR=%GAME_DIR%\BepInEx\plugins\APIShared_Serp"
+set "API_SHARED_DIR=%~dp0..\APIShared\BepInEx\plugins\APIShared_Serp"
 set "LOCAL_API_SHARED_DIR=%PROJECT_DIR%..\APIShared\BepInEx\plugins\APIShared_Serp"
 rem The installed release is canonical; SHCDESE_EXTENDER_DIR is the explicit override.
 if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
 rem APIShared may be supplied explicitly or by the validated workspace package.
 if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
-if not exist "%API_SHARED_DIR%\APIShared.dll" if exist "%LOCAL_API_SHARED_DIR%\APIShared.dll" set "API_SHARED_DIR=%LOCAL_API_SHARED_DIR%"
+echo [%date% %time%] PowerShell checks / build step
+powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0.." -PackageDirectory "%API_SHARED_DIR%"
+if errorlevel 1 exit /b 1
 set "LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
 set "LOCAL_SCRIPT_EXTENDER_MOD_OUTPUT=%GAME_SCRIPT_EXTENDER_DIR%"
 set "EXTENDER_DIR="
@@ -22,11 +48,11 @@ set "NO_PAUSE=0"
 if /I "%~1"=="/nopause" set "NO_PAUSE=1"
 
 rem Never touch build or installation output while the game has plugin DLLs loaded.
+echo [%date% %time%] Check that the game is closed
 powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
 if errorlevel 1 (
   echo Build und Installation abgebrochen: Stronghold Crusader Definitive Edition ist noch gestartet.
   echo Lokales Paket und installierter Mod wurden nicht veraendert.
-  if "%NO_PAUSE%"=="0" pause
   exit /b 1
 )
 
@@ -34,7 +60,6 @@ if not exist "%MSBUILD%" (
   echo MSBuild wurde nicht gefunden:
   echo !MSBUILD!
   echo.
-  if "%NO_PAUSE%"=="0" pause
   exit /b 1
 )
 
@@ -42,7 +67,6 @@ if not exist "%GAME_DIR%\BepInEx\core\BepInEx.dll" (
   echo BepInEx.dll wurde im Spielordner nicht gefunden:
   echo !GAME_DIR!\BepInEx\core\BepInEx.dll
   echo.
-  if "%NO_PAUSE%"=="0" pause
   exit /b 1
 )
 
@@ -59,7 +83,6 @@ if exist "%LOCAL_SCRIPT_EXTENDER_ROOT%\" (
     echo Baue zuerst ..\shcde-script-extender\build.bat oder entferne den Nebenordner,
     echo wenn gegen die installierte Spiel-DLL kompiliert werden soll.
     echo.
-    if "%NO_PAUSE%"=="0" pause
     exit /b 1
   )
 ) else (
@@ -70,7 +93,6 @@ if not exist "%EXTENDER_DIR%\SHCDESE.dll" (
   echo SHCDESE.dll wurde nicht gefunden:
   echo !EXTENDER_DIR!\SHCDESE.dll
   echo.
-  if "%NO_PAUSE%"=="0" pause
   exit /b 1
 )
 
@@ -78,7 +100,6 @@ if not exist "%API_SHARED_DIR%\APIShared.dll" (
   echo APIShared.dll wurde nicht gefunden:
   echo !API_SHARED_DIR!\APIShared.dll
   echo.
-  if "%NO_PAUSE%"=="0" pause
   exit /b 1
 )
 
@@ -93,6 +114,7 @@ if errorlevel 1 (
   popd
   goto build_failed
 )
+echo [%date% %time%] Compile projects
 dotnet build AIVPlacement.Tests\CastlePlanner.AIVPlacement.Tests.csproj -c Release --no-restore -m:1 -p:BuildInParallel=false
 if errorlevel 1 (
   set "BUILD_EXIT_CODE=1"
@@ -106,14 +128,17 @@ if not "%ERRORLEVEL%"=="0" (
   goto build_failed
 )
 
+echo [%date% %time%] PowerShell checks / build step
 powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Prepare-ThinPackage.ps1" -PackageDirectory "%PROJECT_DIR%BepInEx\plugins\CastlePlanner_Serp"
 if errorlevel 1 (
   popd
   goto build_failed
 )
+echo [%date% %time%] Compile projects
 "%MSBUILD%" CastlePlanner.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%"
 set "BUILD_EXIT_CODE=%ERRORLEVEL%"
 if "%BUILD_EXIT_CODE%"=="0" (
+echo [%date% %time%] PowerShell checks / build step
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Prepare-ThinPackage.ps1" -PackageDirectory "%PROJECT_DIR%BepInEx\plugins\CastlePlanner_Serp" -ValidateOnly
   if errorlevel 1 set "BUILD_EXIT_CODE=1"
 )
@@ -140,27 +165,28 @@ if "%BUILD_EXIT_CODE%"=="0" (
     if exist "!LEGACY_MAIN_HUD_PATCH!" goto copy_failed
   )
 
+echo [%date% %time%] PowerShell checks / build step
   powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%Prepare-ThinPackage.ps1" -PackageDirectory "!GAME_PLUGIN_DIR!"
   if errorlevel 1 goto copy_failed
 
   rem Overlay managed files so Script Extender Msgpack settings survive rebuilds.
+echo [%date% %time%] Copy package files
   xcopy "!LOCAL_PLUGIN_DIR!" "!GAME_PLUGIN_DIR!\" /E /I /Q /Y
   if errorlevel 1 goto copy_failed
-  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Release\Write-LocalBuildManifest.ps1" -ModName CastlePlanner
+echo [%date% %time%] PowerShell checks / build step
+  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName CastlePlanner
   if errorlevel 1 goto copy_failed
   echo Plugin kopiert; vorhandene Laufzeitdaten wurden beibehalten.
 ) else (
   echo Build fehlgeschlagen. Exit Code: %BUILD_EXIT_CODE%
 )
 echo.
-if "%NO_PAUSE%"=="0" pause
 exit /b %BUILD_EXIT_CODE%
 
 :build_failed
 echo.
 echo Build oder AIV-Placement-Tests fehlgeschlagen.
 echo.
-if "%NO_PAUSE%"=="0" pause
 exit /b 1
 
 :copy_failed
@@ -168,5 +194,4 @@ echo.
 echo Kopieren fehlgeschlagen. Ist das Spiel noch gestartet?
 echo Beende Stronghold Crusader Definitive Edition und starte build.bat erneut.
 echo.
-if "%NO_PAUSE%"=="0" pause
 exit /b 1

diff --git a/CastlePlanner/CastlePlanner.csproj b/CastlePlanner/CastlePlanner.csproj
index 8b352da4c..7c4d03f76 100644
--- a/CastlePlanner/CastlePlanner.csproj
+++ b/CastlePlanner/CastlePlanner.csproj
@@ -165,40 +165,43 @@
   </ItemGroup>
 
   <ItemGroup>
-    <Compile Include="..\Shared\GameBuildingFootprint.cs"><Link>Shared\GameBuildingFootprint.cs</Link></Compile>
-    <Compile Include="..\Shared\UnityMainThreadDispatch.cs"><Link>Shared\UnityMainThreadDispatch.cs</Link></Compile>
-    <Compile Include="..\Shared\ActivePlayerHelper.cs">
-      <Link>Shared\ActivePlayerHelper.cs</Link>
+    <Compile Include="..\Shared\Runtime\Native\GameBuildingFootprint.cs"><Link>Shared\Runtime\Native\GameBuildingFootprint.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Threading\UnityMainThreadDispatch.cs"><Link>Shared\Runtime\Threading\UnityMainThreadDispatch.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Gameplay\ActivePlayerHelper.cs">
+      <Link>Shared\Runtime\Gameplay\ActivePlayerHelper.cs</Link>
     </Compile>
-    <Compile Include="..\Shared\DebugLogHelper.cs">
-      <Link>Shared\DebugLogHelper.cs</Link>
+    <Compile Include="..\Shared\Runtime\Diagnostics\DebugLogHelper.cs">
+      <Link>Shared\Runtime\Diagnostics\DebugLogHelper.cs</Link>
     </Compile>
-    <Compile Include="..\Shared\DependencyFreeJson.cs">
-      <Link>Shared\DependencyFreeJson.cs</Link>
+    <Compile Include="..\Shared\Runtime\Persistence\DependencyFreeJson.cs">
+      <Link>Shared\Runtime\Persistence\DependencyFreeJson.cs</Link>
     </Compile>
-    <Compile Include="..\Shared\NativePatternResolver.cs">
-      <Link>Shared\NativePatternResolver.cs</Link>
+    <Compile Include="..\Shared\Runtime\Native\NativePatternResolver.cs">
+      <Link>Shared\Runtime\Native\NativePatternResolver.cs</Link>
     </Compile>
-    <Compile Include="..\Shared\GameModeHelper.cs">
-      <Link>Shared\GameModeHelper.cs</Link>
+    <Compile Include="..\Shared\Adapters\APIShared\PlayerIdentityHelper.cs">
+      <Link>Shared\Adapters\APIShared\PlayerIdentityHelper.cs</Link>
     </Compile>
-    <Compile Include="..\Shared\GameplaySessionLifecycle.cs">
-      <Link>Shared\GameplaySessionLifecycle.cs</Link>
+    <Compile Include="..\Shared\Adapters\APIShared\MissionEventsAdapter.cs">
+      <Link>Shared\Adapters\APIShared\MissionEventsAdapter.cs</Link>
     </Compile>
-    <Compile Include="..\Shared\GameplayModActivationGate.cs">
-      <Link>Shared\GameplayModActivationGate.cs</Link>
+    <Compile Include="..\Shared\Adapters\APIShared\DirectLaunchSettingsNotice.cs"><Link>Shared\Adapters\APIShared\DirectLaunchSettingsNotice.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\SerpsModProfiles.cs"><Link>Shared\Adapters\APIShared\SerpsModProfiles.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\GameplayFeatureModePolicy.cs"><Link>Shared\Adapters\APIShared\GameplayFeatureModePolicy.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\GameplayModActivationGate.cs">
+      <Link>Shared\Adapters\APIShared\GameplayModActivationGate.cs</Link>
     </Compile>
-    <Compile Include="..\Shared\SerpLocalization.cs">
-      <Link>Shared\SerpLocalization.cs</Link>
+    <Compile Include="..\Shared\Runtime\Localization\SerpLocalization.cs">
+      <Link>Shared\Runtime\Localization\SerpLocalization.cs</Link>
     </Compile>
-    <Compile Include="..\Shared\AiSettingsHelpHover.cs">
-      <Link>Shared\AiSettingsHelpHover.cs</Link>
+    <Compile Include="..\Shared\Runtime\UI\AiSettingsHelpHover.cs">
+      <Link>Shared\Runtime\UI\AiSettingsHelpHover.cs</Link>
     </Compile>
-    <Compile Include="..\Shared\ToolTipPresentation.cs">
-      <Link>Shared\ToolTipPresentation.cs</Link>
+    <Compile Include="..\Shared\Runtime\UI\ToolTipPresentation.cs">
+      <Link>Shared\Runtime\UI\ToolTipPresentation.cs</Link>
     </Compile>
-    <Compile Include="..\Shared\WorkshopContentPaths.cs">
-      <Link>Shared\WorkshopContentPaths.cs</Link>
+    <Compile Include="..\Shared\Runtime\Workshop\WorkshopContentPaths.cs">
+      <Link>Shared\Runtime\Workshop\WorkshopContentPaths.cs</Link>
     </Compile>
     <Compile Include="src\AivFileCatalog.cs" />
     <Compile Include="src\AivCandidateFilePolicy.cs" />

diff --git a/CastlePlanner/src/AIVPlacement/AivPlacementRuntime.cs b/CastlePlanner/src/AIVPlacement/AivPlacementRuntime.cs
index 0e52c70be..889d06aa6 100644
--- a/CastlePlanner/src/AIVPlacement/AivPlacementRuntime.cs
+++ b/CastlePlanner/src/AIVPlacement/AivPlacementRuntime.cs
@@ -182,7 +182,7 @@ namespace CastlePlanner.AIVPlacement
                 bool featureEnabled = isEnabled();
                 if (!lobbySetupObserved || lastLobbyFeatureEnabled != featureEnabled)
                 {
-                    Shared.DebugLogHelper.LogInfo(
+                    Shared.DebugLogHelper.LogDebug(
                         log,
                         $"AIV lobby placement: active={featureEnabled}, " +
                         $"host={self?.currentLobby?.isHost == true}.");
@@ -535,7 +535,7 @@ namespace CastlePlanner.AIVPlacement
             // Superseded generations are expected and must not become UI failures.
             if (completed.IsCanceled)
             {
-                Shared.DebugLogHelper.LogInfo(log,
+                Shared.DebugLogHelper.LogDebug(log,
                     $"AIV evaluation canceled: generation={batch.Generation}.");
                 return;
             }
@@ -552,7 +552,7 @@ namespace CastlePlanner.AIVPlacement
                 return;
             }
 
-            Shared.DebugLogHelper.LogInfo(log,
+            Shared.DebugLogHelper.LogDebug(log,
                 $"AIV evaluation completed: generation={batch.Generation}, " +
                 $"current={generations.IsCurrent(batch.Generation)}.");
 
@@ -676,7 +676,7 @@ namespace CastlePlanner.AIVPlacement
                 }
                 if (!generations.IsCurrent(result.Generation))
                 {
-                    Shared.DebugLogHelper.LogInfo(log,
+                    Shared.DebugLogHelper.LogDebug(log,
                         $"AIV evaluation discarded: generation={result.Generation}, " +
                         $"playerId={result.PlayerId}, currentGeneration={activeGeneration}.");
                     continue;
@@ -693,7 +693,7 @@ namespace CastlePlanner.AIVPlacement
                 currentResults[result.PlayerId] = result;
                 selectionDialog.Publish(result);
                 if (pendingPlayerIds.Count == 0)
-                    Shared.DebugLogHelper.LogInfo(log,
+                    Shared.DebugLogHelper.LogDebug(log,
                         $"AIV evaluation published: generation={result.Generation}, all players complete.");
                 int evaluableCandidates = result.Candidates.Count(candidate =>
                     candidate.Status != AivPlacementStatus.NotEvaluable);
@@ -714,7 +714,7 @@ namespace CastlePlanner.AIVPlacement
                             ? candidate.Selection.Variants[outcome.RotationIndex].Rotation.ToString()
                             : "none";
                     }).Distinct());
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"AIV lobby placement result: generation={result.Generation}, " +
                     $"playerId={result.PlayerId}, preBuild={result.PreBuildSetting}, " +
@@ -846,7 +846,7 @@ namespace CastlePlanner.AIVPlacement
             evaluationCancellation = new CancellationTokenSource();
             activeGeneration = batch.Generation;
             lastEvaluationActivityTimestamp = Stopwatch.GetTimestamp();
-            Shared.DebugLogHelper.LogInfo(log,
+            Shared.DebugLogHelper.LogDebug(log,
                 $"AIV evaluation started: generation={batch.Generation}, players={batch.Requests.Count}, " +
                 $"trailMaker={frontend?.trailMakerMode == true}, retry={stalledRetries}.");
             currentResults.Clear();
@@ -906,7 +906,7 @@ namespace CastlePlanner.AIVPlacement
             if (!lobbyContextActive)
                 return;
 
-            Shared.DebugLogHelper.LogInfo(log,
+            Shared.DebugLogHelper.LogDebug(log,
                 $"AIV evaluation reset: generation={activeGeneration}, reason={reason}, " +
                 $"pendingPlayers={pendingPlayerIds.Count}.");
             lobbyContextActive = false;

diff --git a/CastlePlanner/src/AIVPlacement/AivSelectionDialogRuntime.cs b/CastlePlanner/src/AIVPlacement/AivSelectionDialogRuntime.cs
index ca7783e54..4b9291ee0 100644
--- a/CastlePlanner/src/AIVPlacement/AivSelectionDialogRuntime.cs
+++ b/CastlePlanner/src/AIVPlacement/AivSelectionDialogRuntime.cs
@@ -841,7 +841,7 @@ namespace CastlePlanner.AIVPlacement
                     string.Equals(prior, value, StringComparison.Ordinal))
                     continue;
                 publishedUiStates[key] = value;
-                Shared.DebugLogHelper.LogInfo(log,
+                Shared.DebugLogHelper.LogDebug(log,
                     $"AIV UI published route={uiRoute} player={playerId} candidate={candidateId} " +
                     $"checksum={aiv.checksum} status={state.Status?.ToString() ?? "Pending"} " +
                     $"practice={state.PercentageText} " +

diff --git a/CastlePlanner/src/BlueprintBuildingCaptureCatalog.cs b/CastlePlanner/src/BlueprintBuildingCaptureCatalog.cs
index 2c27e5cd4..3f60103dc 100644
--- a/CastlePlanner/src/BlueprintBuildingCaptureCatalog.cs
+++ b/CastlePlanner/src/BlueprintBuildingCaptureCatalog.cs
@@ -196,10 +196,10 @@ namespace CastlePlanner
             {
                 bool rear = drawbridgePosition == BlueprintDrawbridgePosition.TopLeft ||
                     drawbridgePosition == BlueprintDrawbridgePosition.TopRight;
-                // The bundled rear canonical image is the former TopRight
-                // asset; only TopLeft is derived by mirroring it.
-                bool flip = drawbridgePosition == BlueprintDrawbridgePosition.BottomRight ||
-                    drawbridgePosition == BlueprintDrawbridgePosition.TopLeft;
+                // Canonical PNGs face BottomRight (front) and TopLeft (rear).
+                // Composite, depth-fragment and fallback paths share this mapping.
+                bool flip = BlueprintBuildingIconCatalog
+                    .ResolveDrawbridgeImage(drawbridgePosition).FlipHorizontally;
                 return new BlueprintCaptureRequest(
                     mapperName,
                     skin,

diff --git a/CastlePlanner/src/BlueprintBuildingIconCatalog.cs b/CastlePlanner/src/BlueprintBuildingIconCatalog.cs
index b36882f6d..529dfc2c5 100644
--- a/CastlePlanner/src/BlueprintBuildingIconCatalog.cs
+++ b/CastlePlanner/src/BlueprintBuildingIconCatalog.cs
@@ -178,26 +178,26 @@ namespace CastlePlanner
                     [BlueprintDrawbridgePosition.BottomLeft] =
                         new BlueprintDrawbridgeImageDefinition(
                             "ST49_Drawbridge.png",
-                            false,
+                            true,
                             false,
                             false),
                     [BlueprintDrawbridgePosition.BottomRight] =
                         new BlueprintDrawbridgeImageDefinition(
                             "ST49_Drawbridge.png",
-                            true,
+                            false,
                             false,
                             false),
                     [BlueprintDrawbridgePosition.TopLeft] =
                         new BlueprintDrawbridgeImageDefinition(
                             "MAPPER_DRAWBRIDGE_Generic_DrawbridgeRear.png",
-                            true,
+                            false,
                             false,
                             true,
                             80.5f),
                     [BlueprintDrawbridgePosition.TopRight] =
                         new BlueprintDrawbridgeImageDefinition(
                             "MAPPER_DRAWBRIDGE_Generic_DrawbridgeRear.png",
-                            false,
+                            true,
                             false,
                             true,
                             80.5f)

diff --git a/CastlePlanner/src/BlueprintBuildingImageLibrary.cs b/CastlePlanner/src/BlueprintBuildingImageLibrary.cs
index 79f17c8ab..7bb3bf603 100644
--- a/CastlePlanner/src/BlueprintBuildingImageLibrary.cs
+++ b/CastlePlanner/src/BlueprintBuildingImageLibrary.cs
@@ -541,15 +541,15 @@ namespace CastlePlanner
                         missingDepth.Add(request.Key);
                 }
             }
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Blueprint capture status: available={required - missing.Count}, required={required}, " +
                 $"missing={missing.Count}, depthAtlases={required - missingDepth.Count}, " +
                 $"missingDepth={missingDepth.Count}.");
             if (missing.Count > 0)
-                Shared.DebugLogHelper.LogInfo(log, "Missing Blueprint captures: " + string.Join(", ", missing));
+                Shared.DebugLogHelper.LogWarning(log, "Missing Blueprint captures: " + string.Join(", ", missing));
             if (missingDepth.Count > 0)
-                Shared.DebugLogHelper.LogInfo(log, "Missing Blueprint depth atlases: " + string.Join(", ", missingDepth));
+                Shared.DebugLogHelper.LogWarning(log, "Missing Blueprint depth atlases: " + string.Join(", ", missingDepth));
         }
 
         private static IEnumerable<BlueprintCaptureRequest> GetRequiredRequests(string mapperName)

diff --git a/CastlePlanner/src/BlueprintBuildingSizeCalibration.cs b/CastlePlanner/src/BlueprintBuildingSizeCalibration.cs
index d90132776..8297ca6b5 100644
--- a/CastlePlanner/src/BlueprintBuildingSizeCalibration.cs
+++ b/CastlePlanner/src/BlueprintBuildingSizeCalibration.cs
@@ -141,7 +141,7 @@ namespace CastlePlanner
                 BlueprintBuildingIconCatalog
                     .CurrentCalibrationRevision);
             Save();
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Blueprint building alignment calibrated from Vanilla preview: " +
                 $"mapper={mapperName} ({mapperValue}), " +
@@ -510,7 +510,7 @@ namespace CastlePlanner
                     IsUsableMeasurement);
                 int alignedCount = measurements.Values.Count(
                     HasUsableGroundOffset);
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Loaded {measurements.Count} Blueprint building-size " +
                     $"calibrations ({usableCount} usable sizes, " +

diff --git a/CastlePlanner/src/BlueprintHudViewModel.cs b/CastlePlanner/src/BlueprintHudViewModel.cs
index 96be6b7a6..bb2dc2bb2 100644
--- a/CastlePlanner/src/BlueprintHudViewModel.cs
+++ b/CastlePlanner/src/BlueprintHudViewModel.cs
@@ -280,9 +280,6 @@ namespace CastlePlanner
                 nameof(PanelTop));
         }
 
-        public double TriggerVerticalOffset =>
-            vanillaButtonOccupiesFirstSlot ? -ButtonSlotHeight : 0.0;
-
         public bool CanToggle
         {
             get => canToggle;
@@ -359,13 +356,12 @@ namespace CastlePlanner
             OnPropertyChanged(nameof(StatusText));
         }
 
-        public void UpdateVanillaButtonSlot(bool isOccupied)
+        public void UpdateVanillaPanelAnchor(bool isOccupied)
         {
             if (vanillaButtonOccupiesFirstSlot == isOccupied)
                 return;
 
             vanillaButtonOccupiesFirstSlot = isOccupied;
-            OnPropertyChanged(nameof(TriggerVerticalOffset));
             if (!settings.TryGetBlueprintHudPosition(out _, out _))
                 ApplyStoredOrDefaultPosition();
         }

diff --git a/CastlePlanner/src/BlueprintRenderer.cs b/CastlePlanner/src/BlueprintRenderer.cs
index 47e255ed0..93792592b 100644
--- a/CastlePlanner/src/BlueprintRenderer.cs
+++ b/CastlePlanner/src/BlueprintRenderer.cs
@@ -227,7 +227,7 @@ namespace CastlePlanner
                 }
             }
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Blueprint first visible output prepared: tiles={renderedTiles}, " +
                 $"icons={renderedIcons}, depthReady={CompletedDepthCaptureCount}/" +
@@ -402,7 +402,7 @@ namespace CastlePlanner
             if (progressiveCompletionLogged || progressiveLoadTimer == null || IsDepthLoading)
                 return;
             progressiveCompletionLogged = true;
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Blueprint progressive rendering complete: depthCaptures=" +
                 $"{CompletedDepthCaptureCount}/{RequestedDepthCaptureCount}, " +
@@ -900,7 +900,7 @@ namespace CastlePlanner
                 Object.DontDestroyOnLoad(texture);
                 Object.DontDestroyOnLoad(sprite);
                 helpImageSprites.Add(cacheKey, sprite);
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Loaded bundled directional Drawbridge Blueprint: " +
                     $"file='{fileName}', size={texture.width}x{texture.height}, " +
@@ -922,7 +922,7 @@ namespace CastlePlanner
         private BlueprintDrawbridgePosition ResolveDrawbridgePosition(
             BlueprintIconPlacement placement)
         {
-            if (placement.MapperValue != 105)
+            if (placement.MapperValue != (int)eMappers.MAPPER_DRAWBRIDGE)
                 return BlueprintDrawbridgePosition.NotApplicable;
             if (!placement.AdjacentGateCenter.HasValue)
                 return BlueprintDrawbridgePosition.Unknown;
@@ -933,11 +933,11 @@ namespace CastlePlanner
                 (placement.MinimumWorldY + placement.MaximumWorldY) / 2;
             BlueprintWorldTile gateCenter =
                 placement.AdjacentGateCenter.Value;
-            if (!TryGetGroundPosition(
+            if (!TryGetPlanarPosition(
                     bridgeCenterX,
                     bridgeCenterY,
                     out Vector3 bridgePosition) ||
-                !TryGetGroundPosition(
+                !TryGetPlanarPosition(
                     gateCenter.X,
                     gateCenter.Y,
                     out Vector3 gatePosition))
@@ -1000,6 +1000,27 @@ namespace CastlePlanner
             return highPosition.x < lowPosition.x;
         }
 
+        private bool TryGetPlanarPosition(
+            int worldX,
+            int worldY,
+            out Vector3 position)
+        {
+            if (!TryGetRenderedTile(
+                    worldX,
+                    worldY,
+                    out GameMapTile mapTile,
+                    out Vector3Int tilePosition))
+            {
+                position = default;
+                return false;
+            }
+
+            // Direction follows the camera-rotated grid, never terrain height
+            // or flattened-view height. Ground positioning remains separate.
+            position = mapTile.tilemapRef.GetCellCenterWorld(tilePosition);
+            return true;
+        }
+
         private bool TryGetGroundPosition(
             int worldX,
             int worldY,
@@ -1157,7 +1178,7 @@ namespace CastlePlanner
                 Object.DontDestroyOnLoad(texture);
                 Object.DontDestroyOnLoad(sprite);
                 helpImageSprites.Add(fileName, sprite);
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Loaded clean Vanilla help image for Blueprint: " +
                     $"file='{fileName}', source={texture.width}x{texture.height}, " +
@@ -1416,7 +1437,7 @@ namespace CastlePlanner
                 SpriteMeshType.FullRect);
             sprite.name = "CastlePlanner_" +
                 resourceKey.Replace(' ', '_');
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Vanilla build-menu atlas linked: resource='{resourceKey}', " +
                 $"uri='{uri}', crop={crop}, unityRect={spriteRect}, " +

diff --git a/CastlePlanner/src/BlueprintRuntimeController.cs b/CastlePlanner/src/BlueprintRuntimeController.cs
index a6bc0c719..b4ae3ea76 100644
--- a/CastlePlanner/src/BlueprintRuntimeController.cs
+++ b/CastlePlanner/src/BlueprintRuntimeController.cs
@@ -1,3 +1,5 @@
+using APIShared.GameModes;
+using Shared;
 using BepInEx.Logging;
 using CrusaderDE;
 using MonoMod.RuntimeDetour;
@@ -63,6 +65,7 @@ namespace CastlePlanner
         private bool depthLoadReady;
         private bool hudObserverPending;
         private MainViewModel observedHudViewModel;
+        private APIShared.IHudExtrasButtonRegistration hudExtrasRegistration;
         private Hook cameraUpdateHook;
         private CameraUpdateDelegate cameraUpdateTrampoline;
         private Hook editorPlayerHook;
@@ -99,6 +102,7 @@ namespace CastlePlanner
                 sizeCalibration,
                 buildingImageLibrary);
             Hud = new BlueprintHudViewModel(ToggleBlueprint, settings, preview);
+            RegisterHudExtrasButton();
             InstallEditorPlayerHook();
             InstallCameraWheelGuard();
 
@@ -135,7 +139,7 @@ namespace CastlePlanner
             // TickOncePerFrame deduplicates multiple callbacks within the same rendered frame.
             Application.onBeforeRender += OnBeforeRender;
             Application.focusChanged += OnApplicationFocusChanged;
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 "Persistent local blueprint runtime initialized; " +
                 "Application.onBeforeRender frame loop and focus-loss guard " +
@@ -166,6 +170,39 @@ namespace CastlePlanner
             }
         }
 
+        private void RegisterHudExtrasButton()
+        {
+            Hud.PropertyChanged += OnExtrasHudPropertyChanged;
+            APIShared.ApiShared.ForMod(CastlePlannerPlugin.PluginGuid).WhenReady(client => {
+                try
+                {
+                    if (!client.TryGetHudExtrasButtons(out var buttons, out var diagnostic))
+                        throw new InvalidOperationException(diagnostic.Reason);
+                    var definition = new APIShared.HudExtrasButtonDefinition("blueprints", Hud.ToggleSettingsPanelCommand,
+                        "Blueprints", context => {
+                            var button = new Noesis.Button { Style = context.Hud.TryFindResource("BTN_Building") as Noesis.Style };
+                            var normal = context.Hud.TryFindResource("UI-Buildings A001") as Noesis.ImageSource;
+                            var highlight = context.Hud.TryFindResource("UI-Buildings A002") as Noesis.ImageSource;
+                            if (normal == null || highlight == null || button.Style == null)
+                                throw new InvalidOperationException("Vanilla Blueprint button resources are unavailable.");
+                            PropEx.SetSprite1(button, normal); PropEx.SetSprite2(button, highlight);
+                            PropEx.SetSprite3(button, highlight); PropEx.SetSprite4(button, normal);
+                            return button;
+                        });
+                    if (!buttons.TryRegisterButton(definition, out hudExtrasRegistration, out diagnostic))
+                        throw new InvalidOperationException(diagnostic.Reason);
+                    hudExtrasRegistration.SetVisible(Hud.HudVisible);
+                }
+                catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "Blueprint side-HUD registration failed: " + ex); }
+            });
+        }
+
+        private void OnExtrasHudPropertyChanged(object sender, PropertyChangedEventArgs args)
+        {
+            if (args.PropertyName == nameof(BlueprintHudViewModel.HudVisible))
+                hudExtrasRegistration?.SetVisible(Hud.HudVisible);
+        }
+
         private void OnBeforeRender()
         {
             // A hidden projection has no frame-dependent work after its one-time preparation.
@@ -477,7 +514,7 @@ namespace CastlePlanner
                     hotkeyCaptureIgnoredKeys.Add(key);
             }
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Blueprint hotkey capture armed; ignoredHeldMouseButtons=" +
                 $"{hotkeyCaptureIgnoredKeys.Count}.");
@@ -574,7 +611,7 @@ namespace CastlePlanner
             if (!settings.CompleteHotkeyCapture(key, alt, control, shift))
                 return false;
             StopHotkeyCapture();
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Blueprint hotkey capture finished: key={key}, " +
                 $"value={(int)key}, source={source}.");
@@ -697,7 +734,7 @@ namespace CastlePlanner
                 layoutKeepX = int.MinValue;
                 layoutKeepY = int.MinValue;
                 renderer.Clear();
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     "Blueprint hidden because the start selection contains no castle.");
                 return false;
@@ -748,7 +785,7 @@ namespace CastlePlanner
                 layoutKeepX = keepX;
                 layoutKeepY = keepY;
                 renderer.PreloadDepthCaptures(layout);
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Blueprint prepared locally: reason={reason}, " +
                     $"file={fullPath}, keep=({keepX},{keepY}), " +
@@ -909,7 +946,7 @@ namespace CastlePlanner
                 blueprintVisible = false;
                 pendingViewSettleTime = -1f;
                 suppressOverlayUntilViewSettled = false;
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Blueprint hidden locally: reason={reason}.");
             }
@@ -950,7 +987,7 @@ namespace CastlePlanner
             suppressOverlayUntilViewSettled = true;
             pendingViewSettleTime =
                 Time.unscaledTime + ViewSettleDelaySeconds;
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Blueprint temporarily hidden while the normal map " +
                 $"projection settles: delay={ViewSettleDelaySeconds:F2}s, " +
@@ -984,7 +1021,7 @@ namespace CastlePlanner
                 lastRotation = (int)GameMap.instance.CurrentRotation();
                 lastFlattenedLandscape =
                     EngineInterface.FlattenedLandscape;
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Blueprint rendered locally: reason={reason}, " +
                     $"tiles={result.RenderedTiles}, icons={result.RenderedIcons}, " +
@@ -1044,7 +1081,7 @@ namespace CastlePlanner
             Hud?.UpdateViewportSize(
                 MainViewModel.iUIScaleValueWidth,
                 MainViewModel.iUIScaleValueHeight);
-            UpdateVanillaButtonSlot(viewModel);
+            UpdateVanillaPanelAnchor(viewModel);
             if (preparePending)
                 TryPrepareBlueprint();
         }
@@ -1053,12 +1090,12 @@ namespace CastlePlanner
         {
             if (args?.PropertyName == nameof(MainViewModel.Show_HUD_Extras_Button_Objectves) ||
                 args?.PropertyName == nameof(MainViewModel.Show_HUD_Extras_Button_Freebuild))
-                UpdateVanillaButtonSlot(sender as MainViewModel);
+                UpdateVanillaPanelAnchor(sender as MainViewModel);
         }
 
-        private void UpdateVanillaButtonSlot(MainViewModel viewModel)
+        private void UpdateVanillaPanelAnchor(MainViewModel viewModel)
         {
-            Hud?.UpdateVanillaButtonSlot(viewModel != null &&
+            Hud?.UpdateVanillaPanelAnchor(viewModel != null &&
                 (viewModel.Show_HUD_Extras_Button_Objectves ||
                  viewModel.Show_HUD_Extras_Button_Freebuild));
         }
@@ -1129,7 +1166,7 @@ namespace CastlePlanner
             return GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1;
         }
 
-        private static bool IsMapEditor() => Shared.GameModeHelper.IsMapEditor();
+        private static bool IsMapEditor() => APIShared.GameModes.GameModeHelper.IsMapEditor();
 
         private static bool CanUseGameplayHotkeys()
         {

diff --git a/CastlePlanner/src/CastleDropDownHeightController.cs b/CastlePlanner/src/CastleDropDownHeightController.cs
index e33aec63a..1c48479de 100644
--- a/CastlePlanner/src/CastleDropDownHeightController.cs
+++ b/CastlePlanner/src/CastleDropDownHeightController.cs
@@ -72,7 +72,7 @@ namespace CastlePlanner
                 return null;
             }
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 "CastlePlanner castle dropdown controller attached; " +
                 $"desiredMaximum={DesiredMaximumHeight:0}, " +
@@ -136,7 +136,7 @@ namespace CastlePlanner
             bool loaded = settings.EnsureCastleCatalogLoaded();
             EnsurePopupPlacementBelow();
             UpdateDropDownHeight();
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"CastlePlanner {(loaded ? "loaded" : "reused")} the cached AIVJSON catalog when mod settings opened; " +
                 $"count={settings.AvailableFileCount}.");
@@ -227,7 +227,7 @@ namespace CastlePlanner
                 return;
 
             lastAppliedHeight = height;
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 "CastlePlanner castle dropdown height updated from current Noesis " +
                 $"geometry: viewHeight={viewHeight:0.0}, " +

diff --git a/CastlePlanner/src/CastlePlannerPlugin.cs b/CastlePlanner/src/CastlePlannerPlugin.cs
index 0818a4e69..c83e043e9 100644
--- a/CastlePlanner/src/CastlePlannerPlugin.cs
+++ b/CastlePlanner/src/CastlePlannerPlugin.cs
@@ -1,3 +1,4 @@
+using APIShared.ModSettings;
 using BepInEx;
 using MonoMod.RuntimeDetour;
 using SHCDESE.API;
@@ -9,7 +10,7 @@ using System.Reflection;
 namespace CastlePlanner
 {
     [BepInDependency(ScriptExtenderGuid, "2.10.4")]
-    [BepInDependency("APIShared_Serp", "0.4.6")]
+    [BepInDependency("APIShared_Serp", "0.6.0")]
     [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
     [BepInDependency("ExtraFeatures_Serp", BepInDependency.DependencyFlags.SoftDependency)]
     [BepInDependency("BugfixesAndQoL_Serp", BepInDependency.DependencyFlags.SoftDependency)]
@@ -22,7 +23,7 @@ namespace CastlePlanner
 
         public const string PluginGuid = "CastlePlanner_Serp";
         public const string PluginName = "CastlePlanner";
-        public const string PluginVersion = "0.8.36";
+        public const string PluginVersion = "0.8.40";
 
         // The BepInEx component is destroyed during startup, so runtime state remains static.
         private static CastlePlannerRuntime runtime;
@@ -49,9 +50,6 @@ namespace CastlePlanner
             previewRuntime = new FreeCastlePreviewRuntime(Logger, Settings);
             runtime = new CastlePlannerRuntime(Logger, Settings, previewRuntime);
             CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
-            Shared.DebugLogHelper.LogInfo(
-                Logger,
-                $"{PluginName} {PluginVersion} loaded; the AIVJSON catalog will be cached automatically in the lobby.");
         }
 
         private static void InstallSteamReadyHook()
@@ -119,8 +117,10 @@ namespace CastlePlanner
 
             try
             {
-                Shared.LobbyModSettingsPresetRegistration.Register(
-                    this, Logger, PluginGuid, Settings, "ScriptExtenderUI/CastlePlannerSettings.xaml");
+                Shared.DirectLaunchSettingsNotice.Configure(Settings, PluginGuid, blueprintsRemainAvailable: true);
+                APIShared.ModSettings.LobbyModSettingsPresetRegistration.Register(
+                    this, Logger, PluginGuid, Settings, "ScriptExtenderUI/CastlePlannerSettings.xaml",
+                    logRoutineActivity: true, enableScrollDiagnostics: true);
                 Settings.PumpCastleCatalogLoad();
             }
             catch (Exception ex)
@@ -146,9 +146,6 @@ namespace CastlePlanner
                 GameXAMLManagerAPI.Instance.RegisterBinding(
                     "CastlePlannerBlueprintHud",
                     blueprintRuntime.Hud);
-                GameXAMLManagerAPI.Instance.RegisterBinding(
-                    "CastlePlannerBlueprintSettingsButton",
-                    blueprintRuntime.Hud);
             }, failedOptionalStages);
 
             TryInitializeStage("AIV placement runtime", () =>
@@ -201,7 +198,7 @@ namespace CastlePlanner
 
             if (failedOptionalStages.Count == 0)
             {
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     Logger,
                     "Crusader library initialization completed; all optional stages completed successfully.");
             }

diff --git a/CastlePlanner/src/CastlePlannerRuntime.cs b/CastlePlanner/src/CastlePlannerRuntime.cs
index 7ca600ed2..24b0d0351 100644
--- a/CastlePlanner/src/CastlePlannerRuntime.cs
+++ b/CastlePlanner/src/CastlePlannerRuntime.cs
@@ -1,3 +1,5 @@
+using APIShared.GameModes;
+using Shared;
 using BepInEx.Logging;
 using BepInEx.Bootstrap;
 using AIVParser.Core;
@@ -208,7 +210,7 @@ namespace CastlePlanner
             GameTimeManagerAPI.Instance.OnTick += OnGameTick;
 
             installed = true;
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 "Native AIV castle spawner installed; all private functions and globals resolved uniquely.");
         }
@@ -218,7 +220,7 @@ namespace CastlePlanner
             handledCurrentMap = true;
             ClearDeferredCompoundPlacements("savegame-load");
             ClearMapSpawnState();
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 "Savegame load detected; native castle spawning is disabled for this map.");
         }
@@ -288,7 +290,7 @@ namespace CastlePlanner
                 }
 
                 EnsureSupportedGameMode(gameMode);
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Game-mode guard accepted supported skirmish: " +
                     $"sharedSingleplayerSkirmish={gameMode.SharedSingleplayerSkirmish}, " +
@@ -321,7 +323,7 @@ namespace CastlePlanner
                         $"preImportFailure='{spawnPlanFailure}'.");
                 }
 
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Native multiplayer castle execution completed inside map start: " +
                     $"executedPlayers=[{string.Join(",", executedPlayers)}].");
@@ -385,7 +387,7 @@ namespace CastlePlanner
                 for (int index = 0; index < castleRequests.Count; index++)
                     ImportPlayerCastle(castleRequests[index], preparedImports[index]);
 
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Native AIV pre-import transaction completed: " +
                     $"humanPlayers=[{string.Join(",", humanPlayerIds)}], " +
@@ -707,7 +709,7 @@ namespace CastlePlanner
             int ownedBuildingsBefore = CountOwnedBuildings(playerId);
             ImportedCandidateSnapshot importedCandidates =
                 CaptureImportedCandidates(playerId - 1);
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Native castle spawn planned: playerId={playerId}, " +
                 $"phase=VanillaHumanStart(PreCoordinateRead), keepReference=({keepX},{keepY}), " +
@@ -758,7 +760,7 @@ namespace CastlePlanner
                     "only candidate zero was imported.");
             }
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Native AIV placement selected: specIndex={specIndex}, candidateId={candidateId}, " +
                 $"orientation={orientation} ({DescribeOrientation(orientation)}), " +
@@ -788,7 +790,7 @@ namespace CastlePlanner
                     $"({nativePreparedKeepX},{nativePreparedKeepY}).");
             }
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Native AIV layout prepared before Vanilla Keep: playerId={playerId}, " +
                 $"specIndex={specIndex}, highestFrame={highestFrame}, " +
@@ -820,7 +822,7 @@ namespace CastlePlanner
             nativeCastleExecutionPlayerId = castle.PlayerId;
             nextHovelVisualStyle = 0;
             correctedHovelVisualCount = 0;
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Native castle execution planned: playerId={castle.PlayerId}, " +
                 $"specIndex={castle.SpecIndex}, highestFrame={castle.HighestFrame}, " +
@@ -840,7 +842,7 @@ namespace CastlePlanner
             }
 
             int ownedBuildingsAfter = CountOwnedBuildings(castle.PlayerId);
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Native castle execution completed: playerId={castle.PlayerId}, " +
                 $"specIndex={castle.SpecIndex}, highestFrame={castle.HighestFrame}, " +
@@ -875,7 +877,7 @@ namespace CastlePlanner
             AivRotation rotation = ToAivRotation(castle.Orientation);
             int nativeReferenceX = castle.RequestedKeepX;
             int nativeReferenceY = castle.RequestedKeepY;
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Supplemental castle anchor resolved: playerId={castle.PlayerId}, " +
                 $"nativeReference=({nativeReferenceX},{nativeReferenceY}), " +
@@ -1029,7 +1031,7 @@ namespace CastlePlanner
                             castle.PlayerId,
                             projectileType))
                     {
-                        Shared.DebugLogHelper.LogInfo(
+                        Shared.DebugLogHelper.LogDebug(
                             log,
                             $"Supplemental decoration skipped because it already exists or was queued: playerId={castle.PlayerId}, sourceIndex={index}, mapper={mapper}, position=({tile.X},{tile.Y}).");
                         continue;
@@ -1063,7 +1065,7 @@ namespace CastlePlanner
             string digest;
             using (SHA256 sha = SHA256.Create())
                 digest = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(digestPayload))).Replace("-", string.Empty).ToLowerInvariant();
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Supplemental castle spawn digest: playerId={castle.PlayerId}, objects={digestRows.Count}, sha256={digest}, entries=[{digestPayload}].");
         }
@@ -1169,7 +1171,7 @@ namespace CastlePlanner
                 return false;
             }
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Supplemental decoration created: playerId={playerId}, projectileId={projectileId}, mapper={mapper}, projectileType={projectileType}, position=({worldX},{worldY}), height={height}.");
             return true;
@@ -1294,7 +1296,7 @@ namespace CastlePlanner
                 TryFindCompoundBuilding(castle.PlayerId, item, out _, out _));
             if (existing == compoundPlan.Count)
             {
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Deferred compound-building queue not needed: playerId={castle.PlayerId}, " +
                     $"planned={compoundPlan.Count}, existing={existing}.");
@@ -1307,7 +1309,7 @@ namespace CastlePlanner
                 castle.RequestedKeepY,
                 ToAivRotation(castle.Orientation),
                 compoundPlan);
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Deferred compound-building queue armed: playerId={castle.PlayerId}, " +
                 $"planned={compoundPlan.Count}, existing={existing}, " +
@@ -1393,7 +1395,7 @@ namespace CastlePlanner
                     if (existingState != AliveState.IsAlive)
                         return false;
 
-                    Shared.DebugLogHelper.LogInfo(
+                    Shared.DebugLogHelper.LogDebug(
                         log,
                         $"Deferred compound-building prerequisite ready: playerId={queue.PlayerId}, " +
                         $"sourceOrdinal={placement.SourceOrdinal}, mapper={placement.Mapper}, " +
@@ -1454,7 +1456,7 @@ namespace CastlePlanner
                     queue.DigestRows.Add(
                         $"building:{(int)placement.Mapper}:{queue.PlayerId}:" +
                         $"{placement.BuildOrigin.X}:{placement.BuildOrigin.Y}:{height}");
-                    Shared.DebugLogHelper.LogInfo(
+                    Shared.DebugLogHelper.LogDebug(
                         log,
                         $"Deferred compound-building placement accepted by Vanilla: " +
                         $"playerId={queue.PlayerId}, sourceOrdinal={placement.SourceOrdinal}, " +
@@ -1476,7 +1478,7 @@ namespace CastlePlanner
                 return false;
             }
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Deferred compound-building queue completed: playerId={queue.PlayerId}, " +
                 $"placements={queue.Placements.Count}, tick={tick}.");
@@ -1489,7 +1491,7 @@ namespace CastlePlanner
                     .Replace("-", string.Empty)
                     .ToLowerInvariant();
             }
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Deferred compound-building spawn digest: playerId={queue.PlayerId}, " +
                 $"objects={queue.DigestRows.Count}, sha256={digest}, entries=[{digestPayload}].");
@@ -1543,7 +1545,7 @@ namespace CastlePlanner
         {
             if (deferredCompoundBuildings.Count > 0)
             {
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Deferred compound-building queues cleared: reason={reason}, " +
                     $"players=[{string.Join(",", deferredCompoundBuildings.Keys)}].");
@@ -1660,7 +1662,7 @@ namespace CastlePlanner
                 displacementOffset: 3,
                 instructionLength: 7);
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Native AIV bindings resolved: module=0x{libraryHandle.ToInt64():X}, " +
                 $"aivState=0x{aivState.ToInt64():X}, " +
@@ -1738,7 +1740,7 @@ namespace CastlePlanner
             }
             else
             {
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"CastlePlanner AIV import backend: official SHCDE-SE {extenderVersion} " +
                     $"GameAIVManagerAPI is available; compatibility workaround inactive.");
@@ -1810,7 +1812,7 @@ namespace CastlePlanner
                 throw;
             }
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Early Vanilla human-start hooks installed: " +
                 $"keepCoordinateRva=0x{humanStartHookRva:X}, " +
@@ -1835,7 +1837,7 @@ namespace CastlePlanner
             if (!pendingAivImports.TryGetValue(playerId, out PendingAivImport imported))
             {
                 *(int*)(registers->RSP + 0x30) = rotation;
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Vanilla human Keep rotation prepared without an AIV castle: " +
                     $"playerId={playerId}, orientation={rotation} " +
@@ -1873,7 +1875,7 @@ namespace CastlePlanner
                 *(int*)(registers->RSP + 0x30) = castle.Orientation;
                 preparedAivCastles[playerId] = castle;
 
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Vanilla human start prepared before its first Keep-coordinate read: " +
                     $"playerId={playerId}, startIndex={startIndex}, " +
@@ -1898,7 +1900,7 @@ namespace CastlePlanner
                 return;
 
             options.SpawnStockpile = placeGoodsyard;
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Fixes goodsyard policy applied to AIV spawn plan: " +
                 $"playerId={playerId}, placeGoodsyard={placeGoodsyard}.");
@@ -2096,7 +2098,7 @@ namespace CastlePlanner
 
         private static GameModeSnapshot CaptureGameMode(APIShared.MissionLifecycleNotification args)
         {
-            Shared.GameModeSnapshot sharedMode =
+            APIShared.GameModes.GameModeSnapshot sharedMode =
                 Shared.GameplayModActivationGate.Snapshot;
             Director director = Director.instance;
             GameData gameData = GameData.Instance;
@@ -2293,7 +2295,7 @@ namespace CastlePlanner
                     ref building, out Shared.GameBuildingFootprintBounds bounds)
                     ? $"({bounds.MinX},{bounds.MinY})-({bounds.MaxX},{bounds.MaxY})"
                     : "<invalid>";
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Native special-building diagnostics: playerId={playerId}, " +
                     $"buildingId={spanIndex + 1}, globalId={building.r_GlobalId}, " +
@@ -2308,7 +2310,7 @@ namespace CastlePlanner
                     $"health={building.r_CurrentHealth}/{building.r_MaxHealth}.");
             }
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Native special-building summary: playerId={playerId}, " +
                 $"granaries={granaryCount}, hovels={hovelCount}.");

diff --git a/CastlePlanner/src/CastlePlannerSettingsViewModel.cs b/CastlePlanner/src/CastlePlannerSettingsViewModel.cs
index 19a55a7e5..7ec57f94f 100644
--- a/CastlePlanner/src/CastlePlannerSettingsViewModel.cs
+++ b/CastlePlanner/src/CastlePlannerSettingsViewModel.cs
@@ -1,3 +1,4 @@
+using APIShared.ModSettings;
 using BepInEx.Logging;
 using SHCDESE.API;
 using SHCDESE.API.Components.ModManager;
@@ -30,7 +31,7 @@ namespace CastlePlanner
         }
     }
 
-    public sealed class CastlePlannerSettingsViewModel : Shared.PresetLobbyModSettingsViewModel
+    public sealed class CastlePlannerSettingsViewModel : APIShared.ModSettings.PresetLobbyModSettingsViewModel
     {
         private readonly ManualLogSource log;
         private AivFileCatalog catalog = new AivFileCatalog();
@@ -215,7 +216,7 @@ namespace CastlePlanner
                 });
                 castleCatalogTask.ContinueWith(_ =>
                     Shared.UnityMainThreadDispatch.TryEnqueue(PumpCastleCatalogLoad));
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     "Asynchronous AIVJSON catalog loading started; recursive discovery and hashing will not block the game thread.");
             }
@@ -267,7 +268,7 @@ namespace CastlePlanner
             OnPropertyChanged(nameof(AvailableFileCount));
             if (selectionChanged)
                 OnPropertyChanged(nameof(SelectedCastle));
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"CastlePlanner cached AIVJSON choices including Steam Workshop content; " +
                 $"unique={CastleOptions.Count}, identicalDuplicatesIgnored={catalog.IdenticalFileCount}, " +
@@ -332,7 +333,7 @@ namespace CastlePlanner
         public string CastleSectionTitleText => SerpLocalization.Get("CastlePlanner.CastleSectionTitle");
         public string PlacementControlsTitleText => SerpLocalization.Get("CastlePlanner.PlacementControlsTitle");
 
-        [Shared.PresetLocal]
+        [APIShared.ModSettings.PresetLocal]
         public bool EnableClientFeatures
         {
             get => enableClientFeatures;
@@ -344,7 +345,7 @@ namespace CastlePlanner
                 enableClientFeatures = value;
                 OnPropertyChanged(nameof(EnableClientFeatures));
                 OnPropertyChanged(nameof(IsBlueprintMode));
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"CastlePlanner local activation changed to {enableClientFeatures}.");
                 PumpCastleCatalogLoad();
@@ -364,7 +365,7 @@ namespace CastlePlanner
                 enableMod = value;
                 OnPropertyChanged(nameof(EnableMod));
                 OnPropertyChanged(nameof(IsSpawnMode));
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"CastlePlanner host activation changed to {enableMod}.");
                 PumpCastleCatalogLoad();
@@ -386,13 +387,13 @@ namespace CastlePlanner
 
                 enableAivPlacementLobby = value;
                 OnPropertyChanged(nameof(EnableAivPlacementLobby));
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"CastlePlanner host AIV placement feature changed to {enableAivPlacementLobby}.");
             }
         }
 
-        [Shared.PresetLocal]
+        [APIShared.ModSettings.PresetLocal]
         public bool Blueprints
         {
             get => blueprints;
@@ -404,7 +405,7 @@ namespace CastlePlanner
                 blueprints = value;
                 OnPropertyChanged(nameof(Blueprints));
                 OnPropertyChanged(nameof(IsBlueprintMode));
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"CastlePlanner local Blueprints changed to {blueprints}.");
                 PumpCastleCatalogLoad();
@@ -412,7 +413,7 @@ namespace CastlePlanner
             }
         }
 
-        [Shared.PresetLocal]
+        [APIShared.ModSettings.PresetLocal]
         public bool BlueprintShowFortifications
         {
             get => blueprintShowFortifications;
@@ -422,7 +423,7 @@ namespace CastlePlanner
                 nameof(BlueprintShowFortifications));
         }
 
-        [Shared.PresetLocal]
+        [APIShared.ModSettings.PresetLocal]
         public bool BlueprintShowBuildings
         {
             get => blueprintShowBuildings;
@@ -432,7 +433,7 @@ namespace CastlePlanner
                 nameof(BlueprintShowBuildings));
         }
 
-        [Shared.PresetLocal]
+        [APIShared.ModSettings.PresetLocal]
         public bool BlueprintShowDefensiveGroundFeatures
         {
             get => blueprintShowDefensiveGroundFeatures;
@@ -442,7 +443,7 @@ namespace CastlePlanner
                 nameof(BlueprintShowDefensiveGroundFeatures));
         }
 
-        [Shared.PresetLocal]
+        [APIShared.ModSettings.PresetLocal]
         public bool BlueprintShowFearFactorBuildings
         {
             get => blueprintShowFearFactorBuildings;
@@ -475,7 +476,7 @@ namespace CastlePlanner
                 spawnCastle = value;
                 OnPropertyChanged(nameof(SpawnCastle));
                 OnPropertyChanged(nameof(IsSpawnMode));
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"CastlePlanner host Spawn Castle changed to {spawnCastle}.");
                 PumpCastleCatalogLoad();
@@ -500,7 +501,7 @@ namespace CastlePlanner
                 castleSelectionTimeoutSeconds = normalized;
                 OnPropertyChanged(nameof(CastleSelectionTimeoutSeconds));
                 OnPropertyChanged(nameof(CastleSelectionTimeoutValueText));
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"CastlePlanner host castle-selection timeout changed to {castleSelectionTimeoutSeconds} real seconds.");
             }
@@ -554,7 +555,7 @@ namespace CastlePlanner
                 nameof(SpawnBraziersAndFlags));
         }
 
-        [Shared.PresetLocal]
+        [APIShared.ModSettings.PresetLocal]
         public string SelectedCastle
         {
             get => selectedCastle;
@@ -566,21 +567,21 @@ namespace CastlePlanner
 
                 selectedCastle = normalized;
                 OnPropertyChanged(nameof(SelectedCastle));
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"CastlePlanner local AIVJSON selection changed to '{selectedCastle}'.");
                 SettingsChanged?.Invoke();
             }
         }
 
-        [Shared.PresetLocal]
+        [APIShared.ModSettings.PresetLocal]
         public int BlueprintHotkey
         {
             get => blueprintHotkey;
             set => SetHotkey(NormalizeHotkey(value));
         }
 
-        [Shared.PresetLocal]
+        [APIShared.ModSettings.PresetLocal]
         public double BlueprintIconScale
         {
             get => blueprintIconScale;
@@ -597,7 +598,7 @@ namespace CastlePlanner
             }
         }
 
-        [Shared.PresetLocal]
+        [APIShared.ModSettings.PresetLocal]
         public double BlueprintIconAlpha
         {
             get => blueprintIconAlpha;
@@ -685,7 +686,7 @@ namespace CastlePlanner
 
             field = value;
             OnPropertyChanged(propertyName);
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"CastlePlanner local {propertyName} changed to {value}.");
             BlueprintContentSettingsChanged?.Invoke();
@@ -737,7 +738,7 @@ namespace CastlePlanner
 
             field = value;
             OnPropertyChanged(propertyName);
-            Shared.DebugLogHelper.LogInfo(log, $"CastlePlanner host {propertyName} changed to {value}.");
+            Shared.DebugLogHelper.LogDebug(log, $"CastlePlanner host {propertyName} changed to {value}.");
             SettingsChanged?.Invoke();
         }
 
@@ -774,7 +775,7 @@ namespace CastlePlanner
             runtimeState.BlueprintHudPositionY =
                 NormalizeUnitValue(normalizedY);
             runtimeStorage.Save(runtimeState);
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Blueprint HUD position saved: " +
                 $"x={runtimeState.BlueprintHudPositionX:0.000}, " +
@@ -1084,7 +1085,7 @@ namespace CastlePlanner
             blueprintHotkey = key;
             OnPropertyChanged(nameof(BlueprintHotkey));
             OnPropertyChanged(nameof(HotkeyDisplayText));
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Blueprint toggle hotkey changed to '{HotkeyDisplayText}' ({blueprintHotkey}).");
         }

diff --git a/CastlePlanner/src/FixesKeepRotationCompatibility.cs b/CastlePlanner/src/FixesKeepRotationCompatibility.cs
index d913ff376..0fb84d7fa 100644
--- a/CastlePlanner/src/FixesKeepRotationCompatibility.cs
+++ b/CastlePlanner/src/FixesKeepRotationCompatibility.cs
@@ -70,7 +70,7 @@ namespace CastlePlanner
                         selection.PlayerId);
                 }
                 overriddenData = data;
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Temporarily aligned Fixes keep rotations for CastlePlanner players " +
                     $"[{string.Join(",", originalValues.Keys)}].");
@@ -105,7 +105,7 @@ namespace CastlePlanner
             {
                 foreach (KeyValuePair<int, object> pair in originalValues)
                     overriddenData.SetValue(pair.Value, pair.Key);
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Restored Fixes keep rotations after CastlePlanner spawn ({reason}).");
             }

diff --git a/CastlePlanner/src/FreeCastlePreviewRuntime.cs b/CastlePlanner/src/FreeCastlePreviewRuntime.cs
index 986029da0..08d21aee8 100644
--- a/CastlePlanner/src/FreeCastlePreviewRuntime.cs
+++ b/CastlePlanner/src/FreeCastlePreviewRuntime.cs
@@ -1,3 +1,6 @@
+using APIShared.Commands;
+using APIShared.GameModes;
+using Shared;
 using BepInEx.Logging;
 using CrusaderDE;
 using MonoMod.RuntimeDetour;
@@ -25,11 +28,6 @@ namespace CastlePlanner
 {
     internal sealed class FreeCastlePreviewRuntime : INotifyPropertyChanged
     {
-        private delegate int GameActionDelegate(
-            Enums.GameActionCommand command,
-            int structureId,
-            int state,
-            int value2);
         private delegate void LeaveLobbyDelegate(
             Platform_Multiplayer self,
             bool preserveGameMembers);
@@ -88,12 +86,10 @@ namespace CastlePlanner
         private IDisposable packetSubscription;
         private IDisposable mapStartSubscription;
         private IDisposable mapUnloadSubscription;
-        private Hook gameActionHook;
         private Hook leaveLobbyHook;
         private Hook startGameHook;
         private Hook delayShowDisconnectHook;
         private MethodInfo initFastMethod;
-        private GameActionDelegate gameActionTrampoline;
         private LeaveLobbyDelegate leaveLobbyTrampoline;
         private StartGameDelegate startGameTrampoline;
         private EngineInterface.MultiplayerSetupData capturedSetup;
@@ -218,7 +214,7 @@ namespace CastlePlanner
                 if (selectedRotation == normalized)
                     return;
                 selectedRotation = normalized;
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Free-castle rotation changed: ui={selectedRotation}, native={SelectedNativeRotation}.");
                 Notify(nameof(SelectedRotation));
@@ -261,12 +257,6 @@ namespace CastlePlanner
             mapUnloadSubscription = Shared.MissionEvents.Ended
                 .Subscribe(OnUnloadMap);
 
-            MethodInfo action = typeof(EngineInterface).GetMethod(
-                nameof(EngineInterface.GameAction),
-                BindingFlags.Public | BindingFlags.Static,
-                null,
-                new[] { typeof(Enums.GameActionCommand), typeof(int), typeof(int), typeof(int) },
-                null) ?? throw new MissingMethodException("EngineInterface.GameAction");
             MethodInfo leave = typeof(Platform_Multiplayer).GetMethod(
                 "LeaveLobby",
                 BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
@@ -296,8 +286,8 @@ namespace CastlePlanner
                 Type.EmptyTypes,
                 null) ?? throw new MissingMethodException("Platform_Multiplayer.initFast");
 
-            gameActionHook = new Hook(action, (GameActionDelegate)GameActionHook);
-            gameActionTrampoline = gameActionHook.GenerateTrampoline<GameActionDelegate>();
+            if (!GameActionEvents.TryRegister(CastlePlannerPlugin.PluginGuid, "PreviewPause", BeforeGameAction, null, out string actionReason))
+                throw new InvalidOperationException(actionReason);
             leaveLobbyHook = new Hook(leave, (LeaveLobbyDelegate)LeaveLobbyHook);
             leaveLobbyTrampoline = leaveLobbyHook.GenerateTrampoline<LeaveLobbyDelegate>();
             startGameHook = new Hook(start, (StartGameDelegate)StartGameHook);
@@ -306,7 +296,7 @@ namespace CastlePlanner
                 delayShowDisconnect,
                 (DelayShowDisconnectDelegate)DelayShowDisconnectHook);
             Application.onBeforeRender += OnBeforeRender;
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Free-castle preview initialized: packetId={packetHook.GetPacketId()}, " +
                 $"defaultTimeout={FreeCastleProtocol.DefaultPreviewTimeoutSeconds}s, " +
@@ -403,28 +393,21 @@ namespace CastlePlanner
             viewModel.Show_MP_LoadingButton = true;
         }
 
-        private int GameActionHook(
-            Enums.GameActionCommand command,
-            int structureId,
-            int actionState,
-            int value2)
+        private void BeforeGameAction(GameActionPreEventArgs args)
         {
-            if (IsFeatureModeAllowed() &&
-                !bypassPauseHook && IsPreviewPendingOrActive &&
-                command == Enums.GameActionCommand.Game_Paused && actionState == 0)
+            if (IsFeatureModeAllowed() && !bypassPauseHook && IsPreviewPendingOrActive &&
+                args.Command == Enums.GameActionCommand.Game_Paused && args.ActionState == 0)
             {
-                Shared.DebugLogHelper.LogInfo(log, "Unpause command suppressed during castle selection.");
-                return 0;
+                Shared.DebugLogHelper.LogDebug(log, "Unpause command suppressed during castle selection.");
+                args.SkipOriginalFunction = true;
             }
-            return gameActionTrampoline(command, structureId, actionState, value2);
         }
-
         private void LeaveLobbyHook(Platform_Multiplayer self, bool preserveGameMembers)
         {
             if (IsFeatureModeAllowed() &&
                 !bypassLeaveLobbyHook && IsPreviewPendingOrActive && realMultiplayer)
             {
-                Shared.DebugLogHelper.LogInfo(log, "Vanilla lobby departure deferred during castle selection.");
+                Shared.DebugLogHelper.LogDebug(log, "Vanilla lobby departure deferred during castle selection.");
                 return;
             }
             leaveLobbyTrampoline(self, preserveGameMembers);
@@ -463,7 +446,7 @@ namespace CastlePlanner
                 NotifyAll();
                 if (livenessReady)
                 {
-                    Shared.DebugLogHelper.LogInfo(
+                    Shared.DebugLogHelper.LogDebug(
                         log,
                         $"Castle preview pause armed in OnStartMap(Pre): operation={operationId}, localPlayer={localPlayerId}, multiplayer={realMultiplayer}, roster=[{string.Join(",", roster.OrderBy(id => id))}].");
                 }
@@ -485,7 +468,7 @@ namespace CastlePlanner
             {
                 ApplyPause(true);
                 settings.PumpCastleCatalogLoad();
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     "Castle selection is waiting for Vanilla's start-situation screen to close.");
             }
@@ -496,7 +479,7 @@ namespace CastlePlanner
             if (!IsFeatureModeAllowed() ||
                 !settings.IsSpawnMode || args.Context.IsSave || args.Context.Mode.CampaignMapId != 0)
                 return false;
-            Shared.GameModeSnapshot mode = Shared.GameplayModActivationGate.Snapshot;
+            APIShared.GameModes.GameModeSnapshot mode = Shared.GameplayModActivationGate.Snapshot;
             return mode.IsRealMultiplayer || mode.IsSingleplayerSkirmishMode;
         }
 
@@ -563,7 +546,7 @@ namespace CastlePlanner
                 return;
             try
             {
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Free-castle confirmation requested: playerId={localPlayerId}, " +
                     $"choice='{SelectedChoice}', uiRotation={SelectedRotation}, " +
@@ -703,7 +686,7 @@ namespace CastlePlanner
             manifestAcks.Clear();
             manifestAcks.Add(SteamUser.GetSteamID().m_SteamID);
             SendManifestToPeers(encoded);
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 "Castle manifest distributed; waiting for every participant without a connection-speed deadline.");
             NotifyAll();
@@ -1071,7 +1054,7 @@ namespace CastlePlanner
             ApplyPause(true);
             state = PreviewState.Loading;
             NotifyAll();
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 briefingObserved
                     ? "Vanilla start-situation screen closed; castle selection opened."
@@ -1089,7 +1072,7 @@ namespace CastlePlanner
                 if (!catalogWaitLogged)
                 {
                     catalogWaitLogged = true;
-                    Shared.DebugLogHelper.LogInfo(
+                    Shared.DebugLogHelper.LogDebug(
                         log,
                         "Castle selection is waiting for asynchronous AIVJSON catalog loading without blocking rendered frames.");
                 }
@@ -1098,7 +1081,7 @@ namespace CastlePlanner
 
             RebuildChoices();
             localCatalogReady = true;
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Castle selection catalog is ready; choices={castleChoices.Count}.");
             MarkLocalReady();
@@ -1117,7 +1100,7 @@ namespace CastlePlanner
                     Platform_Multiplayer platform = Platform_Multiplayer.Instance ??
                         throw new InvalidOperationException("The multiplayer platform is unavailable.");
 
-                    Shared.DebugLogHelper.LogInfo(
+                    Shared.DebugLogHelper.LogDebug(
                         log,
                         $"Free-castle multiplayer restart reset beginning: " +
                         $"operation={operationId}, mpGameActive={Platform_Multiplayer.MPGameActive}, " +
@@ -1130,7 +1113,7 @@ namespace CastlePlanner
                     initFastMethod.Invoke(platform, null);
                     platform.initFastFollowOn();
                     RelinquishMultiplayerLivenessGuardAfterVanillaReset();
-                    Shared.DebugLogHelper.LogInfo(
+                    Shared.DebugLogHelper.LogDebug(
                         log,
                         $"Free-castle multiplayer restart reset completed: " +
                         $"operation={operationId}, mpGameActive={Platform_Multiplayer.MPGameActive}, " +
@@ -1153,7 +1136,7 @@ namespace CastlePlanner
                     // seed handshake. Vanilla always follows it with this Director
                     // activation so messages and host acknowledgements are processed.
                     Director.instance.StartMultiplayerGame();
-                    Shared.DebugLogHelper.LogInfo(
+                    Shared.DebugLogHelper.LogDebug(
                         log,
                         $"Free-castle multiplayer restart handshake activated: " +
                         $"operation={operationId}, mpGameActive={Platform_Multiplayer.MPGameActive}, " +
@@ -1215,7 +1198,7 @@ namespace CastlePlanner
             try
             {
                 bypassPauseHook = true;
-                gameActionTrampoline(Enums.GameActionCommand.Game_Paused, 0, 0, 0);
+                EngineInterface.GameAction(Enums.GameActionCommand.Game_Paused, 0, 0, 0);
             }
             finally
             {
@@ -1348,7 +1331,7 @@ namespace CastlePlanner
             previousResyncingOrSaving = platform.resyncingOrSaving;
             platform.resyncingOrSaving = true;
             multiplayerLivenessGuardArmed = true;
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Castle-preview multiplayer liveness guard armed: operation={operationId}, " +
                 $"previousResyncingOrSaving={previousResyncingOrSaving}, " +
@@ -1398,7 +1381,7 @@ namespace CastlePlanner
                 }
             }
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Castle-preview multiplayer liveness guard released: operation={operationId}, " +
                 $"reason={reason}, restoredResyncingOrSaving={restoreValue}, " +
@@ -1412,7 +1395,7 @@ namespace CastlePlanner
 
             multiplayerLivenessGuardArmed = false;
             previousResyncingOrSaving = false;
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Castle-preview multiplayer liveness guard ownership relinquished after Vanilla reset: operation={operationId}.");
         }

diff --git a/Shared/Adapters/APIShared/DirectLaunchSettingsNotice.cs b/Shared/Adapters/APIShared/DirectLaunchSettingsNotice.cs
new file mode 100644
index 000000000..1c9450b2a
--- /dev/null
+++ b/Shared/Adapters/APIShared/DirectLaunchSettingsNotice.cs
@@ -0,0 +1,71 @@
+using APIShared.ModSettings;
+using APIShared.GameModes;
+using System;
+
+namespace Shared
+{
+    /// <summary>Serps gameplay-policy notice; APIShared only supplies the reusable presentation contract.</summary>
+    internal static class DirectLaunchSettingsNotice
+    {
+        internal static void Configure(PresetLobbyModSettingsViewModel settings, string modGuid, bool blueprintsRemainAvailable = false)
+        {
+            if (settings == null) throw new ArgumentNullException(nameof(settings));
+            settings.System_ModeAvailability.ConfigureDefault(SerpsModProfiles.GetProfile(modGuid, modGuid));
+            ConfigureFeatureRules(settings.System_ModeAvailability, modGuid);
+            settings.System_ConfigureDirectLaunchNotice(() => ResolveText(blueprintsRemainAvailable) + " " + settings.System_ModeNoticeText);
+        }
+
+        private static void ConfigureFeatureRules(ModSettingsModeAvailability availability, string modGuid)
+        {
+            GameplayFeatureId? defaultFeature = null;
+            switch (modGuid)
+            {
+                case "BuildingLimit_Serp": defaultFeature = GameplayFeatureId.BuildingLimitEnforcement; break;
+                case "UnitCosts_Serp": defaultFeature = GameplayFeatureId.UnitCostEnforcement; break;
+                case "UnitLimit_Serp": defaultFeature = GameplayFeatureId.UnitLimitEnforcement; break;
+                case "CheatMod_Serp": defaultFeature = GameplayFeatureId.EndlessExtremePowersRecharge; break;
+                case "RandomEvents_Serp": defaultFeature = GameplayFeatureId.RandomEventsRuntime; break;
+            }
+            if (defaultFeature.HasValue) availability.ConfigureDefault(ToSharedProfile(modGuid, defaultFeature.Value));
+            if (modGuid == "ExtraFeatures_Serp")
+            {
+                availability.Configure("extra.lord-health", ToSharedProfile(modGuid, GameplayFeatureId.LordHealthMultipliers));
+                // These settings are intentionally separate values for the two network contexts.
+                availability.ConfigureNetworkVariant("extra.human-enemy-proximity-singleplayer", false);
+                availability.ConfigureNetworkVariant("extra.ai-enemy-proximity-singleplayer", false);
+                availability.ConfigureNetworkVariant("extra.human-enemy-proximity-multiplayer", true);
+                availability.ConfigureNetworkVariant("extra.ai-enemy-proximity-multiplayer", true);
+            }
+            if (modGuid == "ImprovedHunters_Serp")
+            {
+                availability.Configure("hunters.improved-target-selection", ToSharedProfile(modGuid, GameplayFeatureId.ImprovedHunterTargetSelection));
+                availability.Configure("hunters.improved-pathfinding", ToSharedProfile(modGuid, GameplayFeatureId.ImprovedHunterPathfinding));
+                availability.Configure("hunters.allow-dead-targets", ToSharedProfile(modGuid, GameplayFeatureId.ImprovedHunterTargetSelection));
+            }
+            if (modGuid == "CastlePlanner_Serp")
+            {
+                availability.ConfigureDefault(ToSharedProfile(modGuid, GameplayFeatureId.CastleSpawning));
+                availability.Configure("castle-planner.blueprints", ToSharedProfile(modGuid, GameplayFeatureId.CastleBlueprints));
+                availability.Configure("castle-planner.hotkey", null);
+            }
+        }
+
+        private static GameplayModActivationProfile ToSharedProfile(string modGuid, GameplayFeatureId featureId)
+        {
+            var feature = GameplayFeatureModePolicy.GetProfile(modGuid, featureId);
+            return new GameplayModActivationProfile(modGuid, featureId.ToString(), feature.AllowedContexts, feature.AllowRealMultiplayer);
+        }
+
+        private static string ResolveText(bool blueprintsRemainAvailable)
+        {
+            bool german = SerpLocalization.GetActiveLocale().StartsWith("de", StringComparison.OrdinalIgnoreCase);
+            if (blueprintsRemainAvailable)
+                return german
+                    ? "Burgplatzierung und Spieländerungen sind für diesen direkten Start inaktiv; Blaupausen bleiben verfügbar. Über „Customize“ starten, um die Spieländerungen zu nutzen. Änderungen hier werden gespeichert."
+                    : "Castle spawning and gameplay changes are inactive for this direct start. Blueprints remain available. Use Customize to play with these changes; edits here are saved for later games.";
+            return german
+                ? "Die Spieländerungen dieser Mod sind für diesen direkten Start inaktiv. Über „Customize“ starten, um damit zu spielen. Änderungen hier werden für spätere Partien gespeichert."
+                : "This mod's gameplay changes are inactive for this direct start. Use Customize to play with them; edits here are saved for later games.";
+        }
+    }
+}

diff --git a/Shared/Adapters/APIShared/GameplayFeatureModePolicy.cs b/Shared/Adapters/APIShared/GameplayFeatureModePolicy.cs
new file mode 100644
index 000000000..2fcfbd9a8
--- /dev/null
+++ b/Shared/Adapters/APIShared/GameplayFeatureModePolicy.cs
@@ -0,0 +1,288 @@
+using APIShared.GameModes;
+using BepInEx.Logging;
+using System;
+using System.Collections.Generic;
+
+namespace Shared
+{
+    /// <summary>GameplayFeatureId in the centralized mission policy contract.</summary>
+    internal enum GameplayFeatureId
+    {
+        /// <summary>BuildingCostTooltip.</summary>
+        BuildingCostTooltip,
+        /// <summary>BuildingLimitEnforcement.</summary>
+        BuildingLimitEnforcement,
+        /// <summary>UnitCostEnforcement.</summary>
+        UnitCostEnforcement,
+        /// <summary>UnitLimitEnforcement.</summary>
+        UnitLimitEnforcement,
+        /// <summary>LordHealthMultipliers.</summary>
+        LordHealthMultipliers,
+        /// <summary>EndlessExtremePowersRecharge.</summary>
+        EndlessExtremePowersRecharge,
+        /// <summary>RandomEventsRuntime.</summary>
+        RandomEventsRuntime,
+        /// <summary>ImprovedHunterTargetSelection.</summary>
+        ImprovedHunterTargetSelection,
+        /// <summary>ImprovedHunterPathfinding.</summary>
+        ImprovedHunterPathfinding,
+        /// <summary>CastleSpawning.</summary>
+        CastleSpawning,
+        /// <summary>FreeCastlePreview.</summary>
+        FreeCastlePreview,
+        /// <summary>CastleBlueprints.</summary>
+        CastleBlueprints,
+    }
+
+    /// <summary>GameplayFeatureActivationProfile in the centralized mission policy contract.</summary>
+    internal readonly struct GameplayFeatureActivationProfile
+    {
+        /// <summary>GameplayFeatureActivationProfile in the centralized mission policy contract.</summary>
+        public GameplayFeatureActivationProfile(
+            string modGuid,
+            GameplayFeatureId featureId,
+            GameplayModAllowedContext allowedContexts,
+            bool allowRealMultiplayer)
+        {
+            ModGuid = modGuid ?? throw new ArgumentNullException(nameof(modGuid));
+            FeatureId = featureId;
+            AllowedContexts = allowedContexts;
+            AllowRealMultiplayer = allowRealMultiplayer;
+        }
+
+        /// <summary>ModGuid in the centralized mission policy contract.</summary>
+        public string ModGuid { get; }
+        /// <summary>FeatureId in the centralized mission policy contract.</summary>
+        public GameplayFeatureId FeatureId { get; }
+        /// <summary>AllowedContexts in the centralized mission policy contract.</summary>
+        public GameplayModAllowedContext AllowedContexts { get; }
+        /// <summary>AllowRealMultiplayer in the centralized mission policy contract.</summary>
+        public bool AllowRealMultiplayer { get; }
+    }
+
+    /// <summary>
+    /// Typed source of truth for features that intentionally have a narrower
+    /// mode contract than their owning gameplay mod.
+    /// </summary>
+    internal static class GameplayFeatureModePolicy
+    {
+        private const GameplayModAllowedContext NonEditorGameplayContexts =
+            GameplayModAllowedContext.CustomGame |
+            GameplayModAllowedContext.CustomizedVanillaTrail |
+            GameplayModAllowedContext.CustomizedCustomTrail |
+            GameplayModAllowedContext.CustomizedCoopTrail |
+            GameplayModAllowedContext.CustomizedSandsOfTime;
+
+        private const GameplayModAllowedContext AllRecognizedContexts =
+            NonEditorGameplayContexts |
+            GameplayModAllowedContext.MapEditor |
+            GameplayModAllowedContext.Campaign |
+            GameplayModAllowedContext.StandaloneMission |
+            GameplayModAllowedContext.VanillaTrail |
+            GameplayModAllowedContext.CustomTrail |
+            GameplayModAllowedContext.CoopTrail |
+            GameplayModAllowedContext.SandsOfTime;
+
+        private static readonly object LogSync = new object();
+        private static readonly Dictionary<GameplayFeatureId, bool> LoggedDecisions =
+            new Dictionary<GameplayFeatureId, bool>();
+
+        /// <summary>GetProfile in the centralized mission policy contract.</summary>
+        public static GameplayFeatureActivationProfile GetProfile(
+            string modGuid,
+            GameplayFeatureId featureId)
+        {
+            string expectedGuid;
+            GameplayModAllowedContext contexts;
+            bool allowRealMultiplayer = true;
+
+            switch (featureId)
+            {
+                case GameplayFeatureId.BuildingCostTooltip:
+                    expectedGuid = "BuildingCosts_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.BuildingLimitEnforcement:
+                    expectedGuid = "BuildingLimit_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.UnitCostEnforcement:
+                    expectedGuid = "UnitCosts_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.UnitLimitEnforcement:
+                    expectedGuid = "UnitLimit_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.LordHealthMultipliers:
+                    expectedGuid = "ExtraFeatures_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.EndlessExtremePowersRecharge:
+                    expectedGuid = "CheatMod_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.RandomEventsRuntime:
+                    expectedGuid = "RandomEvents_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.ImprovedHunterTargetSelection:
+                case GameplayFeatureId.ImprovedHunterPathfinding:
+                    expectedGuid = "ImprovedHunters_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    allowRealMultiplayer = false;
+                    break;
+                case GameplayFeatureId.CastleSpawning:
+                case GameplayFeatureId.FreeCastlePreview:
+                    expectedGuid = "CastlePlanner_Serp";
+                    contexts = NonEditorGameplayContexts;
+                    break;
+                case GameplayFeatureId.CastleBlueprints:
+                    expectedGuid = "CastlePlanner_Serp";
+                    contexts = AllRecognizedContexts;
+                    break;
+                default:
+                    throw new ArgumentOutOfRangeException(nameof(featureId), featureId, "Unknown gameplay feature ID.");
+            }
+
+            if (!string.Equals(modGuid, expectedGuid, StringComparison.Ordinal))
+            {
+                throw new ArgumentOutOfRangeException(
+                    nameof(modGuid),
+                    modGuid,
+                    $"Feature {featureId} belongs to mod GUID {expectedGuid}.");
+            }
+
+            return new GameplayFeatureActivationProfile(
+                expectedGuid,
+                featureId,
+                contexts,
+                allowRealMultiplayer);
+        }
+
+        /// <summary>IsAllowed in the centralized mission policy contract.</summary>
+        public static bool IsAllowed(
+            string modGuid,
+            GameplayFeatureId featureId,
+            GameModeSnapshot snapshot)
+        {
+            try
+            {
+                return IsAllowed(GetProfile(modGuid, featureId), snapshot, out _);
+            }
+            catch (ArgumentOutOfRangeException)
+            {
+                // A bad GUID/feature pair is a programming or versioning error;
+                // gameplay hooks must still leave Vanilla unchanged.
+                return false;
+            }
+        }
+
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/CastlePlanner.diff).
