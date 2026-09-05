# RandomEvents release status

**Status:** code newer

- Release: [v1.0.34](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/RandomEvents/v1.0.34)
- Release commit: [01f295a](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/01f295a592b5a6685723231327969dc1d67d46b9)
- Current main commit: [18fd969](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/18fd9693929726316a7478878360d5f98d0770cb)

## Relevant changed files

- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/RandomEvents.dll`
- `RandomEvents/BepInEx/plugins/RandomEvents_Serp/RandomEvents.pdb`
- `RandomEvents/info.json`
- `RandomEvents/RandomEvents.csproj`
- `RandomEvents/src/BanditTargetEligibility.cs`
- `RandomEvents/src/RandomEventsCalendar.cs`
- `RandomEvents/src/RandomEventsChoreSender.cs`
- `RandomEvents/src/RandomEventsPlugin.cs`
- `RandomEvents/src/RandomEventsRuntime.cs`
- `Shared/GameModeHelper.cs`
- `Shared/GameplayFeatureModePolicy.cs`
- `Shared/GameplayModActivationGate.cs`
- `Shared/GameplayModModePolicy.cs`
- `Shared/PresetLobbyModSettingsViewModel.cs`

## Diff

```diff
diff --git a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json
index abcda2a6..3868ce13 100644
--- a/RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json
+++ b/RandomEvents/BepInEx/plugins/RandomEvents_Serp/info.json
@@ -3,10 +3,17 @@
   "Author": "Serpens66",
   "Name": "Random Events",
   "Description": "Runs configurable Vanilla scenario events in skirmishes, Trail missions, and multiplayer games.",
