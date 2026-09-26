# APIShared release status

**Status:** code newer

- Release: [v0.3.7](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/APIShared/v0.3.7)
- Release commit: [d34061e](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/d34061efa42f0d90490cf3f222e217ff7954c8fb)
- Current main commit: [d43d3d8](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/d43d3d831cc5f89e83ff1abdddb547a7fa579867)

## Relevant changed files

- `APIShared/APIShared.csproj`
- `APIShared/BepInEx/plugins/APIShared_Serp/APIShared.dll`
- `APIShared/BepInEx/plugins/APIShared_Serp/APIShared.pdb`
- `APIShared/BepInEx/plugins/APIShared_Serp/APIShared.xml`
- `APIShared/BepInEx/plugins/APIShared_Serp/info.json`
- `APIShared/build.bat`
- `APIShared/info.json`
- `APIShared/src/APISharedPlugin.cs`
- `APIShared/src/ApiSharedRuntime.cs`
- `APIShared/src/BriefingGoldContracts.cs`
- `APIShared/src/BriefingGoldPresentationCapability.cs`
- `APIShared/src/Contracts.cs`
- `APIShared/src/ElevatedMoatAiState.cs`
- `APIShared/src/GameplayFeatureModePolicy.cs`
- `APIShared/src/GatehouseDistanceOriginCapability.cs`
- `APIShared/src/GatehousePermanentRuntimeState.cs`
- `APIShared/src/GatehouseTimingCapability.cs`
- `APIShared/src/LobbyPreparationOverride.cs`
- `APIShared/src/MissionModePolicy.cs`
- `APIShared/src/ModSettingsPresetDocuments.cs`
- `APIShared/src/ModSettingsSearch.cs`
- `APIShared/src/NativeInfrastructure.cs`
- `APIShared/src/PresetLobbyModSettingsViewModel.cs`
- `APIShared/src/UnitHudPresentationCapability.cs`
- `Shared/AtomicFileReplacement.cs`
- `Shared/DebugLogHelper.cs`
- `Shared/DependencyFreeJson.cs`
- `Shared/GameModeHelper.cs`
- `Shared/GameplaySessionLifecycle.cs`

## Diff

