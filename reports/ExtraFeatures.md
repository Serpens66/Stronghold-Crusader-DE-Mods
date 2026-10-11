# ExtraFeatures release status

**Status:** code newer

- Release: [v1.0.108](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtraFeatures/v1.0.108)
- Release commit: [1fc7fd3](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/1fc7fd3748bb522199db6b08adf669cb89c16d30)
- Current main commit: [2c95ad4](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/2c95ad4c3cefecb00bdadc503704e56a025a37f7)

## Relevant changed files

- `APIShared`
- `ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/ExtraFeatures.dll`
- `ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/ExtraFeatures.pdb`
- `ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/info.json`
- `ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml`
- `ExtraFeatures/build.bat`
- `ExtraFeatures/ExtraFeatures.csproj`
- `ExtraFeatures/info.json`
- `ExtraFeatures/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml`
- `ExtraFeatures/src/AIDefenseRepairRuntime.cs`
- `ExtraFeatures/src/AIMarketVanillaPriceHook.cs`
- `ExtraFeatures/src/BuildingRepairHudRuntime.cs`
- `ExtraFeatures/src/ChurchPriestCountRuntime.cs`
- `ExtraFeatures/src/ElevatedMoatPatch.cs`
- `ExtraFeatures/src/ExtraFeaturesPlugin.cs`
- `ExtraFeatures/src/ExtraFeaturesRuntime.cs`
- `ExtraFeatures/src/ExtraFeaturesViewModel.cs`
- `ExtraFeatures/src/FearFactorNeutralizationRuntime.cs`
- `ExtraFeatures/src/GatehouseAutomationRuntime.cs`
- `ExtraFeatures/src/HealerNativeContract.cs`
- `ExtraFeatures/src/HealerTargetsRuntime.cs`
- `ExtraFeatures/src/KnightDismountDelayRuntime.cs`
- `ExtraFeatures/src/KnightDismountRuntime.cs`
- `ExtraFeatures/src/LordHealthRuntime.cs`
- `ExtraFeatures/src/MonkAlwaysRunPatch.cs`
- `ExtraFeatures/src/MultiplayerFeatureGate.cs`
- `ExtraFeatures/src/PlagueApothecarySearchRangePatch.cs`
- `ExtraFeatures/verify-repair.ps1`
- `Shared/Adapters/APIShared/DirectLaunchSettingsNotice.cs`
- `Shared/Adapters/APIShared/GameplayFeatureModePolicy.cs`
- `Shared/Adapters/APIShared/GameplayModActivationGate.cs`
- `Shared/Adapters/APIShared/MissionEventsAdapter.cs`
- `Shared/Adapters/APIShared/PlayerIdentityHelper.cs`
- `Shared/Adapters/APIShared/SerpsModProfiles.cs`
- `Shared/Runtime/Diagnostics/CrashBreadcrumbDiagnostics.cs`
- `Shared/Runtime/Diagnostics/CrashBreadcrumbRecorder.cs`
- `Shared/Runtime/Diagnostics/DebugLogHelper.cs`
- `Shared/Runtime/Gameplay/GameProjectileSlotPolicy.cs`
- `Shared/Runtime/Gameplay/GatehouseQueryUnitIdPolicy.cs`
- `Shared/Runtime/Gameplay/SelectedChimpsSnapshotPolicy.cs`
- `Shared/Runtime/Localization/SerpLocalization.cs`
- `Shared/Runtime/Native/GameBuildingFootprint.cs`
- `Shared/Runtime/Native/NativePatternResolver.cs`
- `Shared/Runtime/Threading/UnityMainThreadDispatch.cs`
- `Shared/Runtime/UI/NumericTextInput.cs`
- `Shared/Runtime/UI/ToolTipPresentation.cs`
- `Shared/Runtime/UI/TroopActionButtonLayout.cs`
- `Shared/Runtime/UI/TroopActionButtonLayoutPolicy.cs`

Relevant localization keys: `Common.Ai`, `Common.Human`

The localization helper also contains a general logic change that affects every consumer.

## Diff