-  "Version": "1.0.34",
+  "Version": "1.0.35",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "Manifest": 1,
+  "NetworkMode": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.35",
+      "Changes": [
+        "Exclude indestructible stockpiles from bandit raid destinations and log each activated group's selected building type."
+      ]
+    },
     {
       "Version": "1.0.34",
       "Changes": [

diff --git a/RandomEvents/info.json b/RandomEvents/info.json
index abcda2a6..3868ce13 100644
--- a/RandomEvents/info.json
+++ b/RandomEvents/info.json
@@ -3,10 +3,17 @@
   "Author": "Serpens66",
   "Name": "Random Events",
   "Description": "Runs configurable Vanilla scenario events in skirmishes, Trail missions, and multiplayer games.",
-  "Version": "1.0.34",
+  "Version": "1.0.35",
   "Website": "https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main",
   "Manifest": 1,
+  "NetworkMode": 1,
   "SerpChangelog": [
+    {
+      "Version": "1.0.35",
+      "Changes": [
+        "Exclude indestructible stockpiles from bandit raid destinations and log each activated group's selected building type."
+      ]
+    },
     {
       "Version": "1.0.34",
       "Changes": [

diff --git a/RandomEvents/RandomEvents.csproj b/RandomEvents/RandomEvents.csproj
index acd67b96..4d834bfd 100644
--- a/RandomEvents/RandomEvents.csproj
+++ b/RandomEvents/RandomEvents.csproj
@@ -26,7 +26,6 @@
     <Reference Include="System.Memory"><HintPath>$(ExtenderDir)\System.Memory.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="MessagePack"><HintPath>$(ExtenderDir)\MessagePack.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="MessagePack.Annotations"><HintPath>$(ExtenderDir)\MessagePack.Annotations.dll</HintPath><Private>false</Private></Reference>
-    <Reference Include="Zhuqiaomon"><HintPath>$(ExtenderDir)\Zhuqiaomon.dll</HintPath><Private>false</Private></Reference>
   </ItemGroup>
   <ItemGroup>
     <Compile Include="..\Shared\ActivePlayerHelper.cs"><Link>Shared\ActivePlayerHelper.cs</Link></Compile>
@@ -34,14 +33,20 @@
     <Compile Include="..\Shared\DebugLogHelper.cs"><Link>Shared\DebugLogHelper.cs</Link></Compile>
     <Compile Include="..\Shared\NativePatternResolver.cs"><Link>Shared\NativePatternResolver.cs</Link></Compile>
     <Compile Include="..\Shared\GameModeHelper.cs"><Link>Shared\GameModeHelper.cs</Link></Compile>
+    <Compile Include="..\Shared\GameplayModActivationGate.cs"><Link>Shared\GameplayModActivationGate.cs</Link></Compile>
+    <Compile Include="..\Shared\GameplayModModePolicy.cs"><Link>Shared\GameplayModModePolicy.cs</Link></Compile>
+    <Compile Include="..\Shared\GameplayFeatureModePolicy.cs"><Link>Shared\GameplayFeatureModePolicy.cs</Link></Compile>
     <Compile Include="..\Shared\SerpLocalization.cs"><Link>Shared\SerpLocalization.cs</Link></Compile>
     <Compile Include="src\RandomEventDefinitions.cs" />
     <Compile Include="src\ArcherSourceNativeLayout.cs" />
+    <Compile Include="src\BanditTargetEligibility.cs" />
     <Compile Include="src\NativeBanditEventSupport.cs" />
     <Compile Include="src\NativeVanillaEventDispatcher.cs" />
     <Compile Include="src\NativeWildlifeEventDispatcher.cs" />
     <Compile Include="src\RandomEventsPlugin.cs" />
     <Compile Include="src\RandomEventsChorePacket.cs" />
+    <Compile Include="src\RandomEventsChoreSender.cs" />
+    <Compile Include="src\RandomEventsCalendar.cs" />
     <Compile Include="src\RandomEventsBatchValidator.cs" />
     <Compile Include="src\RandomEventsCooldownCodec.cs" />
     <Compile Include="src\RandomEventsDiagnostics.cs" />

diff --git a/RandomEvents/src/BanditTargetEligibility.cs b/RandomEvents/src/BanditTargetEligibility.cs
new file mode 100644
index 00000000..62e93ca3
--- /dev/null
+++ b/RandomEvents/src/BanditTargetEligibility.cs
@@ -0,0 +1,18 @@
+using SHCDESE.Interop;
+
+namespace RandomEvents
+{
+    internal static class BanditTargetEligibility
+    {
+        public static bool IsEligibleStructureType(eStructs buildingType)
+        {
+            // Keeps and the indestructible stockpile cannot serve as meaningful raid destinations.
+            return buildingType != eStructs.STRUCT_GOODS_YARD &&
+                   buildingType != eStructs.STRUCT_KEEP_ONE &&
+                   buildingType != eStructs.STRUCT_KEEP_TWO &&
+                   buildingType != eStructs.STRUCT_KEEP_THREE &&
+                   buildingType != eStructs.STRUCT_KEEP_FOUR &&
+                   buildingType != eStructs.STRUCT_KEEP_FIVE;
+        }
+    }
+}

diff --git a/RandomEvents/src/RandomEventsCalendar.cs b/RandomEvents/src/RandomEventsCalendar.cs
new file mode 100644
index 00000000..0a0c2a70
--- /dev/null
+++ b/RandomEvents/src/RandomEventsCalendar.cs
@@ -0,0 +1,20 @@
+using System;
+
+namespace RandomEvents
+{
+    internal static class RandomEventsCalendar
+    {
+        internal const uint MonthsPerYear = 12;
+
+        internal static int ToAbsoluteMonth(uint currentYear, uint currentMonth)
+        {
+            if (currentMonth >= MonthsPerYear)
+                throw new InvalidOperationException($"Unsupported Vanilla calendar values year={currentYear}, month={currentMonth}.");
+
+            ulong absoluteMonth = (ulong)currentYear * MonthsPerYear + currentMonth;
+            if (absoluteMonth > int.MaxValue)
+                throw new InvalidOperationException($"Vanilla calendar value exceeds the Random Events state range: year={currentYear}, month={currentMonth}.");
+            return (int)absoluteMonth;
+        }
+    }
+}

diff --git a/RandomEvents/src/RandomEventsChoreSender.cs b/RandomEvents/src/RandomEventsChoreSender.cs
new file mode 100644
index 00000000..90390a68
--- /dev/null
+++ b/RandomEvents/src/RandomEventsChoreSender.cs
@@ -0,0 +1,51 @@
+using System;
+
+namespace RandomEvents
+{
+    internal static class RandomEventsChoreSender
+    {
+        internal const int MaximumPayloadBytes = 1200;
+
+        internal static bool TrySend<T>(
+            T packet,
+            short packetId,
+            bool packetHookRegistered,
+            Func<T, byte[]> serialize,
+            Func<ulong> getChoreManagerAddress,
+            Action<T, short> sendViaChore,
+            out byte[] body,
+            out string rejectionReason)
+            where T : class
+        {
+            body = Array.Empty<byte>();
+            rejectionReason = null;
+            if (!packetHookRegistered)
+                return Reject("packet hook is not registered", out rejectionReason);
+            if (packet == null || serialize == null || getChoreManagerAddress == null || sendViaChore == null)
+                return Reject("Chore send prerequisites are incomplete", out rejectionReason);
+
+            try
+            {
+                // The public 2.0.2 API serializes this same object again before queuing the Chore.
+                body = serialize(packet) ?? throw new InvalidOperationException("the packet serializer returned null");
+                if (getChoreManagerAddress() == 0)
+                    return Reject("the Chore manager is unavailable", out rejectionReason);
+                if (body.Length > MaximumPayloadBytes - sizeof(short))
+                    return Reject($"payload has {sizeof(short) + body.Length} bytes; limit is {MaximumPayloadBytes}", out rejectionReason);
+
+                sendViaChore(packet, packetId);
+                return true;
+            }
+            catch (Exception ex)
+            {
+                return Reject("Chore send failed: " + ex.Message, out rejectionReason);
+            }
+        }
+
+        private static bool Reject(string reason, out string rejectionReason)
+        {
+            rejectionReason = reason;
+            return false;
+        }
+    }
+}

diff --git a/RandomEvents/src/RandomEventsPlugin.cs b/RandomEvents/src/RandomEventsPlugin.cs
index 26ff4b55..cb251270 100644
--- a/RandomEvents/src/RandomEventsPlugin.cs
+++ b/RandomEvents/src/RandomEventsPlugin.cs
@@ -5,7 +5,7 @@ using System;
 
 namespace RandomEvents
 {
-    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
+    [BepInDependency(ScriptExtenderGuid, "2.0.2")]
     [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
     [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
     public sealed class RandomEventsPlugin : BaseUnityPlugin
@@ -13,7 +13,7 @@ namespace RandomEvents
         private const string ScriptExtenderGuid = "000shcdese";
         public const string PluginGuid = "RandomEvents_Serp";
         public const string PluginName = "Random Events";
-        public const string PluginVersion = "1.0.34";
+        public const string PluginVersion = "1.0.35";
 
         private RandomEventsRuntime runtime;
 
@@ -26,7 +26,7 @@ namespace RandomEvents
             CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
         }
 
-        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
+        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
         {
             try
             {
@@ -68,7 +68,7 @@ namespace RandomEvents
 
             try
             {
-                runtime.InitializeNative(libraryHandle, memory, referenceHashMatches);
+                runtime.InitializeNative(context.ModuleHandle, context.Memory, referenceHashMatches);
             }
             catch (Exception ex)
             {

diff --git a/RandomEvents/src/RandomEventsRuntime.cs b/RandomEvents/src/RandomEventsRuntime.cs
index 1ab51dbc..6e3143e6 100644
--- a/RandomEvents/src/RandomEventsRuntime.cs
+++ b/RandomEvents/src/RandomEventsRuntime.cs
@@ -22,7 +22,6 @@ namespace RandomEvents
     internal sealed class RandomEventsRuntime : IDisposable
     {
         private const string SaveDataIdentifier = "serp-randomevents-state";
-        private const int VanillaMonthsPerYear = 12;
         private const int RabbitSpawnRadius = 12;
         private const int LionSpawnRadius = 12;
         private const int BanditVisualPlayerId = 0;
@@ -34,7 +33,6 @@ namespace RandomEvents
         private const int ChoreProtocolVersion = 2;
         private const int MultiplayerStartupDelayMilliseconds = 5000;
         private const int MultiplayerStartupMinimumTicks = 30;
-        private const int MaximumChorePayloadBytes = 1200;
 
         private readonly ManualLogSource log;
         private readonly RandomEventsSettingsViewModel settings;
@@ -73,6 +71,7 @@ namespace RandomEvents
         private long lastInitializationSendTimestamp;
         private byte[] initializationStateDigest = Array.Empty<byte>();
         private byte[] cachedInitializationBody = Array.Empty<byte>();
+        private RandomEventsInitializationChorePacket cachedInitializationPacket;
         private string cachedInitializationBodyHash = string.Empty;
         private RandomEventsCooldownEncoding cachedInitializationCooldownEncoding;
         private int acceptedInitializationOperationId;
@@ -93,6 +92,8 @@ namespace RandomEvents
         {
             this.log = log;
             this.settings = settings;
+            Shared.GameplayModActivationGate.Initialize(log, RandomEventsPlugin.PluginGuid, RandomEventsPlugin.PluginName, () => settings.EnableMod);
+            Shared.GameplayModActivationGate.StateChanged += OnModeAllowedChanged;
             signpostRegistry = new ScenarioSignpostRegistry(log);
             signpostPlacement = new SignpostPlacementService(log, signpostRegistry);
             nativeEventDispatcher = new NativeVanillaEventDispatcher(log);
@@ -183,6 +184,7 @@ namespace RandomEvents
         public void Dispose()
         {
             if (disposed) return;
+            Shared.GameplayModActivationGate.StateChanged -= OnModeAllowedChanged;
             if (tickSubscribed)
             {
                 GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
@@ -207,6 +209,11 @@ namespace RandomEvents
 
         private void OnStartMap(MapStartEventArgs args)
         {
+            if (!Shared.GameplayModActivationGate.IsAllowed)
+            {
+                ResetMapState();
+                return;
+            }
             mapStartPending = true;
             mapActive = false;
             mapStartedFromMultiplayerSave = args.bMultiplayerSave != 0;
@@ -220,6 +227,12 @@ namespace RandomEvents
             ResetMapState();
         }
 
+        private void OnModeAllowedChanged(bool allowed)
+        {
+            if (!allowed)
+                ResetMapState();
+        }
+
         private void ResetMapState()
         {
             mapStartPending = false;
@@ -243,6 +256,7 @@ namespace RandomEvents
             lastInitializationSendTimestamp = 0;
             initializationStateDigest = Array.Empty<byte>();
             cachedInitializationBody = Array.Empty<byte>();
+            cachedInitializationPacket = null;
             cachedInitializationBodyHash = string.Empty;
             cachedInitializationCooldownEncoding = RandomEventsCooldownEncoding.None;
             acceptedInitializationBodyHash = string.Empty;
@@ -258,6 +272,9 @@ namespace RandomEvents
         {
             try
             {
+                if (!Shared.GameplayModActivationGate.IsAllowed)
+                    return;
+
                 if (mapStartPending)
                 {
                     // OnStartMap(Post) can precede the running simulation. The first positive map tick is late
@@ -323,24 +340,26 @@ namespace RandomEvents
         {
             mapStartPending = false;
 
-            Shared.GameModeSnapshot gameMode = Shared.GameModeHelper.Capture(mapStartedFromMultiplayerSave);
+            Shared.GameModeSnapshot gameMode = Shared.GameplayModActivationGate.Snapshot;
             string gameModeDetails = gameMode.ToDiagnosticString();
-            if (gameMode.IsRealMultiplayer)
+            if (!Shared.GameplayModActivationGate.IsAllowed)
             {
-                InitializeMultiplayerMap(gameModeDetails);
+                LogDebug("Random Events disabled by the gameplay-mode gate: " + gameModeDetails);
+                ResetMapState();
                 return;
             }
-
-            if (gameMode.IsMapEditor)
+            if (gameMode.IsRealMultiplayer)
             {
-                LogDebug("Random Events disabled for map editor session.");
-                state = null;
+                InitializeMultiplayerMap(gameModeDetails);
                 return;
             }
 
-            if (!gameMode.IsSingleplayerSkirmishMode)
+            if (!Shared.GameplayFeatureModePolicy.IsAllowed(
+                RandomEventsPlugin.PluginGuid,
+                Shared.GameplayFeatureId.RandomEventsRuntime,
+                gameMode))
             {
-                LogDebug("Random Events disabled because the map is neither a singleplayer skirmish nor a singleplayer Trail mission.");
+                LogDebug("Random Events disabled for map editor session.");
                 state = null;
                 return;
             }
@@ -356,7 +375,7 @@ namespace RandomEvents
             }
 
             loadedStateAvailable = false;
-            if (!settings.EnableMod)
+            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
             {
                 LogDebug("Random Events disabled by the effective map setting.");
                 state = null;
@@ -374,13 +393,13 @@ namespace RandomEvents
             isLocalHost = GameNetworkAPI.IsLocalHost();
             if (!networkInitialized || initializationChorePacketHook == null || batchChorePacketHook == null ||
                 signpostChorePacketHook == null || initializationAckPacketHook == null ||
-                !ChoreNetworkTransport.IsAvailable)
+                GameGlobalsManager.Instance.ChoreManagerVA == 0)
             {
                 DisableForNetwork("tick-aligned Chore transport is unavailable", gameModeDetails);
                 return;
             }
 
-            if (!settings.EnableMod)
+            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
             {
                 DisableForNetwork("disabled by the effective host setting", gameModeDetails);
                 return;
@@ -475,7 +494,7 @@ namespace RandomEvents
                 chances[index] = Math.Max(0, Math.Min(100, chances[index]));
             return new RandomEventsConfigurationSnapshot
             {
-                Enabled = settings.EnableMod,
+                Enabled = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod),
                 IntervalMonths = Math.Max(1, Math.Min(90, settings.IntervalMonths)),
                 CooldownMonths = Math.Max(0, Math.Min(90, settings.CooldownMonths)),
                 MultiplayerMode = Math.Max(0, Math.Min(1, settings.MultiplayerEventModeIndex)),
@@ -608,11 +627,13 @@ namespace RandomEvents
                 return false;
             if (cachedInitializationBody.Length == 0 && !CreateInitializationAttempt())
                 return false;
-            if (!TrySendRawChore(initializationChorePacketHook.GetPacketId(), cachedInitializationBody, initializationOperationId, "initialization"))
+            if (!TrySendChore(initializationChorePacketHook, cachedInitializationPacket, initializationOperationId, "initialization", out byte[] body))
             {
                 initializationChoreQueued = false;
                 return false;
             }
+            cachedInitializationBody = body;
+            cachedInitializationBodyHash = RandomEventsDiagnostics.HashBytes(body);
             initializationAttemptCount++;
             initializationChoreQueued = true;
             lastInitializationSendTimestamp = Stopwatch.GetTimestamp();
@@ -634,6 +655,7 @@ namespace RandomEvents
             initializationStateDigest = RandomEventsDiagnostics.GetStateDigestBytes(state);
             RandomEventsCooldownPayload[] candidates = RandomEventsCooldownCodec.CreateCandidates(state);
             byte[] smallestBody = null;
+            RandomEventsInitializationChorePacket smallestPacket = null;
             foreach (RandomEventsCooldownPayload cooldown in candidates)
             {
                 var packet = new RandomEventsInitializationChorePacket
@@ -648,10 +670,12 @@ namespace RandomEvents
                 if (smallestBody == null || body.Length < smallestBody.Length)
                 {
                     smallestBody = body;
+                    smallestPacket = packet;
                     cachedInitializationCooldownEncoding = cooldown.Encoding;
                 }
             }
             cachedInitializationBody = smallestBody ?? Array.Empty<byte>();
+            cachedInitializationPacket = smallestPacket;
             cachedInitializationBodyHash = RandomEventsDiagnostics.HashBytes(cachedInitializationBody);
             initializationAcknowledgedPlayerIds.Clear();
             return cachedInitializationBody.Length > 0;
@@ -691,8 +715,7 @@ namespace RandomEvents
                 return false;
             }
 
-            byte[] body = RandomEventsDiagnostics.SerializeAndVerify(packet);
-            if (!TrySendRawChore(batchChorePacketHook.GetPacketId(), body, packet.OperationId, "event batch"))
+            if (!TrySendChore(batchChorePacketHook, packet, packet.OperationId, "event batch", out _))
                 return false;
 
             batchChoreQueued = true;
@@ -706,17 +729,18 @@ namespace RandomEvents
                 ProtocolVersion = ChoreProtocolVersion,
                 OperationId = NextOperationId()
             };
-            byte[] body = RandomEventsDiagnostics.SerializeAndVerify(packet);
-            if (!TrySendRawChore(signpostChorePacketHook.GetPacketId(), body, packet.OperationId, "signpost initialization"))
+            if (!TrySendChore(signpostChorePacketHook, packet, packet.OperationId, "signpost initialization", out _))
                 return false;
 
             signpostChoreQueued = true;
             return true;
         }
 
-        private bool TrySendRawChore(short packetId, byte[] body, int operationId, string label)
+        private bool TrySendChore<T>(R3PacketEventHook<T> packetHook, T packet, int operationId, string label, out byte[] body)
+            where T : class
         {
-            if (!networkInitialized || !ChoreNetworkTransport.IsAvailable)
+            body = Array.Empty<byte>();
+            if (!networkInitialized)
             {
                 LogError($"Random Events {label} refused because the Chore transport is unavailable.");
                 return false;
@@ -731,31 +755,30 @@ namespace RandomEvents
                 return false;
             }
 
-            byte[] blob = new byte[sizeof(short) + body.Length];
-            if (blob.Length > MaximumChorePayloadBytes)
-            {
-                LogError(
-                    $"Random Events {label} refused because its serialized Chore exceeds the Script Extender limit: " +
-                    $"operationId={operationId}, payloadBytes={blob.Length}, limit={MaximumChorePayloadBytes}.");
-                return false;
-            }
-            BitConverter.GetBytes(packetId).CopyTo(blob, 0);
-            Buffer.BlockCopy(body, 0, blob, sizeof(short), body.Length);
-            Func<byte[], bool> sendRawBlob = ChoreNetworkTransport.SendRawBlob;
-            bool queued = sendRawBlob != null && sendRawBlob(blob);
-            if (!queued)
+            short packetId = packetHook?.GetPacketId() ?? (short)0;
+            if (!RandomEventsChoreSender.TrySend(
+                packet,
+                packetId,
+                packetHook != null,
+                value => GameNetworkAPI.Serialize(value),
+                () => GameGlobalsManager.Instance.ChoreManagerVA,
+                (value, id) => GameNetworkAPI.SendPacketToAllEx2(value, id, viaChore: true),
+                out body,
+                out string rejectionReason))
             {
-                LogError($"Random Events {label} Chore was not queued; no local simulation action was applied: operationId={operationId}, payloadBytes={blob.Length}.");
+                LogError($"Random Events {label} Chore was not queued; no local simulation action was applied: operationId={operationId}, reason={rejectionReason}.");
                 return false;
             }
 
             lastRandomEventsChoreQueuedTick = queueTick;
-            LogDebug($"Random Events {label} Chore queued: packetId={packetId}, operationId={operationId}, bodyBytes={body.Length}, payloadBytes={blob.Length}, bodySha256={RandomEventsDiagnostics.HashBytes(body)}.");
+            LogDebug($"Random Events {label} Chore queued: packetId={packetId}, operationId={operationId}, bodyBytes={body.Length}, payloadBytes={sizeof(short) + body.Length}, bodySha256={RandomEventsDiagnostics.HashBytes(body)}.");
             return true;
         }
 
         private void OnInitializationChorePacketReceived(ReceiveCustomPacketEventArgs<RandomEventsInitializationChorePacket> args)
         {
+            if (!Shared.GameplayModActivationGate.IsAllowed)
+                return;
             RandomEventsInitializationChorePacket packet = args?.Packet;
             if (packet == null || packet.ProtocolVersion != ChoreProtocolVersion)
             {
@@ -776,12 +799,16 @@ namespace RandomEvents
 
         private void OnBatchChorePacketReceived(ReceiveCustomPacketEventArgs<RandomEventsBatchChorePacket> args)
         {
+            if (!Shared.GameplayModActivationGate.IsAllowed)
+                return;
             try { ApplyBatchChore(args?.Packet); }
             catch (Exception ex) { mapActive = false; LogError($"Random Events batch Chore execution failed: {ex}"); }
         }
 
         private void OnSignpostChorePacketReceived(ReceiveCustomPacketEventArgs<RandomEventsSignpostChorePacket> args)
         {
+            if (!Shared.GameplayModActivationGate.IsAllowed)
+                return;
             try { ApplySignpostInitializationChore(args?.Packet); }
             catch (Exception ex) { mapActive = false; LogError($"Random Events signpost Chore execution failed: {ex}"); }
         }
@@ -789,6 +816,8 @@ namespace RandomEvents
         private void OnInitializationAckPacketReceived(
             ReceiveCustomPacketEventArgs<RandomEventsInitializationAckPacket> args)
         {
+            if (!Shared.GameplayModActivationGate.IsAllowed)
+                return;
             if (!isRealMultiplayer || !isLocalHost || multiplayerInitializationConfirmed)
                 return;
 
@@ -1206,7 +1235,7 @@ namespace RandomEvents
 
         private byte[] SaveState(SaveContext context)
         {
-            if (!context.IsSaveFile || !mapActive || state == null)
+            if (!context.IsSaveFile || !Shared.GameplayModActivationGate.IsAllowed || !mapActive || state == null)
                 return null;
             RandomEventsSaveState saved = CreateSaveState(state);
             if (deferredPreparedState != null)
@@ -1221,6 +1250,8 @@ namespace RandomEvents
 
         private void LoadState(byte[] bytes, LoadContext context)
         {
+            // Save data may be delivered before the map event resolves the mode.
+            // Keep it pending; InitializeCurrentMap validates the fail-closed gate.
             if (!context.IsSaveFile)
                 return;
             try
@@ -1265,7 +1296,7 @@ namespace RandomEvents
                     LogWarning(
                         $"Loaded Random Events state uses an implausible event date and will be initialized fresh: " +
                         $"currentAbsoluteMonth={currentAbsoluteMonth}, startAbsoluteMonth={loaded.StartAbsoluteMonth}, " +
-                        $"loadedNextDueAbsoluteMonth={loaded.NextDueAbsoluteMonth}, effectiveMonthsPerYear={VanillaMonthsPerYear}.");
+                        $"loadedNextDueAbsoluteMonth={loaded.NextDueAbsoluteMonth}, effectiveMonthsPerYear={RandomEventsCalendar.MonthsPerYear}.");
                 }
             }
             if (!valid)
@@ -1890,7 +1921,7 @@ namespace RandomEvents
                 building->r_GlobalId != target.BuildingGlobalId ||
                 building->r_AliveState != AliveState.IsAlive ||
                 building->r_PlayerIdOwner != pending.TargetPlayerId ||
-                IsKeepType(building->r_BuildingType))
+                !BanditTargetEligibility.IsEligibleStructureType(building->r_BuildingType))
             {
                 LogWarning(
                     $"Bandit group activation skipped: ownerPlayerId={pending.OwnerPlayerId}, " +
@@ -1973,6 +2004,10 @@ namespace RandomEvents
                     $"Vanilla rejected MoveHere for bandit tribe {tribeId}.");
             }
 
+            LogDebug(
+                $"Bandit group activated: ownerPlayerId={pending.OwnerPlayerId}, targetPlayerId={pending.TargetPlayerId}, " +
+                $"tribeId={tribeId}, targetBuildingId={target.BuildingId}, targetBuildingType={building->r_BuildingType}, " +
+                $"targetTile=({target.TileX},{target.TileY}), command=MoveHerePosition.");
         }
 
         private static unsafe List<BanditMoveTarget> FindBanditMoveTargets(
@@ -1987,7 +2022,7 @@ namespace RandomEvents
                 if (building.r_PlayerIdOwner != targetPlayerId ||
                     building.r_AliveState != AliveState.IsAlive ||
                     building.r_GlobalId == 0 ||
-                    IsKeepType(building.r_BuildingType) ||
+                    !BanditTargetEligibility.IsEligibleStructureType(building.r_BuildingType) ||
                     !TryFindBanditApproachTile(in building, sourcePathComponent, out int tileX, out int tileY))
                 {
                     continue;
@@ -2003,13 +2038,6 @@ namespace RandomEvents
             return targets;
         }
 
-        private static bool IsKeepType(eStructs buildingType) =>
-            buildingType == eStructs.STRUCT_KEEP_ONE ||
-            buildingType == eStructs.STRUCT_KEEP_TWO ||
-            buildingType == eStructs.STRUCT_KEEP_THREE ||
-            buildingType == eStructs.STRUCT_KEEP_FOUR ||
-            buildingType == eStructs.STRUCT_KEEP_FIVE;
-
         private static bool TryFindBanditApproachTile(
             in GameBuilding building,
             ushort sourcePathComponent,
@@ -2188,7 +2216,7 @@ namespace RandomEvents
                 if (building.r_PlayerIdOwner == targetPlayerId &&
                     building.r_AliveState == AliveState.IsAlive &&
                     building.r_GlobalId != 0 &&
-                    !IsKeepType(building.r_BuildingType) &&
+                    BanditTargetEligibility.IsEligibleStructureType(building.r_BuildingType) &&
                     HasReachableBanditApproach(in building, sourcePathComponent))
                 {
                     return true;
@@ -2386,10 +2414,15 @@ namespace RandomEvents
 
         private int GetCurrentAbsoluteMonth()
         {
-            int currentYear = GameTimeManagerAPI.Instance.GetCurrentYear();
-            int currentMonth = GameTimeManagerAPI.Instance.GetCurrentMonth();
-            ValidateCalendarApi(currentYear, currentMonth);
-            return checked(currentYear * VanillaMonthsPerYear + currentMonth);
+            uint currentYear = GameTimeManagerAPI.Instance.GetCurrentYear();
+            uint currentMonth = GameTimeManagerAPI.Instance.GetCurrentMonth();
+            try { return RandomEventsCalendar.ToAbsoluteMonth(currentYear, currentMonth); }
+            catch
+            {
+                mapActive = false;
+                state = null;
+                throw;
+            }
         }
 
         private int GetElapsedMonthsSinceStart() =>
@@ -2402,19 +2435,6 @@ namespace RandomEvents
             return checked((int)(numerator / (ScaledStrengthMonthsPerPeriod * ScaledStrengthTenthsPerUnit)));
         }
 
-        private void ValidateCalendarApi(int currentYear, int currentMonth)
-        {
-            if (currentYear < 0 || currentMonth < 0 || currentMonth >= VanillaMonthsPerYear)
-            {
-                mapActive = false;
-                state = null;
-                throw new InvalidOperationException(
-                    $"Unsupported Vanilla calendar values year={currentYear}, month={currentMonth}; " +
-                    "Random Events was disabled for this map to prevent incorrectly dated events.");
-            }
-
-        }
-
         private static bool HasElapsedMilliseconds(long startTimestamp, int milliseconds)
         {
             if (startTimestamp <= 0)

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
```

The embedded diff was limited to 2000 lines. [Open the complete filtered patch](../diffs/RandomEvents.diff).
