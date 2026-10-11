# RandomEvents release status

**Status:** code newer

- Release: [v1.0.47](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/RandomEvents/v1.0.47)
- Release commit: [07e059a](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/07e059adde5b40307b8b1ce8af1ea437c13e421e)
- Current main commit: [2c95ad4](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/2c95ad4c3cefecb00bdadc503704e56a025a37f7)

## Relevant changed files

- `APIShared`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/RandomEvents.dll`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/RandomEvents.pdb`
- `RandomEvents/build.bat`
- `RandomEvents/info.json`
- `RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml`
- `RandomEvents/RandomEvents.csproj`
- `RandomEvents/src/RandomEventsParticipantResolver.cs`
- `RandomEvents/src/RandomEventsPlugin.cs`
- `RandomEvents/src/RandomEventsRuntime.cs`
- `RandomEvents/src/RandomEventsSettingsViewModel.cs`
- `RandomEvents/src/ScenarioSignpostRegistry.cs`
- `RandomEvents/src/SignpostPlacementService.cs`
- `Shared/Adapters/APIShared/DirectLaunchSettingsNotice.cs`
- `Shared/Adapters/APIShared/GameplayFeatureModePolicy.cs`
- `Shared/Adapters/APIShared/GameplayModActivationGate.cs`
- `Shared/Adapters/APIShared/MissionEventsAdapter.cs`
- `Shared/Adapters/APIShared/PlayerIdentityHelper.cs`
- `Shared/Adapters/APIShared/SerpsModProfiles.cs`
- `Shared/Runtime/Diagnostics/DebugLogHelper.cs`
- `Shared/Runtime/Gameplay/ActivePlayerHelper.cs`
- `Shared/Runtime/Gameplay/ActivePlayerKeepReadiness.cs`
- `Shared/Runtime/Localization/SerpLocalization.cs`
- `Shared/Runtime/Native/GameBuildingFootprint.cs`
- `Shared/Runtime/Native/NativePatternResolver.cs`
- `Shared/Runtime/Threading/UnityMainThreadDispatch.cs`
- `Shared/Runtime/UI/NumericTextInput.cs`
- `Shared/Runtime/UI/ToolTipPresentation.cs`

The localization helper also contains a general logic change that affects every consumer.

## Diff