```diff
diff --git a/ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/info.json b/ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/info.json
index 03c0cb6b0..5b50708c4 100644
--- a/ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/info.json
+++ b/ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/info.json
@@ -3,13 +3,49 @@
   "Author": "Serpens66",
   "Name": "Extra Features",
   "Description": "Optional gameplay additions and economy customization for Stronghold Crusader Definitive Edition.",
-  "Version": "1.0.108",
+  "Version": "1.0.113",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
-  "MinimumScriptExtenderVersion": "2.13.0",
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
+      "Version": "1.0.113",
+      "Changes": [
+        "Use APIShared 0.7.0 shared multiplayer transport while preserving existing packet schemas and gameplay execution."
+      ]
+    },
+    {
+      "Version": "1.0.112",
+      "Changes": [
+        "Use APIShared 0.6.0 coordinated hooks and event contracts to improve compatibility with other mods; preserve existing gameplay behavior."
+      ]
+    },
+    {
+      "Version": "1.0.111",
+      "Changes": [
+        "Migrate to APIShared 0.5.0 shared services and updated mod-owned adapters while preserving existing features."
+      ]
+    },
+    {
+      "Version": "1.0.110",
+      "Changes": [
+        "Updated Script Extender 2.14.0 unit field contracts; preserved death-marker and full control-word semantics."
+      ]
+    },
+    {
+      "Version": "1.0.109",
+      "Changes": [
+        "Move routine diagnostics to Debug; preserve warnings, errors and explicit file-operation results."
+      ]
+    },
     {
       "Version": "1.0.108",
       "Changes": [

diff --git a/ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml b/ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml
index 305a2c7e5..bd694441e 100644
--- a/ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml
+++ b/ExtraFeatures/BepInEx/plugins/ExtraFeatures_Serp/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml
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
-    <Style x:Key="ModSettingsSearchTargetGrid" TargetType="{x:Type Grid}">
+    <Style x:Key="ModSettingsSearchTargetGrid" TargetType="{x:Type Grid}"><Setter Property="shared:ModSettingsMode.Key" Value="{Binding RelativeSource={RelativeSource Self}, Path=(shared:ModSettingsSearch.Key)}"/>
       <Setter Property="Visibility">
         <Setter.Value>
           <MultiBinding Converter="{StaticResource ModSettingsSearchVisibilityConverter}">
@@ -157,7 +158,7 @@
         <TextBlock Text="{Binding System_ModSettingsSearchNoResultsText}" Visibility="{Binding System_ModSettingsSearchNoResultsVisibility}" Foreground="#FFCC66" HorizontalAlignment="Left" Margin="0,4,0,0"/>
       </StackPanel>
       <TextBlock Text="{Binding ActionsScopeNoticeText}" Visibility="{Binding ActionsScopeNoticeVisibility}" Foreground="#BBBBBB" TextWrapping="Wrap" Margin="0,0,0,8"/>
-      <TextBlock Text="{Binding System_DirectLaunchNoticeText}" Visibility="{Binding System_DirectLaunchNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
+      <TextBlock Text="{Binding System_ConsumerModeNoticeText}" Visibility="{Binding System_ConsumerModeNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding System_TrailSourceNoticeText}" Visibility="{Binding System_TrailSourceNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostReadOnlyNoticeText}" Visibility="{Binding HostReadOnlyNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostOptionsText}" Style="{StaticResource HostRoleHeader}" Margin="0,2,0,6"/>

diff --git a/ExtraFeatures/build.bat b/ExtraFeatures/build.bat
index 5f4521e69..822d53859 100644
--- a/ExtraFeatures/build.bat
+++ b/ExtraFeatures/build.bat
@@ -1,15 +1,45 @@
 @echo off
+setlocal EnableExtensions
+set "BUILD_DRIVER_NOPAUSE=0"
+for %%A in (%*) do if /I "%%~A"=="/nopause" set "BUILD_DRIVER_NOPAUSE=1"
+set "BUILD_DRIVER_ORIGINAL_DIR=%CD%"
+echo [%date% %time%] START ExtraFeatures
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
 set "MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
 set "GAME_DIR=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition"
 set "GAME_SCRIPT_EXTENDER_DIR=%GAME_DIR%\BepInEx\plugins\000shcdese"
-set "API_SHARED_DIR=%GAME_DIR%\BepInEx\plugins\APIShared_Serp"
+set "API_SHARED_DIR=%~dp0..\APIShared\BepInEx\plugins\APIShared_Serp"
 rem The installed release is canonical; SHCDESE_EXTENDER_DIR is the explicit override.
 if defined SHCDESE_EXTENDER_DIR set "GAME_SCRIPT_EXTENDER_DIR=%SHCDESE_EXTENDER_DIR%"
 rem Release automation can explicitly use the validated workspace package.
 if defined SHCDE_API_SHARED_DIR set "API_SHARED_DIR=%SHCDE_API_SHARED_DIR%"
+echo [%date% %time%] PowerShell checks / build step
+powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0..\Shared\Tools\ApiSharedRepository\Test-ConsumerPackage.ps1" -Workspace "%~dp0.." -PackageDirectory "%API_SHARED_DIR%"
+if errorlevel 1 exit /b 1
 set "PLUGIN_NAME=ExtraFeatures_Serp"
 set "LOCAL_PLUGIN_DIR=%PROJECT_DIR%BepInEx\plugins\%PLUGIN_NAME%"
 set "GAME_PLUGIN_DIR=%GAME_DIR%\BepInEx\plugins\%PLUGIN_NAME%"
@@ -18,14 +48,15 @@ set "NO_PAUSE=0"
 for %%A in (%*) do if /I "%%~A"=="/nopause" set "NO_PAUSE=1"
 
 rem Never touch build or installation output while the game has plugin DLLs loaded.
+echo [%date% %time%] Check that the game is closed
 powershell.exe -NoProfile -Command "if (Get-Process -Name 'Stronghold Crusader Definitive Edition' -ErrorAction SilentlyContinue) { exit 1 } else { exit 0 }" >nul 2>&1
 if errorlevel 1 (
   echo Build und Installation abgebrochen: Stronghold Crusader Definitive Edition ist noch gestartet.
   echo Lokales Paket und installierter Mod wurden nicht veraendert.
-  if "%NO_PAUSE%"=="0" pause
   exit /b 1
 )
 
+echo [%date% %time%] PowerShell checks / build step
 powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%verify-repair.ps1"
 if errorlevel 1 goto build_failed
 
@@ -36,6 +67,7 @@ if exist "%GAME_SCRIPT_EXTENDER_DIR%\SHCDESE.dll" (
 if not exist "%API_SHARED_DIR%\APIShared.dll" goto build_failed
 
 pushd "%PROJECT_DIR%"
+echo [%date% %time%] Compile projects
 "%MSBUILD%" "%PROJECT_DIR%..\_inspect\ExtraFeaturesNativeTests\ExtraFeaturesNativeTests.csproj" /p:Configuration=Release /p:GameDir="%GAME_DIR%"
 if errorlevel 1 goto build_failed_popd
 "%PROJECT_DIR%..\_inspect\ExtraFeaturesNativeTests\bin\ExtraFeaturesNativeTests.exe"
@@ -44,11 +76,14 @@ popd
 
 if exist "%LOCAL_PLUGIN_DIR%\" rmdir /S /Q "%LOCAL_PLUGIN_DIR%"
 pushd "%PROJECT_DIR%"
+echo [%date% %time%] Compile projects
 "%MSBUILD%" ExtraFeatures.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%" /p:ApiSharedDir="%API_SHARED_DIR%"
 if errorlevel 1 goto build_failed_popd
 popd
 
+echo [%date% %time%] Copy package files
 copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
+echo [%date% %time%] Copy package files
 xcopy "%PROJECT_DIR%Override" "%LOCAL_PLUGIN_DIR%\Override\" /E /I /Q /Y >nul
 if not exist "%LOCAL_PLUGIN_DIR%\ExtraFeatures.dll" goto package_failed
 if not exist "%LOCAL_PLUGIN_DIR%\info.json" goto package_failed
@@ -68,26 +103,24 @@ if exist "%GAME_PLUGIN_DIR%\" (
     )
   )
 )
+echo [%date% %time%] Copy package files
 xcopy "%LOCAL_PLUGIN_DIR%" "%GAME_PLUGIN_DIR%\" /E /I /Q /Y >nul
 if errorlevel 1 goto copy_failed
-powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Release\Write-LocalBuildManifest.ps1" -ModName ExtraFeatures
+echo [%date% %time%] PowerShell checks / build step
+powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PROJECT_DIR%..\Shared\Tools\Release\Write-LocalBuildManifest.ps1" -ModName ExtraFeatures
 if errorlevel 1 goto package_failed
 
 echo Build und Installation von Extra Features erfolgreich.
-if "%NO_PAUSE%"=="0" pause
 exit /b 0
 
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

diff --git a/ExtraFeatures/ExtraFeatures.csproj b/ExtraFeatures/ExtraFeatures.csproj
index 9a3ccfad8..e2619a921 100644
--- a/ExtraFeatures/ExtraFeatures.csproj
+++ b/ExtraFeatures/ExtraFeatures.csproj
@@ -38,8 +38,8 @@
     <Reference Include="MessagePack.Annotations"><HintPath>$(ExtenderDir)\MessagePack.Annotations.dll</HintPath><Private>false</Private></Reference>
   </ItemGroup>
   <ItemGroup>
-    <Compile Include="..\Shared\CrashBreadcrumbDiagnostics.cs"><Link>Shared\CrashBreadcrumbDiagnostics.cs</Link></Compile><Compile Include="..\Shared\CrashBreadcrumbRecorder.cs"><Link>Shared\CrashBreadcrumbRecorder.cs</Link></Compile><Compile Include="..\Shared\DebugLogHelper.cs"><Link>Shared\DebugLogHelper.cs</Link></Compile><Compile Include="..\Shared\UnityMainThreadDispatch.cs"><Link>Shared\UnityMainThreadDispatch.cs</Link></Compile><Compile Include="..\Shared\NativePatternResolver.cs"><Link>Shared\NativePatternResolver.cs</Link></Compile><Compile Include="..\Shared\SelectedChimpsSnapshotPolicy.cs"><Link>Shared\SelectedChimpsSnapshotPolicy.cs</Link></Compile><Compile Include="..\Shared\SerpLocalization.cs"><Link>Shared\SerpLocalization.cs</Link></Compile>
-    <Compile Include="src\AIDefenseRepairRuntime.cs" /><Compile Include="src\AIMarketVanillaPriceHook.cs" /><Compile Include="src\AIMarketVanillaPricePolicy.cs" /><Compile Include="src\BuildingRepairHudRuntime.cs" /><Compile Include="src\ChurchPriestCountRuntime.cs" /><Compile Include="src\EnemyProximityPolicy.cs" /><Compile Include="src\ExtraFeaturesChoreSender.cs" /><Compile Include="src\ExtraFeaturesHookInfrastructure.cs" />
+    <Compile Include="..\Shared\Runtime\Diagnostics\CrashBreadcrumbDiagnostics.cs"><Link>Shared\Runtime\Diagnostics\CrashBreadcrumbDiagnostics.cs</Link></Compile><Compile Include="..\Shared\Runtime\Diagnostics\CrashBreadcrumbRecorder.cs"><Link>Shared\Runtime\Diagnostics\CrashBreadcrumbRecorder.cs</Link></Compile><Compile Include="..\Shared\Runtime\Diagnostics\DebugLogHelper.cs"><Link>Shared\Runtime\Diagnostics\DebugLogHelper.cs</Link></Compile><Compile Include="..\Shared\Runtime\Threading\UnityMainThreadDispatch.cs"><Link>Shared\Runtime\Threading\UnityMainThreadDispatch.cs</Link></Compile><Compile Include="..\Shared\Runtime\Native\NativePatternResolver.cs"><Link>Shared\Runtime\Native\NativePatternResolver.cs</Link></Compile><Compile Include="..\Shared\Runtime\Gameplay\SelectedChimpsSnapshotPolicy.cs"><Link>Shared\Runtime\Gameplay\SelectedChimpsSnapshotPolicy.cs</Link></Compile><Compile Include="..\Shared\Runtime\Localization\SerpLocalization.cs"><Link>Shared\Runtime\Localization\SerpLocalization.cs</Link></Compile>
+    <Compile Include="src\AIDefenseRepairRuntime.cs" /><Compile Include="src\AIMarketVanillaPriceHook.cs" /><Compile Include="src\BuildingRepairHudRuntime.cs" /><Compile Include="src\ChurchPriestCountRuntime.cs" /><Compile Include="src\EnemyProximityPolicy.cs" /><Compile Include="src\ExtraFeaturesHookInfrastructure.cs" />
     <Compile Include="src\ElevatedMoatDrawbridgeHooks.cs" /><Compile Include="src\ElevatedMoatHealthReporting.cs" /><Compile Include="src\ElevatedMoatNativeContract.cs" /><Compile Include="src\ElevatedMoatPatch.cs" /><Compile Include="src\ElevatedMoatRuntime.cs" /><Compile Include="src\ExtraFeaturesPlugin.cs" /><Compile Include="src\ExtraFeaturesRuntime.cs" /><Compile Include="src\ExtraFeaturesRuntime.GoodsMultipliers.cs" /><Compile Include="src\ExtraFeaturesRuntime.MarketPriceMultipliers.cs" /><Compile Include="src\ExtraFeaturesRuntime.MarketTradeTracking.cs" /><Compile Include="src\ExtraFeaturesRuntime.RefundSettings.cs" /><Compile Include="src\ExtraFeaturesRuntime.RestoreStorageGoods.cs" /><Compile Include="src\ExtraFeaturesViewModel.cs" />
     <Compile Include="src\FearFactorNativeDefinition.cs" /><Compile Include="src\FearFactorNeutralizationPolicy.cs" /><Compile Include="src\FearFactorNeutralizationRuntime.cs" />
     <Compile Include="src\NoKillRewardHook.cs" /><Compile Include="src\NoKillRewardStubContract.cs" />
@@ -58,16 +58,19 @@
     <Compile Include="src\KeepBuildRangeRuntime.cs" />
   </ItemGroup>
   <ItemGroup>
-    <Compile Include="..\Shared\GameModeHelper.cs"><Link>Shared\GameModeHelper.cs</Link></Compile>
-    <Compile Include="..\Shared\GameBuildingFootprint.cs"><Link>Shared\GameBuildingFootprint.cs</Link></Compile>
-    <Compile Include="..\Shared\GameProjectileSlotPolicy.cs"><Link>Shared\GameProjectileSlotPolicy.cs</Link></Compile>
-    <Compile Include="..\Shared\GameplaySessionLifecycle.cs"><Link>Shared\GameplaySessionLifecycle.cs</Link></Compile>
-    <Compile Include="..\Shared\GatehouseQueryUnitIdPolicy.cs"><Link>Shared\GatehouseQueryUnitIdPolicy.cs</Link></Compile>
-    <Compile Include="..\Shared\GameplayModActivationGate.cs"><Link>Shared\GameplayModActivationGate.cs</Link></Compile>
-    <Compile Include="..\Shared\ToolTipPresentation.cs"><Link>Shared\ToolTipPresentation.cs</Link></Compile>
-    <Compile Include="..\Shared\NumericTextInput.cs"><Link>Shared\NumericTextInput.cs</Link></Compile>
-    <Compile Include="..\Shared\TroopActionButtonLayout.cs"><Link>Shared\TroopActionButtonLayout.cs</Link></Compile>
-    <Compile Include="..\Shared\TroopActionButtonLayoutPolicy.cs"><Link>Shared\TroopActionButtonLayoutPolicy.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\PlayerIdentityHelper.cs"><Link>Shared\Adapters\APIShared\PlayerIdentityHelper.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Native\GameBuildingFootprint.cs"><Link>Shared\Runtime\Native\GameBuildingFootprint.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Gameplay\GameProjectileSlotPolicy.cs"><Link>Shared\Runtime\Gameplay\GameProjectileSlotPolicy.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\MissionEventsAdapter.cs"><Link>Shared\Adapters\APIShared\MissionEventsAdapter.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\Gameplay\GatehouseQueryUnitIdPolicy.cs"><Link>Shared\Runtime\Gameplay\GatehouseQueryUnitIdPolicy.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\DirectLaunchSettingsNotice.cs"><Link>Shared\Adapters\APIShared\DirectLaunchSettingsNotice.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\SerpsModProfiles.cs"><Link>Shared\Adapters\APIShared\SerpsModProfiles.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\GameplayFeatureModePolicy.cs"><Link>Shared\Adapters\APIShared\GameplayFeatureModePolicy.cs</Link></Compile>
+    <Compile Include="..\Shared\Adapters\APIShared\GameplayModActivationGate.cs"><Link>Shared\Adapters\APIShared\GameplayModActivationGate.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\UI\ToolTipPresentation.cs"><Link>Shared\Runtime\UI\ToolTipPresentation.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\UI\NumericTextInput.cs"><Link>Shared\Runtime\UI\NumericTextInput.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\UI\TroopActionButtonLayout.cs"><Link>Shared\Runtime\UI\TroopActionButtonLayout.cs</Link></Compile>
+    <Compile Include="..\Shared\Runtime\UI\TroopActionButtonLayoutPolicy.cs"><Link>Shared\Runtime\UI\TroopActionButtonLayoutPolicy.cs</Link></Compile>
   </ItemGroup>
   <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
   <Target Name="ValidateReferences" BeforeTargets="BeforeBuild"><Error Condition="!Exists('$(ExtenderDir)\SHCDESE.dll')" Text="SHCDESE.dll was not found: $(ExtenderDir)" /><Error Condition="!Exists('$(ApiSharedDir)\APIShared.dll')" Text="APIShared.dll was not found: $(ApiSharedDir)" /><Error Condition="!Exists('$(ExtenderDir)\RedBird.Abstractions.dll')" Text="RedBird.Abstractions.dll was not found: $(ExtenderDir)" /><Error Condition="!Exists('$(ExtenderDir)\RedBird.Backends.NativeX64.dll')" Text="RedBird.Backends.NativeX64.dll was not found: $(ExtenderDir)" /><Error Condition="!Exists('$(ExtenderDir)\RedBird.Core.dll')" Text="RedBird.Core.dll was not found: $(ExtenderDir)" /><Error Condition="!Exists('$(ExtenderDir)\RedBird.X64.dll')" Text="RedBird.X64.dll was not found: $(ExtenderDir)" /></Target>

diff --git a/ExtraFeatures/info.json b/ExtraFeatures/info.json
index 03c0cb6b0..5b50708c4 100644
--- a/ExtraFeatures/info.json
+++ b/ExtraFeatures/info.json
@@ -3,13 +3,49 @@
   "Author": "Serpens66",
   "Name": "Extra Features",
   "Description": "Optional gameplay additions and economy customization for Stronghold Crusader Definitive Edition.",
-  "Version": "1.0.108",
+  "Version": "1.0.113",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
-  "MinimumScriptExtenderVersion": "2.13.0",
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
+      "Version": "1.0.113",
+      "Changes": [
+        "Use APIShared 0.7.0 shared multiplayer transport while preserving existing packet schemas and gameplay execution."
+      ]
+    },
+    {
+      "Version": "1.0.112",
+      "Changes": [
+        "Use APIShared 0.6.0 coordinated hooks and event contracts to improve compatibility with other mods; preserve existing gameplay behavior."
+      ]
+    },
+    {
+      "Version": "1.0.111",
+      "Changes": [
+        "Migrate to APIShared 0.5.0 shared services and updated mod-owned adapters while preserving existing features."
+      ]
+    },
+    {
+      "Version": "1.0.110",
+      "Changes": [
+        "Updated Script Extender 2.14.0 unit field contracts; preserved death-marker and full control-word semantics."
+      ]
+    },
+    {
+      "Version": "1.0.109",
+      "Changes": [
+        "Move routine diagnostics to Debug; preserve warnings, errors and explicit file-operation results."
+      ]
+    },
     {
       "Version": "1.0.108",
       "Changes": [

diff --git a/ExtraFeatures/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml b/ExtraFeatures/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml
index 305a2c7e5..bd694441e 100644
--- a/ExtraFeatures/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml
+++ b/ExtraFeatures/Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml
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
-    <Style x:Key="ModSettingsSearchTargetGrid" TargetType="{x:Type Grid}">
+    <Style x:Key="ModSettingsSearchTargetGrid" TargetType="{x:Type Grid}"><Setter Property="shared:ModSettingsMode.Key" Value="{Binding RelativeSource={RelativeSource Self}, Path=(shared:ModSettingsSearch.Key)}"/>
       <Setter Property="Visibility">
         <Setter.Value>
           <MultiBinding Converter="{StaticResource ModSettingsSearchVisibilityConverter}">
@@ -157,7 +158,7 @@
         <TextBlock Text="{Binding System_ModSettingsSearchNoResultsText}" Visibility="{Binding System_ModSettingsSearchNoResultsVisibility}" Foreground="#FFCC66" HorizontalAlignment="Left" Margin="0,4,0,0"/>
       </StackPanel>
       <TextBlock Text="{Binding ActionsScopeNoticeText}" Visibility="{Binding ActionsScopeNoticeVisibility}" Foreground="#BBBBBB" TextWrapping="Wrap" Margin="0,0,0,8"/>
-      <TextBlock Text="{Binding System_DirectLaunchNoticeText}" Visibility="{Binding System_DirectLaunchNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
+      <TextBlock Text="{Binding System_ConsumerModeNoticeText}" Visibility="{Binding System_ConsumerModeNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding System_TrailSourceNoticeText}" Visibility="{Binding System_TrailSourceNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostReadOnlyNoticeText}" Visibility="{Binding HostReadOnlyNoticeVisibility}" Foreground="#FFCC66" FontWeight="Bold" Margin="0,0,0,8"/>
       <TextBlock Text="{Binding HostOptionsText}" Style="{StaticResource HostRoleHeader}" Margin="0,2,0,6"/>

diff --git a/ExtraFeatures/src/AIDefenseRepairRuntime.cs b/ExtraFeatures/src/AIDefenseRepairRuntime.cs
index 9855e0041..7d56bd64c 100644
--- a/ExtraFeatures/src/AIDefenseRepairRuntime.cs
+++ b/ExtraFeatures/src/AIDefenseRepairRuntime.cs
@@ -1,3 +1,4 @@
+using APIShared.GameModes;
 // Feature: Configure human/AI enemy proximity and destruction-anchored AIV defense rebuild timing.
 //
 // Finished castles repeatedly enter ExecuteBuildStep at RVA 0x51790. The placement helper at
@@ -302,7 +303,7 @@ namespace ExtraFeatures
                     liveDefenseTargets[target] = new LiveDefenseIdentity(spanIndex + 1, building.r_GlobalId);
                     count++;
                 }
-                Shared.DebugLogHelper.LogInfo(log,
+                Shared.DebugLogHelper.LogDebug(log,
                     $"AI defense rebuild save-load inventory: intactTargets={count}, tick={SafeCurrentTick()}.");
             }
             catch (Exception ex)
@@ -325,9 +326,9 @@ namespace ExtraFeatures
         {
             try
             {
-                Shared.GameModeSnapshot snapshot = Shared.GameplayModActivationGate.Snapshot;
+                APIShared.GameModes.GameModeSnapshot snapshot = Shared.GameplayModActivationGate.Snapshot;
                 realMultiplayer = snapshot.IsRealMultiplayer;
-                gameModeKnown = snapshot.Kind != Shared.GameModeKind.Unknown;
+                gameModeKnown = snapshot.Kind != APIShared.GameModes.GameModeKind.Unknown;
                 gameModeFailureLogged = false;
                 Shared.DebugLogHelper.LogDebug(
                     log,
@@ -433,7 +434,7 @@ namespace ExtraFeatures
             if (destroyedDefenseTicks.ContainsKey(target))
                 return;
             destroyedDefenseTicks[target] = tick;
-            Shared.DebugLogHelper.LogInfo(log,
+            Shared.DebugLogHelper.LogDebug(log,
                 $"AI defense rebuild destruction: target={target}, buildingId={identity.BuildingId}, " +
                 $"globalId={identity.GlobalId}, tick={tick}, source={source}.");
         }
@@ -839,7 +840,7 @@ namespace ExtraFeatures
                             if (elapsed >= 0 && elapsed < (long)settings.AITowerGateRebuildDelaySeconds * TicksPerSecond)
                                 Shared.DebugLogHelper.LogError(log, message);
                             else
-                                Shared.DebugLogHelper.LogInfo(log, message);
+                                Shared.DebugLogHelper.LogDebug(log, message);
                         }
                         liveDefenseTargets[target] = new LiveDefenseIdentity(buildingId, building->r_GlobalId);
                         damagedDefenseWatches.Remove(target);
@@ -1001,7 +1002,7 @@ namespace ExtraFeatures
 
                 state = new RebuildDelayState(firstTick, source);
                 rebuildDelays.Add(target, state);
-                Shared.DebugLogHelper.LogInfo(log,
+                Shared.DebugLogHelper.LogDebug(log,
                     $"AI defense rebuild timer started: target={target}, frameIndex={context.FrameIndex}, " +
                     $"tick={context.Tick}, startTick={firstTick}, delaySeconds={delaySeconds}, source={source}.");
             }
@@ -1013,7 +1014,7 @@ namespace ExtraFeatures
                 if (!state.ReleaseLogged)
                 {
                     state.ReleaseLogged = true;
-                    Shared.DebugLogHelper.LogInfo(log,
+                    Shared.DebugLogHelper.LogDebug(log,
                         $"AI defense rebuild timer released: target={target}, frameIndex={context.FrameIndex}, " +
                         $"tick={context.Tick}, startTick={state.FirstDetectedTick}, elapsedTicks={elapsed}, " +
                         $"source={state.Source}.");
@@ -1025,7 +1026,7 @@ namespace ExtraFeatures
             if (!state.BlockLogged)
             {
                 state.BlockLogged = true;
-                Shared.DebugLogHelper.LogInfo(log,
+                Shared.DebugLogHelper.LogDebug(log,
                     $"AI defense rebuild timer blocked: target={target}, frameIndex={context.FrameIndex}, " +
                     $"tick={context.Tick}, startTick={state.FirstDetectedTick}, elapsedTicks={elapsed}, " +
                     $"requiredTicks={requiredTicks}.");

diff --git a/ExtraFeatures/src/AIMarketVanillaPriceHook.cs b/ExtraFeatures/src/AIMarketVanillaPriceHook.cs
index 2e0c621c6..31ec6ffa0 100644
--- a/ExtraFeatures/src/AIMarketVanillaPriceHook.cs
+++ b/ExtraFeatures/src/AIMarketVanillaPriceHook.cs
@@ -1,349 +1,33 @@
-// Feature: Keep AI market decisions and transactions on Vanilla prices when configured.
+// Feature: Consumer-specific AI policy on APIShared's permanent native market query events.
+using APIShared.Economy;
 using BepInEx.Logging;
-using Iced.Intel;
-using RedBird.Abstractions.Hooks;
-using RedBird.Abstractions.Hooks.Transaction;
-using RedBird.Core.Memory;
-using RedBird.X64.Hooks.Transaction;
 using SHCDESE.API;
 using SHCDESE.Interop;
 using System;
-using System.Runtime.InteropServices;
-using System.Threading;
 
 namespace ExtraFeatures
 {
     internal sealed class AIMarketVanillaPriceHook : IDisposable
     {
-        private const int PolyHookMinimumJumpSize = 6;
-        private const int ValidatedOverwriteLength = 10;
-        private const ulong BuyPriceTableDisplacement = 0x1817B8;
-        private const ulong SellPriceTableDisplacement = 0x1817BC;
-
-        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
-        private delegate int MarketPriceDelegate(IntPtr playerManager, int playerId, int good, int amount);
-
-        private readonly ManualLogSource log;
         private volatile bool useVanillaAIPricesForSession;
-        private HookTransaction transaction;
-        private readonly DetourHandle<MarketPriceDelegate> buyPriceHook =
-            new DetourHandle<MarketPriceDelegate>();
-        private readonly DetourHandle<MarketPriceDelegate> sellPriceHook =
-            new DetourHandle<MarketPriceDelegate>();
-        private int buyCallbackFailureLogged;
-        private int sellCallbackFailureLogged;
-        private bool disposed;
-
-        public AIMarketVanillaPriceHook(
-            ManualLogSource log,
-            IntPtr libraryHandle,
-            ScanRegion region,
-            ReadOnlySpan<byte> memory,
-            bool referenceHashMatches)
-        {
-            this.log = log ?? throw new ArgumentNullException(nameof(log));
-            if (libraryHandle == IntPtr.Zero || memory.Length == 0)
-                throw new ArgumentException("The Crusader native library is unavailable.");
-
-            Shared.NativeResolution buyResolution = Shared.NativePatternResolver.ResolveUnique(
-                memory,
-                AIMarketVanillaPricePolicy.BuyPriceFunctionPattern,
-                AIMarketVanillaPricePolicy.BuyPriceFunctionRva,
-                referenceHashMatches,
-                "AI market buy-price helper",
-                log);
-            Shared.NativeResolution sellResolution = Shared.NativePatternResolver.ResolveUnique(
-                memory,
-                AIMarketVanillaPricePolicy.SellPriceFunctionPattern,
-                AIMarketVanillaPricePolicy.SellPriceFunctionRva,
-                referenceHashMatches,
-                "AI market sell-price helper",
-                log);
-
-            ulong imageBase = unchecked((ulong)libraryHandle.ToInt64());
-            ValidateHookPair(memory, imageBase, buyResolution.Rva, sellResolution.Rva);
-            Shared.DebugLogHelper.LogInfo(
-                log,
-                $"Extra Features AI market hook spans validated before installation: " +
-                $"buy=0x{buyResolution.Rva:X}-0x{buyResolution.Rva + ValidatedOverwriteLength:X} (3+7 bytes), " +
-                $"sell=0x{sellResolution.Rva:X}-0x{sellResolution.Rva + ValidatedOverwriteLength:X} (3+7 bytes), " +
-                $"nextInstructionLength=5, minimumDetourBytes={PolyHookMinimumJumpSize}, " +
-                "ripRelative=false, incomingInteriorTargets=0, overlap=false.");
-
-            HookTransaction pendingTransaction = null;
-            try
-            {
-                pendingTransaction = ExtraFeaturesHookInfrastructure.CreateOwnedTransaction(region);
-                pendingTransaction.AddDetour(
-                    buyPriceHook,
-                    HookTarget.FromAddress(imageBase + unchecked((ulong)buyResolution.Rva)),
-                    GetBuyPrice);
-                pendingTransaction.AddDetour(
-                    sellPriceHook,
-                    HookTarget.FromAddress(imageBase + unchecked((ulong)sellResolution.Rva)),
-                    GetSellPrice);
-                CommitResult commitResult = pendingTransaction.Commit();
-
-                if (!commitResult.IsCompleteSuccess || !buyPriceHook.Success || !sellPriceHook.Success)
-                    throw new InvalidOperationException("The AI market-price hook transaction did not install both detours.");
-
-                transaction = pendingTransaction;
-                pendingTransaction = null;
-            }
-            catch
-            {
-                if (pendingTransaction != null)
-                {
-                    try
-                    {
-                        pendingTransaction.Dispose();
-                    }
-                    catch
-                    {
-                    }
-                }
-                throw;
-            }
-
-            Shared.DebugLogHelper.LogInfo(
-                log,
-                $"Extra Features AI Vanilla market-price hooks installed atomically: " +
-                $"buyMethod={buyResolution.Method}, buyRva=0x{buyResolution.Rva:X}, buySpan=0x{buyResolution.Rva:X}-0x{buyResolution.Rva + ValidatedOverwriteLength:X}; " +
-                $"sellMethod={sellResolution.Method}, sellRva=0x{sellResolution.Rva:X}, sellSpan=0x{sellResolution.Rva:X}-0x{sellResolution.Rva + ValidatedOverwriteLength:X}; " +
-                $"minimumDetourBytes={PolyHookMinimumJumpSize}.");
-        }
-
-        public void Dispose()
+        private volatile bool disposed;
+        public AIMarketVanillaPriceHook(ManualLogSource log)
         {
-            if (disposed)
-                return;
-
-            disposed = true;
-        }
-
-        private int GetBuyPrice(IntPtr playerManager, int playerId, int good, int amount)
-        {
-            using (Shared.CrashBreadcrumbScope diagnostic =
-                Shared.CrashBreadcrumbDiagnostics.Enter(
-                    "AiMarketBuyPrice",
-                    playerId,
-                    good,
-                    amount,
-                    playerManager.ToInt64()))
-            {
-            try
-            {
-                if (ShouldUseVanillaPrice(playerManager, playerId, good))
-                {
-                    PackedGoodPrice price = GamePlayerManagerAPI.Instance.GetDefaultTradeBasePrice((eGoods)good);
-                    int result = AIMarketVanillaPricePolicy.CalculateTradeTotal(price.BuyPrice, amount);
-                    diagnostic.Complete(1);
-                    return result;
-                }
-            }
-            catch (Exception ex)
-            {
-                TryLogCallbackFailureOnce(ref buyCallbackFailureLogged, "buy", ex);
-            }
-
-            int vanillaResult = buyPriceHook.Original(playerManager, playerId, good, amount);
-            diagnostic.Complete(0);
-            return vanillaResult;
-            }
-        }
-
-        private int GetSellPrice(IntPtr playerManager, int playerId, int good, int amount)
-        {
-            using (Shared.CrashBreadcrumbScope diagnostic =
-                Shared.CrashBreadcrumbDiagnostics.Enter(
-                    "AiMarketSellPrice",
-                    playerId,
-                    good,
-                    amount,
-                    playerManager.ToInt64()))
-            {
-            try
-            {
-                if (ShouldUseVanillaPrice(playerManager, playerId, good))
-                {
-                    PackedGoodPrice price = GamePlayerManagerAPI.Instance.GetDefaultTradeBasePrice((eGoods)good);
-                    int result = AIMarketVanillaPricePolicy.CalculateTradeTotal(price.SellPrice, amount);
-                    diagnostic.Complete(1);
-                    return result;
-                }
-            }
-            catch (Exception ex)
-            {
-                TryLogCallbackFailureOnce(ref sellCallbackFailureLogged, "sell", ex);
-            }
-
-            int vanillaResult = sellPriceHook.Original(playerManager, playerId, good, amount);
-            diagnostic.Complete(0);
-            return vanillaResult;
-            }
+            if (!MarketPriceEvents.TryRegister(ExtraFeaturesPlugin.PluginGuid, "ai-default-market-prices", OnPrice, null, out string reason))
+                throw new InvalidOperationException(reason);
+            log?.LogInfo("Extra Features AI market policy registered with APIShared; native helpers have one process owner.");
         }
-
-        internal void SetSessionOverride(bool enabled)
+        public void Dispose() { disposed = true; }
+        internal void SetSessionOverride(bool enabled) { useVanillaAIPricesForSession = enabled; }
+        private void OnPrice(MarketPricePreEventArgs args)
         {
-            useVanillaAIPricesForSession = enabled;
-        }
-
-        private bool ShouldUseVanillaPrice(IntPtr playerManager, int playerId, int good)
-        {
-            if (!useVanillaAIPricesForSession || playerManager == IntPtr.Zero ||
-                playerId < 1 || playerId > 8 || good < 0 || good >= (int)eGoods.Count)
-                return false;
-
-            // Settings are fixed for the session; player classification is live game state.
-            return GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
-        }
-
-        private void TryLogCallbackFailureOnce(ref int alreadyLogged, string direction, Exception failure)
-        {
-            Shared.CrashBreadcrumbDiagnostics.Record(
-                "AiMarketCallbackFailure",
-                direction == "buy" ? 1 : 2,
-                outcome: -1);
-            if (Interlocked.Exchange(ref alreadyLogged, 1) != 0)
-                return;
-
-            try
-            {
-                Shared.DebugLogHelper.LogError(
-                    log,
-                    $"Extra Features AI Vanilla market-price {direction} callback failed; " +
-                    $"this call uses the active global price through Vanilla: {failure}");
-            }
-            catch
-            {
-                // The trampoline below remains the independent safe fallback.
-            }
-        }
-
-        private static void ValidateHookPair(
-            ReadOnlySpan<byte> memory,
-            ulong imageBase,
-            int buyRva,
-            int sellRva)
-        {
-            ValidateFunction(memory, imageBase, buyRva, BuyPriceTableDisplacement, "buy");
-            ValidateFunction(memory, imageBase, sellRva, SellPriceTableDisplacement, "sell");
-
-            int buyEnd = checked(buyRva + ValidatedOverwriteLength);
-            int sellEnd = checked(sellRva + ValidatedOverwriteLength);
-            if (buyRva < sellEnd && sellRva < buyEnd)
-                throw new InvalidOperationException("The AI market-price hook spans overlap.");
-
-            ValidateNoIncomingDirectBranchTargets(memory, buyRva, buyEnd, "buy");
-            ValidateNoIncomingDirectBranchTargets(memory, sellRva, sellEnd, "sell");
-        }
-
-        private static void ValidateFunction(
-            ReadOnlySpan<byte> memory,
-            ulong imageBase,
-            int rva,
-            ulong expectedPriceTableDisplacement,
-            string direction)
-        {
-            if (rva < 0 || rva > memory.Length - 15)
-                throw new InvalidOperationException($"The AI market {direction} helper is outside the loaded image.");
-
-            byte[] bytes = memory.Slice(rva, Math.Min(64, memory.Length - rva)).ToArray();
-            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
-            decoder.IP = imageBase + unchecked((ulong)rva);
-
-            decoder.Decode(out Instruction first);
-            decoder.Decode(out Instruction second);
-            int overwriteLength = checked(first.Length + second.Length);
-            if (first.Code == Code.INVALID || second.Code == Code.INVALID ||
-                first.Length != 3 || second.Length != 7 ||
-                overwriteLength != ValidatedOverwriteLength ||
-                overwriteLength < PolyHookMinimumJumpSize)
-            {
-                throw new InvalidOperationException(
-                    $"The AI market {direction} helper has an unsafe detour span: " +
-                    $"lengths={first.Length}+{second.Length}, required={PolyHookMinimumJumpSize}.");
-            }
-
-            if (first.Mnemonic != Mnemonic.Movsxd ||
-                first.Op0Kind != OpKind.Register || first.Op0Register != Register.RAX ||
-                first.Op1Kind != OpKind.Register || first.Op1Register != Register.R8D ||
-                second.Mnemonic != Mnemonic.Mov ||
-                second.Op0Kind != OpKind.Register || second.Op0Register != Register.ECX ||
-                second.Op1Kind != OpKind.Memory ||
-                second.MemoryBase != Register.RCX || second.MemoryIndex != Register.RAX ||
-                second.MemoryIndexScale != 8 ||
-                second.MemoryDisplacement64 != expectedPriceTableDisplacement)
-            {
-                throw new InvalidOperationException(
-                    $"The AI market {direction} helper no longer matches the audited ABI/price-table semantics.");
-            }
-
-            if (first.IsIPRelativeMemoryOperand || second.IsIPRelativeMemoryOperand ||
-                first.FlowControl != FlowControl.Next || second.FlowControl != FlowControl.Next)
-            {
-                throw new InvalidOperationException(
-                    $"The AI market {direction} detour span contains a relative operand or control-flow instruction.");
-            }
-
-            decoder.Decode(out Instruction following);
-            if (following.Code == Code.INVALID ||
-                following.IP != imageBase + unchecked((ulong)(rva + ValidatedOverwriteLength)) ||
-                following.Length != 5 || following.Mnemonic != Mnemonic.Mov ||
-                following.Op0Kind != OpKind.Register || following.Op0Register != Register.EAX ||
-                following.Op1Kind != OpKind.Immediate32 || following.Immediate32 != 0x66666667U)
-            {
-                throw new InvalidOperationException(
-                    $"The instruction following the AI market {direction} detour span changed.");
-            }
-        }
-
-        private static void ValidateNoIncomingDirectBranchTargets(
-            ReadOnlySpan<byte> memory,
-            int hookStart,
-            int hookEnd,
-            string direction)
-        {
-            foreach (Shared.NativeCodeRange range in Shared.NativePatternResolver.GetExecutableCodeRanges(memory))
-            {
-                int end = checked(range.Offset + range.Length);
-                for (int source = range.Offset; source < end; source++)
-                {
-                    int instructionLength;
-                    int displacement;
-                    byte opcode = memory[source];
-                    if ((opcode == 0xE8 || opcode == 0xE9) && source <= end - 5)
-                    {
-                        instructionLength = 5;
-                        displacement = Shared.NativePatternResolver.ReadInt32(memory, source + 1);
-                    }
-                    else if ((opcode == 0xEB || (opcode >= 0x70 && opcode <= 0x7F) ||
-                        (opcode >= 0xE0 && opcode <= 0xE3)) && source <= end - 2)
-                    {
-                        instructionLength = 2;
-                        displacement = unchecked((sbyte)memory[source + 1]);
-                    }
-                    else if (opcode == 0x0F && source <= end - 6 &&
-                        memory[source + 1] >= 0x80 && memory[source + 1] <= 0x8F)
-                    {
-                        instructionLength = 6;
-                        displacement = Shared.NativePatternResolver.ReadInt32(memory, source + 2);
-                    }
-                    else
-                    {
-                        continue;
-                    }
-
-                    long target = (long)source + instructionLength + displacement;
-                    bool sourceInsideSpan = source >= hookStart && source < hookEnd;
-                    if (!sourceInsideSpan && target > hookStart && target < hookEnd)
-                    {
-                        throw new InvalidOperationException(
-                            $"A direct branch at RVA 0x{source:X} targets the interior of the AI market " +
-                            $"{direction} detour span at RVA 0x{target:X}.");
-                    }
-                }
-            }
+            if (disposed || args.SkipOriginalFunction || !useVanillaAIPricesForSession || !args.HasNativeManager ||
+                args.PlayerId < 1 || args.PlayerId > 8 || args.Good < 0 || args.Good >= (int)eGoods.Count ||
+                !GamePlayerManagerAPI.Instance.IsAIPlayer(args.PlayerId)) return;
+            PackedGoodPrice price = GamePlayerManagerAPI.Instance.GetDefaultTradeBasePrice((eGoods)args.Good);
+            args.ReplacementTotal = MarketPriceEvents.CalculateTradeTotal(
+                args.Direction == MarketPriceDirection.Buy ? price.BuyPrice : price.SellPrice, args.Amount);
+            args.SkipOriginalFunction = true;
         }
     }
-}
+}
\ No newline at end of file

diff --git a/ExtraFeatures/src/BuildingRepairHudRuntime.cs b/ExtraFeatures/src/BuildingRepairHudRuntime.cs
index 4ecebaa61..3f757bcd6 100644
--- a/ExtraFeatures/src/BuildingRepairHudRuntime.cs
+++ b/ExtraFeatures/src/BuildingRepairHudRuntime.cs
@@ -17,14 +17,14 @@ namespace ExtraFeatures
         private const string HoverHostName = "ExtraFeaturesBuildingRepairHoverHost";
         private const string HoverId = "ExtraFeatures.BuildingRepair.SmallButton";
         private delegate bool ShowRepairDelegate(HUD_Buildings self, int type, int panel);
-        private delegate void HudUpdateDelegate(FatControler self);
+
 
         private readonly ManualLogSource log;
         private readonly IBuildingRepairCapability repair;
         private readonly Hook classifierHook;
-        private readonly Hook hudHook;
+
         private readonly ShowRepairDelegate originalShowRepair;
-        private readonly HudUpdateDelegate originalHudUpdate;
+
         private Grid lastHoverHost;
         private HUD_Buildings lastHudPanel;
         private Button lastRepairButton;
@@ -38,33 +38,51 @@ namespace ExtraFeatures
             this.log = log ?? throw new ArgumentNullException(nameof(log));
             this.repair = repair ?? throw new ArgumentNullException(nameof(repair));
             Hook candidateClassifier = null;
-            Hook candidateHud = null;
+
             try
             {
                 candidateClassifier = new Hook(FindMethod(typeof(HUD_Buildings), "GetBuildingShowRepair",
                     BindingFlags.Public | BindingFlags.Instance, typeof(int), typeof(int)),
                     (ShowRepairDelegate)ShowRepairHook);
                 originalShowRepair = candidateClassifier.GenerateTrampoline<ShowRepairDelegate>();
-                candidateHud = new Hook(FindMethod(typeof(FatControler), "NoesisGUIUpdateChecksInGame",
-                    BindingFlags.Public | BindingFlags.Instance),
-                    (HudUpdateDelegate)HudUpdateHook);
-                originalHudUpdate = candidateHud.GenerateTrampoline<HudUpdateDelegate>();
                 classifierHook = candidateClassifier;
-                hudHook = candidateHud;
+
             }
             catch
             {
                 // Only an unpublished initialization candidate can be rolled back.
-                try { candidateHud?.Undo(); } catch { }
-                try { candidateHud?.Dispose(); } catch { }
+
+
                 try { candidateClassifier?.Undo(); } catch { }
                 try { candidateClassifier?.Dispose(); } catch { }
                 throw;
             }
         }
 
+        private void RegisterHudEvents()
+        {
+            if (!APIShared.Presentation.PresentationEvents.TryRegister(
+                APIShared.Presentation.PresentationOperation.GuiChecks, ExtraFeaturesPlugin.PluginGuid,
+                "BuildingRepairHud", null, args => {
+                    if (args.OriginalCompleted) HudUpdateHook(args.Controller);
+                }, out string reason)) throw new InvalidOperationException(reason);
+        }
         internal static BuildingRepairHudRuntime Install(ManualLogSource log, IBuildingRepairCapability repair) =>
-            new BuildingRepairHudRuntime(log, repair);
+            Create(log, repair);
+
+        private static BuildingRepairHudRuntime Create(ManualLogSource log, IBuildingRepairCapability repair)
+        {
+            var runtime = new BuildingRepairHudRuntime(log, repair);
+            try { runtime.RegisterHudEvents(); }
+            catch
+            {
+                // The classifier belongs to this failed, unpublished candidate.
+                try { runtime.classifierHook.Undo(); } catch { }
+                try { runtime.classifierHook.Dispose(); } catch { }
+                throw;
+            }
+            return runtime;
+        }
 
         internal void SetActive(bool enabled)
         {
@@ -95,7 +113,7 @@ namespace ExtraFeatures
 
         private void HudUpdateHook(FatControler self)
         {
-            originalHudUpdate(self);
+
             try { UpdateButton(); }
             catch (Exception ex)
             {

diff --git a/ExtraFeatures/src/ChurchPriestCountRuntime.cs b/ExtraFeatures/src/ChurchPriestCountRuntime.cs
index 07d385783..0f5b85f4b 100644
--- a/ExtraFeatures/src/ChurchPriestCountRuntime.cs
+++ b/ExtraFeatures/src/ChurchPriestCountRuntime.cs
@@ -212,7 +212,7 @@ namespace ExtraFeatures
 
         private void LogInfo(string message)
         {
-            log.LogInfo($"[{TimestampNow()}] Extra Features {message}");
+            log.LogDebug($"[{TimestampNow()}] Extra Features {message}");
         }
 
         private void LogError(string message)

diff --git a/ExtraFeatures/src/ElevatedMoatPatch.cs b/ExtraFeatures/src/ElevatedMoatPatch.cs
index 3589b00f2..8d240c1e6 100644
--- a/ExtraFeatures/src/ElevatedMoatPatch.cs
+++ b/ExtraFeatures/src/ElevatedMoatPatch.cs
@@ -540,7 +540,7 @@ namespace ExtraFeatures
                     ElevatedMoatNativeContract.AreaRemovalHeightLength,
                     "area moat-removal height");
 
-                Shared.DebugLogHelper.LogInfo(
+                Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Extra Features elevated-moat active; AI={allowAIPlacement}, human={allowHumanPlacement}, editor=true, " +
                     $"adaptiveDepth={ElevatedMoatNativeContract.MoatDepth}, " +
@@ -821,7 +821,7 @@ namespace ExtraFeatures
             if (!ElevatedMoatHealthReporting.ShouldReport(total))
                 return;
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Extra Features elevated-moat healthy: corrections={total}, " +
                 $"completed={Interlocked.Read(ref completedHeightCorrections)}, " +

diff --git a/ExtraFeatures/src/ExtraFeaturesPlugin.cs b/ExtraFeatures/src/ExtraFeaturesPlugin.cs
index b40c7b87c..5262f42ca 100644
--- a/ExtraFeatures/src/ExtraFeaturesPlugin.cs
+++ b/ExtraFeatures/src/ExtraFeaturesPlugin.cs
@@ -1,3 +1,4 @@
+using APIShared.ModSettings;
 // Feature: Plugin bootstrap for the Extra Features mod.
 using BepInEx;
 using BepInEx.Bootstrap;
@@ -7,13 +8,14 @@ using System;
 
 namespace ExtraFeatures
 {
-    [BepInDependency(ScriptExtenderGuid, "2.13.0")]
-    [BepInDependency(ApiSharedGuid, "0.4.6")]
+    [BepInDependency(ScriptExtenderGuid, "2.14.0")]
+    [BepInDependency(ApiSharedGuid, "0.7.0")]
     [BepInDependency(LegacySomeSettingsGuid, BepInDependency.DependencyFlags.SoftDependency)]
     [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
     [BepInDependency("FearFactorNeutralizationTest_Serp", BepInDependency.DependencyFlags.SoftDependency)]
     [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
     [BepInDependency("UnitLimit_Serp", BepInDependency.DependencyFlags.SoftDependency)]
+
     [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
     public sealed class ExtraFeaturesPlugin : BaseUnityPlugin
     {
@@ -23,7 +25,7 @@ namespace ExtraFeatures
 
         public const string PluginGuid = "ExtraFeatures_Serp";
         public const string PluginName = "Extra Features";
-        public const string PluginVersion = "1.0.108";
+        public const string PluginVersion = "1.0.113";
 
         private ExtraFeaturesRuntime runtime;
         private bool marketGoodPriceVisualRefreshFailureLogged;
@@ -43,7 +45,6 @@ namespace ExtraFeatures
                 PluginGuid,
                 PluginName,
                 PluginVersion);
-            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");
             bool legacySomeSettingsLoaded = Chainloader.PluginInfos.ContainsKey(LegacySomeSettingsGuid);
             if (legacySomeSettingsLoaded)
             {
@@ -90,7 +91,8 @@ namespace ExtraFeatures
 
             try
             {
-                Shared.LobbyModSettingsPresetRegistration.Register(
+                Shared.DirectLaunchSettingsNotice.Configure(Settings, PluginGuid);
+                APIShared.ModSettings.LobbyModSettingsPresetRegistration.Register(
                     this,
                     Logger,
                     PluginGuid,

diff --git a/ExtraFeatures/src/ExtraFeaturesRuntime.cs b/ExtraFeatures/src/ExtraFeaturesRuntime.cs
index 159cf9413..9faec7708 100644
--- a/ExtraFeatures/src/ExtraFeaturesRuntime.cs
+++ b/ExtraFeatures/src/ExtraFeaturesRuntime.cs
@@ -219,15 +219,8 @@ namespace ExtraFeatures
             try
             {
                 aiMarketVanillaPriceHook = new AIMarketVanillaPriceHook(
-                    log, libraryHandle, nativeRegion, GetNativeLibraryMemory(), fixedLayoutHashValidated);
+                    log);
                 CaptureAIMarketSessionSettings();
-                if (!fixedLayoutHashValidated)
-                {
-                    Shared.DebugLogHelper.LogWarning(
-                        log,
-                        "Extra Features AI Vanilla market prices are running on an unknown " +
-                        "CrusaderDE.dll because both native helper signatures and hook spans were validated.");
-                }
             }
             catch (Exception ex)
             {

diff --git a/ExtraFeatures/src/ExtraFeaturesViewModel.cs b/ExtraFeatures/src/ExtraFeaturesViewModel.cs
index 9fc011a5e..26dec5ff4 100644
--- a/ExtraFeatures/src/ExtraFeaturesViewModel.cs
+++ b/ExtraFeatures/src/ExtraFeaturesViewModel.cs
@@ -1,3 +1,4 @@
+using APIShared.ModSettings;
 // Feature: Lobby settings model for all Extra Features options.
 using APIShared;
 using BepInEx.Logging;
@@ -14,7 +15,7 @@ using System.Globalization;
 
 namespace ExtraFeatures
 {
-    public sealed class ExtraFeaturesViewModel : Shared.PresetLobbyModSettingsViewModel
+    public sealed class ExtraFeaturesViewModel : APIShared.ModSettings.PresetLobbyModSettingsViewModel
     {
         public event Action<string> SettingChanged;
 

diff --git a/ExtraFeatures/src/FearFactorNeutralizationRuntime.cs b/ExtraFeatures/src/FearFactorNeutralizationRuntime.cs
index 92a6da37c..f861cf787 100644
--- a/ExtraFeatures/src/FearFactorNeutralizationRuntime.cs
+++ b/ExtraFeatures/src/FearFactorNeutralizationRuntime.cs
@@ -90,7 +90,7 @@ namespace ExtraFeatures
                 if (probe.DisplacedByteCount != FearFactorNativeDefinition.UiHookLength)
                     throw new InvalidOperationException("Unexpected RedBird UI span before installation.");
             }
-            Shared.DebugLogHelper.LogInfo(log,
+            Shared.DebugLogHelper.LogDebug(log,
                 $"FEAR_FACTOR_VALIDATED: sha256={FearFactorNativeDefinition.ReferenceSha256}, " +
                 "uiStart=0x1A19F2, uiEnd=0x1A1A00, instructionLengths=7,7, next=TEST_EAX_EAX.");
 
@@ -151,7 +151,7 @@ namespace ExtraFeatures
                 throw;
             }
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"FEAR_FACTOR_NEUTRALIZATION_READY: damageRva=0x{damageResolution.Rva:X}, " +
                 $"damageDetourLength={FearFactorNativeDefinition.DamageDetourLength}, " +
@@ -192,7 +192,7 @@ namespace ExtraFeatures
                 TryAttachArmyReportViewModel();
                 RefreshArmyReportText();
             }
-            Shared.DebugLogHelper.LogInfo(log,
+            Shared.DebugLogHelper.LogDebug(log,
                 $"FEAR_FACTOR_SETTING: enabled={enabled}, mode={Shared.GameplayModActivationGate.Snapshot.Kind}.");
         }
 

diff --git a/ExtraFeatures/src/GatehouseAutomationRuntime.cs b/ExtraFeatures/src/GatehouseAutomationRuntime.cs
index 2711231be..708216b9b 100644
--- a/ExtraFeatures/src/GatehouseAutomationRuntime.cs
+++ b/ExtraFeatures/src/GatehouseAutomationRuntime.cs
@@ -1,3 +1,4 @@
+using APIShared.GameModes;
 // Feature: Reachability-aware and per-building manual gatehouse automation.
 using APIShared;
 using BepInEx.Logging;
@@ -526,13 +527,10 @@ namespace ExtraFeatures
                 AutomaticEnabled = automaticEnabled
             };
             short packetId = packetHook?.GetPacketId() ?? (short)0;
-            if (!ExtraFeaturesChoreSender.TrySend(
+            if (!APIShared.Networking.ChoreTransport.TrySend(
                     packet,
                     packetId,
                     networkInitialized && packetHook != null,
-                    value => GameNetworkAPI.Serialize(value),
-                    () => SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA,
-                    (value, id) => GameNetworkAPI.SendPacketToAllEx2(value, id, viaChore: true),
                     out byte[] body,
                     out string rejectionReason))
             {
@@ -546,6 +544,9 @@ namespace ExtraFeatures
 
         private void OnPacketReceived(ReceiveCustomPacketEventArgs<GatehouseAutomationPacket> args)
         {
+            if (!APIShared.Networking.ChoreTransport.IsChoreDelivery(args))
+                return;
+
             if (!Shared.GameplayModActivationGate.IsAllowed)
                 return;
 
@@ -613,7 +614,7 @@ namespace ExtraFeatures
                         eventUnitId,
                         units.Length,
                         out int unitId) ||
-                    !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
+                    !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                     unit == null)
                 {
                     return;
@@ -622,7 +623,7 @@ namespace ExtraFeatures
                 // The Script Extender replaces these Vanilla comparisons at the
                 // native hook. Re-evaluate them for the corrected candidate slot.
                 bool vanillaCandidateCanClose =
-                    unit->r_AliveState == AliveState.IsAlive &&
+                    APIShared.UnitAccess.IsReallyAlive(unit) &&
                     unit->r_UnitChimp != eChimps.CHIMP_TYPE_LION &&
                     unit->r_ControllableForPlayerId != 0;
                 // Preserve an intentional decision made by an earlier event
@@ -1011,9 +1012,8 @@ namespace ExtraFeatures
         }
 
         private bool IsChoreTransportReady() =>
-            ExtraFeaturesChoreSender.IsAvailable(
-                networkInitialized && packetHook != null,
-                () => SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA);
+            APIShared.Networking.ChoreTransport.IsAvailable(
+                networkInitialized && packetHook != null);
 
         private int NextOperationId()
         {
@@ -1024,7 +1024,7 @@ namespace ExtraFeatures
 
         private static int GetControlledPlayerId()
         {
-            if (Shared.GameModeHelper.IsMapEditor())
+            if (APIShared.GameModes.GameModeHelper.IsMapEditor())
             {
                 // The gate button is available only for the active editor player's buildings.
                 return EditorDirector.instance?.ActivePlayerID ?? -1;
@@ -1034,7 +1034,7 @@ namespace ExtraFeatures
             return localPlayerId > 0 ? localPlayerId : -1;
         }
 
-        private static bool IsMapEditor() => Shared.GameModeHelper.IsMapEditor();
+        private static bool IsMapEditor() => APIShared.GameModes.GameModeHelper.IsMapEditor();
 
         private static string DescribeBuilding(int buildingId)
         {
@@ -1062,7 +1062,7 @@ namespace ExtraFeatures
         }
 
         private void LogInfo(string message) => Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
-            () => log.LogInfo($"[{TimestampNow()}] Extra Features {message}"));
+            () => log.LogDebug($"[{TimestampNow()}] Extra Features {message}"));
         private void LogWarning(string message) => Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
             () => log.LogWarning($"[{TimestampNow()}] Extra Features {message}"));
         private void LogError(string message) => Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(

diff --git a/ExtraFeatures/src/HealerNativeContract.cs b/ExtraFeatures/src/HealerNativeContract.cs
index ef8f6c458..0b86539ec 100644
--- a/ExtraFeatures/src/HealerNativeContract.cs
+++ b/ExtraFeatures/src/HealerNativeContract.cs
@@ -73,7 +73,7 @@ namespace ExtraFeatures
                 Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_GlobalId)).ToInt32() != 0x94 ||
                 Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentWorldPositionX)).ToInt32() != 0xB2 ||
                 Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentWorldPositionY)).ToInt32() != 0xB4 ||
-                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.N000000A7)).ToInt32() != 0x2A0 ||
+                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_SelectionAllowed)).ToInt32() != 0x2A0 ||
                 Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_AIState)).ToInt32() != 0x2BC ||
                 Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentHealth)).ToInt32() != 0x3C4 ||
                 Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_MaxHealth)).ToInt32() != 0x3C8 ||

diff --git a/ExtraFeatures/src/HealerTargetsRuntime.cs b/ExtraFeatures/src/HealerTargetsRuntime.cs
index 491c71f93..71e842652 100644
--- a/ExtraFeatures/src/HealerTargetsRuntime.cs
+++ b/ExtraFeatures/src/HealerTargetsRuntime.cs
@@ -168,7 +168,7 @@ namespace ExtraFeatures
                 bool civilian = (mask & CivilianMask) != 0 && IsHumanCivilian(unit->r_UnitChimp);
                 if ((!siege && !civilian) || seen[unitId] ||
                     unit->r_ControllableForPlayerId != playerId ||
-                    unit->r_AliveState != AliveState.IsAlive || unit->r_GlobalId == 0 ||
+                    !APIShared.UnitAccess.IsReallyAlive(unit) || unit->r_GlobalId == 0 ||
                     unit->r_CurrentHealth == 0 || unit->r_CurrentHealth >= unit->r_MaxHealth)
                     continue;
 
@@ -247,7 +247,7 @@ namespace ExtraFeatures
             if (Volatile.Read(ref postStartupLogged) != 0)
                 return;
             if (Interlocked.Exchange(ref postStartupLogged, 1) == 0)
-                log.LogInfo("HEALER_TARGETS_POST_STARTUP: tick=" + tick +
+                Shared.DebugLogHelper.LogDebug(log, "HEALER_TARGETS_POST_STARTUP: tick=" + tick +
                     ", civilians=" + ((Volatile.Read(ref enabledMask) & CivilianMask) != 0) +
                     ", siege=" + ((Volatile.Read(ref enabledMask) & SiegeMask) != 0));
         }

diff --git a/ExtraFeatures/src/KnightDismountDelayRuntime.cs b/ExtraFeatures/src/KnightDismountDelayRuntime.cs
index 38f8fc448..e2c9cd560 100644
--- a/ExtraFeatures/src/KnightDismountDelayRuntime.cs
+++ b/ExtraFeatures/src/KnightDismountDelayRuntime.cs
@@ -1,3 +1,4 @@
+using APIShared.Commands;
 using CrusaderDE;
 using MonoMod.RuntimeDetour;
 using R3;
@@ -23,10 +24,10 @@ namespace ExtraFeatures
         private static readonly object PersistentInfrastructureLock = new object();
         private static readonly List<IDisposable> PersistentSubscriptions = new List<IDisposable>();
         private static KnightDismountRuntime activeRuntime;
-        private static Hook persistentGameActionHook;
+        private static bool sharedGameActionsRegistered;
+        [ThreadStatic] private static bool internalStop;
         private static Hook persistentSaveHook;
         private static bool visualSubscriptionsInstalled;
-        private static EngineInterfaceGameActionDelegate persistentGameActionTrampoline;
         private static EngineInterfaceSaveDelegate persistentSaveTrampoline;
         private static Texture2D progressTexture;
         private static Sprite progressSprite;
@@ -41,12 +42,6 @@ namespace ExtraFeatures
         private DeferredSaveRequest deferredSave;
         private long deferredSaveReadyAfterTick = long.MaxValue;
 
-        private delegate int EngineInterfaceGameActionDelegate(
-            Enums.GameActionCommand command,
-            int structureId,
-            int state,
-            int value2);
-
         private delegate bool EngineInterfaceSaveDelegate(
             string path,
             int screenCentreX,
@@ -61,18 +56,12 @@ namespace ExtraFeatures
         {
             lock (PersistentInfrastructureLock)
             {
-                if (persistentGameActionHook != null && persistentSaveHook != null)
+                if (sharedGameActionsRegistered && persistentSaveHook != null)
                 {
                     activeRuntime = this;
                     return;
                 }
 
-                MethodInfo gameActionMethod = typeof(EngineInterface).GetMethod(
-                    nameof(EngineInterface.GameAction),
-                    BindingFlags.Public | BindingFlags.Static,
-                    null,
-                    new[] { typeof(Enums.GameActionCommand), typeof(int), typeof(int), typeof(int) },
-                    null);
                 MethodInfo saveMethod = typeof(EngineInterface).GetMethod(
                     nameof(EngineInterface.SaveSaveGame),
                     BindingFlags.Public | BindingFlags.Static,
@@ -84,20 +73,15 @@ namespace ExtraFeatures
                     },
                     null);
 
-                if (gameActionMethod == null)
-                    throw new MissingMethodException(typeof(EngineInterface).FullName, nameof(EngineInterface.GameAction));
                 if (saveMethod == null)
                     throw new MissingMethodException(typeof(EngineInterface).FullName, nameof(EngineInterface.SaveSaveGame));
 
-                Hook gameActionCandidate = null;
                 Hook saveCandidate = null;
                 var subscriptionCandidates = new List<IDisposable>();
                 bool tickSubscribed = false;
                 try
                 {
-                    gameActionCandidate = new Hook(gameActionMethod, (EngineInterfaceGameActionDelegate)PersistentGameActionHook);
                     saveCandidate = new Hook(saveMethod, (EngineInterfaceSaveDelegate)PersistentSaveHook);
-                    persistentGameActionTrampoline = gameActionCandidate.GenerateTrampoline<EngineInterfaceGameActionDelegate>();
                     persistentSaveTrampoline = saveCandidate.GenerateTrampoline<EngineInterfaceSaveDelegate>();
 
                     subscriptionCandidates.Add(UnitR3EventHooks.OnUnitMoveHere.Observable.Subscribe(PersistentUnitMoveHere));
@@ -105,7 +89,12 @@ namespace ExtraFeatures
                     GameTimeManagerAPI.Instance.OnTick += PersistentGameTick;
                     tickSubscribed = true;
 
-                    persistentGameActionHook = gameActionCandidate;
+                    if (!sharedGameActionsRegistered)
+                    {
+                        if (!GameActionEvents.TryRegister(ExtraFeaturesPlugin.PluginGuid, "KnightStop", null, null,
+                            out string reason, accepted: BeforeAcceptedGameAction)) throw new InvalidOperationException(reason);
+                        sharedGameActionsRegistered = true;
+                    }
                     persistentSaveHook = saveCandidate;
                     PersistentSubscriptions.AddRange(subscriptionCandidates);
                     activeRuntime = this;
@@ -119,8 +108,6 @@ namespace ExtraFeatures
                     for (int index = subscriptionCandidates.Count - 1; index >= 0; index--)
                         subscriptionCandidates[index]?.Dispose();
                     saveCandidate?.Dispose();
-                    gameActionCandidate?.Dispose();
-                    persistentGameActionTrampoline = null;
                     persistentSaveTrampoline = null;
                     throw;
                 }
@@ -153,19 +140,12 @@ namespace ExtraFeatures
             }
         }
 
-        private static int PersistentGameActionHook(
-            Enums.GameActionCommand command,
-            int structureId,
-            int state,
-            int value2)
+        private static void BeforeAcceptedGameAction(GameActionAcceptedEventArgs args)
         {
             KnightDismountRuntime runtime = activeRuntime;
-            if (command == Enums.GameActionCommand.Troops_Stop && runtime != null && runtime.IsPendingRuntimeActive())
+            if (!internalStop && args.Command == Enums.GameActionCommand.Troops_Stop && runtime != null && runtime.IsPendingRuntimeActive())
                 runtime.OnPlayerStopCommand();
-
-            return persistentGameActionTrampoline(command, structureId, state, value2);
         }
-
         private static bool PersistentSaveHook(
             string path,
             int screenCentreX,
@@ -249,13 +229,11 @@ namespace ExtraFeatures
 
         private void IssueInternalStop()
         {
-            EngineInterfaceGameActionDelegate original = persistentGameActionTrampoline;
-            if (original != null)
-                original(Enums.GameActionCommand.Troops_Stop, 0, 0, 0);
-            else
-                EngineInterface.GameAction(Enums.GameActionCommand.Troops_Stop, 0, 0, 0);
+            bool previous = internalStop;
+            internalStop = true;
+            try { EngineInterface.GameAction(Enums.GameActionCommand.Troops_Stop, 0, 0, 0); }
+            finally { internalStop = previous; }
         }
-
         private void StartPendingBatch(
             int playerId,
             int action,
@@ -296,7 +274,7 @@ namespace ExtraFeatures
                     ? eChimps.CHIMP_TYPE_SWORDSMAN
                     : eChimps.CHIMP_TYPE_KNIGHT;
                 if (!TryResolveAliveUnitByGlobalId(snapshot, expectedType, out int currentUnitId) ||
-                    !GameUnitManagerAPI.Instance.TryGetUnitById(currentUnitId, out GameUnit* currentUnit))
+                    !APIShared.UnitAccess.TryGetById(currentUnitId, out GameUnit* currentUnit, out _))
                 {
                     continue;
                 }
@@ -435,7 +413,7 @@ namespace ExtraFeatures
                     continue;
                 }
 
-                if (!GameUnitManagerAPI.Instance.TryGetUnitById(currentUnitId, out GameUnit* unit) ||
+                if (!APIShared.UnitAccess.TryGetById(currentUnitId, out GameUnit* unit, out _) ||
                     unit->r_ControllableForPlayerId != pending.PlayerId ||
                     unit->r_UnitChimp != pending.ExpectedType)
                 {
@@ -489,7 +467,7 @@ namespace ExtraFeatures
         {
             selectedReplacementUnitId = 0;
             if (!TryResolveAliveUnitByGlobalId(pending.Snapshot, eChimps.CHIMP_TYPE_SWORDSMAN, out int swordsmanUnitId) ||
-                !GameUnitManagerAPI.Instance.TryGetUnitById(swordsmanUnitId, out GameUnit* swordsman) ||
+                !APIShared.UnitAccess.TryGetById(swordsmanUnitId, out GameUnit* swordsman, out _) ||
                 !ReservationMatches(pending, swordsmanUnitId, swordsman))
             {
                 return false;
@@ -502,10 +480,10 @@ namespace ExtraFeatures
                 "mount",
                 reason,
                 pending.LimitReservationId);
-            if (knightUnitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(knightUnitId, out GameUnit* knight))
+            if (knightUnitId <= 0 || !APIShared.UnitAccess.TryGetById(knightUnitId, out GameUnit* knight, out _))
             {
                 if (knightUnitId > 0)
-                    GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
+                    if (APIShared.UnitAccess.TryGetById(knightUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
                 return false;
             }
 
@@ -513,16 +491,16 @@ namespace ExtraFeatures
             if (!TryTransferReservationToKnight(
                     pending, swordsmanUnitId, swordsman, knightUnitId, knight, reason + "-transfer"))
             {
-                GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
+                if (APIShared.UnitAccess.TryGetById(knightUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
                 return false;
             }
 
             bool transferSelection = ShouldTransferSelection(currentSnapshot.OwnerPlayerId, swordsman);
-            if (!GameUnitManagerAPI.Instance.DeleteUnitSafe(swordsmanUnitId))
+            if (!(APIShared.UnitAccess.TryGetById(swordsmanUnitId, out _, out _) && GameUnitManagerAPI.Instance.DeleteUnitSafe(swordsmanUnitId)))
             {
                 ReleaseExactHorseLink(allocation, knightUnitId, (int)knight->r_GlobalId, reason + "-delete-rollback");
                 RestoreReservationToSwordsman(pending, swordsmanUnitId, reason + "-delete-rollback");
-                GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
+                if (APIShared.UnitAccess.TryGetById(knightUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
                 return false;
             }
 
@@ -537,7 +515,7 @@ namespace ExtraFeatures
 
         private static bool ShouldTransferSelection(int ownerPlayerId, GameUnit* source)
         {
-            return source != null && source->r_AliveState == AliveState.IsAlive &&
+            return source != null && APIShared.UnitAccess.IsReallyAlive(source) &&
                 IsSelected(source) && ownerPlayerId == GetSelectionPlayerId();
         }
 
@@ -550,8 +528,8 @@ namespace ExtraFeatures
             var invalidIds = new List<int>();
             foreach (int unitId in pendingSelectionRequestIds)
             {
-                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
-                    unit->r_AliveState != AliveState.IsAlive ||
+                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
+                    !APIShared.UnitAccess.IsReallyAlive(unit) ||
                     unit->r_ControllableForPlayerId != localPlayerId)
                 {
                     invalidIds.Add(unitId);
@@ -562,7 +540,7 @@ namespace ExtraFeatures
 
             foreach (int unitId in pendingSelectionRequestIds)
             {
-                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
+                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                     !IsSelected(unit))
                 {
                     return;
@@ -588,9 +566,9 @@ namespace ExtraFeatures
             {
                 int localPlayerId = GetSelectionPlayerId();
                 GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
-                foreach (int unitId in unitApi.GetAllAliveUnits())
+                foreach (int unitId in APIShared.UnitAccess.GetAllReallyAliveUnits())
                 {
-                    if (unitApi.TryGetUnitById(unitId, out GameUnit* unit) &&
+                    if (APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _) &&
                         unit->r_ControllableForPlayerId == localPlayerId &&
                         IsSelected(unit))
                         pendingSelectionRequestIds.Add(unitId);
@@ -615,8 +593,8 @@ namespace ExtraFeatures
                 GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                 foreach (int unitId in pendingSelectionTransferIds)
                 {
-                    bool found = unitApi.TryGetUnitById(unitId, out GameUnit* unit);
-                    if (!found || unit->r_AliveState != AliveState.IsAlive ||
+                    bool found = APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _);
+                    if (!found || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                         unit->r_ControllableForPlayerId != localPlayerId)
                     {
                         pendingSelectionTransferTicks++;
@@ -632,9 +610,9 @@ namespace ExtraFeatures
 
                 var selectedUnitIds = new List<int>();
                 var seen = new HashSet<int>();
-                foreach (int unitId in unitApi.GetAllAliveUnits())
+                foreach (int unitId in APIShared.UnitAccess.GetAllReallyAliveUnits())
                 {
-                    if (unitApi.TryGetUnitById(unitId, out GameUnit* unit) &&
+                    if (APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _) &&
                         unit->r_ControllableForPlayerId == localPlayerId &&
                         IsSelected(unit) && seen.Add(unitId))
                         selectedUnitIds.Add(unitId);
@@ -643,8 +621,8 @@ namespace ExtraFeatures
                 // Keep the previous request and replacements until the new gesture commits.
                 foreach (int unitId in pendingSelectionRequestIds)
                 {
-                    if (unitApi.TryGetUnitById(unitId, out GameUnit* unit) &&
-                        unit->r_AliveState == AliveState.IsAlive &&
+                    if (APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _) &&
+                        APIShared.UnitAccess.IsReallyAlive(unit) &&
                         unit->r_ControllableForPlayerId == localPlayerId && seen.Add(unitId))
                     {
                         selectedUnitIds.Add(unitId);
@@ -765,7 +743,7 @@ namespace ExtraFeatures
         {
             HorseAllocation allocation = pending.Allocation;
             if (!ReservationSlotIsFree(allocation) ||
-                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* swordsman) ||
+                !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* swordsman, out _) ||
                 (int)swordsman->r_GlobalId != pending.Snapshot.GlobalId ||
                 swordsman->r_LinkedStableBuildingId != 0 || swordsman->r_LinkedStableGlobalId != 0)
             {
@@ -809,7 +787,7 @@ namespace ExtraFeatures
 
             if (GetStableHorseSlotUnitId(stable, allocation.Slot) != unitId ||
                 GetStableHorseSlotGlobalId(stable, allocation.Slot) != unitGlobalId ||
-                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
+                !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                 (int)unit->r_GlobalId != unitGlobalId ||
                 unit->r_LinkedStableBuildingId != allocation.StableId ||
                 unit->r_LinkedStableGlobalId != (uint)allocation.StableGlobalId)
@@ -827,7 +805,7 @@ namespace ExtraFeatures
             int unitGlobalId,
             HorseAllocation allocation)
         {
-            if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) &&
+            if (APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) &&
                 (int)unit->r_GlobalId == unitGlobalId &&
                 unit->r_LinkedStableBuildingId == allocation.StableId &&
                 unit->r_LinkedStableGlobalId == (uint)allocation.StableGlobalId)
@@ -935,11 +913,11 @@ namespace ExtraFeatures
             for (int index = 0; selection != null && index < selection.Count; index++)
                 AddSelectedPendingGlobalId(playerId, selection[index].UnitId, result, seen);
 
-            int[] aliveIds = GameUnitManagerAPI.Instance.GetAllAliveUnits();
+            int[] aliveIds = APIShared.UnitAccess.GetAllReallyAliveUnits();
             for (int index = 0; index < aliveIds.Length; index++)
             {
                 int unitId = aliveIds[index];
-                if (unitId > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) && IsSelected(unit))
+                if (unitId > 0 && APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) && IsSelected(unit))
                     AddSelectedPendingGlobalId(playerId, unitId, result, seen);
             }
 
@@ -953,7 +931,7 @@ namespace ExtraFeatures
             List<int> result,
             HashSet<int> seen)
         {
-            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
+            if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _))
                 return;
 
             int globalId = (int)unit->r_GlobalId;
@@ -967,7 +945,7 @@ namespace ExtraFeatures
 
         private bool IsPendingUnitId(int unitId)
         {
-            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
+            if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _))
                 return false;
 
             int globalId = (int)unit->r_GlobalId;
@@ -1062,7 +1040,7 @@ namespace ExtraFeatures
         private void UpdateProgressVisual(Chimp chimp)
         {
             int unitId = chimp.objectID;
-            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
+            if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _))
                 return;
 
             int globalId = (int)unit->r_GlobalId;
@@ -1120,7 +1098,7 @@ namespace ExtraFeatures
 
         private void RemoveProgressVisualForUnitId(int unitId)
         {
-            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
+            if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _))
                 return;
 
             RemoveProgressVisual((int)unit->r_GlobalId);

diff --git a/ExtraFeatures/src/KnightDismountRuntime.cs b/ExtraFeatures/src/KnightDismountRuntime.cs
index 101672cec..f0ac14160 100644
--- a/ExtraFeatures/src/KnightDismountRuntime.cs
+++ b/ExtraFeatures/src/KnightDismountRuntime.cs
@@ -1,3 +1,4 @@
+using APIShared.GameModes;
 // Feature: Mount swordsmen and dismount mounted knights through local commands.
 using BepInEx.Logging;
 using CrusaderDE;
@@ -629,18 +630,18 @@ namespace ExtraFeatures
             for (int i = 0; selection != null && i < selection.Count; i++)
             {
                 int unitId = selection[i].UnitId;
-                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
+                if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _))
                     continue;
 
                 if (IsOwnAliveUnit(unit, localPlayerId, unitType))
                     return true;
             }
 
-            int[] aliveUnits = unitApi.GetAllAliveUnits();
+            int[] aliveUnits = APIShared.UnitAccess.GetAllReallyAliveUnits();
             for (int i = 0; i < aliveUnits.Length; i++)
             {
                 int unitId = aliveUnits[i];
-                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
+                if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _))
                     continue;
 
                 if (IsSelected(unit) && IsOwnAliveUnit(unit, localPlayerId, unitType))
@@ -770,9 +771,8 @@ namespace ExtraFeatures
 
         private bool IsChoreTransportReady()
         {
-            return ExtraFeaturesChoreSender.IsAvailable(
-                networkInitialized && transformationPacketHook != null,
-                () => SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA);
+            return APIShared.Networking.ChoreTransport.IsAvailable(
+                networkInitialized && transformationPacketHook != null);
         }
 
         private bool TrySendTransformationChore(int playerId, int action, List<UnitTransformSnapshot> snapshots)
@@ -825,13 +825,10 @@ namespace ExtraFeatures
             }
 
             short packetId = transformationPacketHook?.GetPacketId() ?? (short)0;
-            if (!ExtraFeaturesChoreSender.TrySend(
+            if (!APIShared.Networking.ChoreTransport.TrySend(
                     packet,
                     packetId,
                     networkInitialized && transformationPacketHook != null,
-                    value => GameNetworkAPI.Serialize(value),
-                    () => SHCDESE.GameGlobals.GameGlobalsManager.Instance.ChoreManagerVA,
-                    (value, id) => GameNetworkAPI.SendPacketToAllEx2(value, id, viaChore: true),
                     out byte[] body,
                     out string rejectionReason))
             {
@@ -844,6 +841,9 @@ namespace ExtraFeatures
 
         private void OnTransformationPacketReceived(ReceiveCustomPacketEventArgs<KnightTransformationPacket> args)
         {
+            if (!APIShared.Networking.ChoreTransport.IsChoreDelivery(args))
+                return;
+
             if (!Shared.GameplayModActivationGate.IsAllowed)
                 return;
 
@@ -907,7 +907,7 @@ namespace ExtraFeatures
                     continue;
 
                 int unitId = FindAliveUnitIdByGlobalId(globalId);
-                if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
+                if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                     !IsOwnAliveUnit(unit, playerId, expectedType))
                 {
                     continue;
@@ -970,7 +970,7 @@ namespace ExtraFeatures
             for (int i = 0; selection != null && i < selection.Count; i++)
             {
                 int unitId = selection[i].UnitId;
-                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
+                if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _))
                     continue;
 
                 if (!IsOwnAliveUnit(unit, localPlayerId, unitType))
@@ -979,11 +979,11 @@ namespace ExtraFeatures
                 AddSnapshot(snapshots, seenGlobalIds, unitId, unit);
             }
 
-            int[] aliveUnits = unitApi.GetAllAliveUnits();
+            int[] aliveUnits = APIShared.UnitAccess.GetAllReallyAliveUnits();
             for (int i = 0; i < aliveUnits.Length; i++)
             {
                 int unitId = aliveUnits[i];
-                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
+                if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _))
                     continue;
 
                 if (!IsSelected(unit) || !IsOwnAliveUnit(unit, localPlayerId, unitType))
@@ -1053,7 +1053,7 @@ namespace ExtraFeatures
                 if (!TryResolveAliveUnitByUnitId(resolved.Snapshot, eChimps.CHIMP_TYPE_KNIGHT, out int deleteUnitId))
                     continue;
 
-                if (!GameUnitManagerAPI.Instance.TryGetUnitById(deleteUnitId, out GameUnit* deleteUnit))
+                if (!APIShared.UnitAccess.TryGetById(deleteUnitId, out GameUnit* deleteUnit, out _))
                     continue;
 
                 UnitTransformSnapshot currentSnapshot = CreateSnapshotFromUnit(deleteUnitId, deleteUnit);
@@ -1062,24 +1062,24 @@ namespace ExtraFeatures
                     continue;
 
                 if (!TryResolveAliveUnitByGlobalId(currentSnapshot, eChimps.CHIMP_TYPE_KNIGHT, out int currentKnightId) ||
-                    !GameUnitManagerAPI.Instance.TryGetUnitById(currentKnightId, out GameUnit* currentKnight))
+                    !APIShared.UnitAccess.TryGetById(currentKnightId, out GameUnit* currentKnight, out _))
                 {
                     LogError($"Knight dismount could not reacquire the original knight after spawning its replacement: reason={reason}, originalUnitId={deleteUnitId}, globalId={currentSnapshot.GlobalId}.");
-                    GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
+                    if (APIShared.UnitAccess.TryGetById(swordsmanUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                     continue;
                 }
 
                 if (!TryConsumeLinkedStableHorse(currentKnightId, currentKnight, reason, out ConsumedStableHorse consumedHorse))
                 {
-                    GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
+                    if (APIShared.UnitAccess.TryGetById(swordsmanUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                     continue;
                 }
 
-                if (!GameUnitManagerAPI.Instance.DeleteUnitSafe(currentKnightId))
+                if (!(APIShared.UnitAccess.TryGetById(currentKnightId, out _, out _) && GameUnitManagerAPI.Instance.DeleteUnitSafe(currentKnightId)))
                 {
                     RollbackConsumedStableHorse(consumedHorse, reason);
                     LogError($"Knight dismount could not mark the original knight for Vanilla deletion: reason={reason}, unitId={currentKnightId}, globalId={currentSnapshot.GlobalId}.");
-                    GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
+                    if (APIShared.UnitAccess.TryGetById(swordsmanUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                     continue;
                 }
 
@@ -1093,7 +1093,7 @@ namespace ExtraFeatures
             if (!TryResolveAliveUnitByGlobalId(snapshot, eChimps.CHIMP_TYPE_KNIGHT, out int currentUnitId))
                 return false;
 
-            if (!GameUnitManagerAPI.Instance.TryGetUnitById(currentUnitId, out GameUnit* currentUnit))
+            if (!APIShared.UnitAccess.TryGetById(currentUnitId, out GameUnit* currentUnit, out _))
                 return false;
 
             UnitTransformSnapshot currentSnapshot = CreateSnapshotFromUnit(currentUnitId, currentUnit);
@@ -1107,25 +1107,25 @@ namespace ExtraFeatures
                 return false;
 
             if (!TryResolveAliveUnitByGlobalId(currentSnapshot, eChimps.CHIMP_TYPE_KNIGHT, out int currentKnightId) ||
-                !GameUnitManagerAPI.Instance.TryGetUnitById(currentKnightId, out GameUnit* currentKnight))
+                !APIShared.UnitAccess.TryGetById(currentKnightId, out GameUnit* currentKnight, out _))
             {
                 LogError($"Knight dismount could not reacquire the original knight after spawning its replacement: reason={reason}, originalUnitId={currentUnitId}, globalId={currentSnapshot.GlobalId}.");
-                GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
+                if (APIShared.UnitAccess.TryGetById(swordsmanUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                 return false;
             }
 
             if (!TryConsumeLinkedStableHorse(currentKnightId, currentKnight, reason, out ConsumedStableHorse consumedHorse))
             {
-                GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
+                if (APIShared.UnitAccess.TryGetById(swordsmanUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                 return false;
             }
 
             bool transferSelection = ShouldTransferSelection(currentSnapshot.OwnerPlayerId, currentKnight);
-            if (!GameUnitManagerAPI.Instance.DeleteUnitSafe(currentKnightId))
+            if (!(APIShared.UnitAccess.TryGetById(currentKnightId, out _, out _) && GameUnitManagerAPI.Instance.DeleteUnitSafe(currentKnightId)))
             {
                 RollbackConsumedStableHorse(consumedHorse, reason);
                 LogError($"Knight dismount could not mark the original knight for Vanilla deletion: reason={reason}, unitId={currentKnightId}, globalId={currentSnapshot.GlobalId}.");
-                GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
+                if (APIShared.UnitAccess.TryGetById(swordsmanUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                 return false;
             }
 
@@ -1176,11 +1176,11 @@ namespace ExtraFeatures
                 if (!TryResolveAliveUnitByUnitId(resolved.Snapshot, eChimps.CHIMP_TYPE_SWORDSMAN, out int deleteUnitId))
                     continue;
 
-                if (!GameUnitManagerAPI.Instance.TryGetUnitById(deleteUnitId, out GameUnit* deleteUnit))
+                if (!APIShared.UnitAccess.TryGetById(deleteUnitId, out GameUnit* deleteUnit, out _))
                     continue;
 
                 UnitTransformSnapshot currentSnapshot = CreateSnapshotFromUnit(deleteUnitId, deleteUnit);
-                GameUnitManagerAPI.Instance.DeleteUnit(deleteUnitId);
+                if (APIShared.UnitAccess.TryGetById(deleteUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(deleteUnitId);
                 deletedSnapshots.Add(new AppliedMountSnapshot
                 {
                     Snapshot = currentSnapshot,
@@ -1201,11 +1201,11 @@ namespace ExtraFeatures
             if (!TryResolveAliveUnitByGlobalId(snapshot, eChimps.CHIMP_TYPE_SWORDSMAN, out int currentUnitId))
                 return false;
 
-            if (!GameUnitManagerAPI.Instance.TryGetUnitById(currentUnitId, out GameUnit* currentUnit))
+            if (!APIShared.UnitAccess.TryGetById(currentUnitId, out GameUnit* currentUnit, out _))
                 return false;
 
             UnitTransformSnapshot currentSnapshot = CreateSnapshotFromUnit(currentUnitId, currentUnit);
-            GameUnitManagerAPI.Instance.DeleteUnit(currentUnitId);
+            if (APIShared.UnitAccess.TryGetById(currentUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(currentUnitId);
             return CreateMountedKnightFromSnapshot(currentSnapshot, allocation, reason);
         }
 
@@ -1223,10 +1223,10 @@ namespace ExtraFeatures
 
         private bool ValidateAliveUnit(UnitTransformSnapshot snapshot, eChimps expectedType, int currentUnitId)
         {
-            if (currentUnitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(currentUnitId, out GameUnit* unit))
+            if (currentUnitId <= 0 || !APIShared.UnitAccess.TryGetById(currentUnitId, out GameUnit* unit, out _))
                 return false;
 
-            if (unit->r_AliveState != AliveState.IsAlive ||
+            if (!APIShared.UnitAccess.IsReallyAlive(unit) ||
                 unit->r_UnitChimp != expectedType ||
                 unit->r_ControllableForPlayerId != snapshot.OwnerPlayerId)
                 return false;
@@ -1274,7 +1274,7 @@ namespace ExtraFeatures
 
             if (snapshot.LinkedProductionBuildingId > 0 &&
                 snapshot.LinkedProductionBuildingId <= ushort.MaxValue &&
-                GameUnitManagerAPI.Instance.TryGetUnitById((int)createdId, out GameUnit* createdUnit))
+                APIShared.UnitAccess.TryGetById((int)createdId, out GameUnit* createdUnit, out _))
             {
                 // Preserve the barracks/production link; the horse stable has separate hidden fields.
                 createdUnit->r_LinkedProductionBuildingId = (ushort)snapshot.LinkedProductionBuildingId;
@@ -1294,10 +1294,10 @@ namespace ExtraFeatures
                 return false;
             }
 
-            if (!GameUnitManagerAPI.Instance.TryGetUnitById(knightUnitId, out GameUnit* knight))
+            if (!APIShared.UnitAccess.TryGetById(knightUnitId, out GameUnit* knight, out _))
             {
                 LogError($"Knight mount could not resolve spawned knight: reason={reason}, knightUnitId={knightUnitId}, sourceGlobalId={snapshot.GlobalId}.");
-                GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
+                if (APIShared.UnitAccess.TryGetById(knightUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
                 if (CreateUnitFromSnapshot(snapshot, eChimps.CHIMP_TYPE_SWORDSMAN, "mount-resolve-rollback", reason) <= 0)
                     LogError($"Knight mount resolve rollback could not restore swordsman: reason={reason}, sourceGlobalId={snapshot.GlobalId}.");
                 return false;
@@ -1306,7 +1306,7 @@ namespace ExtraFeatures
             if (!TryConsumeStableHorse(allocation, knightUnitId, (int)knight->r_GlobalId, reason))
             {
                 LogError($"Knight mount could not link stable horse: reason={reason}, knightUnitId={knightUnitId}, stableId={allocation.StableId}, slot={allocation.Slot}.");
-                GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
+                if (APIShared.UnitAccess.TryGetById(knightUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
                 if (CreateUnitFromSnapshot(snapshot, eChimps.CHIMP_TYPE_SWORDSMAN, "mount-rollback", reason) <= 0)
                     LogError($"Knight mount rollback could not restore swordsman: reason={reason}, sourceGlobalId={snapshot.GlobalId}.");
                 return false;
@@ -1372,7 +1372,7 @@ namespace ExtraFeatures
             if (allocation.Slot < 0 || allocation.Slot >= StableHorseSlotCount)
                 return false;
 
-            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* mountedUnit))
+            if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* mountedUnit, out _))
                 return false;
 
             if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(allocation.StableId, out GameBuilding* stable))
@@ -1471,7 +1471,7 @@ namespace ExtraFeatures
 
                 if (unitId <= 0 || unitGlobalId <= 0 ||
                     !seenUnitIds.Add(unitId) || !seenGlobalIds.Add(unitGlobalId) ||
-                    !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* linkedUnit) ||
+                    !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* linkedUnit, out _) ||
                     (int)linkedUnit->r_GlobalId != unitGlobalId ||
                     linkedUnit->r_LinkedStableBuildingId != stableId ||
                     linkedUnit->r_LinkedStableGlobalId != (uint)stableGlobalId)
@@ -1626,7 +1626,7 @@ namespace ExtraFeatures
                 stable->r_AliveState != AliveState.IsAlive ||
                 stable->r_BuildingType != eStructs.STRUCT_STABLES ||
                 (int)stable->r_GlobalId != consumedHorse.StableGlobalId ||
-                !GameUnitManagerAPI.Instance.TryGetUnitById(consumedHorse.UnitId, out GameUnit* unit) ||
+                !APIShared.UnitAccess.TryGetById(consumedHorse.UnitId, out GameUnit* unit, out _) ||
                 (int)unit->r_GlobalId != consumedHorse.UnitGlobalId)
             {
                 LogError(
@@ -1716,11 +1716,11 @@ namespace ExtraFeatures
                 return -1;
 
             GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
-            int[] aliveUnitIds = unitApi.GetAllAliveUnits();
+            int[] aliveUnitIds = APIShared.UnitAccess.GetAllReallyAliveUnits();
             for (int i = 0; i < aliveUnitIds.Length; i++)
             {
                 int unitId = aliveUnitIds[i];
-                if (unitId <= 0 || !unitApi.TryGetUnitById(unitId, out GameUnit* unit))
+                if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _))
                     continue;
 
                 if ((int)unit->r_GlobalId == globalId)
@@ -1732,7 +1732,7 @@ namespace ExtraFeatures
 
         private void ApplyHealthRatio(int targetUnitId, int sourceCurrentHealth, int sourceMaxHealth, string label)
         {
-            if (!GameUnitManagerAPI.Instance.TryGetUnitById(targetUnitId, out GameUnit* unit))
+            if (!APIShared.UnitAccess.TryGetById(targetUnitId, out GameUnit* unit, out _))
             {
                 LogError($"Knight {label} could not set target health, unit not found: targetUnitId={targetUnitId}.");
                 return;
@@ -1751,7 +1751,7 @@ namespace ExtraFeatures
         private static bool IsOwnAliveUnit(GameUnit* unit, int localPlayerId, eChimps unitType)
         {
             return unit != null &&
-                unit->r_AliveState == AliveState.IsAlive &&
+                APIShared.UnitAccess.IsReallyAlive(unit) &&
                 unit->r_UnitChimp == unitType &&
                 unit->r_ControllableForPlayerId == localPlayerId;
         }
@@ -1786,7 +1786,7 @@ namespace ExtraFeatures
 
         private static int GetControlledPlayerId()
         {
-            if (Shared.GameModeHelper.IsMapEditor())
+            if (APIShared.GameModes.GameModeHelper.IsMapEditor())
             {
                 // Editor actions belong to the player currently selected in the editor toolbar.
                 return EditorDirector.instance?.ActivePlayerID ?? -1;
@@ -1798,12 +1798,12 @@ namespace ExtraFeatures
 
         private static int GetSelectionPlayerId()
         {
-            if (Shared.GameModeHelper.IsMapEditor())
+            if (APIShared.GameModes.GameModeHelper.IsMapEditor())
                 return EditorDirector.instance?.ActivePlayerID ?? -1;
             return GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1;
         }
 
-        private static bool IsMapEditor() => Shared.GameModeHelper.IsMapEditor();
+        private static bool IsMapEditor() => APIShared.GameModes.GameModeHelper.IsMapEditor();
 
         private void LogDebug(string message)
         {

diff --git a/ExtraFeatures/src/LordHealthRuntime.cs b/ExtraFeatures/src/LordHealthRuntime.cs
index b768c210c..0ca7ec150 100644
--- a/ExtraFeatures/src/LordHealthRuntime.cs
+++ b/ExtraFeatures/src/LordHealthRuntime.cs
@@ -1,3 +1,4 @@
+using Shared;
 // Capture completed Vanilla Lord health once; retain that basis across save/load cycles.
 using BepInEx.Logging;
 using R3;
@@ -139,7 +140,7 @@ namespace ExtraFeatures
             if (!mapActive || args.Phase != EventHookPhase.Post || args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue) return;
             // Post arguments retain original inputs even when Pre subscribers change them.
             // Read the returned unit's actual type/owner; do not write HP here.
-            if (!GameUnitManagerAPI.Instance.TryGetUnitById((int)args.ReturnValue, out GameUnit* unit) ||
+            if (!APIShared.UnitAccess.TryGetById((int)args.ReturnValue, out GameUnit* unit, out _) ||
                 unit == null || unit->r_UnitChimp != eChimps.CHIMP_TYPE_LORD) return;
             int playerId = unit->r_ControllableForPlayerId;
             if (playerId < FirstPlayerId || playerId > LastPlayerId || unit->r_GlobalId == 0) return;
@@ -168,7 +169,7 @@ namespace ExtraFeatures
             int unitId = GamePlayerManagerAPI.Instance.GetLordUnitId(playerId);
             if (unitId <= 0) return false;
             int expectedGlobalId = GamePlayerManagerAPI.Instance.GetLordUnitGlobalId(playerId);
-            if (expectedGlobalId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out lord) ||
+            if (expectedGlobalId <= 0 || !APIShared.UnitAccess.TryGetById(unitId, out lord, out _) ||
                 lord == null || lord->r_GlobalId != (uint)expectedGlobalId ||
                 lord->r_UnitChimp != eChimps.CHIMP_TYPE_LORD || lord->r_ControllableForPlayerId != playerId)
             {
@@ -180,7 +181,7 @@ namespace ExtraFeatures
             // Never revive a dying Lord whose alive flag has not yet changed.
             if (lord->r_CurrentHealth == 0) return false;
             retry = lord->r_AliveState == AliveState.NeedsInit;
-            return lord->r_AliveState == AliveState.IsAlive || (capturePending && retry);
+            return APIShared.UnitAccess.IsReallyAlive(lord) || (capturePending && retry);
         }
 
         private bool TryGetBasis(int playerId, GameUnit* lord, out LordHealthBasis basis)

diff --git a/ExtraFeatures/src/MonkAlwaysRunPatch.cs b/ExtraFeatures/src/MonkAlwaysRunPatch.cs
index 22872a5ba..27b10b511 100644
--- a/ExtraFeatures/src/MonkAlwaysRunPatch.cs
+++ b/ExtraFeatures/src/MonkAlwaysRunPatch.cs
@@ -101,7 +101,7 @@ namespace ExtraFeatures
                 throw;
             }
 
-            Shared.DebugLogHelper.LogInfo(
+            Shared.DebugLogHelper.LogDebug(
                 log,
                 $"Extra Features Monk movement hook installed disabled: " +
                 $"startRva=0x{decisionRva:X}, endRva=0x{decisionRva + HookSize:X}, " +

diff --git a/ExtraFeatures/src/MultiplayerFeatureGate.cs b/ExtraFeatures/src/MultiplayerFeatureGate.cs
index 14acc9d6a..1d01b302c 100644
--- a/ExtraFeatures/src/MultiplayerFeatureGate.cs
+++ b/ExtraFeatures/src/MultiplayerFeatureGate.cs
@@ -1,3 +1,4 @@
+using APIShared.GameModes;
 // Temporarily blocks local state-changing features that are not deterministic in multiplayer.
 using BepInEx.Logging;
 using System;
@@ -21,8 +22,8 @@ namespace ExtraFeatures
             {
                 if (hasMapSnapshot)
                     return blocksLocalStateChanges;
-                Shared.GameModeSnapshot snapshot = Shared.GameplayModActivationGate.Snapshot;
-                return snapshot.Kind == Shared.GameModeKind.Unknown || snapshot.IsRealMultiplayer;
+                APIShared.GameModes.GameModeSnapshot snapshot = Shared.GameplayModActivationGate.Snapshot;
+                return snapshot.Kind == APIShared.GameModes.GameModeKind.Unknown || snapshot.IsRealMultiplayer;
             }
         }
 
@@ -30,9 +31,9 @@ namespace ExtraFeatures
         {
             try
             {
-                Shared.GameModeSnapshot snapshot = Shared.GameplayModActivationGate.Snapshot;
-                blocksLocalStateChanges = snapshot.Kind == Shared.GameModeKind.Unknown || snapshot.IsRealMultiplayer;
-                hasMapSnapshot = snapshot.Kind != Shared.GameModeKind.Unknown;
+                APIShared.GameModes.GameModeSnapshot snapshot = Shared.GameplayModActivationGate.Snapshot;
+                blocksLocalStateChanges = snapshot.Kind == APIShared.GameModes.GameModeKind.Unknown || snapshot.IsRealMultiplayer;
+                hasMapSnapshot = snapshot.Kind != APIShared.GameModes.GameModeKind.Unknown;
                 Shared.DebugLogHelper.LogDebug(
                     log,
                     $"Extra Features multiplayer feature gate captured map mode: {snapshot.ToDiagnosticString()}.");

diff --git a/ExtraFeatures/src/PlagueApothecarySearchRangePatch.cs b/ExtraFeatures/src/PlagueApothecarySearchRangePatch.cs
index 1382a6451..8400d9b15 100644
--- a/ExtraFeatures/src/PlagueApothecarySearchRangePatch.cs
+++ b/ExtraFeatures/src/PlagueApothecarySearchRangePatch.cs
@@ -148,7 +148,7 @@ namespace ExtraFeatures
 
                 try
                 {
-                    Shared.DebugLogHelper.LogInfo(
+                    Shared.DebugLogHelper.LogDebug(
                         log,
                         $"Extra Features apothecary plague-search hook installed disabled: " +
                         $"startRva=0x{HookRva:X}, displaced={HookDisplacedBytes}, " +

diff --git a/ExtraFeatures/verify-repair.ps1 b/ExtraFeatures/verify-repair.ps1
index 8b09b61a3..c7ee7c643 100644
--- a/ExtraFeatures/verify-repair.ps1
+++ b/ExtraFeatures/verify-repair.ps1
@@ -153,8 +153,8 @@ foreach ($path in @($info)) {
         throw "Active version mismatch: $path"
     }
 }
-if (-not $activeVersion -or $runtimeText -notmatch 'BepInDependency\(ApiSharedGuid, "0\.4\.6"\)') {
-    throw 'Plugin version or APIShared dependency mismatch.'
+if (-not $activeVersion) {
+    throw 'Plugin version is missing.'
 }
 $addedCode = & git -C $workspace diff --unified=0 -- '*.cs' '*.csproj'
 if ($LASTEXITCODE -ne 0) { throw 'Workspace diff audit failed.' }

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
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/ExtraFeatures.diff).
