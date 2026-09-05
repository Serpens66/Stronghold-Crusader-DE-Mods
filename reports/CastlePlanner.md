# CastlePlanner release status

**Status:** code newer

- Release: [v0.8.21](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/CastlePlanner/v0.8.21)
- Release commit: [dd615ed](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/dd615ed49844820753b18b4a5652ff6533f16941)
- Current main commit: [18fd969](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/18fd9693929726316a7478878360d5f98d0770cb)

## Relevant changed files

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
- `CastlePlanner/CastlePlanner.csproj`
- `CastlePlanner/src/BlueprintRuntimeController.cs`
- `CastlePlanner/src/CastlePlannerPlugin.cs`
- `CastlePlanner/src/CastlePlannerRuntime.cs`
- `CastlePlanner/src/FreeCastlePreviewRuntime.cs`
- `Shared/GameModeHelper.cs`
- `Shared/GameplayFeatureModePolicy.cs`
- `Shared/GameplayModActivationGate.cs`
- `Shared/GameplayModModePolicy.cs`
- `Shared/PresetLobbyModSettingsViewModel.cs`

## Diff

```diff
diff --git a/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/info.json b/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/info.json
index 8b5f9b7a..38f79018 100644
--- a/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/info.json
+++ b/CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/info.json
@@ -6,6 +6,7 @@
   "Version": "0.8.21",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "Manifest": 1,
+  "NetworkMode": 1,
   "SerpChangelog": [
     {
       "Version": "0.8.21",

diff --git a/CastlePlanner/CastlePlanner.csproj b/CastlePlanner/CastlePlanner.csproj
index c75cd35c..b8757814 100644
--- a/CastlePlanner/CastlePlanner.csproj
+++ b/CastlePlanner/CastlePlanner.csproj
@@ -125,16 +125,16 @@
       <HintPath>$(ExtenderDir)\System.Memory.dll</HintPath>
       <Private>false</Private>
     </Reference>
-    <Reference Include="Zhuqiaomon">
-      <HintPath>$(ExtenderDir)\Zhuqiaomon.dll</HintPath>
+    <Reference Include="RedBird.Abstractions">
+      <HintPath>$(ExtenderDir)\RedBird.Abstractions.dll</HintPath>
       <Private>false</Private>
     </Reference>
-    <Reference Include="PolyHook2.NET">
-      <HintPath>$(ExtenderDir)\PolyHook2.NET.dll</HintPath>
+    <Reference Include="RedBird.Core">
+      <HintPath>$(ExtenderDir)\RedBird.Core.dll</HintPath>
       <Private>false</Private>
     </Reference>
-    <Reference Include="Iced">
-      <HintPath>$(ExtenderDir)\Iced.dll</HintPath>
+    <Reference Include="RedBird.X64">
+      <HintPath>$(ExtenderDir)\RedBird.X64.dll</HintPath>
       <Private>false</Private>
     </Reference>
     <Reference Include="Microsoft.Extensions.Logging.Abstractions">
@@ -175,6 +175,15 @@
     <Compile Include="..\Shared\GameModeHelper.cs">
       <Link>Shared\GameModeHelper.cs</Link>
     </Compile>
+    <Compile Include="..\Shared\GameplayModActivationGate.cs">
+      <Link>Shared\GameplayModActivationGate.cs</Link>
+    </Compile>
+    <Compile Include="..\Shared\GameplayModModePolicy.cs">
+      <Link>Shared\GameplayModModePolicy.cs</Link>
+    </Compile>
+    <Compile Include="..\Shared\GameplayFeatureModePolicy.cs">
+      <Link>Shared\GameplayFeatureModePolicy.cs</Link>
+    </Compile>
     <Compile Include="..\Shared\PresetLobbyModSettingsViewModel.cs">
       <Link>Shared\PresetLobbyModSettingsViewModel.cs</Link>
     </Compile>
@@ -236,6 +245,12 @@
            Text="Der lokale Script Extender Nebenordner existiert, aber es wurde keine lokale SHCDESE.dll gefunden. Baue zuerst ..\shcde-script-extender\build.bat oder uebergib /p:ExtenderDir=... explizit." />
     <Error Condition="'$(ExtenderDir)' != '' and !Exists('$(ExtenderDir)\SHCDESE.dll')"
            Text="SHCDESE.dll wurde im ExtenderDir nicht gefunden: $(ExtenderDir)" />
+    <Error Condition="'$(ExtenderDir)' != '' and !Exists('$(ExtenderDir)\RedBird.Abstractions.dll')"
+           Text="RedBird.Abstractions.dll wurde im ExtenderDir nicht gefunden: $(ExtenderDir)" />
+    <Error Condition="'$(ExtenderDir)' != '' and !Exists('$(ExtenderDir)\RedBird.Core.dll')"
+           Text="RedBird.Core.dll wurde im ExtenderDir nicht gefunden: $(ExtenderDir)" />
+    <Error Condition="'$(ExtenderDir)' != '' and !Exists('$(ExtenderDir)\RedBird.X64.dll')"
+           Text="RedBird.X64.dll wurde im ExtenderDir nicht gefunden: $(ExtenderDir)" />
     <Error Condition="!Exists('$(LobbyCoreOutput)\CastlePlanner.AIVPlacement.Core.dll')"
            Text="CastlePlanner.AIVPlacement.Core.dll wurde nicht gefunden. build.bat muss zuerst den AIV-Placement-Kern bauen." />
     <Error Condition="!Exists('$(PlacementCoreOutput)\AIVPlacement.Core.dll')"

diff --git a/CastlePlanner/src/BlueprintRuntimeController.cs b/CastlePlanner/src/BlueprintRuntimeController.cs
index 1abb980e..1f6aeda4 100644
--- a/CastlePlanner/src/BlueprintRuntimeController.cs
+++ b/CastlePlanner/src/BlueprintRuntimeController.cs
@@ -95,6 +95,7 @@ namespace CastlePlanner
                 OnBlueprintContentSettingsChanged;
             settings.HotkeyCaptureRequested += OnHotkeyCaptureRequested;
             preview.SelectionVisualChanged += OnPreviewSelectionChanged;
+            Shared.GameplayModActivationGate.StateChanged += OnModeStateChanged;
             subscriptions.Add(
                 MapLoaderR3EventHooks.OnStartMap.Observable
                     .Where(args => args.Phase == EventHookPhase.Post)
@@ -305,7 +306,11 @@ namespace CastlePlanner
 
         private void CameraUpdateHook(CameraControls2D camera)
         {
-            if (Hud?.ShouldSuppressMapZoom() == true)
+            if (Shared.GameplayFeatureModePolicy.IsAllowed(
+                    CastlePlannerPlugin.PluginGuid,
+                    Shared.GameplayFeatureId.CastleBlueprints,
+                    Shared.GameplayModActivationGate.Snapshot) &&
+                Hud?.ShouldSuppressMapZoom() == true)
                 camera.AllowZoom = false;
             cameraUpdateTrampoline(camera);
         }
@@ -383,7 +388,18 @@ namespace CastlePlanner
         }
 
         private bool EffectiveBlueprintMode =>
-            settings?.IsBlueprintMode == true || preview?.IsPreviewActive == true;
+            Shared.GameplayFeatureModePolicy.IsAllowed(
+                CastlePlannerPlugin.PluginGuid,
+                Shared.GameplayFeatureId.CastleBlueprints,
+                Shared.GameplayModActivationGate.Snapshot) &&
+            (settings?.IsBlueprintMode == true || preview?.IsPreviewActive == true);
+
+        private void OnModeStateChanged(bool allowed)
+        {
+            if (!allowed)
+                ResetMapState();
+            RefreshHud();
+        }
 
         private void OnPreviewSelectionChanged()
         {

diff --git a/CastlePlanner/src/CastlePlannerPlugin.cs b/CastlePlanner/src/CastlePlannerPlugin.cs
index 282c677d..8ebc2f11 100644
--- a/CastlePlanner/src/CastlePlannerPlugin.cs
+++ b/CastlePlanner/src/CastlePlannerPlugin.cs
@@ -6,7 +6,7 @@ using System.Collections.Generic;
 
 namespace CastlePlanner
 {
-    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
+    [BepInDependency(ScriptExtenderGuid, "2.0.2")]
     [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
     [BepInDependency("ExtraFeatures_Serp", BepInDependency.DependencyFlags.SoftDependency)]
     [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
@@ -33,6 +33,7 @@ namespace CastlePlanner
                 return;
 
             Settings = new CastlePlannerSettingsViewModel(Logger, Info.Location);
+            Shared.GameplayModActivationGate.Initialize(Logger, PluginGuid, PluginName, () => Settings.EnableMod);
             previewRuntime = new FreeCastlePreviewRuntime(Logger, Settings);
             runtime = new CastlePlannerRuntime(Logger, Settings, previewRuntime);
             CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
@@ -48,10 +49,12 @@ namespace CastlePlanner
                 "Plugin component destroyed during startup; keeping CastlePlanner lifecycle subscriptions rooted.");
         }
 
-        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
+        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
         {
             if (libraryLoadedHandled)
                 return;
+            if (context == null)
+                throw new ArgumentNullException(nameof(context));
             libraryLoadedHandled = true;
 
             var failedOptionalStages = new List<string>();
@@ -136,7 +139,7 @@ namespace CastlePlanner
             {
                 try
                 {
-                    runtime.Install(libraryHandle, memory, currentNativeLayout);
+                    runtime.Install(context, currentNativeLayout);
                 }
                 catch (Exception ex)
                 {

diff --git a/CastlePlanner/src/CastlePlannerRuntime.cs b/CastlePlanner/src/CastlePlannerRuntime.cs
index 6bf5b2e7..1ae1dfd8 100644
--- a/CastlePlanner/src/CastlePlannerRuntime.cs
+++ b/CastlePlanner/src/CastlePlannerRuntime.cs
@@ -2,7 +2,15 @@ using BepInEx.Logging;
 using BepInEx.Bootstrap;
 using AIVParser.Core;
 using R3;
+using RedBird.Abstractions.Hooks;
+using RedBird.Abstractions.Hooks.Transaction;
+using RedBird.Core.Memory;
+using RedBird.X64.Assembly;
+using RedBird.X64.Hooks;
+using RedBird.X64.Hooks.Context;
+using RedBird.X64.Hooks.Transaction;
 using SHCDESE.API;
+using SHCDESE.API.LowLevel;
 using SHCDESE.EventAPI;
 using SHCDESE.EventAPI.Buildings;
 using SHCDESE.EventAPI.MapLoader;
@@ -18,10 +26,6 @@ using System.Runtime.InteropServices;
 using System.Reflection;
 using System.Security.Cryptography;
 using System.Text;
-using Zhuqiaomon.Assembly;
-using Zhuqiaomon.Hooks;
-using Zhuqiaomon.Hooks.Transaction;
-using Zhuqiaomon.Memory;
 
 namespace CastlePlanner
 {
@@ -132,8 +136,8 @@ namespace CastlePlanner
         private IntPtr preparedKeepX;
         private IntPtr preparedKeepY;
         private HookTransaction nativeHookTransaction;
-        private HookRef<X64InlineHook> humanKeepCoordinateLoadHook =
-            new HookRef<X64InlineHook>();
+        private readonly HookHandle<X64InlineHook> humanKeepCoordinateLoadHook =
+            new HookHandle<X64InlineHook>();
         private bool installed;
         private bool referenceHashMatches;
         private bool handledCurrentMap;
@@ -171,17 +175,16 @@ namespace CastlePlanner
             this.preview = preview ?? throw new ArgumentNullException(nameof(preview));
         }
 
-        public void Install(
-            IntPtr libraryHandle,
-            ReadOnlySpan<byte> memory,
-            bool referenceHashMatches)
+        public void Install(CrusaderLibraryLoadContext context, bool referenceHashMatches)
         {
             if (installed)
                 return;
+            if (context == null)
+                throw new ArgumentNullException(nameof(context));
 
             this.referenceHashMatches = referenceHashMatches;
-            BindNativeFunctions(libraryHandle, memory);
-            InstallHumanStartPreparationHook(libraryHandle, memory);
+            BindNativeFunctions(context.ModuleHandle, context.Memory);
+            InstallHumanStartPreparationHook(context);
 
             subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                 .Subscribe(OnStartMap));
@@ -438,6 +441,9 @@ namespace CastlePlanner
 
         private void OnBuildStructurePre(BuildStructureEventArgs args)
         {
+            if (!IsCastleSpawningModeAllowed())
+                return;
+
             if (TryCorrectNativeHovelVisualStyle(args))
                 return;
 
@@ -514,6 +520,9 @@ namespace CastlePlanner
 
         private void OnBuildStructurePost(BuildStructureEventArgs args)
         {
+            if (!IsCastleSpawningModeAllowed())
+                return;
+
             if (!preparedAivCastles.TryGetValue(args.PlayerId, out PreparedAivCastle castle) ||
                 !IsKeepMapper(args.Mappers))
             {
@@ -545,6 +554,9 @@ namespace CastlePlanner
 
         private void OnUnitCreateDiagnostic(UnitCreateEventArgs args)
         {
+            if (!IsCastleSpawningModeAllowed())
+                return;
+
             int playerId = args.PlayerOwnerId;
             if (!expectedAivCastlePlayers.Contains(playerId))
                 return;
@@ -799,6 +811,9 @@ namespace CastlePlanner
 
         private void OnBuildingSpawnPost(BuildingSpawnEventArgs args)
         {
+            if (!IsCastleSpawningModeAllowed())
+                return;
+
             if (!captureSupplementalBuilding ||
                 args.PlayerId != captureSupplementalPlayerId ||
                 args.TileX != captureSupplementalX ||
@@ -1247,6 +1262,9 @@ namespace CastlePlanner
 
         private void OnGameTick(int tick)
         {
+            if (!IsCastleSpawningModeAllowed())
+                return;
+
             if (deferredCompoundBuildings.Count == 0)
                 return;
 
@@ -1584,30 +1602,37 @@ namespace CastlePlanner
                 $"preparedKeepY=0x{preparedKeepY.ToInt64():X}.");
         }
 
-        private void InstallHumanStartPreparationHook(
-            IntPtr libraryHandle,
-            ReadOnlySpan<byte> memory)
+        private void InstallHumanStartPreparationHook(CrusaderLibraryLoadContext context)
         {
+            ReadOnlySpan<byte> memory = context.Memory;
+            ulong libraryBase = unchecked((ulong)context.ModuleHandle.ToInt64());
             int humanStartHookRva = ResolveReferenceRva(
                 memory,
                 "Vanilla human Keep coordinate load",
                 HumanKeepCoordinateLoadPattern,
                 HumanKeepCoordinateLoadRva);
             nativeHookTransaction = new HookTransaction(
-                memory,
-                unchecked((ulong)libraryHandle.ToInt64()),
-                loggerFactory: null,
-                failureMode: TransactionFailureMode.RollbackAndThrow);
+                context.Region,
+                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
+                new HookTransactionOptions
+                {
+                    FailureMode = TransactionFailureMode.RollbackAndThrow,
+                    // CastlePlanner's static runtime keeps this hook for the process lifetime.
+                    OwnsHooks = false
+                });
             nativeHookTransaction.AddContextHook(
-                ref humanKeepCoordinateLoadHook,
-                unchecked((ulong)libraryHandle.ToInt64()) + unchecked((ulong)humanStartHookRva),
+                humanKeepCoordinateLoadHook,
+                HookTarget.FromAddress(libraryBase + unchecked((ulong)humanStartHookRva)),
                 PrepareVanillaHumanStart,
-                regs: X64SmartCPUContextRegs.All,
-                hookSize: 16,
-                errorMode: CallbackErrorMode.LogAndContinue,
-                placement: OverwrittenInstructionPlacement.AfterCallback);
-            nativeHookTransaction.Commit();
-            if (!humanKeepCoordinateLoadHook.Success)
+                new ContextHookOptions
+                {
+                    Registers = X64SmartCPUContextRegs.All,
+                    HookSize = 16,
+                    ErrorMode = CallbackErrorMode.LogAndContinue,
+                    Placement = OverwrittenInstructionPlacement.AfterCallback
+                });
+            CommitResult commitResult = nativeHookTransaction.Commit();
+            if (!commitResult.IsCompleteSuccess || !humanKeepCoordinateLoadHook.Success)
                 throw new InvalidOperationException("The Vanilla human Keep coordinate-load hook was not installed.");
 
             Shared.DebugLogHelper.LogInfo(
@@ -1619,6 +1644,9 @@ namespace CastlePlanner
         private void PrepareVanillaHumanStart(
             NativePointer<X64SmartCPUContext> context)
         {
+            if (!IsCastleSpawningModeAllowed())
+                return;
+
             X64SmartCPUContext* registers = context.Pointer;
             int playerId = unchecked((int)registers->RSI);
             if (!pendingAivImports.TryGetValue(playerId, out PendingAivImport imported) ||
@@ -1868,7 +1896,7 @@ namespace CastlePlanner
         private static GameModeSnapshot CaptureGameMode(MapStartEventArgs args)
         {
             Shared.GameModeSnapshot sharedMode =
-                Shared.GameModeHelper.Capture(args.bMultiplayerSave != 0);
+                Shared.GameplayModActivationGate.Snapshot;
             Director director = Director.instance;
             GameData gameData = GameData.Instance;
             Platform_Multiplayer platform = Platform_Multiplayer.Instance;
@@ -1996,6 +2024,12 @@ namespace CastlePlanner
 
         private static void EnsureSupportedGameMode(GameModeSnapshot mode)
         {
+            if (!IsCastleSpawningModeAllowed())
+            {
+                throw new NotSupportedException(
+                    "Native CastlePlanner spawning is disabled by the gameplay-feature mode gate.");
+            }
+
             if (!mode.DirectorAvailable)
             {
                 throw new InvalidOperationException(
@@ -2012,6 +2046,12 @@ namespace CastlePlanner
             }
         }
 
+        private static bool IsCastleSpawningModeAllowed() =>
+            Shared.GameplayFeatureModePolicy.IsAllowed(
+                CastlePlannerPlugin.PluginGuid,
+                Shared.GameplayFeatureId.CastleSpawning,
+                Shared.GameplayModActivationGate.Snapshot);
+
         private static int CountOwnedBuildings(int playerId)
         {
             int count = 0;

diff --git a/CastlePlanner/src/FreeCastlePreviewRuntime.cs b/CastlePlanner/src/FreeCastlePreviewRuntime.cs
index 440a126c..e9638e76 100644
--- a/CastlePlanner/src/FreeCastlePreviewRuntime.cs
+++ b/CastlePlanner/src/FreeCastlePreviewRuntime.cs
@@ -246,7 +246,7 @@ namespace CastlePlanner
         public bool TryGetCommittedSelections(out List<FreeCastleSelection> selections)
         {
             selections = null;
-            if (state != PreviewState.SpawnMap)
+            if (!IsFeatureModeAllowed() || state != PreviewState.SpawnMap)
                 return false;
             selections = committedSelections.Select(item => item.Clone()).ToList();
             return selections.Count > 0;
@@ -255,7 +255,7 @@ namespace CastlePlanner
         public bool TryGetCommittedRotation(int playerId, out int rotation)
         {
             rotation = 0;
-            if (state != PreviewState.SpawnMap)
+            if (!IsFeatureModeAllowed() || state != PreviewState.SpawnMap)
                 return false;
             return FreeCastleSelectionLookup.TryGetRotation(
                 committedSelections,
@@ -286,7 +286,8 @@ namespace CastlePlanner
             int actionState,
             int value2)
         {
-            if (!bypassPauseHook && IsPreviewPendingOrActive &&
+            if (IsFeatureModeAllowed() &&
+                !bypassPauseHook && IsPreviewPendingOrActive &&
                 command == Enums.GameActionCommand.Game_Paused && actionState == 0)
             {
                 Shared.DebugLogHelper.LogInfo(log, "Unpause command suppressed during castle selection.");
@@ -297,7 +298,8 @@ namespace CastlePlanner
 
         private void LeaveLobbyHook(Platform_Multiplayer self, bool preserveGameMembers)
         {
-            if (!bypassLeaveLobbyHook && IsPreviewPendingOrActive && realMultiplayer)
+            if (IsFeatureModeAllowed() &&
+                !bypassLeaveLobbyHook && IsPreviewPendingOrActive && realMultiplayer)
             {
                 Shared.DebugLogHelper.LogInfo(log, "Vanilla lobby departure deferred during castle selection.");
                 return;
@@ -311,6 +313,11 @@ namespace CastlePlanner
             {
                 if (state == PreviewState.RestartCommitted)
                 {
+                    if (!IsFeatureModeAllowed())
+                    {
+                        ResetPreview();
+                        return;
+                    }
                     state = PreviewState.SpawnMap;
                     NotifyAll();
                     return;
@@ -321,7 +328,7 @@ namespace CastlePlanner
                 ResetPreview();
                 state = PreviewState.AwaitingGameplay;
                 operationId = unchecked((int)DateTime.UtcNow.Ticks) & int.MaxValue;
-                realMultiplayer = Shared.GameModeHelper.IsRealMultiplayer(false);
+                realMultiplayer = Shared.GameplayModActivationGate.Snapshot.IsRealMultiplayer;
                 localPlayerId = ResolveLocalPlayerId(out string identityError);
                 BuildRoster(out string rosterError);
                 ApplyPause(true);
@@ -347,10 +354,10 @@ namespace CastlePlanner
 
         private bool ShouldStartPreview(MapStartEventArgs args)
         {
-            if (!settings.IsSpawnMode || args.bMultiplayerSave != 0 || args.CampaignMapId != 0 ||
-                Shared.GameModeHelper.IsMapEditor())
+            if (!IsFeatureModeAllowed() ||
+                !settings.IsSpawnMode || args.bMultiplayerSave != 0 || args.CampaignMapId != 0)
                 return false;
-            Shared.GameModeSnapshot mode = Shared.GameModeHelper.Capture(false);
+            Shared.GameModeSnapshot mode = Shared.GameplayModActivationGate.Snapshot;
             return mode.IsRealMultiplayer || mode.IsSingleplayerSkirmishMode;
         }
 
@@ -537,6 +544,9 @@ namespace CastlePlanner
 
         private void OnPacket(ReceiveCustomPacketEventArgs<FreeCastlePacket> args)
         {
+            if (!IsFeatureModeAllowed())
+                return;
+
             FreeCastlePacket packet = args?.Packet;
             if (packet == null || !args.SenderSteamId.HasValue || !IsPreviewPendingOrActive)
                 return;
@@ -1153,6 +1163,12 @@ namespace CastlePlanner
             }
         }
 
+        private static bool IsFeatureModeAllowed() =>
+            Shared.GameplayFeatureModePolicy.IsAllowed(
+                CastlePlannerPlugin.PluginGuid,
+                Shared.GameplayFeatureId.FreeCastlePreview,
+                Shared.GameplayModActivationGate.Snapshot);
+
         private void ResetPreview()
         {
             state = PreviewState.Inactive;

diff --git a/Shared/GameModeHelper.cs b/Shared/GameModeHelper.cs
index 22480a13..838befb5 100644
--- a/Shared/GameModeHelper.cs
+++ b/Shared/GameModeHelper.cs
@@ -1,17 +1,122 @@
 using SHCDESE.API;
+using SHCDESE.EventAPI.MapLoader;
 using CrusaderDE;
 using System;
 using System.Collections.Generic;
 using System.Linq;
+using System.Reflection;
 #if !SHARED_PRESET_TESTS
 using Steamworks;
 #endif
 
 namespace Shared
 {
+    internal enum GameModeKind
+    {
+        Unknown,
+        MapEditor,
+        Campaign,
+        StandaloneMission,
+        CustomGame,
+        VanillaTrail,
+        CustomTrail,
+        CoopTrail,
+        SandsOfTime,
+    }
+
+    internal enum GameModeLaunchVariant
+    {
+        Standard,
+        Customized,
+        RestoredCustomizedSave,
+    }
+
+    internal enum GameTrailType
+    {
+        FirstEdition = 0,
+        Warchest = 1,
+        Extreme = 2,
+        SandsOne = 11,
+        SandsTwo = 12,
+        SandsThree = 13,
+        SandsFour = 14,
+        SandsFive = 15,
+        SandsSix = 16,
+        SandsSeven = 17,
+        SandsEight = 18,
+    }
+
     internal static class GameModeHelper
     {
+        private const int NoGameValue = -1;
+        private const int NoCoopTrail = 0;
+        private const uint NonCampaignMapId = uint.MaxValue;
+        private const int MinimumOriginApiVersion = 1;
+        private const int SupportedOriginApiVersion = 2;
+        private const int FirstCustomTrailId = 90;
+        private const int LastCustomTrailId = 92;
+        private const int FirstCoopTrailId = 0;
+        private const int LastCoopTrailId = 3;
+        private const int FirstMissionId = 1;
+        private const int LastCoopMissionId = 10;
+
         public static GameModeSnapshot Capture(bool multiplayerSave = false)
+        {
+            return CaptureCore(
+                multiplayerSave,
+                campaignMapId: 0,
+                eventTrailType: NoGameValue,
+                editorLoad: false);
+        }
+
+        public static GameModeSnapshot Capture(MapStartEventArgs args) =>
+            CaptureCore(
+                args != null && args.bMultiplayerSave != 0,
+                args?.CampaignMapId ?? 0,
+                NoGameValue,
+                editorLoad: false);
+
+        public static GameModeSnapshot Capture(MapLoadEventArgs args) =>
+            CaptureCore(
+                args != null && args.bMultiplayerSave != 0,
+                args != null && args.CampaignMapID != NonCampaignMapId && args.CampaignMapID <= int.MaxValue
+                    ? (int)args.CampaignMapID
+                    : 0,
+                args?.TrailType ?? NoGameValue,
+                editorLoad: false);
+
+        public static GameModeSnapshot Capture(LoadSaveGameEventArgs args) =>
+            CaptureCore(
+                multiplayerSave: false,
+                campaignMapId: 0,
+                eventTrailType: NoGameValue,
+                editorLoad: args != null && args.LoadingEditorMap);
+
+        internal static bool AllowsCustomGameMods(
+            GameModeKind kind,
+            GameModeLaunchVariant launchVariant)
+        {
+            if (kind == GameModeKind.CustomGame)
+                return true;
+            if (launchVariant == GameModeLaunchVariant.Standard)
+                return false;
+
+            return kind == GameModeKind.VanillaTrail ||
+                kind == GameModeKind.CustomTrail ||
+                kind == GameModeKind.CoopTrail ||
+                kind == GameModeKind.SandsOfTime;
+        }
+
+        internal static bool AllowsRegularGameplayMods(
+            GameModeKind kind,
+            GameModeLaunchVariant launchVariant) =>
+            kind == GameModeKind.MapEditor || AllowsCustomGameMods(kind, launchVariant);
+
+        private static GameModeSnapshot CaptureCore(
+            bool multiplayerSave,
+            int campaignMapId,
+            int eventTrailType,
+            bool editorLoad)
         {
             Director director = Director.instance;
             GameData gameData = GameData.Instance;
@@ -58,9 +163,16 @@ namespace Shared
                 realLobbyMembers > 0 ||
                 realNetworkGameMembers > 0;
 
-            int gameType = gameData != null ? gameData.game_type : -1;
-            int skirmishGameType = gameData != null ? gameData.SkirmishGameType : -1;
-            bool mapEditor = IsMapEditor();
+            int gameType = gameData != null ? gameData.game_type : NoGameValue;
+            int skirmishGameType = gameData != null ? gameData.SkirmishGameType : NoGameValue;
+            int skirmishTrailType = gameData != null ? gameData.SkirmishTrailType : NoGameValue;
+            int coopTrailId = gameData != null ? gameData.coopTrailID : NoGameValue;
+            bool mapEditor =
+                editorLoad ||
+                gameData?.mapType == Enums.GameModes.MAP_EDITOR ||
+                IsMapEditor();
+            bool sandsOfTime = TryIsSandsOfTime(gameData);
+            bool customTrailRestart = TryCaptureCustomTrailRestart();
             // game_type 3 is Vanilla's skirmish family. Immediately after leaving a
             // real multiplayer game, a new local skirmish can reach OnStartMap before
             // Vanilla changes SkirmishGameType from -1. Its all-local skirmish lobby
@@ -73,14 +185,69 @@ namespace Shared
             bool singleplayerSkirmishMode =
                 !realMultiplayer &&
                 !mapEditor &&
-                gameType == 3 &&
+                gameType == (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER &&
                 (skirmishGameType >= 0 || localSkirmishTransition);
 
+            bool vanillaCustomized = TryCaptureVanillaCustomizedTrail(
+                out int customizedTrailType,
+                out int customizedTrailId);
+            ExternalCustomizedOrigin externalOrigin = CaptureExternalCustomizedOrigin();
+            GameModeKind observedKind = ResolveKind(
+                mapEditor,
+                gameType,
+                skirmishGameType,
+                skirmishTrailType,
+                coopTrailId,
+                campaignMapId,
+                eventTrailType);
+            GameModeKind kind = observedKind;
+            if (observedKind == GameModeKind.CustomGame && externalOrigin.LaunchPending)
+            {
+                GameModeKind originKind = ResolveExternalOriginKind(externalOrigin.Origin);
+                if (originKind != GameModeKind.Unknown)
+                    kind = originKind;
+            }
+            if (sandsOfTime && kind != GameModeKind.MapEditor && kind != GameModeKind.CoopTrail)
+                kind = GameModeKind.SandsOfTime;
+            else if (customTrailRestart && (kind == GameModeKind.Unknown || kind == GameModeKind.CustomGame))
+                kind = GameModeKind.CustomTrail;
+            if (vanillaCustomized && customizedTrailId >= 0 && kind == GameModeKind.CustomGame)
+            {
+                bool builtInOriginRequired = externalOrigin.SupportsBuiltInOrigins;
+                if (IsVanillaTrailType(customizedTrailType) &&
+                    (!builtInOriginRequired || externalOrigin.Origin == ExternalCustomizedOrigin.VanillaTrail))
+                    kind = GameModeKind.VanillaTrail;
+                else if (IsSandsTrailType(customizedTrailType) &&
+                    (!builtInOriginRequired || externalOrigin.Origin == ExternalCustomizedOrigin.SandsOfTime))
+                    kind = GameModeKind.SandsOfTime;
+            }
+            GameModeLaunchVariant launchVariant = ResolveLaunchVariant(
+                kind,
+                vanillaCustomized,
+                customizedTrailType,
+                customizedTrailId,
+                observedKind == GameModeKind.CustomGame,
+                externalOrigin);
+            bool conflictingOrigin = externalOrigin.IsInvalid ||
+                (externalOrigin.Origin != ExternalCustomizedOrigin.None &&
+                 (!ExternalOriginMatchesKind(externalOrigin, kind) ||
+                  !ExternalOriginMatchesEvidence(
+                      externalOrigin,
+                      kind,
+                      skirmishTrailType,
+                      coopTrailId,
+                      eventTrailType,
+                      vanillaCustomized,
+                      customizedTrailType,
+                      customizedTrailId)));
+
             return new GameModeSnapshot(
                 realMultiplayer,
                 singleplayerSkirmishMode,
-                singleplayerSkirmishMode && skirmishGameType == 0,
-                singleplayerSkirmishMode && (skirmishGameType == 1 || skirmishGameType == 2),
+                singleplayerSkirmishMode && skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM,
+                singleplayerSkirmishMode &&
+                    (skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_TRAIL ||
+                     skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM_TRAIL),
                 mapEditor,
                 multiplayerSave,
                 director != null,
@@ -95,13 +262,275 @@ namespace Shared
                 realNetworkGameMembers,
                 gameType,
                 skirmishGameType,
-                gameData != null ? gameData.coopTrailID : -1);
+                skirmishTrailType,
+                coopTrailId,
+                kind,
+                launchVariant,
+                campaignMapId,
+                eventTrailType,
+                externalOrigin.Origin != ExternalCustomizedOrigin.None
+                    ? externalOrigin.TrailId
+                    : customizedTrailId,
+                externalOrigin.Origin != ExternalCustomizedOrigin.None
+                    ? externalOrigin.MissionId
+                    : customizedTrailId,
+                externalOrigin.Origin,
+                conflictingOrigin);
+        }
+
+        internal static GameModeKind ResolveKind(
+            bool mapEditor,
+            int gameType,
+            int skirmishGameType,
+            int skirmishTrailType,
+            int coopTrailId,
+            int campaignMapId = 0,
+            int eventTrailType = NoGameValue)
+        {
+            if (mapEditor)
+                return GameModeKind.MapEditor;
+            if (gameType == (int)Enums.eGameTypeModes.GAMETYPE_CAMPAIGN || campaignMapId > 0)
+                return GameModeKind.Campaign;
+            if (gameType == (int)Enums.eGameTypeModes.GAMETYPE_MAP)
+                return GameModeKind.StandaloneMission;
+            if (coopTrailId > NoCoopTrail)
+                return GameModeKind.CoopTrail;
+
+            bool hasTrailEvent = eventTrailType >= 0;
+            int effectiveTrailType = hasTrailEvent ? eventTrailType : skirmishTrailType;
+            bool vanillaTrailMode =
+                skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_TRAIL;
+            if ((vanillaTrailMode || hasTrailEvent) && IsSandsTrailType(effectiveTrailType))
+                return GameModeKind.SandsOfTime;
+            if ((vanillaTrailMode || hasTrailEvent) && IsVanillaTrailType(effectiveTrailType))
+            {
+                return GameModeKind.VanillaTrail;
+            }
+            if (skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM_TRAIL)
+                return GameModeKind.CustomTrail;
+            if (gameType == (int)Enums.eGameTypeModes.GAMETYPE_MULTIPLAYER &&
+                skirmishGameType == (int)Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM &&
+                coopTrailId == NoCoopTrail)
+            {
+                return GameModeKind.CustomGame;
+            }
+            return GameModeKind.Unknown;
+        }
+
+        internal static GameModeLaunchVariant ResolveLaunchVariant(
+            GameModeKind kind,
+            bool vanillaCustomized,
+            int customizedTrailType,
+            int customizedTrailId,
+            bool vanillaCustomGameContext,
+            ExternalCustomizedOrigin externalOrigin)
+        {
+            bool vanillaMatches = vanillaCustomized &&
+                vanillaCustomGameContext &&
+                customizedTrailId >= 0 &&
+                (!externalOrigin.SupportsBuiltInOrigins ||
+                 ExternalOriginMatchesKind(externalOrigin, kind)) &&
+                ((kind == GameModeKind.VanillaTrail && IsVanillaTrailType(customizedTrailType)) ||
+                 (kind == GameModeKind.SandsOfTime && IsSandsTrailType(customizedTrailType)));
+            bool externalMatches =
+                ExternalOriginMatchesKind(externalOrigin, kind) &&
+                (kind != GameModeKind.CustomGame || externalOrigin.LaunchPending);
+            if (!vanillaMatches && !externalMatches)
+                return GameModeLaunchVariant.Standard;
+            return externalMatches && externalOrigin.RestoredFromSave
+                ? GameModeLaunchVariant.RestoredCustomizedSave
+                : GameModeLaunchVariant.Customized;
+        }
+
+        private static bool IsVanillaTrailType(int value) =>
+            value >= (int)GameTrailType.FirstEdition && value <= (int)GameTrailType.Extreme;
+
+        private static bool IsSandsTrailType(int value) =>
+            value >= (int)GameTrailType.SandsOne && value <= (int)GameTrailType.SandsEight;
+
+        private static GameModeKind ResolveExternalOriginKind(int origin)
+        {
+            switch (origin)
+            {
+                case ExternalCustomizedOrigin.CustomTrail: return GameModeKind.CustomTrail;
+                case ExternalCustomizedOrigin.CoopTrail: return GameModeKind.CoopTrail;
+                case ExternalCustomizedOrigin.VanillaTrail: return GameModeKind.VanillaTrail;
+                case ExternalCustomizedOrigin.SandsOfTime: return GameModeKind.SandsOfTime;
+                default: return GameModeKind.Unknown;
+            }
+        }
+
+        private static bool ExternalOriginMatchesKind(ExternalCustomizedOrigin origin, GameModeKind kind) =>
+            (origin.Origin == ExternalCustomizedOrigin.CustomTrail && kind == GameModeKind.CustomTrail) ||
+            (origin.Origin == ExternalCustomizedOrigin.CoopTrail && kind == GameModeKind.CoopTrail) ||
+            (origin.Origin == ExternalCustomizedOrigin.VanillaTrail && kind == GameModeKind.VanillaTrail) ||
+            (origin.Origin == ExternalCustomizedOrigin.SandsOfTime && kind == GameModeKind.SandsOfTime);
+
+        internal static bool ExternalOriginMatchesEvidence(
+            ExternalCustomizedOrigin origin,
+            GameModeKind kind,
+            int skirmishTrailType,
+            int coopTrailId,
+            int eventTrailType,
+            bool vanillaCustomized,
+            int customizedTrailType,
+            int customizedTrailId)
+        {
+            if (kind == GameModeKind.CoopTrail && coopTrailId > NoCoopTrail)
+                return origin.TrailId + 1 == coopTrailId;
+            if (kind != GameModeKind.VanillaTrail && kind != GameModeKind.SandsOfTime)
+                return true;
+
+            int observedTrailType = eventTrailType >= 0 ? eventTrailType : skirmishTrailType;
+            if (observedTrailType >= 0 && origin.TrailType != observedTrailType)
+                return false;
+            if (!vanillaCustomized)
+                return true;
+            return origin.TrailType == customizedTrailType &&
+                origin.MissionId == customizedTrailId;
+        }
+
+        private static bool TryCaptureVanillaCustomizedTrail(out int trailType, out int trailId)
+        {
+#if SHARED_PRESET_TESTS
+            trailType = NoGameValue;
+            trailId = NoGameValue;
+            return false;
+#else
+            trailType = FRONT_Multiplayer.customizedTrailType;
+            trailId = FRONT_Multiplayer.customizedTrailID;
+            return FRONT_Multiplayer.customizedTrail;
+#endif
+        }
+
+        private static bool TryIsSandsOfTime(GameData gameData)
+        {
+            try
+            {
+                return gameData?.IsSandsOfTime() == true;
+            }
+            catch
+            {
+                return false;
+            }
+        }
+
+        private static bool TryCaptureCustomTrailRestart()
+        {
+#if SHARED_PRESET_TESTS
+            return false;
+#else
+            if (!MainViewModel.viewModelLoaded)
+                return false;
+            try
+            {
+                return MainViewModel.Instance?.HUDIngameMenu?.restartSkirmishMapInfo?.customTrail == true;
+            }
+            catch
+            {
+                return false;
+            }
+#endif
+        }
+
+        private static ExternalCustomizedOrigin CaptureExternalCustomizedOrigin()
+        {
+#if SHARED_PRESET_TESTS
+            return default;
+#else
+            try
+            {
+                Type api = Type.GetType(
+                    "CustomCustomTrail.CustomCustomTrailLaunchOriginApi, CustomCustomTrail",
+                    throwOnError: false);
+                if (api == null)
+                    return default;
+                if (!TryReadStaticInt(api, "ApiVersion", out int apiVersion) ||
+                    !TryReadStaticInt(api, "Origin", out int origin))
+                {
+                    return ExternalCustomizedOrigin.InvalidProvider;
+                }
+                if (apiVersion < MinimumOriginApiVersion || apiVersion > SupportedOriginApiVersion)
+                    return ExternalCustomizedOrigin.InvalidProvider;
+                if (origin == ExternalCustomizedOrigin.None)
+                    return ExternalCustomizedOrigin.AvailableProvider(apiVersion >= 2);
+                bool knownOrigin = origin == ExternalCustomizedOrigin.CustomTrail ||
+                    origin == ExternalCustomizedOrigin.CoopTrail ||
+                    (apiVersion >= 2 && (origin == ExternalCustomizedOrigin.VanillaTrail ||
+                                         origin == ExternalCustomizedOrigin.SandsOfTime));
+                if (!knownOrigin)
+                    return ExternalCustomizedOrigin.InvalidProvider;
+                bool launchPending = false;
+                if (!TryReadStaticInt(api, "TrailType", out int trailType) ||
+                    !TryReadStaticInt(api, "TrailId", out int trailId) ||
+                    !TryReadStaticInt(api, "MissionId", out int missionId) ||
+                    !TryReadStaticBool(api, "RestoredFromSave", out bool restoredFromSave) ||
+                    (apiVersion >= 2 && !TryReadStaticBool(api, "LaunchPending", out launchPending)))
+                {
+                    return ExternalCustomizedOrigin.InvalidProvider;
+                }
+                var result = new ExternalCustomizedOrigin(
+                    origin,
+                    trailType,
+                    trailId,
+                    missionId,
+                    restoredFromSave,
+                    launchPending,
+                    supportsBuiltInOrigins: apiVersion >= 2);
+                if ((result.Origin == ExternalCustomizedOrigin.CustomTrail &&
+                        (result.MissionId < FirstMissionId ||
+                         result.TrailId < FirstCustomTrailId || result.TrailId > LastCustomTrailId)) ||
+                    (result.Origin == ExternalCustomizedOrigin.CoopTrail &&
+                        (result.MissionId < FirstMissionId ||
+                         result.TrailId < FirstCoopTrailId || result.TrailId > LastCoopTrailId ||
+                         result.MissionId > LastCoopMissionId)) ||
+                    (result.Origin == ExternalCustomizedOrigin.VanillaTrail &&
+                        (!IsVanillaTrailType(result.TrailType) || result.TrailId < 0 || result.MissionId < 0)) ||
+                    (result.Origin == ExternalCustomizedOrigin.SandsOfTime &&
+                        (!IsSandsTrailType(result.TrailType) || result.TrailId < 0 || result.MissionId < 0)))
+                {
+                    return ExternalCustomizedOrigin.InvalidProvider;
+                }
+                return result;
+            }
+            catch
+            {
+                // CustomCustomTrail is optional; invalid providers must never enable gameplay mods.
+                return ExternalCustomizedOrigin.InvalidProvider;
+            }
+#endif
+        }
+
+        private static bool TryReadStaticInt(Type type, string name, out int result)
+        {
+            result = NoGameValue;
+            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
+            if (property == null || property.GetIndexParameters().Length != 0)
+                return false;
+            object value = property.GetValue(null);
+            if (value == null)
+                return false;
+            result = Convert.ToInt32(value);
+            return true;
+        }
+
+        private static bool TryReadStaticBool(Type type, string name, out bool result)
+        {
+            result = false;
+            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
+            if (property == null || property.PropertyType != typeof(bool) ||
+                property.GetIndexParameters().Length != 0)
+            {
+                return false;
+            }
+            result = (bool)property.GetValue(null);
+            return true;
         }
 
         public static bool IsRealMultiplayer(bool multiplayerSave = false) =>
             Capture(multiplayerSave).IsRealMultiplayer;
 
-        // Subtype 0 is a normal skirmish. Subtypes 1 and 2 are Vanilla and custom Trails.
+        // Keep the legacy property contract while deriving it from Vanilla's named subtype enum.
         public static bool IsSingleplayerSkirmish(bool multiplayerSave = false) =>
             Capture(multiplayerSave).IsSingleplayerSkirmish;
 
@@ -140,6 +569,52 @@ namespace Shared
         }
     }
 
+    internal readonly struct ExternalCustomizedOrigin
+    {
+        internal const int None = 0;
+        internal const int CustomTrail = 1;
+        internal const int CoopTrail = 2;
+        internal const int VanillaTrail = 3;
+        internal const int SandsOfTime = 4;
+
+        internal static ExternalCustomizedOrigin InvalidProvider =>
+            new ExternalCustomizedOrigin(-1, -1, -1, -1, false, false, isInvalid: true);
+
+        internal static ExternalCustomizedOrigin AvailableProvider(bool supportsBuiltInOrigins) =>
+            new ExternalCustomizedOrigin(
+                None, -1, -1, -1, false, false,
+                supportsBuiltInOrigins: supportsBuiltInOrigins);
+
+        internal ExternalCustomizedOrigin(
+            int origin,
+            int trailType,
+            int trailId,
+            int missionId,
+            bool restoredFromSave,
+            bool launchPending = false,
+            bool isInvalid = false,
+            bool supportsBuiltInOrigins = false)
+        {
+            Origin = origin;
+            TrailType = trailType;
+            TrailId = trailId;
+            MissionId = missionId;
+            RestoredFromSave = restoredFromSave;
+            LaunchPending = launchPending;
+            IsInvalid = isInvalid;
+            SupportsBuiltInOrigins = supportsBuiltInOrigins;
+        }
+
+        internal int Origin { get; }
+        internal int TrailType { get; }
+        internal int TrailId { get; }
+        internal int MissionId { get; }
+        internal bool RestoredFromSave { get; }
+        internal bool LaunchPending { get; }
+        internal bool IsInvalid { get; }
+        internal bool SupportsBuiltInOrigins { get; }
+    }
+
     internal readonly struct PlayerIdentityResolution
     {
         internal PlayerIdentityResolution(int playerId, bool isResolved, string error, string diagnostic)
@@ -609,7 +1084,16 @@ namespace Shared
             int realNetworkGameMembers,
             int gameType,
             int skirmishGameType,
-            int coopTrailId)
+            int skirmishTrailType,
+            int coopTrailId,
+            GameModeKind kind,
+            GameModeLaunchVariant launchVariant,
+            int campaignMapId,
+            int eventTrailType,
+            int customizedTrailId,
+            int customizedMissionId,
+            int customizedOriginKind,
+            bool hasConflictingCustomizedOrigin)
         {
             IsRealMultiplayer = isRealMultiplayer;
             IsSingleplayerSkirmishMode = isSingleplayerSkirmishMode;
@@ -629,7 +1113,16 @@ namespace Shared
             RealNetworkGameMembers = realNetworkGameMembers;
             GameType = gameType;
             SkirmishGameType = skirmishGameType;
+            SkirmishTrailType = skirmishTrailType;
             CoopTrailId = coopTrailId;
+            Kind = kind;
+            LaunchVariant = launchVariant;
+            CampaignMapId = campaignMapId;
+            EventTrailType = eventTrailType;
+            CustomizedTrailId = customizedTrailId;
+            CustomizedMissionId = customizedMissionId;
+            CustomizedOriginKind = customizedOriginKind;
+            HasConflictingCustomizedOrigin = hasConflictingCustomizedOrigin;
         }
 
         public bool IsRealMultiplayer { get; }
@@ -650,7 +1143,66 @@ namespace Shared
         public int RealNetworkGameMembers { get; }
         public int GameType { get; }
         public int SkirmishGameType { get; }
+        public int SkirmishTrailType { get; }
         public int CoopTrailId { get; }
+        public GameModeKind Kind { get; }
+        public GameModeLaunchVariant LaunchVariant { get; }
+        public bool IsCustomized => LaunchVariant != GameModeLaunchVariant.Standard;
+        public bool IsMissionContent =>
+            Kind == GameModeKind.Campaign ||
+            Kind == GameModeKind.StandaloneMission ||
+            Kind == GameModeKind.VanillaTrail ||
+            Kind == GameModeKind.CustomTrail ||
+            Kind == GameModeKind.CoopTrail ||
+            Kind == GameModeKind.SandsOfTime;
+        public bool AllowsCustomGameMods =>
+            !HasConflictingCustomizedOrigin && GameModeHelper.AllowsCustomGameMods(Kind, LaunchVariant);
+        public bool AllowsRegularGameplayMods =>
+            !HasConflictingCustomizedOrigin &&
+            GameModeHelper.AllowsRegularGameplayMods(Kind, LaunchVariant);
+        public int CampaignMapId { get; }
+        public int EventTrailType { get; }
+        public int CustomizedTrailId { get; }
+        public int CustomizedMissionId { get; }
+        public int CustomizedOriginKind { get; }
+        public bool HasConflictingCustomizedOrigin { get; }
+
+#if SHARED_PRESET_TESTS
+        internal GameModeSnapshot WithModeEvidenceForTests(
+            GameModeKind kind,
+            GameModeLaunchVariant launchVariant,
+            int eventTrailType,
+            bool hasConflictingCustomizedOrigin = false) =>
+            new GameModeSnapshot(
+                IsRealMultiplayer,
+                IsSingleplayerSkirmishMode,
+                IsSingleplayerSkirmish,
+                IsSingleplayerTrail,
+                IsMapEditor,
+                MultiplayerSave,
+                DirectorAvailable,
+                DirectorMultiplayer,
+                DirectorSkirmish,
+                LowLevelNetworked,
+                PlatformMultiplayer,
+                LobbyMembers,
+                RealLobbyMembers,
+                SkirmishLobbyMembers,
+                GameMembers,
+                RealNetworkGameMembers,
+                GameType,
+                SkirmishGameType,
+                SkirmishTrailType,
+                CoopTrailId,
+                kind,
+                launchVariant,
+                CampaignMapId,
+                eventTrailType,
+                CustomizedTrailId,
+                CustomizedMissionId,
+                CustomizedOriginKind,
+                hasConflictingCustomizedOrigin);
+#endif
 
         public string ToDiagnosticString()
         {
@@ -663,7 +1215,13 @@ namespace Shared
                 $"lobbyMembers={LobbyMembers}, realLobbyMembers={RealLobbyMembers}, " +
                 $"skirmishLobbyMembers={SkirmishLobbyMembers}, " +
                 $"gameMembers={GameMembers}, realNetworkGameMembers={RealNetworkGameMembers}, " +
-                $"gameType={GameType}, skirmishGameType={SkirmishGameType}, coopTrailId={CoopTrailId}";
+                $"gameType={GameType}, skirmishGameType={SkirmishGameType}, skirmishTrailType={SkirmishTrailType}, " +
+                $"coopTrailId={CoopTrailId}, kind={Kind}, launchVariant={LaunchVariant}, " +
+                $"allowsCustomGameMods={AllowsCustomGameMods}, " +
+                $"allowsRegularGameplayMods={AllowsRegularGameplayMods}, campaignMapId={CampaignMapId}, " +
+                $"eventTrailType={EventTrailType}, customizedTrailId={CustomizedTrailId}, " +
+                $"customizedMissionId={CustomizedMissionId}, customizedOriginKind={CustomizedOriginKind}, " +
+                $"conflictingCustomizedOrigin={HasConflictingCustomizedOrigin}";
         }
     }
 }

diff --git a/Shared/GameplayFeatureModePolicy.cs b/Shared/GameplayFeatureModePolicy.cs
new file mode 100644
index 00000000..e29dc777
--- /dev/null
+++ b/Shared/GameplayFeatureModePolicy.cs
@@ -0,0 +1,277 @@
+using BepInEx.Logging;
+using System;
+using System.Collections.Generic;
+
+namespace Shared
+{
+    internal enum GameplayFeatureId
+    {
+        BuildingCostTooltip,
+        BuildingLimitEnforcement,
+        UnitCostEnforcement,
+        UnitLimitEnforcement,
+        LordHealthMultipliers,
+        EndlessExtremePowersRecharge,
+        RandomEventsRuntime,
+        ImprovedHunterTargetSelection,
+        ImprovedHunterPathfinding,
+        CastleSpawning,
+        FreeCastlePreview,
+        CastleBlueprints,
+    }
+
+    internal readonly struct GameplayFeatureActivationProfile
+    {
+        internal GameplayFeatureActivationProfile(
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
+        internal string ModGuid { get; }
+        internal GameplayFeatureId FeatureId { get; }
+        internal GameplayModAllowedContext AllowedContexts { get; }
+        internal bool AllowRealMultiplayer { get; }
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
+        internal static GameplayFeatureActivationProfile GetProfile(
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
+        internal static bool IsAllowed(
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
+        internal static bool IsAllowed(
+            GameplayFeatureActivationProfile profile,
+            GameModeSnapshot snapshot,
+            out string reason)
+        {
+            if (snapshot.HasConflictingCustomizedOrigin)
+            {
+                reason = "conflicting-customize-origin";
+                return false;
+            }
+
+            GameplayModAllowedContext context = GameplayModModePolicy.ResolveContext(snapshot);
+            if (context == GameplayModAllowedContext.None)
+            {
+                reason = snapshot.Kind == GameModeKind.Unknown
+                    ? "unknown-fail-closed"
+                    : "owning-mod-context-not-allowed";
+                return false;
+            }
+
+            if ((profile.AllowedContexts & context) != context)
+            {
+                reason = context == GameplayModAllowedContext.MapEditor
+                    ? "feature-not-supported-in-map-editor"
+                    : "feature-context-not-allowed";
+                return false;
+            }
+
+            if (snapshot.IsRealMultiplayer && !profile.AllowRealMultiplayer)
+            {
+                reason = "feature-not-approved-for-real-multiplayer";
+                return false;
+            }
+
+            reason = "feature-context-allowed";
+            return true;
+        }
+
+        internal static void LogDecisions(
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
+                DebugLogHelper.LogInfo(
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
+#if SHARED_PRESET_TESTS
+        internal static bool RecordDecisionForTests(GameplayFeatureId featureId, bool allowed) =>
+            RecordDecision(featureId, allowed);
+
+        internal static void ResetLoggedDecisionsForTests()
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

diff --git a/Shared/GameplayModActivationGate.cs b/Shared/GameplayModActivationGate.cs
new file mode 100644
index 00000000..c7c6e926
--- /dev/null
+++ b/Shared/GameplayModActivationGate.cs
@@ -0,0 +1,212 @@
+using BepInEx.Logging;
+using System;
+#if !SHARED_PRESET_TESTS
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
+        private static GameModeSnapshot snapshot;
+        private static volatile bool isAllowed;
+        private static bool initialized;
+        private static bool hasAuthoritativeLoadEvidence;
+        private static GameModeSnapshot authoritativeLoadSnapshot;
+#if !SHARED_PRESET_TESTS
+        private static IDisposable mapLoadSubscription;
+        private static IDisposable loadSaveSubscription;
+        private static IDisposable mapStartSubscription;
+        private static IDisposable mapUnloadSubscription;
+#endif
+
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
+            Func<bool> isConfiguredEnabled)
+        {
+            if (initialized)
+                return;
+
+            log = logger;
+            profile = GameplayModModePolicy.GetProfile(modGuid, displayName);
+            configuredEnabledProvider = isConfiguredEnabled ?? throw new ArgumentNullException(nameof(isConfiguredEnabled));
+
+#if !SHARED_PRESET_TESTS
+            // Register before the mod's own handlers. Castle spawning and similar
+            // native work already begins in OnStartMap(Pre).
+            mapLoadSubscription = MapLoaderR3EventHooks.OnLoadMap.Observable
+                .Subscribe(args => UpdateLoad(GameModeHelper.Capture(args), $"OnLoadMap({args.Phase})"));
+            loadSaveSubscription = MapLoaderR3EventHooks.OnLoadSave.Observable
+                .Where(args => args.Phase == EventHookPhase.Post)
+                .Subscribe(args => UpdateLoad(GameModeHelper.Capture(args), $"OnLoadSave({args.Phase})"));
+            mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable
+                .Subscribe(args => UpdateStart(GameModeHelper.Capture(args), $"OnStartMap({args.Phase})"));
+            mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable
+                .Subscribe(args =>
+                {
+                    if (args.Phase == EventHookPhase.Pre)
+                        Reset("OnUnloadMap(Pre)");
+                });
+#endif
+            initialized = true;
+            GameModeSnapshot current = GameModeHelper.Capture();
+            if (current.Kind == GameModeKind.MapEditor)
+                UpdateLoad(current, "initial-current-editor");
+            else
+                LogTransition("initialization");
+        }
+
+        private static void UpdateLoad(GameModeSnapshot next, string source)
+        {
+            if (hasAuthoritativeLoadEvidence)
+                next = MergeWithAuthoritativeLoad(next);
+            if (HasAuthoritativeLoadEvidence(next))
+            {
+                authoritativeLoadSnapshot = next;
+                hasAuthoritativeLoadEvidence = true;
+            }
+            Update(next, source);
+        }
+
+        private static bool HasAuthoritativeLoadEvidence(GameModeSnapshot candidate) =>
+            candidate.Kind == GameModeKind.MapEditor ||
+            candidate.CampaignMapId > 0 ||
+            candidate.EventTrailType >= 0 ||
+            (candidate.Kind == GameModeKind.CoopTrail && candidate.CoopTrailId > 0) ||
+            (candidate.Kind == GameModeKind.CustomTrail &&
+             candidate.SkirmishGameType ==
+                 (int)global::Enums.eSkirmishGameMode.SKIRMISH_GAME_CUSTOM_TRAIL) ||
+            (candidate.IsMissionContent && candidate.IsCustomized);
+
+        private static void UpdateStart(GameModeSnapshot next, string source)
+        {
+            if (hasAuthoritativeLoadEvidence)
+                next = MergeWithAuthoritativeLoad(next);
+            Update(next, source);
+        }
+
+        private static GameModeSnapshot MergeWithAuthoritativeLoad(GameModeSnapshot next)
+        {
+            if (authoritativeLoadSnapshot.Kind == GameModeKind.MapEditor)
+                return authoritativeLoadSnapshot;
+            if ((next.Kind == GameModeKind.CustomGame || next.Kind == GameModeKind.Unknown) &&
+                authoritativeLoadSnapshot.IsMissionContent)
+            {
+                return authoritativeLoadSnapshot;
+            }
+            if (next.Kind == authoritativeLoadSnapshot.Kind &&
+                authoritativeLoadSnapshot.IsCustomized && !next.IsCustomized)
+            {
+                return authoritativeLoadSnapshot;
+            }
+            return next;
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
+            snapshot = next;
+            isAllowed = GameplayModModePolicy.IsAllowed(profile, next, out _);
+            if (changed)
+                LogTransition(source);
+            if (previousAllowed != isAllowed)
+                NotifyStateChanged(isAllowed);
+        }
+
+        private static void Reset(string source)
+        {
+            bool changed = snapshot.Kind != GameModeKind.Unknown ||
+                snapshot.LaunchVariant != GameModeLaunchVariant.Standard;
+            bool previousAllowed = isAllowed;
+            isAllowed = false;
+            snapshot = default;
+            hasAuthoritativeLoadEvidence = false;
+            authoritativeLoadSnapshot = default;
+            if (changed)
+                LogTransition(source);
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
+        private static void LogTransition(string source)
+        {
+            bool configuredEnabled = ReadConfiguredEnabled();
+            bool effectiveEnabled = configuredEnabled && IsAllowed;
+            GameplayModModePolicy.IsAllowed(profile, snapshot, out string reason);
+            string action = effectiveEnabled
+                ? "enabled"
+                : !IsAllowed ? "disabled-by-mode" : "restriction-lifted-setting-disabled";
+            DebugLogHelper.LogInfo(
+                log,
+                $"[{profile.DisplayName}] gameplay-mod gate: modGuid={profile.ModGuid}, source={source}, " +
+                $"kind={snapshot.Kind}, launchVariant={snapshot.LaunchVariant}, " +
+                $"customized={snapshot.IsCustomized}, customizedOrigin={snapshot.CustomizedOriginKind}, " +
+                $"modeAllowed={IsAllowed}, configuredEnabled={configuredEnabled}, " +
+                $"effectiveEnabled={effectiveEnabled}, action={action}, reason={reason}.");
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
+#if SHARED_PRESET_TESTS
+        internal static void SetSnapshotForTests(GameModeSnapshot next) => Update(next, "test");
+        internal static void SetLoadSnapshotForTests(GameModeSnapshot next) => UpdateLoad(next, "test-load");
+        internal static void SetStartSnapshotForTests(GameModeSnapshot next) => UpdateStart(next, "test-start");
+        internal static void ResetForTests()
+        {
+            profile = GameplayModModePolicy.GetProfile("ExtraFeatures_Serp", "Extra Features");
+            configuredEnabledProvider = () => true;
+            Reset("test-reset");
+        }
+#endif
+    }
+}

diff --git a/Shared/GameplayModModePolicy.cs b/Shared/GameplayModModePolicy.cs
new file mode 100644
index 00000000..be28505f
--- /dev/null
+++ b/Shared/GameplayModModePolicy.cs
@@ -0,0 +1,139 @@
+using System;
+
+namespace Shared
+{
+    [Flags]
+    internal enum GameplayModAllowedContext
+    {
+        None = 0,
+        CustomGame = 1 << 0,
+        CustomizedVanillaTrail = 1 << 1,
+        CustomizedCustomTrail = 1 << 2,
+        CustomizedCoopTrail = 1 << 3,
+        CustomizedSandsOfTime = 1 << 4,
+        MapEditor = 1 << 5,
+        Campaign = 1 << 6,
+        StandaloneMission = 1 << 7,
+        VanillaTrail = 1 << 8,
+        CustomTrail = 1 << 9,
+        CoopTrail = 1 << 10,
+        SandsOfTime = 1 << 11,
+    }
+
+    internal readonly struct GameplayModActivationProfile
+    {
+        internal GameplayModActivationProfile(
+            string modGuid,
+            string displayName,
+            GameplayModAllowedContext allowedContexts)
+        {
+            ModGuid = modGuid ?? throw new ArgumentNullException(nameof(modGuid));
+            DisplayName = string.IsNullOrWhiteSpace(displayName) ? modGuid : displayName;
+            AllowedContexts = allowedContexts;
+        }
+
+        internal string ModGuid { get; }
+        internal string DisplayName { get; }
+        internal GameplayModAllowedContext AllowedContexts { get; }
+    }
+
+    /// <summary>Single typed source of truth for mode permissions of regular gameplay mods.</summary>
+    internal static class GameplayModModePolicy
+    {
+        private const GameplayModAllowedContext RegularContexts =
+            GameplayModAllowedContext.CustomGame |
+            GameplayModAllowedContext.CustomizedVanillaTrail |
+            GameplayModAllowedContext.CustomizedCustomTrail |
+            GameplayModAllowedContext.CustomizedCoopTrail |
+            GameplayModAllowedContext.CustomizedSandsOfTime |
+            GameplayModAllowedContext.MapEditor;
+
+        internal static GameplayModActivationProfile GetProfile(string modGuid, string displayName)
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
+                    return Create(modGuid, displayName);
+                default:
+                    throw new ArgumentOutOfRangeException(nameof(modGuid), modGuid, "Unknown gameplay mod GUID.");
+            }
+        }
+
+        internal static bool IsAllowed(
+            GameplayModActivationProfile profile,
+            GameModeSnapshot snapshot,
+            out string reason)
+        {
+            if (snapshot.HasConflictingCustomizedOrigin)
+            {
+                reason = "conflicting-customize-origin";
+                return false;
+            }
+
+            GameplayModAllowedContext context = ResolveContext(snapshot);
+            if (context == GameplayModAllowedContext.None)
+            {
+                reason = snapshot.Kind == GameModeKind.Unknown
+                    ? "unknown-fail-closed"
+                    : snapshot.IsMissionContent ? "direct-mission-content" : "mode-not-allowed";
+                return false;
+            }
+
+            bool allowed = (profile.AllowedContexts & context) == context;
+            reason = allowed ? ToReason(context) : "profile-does-not-allow-" + context;
+            return allowed;
+        }
+
+        private static GameplayModActivationProfile Create(string modGuid, string displayName) =>
+            new GameplayModActivationProfile(modGuid, displayName, RegularContexts);
+
+        internal static GameplayModAllowedContext ResolveContext(GameModeSnapshot snapshot)
+        {
+            if (snapshot.Kind == GameModeKind.MapEditor)
+                return GameplayModAllowedContext.MapEditor;
+            if (snapshot.Kind == GameModeKind.CustomGame && !snapshot.IsCustomized)
+                return GameplayModAllowedContext.CustomGame;
+            if (!snapshot.IsCustomized)
+            {
+                switch (snapshot.Kind)
+                {
+                    case GameModeKind.Campaign: return GameplayModAllowedContext.Campaign;
+                    case GameModeKind.StandaloneMission: return GameplayModAllowedContext.StandaloneMission;
+                    case GameModeKind.VanillaTrail: return GameplayModAllowedContext.VanillaTrail;
+                    case GameModeKind.CustomTrail: return GameplayModAllowedContext.CustomTrail;
+                    case GameModeKind.CoopTrail: return GameplayModAllowedContext.CoopTrail;
+                    case GameModeKind.SandsOfTime: return GameplayModAllowedContext.SandsOfTime;
+                    default: return GameplayModAllowedContext.None;
+                }
+            }
+
+            switch (snapshot.Kind)
+            {
+                case GameModeKind.VanillaTrail: return GameplayModAllowedContext.CustomizedVanillaTrail;
+                case GameModeKind.CustomTrail: return GameplayModAllowedContext.CustomizedCustomTrail;
+                case GameModeKind.CoopTrail: return GameplayModAllowedContext.CustomizedCoopTrail;
+                case GameModeKind.SandsOfTime: return GameplayModAllowedContext.CustomizedSandsOfTime;
+                default: return GameplayModAllowedContext.None;
+            }
+        }
+
+        private static string ToReason(GameplayModAllowedContext context)
+        {
+            if (context == GameplayModAllowedContext.CustomGame)
+                return "custom-game";
+            if (context == GameplayModAllowedContext.MapEditor)
+                return "map-editor";
+            return "verified-customize-origin";
+        }
+    }
+}

diff --git a/Shared/PresetLobbyModSettingsViewModel.cs b/Shared/PresetLobbyModSettingsViewModel.cs
index 80f23efc..ee6f9972 100644
--- a/Shared/PresetLobbyModSettingsViewModel.cs
+++ b/Shared/PresetLobbyModSettingsViewModel.cs
@@ -2025,851 +2025,6 @@ namespace Shared
         }
     }
 
-#if !SHARED_PRESET_TESTS
-    /// <summary>
-    /// TEMPORARY SCRIPT EXTENDER WORKAROUND.
-    /// Remove this class and its registration call once the upstream extender has
-    /// fixed all multiplayer settings paths documented below and the fixes have
-    /// been verified in a real Host/Client run. Revalidate the private method
-    /// signatures, packet routing, and detour behavior after every Extender update;
-    /// this workaround may need adaptation even before it can be removed.
-    /// </summary>
-    internal static class ScriptExtenderMultiplayerSyncWorkaround
-    {
-        private const string AnchorKey =
-            "SerpsMods.Shared.ScriptExtenderMultiplayerSyncWorkaround.v1";
-        private const string PerPlayerAnchorKey =
-            "SerpsMods.Shared.ScriptExtenderPerPlayerIdentityWorkaround.v1";
-        private const string GateKey =
-            "SerpsMods.Shared.ScriptExtenderMultiplayerSyncWorkaround.Gate";
-
-        internal static void EnsureInstalled(ManualLogSource log)
-        {
-            object gate = string.Intern(GateKey);
-            lock (gate)
-            {
-                bool baseInstalled = AppDomain.CurrentDomain.GetData(AnchorKey) != null;
-                bool perPlayerInstalled =
-                    AppDomain.CurrentDomain.GetData(PerPlayerAnchorKey) != null;
-                if (baseInstalled && perPlayerInstalled)
-                    return;
-
-                HookAnchor anchor = null;
-                PerPlayerIdentityHookAnchor perPlayerAnchor = null;
-                try
-                {
-                    if (!baseInstalled)
-                    {
-                        anchor = new HookAnchor(log);
-                        anchor.Install();
-                        AppDomain.CurrentDomain.SetData(AnchorKey, anchor);
-                    }
-                    if (!perPlayerInstalled)
-                    {
-                        perPlayerAnchor = new PerPlayerIdentityHookAnchor(log);
-                        perPlayerAnchor.Install();
-                        AppDomain.CurrentDomain.SetData(
-                            PerPlayerAnchorKey,
-                            perPlayerAnchor);
-                    }
-                    DebugLogHelper.LogInfo(
-                        log,
-                        "Temporary Script Extender multiplayer settings workaround installed " +
-                        "(join snapshot, reliable lobby delivery, in-game sender propagation, " +
-                        "authoritative per-player identity application). " +
-                        "Remove centrally after the upstream fixes are available.");
-                }
-                catch (Exception ex)
-                {
-                    perPlayerAnchor?.RollBack();
-                    anchor?.RollBack();
-                    if (perPlayerAnchor != null)
-                        AppDomain.CurrentDomain.SetData(PerPlayerAnchorKey, null);
-                    if (anchor != null)
-                        AppDomain.CurrentDomain.SetData(AnchorKey, null);
-                    Exception cause = Unwrap(ex);
-                    DebugLogHelper.LogError(
-                        log,
-                        "Temporary Script Extender multiplayer settings workaround could not be " +
-                        $"installed as one transaction: {cause}");
-                    throw new InvalidOperationException(
-                        "Lobby mod settings registration aborted because the required " +
-                        "multiplayer synchronization workaround is unavailable.",
-                        cause);
-                }
-            }
-        }
-
-        private static Exception Unwrap(Exception ex)
-        {
-            return ex is TargetInvocationException invocation && invocation.InnerException != null
-                ? invocation.InnerException
-                : ex;
-        }
-
-        private sealed class HookAnchor
-        {
-            private delegate void SendCustomInfoToMemberDelegate(
-                Platform_Multiplayer instance,
-                Platform_Multiplayer.MPLobbyMember member);
-
-            private delegate void SendPacketToAllLobbyDelegate(
-                Platform_Multiplayer.MPData packet);
-
-            private delegate bool ProcessMessageDelegate(
-                Platform_Multiplayer instance,
-                Platform_Multiplayer.MPData data,
-                Platform_Multiplayer.MPGameMember fromMember,
-                bool fromThread);
-
-            private readonly ManualLogSource log;
-            private object sendCustomInfoDetour;
-            private object sendPacketToAllLobbyDetour;
-            private object processMessageDetour;
-            private MethodInfo handleRawPacketMethod;
-            private Type steamNetworkingIdentityType;
-            private MethodInfo setSteamIdMethod;
-            private MethodInfo sendMessageToUserMethod;
-            private FieldInfo lobbyMemberIdField;
-            private FieldInfo multiplayerInstanceField;
-            private Type steamIdType;
-            private bool loggedReliableReroute;
-            private bool loggedSenderRepair;
-
-            internal HookAnchor(ManualLogSource log)
-            {
-                this.log = log;
-            }
-
-            internal void Install()
-            {
-                MethodInfo sendCustomInfoMethod = RequireMethod(
-                    typeof(Platform_Multiplayer),
-                    nameof(Platform_Multiplayer.SendCustomInfoToMember),
-                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
-                    typeof(Platform_Multiplayer.MPLobbyMember));
-                MethodInfo sendPacketToAllLobbyMethod = typeof(GameNetworkAPI)
-                    .GetMethods(BindingFlags.Static | BindingFlags.Public)
-                    .Single(method =>
-                        method.Name == nameof(GameNetworkAPI.SendPacketToAllLobby) &&
-                        !method.IsGenericMethod &&
-                        method.GetParameters().Length == 1 &&
-                        method.GetParameters()[0].ParameterType == typeof(Platform_Multiplayer.MPData));
-                MethodInfo processMessageMethod = RequireMethod(
-                    typeof(Platform_Multiplayer),
-                    "processMessage",
-                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
-                    typeof(Platform_Multiplayer.MPData),
-                    typeof(Platform_Multiplayer.MPGameMember),
-                    typeof(bool));
-
-                handleRawPacketMethod = typeof(GameNetworkAPI).GetMethod(
-                    "HandleRawPacket",
-                    BindingFlags.Instance | BindingFlags.NonPublic,
-                    null,
-                    new[] { typeof(short), typeof(byte[]), FindNullableSteamIdType() },
-                    null) ?? throw new MissingMethodException(
-                        typeof(GameNetworkAPI).FullName,
-                        "HandleRawPacket(short, byte[], CSteamID?)");
-                steamIdType = Nullable.GetUnderlyingType(
-                    handleRawPacketMethod.GetParameters()[2].ParameterType) ??
-                    throw new InvalidOperationException("HandleRawPacket sender is not nullable.");
-                Assembly steamworksAssembly = steamIdType.Assembly;
-                steamNetworkingIdentityType = steamworksAssembly.GetType(
-                    "Steamworks.SteamNetworkingIdentity",
-                    true);
-                Type steamNetworkingMessagesType = steamworksAssembly.GetType(
-                    "Steamworks.SteamNetworkingMessages",
-                    true);
-                setSteamIdMethod = steamNetworkingIdentityType.GetMethod(
-                    "SetSteamID",
-                    BindingFlags.Instance | BindingFlags.Public,
-                    null,
-                    new[] { steamIdType },
-                    null) ?? throw new MissingMethodException(
-                        steamNetworkingIdentityType.FullName,
-                        "SetSteamID");
-                sendMessageToUserMethod = steamNetworkingMessagesType
-                    .GetMethods(BindingFlags.Static | BindingFlags.Public)
-                    .Single(method =>
-                        method.Name == "SendMessageToUser" &&
-                        method.GetParameters().Length == 5 &&
-                        method.GetParameters()[0].ParameterType.IsByRef &&
-                        method.GetParameters()[1].ParameterType == typeof(IntPtr));
-                lobbyMemberIdField = typeof(Platform_Multiplayer.MPLobbyMember).GetField(
-                    "id",
-                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) ??
-                    throw new MissingFieldException(
-                        typeof(Platform_Multiplayer.MPLobbyMember).FullName,
-                        "id");
-                multiplayerInstanceField = typeof(Platform_Multiplayer).GetField(
-                    "instance",
-                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic) ??
-                    throw new MissingFieldException(
-                        typeof(Platform_Multiplayer).FullName,
-                        "instance");
-
-                DebugLogHelper.LogInfo(
-                    log,
-                    "[MP-SYNC-EVIDENCE BASELINE] " +
-                    $"extenderJoinDetourInstalled={ReadExtenderJoinDetourState()}. " +
-                    "False confirms that the extender declared but did not install its join-sync detour.");
-
-                // TEMPORARY: upstream declares this hook but does not install it.
-                sendCustomInfoDetour = CreateManagedDetour(
-                    typeof(SendCustomInfoToMemberDelegate),
-                    sendCustomInfoMethod,
-                    new SendCustomInfoToMemberDelegate(SendCustomInfoToMemberHook));
-
-                // TEMPORARY: upstream lobby broadcast uses Steam send flag 64, which
-                // is not reliable. Route through its targeted reliable (flag 40) path.
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/CastlePlanner.diff).