```diff
diff --git a/APIShared/APIShared.csproj b/APIShared/APIShared.csproj
index 5e3602d9..a3056121 100644
--- a/APIShared/APIShared.csproj
+++ b/APIShared/APIShared.csproj
@@ -10,6 +10,7 @@
     <AssemblyName>APIShared</AssemblyName>
     <TargetFrameworkVersion>v4.8.1</TargetFrameworkVersion>
     <LangVersion>latest</LangVersion>
+    <DefineConstants>$(DefineConstants);API_SHARED_LOBBY_OBSERVER;API_SHARED_INTERNAL_JSON</DefineConstants>
     <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
     <OutputPath>BepInEx\plugins\APIShared_Serp\</OutputPath>
     <DebugSymbols>true</DebugSymbols>
@@ -23,6 +24,9 @@
   <ItemGroup>
     <Reference Include="System" />
     <Reference Include="System.Core" />
+    <Reference Include="System.Xml" />
+    <Reference Include="System.Xml.Linq" />
+    <Reference Include="Iced"><HintPath>$(ExtenderDir)\Iced.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="BepInEx"><HintPath>$(GameDir)\BepInEx\core\BepInEx.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="com.rlabrecque.steamworks.net"><HintPath>$(GameDir)\Stronghold Crusader Definitive Edition_Data\Managed\com.rlabrecque.steamworks.net.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="MonoMod.RuntimeDetour"><HintPath>$(GameDir)\BepInEx\core\MonoMod.RuntimeDetour.dll</HintPath><Private>false</Private></Reference>
@@ -32,6 +36,8 @@
     <Reference Include="UnityEngine"><HintPath>$(GameDir)\Stronghold Crusader Definitive Edition_Data\Managed\UnityEngine.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="UnityEngine.CoreModule"><HintPath>$(GameDir)\Stronghold Crusader Definitive Edition_Data\Managed\UnityEngine.CoreModule.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="SHCDESE"><HintPath>$(ExtenderDir)\SHCDESE.dll</HintPath><Private>false</Private></Reference>
+    <Reference Include="MessagePack"><HintPath>$(ExtenderDir)\MessagePack.dll</HintPath><Private>false</Private></Reference>
+    <Reference Include="MessagePack.Annotations"><HintPath>$(ExtenderDir)\MessagePack.Annotations.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="System.Memory"><HintPath>$(ExtenderDir)\System.Memory.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="Microsoft.Extensions.Logging.Abstractions"><HintPath>$(ExtenderDir)\Microsoft.Extensions.Logging.Abstractions.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="RedBird.Abstractions"><HintPath>$(ExtenderDir)\RedBird.Abstractions.dll</HintPath><Private>false</Private></Reference>
@@ -44,21 +50,32 @@
     <Compile Include="src\MissionModePolicy.cs" />
     <Compile Include="src\GameplayModModePolicy.cs" />
     <Compile Include="src\GameplayFeatureModePolicy.cs" />
+    <Compile Include="..\Shared\AtomicFileReplacement.cs"><Link>Shared\AtomicFileReplacement.cs</Link></Compile>
     <Compile Include="..\Shared\DebugLogHelper.cs"><Link>Shared\DebugLogHelper.cs</Link></Compile>
+    <Compile Include="..\Shared\DependencyFreeJson.cs"><Link>Shared\DependencyFreeJson.cs</Link></Compile>
+    <Compile Include="..\Shared\GameModeHelper.cs"><Link>Shared\GameModeHelper.cs</Link></Compile>
     <Compile Include="..\Shared\GameplaySessionLifecycle.cs"><Link>Shared\GameplaySessionLifecycle.cs</Link></Compile>
+    <Compile Include="src\ModSettingsSearch.cs" />
+    <Compile Include="src\PresetLobbyModSettingsViewModel.cs" />
+    <Compile Include="src\ModSettingsPresetDocuments.cs" />
     <Compile Include="src\MissionLifecycleContracts.cs" />
     <Compile Include="src\MissionLifecycleState.cs" />
     <Compile Include="src\MissionLifecycleCapability.cs" />
     <Compile Include="src\AivBuildStepContracts.cs" />
     <Compile Include="src\LobbyStateContracts.cs" />
+    <Compile Include="src\LobbyPreparationOverride.cs" />
+    <Compile Include="src\ElevatedMoatAiState.cs" />
     <Compile Include="src\PlayerDefeatContracts.cs" />
     <Compile Include="src\PlayerDefeatState.cs" />
     <Compile Include="src\UnitHudContracts.cs" />
+    <Compile Include="src\BriefingGoldContracts.cs" />
     <Compile Include="src\NativeInfrastructure.cs" />
     <Compile Include="src\UniquePatternSearch.cs" />
     <Compile Include="src\GatehouseDistanceOriginCapability.cs" />
     <Compile Include="src\GatehouseTimingCapability.cs" />
+    <Compile Include="src\GatehousePermanentRuntimeState.cs" />
     <Compile Include="src\UnitHudPresentationCapability.cs" />
+    <Compile Include="src\BriefingGoldPresentationCapability.cs" />
     <Compile Include="src\AivBuildStepCapability.cs" />
     <Compile Include="src\LobbyStateCapability.cs" />
     <Compile Include="src\PlayerDefeatCapability.cs" />
@@ -74,6 +91,7 @@
     <Error Condition="!Exists('$(ExtenderDir)\SHCDESE.dll')" Text="SHCDESE.dll was not found: $(ExtenderDir)" />
     <Error Condition="!Exists('$(ExtenderDir)\R3.dll')" Text="R3.dll was not found beside SHCDESE.dll: $(ExtenderDir)" />
     <Error Condition="!Exists('$(ExtenderDir)\Microsoft.Extensions.Logging.Abstractions.dll')" Text="Microsoft.Extensions.Logging.Abstractions.dll was not found beside SHCDESE.dll: $(ExtenderDir)" />
+    <Error Condition="!Exists('$(ExtenderDir)\Iced.dll')" Text="Iced.dll was not found beside SHCDESE.dll: $(ExtenderDir)" />
     <Error Condition="!Exists('$(ExtenderDir)\RedBird.Abstractions.dll')" Text="RedBird.Abstractions.dll was not found beside SHCDESE.dll: $(ExtenderDir)" />
     <Error Condition="!Exists('$(ExtenderDir)\RedBird.Core.dll')" Text="RedBird.Core.dll was not found beside SHCDESE.dll: $(ExtenderDir)" />
     <Error Condition="!Exists('$(ExtenderDir)\RedBird.X64.dll')" Text="RedBird.X64.dll was not found beside SHCDESE.dll: $(ExtenderDir)" />

diff --git a/APIShared/BepInEx/plugins/APIShared_Serp/APIShared.xml b/APIShared/BepInEx/plugins/APIShared_Serp/APIShared.xml
index 9224433b..5895cd9d 100644
--- a/APIShared/BepInEx/plugins/APIShared_Serp/APIShared.xml
+++ b/APIShared/BepInEx/plugins/APIShared_Serp/APIShared.xml
@@ -67,6 +67,9 @@
         <member name="F:APIShared.NativeCapabilityIds.PlayerDefeat">
             <summary>Capability for observing player lord deaths and official defeat transitions.</summary>
         </member>
+        <member name="F:APIShared.NativeCapabilityIds.BriefingGoldPresentation">
+            <summary>Capability for deterministic post-Vanilla mission-briefing gold presentation.</summary>
+        </member>
         <member name="T:APIShared.NativeCapabilityDiagnostic">
             <summary>Immutable diagnostic information returned by capability acquisition and mutation.</summary>
         </member>
@@ -115,6 +118,9 @@
         <member name="M:APIShared.IApiShared.TryGetPlayerDefeat(System.String,APIShared.IPlayerDefeatCapability@,APIShared.NativeCapabilityDiagnostic@)">
             <summary>Attempts to acquire the process-wide managed player-defeat observer.</summary>
         </member>
+        <member name="M:APIShared.IApiShared.TryGetBriefingGoldPresentation(System.String,APIShared.IBriefingGoldPresentationCapability@,APIShared.NativeCapabilityDiagnostic@)">
+            <summary>Attempts to acquire the process-wide mission-briefing gold presentation capability.</summary>
+        </member>
         <member name="T:APIShared.ApiShared">
             <summary>Static access to the process-wide API and its readiness notification.</summary>
         </member>
@@ -391,6 +397,48 @@
         <member name="M:APIShared.ILobbyStateCapability.TryRegisterObserver(System.String,System.Action{APIShared.LobbyStateSnapshot},APIShared.NativeCapabilityDiagnostic@)">
             <summary>Registers one process-lifetime observer under an owner-local stable ID.</summary>
         </member>
+        <member name="T:APIShared.LobbyPreparationOverride">
+            <summary>Process-lifetime, single-owner preparation of a newly opened skirmish lobby.</summary>
+        </member>
+        <member name="M:APIShared.LobbyPreparationOverride.Register(System.String,System.Func{CrusaderDE.FRONT_Multiplayer,System.Boolean},System.Action{CrusaderDE.FRONT_Multiplayer})">
+            <summary>Registers rooted callbacks before the lobby is opened.</summary>
+        </member>
+        <member name="M:APIShared.LobbyPreparationOverride.Tick(CrusaderDE.FRONT_Multiplayer)">
+            <summary>Observes the ready lobby from a process-lifetime frontend callback.</summary>
+        </member>
+        <member name="M:APIShared.LobbyPreparationOverride.Begin(CrusaderDE.FRONT_Multiplayer)">
+            <summary>Reads and validates the preset once for each newly opened lobby.</summary>
+        </member>
+        <member name="P:APIShared.LobbyPreparationOverride.IsActive">
+            <summary>Whether an owner has accepted the current lobby opening.</summary>
+        </member>
+        <member name="M:APIShared.LobbyPreparationOverride.Apply(CrusaderDE.FRONT_Multiplayer)">
+            <summary>Applies the prepared preset after Vanilla has opened its controls.</summary>
+        </member>
+        <member name="T:APIShared.ElevatedMoatAiState">
+            <summary>Effective AI elevated-moat construction state published by ExtraFeatures.</summary>
+        </member>
+        <member name="F:APIShared.ElevatedMoatAiState.Unknown">
+            <summary>No effective runtime state has been established.</summary>
+        </member>
+        <member name="F:APIShared.ElevatedMoatAiState.Disabled">
+            <summary>Elevated AI construction is not enabled by ExtraFeatures.</summary>
+        </member>
+        <member name="F:APIShared.ElevatedMoatAiState.Enabled">
+            <summary>The ExtraFeatures hook is installed and enabled for AI players.</summary>
+        </member>
+        <member name="T:APIShared.ElevatedMoatAiCapability">
+            <summary>Process-wide publication of the effective elevated AI build state.</summary>
+        </member>
+        <member name="P:APIShared.ElevatedMoatAiCapability.Current">
+            <summary>Latest state; unknown until ExtraFeatures publishes a verified state.</summary>
+        </member>
+        <member name="E:APIShared.ElevatedMoatAiCapability.Changed">
+            <summary>Raised when the effective state changes.</summary>
+        </member>
+        <member name="M:APIShared.ElevatedMoatAiCapability.Publish(APIShared.ElevatedMoatAiState)">
+            <summary>Publish the state after reconciling the native patch and AI setting.</summary>
+        </member>
         <member name="T:APIShared.PlayerLordDeathNotification">
             <summary>Immutable notification that a previously confirmed living player lord disappeared or died.</summary>
         </member>
@@ -781,6 +829,51 @@
         <member name="M:APIShared.IUnitHudPresentationCapability.RequestRefresh">
             <summary>Requests a safe Unity-thread presentation refresh.</summary>
         </member>
+        <member name="T:APIShared.BriefingGoldAdjustmentStage">
+            <summary>Fixed ordering stages for mission-briefing starting-gold adjustments.</summary>
+        </member>
+        <member name="F:APIShared.BriefingGoldAdjustmentStage.VanillaCorrection">
+            <summary>Corrections that make the displayed Vanilla base match Vanilla gameplay.</summary>
+        </member>
+        <member name="F:APIShared.BriefingGoldAdjustmentStage.ModAdjustment">
+            <summary>Adjustments contributed by gameplay mods after the effective Vanilla base is known.</summary>
+        </member>
+        <member name="T:APIShared.BriefingGoldContext">
+            <summary>Immutable state for one visible player slot in the mission briefing.</summary>
+        </member>
+        <member name="M:APIShared.BriefingGoldContext.#ctor(System.Int32,System.Boolean,System.Int32,System.Int32,System.Int32,System.Boolean,System.Boolean)">
+            <summary>Creates a mission-briefing starting-gold context.</summary>
+        </member>
+        <member name="P:APIShared.BriefingGoldContext.SlotIndex">
+            <summary>Zero-based slot in the eight-player briefing presentation.</summary>
+        </member>
+        <member name="P:APIShared.BriefingGoldContext.IsHuman">
+            <summary>Whether Vanilla classified this slot as a human player.</summary>
+        </member>
+        <member name="P:APIShared.BriefingGoldContext.VanillaDisplayedGold">
+            <summary>Gold written by Vanilla's briefing method before shared adjustments.</summary>
+        </member>
+        <member name="P:APIShared.BriefingGoldContext.EffectiveVanillaGold">
+            <summary>Vanilla's actual starting gold after its No Starting Gold rule.</summary>
+        </member>
+        <member name="P:APIShared.BriefingGoldContext.CurrentGold">
+            <summary>Value produced by all earlier adjustment stages.</summary>
+        </member>
+        <member name="P:APIShared.BriefingGoldContext.HasNoStartingGoldState">
+            <summary>Whether the current native No Starting Gold state was resolved safely.</summary>
+        </member>
+        <member name="P:APIShared.BriefingGoldContext.NoStartingGoldEnabled">
+            <summary>Whether Vanilla's effective No Starting Gold flag is enabled.</summary>
+        </member>
+        <member name="T:APIShared.BriefingGoldAdjuster">
+            <summary>Returns the non-negative gold value to pass to later adjustments.</summary>
+        </member>
+        <member name="T:APIShared.IBriefingGoldPresentationCapability">
+            <summary>Owner-bound process-wide mission-briefing gold presentation service.</summary>
+        </member>
+        <member name="M:APIShared.IBriefingGoldPresentationCapability.TryRegisterAdjustment(System.String,APIShared.BriefingGoldAdjustmentStage,APIShared.BriefingGoldAdjuster,APIShared.NativeCapabilityDiagnostic@)">
+            <summary>Registers one deterministic process-lifetime adjustment.</summary>
+        </member>
         <member name="T:APIShared.GatehouseDistanceOrigin">
             <summary>Selects the native coordinate used as the origin of gatehouse enemy distance checks.</summary>
         </member>
@@ -1226,5 +1319,135 @@
         <member name="M:Shared.GameplayFeatureModePolicy.LogDecisions(BepInEx.Logging.ManualLogSource,System.String,Shared.GameModeSnapshot,System.String)">
             <summary>LogDecisions in the centralized mission policy contract.</summary>
         </member>
+        <member name="T:Shared.ModSettingsSearchMatcher">
+            <summary>Pure matching policy shared by runtime filtering and isolated tests.</summary>
+        </member>
+        <member name="T:Shared.ModSettingsSearch">
+            <summary>
+            Explicit, localized metadata for one logical mod-setting entry. The properties are
+            attached to the smallest container that represents the logical setting.
+            </summary>
+        </member>
+        <member name="M:Shared.ModSettingsSearch.RegisterSource(System.Object,System.String,BepInEx.Logging.ManualLogSource,System.String)">
+            <summary>
+            Registers the immutable XAML catalog used by the optional SerpsModsHost. Each mod
+            remains standalone because the host consumes only the reflection-friendly data shape.
+            </summary>
+        </member>
+        <member name="T:Shared.ModSettingsSearchEntry">
+            <summary>
+            Public APIShared export shape used by the optional global settings-search host.
+            </summary>
+        </member>
+        <member name="T:Shared.PresetLocalAttribute">
+            <summary>
+            Persists a setting in the shared local preset file without exposing it to
+            the Script Extender's multiplayer synchronization layer.
+            </summary>
+        </member>
+        <member name="T:Shared.PresetLobbyModSettingsViewModel">
+            <summary>
+            Adds two local presets to a Script Extender lobby-settings ViewModel while
+            keeping the outer MessagePack dictionary readable by the Script Extender.
+            </summary>
+        </member>
+        <member name="M:Shared.PresetLobbyModSettingsViewModel.System_ApplyModSettingsSearchTarget(System.String,System.String)">
+            <summary>Safe reflection bridge used by the optional global search host.</summary>
+        </member>
+        <member name="M:Shared.PresetLobbyModSettingsViewModel.SetModDefaultValue``1(System.String,``0)">
+            <summary>
+            Replaces one captured code-default value without changing the current working settings.
+            This is intended for defaults which can only be materialized after dynamic discovery.
+            </summary>
+        </member>
+        <member name="M:Shared.PresetLobbyModSettingsViewModel.ConfigurePerPlayerLobbySettings(Shared.PerPlayerLobbySettingsBuilder)">
+            <summary>
+            Declares the few domain-specific parts of personal settings. Transport,
+            player-slot ownership, lobby convergence and readiness stay in APIShared.
+            </summary>
+        </member>
+        <member name="M:Shared.PresetLobbyModSettingsViewModel.CanMutateSetting(System.String)">
+            <summary>
+            Authorizes a settings mutation before any backing state is changed.
+            Preset and Trail snapshots are trusted internal applications; all other
+            writes use the Script Extender's ownership gate.
+            </summary>
+        </member>
+        <member name="M:Shared.PresetLobbyModSettingsViewModel.CanMutateSettingWithDependents(System.String,System.String[])">
+            <summary>
+            Also refreshes editable proxy properties after a rejected write. The
+            Extender's private revert path keeps these notifications out of sync
+            and storage just like the primary property notification.
+            </summary>
+        </member>
+        <member name="M:Shared.PresetLobbyModSettingsViewModel.System_GetPresetSettingDescriptors">
+            <summary>Returns the persistent settings available to shared preset authoring UI.</summary>
+        </member>
+        <member name="M:Shared.PresetLobbyModSettingsViewModel.System_SavePersonalPreset(System.String,System.String,System.String,System.Collections.Generic.IEnumerable{Shared.PresetSaveSelection},System.Boolean)">
+            <summary>Saves selected settings as a persistent, personal preset JSON file.</summary>
+        </member>
+        <member name="T:Shared.IModSettingsPresetEndpoint">
+            <summary>Typed APIShared endpoint consumed by optional mission-setting providers such as ExtendedData.</summary>
+        </member>
+        <member name="T:Shared.IModSettingsMissionSourceEndpoint">
+            <summary>Optional source metadata supplied before entering a mission preset.</summary>
+        </member>
+        <member name="T:Shared.IModSettingsWorkingCopyEndpoint">
+            <summary>Extended endpoint for replacing an editable mission working copy without touching its source.</summary>
+        </member>
+        <member name="T:Shared.ModSettingsWorkingSourceKind">
+            <summary>Identifies a read-only source which can be materialized into editable working settings.</summary>
+        </member>
+        <member name="T:Shared.ModSettingsWorkingSource">
+            <summary>One source shown by the common ModSettings source selector.</summary>
+        </member>
+        <member name="P:Shared.ModSettingsWorkingSource.IsPreferred">
+            <summary>Whether this source represents the provider's current mission context.</summary>
+        </member>
+        <member name="P:Shared.ModSettingsWorkingSource.PreferenceContextId">
+            <summary>Optional stable identity of the mission context used to distinguish context changes from refreshes.</summary>
+        </member>
+        <member name="T:Shared.IModSettingsWorkingSourceProvider">
+            <summary>Optional provider implemented by mission-data mods such as ExtendedData.</summary>
+        </member>
+        <member name="T:Shared.ModSettingsWorkingSourceRegistry">
+            <summary>Process-wide bridge between APIShared consumers and the optional mission source provider.</summary>
+        </member>
+        <member name="T:Shared.PublishedPresetValueMode">
+            <summary>Determines how a published preset resolves one selected setting.</summary>
+        </member>
+        <member name="T:Shared.PresetSaveBulkMode">
+            <summary>Bulk choices offered by the standard personal-preset save dialog.</summary>
+        </member>
+        <member name="T:Shared.PresetSettingScope">
+            <summary>Describes the ownership of a persistent lobby setting.</summary>
+        </member>
+        <member name="T:Shared.ModSettingsPresetSourceKind">
+            <summary>Identifies who owns a preset and whether it may be replaced by the player.</summary>
+        </member>
+        <member name="T:Shared.PresetSettingDescriptor">
+            <summary>One persistent setting exposed to preset authoring UI.</summary>
+        </member>
+        <member name="T:Shared.PresetSaveSelection">
+            <summary>One author-selected setting passed to personal-preset persistence.</summary>
+        </member>
+        <member name="T:Shared.PresetSaveSettingViewModel">
+            <summary>Mutable row model used by the standard personal-preset save dialog.</summary>
+        </member>
+        <member name="T:Shared.PublishedPresetSetting">
+            <summary>One setting selected by a published preset.</summary>
+        </member>
+        <member name="T:Shared.PublishedModSettingsPreset">
+            <summary>A validated, provider-qualified preset offered by a target mod.</summary>
+        </member>
+        <member name="T:Shared.ModSettingsPresetListEntry">
+            <summary>One source-labelled row shown by the standard preset load/save dialogs.</summary>
+        </member>
+        <member name="T:Shared.ModSettingsPresetSaveTarget">
+            <summary>A deliberate destination offered by the personal-preset save dialog.</summary>
+        </member>
+        <member name="T:Shared.ModSettingsPresetJson">
+            <summary>Shared JSON contract for loose <c>preset_*.json</c> files.</summary>
+        </member>
     </members>
 </doc>

diff --git a/APIShared/BepInEx/plugins/APIShared_Serp/info.json b/APIShared/BepInEx/plugins/APIShared_Serp/info.json
index a5d6615c..2d7f0df4 100644
--- a/APIShared/BepInEx/plugins/APIShared_Serp/info.json
+++ b/APIShared/BepInEx/plugins/APIShared_Serp/info.json
@@ -2,14 +2,26 @@
   "GUID": "APIShared_Serp",
   "Author": "Serpens66",
   "Name": "APIShared",
-  "Description": "Shared typed and conflict-aware native patch and hook infrastructure for Serps mods.",
-  "Version": "0.3.7",
+  "Description": "Shared typed native infrastructure and extensible lobby ModSettings preset API for Serps and third-party mods.",
+  "Version": "0.4.1",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
-  "MinimumScriptExtenderVersion": "2.3.0",
+  "MinimumScriptExtenderVersion": "2.9.0",
   "MaximumScriptExtenderVersion": "",
   "Manifest": 1,
   "NetworkMode": 1,
   "SerpChangelog": [
+    {
+      "Version": "0.4.1",
+      "Changes": [
+        "Avoided constructing the game ViewModel during early mod-settings registration, restoring fast startup."
+      ]
+    },
+    {
+      "Version": "0.4.0",
+      "Changes": [
+        "Added the public extensible lobby ModSettings preset API, loose JSON discovery, typed ExtendedData integration, copy actions, and atomic preset export."
+      ]
+    },
     {
       "Version": "0.3.7",
       "Changes": [

diff --git a/APIShared/build.bat b/APIShared/build.bat
index c150197c..5bda2163 100644
--- a/APIShared/build.bat
+++ b/APIShared/build.bat
@@ -34,6 +34,8 @@ if errorlevel 1 goto build_failed_popd
 if not "%ERRORLEVEL%"=="0" goto build_failed_popd
 "%MSBUILD%" APIShared.csproj /p:Configuration=Debug /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
 if errorlevel 1 goto build_failed_popd
+"%MSBUILD%" "%PROJECT_DIR%..\_inspect\APISharedPresetConsumerTests\APISharedPresetConsumerTests.csproj" /t:Rebuild /p:Configuration=Release /p:GameDir="%GAME_DIR%" /p:ExtenderDir="%EXTENDER_DIR%"
+if errorlevel 1 goto build_failed_popd
 popd
 copy /Y "%PROJECT_DIR%info.json" "%LOCAL_PLUGIN_DIR%\info.json" >nul
 xcopy "%PROJECT_DIR%Patches" "%LOCAL_PLUGIN_DIR%\Patches\" /E /I /Q /Y >nul

diff --git a/APIShared/info.json b/APIShared/info.json
index a5d6615c..2d7f0df4 100644
--- a/APIShared/info.json
+++ b/APIShared/info.json
@@ -2,14 +2,26 @@
   "GUID": "APIShared_Serp",
   "Author": "Serpens66",
   "Name": "APIShared",
-  "Description": "Shared typed and conflict-aware native patch and hook infrastructure for Serps mods.",
-  "Version": "0.3.7",
+  "Description": "Shared typed native infrastructure and extensible lobby ModSettings preset API for Serps and third-party mods.",
+  "Version": "0.4.1",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
-  "MinimumScriptExtenderVersion": "2.3.0",
+  "MinimumScriptExtenderVersion": "2.9.0",
   "MaximumScriptExtenderVersion": "",
   "Manifest": 1,
   "NetworkMode": 1,
   "SerpChangelog": [
+    {
+      "Version": "0.4.1",
+      "Changes": [
+        "Avoided constructing the game ViewModel during early mod-settings registration, restoring fast startup."
+      ]
+    },
+    {
+      "Version": "0.4.0",
+      "Changes": [
+        "Added the public extensible lobby ModSettings preset API, loose JSON discovery, typed ExtendedData integration, copy actions, and atomic preset export."
+      ]
+    },
     {
       "Version": "0.3.7",
       "Changes": [

diff --git a/APIShared/src/APISharedPlugin.cs b/APIShared/src/APISharedPlugin.cs
index 1c6bfbd2..ce2763f2 100644
--- a/APIShared/src/APISharedPlugin.cs
+++ b/APIShared/src/APISharedPlugin.cs
@@ -7,7 +7,7 @@ using System.Security.Cryptography;
 namespace APIShared
 {
     /// <summary>BepInEx host for the process-wide APIShared.</summary>
-    [BepInDependency(ScriptExtenderGuid, "2.3.0")]
+    [BepInDependency(ScriptExtenderGuid, "2.9.0")]
     [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
     public sealed class APISharedPlugin : BaseUnityPlugin
     {
@@ -17,7 +17,7 @@ namespace APIShared
         /// <summary>Display name of the API plugin.</summary>
         public const string PluginName = "APIShared";
         /// <summary>Current API plugin version.</summary>
-        public const string PluginVersion = "0.3.7";
+        public const string PluginVersion = "0.4.1";
 
         private void Awake()
         {

diff --git a/APIShared/src/ApiSharedRuntime.cs b/APIShared/src/ApiSharedRuntime.cs
index d7f55ebc..153d417f 100644
--- a/APIShared/src/ApiSharedRuntime.cs
+++ b/APIShared/src/ApiSharedRuntime.cs
@@ -21,6 +21,7 @@ namespace APIShared
         private LobbyStateService lobbyState;
         private PlayerDefeatService playerDefeat;
         private MissionLifecycleService missionLifecycle;
+        private BriefingGoldPresentationService briefingGoldPresentation;
         private NativeCapabilityDiagnostic missionLifecycleDiagnostic = Pending(NativeCapabilityIds.MissionLifecycle);
         private NativeCapabilityDiagnostic gatehouseDistanceOriginDiagnostic = Pending(NativeCapabilityIds.GatehouseDistanceOrigin);
         private NativeCapabilityDiagnostic gatehouseDiagnostic = Pending(NativeCapabilityIds.GatehouseTiming);
@@ -28,6 +29,7 @@ namespace APIShared
         private NativeCapabilityDiagnostic aivBuildStepDiagnostic = Pending(NativeCapabilityIds.AivBuildStep);
         private NativeCapabilityDiagnostic lobbyStateDiagnostic = Pending(NativeCapabilityIds.LobbyState);
         private NativeCapabilityDiagnostic playerDefeatDiagnostic = Pending(NativeCapabilityIds.PlayerDefeat);
+        private NativeCapabilityDiagnostic briefingGoldDiagnostic = Pending(NativeCapabilityIds.BriefingGoldPresentation);
         private ManualLogSource log;
 
         internal static ApiSharedRuntime ProcessInstance { get; } = new ApiSharedRuntime();
@@ -55,6 +57,11 @@ namespace APIShared
             {
                 if (missionLifecycleDiagnostic.State == NativeCapabilityState.Pending)
                     MissionLifecycleService.TryCreate(logger, out missionLifecycle, out missionLifecycleDiagnostic);
+                if (briefingGoldDiagnostic.State == NativeCapabilityState.Pending)
+                    BriefingGoldPresentationService.TryCreate(
+                        logger,
+                        out briefingGoldPresentation,
+                        out briefingGoldDiagnostic);
                 if (playerDefeatDiagnostic.State == NativeCapabilityState.Pending)
                     PlayerDefeatService.TryCreate(
                         logger,
@@ -128,6 +135,7 @@ namespace APIShared
                     gatehouseMutationSync,
                     log,
                     gateTarget ?? GatehouseBuildTarget.Supported,
+                    nativeRegion,
                     out gatehouseDistanceOrigin,
                     out gatehouseDistanceOriginDiagnostic,
                     out gatehouse,
@@ -176,7 +184,7 @@ namespace APIShared
                 callbacks = readyCallbacks.ToArray();
                 readyCallbacks.Clear();
             }
-            NativeApiLog.Info(log, $"APIShared initialized: state={terminalState}, build={binaryHash}, lobbyState={lobbyStateDiagnostic.State}, playerDefeat={playerDefeatDiagnostic.State}, gatehouseDistanceOrigin={gatehouseDistanceOriginDiagnostic.State}, gatehouseTiming={gatehouseDiagnostic.State}, unitHudPresentation={unitHudDiagnostic.State}, aivBuildStep={aivBuildStepDiagnostic.State}.");
+            NativeApiLog.Info(log, $"APIShared initialized: state={terminalState}, build={binaryHash}, lobbyState={lobbyStateDiagnostic.State}, playerDefeat={playerDefeatDiagnostic.State}, briefingGold={briefingGoldDiagnostic.State}, gatehouseDistanceOrigin={gatehouseDistanceOriginDiagnostic.State}, gatehouseTiming={gatehouseDiagnostic.State}, unitHudPresentation={unitHudDiagnostic.State}, aivBuildStep={aivBuildStepDiagnostic.State}.");
             foreach (Action<IApiShared> callback in callbacks)
             {
                 try { callback(this); }
@@ -337,6 +345,31 @@ namespace APIShared
             }
         }
 
+        public bool TryGetBriefingGoldPresentation(
+            string ownerGuid,
+            out IBriefingGoldPresentationCapability capability,
+            out NativeCapabilityDiagnostic diagnostic)
+        {
+            capability = null;
+            if (string.IsNullOrWhiteSpace(ownerGuid))
+            {
+                diagnostic = new NativeCapabilityDiagnostic(
+                    NativeCapabilityIds.BriefingGoldPresentation,
+                    NativeCapabilityState.ValidationFailed,
+                    string.Empty,
+                    "A non-empty BepInEx owner GUID is required.");
+                return false;
+            }
+            lock (sync)
+            {
+                diagnostic = briefingGoldDiagnostic;
+                if (briefingGoldPresentation == null)
+                    return false;
+                capability = briefingGoldPresentation.Bind(ownerGuid);
+                return true;
+            }
+        }
+
         private bool ValidateOwner(string ownerGuid, string capabilityId, out NativeCapabilityDiagnostic diagnostic)
         {
             lock (sync)

diff --git a/APIShared/src/BriefingGoldContracts.cs b/APIShared/src/BriefingGoldContracts.cs
new file mode 100644
index 00000000..d0553d73
--- /dev/null
+++ b/APIShared/src/BriefingGoldContracts.cs
@@ -0,0 +1,65 @@
+using System;
+
+namespace APIShared
+{
+    /// <summary>Fixed ordering stages for mission-briefing starting-gold adjustments.</summary>
+    public enum BriefingGoldAdjustmentStage
+    {
+        /// <summary>Corrections that make the displayed Vanilla base match Vanilla gameplay.</summary>
+        VanillaCorrection = 0,
+        /// <summary>Adjustments contributed by gameplay mods after the effective Vanilla base is known.</summary>
+        ModAdjustment = 100
+    }
+
+    /// <summary>Immutable state for one visible player slot in the mission briefing.</summary>
+    public sealed class BriefingGoldContext
+    {
+        /// <summary>Creates a mission-briefing starting-gold context.</summary>
+        public BriefingGoldContext(
+            int slotIndex,
+            bool isHuman,
+            int vanillaDisplayedGold,
+            int effectiveVanillaGold,
+            int currentGold,
+            bool hasNoStartingGoldState,
+            bool noStartingGoldEnabled)
+        {
+            SlotIndex = slotIndex;
+            IsHuman = isHuman;
+            VanillaDisplayedGold = vanillaDisplayedGold;
+            EffectiveVanillaGold = effectiveVanillaGold;
+            CurrentGold = currentGold;
+            HasNoStartingGoldState = hasNoStartingGoldState;
+            NoStartingGoldEnabled = noStartingGoldEnabled;
+        }
+
+        /// <summary>Zero-based slot in the eight-player briefing presentation.</summary>
+        public int SlotIndex { get; }
+        /// <summary>Whether Vanilla classified this slot as a human player.</summary>
+        public bool IsHuman { get; }
+        /// <summary>Gold written by Vanilla's briefing method before shared adjustments.</summary>
+        public int VanillaDisplayedGold { get; }
+        /// <summary>Vanilla's actual starting gold after its No Starting Gold rule.</summary>
+        public int EffectiveVanillaGold { get; }
+        /// <summary>Value produced by all earlier adjustment stages.</summary>
+        public int CurrentGold { get; }
+        /// <summary>Whether the current native No Starting Gold state was resolved safely.</summary>
+        public bool HasNoStartingGoldState { get; }
+        /// <summary>Whether Vanilla's effective No Starting Gold flag is enabled.</summary>
+        public bool NoStartingGoldEnabled { get; }
+    }
+
+    /// <summary>Returns the non-negative gold value to pass to later adjustments.</summary>
+    public delegate int BriefingGoldAdjuster(BriefingGoldContext context);
+
+    /// <summary>Owner-bound process-wide mission-briefing gold presentation service.</summary>
+    public interface IBriefingGoldPresentationCapability
+    {
+        /// <summary>Registers one deterministic process-lifetime adjustment.</summary>
+        bool TryRegisterAdjustment(
+            string registrationId,
+            BriefingGoldAdjustmentStage stage,
+            BriefingGoldAdjuster adjuster,
+            out NativeCapabilityDiagnostic diagnostic);
+    }
+}

diff --git a/APIShared/src/BriefingGoldPresentationCapability.cs b/APIShared/src/BriefingGoldPresentationCapability.cs
new file mode 100644
index 00000000..b7a2941c
--- /dev/null
+++ b/APIShared/src/BriefingGoldPresentationCapability.cs
@@ -0,0 +1,337 @@
+using BepInEx.Logging;
+using CrusaderDE;
+using MonoMod.RuntimeDetour;
+using SHCDESE.API;
+using System;
+using System.Collections.Generic;
+using System.Globalization;
+using System.Linq;
+using System.Reflection;
+
+namespace APIShared
+{
+    internal sealed unsafe class BriefingGoldPresentationService
+    {
+        private const int BriefingSlotCount = 8;
+        private delegate void ButtonGotoBriefingDelegate(MainViewModel self, object parameter);
+
+        private readonly object sync = new object();
+        private readonly List<Registration> registrations = new List<Registration>();
+        private readonly HashSet<string> loggedFailures = new HashSet<string>(StringComparer.Ordinal);
+        private readonly ManualLogSource log;
+        private Registration[] registrationView = Array.Empty<Registration>();
+        private Hook briefingHook;
+        private ButtonGotoBriefingDelegate briefingOriginal;
+
+        internal BriefingGoldPresentationService(ManualLogSource logger)
+        {
+            log = logger;
+        }
+
+        internal static bool TryCreate(
+            ManualLogSource log,
+            out BriefingGoldPresentationService service,
+            out NativeCapabilityDiagnostic diagnostic)
+        {
+            service = null;
+            Hook pending = null;
+            try
+            {
+                var candidate = new BriefingGoldPresentationService(log);
+                MethodInfo method = typeof(MainViewModel).GetMethod(
+                    nameof(MainViewModel.ButtonGotoBriefing),
+                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
+                    null,
+                    new[] { typeof(object) },
+                    null) ?? throw new MissingMethodException(
+                        typeof(MainViewModel).FullName,
+                        nameof(MainViewModel.ButtonGotoBriefing));
+
+                pending = new Hook(
+                    method,
+                    (ButtonGotoBriefingDelegate)candidate.ButtonGotoBriefingHook,
+                    new HookConfig
+                    {
+                        ManualApply = true,
+                        ID = "APIShared.BriefingGold.ButtonGotoBriefing"
+                    });
+                candidate.briefingOriginal = pending.GenerateTrampoline<ButtonGotoBriefingDelegate>();
+                pending.Apply();
+                candidate.briefingHook = pending;
+                pending = null;
+
+                service = candidate;
+                diagnostic = candidate.Available(
+                    "Process-wide post-Vanilla mission-briefing gold presentation is active.");
+                TryLogInfo(log, "Process-wide briefing-gold presentation installed.");
+                return true;
+            }
+            catch (Exception ex)
+            {
+                try { pending?.Undo(); } catch { }
+                try { pending?.Dispose(); } catch { }
+                diagnostic = new NativeCapabilityDiagnostic(
+                    NativeCapabilityIds.BriefingGoldPresentation,
+                    NativeCapabilityState.Faulted,
+                    string.Empty,
+                    ex.Message);
+                TryLogError(log, $"Briefing-gold presentation failed before publication: {ex}");
+                return false;
+            }
+        }
+
+        internal IBriefingGoldPresentationCapability Bind(string ownerGuid) =>
+            new Binding(this, ownerGuid);
+
+        private bool Register(
+            string owner,
+            string registrationId,
+            BriefingGoldAdjustmentStage stage,
+            BriefingGoldAdjuster adjuster,
+            out NativeCapabilityDiagnostic diagnostic)
+        {
+            if (string.IsNullOrWhiteSpace(registrationId) ||
+                adjuster == null ||
+                !Enum.IsDefined(typeof(BriefingGoldAdjustmentStage), stage))
+            {
+                diagnostic = new NativeCapabilityDiagnostic(
+                    NativeCapabilityIds.BriefingGoldPresentation,
+                    NativeCapabilityState.ValidationFailed,
+                    string.Empty,
+                    "A registration ID, supported stage and adjuster are required.");
+                return false;
+            }
+
+            lock (sync)
+            {
+                if (registrations.Any(entry =>
+                    string.Equals(entry.Owner, owner, StringComparison.Ordinal) &&
+                    string.Equals(entry.Id, registrationId, StringComparison.Ordinal)))
+                {
+                    diagnostic = new NativeCapabilityDiagnostic(
+                        NativeCapabilityIds.BriefingGoldPresentation,
+                        NativeCapabilityState.Conflict,
+                        string.Empty,
+                        "The owner already registered this briefing-gold adjustment ID.",
+                        owner);
+                    return false;
+                }
+
+                registrations.Add(new Registration(owner, registrationId, stage, adjuster));
+                registrations.Sort(Compare);
+                registrationView = registrations.ToArray();
+            }
+
+            diagnostic = Available("Briefing-gold adjustment registered for the process lifetime.");
+            return true;
+        }
+
+        private void ButtonGotoBriefingHook(MainViewModel self, object parameter)
+        {
+            briefingOriginal(self, parameter);
+            try
+            {
+                ApplyToVisibleSlots(self);
+            }
+            catch (Exception ex)
+            {
+                LogFailure("presentation", ex);
+            }
+        }
+
+        private void ApplyToVisibleSlots(MainViewModel self)
+        {
+            Registration[] view = registrationView;
+            if (view.Length == 0 || self == null)
+                return;
+
+            bool hasNoGoldState = TryReadNoStartingGold(out bool noGoldEnabled);
+            int count = Math.Min(
+                BriefingSlotCount,
+                Math.Min(
+                    self.SkirmishBriefingGold?.Count ?? 0,
+                    Math.Min(
+                        self.SkirmishBriefingAlly?.Count ?? 0,
+                        self.AlliesHumanFaceVis?.Count ?? 0)));
+
+            for (int slotIndex = 0; slotIndex < count; slotIndex++)
+            {
+                if (!self.SkirmishBriefingAlly[slotIndex])
+                    continue;
+                if (!int.TryParse(
+                    self.SkirmishBriefingGold[slotIndex],
+                    NumberStyles.Integer,
+                    CultureInfo.InvariantCulture,
+                    out int vanillaGold) || vanillaGold < 0)
+                {
+                    LogFailure(
+                        "slot-" + slotIndex,
+                        new FormatException("Vanilla briefing gold is not a non-negative integer."));
+                    continue;
+                }
+
+                bool isHuman = self.AlliesHumanFaceVis[slotIndex];
+                int adjusted = Evaluate(
+                    view,
+                    slotIndex,
+                    isHuman,
+                    vanillaGold,
+                    hasNoGoldState,
+                    noGoldEnabled);
+                if (adjusted != vanillaGold)
+                    self.SkirmishBriefingGold[slotIndex] =
+                        adjusted.ToString(CultureInfo.InvariantCulture);
+            }
+        }
+
+        internal int EvaluateForTests(
+            int slotIndex,
+            bool isHuman,
+            int vanillaGold,
+            bool hasNoGoldState,
+            bool noGoldEnabled) =>
+            Evaluate(
+                registrationView,
+                slotIndex,
+                isHuman,
+                vanillaGold,
+                hasNoGoldState,
+                noGoldEnabled);
+
+        private int Evaluate(
+            Registration[] view,
+            int slotIndex,
+            bool isHuman,
+            int vanillaGold,
+            bool hasNoGoldState,
+            bool noGoldEnabled)
+        {
+            int effectiveVanillaGold =
+                hasNoGoldState && noGoldEnabled && isHuman ? 0 : vanillaGold;
+            int currentGold = vanillaGold;
+            for (int index = 0; index < view.Length; index++)
+            {
+                Registration registration = view[index];
+                var context = new BriefingGoldContext(
+                    slotIndex,
+                    isHuman,
+                    vanillaGold,
+                    effectiveVanillaGold,
+                    currentGold,
+                    hasNoGoldState,
+                    noGoldEnabled);
+                try
+                {
+                    int candidate = registration.Adjuster(context);
+                    if (candidate < 0)
+                    {
+                        LogFailure(
+                            registration.Key,
+                            new InvalidOperationException(
+                                "A briefing-gold adjustment returned a negative value."));
+                        continue;
+                    }
+                    currentGold = candidate;
+                }
+                catch (Exception ex)
+                {
+                    LogFailure(registration.Key, ex);
+                }
+            }
+            return currentGold;
+        }
+
+        private static bool TryReadNoStartingGold(out bool enabled)
+        {
+            enabled = false;
+            try
+            {
+                var options = GamePlayerManagerAPI.Instance._choreManagerOptionsInternal;
+                if (!options.IsValid())
+                    return false;
+                enabled = options.AdvOpt_NoGold > 0;
+                return true;
+            }
+            catch
+            {
+                return false;
+            }
+        }
+
+        private void LogFailure(string area, Exception ex)
+        {
+            lock (sync)
+                if (!loggedFailures.Add(area))
+                    return;
+            TryLogError(
+                log,
+                $"Briefing-gold {area} failed closed; the last safe value remains active: {ex}");
+        }
+
+        private NativeCapabilityDiagnostic Available(string reason) =>
+            new NativeCapabilityDiagnostic(
+                NativeCapabilityIds.BriefingGoldPresentation,
+                NativeCapabilityState.Available,
+                string.Empty,
+                reason);
+
+        private static int Compare(Registration left, Registration right)
+        {
+            int result = left.Stage.CompareTo(right.Stage);
+            if (result != 0)
+                return result;
+            result = string.CompareOrdinal(left.Owner, right.Owner);
+            return result != 0 ? result : string.CompareOrdinal(left.Id, right.Id);
+        }
+
+        private static void TryLogInfo(ManualLogSource log, string message)
+        {
+            try { NativeApiLog.Info(log, message); } catch { }
+        }
+
+        private static void TryLogError(ManualLogSource log, string message)
+        {
+            try { NativeApiLog.Error(log, message); } catch { }
+        }
+
+        private sealed class Binding : IBriefingGoldPresentationCapability
+        {
+            private readonly BriefingGoldPresentationService service;
+            private readonly string owner;
+
+            internal Binding(BriefingGoldPresentationService service, string owner)
+            {
+                this.service = service;
+                this.owner = owner;
+            }
+
+            public bool TryRegisterAdjustment(
+                string registrationId,
+                BriefingGoldAdjustmentStage stage,
+                BriefingGoldAdjuster adjuster,
+                out NativeCapabilityDiagnostic diagnostic) =>
+                service.Register(owner, registrationId, stage, adjuster, out diagnostic);
+        }
+
+        private sealed class Registration
+        {
+            internal Registration(
+                string owner,
+                string id,
+                BriefingGoldAdjustmentStage stage,
+                BriefingGoldAdjuster adjuster)
+            {
+                Owner = owner;
+                Id = id;
+                Stage = stage;
+                Adjuster = adjuster;
+            }
+
+            internal string Owner { get; }
+            internal string Id { get; }
+            internal BriefingGoldAdjustmentStage Stage { get; }
+            internal BriefingGoldAdjuster Adjuster { get; }
+            internal string Key => Owner + ":" + Id;
+        }
+    }
+}

diff --git a/APIShared/src/Contracts.cs b/APIShared/src/Contracts.cs
index 96359da4..3a05b121 100644
--- a/APIShared/src/Contracts.cs
+++ b/APIShared/src/Contracts.cs
@@ -51,6 +51,8 @@ namespace APIShared
         public const string LobbyState = "lobby-state";
         /// <summary>Capability for observing player lord deaths and official defeat transitions.</summary>
         public const string PlayerDefeat = "player-defeat";
+        /// <summary>Capability for deterministic post-Vanilla mission-briefing gold presentation.</summary>
+        public const string BriefingGoldPresentation = "briefing-gold-presentation";
     }
 
     /// <summary>Immutable diagnostic information returned by capability acquisition and mutation.</summary>
@@ -121,6 +123,11 @@ namespace APIShared
             string ownerGuid,
             out IPlayerDefeatCapability capability,
             out NativeCapabilityDiagnostic diagnostic);
+        /// <summary>Attempts to acquire the process-wide mission-briefing gold presentation capability.</summary>
+        bool TryGetBriefingGoldPresentation(
+            string ownerGuid,
+            out IBriefingGoldPresentationCapability capability,
+            out NativeCapabilityDiagnostic diagnostic);
     }
 
     /// <summary>Static access to the process-wide API and its readiness notification.</summary>

diff --git a/APIShared/src/ElevatedMoatAiState.cs b/APIShared/src/ElevatedMoatAiState.cs
new file mode 100644
index 00000000..1a013c6b
--- /dev/null
+++ b/APIShared/src/ElevatedMoatAiState.cs
@@ -0,0 +1,49 @@
+using System;
+using System.Threading;
+
+namespace APIShared
+{
+    /// <summary>Effective AI elevated-moat construction state published by ExtraFeatures.</summary>
+    public enum ElevatedMoatAiState
+    {
+        /// <summary>No effective runtime state has been established.</summary>
+        Unknown = 0,
+        /// <summary>Elevated AI construction is not enabled by ExtraFeatures.</summary>
+        Disabled = 1,
+        /// <summary>The ExtraFeatures hook is installed and enabled for AI players.</summary>
+        Enabled = 2
+    }
+
+    /// <summary>Process-wide publication of the effective elevated AI build state.</summary>
+    public static class ElevatedMoatAiCapability
+    {
+        private static int current;
+
+        /// <summary>Latest state; unknown until ExtraFeatures publishes a verified state.</summary>
+        public static ElevatedMoatAiState Current =>
+            (ElevatedMoatAiState)Volatile.Read(ref current);
+
+        // Subscribers are process-lifetime consumers. The publisher never removes a runtime hook.
+        /// <summary>Raised when the effective state changes.</summary>
+        public static event Action<ElevatedMoatAiState> Changed;
+
+        /// <summary>Publish the state after reconciling the native patch and AI setting.</summary>
+        public static void Publish(ElevatedMoatAiState state)
+        {
+            if (state < ElevatedMoatAiState.Unknown || state > ElevatedMoatAiState.Enabled)
+                throw new ArgumentOutOfRangeException(nameof(state));
+            int previous = Interlocked.Exchange(ref current, (int)state);
+            if (previous != (int)state)
+            {
+                Action<ElevatedMoatAiState> observers = Changed;
+                if (observers == null)
+                    return;
+                foreach (Action<ElevatedMoatAiState> observer in observers.GetInvocationList())
+                {
+                    try { observer(state); }
+                    catch { /* A diagnostic consumer cannot change the native feature state. */ }
+                }
+            }
+        }
+    }
+}

diff --git a/APIShared/src/GameplayFeatureModePolicy.cs b/APIShared/src/GameplayFeatureModePolicy.cs
index 7b49a6a5..34de4b17 100644
--- a/APIShared/src/GameplayFeatureModePolicy.cs
+++ b/APIShared/src/GameplayFeatureModePolicy.cs
@@ -249,7 +249,7 @@ namespace Shared
             }
         }
 
-#if SHARED_PRESET_TESTS
+#if API_SHARED_PRESET_TESTS
         /// <summary>RecordDecisionForTests in the centralized mission policy contract.</summary>
         public static bool RecordDecisionForTests(GameplayFeatureId featureId, bool allowed) =>
             RecordDecision(featureId, allowed);

diff --git a/APIShared/src/GatehouseDistanceOriginCapability.cs b/APIShared/src/GatehouseDistanceOriginCapability.cs
index 95e8c48e..0f88cd08 100644
--- a/APIShared/src/GatehouseDistanceOriginCapability.cs
+++ b/APIShared/src/GatehouseDistanceOriginCapability.cs
@@ -46,7 +46,7 @@ namespace APIShared
     {
         private readonly string binaryHash;
         private readonly GatehouseDistanceOriginTarget target;
-        private readonly INativeMemory memory;
+        private readonly GatehousePermanentRuntimeState runtimeState;
         private readonly NativeOwnershipRegistry ownership;
         private readonly object mutationSync;
         private readonly ManualLogSource log;
@@ -55,14 +55,14 @@ namespace APIShared
         public GatehouseDistanceOriginService(
             string binaryHash,
             GatehouseDistanceOriginTarget target,
-            INativeMemory memory,
+            GatehousePermanentRuntimeState runtimeState,
             NativeOwnershipRegistry ownership,
             object mutationSync,
             ManualLogSource log)
         {
             this.binaryHash = binaryHash;
             this.target = target ?? throw new ArgumentNullException(nameof(target));
-            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
+            this.runtimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
             this.ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
             this.mutationSync = mutationSync ?? throw new ArgumentNullException(nameof(mutationSync));
             this.log = log;
@@ -114,23 +114,7 @@ namespace APIShared
                         return true;
                     }
 
-                    byte[] oldBytes = BytesFor(expectedOrigin);
-                    byte[] desiredBytes = BytesFor(origin);
-                    GatehouseNativeMutation.Execute(
-                        memory,
-                        target.Intervals,
-                        VerifyExpected,
-                        () =>
-                        {
-                            WriteBytes(desiredBytes);
-                            VerifyBytes(desiredBytes, "requested distance-origin block");
-                        },
-                        () =>
-                        {
-                            WriteBytes(oldBytes);
-                            VerifyBytes(oldBytes, "rolled-back distance-origin block");
-                        });
-
+                    runtimeState.PublishOrigin(origin);
                     expectedOrigin = origin;
                     VerifyExpected();
                     diagnostic = Diagnostic(
@@ -152,29 +136,11 @@ namespace APIShared
             }
         }
 
-        private byte[] BytesFor(GatehouseDistanceOrigin origin) =>
-            origin == GatehouseDistanceOrigin.BuildingBoundsCenter ? target.CenteredBytes : target.VanillaBytes;
-
-        private void VerifyExpected() =>
-            VerifyBytes(BytesFor(expectedOrigin), expectedOrigin == GatehouseDistanceOrigin.BuildingBoundsCenter
-                ? "centered distance-origin block"
-                : "Vanilla distance-origin block");
-
-        private void WriteBytes(byte[] values)
+        private void VerifyExpected()
         {
-            for (int index = 0; index < values.Length; index++)
-                memory.WriteByte(target.Block + index, values[index]);
-        }
-
-        private void VerifyBytes(byte[] expected, string name)
-        {
-            for (int index = 0; index < expected.Length; index++)
-            {
-                byte actual = memory.ReadByte(target.Block + index);
-                if (actual != expected[index])
-                    throw new InvalidOperationException(
-                        $"Gatehouse {name} changed unexpectedly at +0x{index:X}: expected=0x{expected[index]:X2}, actual=0x{actual:X2}.");
-            }
+            if (!runtimeState.IsDistanceInstalled || runtimeState.Origin != expectedOrigin)
+                throw new InvalidOperationException(
+                    $"Gatehouse distance-origin logical state changed unexpectedly: expected={expectedOrigin}, actual={runtimeState.Origin}.");
         }
 
         private NativeCapabilityDiagnostic Diagnostic(NativeCapabilityState state, string reason) =>
@@ -209,128 +175,4 @@ namespace APIShared
         }
     }
 
-    internal static class GatehouseNativeMutation
-    {
-        public static void Execute(
-            INativeMemory memory,
-            IReadOnlyList<NativeInterval> intervals,
-            Action verifyExpected,
-            Action writeAndVerify,
-            Action rollbackAndVerify)
-        {
-            if (memory == null)
-                throw new ArgumentNullException(nameof(memory));
-            if (intervals == null || intervals.Count == 0)
-                throw new ArgumentException("At least one native interval is required.", nameof(intervals));
-            if (verifyExpected == null)
-                throw new ArgumentNullException(nameof(verifyExpected));
-            if (writeAndVerify == null)
-                throw new ArgumentNullException(nameof(writeAndVerify));
-            if (rollbackAndVerify == null)
-                throw new ArgumentNullException(nameof(rollbackAndVerify));
-
-            List<PageProtection> protections = AcquireWritablePages(memory, intervals);
-            Exception primary = null;
-            Exception cleanup = null;
-            bool writesStarted = false;
-            try
-            {
-                try
-                {
-                    // Close the race between the public preflight and acquiring page write access.
-                    verifyExpected();
-                    writesStarted = true;
-                    writeAndVerify();
-                }
-                catch (Exception ex)
-                {
-                    primary = ex;
-                    if (writesStarted)
-                    {
-                        try { rollbackAndVerify(); }
-                        catch (Exception rollback)
-                        {
-                            primary = new AggregateException("The native write and rollback both failed.", primary, rollback);
-                        }
-                    }
-                }
-            }
-            finally
-            {
-                for (int index = protections.Count - 1; index >= 0; index--)
-                {
-                    PageProtection protection = protections[index];
-                    try { memory.RestoreProtection(protection.Address, memory.PageSize, protection.Protection); }
-                    catch (Exception ex) { cleanup = Combine(cleanup, ex); }
-                }
-                foreach (NativeInterval interval in intervals)
-                {
-                    try { memory.Flush(interval.Start, checked((int)(interval.End - interval.Start))); }
-                    catch (Exception ex) { cleanup = Combine(cleanup, ex); }
-                }
-            }
-
-            if (primary != null && cleanup != null)
-                throw new AggregateException("The gatehouse transaction and cleanup both failed.", primary, cleanup);
-            if (primary != null)
-                throw primary;
-            if (cleanup != null)
-                throw cleanup;
-        }
-
-        private static List<PageProtection> AcquireWritablePages(
-            INativeMemory memory,
-            IReadOnlyList<NativeInterval> intervals)
-        {
-            if (memory.PageSize <= 0)
-                throw new InvalidOperationException("The native memory adapter returned an invalid page size.");
-
-            var pages = new SortedSet<long>();
-            foreach (NativeInterval interval in intervals)
-            {
-                long firstPage = PageStart(interval.Start, memory.PageSize);
-                long lastPage = PageStart(interval.End - 1, memory.PageSize);
-                for (long page = firstPage; page <= lastPage; page = checked(page + memory.PageSize))
-                    pages.Add(page);
-            }
-
-            var protections = new List<PageProtection>();
-            try
-            {
-                foreach (long page in pages)
-                    protections.Add(new PageProtection(page, memory.MakeWritable(page, memory.PageSize)));
-                return protections;
-            }
-            catch (Exception primary)
-            {
-                Exception cleanup = null;
-                for (int index = protections.Count - 1; index >= 0; index--)
-                {
-                    PageProtection protection = protections[index];
-                    try { memory.RestoreProtection(protection.Address, memory.PageSize, protection.Protection); }
-                    catch (Exception ex) { cleanup = Combine(cleanup, ex); }
-                }
-                if (cleanup != null)
-                    throw new AggregateException("Acquiring writable native pages and cleanup both failed.", primary, cleanup);
-                throw;
-            }
-        }
-
-        private static long PageStart(long address, int pageSize) => address - address % pageSize;
-
-        private static Exception Combine(Exception current, Exception next) =>
-            current == null ? next : new AggregateException(current, next);
-
-        private readonly struct PageProtection
-        {
-            public PageProtection(long address, uint protection)
-            {
-                Address = address;
-                Protection = protection;
-            }
-
-            public long Address { get; }
-            public uint Protection { get; }
-        }
-    }
 }

diff --git a/APIShared/src/GatehousePermanentRuntimeState.cs b/APIShared/src/GatehousePermanentRuntimeState.cs
new file mode 100644
index 00000000..87986f46
--- /dev/null
+++ b/APIShared/src/GatehousePermanentRuntimeState.cs
@@ -0,0 +1,288 @@
+using Iced.Intel;
+using RedBird.Abstractions.Hooks;
+using RedBird.Abstractions.Hooks.Transaction;
+using RedBird.Core.Memory;
+using RedBird.X64.Extensions;
+using RedBird.X64.Hooks;
+using RedBird.X64.Hooks.Transaction;
+using System;
+using System.Collections.Generic;
+using System.Runtime.InteropServices;
+using System.Threading;
+using static Iced.Intel.AssemblerRegisters;
+
+namespace APIShared
+{
+    internal sealed class GatehousePermanentRuntimeState
+    {
+        internal const int DistanceHookRva = 0xB7B70;
+        internal const int DistanceDisplacedBytes = 75;
+        internal const int DecisionHookRva = 0xB7BBB;
+        internal const int DecisionDisplacedBytes = 37;
+        internal const int DecisionReturnRva = 0xB7BE0;
+        internal const int ClosePathRva = 0xB7C39;
+
+        private const int UnitXOffset = 0x67E8B0E;
+        private const int UnitYOffset = 0x67E8B10;
+        private const int BuildingEndXOffset = 0x64CCD0A;
+        private const int BuildingEndYOffset = 0x64CCD0C;
+
+        private readonly IntPtr originFlag = Marshal.AllocHGlobal(sizeof(int));
+        private readonly IntPtr publishedTiming = Marshal.AllocHGlobal(IntPtr.Size);
+        private readonly object publicationSync = new object();
+        private readonly List<IntPtr> timingSnapshots = new List<IntPtr>();
+        private HookTransaction transaction;
+        private readonly HookHandle<X64InlineHook> distanceHook = new HookHandle<X64InlineHook>();
+        private readonly HookHandle<X64InlineHook> decisionHook = new HookHandle<X64InlineHook>();
+        private readonly bool testOnly;
+
+        private GatehousePermanentRuntimeState()
+        {
+            testOnly = true;
+            InitializeState();
+        }
+
+        internal GatehousePermanentRuntimeState(
+            ScanRegion region,
+            ulong moduleBase,
+            bool installDistance,
+            bool installTiming)
+        {
+            if (region == null) throw new ArgumentNullException(nameof(region));
+            if (moduleBase == 0) throw new ArgumentOutOfRangeException(nameof(moduleBase));
+            if (!installDistance && !installTiming)
+                throw new ArgumentException("At least one gatehouse hook must be requested.");
+            InitializeState();
+            try
+            {
+                transaction = new HookTransaction(
+                    region,
+                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
+                    new HookTransactionOptions
+                    {
+                        FailureMode = TransactionFailureMode.RollbackAndThrow,
+                        OwnsHooks = true
+                    });
+                ulong originAddress = unchecked((ulong)originFlag.ToInt64());
+                ulong timingPointerAddress = unchecked((ulong)publishedTiming.ToInt64());
+                if (installDistance)
+                {
+                    transaction.AddInline(
+                    distanceHook,
+                    HookTarget.FromAddress(moduleBase + DistanceHookRva),
+                    (assembler, instructions, returnAddress) =>
+                            GenerateDistanceOrigin(assembler, instructions.ToArray(), originAddress),
+                        hookSize: DistanceDisplacedBytes);
+                }
+                if (installTiming)
+                {
+                    transaction.AddInline(
+                        decisionHook,
+                        HookTarget.FromAddress(moduleBase + DecisionHookRva),
+                        (assembler, instructions, returnAddress) =>
+                            GenerateDecision(
+                                assembler,
+                                instructions.ToArray(),
+                                timingPointerAddress,
+                                moduleBase,
+                                moduleBase + DecisionReturnRva,
+                                moduleBase + ClosePathRva),
+                        hookSize: DecisionDisplacedBytes);
+                }
+                CommitResult result = transaction.Commit();
+                bool distanceValid = !installDistance ||
+                    (distanceHook.Success && distanceHook.IsInstalled &&
+                     distanceHook.Hook.DisplacedByteCount == DistanceDisplacedBytes);
+                bool timingValid = !installTiming ||
+                    (decisionHook.Success && decisionHook.IsInstalled &&
+                     decisionHook.Hook.DisplacedByteCount == DecisionDisplacedBytes);
+                if (!result.IsCompleteSuccess || !distanceValid || !timingValid)
+                {
+                    throw new InvalidOperationException(
+                        $"Gatehouse permanent hooks failed validation: result={result}, " +
+                        $"distance={distanceHook.Hook?.DisplacedByteCount}, decision={decisionHook.Hook?.DisplacedByteCount}.");
+                }
+            }
+            catch
+            {
+                transaction?.Dispose();
+                FreeUnpublishedState();
+                throw;
+            }
+        }
+
+        internal static GatehousePermanentRuntimeState CreateTestState(
+            bool distanceInstalled = true,
+            bool timingInstalled = true)
+        {
+            var state = new GatehousePermanentRuntimeState();
+            state.testDistanceInstalled = distanceInstalled;
+            state.testTimingInstalled = timingInstalled;
+            return state;
+        }
+
+        private bool testDistanceInstalled = true;
+        private bool testTimingInstalled = true;
+
+        internal bool IsDistanceInstalled => testOnly
+            ? testDistanceInstalled
+            : distanceHook.Success && distanceHook.IsInstalled;
+
+        internal bool IsTimingInstalled => testOnly
+            ? testTimingInstalled
+            : decisionHook.Success && decisionHook.IsInstalled;
+
+        internal GatehouseDistanceOrigin Origin =>
+            Marshal.ReadInt32(originFlag) == 0
+                ? GatehouseDistanceOrigin.VanillaBuildingBegin
+                : GatehouseDistanceOrigin.BuildingBoundsCenter;
+
+        internal void PublishOrigin(GatehouseDistanceOrigin origin)
+        {
+            if (!IsDistanceInstalled)
+                throw new InvalidOperationException("The permanent gatehouse distance hook is unavailable.");
+            Thread.MemoryBarrier();
+            Marshal.WriteInt32(originFlag,
+                origin == GatehouseDistanceOrigin.BuildingBoundsCenter ? 1 : 0);
+            Thread.MemoryBarrier();
+        }
+
+        internal void PublishTiming(int aiDistance, int aiDelay, int humanDistance, int humanDelay)
+        {
+            if (!IsTimingInstalled)
+                throw new InvalidOperationException("The permanent gatehouse timing hook is unavailable.");
+            IntPtr snapshot = AllocateTiming(aiDistance, aiDelay, humanDistance, humanDelay);
+            lock (publicationSync)
+            {
+                timingSnapshots.Add(snapshot);
+                Thread.MemoryBarrier();
+                Marshal.WriteIntPtr(publishedTiming, snapshot);
+                Thread.MemoryBarrier();
+            }
+        }
+
+        internal void ReadTiming(out int aiDistance, out int aiDelay, out int humanDistance, out int humanDelay)
+        {
+            IntPtr current = Marshal.ReadIntPtr(publishedTiming);
+            Thread.MemoryBarrier();
+            aiDistance = Marshal.ReadInt32(current, 0);
+            aiDelay = Marshal.ReadInt32(current, 4);
+            humanDistance = Marshal.ReadInt32(current, 8);
+            humanDelay = Marshal.ReadInt32(current, 12);
+        }
+
+        private void InitializeState()
+        {
+            Marshal.WriteInt32(originFlag, 0);
+            IntPtr vanilla = AllocateTiming(
+                GatehouseTimingTarget.VanillaAiDistance,
+                GatehouseTimingTarget.VanillaAiDelay,
+                GatehouseTimingTarget.VanillaHumanDistance,
+                GatehouseTimingTarget.VanillaHumanDelay);
+            timingSnapshots.Add(vanilla);
+            Marshal.WriteIntPtr(publishedTiming, vanilla);
+        }
+
+        private static IntPtr AllocateTiming(
+            int aiDistance,
+            int aiDelay,
+            int humanDistance,
+            int humanDelay)
+        {
+            IntPtr target = Marshal.AllocHGlobal(4 * sizeof(int));
+            Marshal.WriteInt32(target, 0, aiDistance);
+            Marshal.WriteInt32(target, 4, aiDelay);
+            Marshal.WriteInt32(target, 8, humanDistance);
+            Marshal.WriteInt32(target, 12, humanDelay);
+            return target;
+        }
+
+        private void FreeUnpublishedState()
+        {
+            Marshal.FreeHGlobal(originFlag);
+            foreach (IntPtr snapshot in timingSnapshots)
+                Marshal.FreeHGlobal(snapshot);
+            Marshal.FreeHGlobal(publishedTiming);
+        }
+
+        internal static void GenerateDistanceOrigin(
+            Assembler assembler,
+            Instruction[] instructions,
+            ulong originAddress)
+        {
+            if (instructions.Length == 0)
+                throw new InvalidOperationException("The gatehouse distance hook displaced no instructions.");
+            Label vanilla = assembler.CreateLabel("gatehouseDistanceVanilla");
+            Label done = assembler.CreateLabel("gatehouseDistanceDone");
+            assembler.pushfq();
+            assembler.push(rax);
+            assembler.mov(rax, originAddress);
+            assembler.cmp(__dword_ptr[rax], 0);
+            assembler.je(vanilla);
+            assembler.pop(rax);
+            assembler.popfq();
+
+            assembler.movsx(r8d, __word_ptr[rdx + rbp + UnitXOffset]);
+            assembler.movsx(ecx, __word_ptr[rdx + rbp + UnitYOffset]);
+            assembler.movsx(eax, __word_ptr[rbx + rbp + BuildingEndXOffset]);
+            assembler.add(eax, r15d);
+            assembler.shl(eax, 2);
+            assembler.sub(eax, r8d);
+            assembler.cdq();
+            assembler.xor(eax, edx);
+            assembler.sub(eax, edx);
+            assembler.mov(r8d, eax);
+            assembler.movsx(eax, __word_ptr[rbx + rbp + BuildingEndYOffset]);
+            assembler.add(eax, r12d);
+            assembler.shl(eax, 2);
+            assembler.sub(eax, ecx);
+            assembler.cdq();
+            assembler.xor(eax, edx);
+            assembler.sub(eax, edx);
+            assembler.cmp(r8d, eax);
+            assembler.cmovl(r8d, eax);
+            assembler.jmp(done);
+
+            assembler.Label(ref vanilla);
+            assembler.pop(rax);
+            assembler.popfq();
+            foreach (Instruction instruction in instructions)
+                assembler.AddInstruction(instruction);
+            assembler.Label(ref done);
+            assembler.nop();
+        }
+
+        internal static void GenerateDecision(
+            Assembler assembler,
+            Instruction[] instructions,
+            ulong timingPointerAddress,
+            ulong moduleBase,
+            ulong returnAddress,
+            ulong closePathAddress)
+        {
+            if (instructions.Length == 0)
+                throw new InvalidOperationException("The gatehouse decision hook displaced no instructions.");
+            Label human = assembler.CreateLabel("gatehouseHumanDecision");
+            Label noClose = assembler.CreateLabel("gatehouseNoClose");
+            assembler.mov(rax, timingPointerAddress);
+            assembler.mov(rax, __qword_ptr[rax]);
+            assembler.test(sil, sil);
+            assembler.jne(human);
+            assembler.cmp(r8d, __dword_ptr[rax]);
+            assembler.jge(noClose);
+            assembler.mov(eax, __dword_ptr[rax + 4]);
+            assembler.AddUnrestrictedJmp(closePathAddress);
+
+            assembler.Label(ref human);
+            assembler.cmp(r8d, __dword_ptr[rax + 8]);
+            assembler.jge(noClose);
+            assembler.mov(eax, __dword_ptr[rax + 12]);
+            assembler.AddUnrestrictedJmp(closePathAddress);
+
+            assembler.Label(ref noClose);
+            assembler.mov(rax, moduleBase);
+            assembler.mov(rbp, rax);
+            assembler.AddUnrestrictedJmp(returnAddress);
+        }
+    }
+}

diff --git a/APIShared/src/GatehouseTimingCapability.cs b/APIShared/src/GatehouseTimingCapability.cs
index 5504bf86..f89e9fd5 100644
--- a/APIShared/src/GatehouseTimingCapability.cs
+++ b/APIShared/src/GatehouseTimingCapability.cs
@@ -1,4 +1,5 @@
 using BepInEx.Logging;
+using RedBird.Core.Memory;
 using System;
 using System.Collections.Generic;
 
@@ -162,6 +163,7 @@ namespace APIShared
             object mutationSync,
             ManualLogSource log,
             GatehouseBuildTarget target,
+            ScanRegion nativeRegion,
             out GatehouseDistanceOriginService distanceOriginService,
             out NativeCapabilityDiagnostic distanceOriginDiagnostic,
             out GatehouseTimingService timingService,
@@ -170,6 +172,9 @@ namespace APIShared
             distanceOriginService = null;
             timingService = null;
             NativeSection functionSection;
+            GatehousePermanentRuntimeState runtimeState;
+            NativeResolutionException distanceLiveFailure = null;
+            NativeResolutionException timingLiveFailure = null;
 
             if (!string.Equals(binaryHash, target.BuildHash, StringComparison.OrdinalIgnoreCase))
             {
@@ -204,6 +209,23 @@ namespace APIShared
                 string actualFunctionHash = ApiSharedRuntime.ComputeSha256(memory.Slice(target.FunctionRva, target.FunctionSize));
                 if (!string.Equals(actualFunctionHash, target.FunctionHash, StringComparison.OrdinalIgnoreCase))
                     throw new NativeResolutionException(NativeCapabilityState.ValidationFailed, $"The gatehouse handler function hash changed: expected={target.FunctionHash}, actual={actualFunctionHash}.");
+
+                try { ValidateLiveDistance(nativeMemory, moduleBase, target); }
+                catch (NativeResolutionException ex) { distanceLiveFailure = ex; }
+                try { ValidateLiveTiming(nativeMemory, moduleBase, target); }
+                catch (NativeResolutionException ex) { timingLiveFailure = ex; }
+
+                bool installDistance = distanceLiveFailure == null;
+                bool installTiming = timingLiveFailure == null;
+                runtimeState = nativeRegion == null
+                    ? GatehousePermanentRuntimeState.CreateTestState(installDistance, installTiming)
+                    : installDistance || installTiming
+                        ? new GatehousePermanentRuntimeState(
+                            nativeRegion,
+                            unchecked((ulong)moduleBase),
+                            installDistance,
+                            installTiming)
+                        : GatehousePermanentRuntimeState.CreateTestState(false, false);
             }
             catch (NativeResolutionException ex)
             {
@@ -218,7 +240,8 @@ namespace APIShared
                 return;
             }
 
-            ResolveDistanceOrigin(
+            if (distanceLiveFailure == null)
+                ResolveDistanceOrigin(
                 binaryHash,
                 moduleBase,
                 memory,
@@ -228,9 +251,18 @@ namespace APIShared
                 log,
                 target,
                 functionSection,
+                runtimeState,
                 out distanceOriginService,
                 out distanceOriginDiagnostic);
-            ResolveTiming(
+            else
+                distanceOriginDiagnostic = Diagnostic(
+                    NativeCapabilityIds.GatehouseDistanceOrigin,
+                    distanceLiveFailure.State,
+                    binaryHash,
+                    distanceLiveFailure.Message);
+
+            if (timingLiveFailure == null)
+                ResolveTiming(
                 binaryHash,
                 moduleBase,
                 memory,
@@ -240,8 +272,60 @@ namespace APIShared
                 log,
                 target,
                 functionSection,
+                runtimeState,
                 out timingService,
                 out timingDiagnostic);
+            else
+                timingDiagnostic = Diagnostic(
+                    NativeCapabilityIds.GatehouseTiming,
+                    timingLiveFailure.State,
+                    binaryHash,
+                    timingLiveFailure.Message);
+        }
+
+        private static void ValidateLiveDistance(
+            INativeMemory memory,
+            long moduleBase,
+            GatehouseBuildTarget target) =>
+            RequireLiveBytes(
+                memory,
+                moduleBase + target.DistanceBlockRva,
+                target.VanillaDistanceBlockBytes,
+                "live gatehouse distance block");
+
+        private static void ValidateLiveTiming(
+            INativeMemory memory,
+            long moduleBase,
+            GatehouseBuildTarget target)
+        {
+            RequireLiveBytes(
+                memory,
+                moduleBase + target.DecisionBlockRva,
+                target.DecisionBlockBytes,
+                "live gatehouse decision block");
+            RequireLiveBytes(
+                memory,
+                moduleBase + target.HumanDelayBlockRva,
+                target.HumanDelayBlockBytes,
+                "live gatehouse human-delay block");
+        }
+
+        private static void RequireLiveBytes(
+            INativeMemory memory,
+            long address,
+            byte[] expected,
+            string name)
+        {
+            for (int index = 0; index < expected.Length; index++)
+            {
+                byte actual = memory.ReadByte(address + index);
+                if (actual != expected[index])
+                {
+                    throw new NativeResolutionException(
+                        NativeCapabilityState.ValidationFailed,
+                        $"{name} changed at +0x{index:X}: expected=0x{expected[index]:X2}, actual=0x{actual:X2}.");
+                }
+            }
         }
 
         private static void ResolveDistanceOrigin(
@@ -254,6 +338,7 @@ namespace APIShared
             ManualLogSource log,
             GatehouseBuildTarget target,
             NativeSection functionSection,
+            GatehousePermanentRuntimeState runtimeState,
             out GatehouseDistanceOriginService service,
             out NativeCapabilityDiagnostic diagnostic)
         {
@@ -271,7 +356,8 @@ namespace APIShared
                     moduleBase + target.DistanceBlockRva,
                     target.VanillaDistanceBlockBytes,
                     target.CenteredDistanceBlockBytes);
-                service = new GatehouseDistanceOriginService(binaryHash, memoryTarget, nativeMemory, ownership, mutationSync, log);
+                service = new GatehouseDistanceOriginService(
+                    binaryHash, memoryTarget, runtimeState, ownership, mutationSync, log);
                 diagnostic = AvailableDiagnostic(NativeCapabilityIds.GatehouseDistanceOrigin, binaryHash, target);
             }
             catch (NativeResolutionException ex)
@@ -294,6 +380,7 @@ namespace APIShared
             ManualLogSource log,
             GatehouseBuildTarget target,
             NativeSection functionSection,
+            GatehousePermanentRuntimeState runtimeState,
             out GatehouseTimingService service,
             out NativeCapabilityDiagnostic diagnostic)
         {
@@ -322,7 +409,8 @@ namespace APIShared
                     moduleBase + target.HumanCloseDistanceRva,
                     moduleBase + target.HumanReopenDelayRva,
                     invariants);
-                service = new GatehouseTimingService(binaryHash, memoryTarget, nativeMemory, ownership, mutationSync, log);
+                service = new GatehouseTimingService(
+                    binaryHash, memoryTarget, runtimeState, ownership, mutationSync, log);
                 diagnostic = AvailableDiagnostic(NativeCapabilityIds.GatehouseTiming, binaryHash, target);
             }
             catch (NativeResolutionException ex)
@@ -464,7 +552,7 @@ namespace APIShared
         private const int UnitsPerTile = 8;
         private readonly string binaryHash;
         private readonly GatehouseTimingTarget target;
-        private readonly INativeMemory memory;
+        private readonly GatehousePermanentRuntimeState runtimeState;
         private readonly NativeOwnershipRegistry ownership;
         private readonly object mutationSync;
         private readonly ManualLogSource log;
@@ -476,14 +564,14 @@ namespace APIShared
         public GatehouseTimingService(
             string binaryHash,
             GatehouseTimingTarget target,
-            INativeMemory memory,
+            GatehousePermanentRuntimeState runtimeState,
             NativeOwnershipRegistry ownership,
             object mutationSync,
             ManualLogSource log)
         {
             this.binaryHash = binaryHash;
             this.target = target ?? throw new ArgumentNullException(nameof(target));
-            this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
+            this.runtimeState = runtimeState ?? throw new ArgumentNullException(nameof(runtimeState));
             this.ownership = ownership ?? throw new ArgumentNullException(nameof(ownership));
             this.mutationSync = mutationSync ?? throw new ArgumentNullException(nameof(mutationSync));
             this.log = log;
@@ -552,7 +640,7 @@ namespace APIShared
                         diagnostic = Diagnostic(NativeCapabilityState.Available, "The requested gatehouse timing values are already active and were verified.");
                         return true;
                     }
-                    WriteTransaction(aiDistance, aiDelay, humanDistance, humanDelay);
+                    runtimeState.PublishTiming(aiDistance, aiDelay, humanDistance, humanDelay);
                     expectedAiDistance = aiDistance;
                     expectedAiDelay = aiDelay;
                     expectedHumanDistance = humanDistance;
@@ -572,50 +660,8 @@ namespace APIShared
             }
         }
 
-        private void WriteTransaction(int aiDistance, int aiDelay, int humanDistance, int humanDelay)
-        {
-            int oldAiDistance = expectedAiDistance;
-            int oldAiDelay = expectedAiDelay;
-            int oldHumanDistance = expectedHumanDistance;
-            int oldHumanDelay = expectedHumanDelay;
-            GatehouseNativeMutation.Execute(
-                memory,
-                target.Intervals,
-                VerifyOwnedValues,
-                () =>
-                {
-                    memory.WriteInt32(target.AiDistance, aiDistance);
-                    memory.WriteInt32(target.AiDelay, aiDelay);
-                    memory.WriteInt32(target.HumanDistance, humanDistance);
-                    memory.WriteInt32(target.HumanDelay, humanDelay);
-                    Verify(target.AiDistance, aiDistance, "AI distance");
-                    Verify(target.AiDelay, aiDelay, "AI delay");
-                    Verify(target.HumanDistance, humanDistance, "human distance");
-                    Verify(target.HumanDelay, humanDelay, "human delay");
-                },
-                () =>
-                {
-                    memory.WriteInt32(target.AiDistance, oldAiDistance);
-                    memory.WriteInt32(target.AiDelay, oldAiDelay);
-                    memory.WriteInt32(target.HumanDistance, oldHumanDistance);
-                    memory.WriteInt32(target.HumanDelay, oldHumanDelay);
-                    Verify(target.AiDistance, oldAiDistance, "rolled-back AI distance");
-                    Verify(target.AiDelay, oldAiDelay, "rolled-back AI delay");
-                    Verify(target.HumanDistance, oldHumanDistance, "rolled-back human distance");
-                    Verify(target.HumanDelay, oldHumanDelay, "rolled-back human delay");
-                });
-        }
-
         private void VerifyInitialLayoutAndOwnedValues()
         {
-            foreach (NativeByteInvariant invariant in target.InstructionInvariants)
-            {
-                byte actual = memory.ReadByte(invariant.Address);
-                if (actual != invariant.Value)
-                    throw new NativeResolutionException(
-                        NativeCapabilityState.ValidationFailed,
-                        $"Gatehouse instruction byte changed unexpectedly at target offset: expected=0x{invariant.Value:X2}, actual=0x{actual:X2}.");
-            }
             try
             {
                 VerifyOwnedValues();
@@ -628,17 +674,18 @@ namespace APIShared
 
         private void VerifyOwnedValues()
         {
-            Verify(target.AiDistance, expectedAiDistance, "AI distance");
-            Verify(target.AiDelay, expectedAiDelay, "AI delay");
-            Verify(target.HumanDistance, expectedHumanDistance, "human distance");
-            Verify(target.HumanDelay, expectedHumanDelay, "human delay");
-        }
-
-        private void Verify(long address, int expected, string name)
-        {
-            int actual = memory.ReadInt32(address);
-            if (actual != expected)
-                throw new InvalidOperationException($"Gatehouse {name} changed unexpectedly: expected={expected}, actual={actual}.");
+            runtimeState.ReadTiming(
+                out int aiDistance,
+                out int aiDelay,
+                out int humanDistance,
+                out int humanDelay);
+            if (!runtimeState.IsTimingInstalled ||
+                aiDistance != expectedAiDistance || aiDelay != expectedAiDelay ||
+                humanDistance != expectedHumanDistance || humanDelay != expectedHumanDelay)
+            {
+                throw new InvalidOperationException(
+                    "Gatehouse timing logical state changed unexpectedly.");
+            }
         }
 
         private NativeCapabilityDiagnostic Diagnostic(NativeCapabilityState state, string reason) =>

diff --git a/APIShared/src/LobbyPreparationOverride.cs b/APIShared/src/LobbyPreparationOverride.cs
new file mode 100644
index 00000000..953ad9fb
--- /dev/null
+++ b/APIShared/src/LobbyPreparationOverride.cs
@@ -0,0 +1,113 @@
+using CrusaderDE;
+using System;
+using System.Reflection;
+
+namespace APIShared
+{
+    /// <summary>Process-lifetime, single-owner preparation of a newly opened skirmish lobby.</summary>
+    public static class LobbyPreparationOverride
+    {
+        private static string owner;
+        private static Func<FRONT_Multiplayer, bool> prepare;
+        private static Action<FRONT_Multiplayer> apply;
+        private static Platform_Multiplayer.MPLobby currentLobby;
+        private static bool prepared;
+        private static bool applyAttempted;
+        private static bool active;
+        private static readonly FieldInfo selectedMapField = typeof(FRONT_Multiplayer).GetField(
+            "selectedMPHeader", BindingFlags.Instance | BindingFlags.NonPublic);
+        private static readonly FieldInfo mapListField = typeof(FRONT_Multiplayer).GetField(
+            "RefFileLists", BindingFlags.Instance | BindingFlags.NonPublic);
+
+        /// <summary>Registers rooted callbacks before the lobby is opened.</summary>
+        public static void Register(string ownerId, Func<FRONT_Multiplayer, bool> prepareCallback,
+            Action<FRONT_Multiplayer> applyCallback)
+        {
+            if (string.IsNullOrWhiteSpace(ownerId) || prepareCallback == null || applyCallback == null)
+                throw new ArgumentException("A lobby preparation owner and both callbacks are required.");
+            if (owner != null && owner != ownerId)
+                throw new InvalidOperationException("Lobby preparation is already owned by " + owner + ".");
+            owner = ownerId;
+            prepare = prepareCallback;
+            apply = applyCallback;
+        }
+
+        /// <summary>Observes the ready lobby from a process-lifetime frontend callback.</summary>
+        public static void Tick(FRONT_Multiplayer view)
+        {
+            // FRONT_Multiplayer inherits Noesis.BaseComponent, whose == null checks
+            // the native handle rather than the managed reference.
+            if (ReferenceEquals(view, null) || !FRONT_Multiplayer.skirmishGame ||
+                !MainViewModel.viewModelLoaded ||
+                MainViewModel.Instance?.Show_MPGameCreation != true)
+            {
+                if (view?.currentLobby == null)
+                    ResetLobby(null);
+                return;
+            }
+
+            Begin(view);
+            Apply(view);
+        }
+
+        private static void ResetLobby(Platform_Multiplayer.MPLobby lobby)
+        {
+            currentLobby = lobby;
+            prepared = false;
+            applyAttempted = false;
+            active = false;
+        }
+
+        /// <summary>Reads and validates the preset once for each newly opened lobby.</summary>
+        public static bool Begin(FRONT_Multiplayer lobby)
+        {
+            Platform_Multiplayer.MPLobby session = lobby?.currentLobby;
+            if (!ReferenceEquals(currentLobby, session))
+                ResetLobby(session);
+            if (prepare == null || session == null)
+                return false;
+            if (prepared)
+                return active;
+
+            // A setup callback can run before Vanilla has finished opening the panel.
+            // A rejected early preparation gets one more chance on the ready panel.
+            prepared = lobby.panelActive;
+            try { active = prepare(lobby); }
+            catch (Exception exception)
+            {
+                UnityEngine.Debug.LogError("Lobby preparation " + owner + " rejected: " + exception);
+            }
+            return active;
+        }
+
+        /// <summary>Whether an owner has accepted the current lobby opening.</summary>
+        public static bool IsActive => active;
+
+        /// <summary>Applies the prepared preset after Vanilla has opened its controls.</summary>
+        public static void Apply(FRONT_Multiplayer lobby)
+        {
+            if (!active || apply == null || applyAttempted || ReferenceEquals(lobby, null) ||
+                !ReferenceEquals(currentLobby, lobby.currentLobby) ||
+                !lobby.panelActive)
+                return;
+            try
+            {
+                if (selectedMapField == null || mapListField == null)
+                    throw new MissingFieldException("FRONT_Multiplayer lobby map fields are unavailable.");
+                if (selectedMapField.GetValue(lobby) == null ||
+                    (mapListField.GetValue(lobby) as Noesis.ListView)?.ItemsSource == null)
+                    return;
+                // Applying a map can synchronously invoke other lobby hooks.
+                applyAttempted = true;
+                apply(lobby);
+            }
+            catch (Exception exception)
+            {
+                applyAttempted = true;
+                // Keep the owner's memory suppression active: Apply may already have
+                // changed part of the lobby, so saving it as the user's preference is unsafe.
+                UnityEngine.Debug.LogError("Lobby preparation " + owner + " incomplete: " + exception);
+            }
+        }
+    }
+}

diff --git a/APIShared/src/MissionModePolicy.cs b/APIShared/src/MissionModePolicy.cs
index e6888bcd..3fe69d03 100644
--- a/APIShared/src/MissionModePolicy.cs
+++ b/APIShared/src/MissionModePolicy.cs
@@ -5,7 +5,7 @@ using System;
 using System.Collections.Generic;
 using System.Linq;
 using System.Reflection;
-#if !SHARED_PRESET_TESTS
+#if !API_SHARED_PRESET_TESTS
 using Steamworks;
 #endif
 
@@ -401,7 +401,7 @@ namespace Shared
 
         private static bool TryCaptureVanillaCustomizedTrail(out int trailType, out int trailId)
         {
-#if SHARED_PRESET_TESTS
+#if API_SHARED_PRESET_TESTS
             trailType = NoGameValue;
             trailId = NoGameValue;
             return false;
@@ -414,7 +414,7 @@ namespace Shared
 
         private static ExternalCustomizedOrigin CaptureExternalCustomizedOrigin()
         {
-#if SHARED_PRESET_TESTS
+#if API_SHARED_PRESET_TESTS
             return default;
 #else
             try
@@ -752,7 +752,7 @@ namespace Shared
         /// <summary>HasConflictingCustomizedOrigin in the centralized mission policy contract.</summary>
         public bool HasConflictingCustomizedOrigin { get; }
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/APIShared.diff).