```diff
diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json
index aa7888365..216aaa7c8 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json
@@ -3,13 +3,43 @@
   "Author": "Serpens66",
   "Name": "Random Events",
   "Description": "Runs configurable Vanilla scenario events in skirmishes, Trail missions, and multiplayer games.",
-  "Version": "1.0.47",
+  "Version": "1.0.51",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
-  "MinimumScriptExtenderVersion": "2.7.1",
+  "MinimumScriptExtenderVersion": "2.14.0",
   "MaximumScriptExtenderVersion": "",
+  "Dependencies": [
+    {
+      "GUID": "APIShared_Serp",
+      "MinimumVersion": "0.7.0"
+    }
+  ],
   "Manifest": 1,
   "NetworkMode": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.51",
+      "Changes": [
+        "Use APIShared 0.7.0 shared multiplayer transport while preserving existing packet schemas and gameplay execution."
+      ]
+    },
+    {
+      "Version": "1.0.50",
+      "Changes": [
+        "Migrate to APIShared 0.5.0 shared services and updated mod-owned adapters while preserving existing features."
+      ]
+    },
+    {
+      "Version": "1.0.49",
+      "Changes": [
+        "Updated Script Extender 2.14.0 unit field contracts; preserved death-marker and full control-word semantics."
+      ]
+    },
+    {
+      "Version": "1.0.48",
+      "Changes": [
+        "Move routine diagnostics to Debug; preserve warnings, errors and explicit file-operation results."
+      ]
+    },
     {
       "Version": "1.0.47",
       "Changes": [

diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml
index d30a23163..b425c5abf 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/Override/ScriptExtenderUI/RandomEventsSettings.xaml
@@ -2,8 +2,9 @@
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       xmlns:ui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
       xmlns:sys="clr-namespace:System;assembly=mscorlib"
-      xmlns:shared="clr-namespace:Shared;assembly=APIShared"
+      xmlns:shared="clr-namespace:APIShared.ModSettings;assembly=APIShared"
       xmlns:seui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
+      shared:ModSettingsMode.Availability="{Binding System_ModeAvailability}"
       shared:ModSettingsSearch.FilterText="{Binding System_ModSettingsSearchText}"
       shared:ModSettingsSearch.IncludeToolTips="{Binding System_ModSettingsSearchIncludeToolTips}"
       shared:ModSettingsSearch.ExactKey="{Binding System_ModSettingsSearchExactKey}">
@@ -15,7 +16,7 @@
     <Style x:Key="HostActivationBorder" TargetType="{x:Type Border}"><Setter Property="Background" Value="#443B6EA5"/><Setter Property="BorderBrush" Value="#FF77AAFF"/><Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="3"/><Setter Property="Padding" Value="8,4"/></Style>
     <Style x:Key="ClientActivationBorder" TargetType="{x:Type Border}"><Setter Property="Background" Value="#44306950"/><Setter Property="BorderBrush" Value="#FF66CC99"/><Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="3"/><Setter Property="Padding" Value="8,4"/></Style>
     <shared:ModSettingsSearchVisibilityConverter x:Key="ModSettingsSearchVisibilityConverter"/>
-    <Style x:Key="ModSettingsSearchTargetGrid" TargetType="{x:Type Grid}"><Setter Property="Visibility"><Setter.Value><MultiBinding Converter="{StaticResource ModSettingsSearchVisibilityConverter}"><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.FilterText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.IncludeToolTips)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ExactKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.Key)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.Title)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ToolTipText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/></MultiBinding></Setter.Value></Setter></Style>
+    <Style x:Key="ModSettingsSearchTargetGrid" TargetType="{x:Type Grid}"><Setter Property="shared:ModSettingsMode.Key" Value="{Binding RelativeSource={RelativeSource Self}, Path=(shared:ModSettingsSearch.Key)}"/><Setter Property="Visibility"><Setter.Value><MultiBinding Converter="{StaticResource ModSettingsSearchVisibilityConverter}"><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.FilterText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.IncludeToolTips)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ExactKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.Key)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.Title)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ToolTipText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/></MultiBinding></Setter.Value></Setter></Style>
     <Style x:Key="ModSettingsSearchSectionGrid" TargetType="{x:Type Grid}"><Setter Property="Visibility"><Setter.Value><MultiBinding Converter="{StaticResource ModSettingsSearchVisibilityConverter}"><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.FilterText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.IncludeToolTips)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ExactKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/></MultiBinding></Setter.Value></Setter></Style>
     <Style x:Key="ModSettingsSearchIcon" TargetType="{x:Type Path}"><Setter Property="Stroke" Value="#FF77AAFF"/><Style.Triggers><DataTrigger Binding="{Binding System_ModSettingsSearchHasActiveFilter}" Value="True"><Setter Property="Stroke" Value="#FFF2D48A"/></DataTrigger></Style.Triggers></Style>
   </Grid.Resources>
@@ -110,7 +111,7 @@
         <TextBlock Text="{Binding System_ModSettingsSearchNoResultsText}" Visibility="{Binding System_ModSettingsSearchNoResultsVisibility}" Foreground="#FFCC66" HorizontalAlignment="Left" Margin="0,4,0,0"/>
       </StackPanel>
       <TextBlock Text="{Binding ActionsScopeNoticeText}" Visibility="{Binding ActionsScopeNoticeVisibility}" Foreground="#BBBBBB" TextWrapping="Wrap" Margin="0,0,0,8"/>
-      <TextBlock Text="{Binding System_DirectLaunchNoticeText}" Visibility="{Binding System_DirectLaunchNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
+      <TextBlock Text="{Binding System_ConsumerModeNoticeText}" Visibility="{Binding System_ConsumerModeNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding System_TrailSourceNoticeText}" Visibility="{Binding System_TrailSourceNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostReadOnlyNoticeText}" Visibility="{Binding HostReadOnlyNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostOptionsText}" Style="{StaticResource HostRoleHeader}" Margin="0,2,0,6"/>

diff --git a/RandomEvents/build.bat b/RandomEvents/build.bat
index fb0caf14b..33a4987c7 100644
--- a/RandomEvents/build.bat
+++ b/RandomEvents/build.bat
@@ -1,4 +1,31 @@
 @echo off
+setlocal EnableExtensions
+set "BUILD_DRIVER_NOPAUSE=0"
+for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
+set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
+echo [%date% %time%] START RandomEvents
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
+echo [%date% %time%] Unit access regression tests
+powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\Validation\Test-UnitAccess.ps1"
+if errorlevel 1 exit /b 1
 setlocal EnableExtensions EnableDelayedExpansion
 
 set "PROJECT_DIR=%~dp0"
@@ -20,6 +47,7 @@ set "NO_PAUSE=0"
 for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
 
 rem Never touch build or installation output while the game has the plugin DLL loaded.
+echo [%date% %time%] Check that the game is closed
 powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
 if errorlevel 1 goto game_running
 
@@ -34,16 +62,20 @@ if exist "%LOCAL_SCRIPT_EXTENDER_BUILD_OUTPUT%\SHCDESE.dll" (
 
 if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
 pushd "%PROJECT_DIR%"
+echo [%date% %time%] Compile projects
 "%MSBUILD%" RandomEvents.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
 if errorlevel 1 goto build_failed_popd
 popd
 
+echo [%date% %time%] Copy package files
 copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
+echo [%date% %time%] Copy package files
 xcopy "%PROJECT_DIR%Override" "%LOCAL_PLUGIN_DIR%\Override\" /E /I /Q /Y >nul
 if not exist "%LOCAL_PLUGIN_DIR%\RandomEvents.dll" goto package_failed
 if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed
 
 rem Overlay plugin files so saved LobbyModSettings survive development builds.
+echo [%date% %time%] Copy package files
 xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
 if errorlevel 1 goto copy_failed
 if not exist "%GAME_PLUGIN_DIR%\RandomEvents.dll" goto copy_verification_failed
@@ -53,33 +85,28 @@ fc /B "%LOCAL_PLUGIN_DIR%\RandomEvents.dll" "%GAME_PLUGIN_DIR%\RandomEvents.dll"
 if errorlevel 1 goto copy_verification_failed
 fc /B "%LOCAL_PLUGIN_DIR%\info.json" "%GAME_PLUGIN_DIR%\info.json" >nul
 if errorlevel 1 goto copy_verification_failed
-powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Release\Write-LocalBuildManifest.ps1" -ModName RandomEvents
+echo [%date% %time%] PowerShell checks / build step
+powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName RandomEvents
 if errorlevel 1 goto package_failed
 
 echo Build und Installation von Random Events erfolgreich.
-if "%NO_PAUSE%"=="0" pause
 exit /b 0
 
 :game_running
 echo Build und Installation abgebrochen: Stronghold Crusader Definitive Edition ist noch gestartet.
 echo Lokales Paket und installierter Mod wurden nicht veraendert.
-if "%NO_PAUSE%"=="0" pause
 exit /b 1
 :copy_verification_failed
 echo Installationspruefung fehlgeschlagen: DLL, info.json oder Settings-XAML fehlen oder stimmen nicht mit dem lokalen Paket ueberein.
-if "%NO_PAUSE%"=="0" pause
 exit /b 1
 :build_failed_popd
 popd
 :build_failed
 echo Build fehlgeschlagen.
-if "%NO_PAUSE%"=="0" pause
 exit /b 1
 :package_failed
 echo Paketpruefung fehlgeschlagen.
-if "%NO_PAUSE%"=="0" pause
 exit /b 1
 :copy_failed
 echo Installation fehlgeschlagen. Ist das Spiel noch gestartet?
-if "%NO_PAUSE%"=="0" pause
 exit /b 1

diff --git a/RandomEvents/info.json b/RandomEvents/info.json
index aa7888365..216aaa7c8 100644
--- a/RandomEvents/info.json
+++ b/RandomEvents/info.json
@@ -3,13 +3,43 @@
   "Author": "Serpens66",
   "Name": "Random Events",
   "Description": "Runs configurable Vanilla scenario events in skirmishes, Trail missions, and multiplayer games.",
-  "Version": "1.0.47",
+  "Version": "1.0.51",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
-  "MinimumScriptExtenderVersion": "2.7.1",
+  "MinimumScriptExtenderVersion": "2.14.0",
   "MaximumScriptExtenderVersion": "",
+  "Dependencies": [
+    {
+      "GUID": "APIShared_Serp",
+      "MinimumVersion": "0.7.0"
+    }
+  ],
   "Manifest": 1,
   "NetworkMode": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.51",
+      "Changes": [
+        "Use APIShared 0.7.0 shared multiplayer transport while preserving existing packet schemas and gameplay execution."
+      ]
+    },
+    {
+      "Version": "1.0.50",
+      "Changes": [
+        "Migrate to APIShared 0.5.0 shared services and updated mod-owned adapters while preserving existing features."
+      ]
+    },
+    {
+      "Version": "1.0.49",
+      "Changes": [
+        "Updated Script Extender 2.14.0 unit field contracts; preserved death-marker and full control-word semantics."
+      ]
+    },
+    {
+      "Version": "1.0.48",
+      "Changes": [
+        "Move routine diagnostics to Debug; preserve warnings, errors and explicit file-operation results."
+      ]
+    },
     {
       "Version": "1.0.47",
       "Changes": [

diff --git a/RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml b/RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml
index d30a23163..b425c5abf 100644
--- a/RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml
+++ b/RandomEvents/Override/ScriptExtenderUI/RandomEventsSettings.xaml
@@ -2,8 +2,9 @@
       xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
       xmlns:ui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
       xmlns:sys="clr-namespace:System;assembly=mscorlib"
-      xmlns:shared="clr-namespace:Shared;assembly=APIShared"
+      xmlns:shared="clr-namespace:APIShared.ModSettings;assembly=APIShared"
       xmlns:seui="clr-namespace:SHCDESE.UI;assembly=SHCDESE"
+      shared:ModSettingsMode.Availability="{Binding System_ModeAvailability}"
       shared:ModSettingsSearch.FilterText="{Binding System_ModSettingsSearchText}"
       shared:ModSettingsSearch.IncludeToolTips="{Binding System_ModSettingsSearchIncludeToolTips}"
       shared:ModSettingsSearch.ExactKey="{Binding System_ModSettingsSearchExactKey}">
@@ -15,7 +16,7 @@
     <Style x:Key="HostActivationBorder" TargetType="{x:Type Border}"><Setter Property="Background" Value="#443B6EA5"/><Setter Property="BorderBrush" Value="#FF77AAFF"/><Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="3"/><Setter Property="Padding" Value="8,4"/></Style>
     <Style x:Key="ClientActivationBorder" TargetType="{x:Type Border}"><Setter Property="Background" Value="#44306950"/><Setter Property="BorderBrush" Value="#FF66CC99"/><Setter Property="BorderThickness" Value="1"/><Setter Property="CornerRadius" Value="3"/><Setter Property="Padding" Value="8,4"/></Style>
     <shared:ModSettingsSearchVisibilityConverter x:Key="ModSettingsSearchVisibilityConverter"/>
-    <Style x:Key="ModSettingsSearchTargetGrid" TargetType="{x:Type Grid}"><Setter Property="Visibility"><Setter.Value><MultiBinding Converter="{StaticResource ModSettingsSearchVisibilityConverter}"><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.FilterText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.IncludeToolTips)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ExactKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.Key)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.Title)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ToolTipText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/></MultiBinding></Setter.Value></Setter></Style>
+    <Style x:Key="ModSettingsSearchTargetGrid" TargetType="{x:Type Grid}"><Setter Property="shared:ModSettingsMode.Key" Value="{Binding RelativeSource={RelativeSource Self}, Path=(shared:ModSettingsSearch.Key)}"/><Setter Property="Visibility"><Setter.Value><MultiBinding Converter="{StaticResource ModSettingsSearchVisibilityConverter}"><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.FilterText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.IncludeToolTips)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ExactKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.Key)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.Title)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ToolTipText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/></MultiBinding></Setter.Value></Setter></Style>
     <Style x:Key="ModSettingsSearchSectionGrid" TargetType="{x:Type Grid}"><Setter Property="Visibility"><Setter.Value><MultiBinding Converter="{StaticResource ModSettingsSearchVisibilityConverter}"><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.FilterText)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.IncludeToolTips)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.ExactKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionKey)"/><Binding RelativeSource="{RelativeSource Self}" Path="(shared:ModSettingsSearch.SectionTitle)"/></MultiBinding></Setter.Value></Setter></Style>
     <Style x:Key="ModSettingsSearchIcon" TargetType="{x:Type Path}"><Setter Property="Stroke" Value="#FF77AAFF"/><Style.Triggers><DataTrigger Binding="{Binding System_ModSettingsSearchHasActiveFilter}" Value="True"><Setter Property="Stroke" Value="#FFF2D48A"/></DataTrigger></Style.Triggers></Style>
   </Grid.Resources>
@@ -110,7 +111,7 @@
         <TextBlock Text="{Binding System_ModSettingsSearchNoResultsText}" Visibility="{Binding System_ModSettingsSearchNoResultsVisibility}" Foreground="#FFCC66" HorizontalAlignment="Left" Margin="0,4,0,0"/>
       </StackPanel>
       <TextBlock Text="{Binding ActionsScopeNoticeText}" Visibility="{Binding ActionsScopeNoticeVisibility}" Foreground="#BBBBBB" TextWrapping="Wrap" Margin="0,0,0,8"/>
-      <TextBlock Text="{Binding System_DirectLaunchNoticeText}" Visibility="{Binding System_DirectLaunchNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
+      <TextBlock Text="{Binding System_ConsumerModeNoticeText}" Visibility="{Binding System_ConsumerModeNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding System_TrailSourceNoticeText}" Visibility="{Binding System_TrailSourceNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostReadOnlyNoticeText}" Visibility="{Binding HostReadOnlyNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostOptionsText}" Style="{StaticResource HostRoleHeader}" Margin="0,2,0,6"/>

diff --git a/RandomEvents/RandomEvents.csproj b/RandomEvents/RandomEvents.csproj
index 9129fb9e1..cae0f0e8c 100644
--- a/RandomEvents/RandomEvents.csproj
+++ b/RandomEvents/RandomEvents.csproj
@@ -29,16 +29,19 @@
     <Reference Include="MessagePack.Annotations"><HintPath>$(ExtenderDir)\MessagePack.Annotations.dll</HintPath><Private>false</Private></Reference>
   </ItemGroup>
   <ItemGroup>
-    <Compile Include="..\Shared\ActivePlayerHelper.cs"><Link>Shared\ActivePlayerHelper.cs</Link></Compile>
-    <Compile Include="..\Shared\ActivePlayerKeepReadiness.cs"><Link>Shared\ActivePlayerKeepReadiness.cs</Link></Compile>
-    <Compile Include="..\Shared\DebugLogHelper.cs"><Link>Shared\DebugLogHelper.cs</Link></Compile>
-    <Compile Include="..\Shared\UnityMainThreadDispatch.cs"><Link>Shared\UnityMainThreadDispatch.cs</Link></Compile>
-    <Compile Include="..\Shared\NativePatternResolver.cs"><Link>Shared\NativePatternResolver.cs</Link></Compile>
-    <Compile Include="..\Shared\GameModeHelper.cs"><Link>Shared\GameModeHelper.cs</Link></Compile>
-    <Compile Include="..\Shared\GameBuildingFootprint.cs"><Link>Shared\GameBuildingFootprint.cs</Link></Compile>
-    <Compile Include="..\Shared\GameplaySessionLifecycle.cs"><Link>Shared\GameplaySessionLifecycle.cs</Link></Compile>
-    <Compile Include="..\Shared\GameplayModActivationGate.cs"><Link>Shared\GameplayModActivationGate.cs</Link></Compile>
-    <Compile Include="..\Shared\SerpLocalization.cs"><Link>Shared\SerpLocalization.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Gameplay\ActivePlayerHelper.cs"><Link>Shared\Runtime\Gameplay\ActivePlayerHelper.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Gameplay\ActivePlayerKeepReadiness.cs"><Link>Shared\Runtime\Gameplay\ActivePlayerKeepReadiness.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Diagnostics\DebugLogHelper.cs"><Link>Shared\Runtime\Diagnostics\DebugLogHelper.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Threading\UnityMainThreadDispatch.cs"><Link>Shared\Runtime\Threading\UnityMainThreadDispatch.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Native\NativePatternResolver.cs"><Link>Shared\Runtime\Native\NativePatternResolver.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\PlayerIdentityHelper.cs"><Link>Shared\Adapters\APIShared\PlayerIdentityHelper.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Native\GameBuildingFootprint.cs"><Link>Shared\Runtime\Native\GameBuildingFootprint.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\MissionEventsAdapter.cs"><Link>Shared\Adapters\APIShared\MissionEventsAdapter.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\DirectLaunchSettingsNotice.cs"><Link>Shared\Adapters\APIShared\DirectLaunchSettingsNotice.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\SerpsModProfiles.cs"><Link>Shared\Adapters\APIShared\SerpsModProfiles.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\GameplayFeatureModePolicy.cs"><Link>Shared\Adapters\APIShared\GameplayFeatureModePolicy.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\GameplayModActivationGate.cs"><Link>Shared\Adapters\APIShared\GameplayModActivationGate.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Localization\SerpLocalization.cs"><Link>Shared\Runtime\Localization\SerpLocalization.cs</Link></Compile>
     <Compile Include="src\RandomEventDefinitions.cs" />
     <Compile Include="src\ArcherSourceNativeLayout.cs" />
     <Compile Include="src\BanditTargetEligibility.cs" />
@@ -48,7 +51,7 @@
     <Compile Include="src\NativeWildlifeEventDispatcher.cs" />
     <Compile Include="src\RandomEventsPlugin.cs" />
     <Compile Include="src\RandomEventsChorePacket.cs" />
-    <Compile Include="src\RandomEventsChoreSender.cs" />
+
     <Compile Include="src\RandomEventsCalendar.cs" />
     <Compile Include="src\RandomEventsBatchValidator.cs" />
     <Compile Include="src\RandomEventsCooldownCodec.cs" />
@@ -67,8 +70,8 @@
   </ItemGroup>
   <ItemGroup><Content Include="Locales\*.txt"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory></Content></ItemGroup>
   <ItemGroup>
-    <Compile Include="..\Shared\ToolTipPresentation.cs"><Link>Shared\ToolTipPresentation.cs</Link></Compile>
-    <Compile Include="..\Shared\NumericTextInput.cs"><Link>Shared\NumericTextInput.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\UI\ToolTipPresentation.cs"><Link>Shared\Runtime\UI\ToolTipPresentation.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\UI\NumericTextInput.cs"><Link>Shared\Runtime\UI\NumericTextInput.cs</Link></Compile>
   </ItemGroup>
   <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
   <Target Name="ValidateScriptExtenderReference" BeforeTargets="BeforeBuild"><Error Condition="!Exists('$(ExtenderDir)\SHCDESE.dll')" Text="SHCDESE.dll was not found: $(ExtenderDir)" /></Target>

diff --git a/RandomEvents/src/RandomEventsParticipantResolver.cs b/RandomEvents/src/RandomEventsParticipantResolver.cs
index d7a606e38..8c4699bf1 100644
--- a/RandomEvents/src/RandomEventsParticipantResolver.cs
+++ b/RandomEvents/src/RandomEventsParticipantResolver.cs
@@ -47,12 +47,12 @@ namespace RandomEvents
                 failure = "no valid Lord unit is registered";
                 return false;
             }
-            if (!GameUnitManagerAPI.Instance.TryGetUnitById((int)lordUnitId, out GameUnit* lord) || lord == null)
+            if (!APIShared.UnitAccess.TryGetById((int)lordUnitId, out GameUnit* lord, out _) || lord == null)
             {
                 failure = "registered Lord unit cannot be resolved";
                 return false;
             }
-            if (lord->r_AliveState != AliveState.IsAlive)
+            if (!APIShared.UnitAccess.IsReallyAlive(lord))
             {
                 failure = $"registered Lord unit state is {lord->r_AliveState}";
                 return false;

diff --git a/RandomEvents/src/RandomEventsPlugin.cs b/RandomEvents/src/RandomEventsPlugin.cs
index cfbe58cff..a730dc4e2 100644
--- a/RandomEvents/src/RandomEventsPlugin.cs
+++ b/RandomEvents/src/RandomEventsPlugin.cs
@@ -1,3 +1,4 @@
+using APIShared.ModSettings;
 using BepInEx;
 using SHCDESE.API;
 using SHCDESE.API.LowLevel;
@@ -5,16 +6,16 @@ using System;
 
 namespace RandomEvents
 {
-    [BepInDependency(ScriptExtenderGuid, "2.7.1")]
+    [BepInDependency(ScriptExtenderGuid, "2.14.0")]
     [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
-    [BepInDependency("APIShared_Serp", "0.4.6")]
+    [BepInDependency("APIShared_Serp", "0.7.0")]
     [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
     public sealed class RandomEventsPlugin : BaseUnityPlugin
     {
         private const string ScriptExtenderGuid = "000shcdese";
         public const string PluginGuid = "RandomEvents_Serp";
         public const string PluginName = "Random Events";
-        public const string PluginVersion = "1.0.47";
+        public const string PluginVersion = "1.0.51";
 
         private RandomEventsRuntime runtime;
 
@@ -42,7 +43,8 @@ namespace RandomEvents
 
             try
             {
-                Shared.LobbyModSettingsPresetRegistration.Register(
+                Shared.DirectLaunchSettingsNotice.Configure(Settings, PluginGuid);
+                APIShared.ModSettings.LobbyModSettingsPresetRegistration.Register(
                     this,
                     Logger,
                     PluginGuid,

diff --git a/RandomEvents/src/RandomEventsRuntime.cs b/RandomEvents/src/RandomEventsRuntime.cs
index 88dc31804..de44f977b 100644
--- a/RandomEvents/src/RandomEventsRuntime.cs
+++ b/RandomEvents/src/RandomEventsRuntime.cs
@@ -1,3 +1,4 @@
+using APIShared.GameModes;
 using BepInEx.Logging;
 using CrusaderDE;
 using MessagePack;
@@ -114,7 +115,7 @@ namespace RandomEvents
                 memory.Length > PeaceTimeActiveFlagRva)
             {
                 peaceTimeActiveFlagAddress = IntPtr.Add(libraryHandle, PeaceTimeActiveFlagRva);
-                Shared.DebugLogHelper.LogInfo(log,
+                Shared.DebugLogHelper.LogDebug(log,
                     $"Random Events Vanilla Peace Time flag resolved by reference-rva: rva=0x{PeaceTimeActiveFlagRva:X}.");
             }
             else
@@ -355,7 +356,7 @@ namespace RandomEvents
 
         private void InitializeCurrentMap()
         {
-            Shared.GameModeSnapshot gameMode = Shared.GameplayModActivationGate.Snapshot;
+            APIShared.GameModes.GameModeSnapshot gameMode = Shared.GameplayModActivationGate.Snapshot;
             string gameModeDetails = gameMode.ToDiagnosticString();
             if (!Shared.GameplayModActivationGate.IsAllowed)
             {
@@ -804,13 +805,10 @@ namespace RandomEvents
             }
 
             short packetId = packetHook?.GetPacketId() ?? (short)0;
-            if (!RandomEventsChoreSender.TrySend(
+            if (!APIShared.Networking.ChoreTransport.TrySend(
                 packet,
                 packetId,
                 packetHook != null,
-                value => GameNetworkAPI.Serialize(value),
-                () => GameGlobalsManager.Instance.ChoreManagerVA,
-                (value, id) => GameNetworkAPI.SendPacketToAllEx2(value, id, viaChore: true),
                 out body,
                 out string rejectionReason))
             {
@@ -825,6 +823,9 @@ namespace RandomEvents
 
         private void OnInitializationChorePacketReceived(ReceiveCustomPacketEventArgs<RandomEventsInitializationChorePacket> args)
         {
+            if (!APIShared.Networking.ChoreTransport.IsChoreDelivery(args))
+                return;
+
             if (!Shared.GameplayModActivationGate.IsAllowed)
                 return;
             RandomEventsInitializationChorePacket packet = args?.Packet;
@@ -847,6 +848,9 @@ namespace RandomEvents
 
         private void OnBatchChorePacketReceived(ReceiveCustomPacketEventArgs<RandomEventsBatchChorePacket> args)
         {
+            if (!APIShared.Networking.ChoreTransport.IsChoreDelivery(args))
+                return;
+
             if (!Shared.GameplayModActivationGate.IsAllowed)
                 return;
             try { ApplyBatchChore(args?.Packet); }
@@ -855,6 +859,9 @@ namespace RandomEvents
 
         private void OnSignpostChorePacketReceived(ReceiveCustomPacketEventArgs<RandomEventsSignpostChorePacket> args)
         {
+            if (!APIShared.Networking.ChoreTransport.IsChoreDelivery(args))
+                return;
+
             if (!Shared.GameplayModActivationGate.IsAllowed)
                 return;
             try { ApplySignpostInitializationChore(args?.Packet); }
@@ -1757,7 +1764,7 @@ namespace RandomEvents
                         heightElevation: spawnHeight,
                         chimp: eChimps.CHIMP_TYPE_MACEMAN));
                     if (unitId <= 0 ||
-                        !unitApi.TryGetUnitById(unitId, out GameUnit* unit) ||
+                        !APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _) ||
                         unit == null ||
                         unit->r_GlobalId == 0)
                     {
@@ -2027,10 +2034,10 @@ namespace RandomEvents
             List<int> livingUnitIds = new List<int>(pending.Units.Length);
             foreach (BanditUnitReference unitReference in pending.Units)
             {
-                if (!units.TryGetUnitById(unitReference.UnitId, out GameUnit* unit) ||
+                if (!APIShared.UnitAccess.TryGetById(units, unitReference.UnitId, out GameUnit* unit, out _) ||
                     unit == null ||
                     unit->r_GlobalId != unitReference.GlobalId ||
-                    unit->r_AliveState != AliveState.IsAlive)
+                    !APIShared.UnitAccess.IsReallyAlive(unit))
                 {
                     LogWarning(
                         $"Bandit omitted from delayed group activation: unitId={unitReference.UnitId}, " +
@@ -2071,7 +2078,7 @@ namespace RandomEvents
             foreach (int unitId in livingUnitIds)
             {
                 if (!tribes.AssignUnit(tribeId, unitId) ||
-                    !units.TryGetUnitById(unitId, out GameUnit* assignedUnit) ||
+                    !APIShared.UnitAccess.TryGetById(units, unitId, out GameUnit* assignedUnit, out _) ||
                     assignedUnit == null ||
                     assignedUnit->r_TribeId != tribeId)
                 {
@@ -2564,7 +2571,7 @@ namespace RandomEvents
                 () => Shared.DebugLogHelper.LogDebug(log, message));
         private void LogInfo(string message) =>
             Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
-                () => Shared.DebugLogHelper.LogInfo(log, message));
+                () => Shared.DebugLogHelper.LogDebug(log, message));
         private void LogWarning(string message) =>
             Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                 () => Shared.DebugLogHelper.LogWarning(log, message));

diff --git a/RandomEvents/src/RandomEventsSettingsViewModel.cs b/RandomEvents/src/RandomEventsSettingsViewModel.cs
index 769ec4c39..dce743eda 100644
--- a/RandomEvents/src/RandomEventsSettingsViewModel.cs
+++ b/RandomEvents/src/RandomEventsSettingsViewModel.cs
@@ -1,3 +1,4 @@
+using APIShared.ModSettings;
 using SHCDESE.API.Components.Network;
 using SHCDESE.NoesisUtil;
 using SHCDESE.ViewModels;
@@ -6,7 +7,7 @@ using System.Globalization;
 
 namespace RandomEvents
 {
-    public sealed class RandomEventsSettingsViewModel : Shared.PresetLobbyModSettingsViewModel
+    public sealed class RandomEventsSettingsViewModel : APIShared.ModSettings.PresetLobbyModSettingsViewModel
     {
         private bool enableMod = true;
         private int intervalMonths = 6;

diff --git a/RandomEvents/src/ScenarioSignpostRegistry.cs b/RandomEvents/src/ScenarioSignpostRegistry.cs
index d97bce0d0..3fd468017 100644
--- a/RandomEvents/src/ScenarioSignpostRegistry.cs
+++ b/RandomEvents/src/ScenarioSignpostRegistry.cs
@@ -270,8 +270,8 @@ namespace RandomEvents
 
             if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(targetPlayerId, out GamePlayerResources* resources) &&
                 resources != null && resources->r_LordUnitId > 0 && resources->r_LordUnitId <= int.MaxValue &&
-                GameUnitManagerAPI.Instance.TryGetUnitById((int)resources->r_LordUnitId, out GameUnit* lord) &&
-                lord != null && lord->r_AliveState == AliveState.IsAlive &&
+                APIShared.UnitAccess.TryGetById((int)resources->r_LordUnitId, out GameUnit* lord, out _) &&
+                lord != null && APIShared.UnitAccess.IsReallyAlive(lord) &&
                 lord->r_UnitChimp == eChimps.CHIMP_TYPE_LORD && lord->r_ControllableForPlayerId == targetPlayerId)
             {
                 AddPerimeterPathComponents(
@@ -476,7 +476,7 @@ namespace RandomEvents
                 archerSourceCoordinatesAddress = new IntPtr(
                     checked((long)playerManager + resolution.SourceXOffset));
                 targetingUnavailableReason = string.Empty;
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Native address resolved: name=archer signpost source, method={resolution.Method}, " +
                     $"rva=0x{resolution.Rva:X}, sourceXOffset=0x{resolution.SourceXOffset:X}, " +
@@ -563,12 +563,12 @@ namespace RandomEvents
                 {
                     lordFailure = $"no valid Lord unit is registered (lordUnitId={resources->r_LordUnitId})";
                 }
-                else if (!GameUnitManagerAPI.Instance.TryGetUnitById((int)resources->r_LordUnitId, out GameUnit* lord) ||
+                else if (!APIShared.UnitAccess.TryGetById((int)resources->r_LordUnitId, out GameUnit* lord, out _) ||
                          lord == null)
                 {
                     lordFailure = $"registered Lord unit {resources->r_LordUnitId} cannot be resolved";
                 }
-                else if (lord->r_AliveState != AliveState.IsAlive ||
+                else if (!APIShared.UnitAccess.IsReallyAlive(lord) ||
                          lord->r_UnitChimp != eChimps.CHIMP_TYPE_LORD ||
                          lord->r_ControllableForPlayerId != playerId)
                 {
@@ -622,7 +622,7 @@ namespace RandomEvents
                             $"0x{ReferenceSignpostIdsOffset:X}.");
                     }
 
-                    Shared.DebugLogHelper.LogInfo(
+                    Shared.DebugLogHelper.LogDebug(
                         log,
                         $"Native address resolved: name=signpost lookup, method=reference-rva, rva=0x{ExpectedLookupFunctionRva:X}.");
                     return new NativeLookupResolution(slotOffset);
@@ -637,7 +637,7 @@ namespace RandomEvents
                 int match = NativePatternResolver.FindUniquePattern(memory, LookupPattern, "signpost lookup");
                 if (TryValidateLookupCandidate(memory, match, out int slotOffset, out string validationFailure))
                 {
-                    Shared.DebugLogHelper.LogInfo(
+                    Shared.DebugLogHelper.LogDebug(
                         log,
                         $"Native address resolved: name=signpost lookup, method=signature-fallback, rva=0x{match:X}.");
                     return new NativeLookupResolution(slotOffset);
@@ -651,7 +651,7 @@ namespace RandomEvents
 
             if (TryFindUniqueStructuralCandidate(memory, out int structuralRva, out int structuralSlotOffset, out int candidateCount))
             {
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Native address resolved: name=signpost lookup, method=structural-fallback, rva=0x{structuralRva:X}.");
                 return new NativeLookupResolution(structuralSlotOffset);

diff --git a/RandomEvents/src/SignpostPlacementService.cs b/RandomEvents/src/SignpostPlacementService.cs
index 0a0beded0..867c48f0d 100644
--- a/RandomEvents/src/SignpostPlacementService.cs
+++ b/RandomEvents/src/SignpostPlacementService.cs
@@ -798,7 +798,7 @@ namespace RandomEvents
         }
 
         private void LogDebug(string message) => Shared.DebugLogHelper.LogDebug(log, message);
-        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
+        private void LogInfo(string message) => Shared.DebugLogHelper.LogDebug(log, message);
         private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
         private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);
 

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
+        /// <summary>IsAllowed in the centralized mission policy contract.</summary>
+        public static bool IsAllowed(
+            GameplayFeatureActivationProfile profile,
+            GameModeSnapshot snapshot,
+            out string reason)
+        {
+            var sharedProfile = new GameplayModActivationProfile(profile.ModGuid, profile.ModGuid,
+                profile.AllowedContexts, profile.AllowRealMultiplayer);
+            bool allowed = GameplayModModePolicy.IsAllowed(sharedProfile, snapshot, out _);
+            // Preserve the established consumer diagnostics; evaluation belongs to APIShared.
+            var context = GameplayModModePolicy.ResolveContext(snapshot);
+            if (snapshot.HasConflictingCustomizedOrigin)
+                reason = "conflicting-customize-origin";
+            else if (context == GameplayModAllowedContext.None)
+                reason = snapshot.Kind == GameModeKind.Unknown ? "unknown-fail-closed" : "owning-mod-context-not-allowed";
+            else if ((profile.AllowedContexts & context) != context)
+                reason = context == GameplayModAllowedContext.MapEditor ? "feature-not-supported-in-map-editor" : "feature-context-not-allowed";
+            else if (snapshot.IsRealMultiplayer && !profile.AllowRealMultiplayer)
+                reason = "feature-not-approved-for-real-multiplayer";
+            else
+                reason = "feature-context-allowed";
+            return allowed;
+        }
+
+        /// <summary>LogDecisions in the centralized mission policy contract.</summary>
+        public static void LogDecisions(
+            ManualLogSource log,
+            string modGuid,
+            GameModeSnapshot snapshot,
+            string source)
+        {
+            foreach (GameplayFeatureActivationProfile feature in GetProfiles(modGuid))
+            {
+                bool allowed = IsAllowed(feature, snapshot, out string reason);
+                if (!RecordDecision(feature.FeatureId, allowed))
+                    continue;
+
+                DebugLogHelper.LogDebug(
+                    log,
+                    $"[{modGuid}] gameplay-feature gate: feature={feature.FeatureId}, source={source}, " +
+                    $"kind={snapshot.Kind}, launchVariant={snapshot.LaunchVariant}, " +
+                    $"realMultiplayer={snapshot.IsRealMultiplayer}, modeAllowed={allowed}, " +
+                    $"action={(allowed ? "enabled" : "disabled-by-feature-mode")}, reason={reason}.");
+            }
+        }
+
+        private static bool RecordDecision(GameplayFeatureId featureId, bool allowed)
+        {
+            lock (LogSync)
+            {
+                bool changed = !LoggedDecisions.TryGetValue(featureId, out bool previous) ||
+                    previous != allowed;
+                LoggedDecisions[featureId] = allowed;
+                return changed;
+            }
+        }
+
+#if API_SHARED_PRESET_TESTS
+        /// <summary>RecordDecisionForTests in the centralized mission policy contract.</summary>
+        public static bool RecordDecisionForTests(GameplayFeatureId featureId, bool allowed) =>
+            RecordDecision(featureId, allowed);
+
+        /// <summary>ResetLoggedDecisionsForTests in the centralized mission policy contract.</summary>
+        public static void ResetLoggedDecisionsForTests()
+        {
+            lock (LogSync)
+                LoggedDecisions.Clear();
+        }
+#endif
+
+        private static IEnumerable<GameplayFeatureActivationProfile> GetProfiles(string modGuid)
+        {
+            switch (modGuid)
+            {
+                case "BuildingCosts_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.BuildingCostTooltip);
+                    break;
+                case "BuildingLimit_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.BuildingLimitEnforcement);
+                    break;
+                case "UnitCosts_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.UnitCostEnforcement);
+                    break;
+                case "UnitLimit_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.UnitLimitEnforcement);
+                    break;
+                case "ExtraFeatures_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.LordHealthMultipliers);
+                    break;
+                case "CheatMod_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.EndlessExtremePowersRecharge);
+                    break;
+                case "RandomEvents_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.RandomEventsRuntime);
+                    break;
+                case "ImprovedHunters_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.ImprovedHunterTargetSelection);
+                    yield return GetProfile(modGuid, GameplayFeatureId.ImprovedHunterPathfinding);
+                    break;
+                case "CastlePlanner_Serp":
+                    yield return GetProfile(modGuid, GameplayFeatureId.CastleSpawning);
+                    yield return GetProfile(modGuid, GameplayFeatureId.FreeCastlePreview);
+                    yield return GetProfile(modGuid, GameplayFeatureId.CastleBlueprints);
+                    break;
+            }
+        }
+    }
+}

diff --git a/Shared/Adapters/APIShared/GameplayModActivationGate.cs b/Shared/Adapters/APIShared/GameplayModActivationGate.cs
new file mode 100644
index 000000000..1960fc534
--- /dev/null
+++ b/Shared/Adapters/APIShared/GameplayModActivationGate.cs
@@ -0,0 +1,148 @@
+using APIShared.GameModes;
+using BepInEx.Logging;
+using System;
+#if !API_SHARED_PRESET_TESTS
+using R3;
+using SHCDESE.EventAPI;
+using SHCDESE.EventAPI.MapLoader;
+#endif
+
+namespace Shared
+{
+    /// <summary>
+    /// Caches the current map policy for one mod assembly. Shared sources are linked
+    /// into every mod, so one mod can never accidentally change another mod's state.
+    /// </summary>
+    internal static class GameplayModActivationGate
+    {
+        private static ManualLogSource log;
+        private static GameplayModActivationProfile profile;
+        private static Func<bool> configuredEnabledProvider;
+        private static GameplayModeGate gate;
+        private static GameModeSnapshot snapshot => gate?.Snapshot ?? default;
+        private static bool isAllowed => gate?.IsAllowed ?? false;
+        private static bool initialized;
+        private static bool routineLoggingEnabled = true;
+        internal static event Action<bool> StateChanged;
+
+        internal static bool IsAllowed => isAllowed;
+        internal static GameModeSnapshot Snapshot => snapshot;
+        internal static bool IsEnabled(bool configuredEnabled) => configuredEnabled && IsAllowed;
+
+        internal static void Initialize(
+            ManualLogSource logger,
+            string modGuid,
+            string displayName,
+            Func<bool> isConfiguredEnabled,
+            bool logRoutineActivity = true)
+        {
+            if (initialized)
+                return;
+
+            log = logger;
+            profile = SerpsModProfiles.GetProfile(modGuid, displayName);
+            gate = new GameplayModeGate(profile);
+            configuredEnabledProvider = isConfiguredEnabled ?? throw new ArgumentNullException(nameof(isConfiguredEnabled));
+            routineLoggingEnabled = logRoutineActivity;
+
+            MissionEvents.SetOwner(modGuid);
+            MissionEvents.SetGate(e =>
+            {
+                if (e.Kind == APIShared.MissionLifecycleKind.End) Reset("MissionEnd");
+                else Update(e.Context.Mode, $"Mission{e.Kind}({e.Phase}, session={e.Context.SessionId})");
+            });
+            initialized = true;
+            LogTransition("initialization", policyChanged: false);
+        }
+
+        private static void Update(GameModeSnapshot next, string source)
+        {
+            bool previousAllowed = isAllowed;
+            bool changed = next.Kind != snapshot.Kind ||
+                next.LaunchVariant != snapshot.LaunchVariant ||
+                next.CustomizedTrailId != snapshot.CustomizedTrailId ||
+                next.CustomizedMissionId != snapshot.CustomizedMissionId ||
+                next.IsRealMultiplayer != snapshot.IsRealMultiplayer ||
+                next.HasConflictingCustomizedOrigin != snapshot.HasConflictingCustomizedOrigin;
+            gate.Update(next);
+            if (changed)
+                LogTransition(source, previousAllowed != isAllowed);
+            if (previousAllowed != isAllowed)
+                NotifyStateChanged(isAllowed);
+        }
+
+        private static void Reset(string source)
+        {
+            bool changed = snapshot.Kind != GameModeKind.Unknown ||
+                snapshot.LaunchVariant != GameModeLaunchVariant.Standard;
+            bool previousAllowed = isAllowed;
+            gate.Update(default(GameModeSnapshot));
+            if (changed)
+                LogTransition(source, previousAllowed != isAllowed);
+            if (previousAllowed)
+                NotifyStateChanged(false);
+        }
+
+        private static void NotifyStateChanged(bool allowed)
+        {
+            Delegate[] handlers = StateChanged?.GetInvocationList();
+            if (handlers == null)
+                return;
+
+            foreach (Delegate handler in handlers)
+            {
+                try { ((Action<bool>)handler)(allowed); }
+                catch (Exception ex)
+                {
+                    DebugLogHelper.LogError(
+                        log,
+                        $"[{profile.DisplayName}] gameplay-mod gate listener failed closed: {ex}");
+                }
+            }
+        }
+
+        private static void LogTransition(string source, bool policyChanged)
+        {
+            bool configuredEnabled = ReadConfiguredEnabled();
+            if (!routineLoggingEnabled)
+                return;
+
+            bool effectiveEnabled = configuredEnabled && IsAllowed;
+            GameplayModModePolicy.IsAllowed(profile, snapshot, out string reason);
+            string action = effectiveEnabled
+                ? "enabled"
+                : !IsAllowed ? "disabled-by-mode" : "restriction-lifted-setting-disabled";
+            string message =
+                $"[{profile.DisplayName}] gameplay-mod gate: modGuid={profile.ModGuid}, source={source}, " +
+                $"kind={snapshot.Kind}, launchVariant={snapshot.LaunchVariant}, " +
+                $"customized={snapshot.IsCustomized}, customizedOrigin={snapshot.CustomizedOriginKind}, " +
+                $"modeAllowed={IsAllowed}, configuredEnabled={configuredEnabled}, " +
+                $"effectiveEnabled={effectiveEnabled}, action={action}, reason={reason}.";
+            DebugLogHelper.LogDebug(log, message);
+            GameplayFeatureModePolicy.LogDecisions(log, profile.ModGuid, snapshot, source);
+        }
+
+        private static bool ReadConfiguredEnabled()
+        {
+            try { return configuredEnabledProvider?.Invoke() == true; }
+            catch (Exception ex)
+            {
+                DebugLogHelper.LogError(log, $"[{profile.DisplayName}] EnableMod provider failed closed: {ex}");
+                return false;
+            }
+        }
+
+#if API_SHARED_PRESET_TESTS
+        internal static void SetSnapshotForTests(GameModeSnapshot next) => Update(next, "test");
+        internal static void SetLoadSnapshotForTests(GameModeSnapshot next) => Update(next, "test-load");
+        internal static void SetStartSnapshotForTests(GameModeSnapshot next) => Update(next, "test-start");
+        internal static void ResetForTests()
+        {
+            profile = SerpsModProfiles.GetProfile("ExtraFeatures_Serp", "Extra Features");
+            if (gate == null) gate = new GameplayModeGate(profile);
+            configuredEnabledProvider = () => true;
+            Reset("test-reset");
+        }
+#endif
+    }
+}

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
+            {
+                nativePlayerId = GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? 0;
+            }
+            catch
+            {
+                // Native player resources are not guaranteed to exist in the lobby.
+            }
+
+            int lobbyPlayerId = 0;
+            try
+            {
+                if (localSteamId != 0 && platform?.activeLobby != null)
+                    lobbyPlayerId = platform.activeLobby.getThisPlayerFromSteamID(localSteamId);
+            }
+            catch
+            {
+                // The final lobby mapping can still be under construction.
+            }
+
+            return ResolveLocalPlayerId(
+                realMultiplayer,
+                preferInGameRoster && humanGameMembers.Length > 0,
+                nativePlayerId,
+                gameMemberPlayerId,
+                lobbyPlayerId);
+        }
+
+        internal static PlayerIdentityResolution CapturePlayerIdForSteamId(
+            ulong steamId,
+            bool preferInGameRoster)
+        {
+            if (!TryCaptureHumanRoster(
+                    preferInGameRoster,
+                    out Dictionary<int, ulong> playersById,
+                    out string error,
+                    out string diagnostic))
+                return Failure(error);
+
+            PlayerIdentityResolution resolution = ResolvePlayerIdForSteamId(
+                steamId,
+                playersById);
+            if (!resolution.IsResolved)
+                return resolution;
+
+            if (preferInGameRoster)
+            {
+                Platform_Multiplayer.MPLobby lobby = Platform_Multiplayer.Instance?.activeLobby;
+                int vanillaPlayerId = 0;
+                int networkLobbyPlayerId = 0;
+                try
+                {
+                    if (lobby != null)
+                        vanillaPlayerId = lobby.getThisPlayerFromSteamID(steamId);
+                }
+                catch
+                {
+                    // The lobby can disappear while the in-game roster remains authoritative.
+                }
+                try
+                {
+                    networkLobbyPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(
+                        new CSteamID(steamId));
+                }
+                catch
+                {
+                    // Lobby order is diagnostic-only after the game roster exists.
+                }
+
+                if (IsValidPlayerId(vanillaPlayerId) &&
+                    vanillaPlayerId != resolution.PlayerId)
+                {
+                    return Failure(
+                        $"Final lobby mapping disagrees with the authoritative in-game player slot for " +
+                        $"Steam identity {steamId}: gameMember={resolution.PlayerId}, lobby={vanillaPlayerId}, " +
+                        $"networkLobby={networkLobbyPlayerId}.");
+                }
+                if (IsValidPlayerId(networkLobbyPlayerId) &&
+                    networkLobbyPlayerId != resolution.PlayerId)
+                {
+                    diagnostic =
+                        $"Lobby-order player ID differs from the final in-game slot for Steam identity " +
+                        $"{steamId}: networkLobby={networkLobbyPlayerId}, final={resolution.PlayerId}.";
+                }
+            }
+            return Success(resolution.PlayerId, diagnostic);
+        }
+
+        internal static string CaptureProvisionalPlayerIdDiagnostic(
+            ulong steamId,
+            int finalPlayerId,
+            bool inGame)
+        {
+            if (steamId == 0 || !IsValidPlayerId(finalPlayerId))
+                return string.Empty;
+
+            try
+            {
+                int provisionalPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(
+                    new CSteamID(steamId));
+                if (!IsValidPlayerId(provisionalPlayerId) ||
+                    provisionalPlayerId == finalPlayerId)
+                {
+                    return string.Empty;
+                }
+
+                return inGame
+                    ? $"Lobby-order player ID differs from the final in-game slot for Steam identity " +
+                      $"{steamId}: networkLobby={provisionalPlayerId}, final={finalPlayerId}."
+                    : $"Script Extender lobby-order player ID differs from Vanilla's final lobby mapping " +
+                      $"for Steam identity {steamId}: networkLobby={provisionalPlayerId}, " +
+                      $"finalLobby={finalPlayerId}.";
+            }
+            catch
+            {
+                return string.Empty;
+            }
+        }
+
+        internal static bool TryCaptureHumanRoster(
+            bool preferInGameRoster,
+            out Dictionary<int, ulong> playersById,
+            out string error) =>
+            TryCaptureHumanRoster(
+                preferInGameRoster,
+                requireAuthoritativeLobbyRoster: false,
+                out playersById,
+                out error,
+                out _);
+
+        internal static bool TryCaptureHumanRoster(
+            bool preferInGameRoster,
+            out Dictionary<int, ulong> playersById,
+            out string error,
+            out string diagnostic) =>
+            TryCaptureHumanRoster(
+                preferInGameRoster,
+                requireAuthoritativeLobbyRoster: false,
+                out playersById,
+                out error,
+                out diagnostic);
+
+        internal static bool TryCaptureHumanRoster(
+            bool preferInGameRoster,
+            bool requireAuthoritativeLobbyRoster,
+            out Dictionary<int, ulong> playersById,
+            out string error,
+            out string diagnostic)
+        {
+            playersById = new Dictionary<int, ulong>();
+            diagnostic = string.Empty;
+            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
+            Platform_Multiplayer.MPGameMember[] humanGameMembers = platform?.gameMembers?
+                .Where(member => member != null && !member.kicked && !member.skirmishAI)
+                .ToArray() ?? Array.Empty<Platform_Multiplayer.MPGameMember>();
+            if (preferInGameRoster)
+            {
+                if (humanGameMembers.Length == 0)
+                {
+                    error = "The active in-game human roster is unavailable.";
+                    return false;
+                }
+                foreach (Platform_Multiplayer.MPGameMember member in humanGameMembers)
+                {
+                    if (!TryAddPlayer(playersById, member.playerID, member.steamID, out error))
+                        return false;
+                }
+                error = string.Empty;
+                return true;
+            }
+
+            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;
+            if (lobby?.members == null)
+            {
+                error = "The active human lobby roster is unavailable.";
+                return false;
+            }
+
+            var diagnostics = new List<string>();
+            foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
+            {
+                if (member == null || member.dummyToBeKicked ||
+                    (member.SkirmishMember && !member.SkirmishHumanMember))
+                    continue;
+                ulong steamId = member.id.m_SteamID;
+                int vanillaPlayerId = lobby.getThisPlayerFromSteamID(steamId);
+                int networkLobbyPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(member.id);
+                if (requireAuthoritativeLobbyRoster && !IsValidPlayerId(vanillaPlayerId))
+                {
+                    error =
+                        $"Vanilla has not assigned a final player slot to lobby member {steamId} yet.";
+                    return false;
+                }
+                int playerId = IsValidPlayerId(vanillaPlayerId)
+                    ? vanillaPlayerId
+                    : networkLobbyPlayerId;
+                if (IsValidPlayerId(vanillaPlayerId) &&
+                    IsValidPlayerId(networkLobbyPlayerId) &&
+                    vanillaPlayerId != networkLobbyPlayerId)
+                {
+                    diagnostics.Add(
+                        $"steamId={steamId}: networkLobby={networkLobbyPlayerId}, finalLobby={vanillaPlayerId}");
+                }
+                else if (!IsValidPlayerId(vanillaPlayerId) &&
+                         IsValidPlayerId(networkLobbyPlayerId))
+                {
+                    diagnostics.Add(
+                        $"steamId={steamId}: only provisional networkLobby={networkLobbyPlayerId} is available");
+                }
+                if (!TryAddPlayer(playersById, playerId, steamId, out error))
+                    return false;
+            }
+
+            if (playersById.Count == 0)
+            {
+                error = "The active lobby contains no resolved human players.";
+                return false;
+            }
+            diagnostic = diagnostics.Count == 0
+                ? string.Empty
+                : "Lobby player-ID source differences: " + string.Join("; ", diagnostics) + ".";
+            error = string.Empty;
+            return true;
+        }
+#endif
+
+        private static bool TryAddPlayer(
+            IDictionary<int, ulong> playersById,
+            int playerId,
+            ulong steamId,
+            out string error)
+        {
+            if (!IsValidPlayerId(playerId) || steamId == 0)
+            {
+                error = $"A human player has an invalid final identity: playerId={playerId}, steamId={steamId}.";
+                return false;
+            }
+            if (playersById.TryGetValue(playerId, out ulong existingSteamId) && existingSteamId != steamId)
+            {
+                error = $"Final player slot {playerId} is assigned to multiple Steam identities.";
+                return false;
+            }
+            if (playersById.Any(player => player.Key != playerId && player.Value == steamId))
+            {
+                error = $"Steam identity {steamId} is assigned to multiple final player slots.";
+                return false;
+            }
+            playersById[playerId] = steamId;
+            error = string.Empty;
+            return true;
+        }
+
+        private static PlayerIdentityResolution Success(int playerId, string diagnostic) =>
+            new PlayerIdentityResolution(playerId, true, string.Empty, diagnostic);
+
+        private static PlayerIdentityResolution Failure(string error) =>
+            new PlayerIdentityResolution(0, false, error, string.Empty);
+
+        private static bool IsValidPlayerId(int playerId) =>
+            playerId >= FirstPlayerId && playerId <= LastPlayerId;
+    }
+
+}

diff --git a/Shared/Adapters/APIShared/SerpsModProfiles.cs b/Shared/Adapters/APIShared/SerpsModProfiles.cs
new file mode 100644
index 000000000..ac13dd358
--- /dev/null
+++ b/Shared/Adapters/APIShared/SerpsModProfiles.cs
@@ -0,0 +1,40 @@
+using System;
+using APIShared.GameModes;
+
+namespace Shared
+{
+    /// <summary>Established permissions for Serps mods, separate from the optional general evaluator.</summary>
+    internal static class SerpsModProfiles
+    {
+        private const GameplayModAllowedContext RegularContexts =
+            GameplayModAllowedContext.CustomGame |
+            GameplayModAllowedContext.CustomizedVanillaTrail |
+            GameplayModAllowedContext.CustomizedCustomTrail |
+            GameplayModAllowedContext.CustomizedCoopTrail |
+            GameplayModAllowedContext.CustomizedSandsOfTime |
+            GameplayModAllowedContext.MapEditor;
+
+        /// <summary>Gets the established Serps gameplay profile. Unknown mod GUIDs are rejected; third-party mods construct their own profile.</summary>
+        public static GameplayModActivationProfile GetProfile(string modGuid, string displayName)
+        {
+            switch (modGuid)
+            {
+                case "BuildingCosts_Serp":
+                case "BuildingLimit_Serp":
+                case "CastlePlanner_Serp":
+                case "CheatMod_Serp":
+                case "ExtraFeatures_Serp":
+                case "ExtremePowers_Serp":
+                case "ImprovedHunters_Serp":
+                case "RandomEvents_Serp":
+                case "StartConditions_Serp":
+                case "UnitCosts_Serp":
+                case "UnitLimit_Serp":
+                    return new GameplayModActivationProfile(modGuid, displayName, RegularContexts);
+                default:
+                    throw new ArgumentOutOfRangeException(nameof(modGuid), modGuid, "Unknown gameplay mod GUID.");
+            }
+        }
+
+    }
+}

diff --git a/Shared/Runtime/Diagnostics/DebugLogHelper.cs b/Shared/Runtime/Diagnostics/DebugLogHelper.cs
new file mode 100644
index 000000000..0b144f4bf
--- /dev/null
+++ b/Shared/Runtime/Diagnostics/DebugLogHelper.cs
@@ -0,0 +1,250 @@
+using BepInEx.Logging;
+using System;
+using System.Globalization;
+using System.IO;
+using System.Reflection;
+using System.Security.Cryptography;
+
+namespace Shared
+{
+    internal static class DebugLogHelper
+    {
+        public const string CurrentNativeSha256 =
+            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
+
+        private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(1);
+        private static DateTime debugEnabledCacheExpiresAtUtc;
+        private static bool debugEnabledCache;
+
+        public static bool IsDebugEnabled()
+        {
+            DateTime now = DateTime.UtcNow;
+            if (now < debugEnabledCacheExpiresAtUtc)
+                return debugEnabledCache;
+
+            debugEnabledCache = ComputeDebugEnabled();
+            debugEnabledCacheExpiresAtUtc = now + CacheDuration;
+            return debugEnabledCache;
+        }
+
+        public static bool IsDiskDebugEnabled()
+        {
+            try
+            {
+                foreach (ILogListener listener in Logger.Listeners)
+                {
+                    if (listener is DiskLogListener diskLogListener &&
+                        HasDebugFlag(diskLogListener.DisplayedLogLevel))
+                    {
+                        return true;
+                    }
+                }
+            }
+            catch
+            {
+            }
+
+            return false;
+        }
+
+        public static void LogDebug(ManualLogSource log, params object[] parts)
+        {
+            if (log == null || !IsDebugEnabled())
+                return;
+
+            log.LogDebug(WithTimestamp(string.Join(" ", parts)));
+        }
+
+        public static void LogDebug(ManualLogSource log, Func<string> messageFactory)
+        {
+            if (log == null || messageFactory == null || !IsDebugEnabled())
+                return;
+
+            log.LogDebug(WithTimestamp(messageFactory()));
+        }
+
+        public static void LogInfo(ManualLogSource log, string message)
+        {
+            log?.LogInfo(WithTimestamp(message));
+        }
+
+        public static void LogWarning(ManualLogSource log, string message)
+        {
+            log?.LogWarning(WithTimestamp(message));
+        }
+
+        public static void LogError(ManualLogSource log, string message)
+        {
+            log?.LogError(WithTimestamp(message));
+        }
+
+        public static bool ReportNativeLibraryVersion(
+            ManualLogSource log,
+            string componentName,
+            bool requireCurrentVersion = false,
+            bool logSuccess = true)
+        {
+            string label = string.IsNullOrWhiteSpace(componentName)
+                ? "Mod"
+                : componentName;
+
+            try
+            {
+                string path = Path.Combine(
+                    BepInEx.Paths.GameRootPath,
+                    "Stronghold Crusader Definitive Edition_Data",
+                    "Plugins",
+                    "x86_64",
+                    "CrusaderDE.dll");
+                if (!File.Exists(path))
+                {
+                    LogError(log, $"{label} cannot verify CrusaderDE.dll because the installed file was not found: path={path}.");
+                    return false;
+                }
+
+                string actualHash;
+                using (FileStream stream = File.OpenRead(path))
+                using (SHA256 sha256 = SHA256.Create())
+                {
+                    actualHash = BitConverter.ToString(sha256.ComputeHash(stream))
+                        .Replace("-", string.Empty);
+                }
+
+                long fileSize = new FileInfo(path).Length;
+                if (string.Equals(actualHash, CurrentNativeSha256, StringComparison.OrdinalIgnoreCase))
+                {
+                    if (logSuccess)
+                    {
+                        LogDebug(
+                            log,
+                            $"{label} verified the installed CrusaderDE.dll: sha256={actualHash}, size={fileSize}, path={path}.");
+                    }
+                    return true;
+                }
+
+                string message =
+                    $"{label} detected a changed CrusaderDE.dll: expectedSha256={CurrentNativeSha256}, " +
+                    $"actualSha256={actualHash}, size={fileSize}, path={path}.";
+                if (requireCurrentVersion)
+                {
+                    LogError(log, message + " Version-sensitive native code remains inactive.");
+                }
+                else
+                {
+                    LogWarning(
+                        log,
+                        message +
+                        " Signature-validated code may continue; any failed validation is logged and the affected feature remains inactive.");
+                }
+
+                return false;
+            }
+            catch (Exception ex)
+            {
+                if (requireCurrentVersion)
+                {
+                    LogError(log, $"{label} could not verify the installed CrusaderDE.dll; version-sensitive native code must remain inactive: {ex}");
+                }
+                else
+                {
+                    LogWarning(
+                        log,
+                        $"{label} could not verify the installed CrusaderDE.dll hash. " +
+                        $"Signature-validated code may continue; any failed validation is logged and the affected feature remains inactive. Reason: {ex}");
+                }
+                return false;
+            }
+        }
+
+        public static bool IsCurrentNativeLibraryVersion()
+        {
+            try
+            {
+                string path = Path.Combine(
+                    BepInEx.Paths.GameRootPath,
+                    "Stronghold Crusader Definitive Edition_Data",
+                    "Plugins",
+                    "x86_64",
+                    "CrusaderDE.dll");
+                if (!File.Exists(path))
+                    return false;
+
+                using (FileStream stream = File.OpenRead(path))
+                using (SHA256 sha256 = SHA256.Create())
+                {
+                    string actualHash = BitConverter.ToString(sha256.ComputeHash(stream))
+                        .Replace("-", string.Empty);
+                    return string.Equals(
+                        actualHash,
+                        CurrentNativeSha256,
+                        StringComparison.OrdinalIgnoreCase);
+                }
+            }
+            catch
+            {
+                // Callers use false to keep only fixed-layout native code inactive.
+                return false;
+            }
+        }
+
+        private static string WithTimestamp(string message)
+        {
+            return $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)}] {message ?? string.Empty}";
+        }
+
+        private static bool ComputeDebugEnabled()
+        {
+            try
+            {
+                foreach (ILogListener listener in Logger.Listeners)
+                {
+                    if (IsDebugEnabled(listener))
+                        return true;
+                }
+            }
+            catch
+            {
+            }
+
+            return false;
+        }
+
+        private static bool IsDebugEnabled(ILogListener listener)
+        {
+            if (listener == null)
+                return false;
+
+            if (listener is DiskLogListener diskLogListener)
+                return HasDebugFlag(diskLogListener.DisplayedLogLevel);
+
+            object displayedLogLevel = TryGetPropertyValue(listener, "DisplayedLogLevel");
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/RandomEvents.diff).
