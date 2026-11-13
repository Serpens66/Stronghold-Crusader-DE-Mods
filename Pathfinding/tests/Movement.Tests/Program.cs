using VanillaFootprintCandidate = APIShared.GatehouseFootprintCandidate;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CrusaderDE;
using MessagePack;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace Pathfinding.Movement.Tests
{
    // Moved with the features from BugfixesAndQoL's mixed test program; the guarantees are unchanged.
    internal static class Program
    {
        private static int failures;

        private static string ReadCommandFeature(string subject, string pattern) => string.Join("\n",
            Array.ConvertAll(Directory.GetFiles(Path.Combine(FindProjectDirectory(), "src", "UnitCommands", subject),
                pattern, SearchOption.AllDirectories), File.ReadAllText));

        private static int Main()
        {
            TestMountedStockpilePermanentLifecycle();
            TestFriendlyMoatCursorIdGuard();
            TestMovementLoggingState();
            TestFriendlyMoatMovementPolicy();
            TestFriendlyMoatMovementIntegration();
            TestMovementFastPathParity();
            TestMovementHandlerSnapshotIsolation();
            TestMovementSafetyIntegration();
            TestReachableEnemyGatehouseUnitIdContract();
            TestSynchronizedGatehouseReachabilityPolicy();
            TestMoveHereConsumersIgnorePlacementCancel();
            Traversal();
            SaveRecordTests();
            LegacySettingsMigrationTests.Run(Check, FindProjectDirectory());
            TestRunMarkerIndependentOfOptions();
            TestCadenceSpriteAnimationOffset();
            Console.WriteLine("Movement checks: failures=" + failures);
            return failures == 0 ? 0 : 1;
        }

        private static void TestMountedStockpilePermanentLifecycle()
        {
            string runtime = File.ReadAllText(Path.Combine(
                FindProjectDirectory(), "src", "PathfindingRuntime.cs"));
            string patch = File.ReadAllText(Path.Combine(
                FindProjectDirectory(), "src", "TroopMovement/MountedStockpileMovementPatch.cs"));
            Check(runtime.Contains("mountedStockpileMovementPatch.SetEnabled(wanted)") &&
                    !runtime.Contains("mountedStockpileMovementPatch?.Dispose()") &&
                    !runtime.Contains("mountedStockpileMovementPatch = null;") &&
                    patch.Contains("Volatile.Write(ref enabled") &&
                    patch.Contains("Volatile.Read(ref enabled) == 0") &&
                    !patch.Contains("class MountedStockpileMovementPatch : IDisposable"),
                "mounted-stockpile hooks remain rooted and use only an atomic logical activation gate");
        }
        private static void TestFriendlyMoatCursorIdGuard()
        {
            string moatCursor = File.ReadAllText(Path.Combine(FindProjectDirectory(), "src", "UnitCommands", "Cursor", "CursorConnectivity.cs"));
            Check(moatCursor.Contains("disposed || unitId <= 0 || buildingId <= 0") &&
                  moatCursor.IndexOf("unitId <= 0", StringComparison.Ordinal) <
                  moatCursor.IndexOf("UnitAccess.TryGetById", StringComparison.Ordinal),
                "friendly-moat cursor rejects non-positive IDs before ID API lookups");
        }
        private static void TestMovementLoggingState()
        {
            var tracker = new MovementLoggingStateTracker();
            Check(tracker.TryUpdate(true, false, true) &&
                    !tracker.TryUpdate(true, false, true) &&
                    tracker.TryUpdate(true, true, true) &&
                    !tracker.TryUpdate(true, true, true) &&
                    tracker.TryUpdate(false, true, true) &&
                    tracker.TryUpdate(false, false, false),
                "movement logging summarizes only distinct effective states");
        }
        private static void TestFriendlyMoatMovementPolicy()
        {
            string source = File.ReadAllText(Path.Combine(FindProjectDirectory(), "src", "Settings/PathfindingViewModel.cs"));
            Check(source.Contains("enableImprovedManualUnitCommands = true") &&
                source.Contains("EnableImprovedManualUnitCommands = true;"), "manual commands default on and reset on");
            Check(!source.Contains("public int FriendlyMoatMovementMode"), "old traversal setting retired without mapping");
        }
        private static void TestFriendlyMoatMovementIntegration()
        {
            string projectDirectory = FindProjectDirectory();
            string runtime = File.ReadAllText(Path.Combine(projectDirectory, "src", "PathfindingRuntime.cs"));
            string friendlyRuntime = ReadCommandFeature("", "UnitCommandPathRuntime.*.cs");
            string selectionAdapters = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "UnitCommands", "Integration", "AssassinSelectionAdapters.cs"));
            string moatWork = ReadCommandFeature("Moat", "UnitCommandPathRuntime.WorkTarget*.cs");
            string viewModel = File.ReadAllText(Path.Combine(projectDirectory, "src", "Settings/PathfindingViewModel.cs"));
            string xaml = File.ReadAllText(Path.Combine(
                projectDirectory, "Override", "ScriptExtenderUI", "PathfindingSettings.xaml"));
            string english = File.ReadAllText(Path.Combine(projectDirectory, "Locales", "en-US.txt"));
            string german = File.ReadAllText(Path.Combine(projectDirectory, "Locales", "de-DE.txt"));
            string plugin = File.ReadAllText(Path.Combine(projectDirectory, "src", "PathfindingPlugin.cs"));
            Check(runtime.Contains("processUnitCommandPathRuntime") &&
                    runtime.Contains("UnitCommandPathAPI.RegisterCommands(") &&
                    !runtime.Contains("friendlyMoatMovementRuntime?.Dispose()"),
                "integrated native runtime remains process-rooted and is not disposed by plugin teardown");
            Check(moatWork.Contains("settings.EnableMod && settings.EnableImprovedMoatFilling") &&
                    // Dig work (mode 1) is routed only by an active traversal provider (MoatMove), whose own activation is independent of this mod's Host switch.
                    moatWork.Contains("relationshipMode == 1 && !TraversalEnabled") &&
                    moatWork.Contains("if (!ExtensionsEnabled)") &&
                    !moatWork.Contains("RegisterImprovedMoatFillingProvider"),
                "hostile filling remains independent while Off blocks every friendly moat work route");
            Check(viewModel.Contains("enableImprovedManualUnitCommands = true") &&
                    viewModel.Contains("public bool EnableImprovedManualUnitCommands") &&
                    !viewModel.Contains("public int FriendlyMoatMovementMode"), "manual commands are a synchronized boolean host setting");
            Check(xaml.Contains("IsChecked=\"{Binding EnableImprovedManualUnitCommands, Mode=TwoWay}\"") &&
                    !xaml.Contains("FriendlyMoatMovementSliderValue"), "manual commands use a checkbox");
            Check(english.Contains("EnableImprovedManualUnitCommandsHelp=") && german.Contains("EnableImprovedManualUnitCommandsHelp="),
                "manual command help is localized");
            Check(plugin.Contains("[BepInIncompatibility(\"MoveMoatTest_Serp\")]"),
                "legacy standalone plugin is explicitly incompatible");
            Check(friendlyRuntime.Contains("selectionCallAdapters=6") &&
                    friendlyRuntime.Contains("ValidateSelectionCallAdapters(libraryBase)") &&
                    !friendlyRuntime.Contains("CursorTilePairFallbackResultHook") &&
                    !friendlyRuntime.Contains("ObserveCursorTilePairFallbackSelectionContext"),
                "friendly moat selection uses six call-site adapters and removes the unsafe post-call hooks");
            Check(selectionAdapters.Contains("SelectionCallRvas") &&
                    selectionAdapters.Contains("0xB7321") &&
                    selectionAdapters.Contains("E84AF50D0085C0757E488BF333DB") &&
                    selectionAdapters.Contains("original[0].NearBranch64 != libraryBase + 0x196870") &&
                    selectionAdapters.Contains("handle.Require().DisplacedByteCount"),
                "selection adapters validate the SE call, exact complete spans, and installed displacement");
            Check(selectionAdapters.Contains("asm.sub(rsp, 0xE0)") &&
                    selectionAdapters.Contains("asm.movdqu(__xmmword_ptr[rsp + 0xB0], xmm5)") &&
                    selectionAdapters.Contains("asm.pushfq()") &&
                    selectionAdapters.Contains("asm.popfq()") &&
                    selectionAdapters.Contains("for (int i = 1; i < original.Length; i++)"),
                "selection adapters preserve stack, volatile SIMD state, flags, and relocated instructions");
            Check(friendlyRuntime.Contains("internal long ObserveCursorTilePairFallbackSelection(") &&
                    friendlyRuntime.Contains("if (vanillaResult != 0)") &&
                    friendlyRuntime.Contains("if (vanillaResult == 0 && functionalArmed)"),
                "friendly moat selection preserves full-width nonzero SE results and only lifts rejection");
        }
        private static void TestMovementFastPathParity()
        {
            List<string> parityFailures = MovementFastPathParityHarness.Run();
            foreach (string failure in parityFailures)
            {
                Check(false, failure);
            }

            Check(parityFailures.Count == 0,
                "managed reference and fixed-table movement kernels have identical golden-matrix results");
        }
        private static void TestMovementHandlerSnapshotIsolation()
        {
            const ulong libraryBase = 0x180000000;
            var snapshot = new byte[]
            {
                0x10, 0x11, 0x12, 0x13,
                0x20, 0x21, 0x22, 0x23,
                0x30, 0x31, 0x32, 0x33
            };
            byte[] liveMemory = (byte[])snapshot.Clone();
            liveMemory[4] = 0x90;
            liveMemory[5] = 0x90;
            liveMemory[6] = 0x90;
            liveMemory[7] = 0x90;

            byte[] copied = NativeHandlerSnapshot.Copy(
                snapshot,
                libraryBase + 4,
                4,
                libraryBase);
            Check(copied.SequenceEqual(new byte[] { 0x20, 0x21, 0x22, 0x23 }) &&
                    !copied.SequenceEqual(liveMemory.Skip(4).Take(4)),
                "movement handler decoding uses the pristine load-time snapshot instead of patched live bytes");

            bool rejected = false;
            try
            {
                NativeHandlerSnapshot.Copy(
                    snapshot,
                    libraryBase + 10,
                    4,
                    libraryBase);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Check(rejected,
                "movement handler snapshot rejects ranges outside the native module");

            string plugin = File.ReadAllText(Path.Combine(
                "src", "PathfindingPlugin.cs"));
            string manifest = File.ReadAllText("info.json");
            Match minimumMatch = Regex.Match(manifest,
                @"""MinimumScriptExtenderVersion""\s*:\s*""([^""]*)""");
            string minimumExtenderVersion = minimumMatch.Success ? minimumMatch.Groups[1].Value : string.Empty;
            Check(plugin.Contains(
                    "BepInDependency(\"fixes\", BepInDependency.DependencyFlags.SoftDependency)"),
                "BugfixesAndQoL loads after Fixes when the optional mod is installed");
            Check(!manifest.Contains("\"GUID\": \"000shcdese\"") &&
                    !string.IsNullOrEmpty(minimumExtenderVersion) &&
                    plugin.Contains($"[BepInDependency(ScriptExtenderGuid, \"{minimumExtenderVersion}\")]" ) &&
                    !manifest.Contains("\"GUID\": \"fixes\""),
                "BugfixesAndQoL uses the precise host warning, matches its plugin dependency, and keeps Fixes optional");
        }
        private static void TestMovementSafetyIntegration()
        {
            string projectDirectory = FindProjectDirectory();
            string fastRecruit = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "TroopMovement/FastRecruitRallyMovementRuntime.cs"));
            string troopMovement = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "TroopMovement/TroopMovementFixRuntime.cs"));
            string cadencePatch = File.ReadAllText(Path.Combine(
                projectDirectory,
                "src",
                "TroopMovement/SynchronizedMovementCadencePatch.cs"));
            string cadenceDiscovery = File.ReadAllText(Path.Combine(
                projectDirectory,
                "src",
                "TroopMovement/MovementCadenceNativeDiscovery.cs"));
            string integration = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "TroopMovement/MovementCadenceIntegration.cs"));
            string bridge = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "TroopMovement/FastRecruitMovementBridge.cs"));
            string parityHarness = File.ReadAllText(Path.Combine(
                projectDirectory, "tests", "Movement.Tests", "MovementFastPathParityHarness.cs"));
            string english = File.ReadAllText(Path.Combine(
                projectDirectory, "Locales", "en-US.txt"));
            string german = File.ReadAllText(Path.Combine(
                projectDirectory, "Locales", "de-DE.txt"));

            Check(fastRecruit.Contains("!players.IsPlayerIdValid(ownerPlayerId)") &&
                    fastRecruit.Contains("players.IsAIPlayer(ownerPlayerId)") &&
                    fastRecruit.Contains("UnitTransitionSource.MercenaryOutpost") &&
                    fastRecruit.Contains("UnitTransitionSource.EuropeanBarracks") &&
                    fastRecruit.Contains("OnUnitDelete") &&
                    fastRecruit.Contains("OnUnloadMap") &&
                    fastRecruit.Contains("movementPatch.SetRallyTracking(") &&
                    cadencePatch.Contains("UnitOwnerManagerOffset") &&
                    cadencePatch.Contains("RallyGenerationGlobalIdOffset") &&
                    cadencePatch.Contains("RallyUnitTypeOffset"),
                "fast recruit rally tracks only valid human owners and preserves owner identity");
            Check(fastRecruit.Contains("if (enabled &&" +
                    Environment.NewLine +
                    "                args.Phase == EventHookPhase.Pre") &&
                    fastRecruit.Contains("if (enabled && args.Phase == EventHookPhase.Pre)") &&
                    !fastRecruit.Contains("LogRallyAnimationDiagnosticsUnitStop") &&
                    fastRecruit.IndexOf("if (enabled &&", StringComparison.Ordinal) <
                        fastRecruit.IndexOf("RemoveTrackingForTribe(args.TribeId)", StringComparison.Ordinal),
                "disabled fast recruit handlers exit before tribe queries and unit loops");

            int tribeLoop = troopMovement.IndexOf("foreach (int unitId in unitIds)",
                StringComparison.Ordinal);
            int idValidation = troopMovement.IndexOf(
                "unitId <= 0",
                tribeLoop,
                StringComparison.Ordinal);
            int unitLookup = troopMovement.IndexOf(
                "GameUnit* unit = unitArray + unitId - 1;",
                tribeLoop,
                StringComparison.Ordinal);
            Check(tribeLoop >= 0 && idValidation > tribeLoop &&
                    unitLookup > idValidation,
                "tribe synchronization validates one-based IDs and performs one direct array conversion");
            int groupSynchronizationStart = troopMovement.IndexOf(
                "private bool TryApplyMixedGroupSynchronization(",
                StringComparison.Ordinal);
            int groupSynchronizationEnd = troopMovement.IndexOf(
                "private bool TryGetCadence(",
                groupSynchronizationStart,
                StringComparison.Ordinal);
            string groupSynchronization = troopMovement.Substring(
                groupSynchronizationStart,
                groupSynchronizationEnd - groupSynchronizationStart);
            Check(groupSynchronization.Contains("TroopMovementFixModLog.Warning"),
                "movement synchronization retains its warning path");
            int assignHandlerStart = troopMovement.IndexOf(
                "private void OnTribeAssignUnit(",
                StringComparison.Ordinal);
            int assignHandlerEnd = troopMovement.IndexOf(
                "private bool TryApplyMixedGroupSynchronization(",
                assignHandlerStart,
                StringComparison.Ordinal);
            string assignHandler = troopMovement.Substring(
                assignHandlerStart,
                assignHandlerEnd - assignHandlerStart);
            Check(assignHandler.Contains(
                      "RemoveSynchronization(previousTribeId, restoreSpeed: true)") &&
                    assignHandler.Contains(
                      "RemoveSynchronization(args.TribeId, restoreSpeed: true)") &&
                    !assignHandler.Contains("restoreSpeed: false") &&
                    assignHandler.IndexOf(
                      "activeMoveOrderTribeIds.Contains(previousTribeId)",
                      StringComparison.Ordinal) >= 0 &&
                    assignHandler.IndexOf(
                      "activeMoveOrderTribeIds.Contains(args.TribeId)",
                      StringComparison.Ordinal) >= 0 &&
                    troopMovement.Contains("OnTribeCreate.Observable") &&
                    assignHandler.Contains("pendingTribeSourcesByTargetId.TryGetValue(") &&
                    assignHandler.Contains("pendingSource.MemberGlobalIds.TryGetValue("),
                "external tribe reassignment restores source and destination synchronization independently");
            Check(!cadencePatch.Contains("AddContextHook") &&
                    !cadencePatch.Contains("NativePointer<X64SmartCPUContext>") &&
                    !cadencePatch.Contains("TryGetCadenceDelegate") &&
                    !cadenceDiscovery.Contains("AddContextHook") &&
                    !cadenceDiscovery.Contains("NativePointer<X64SmartCPUContext>") &&
                    !cadenceDiscovery.Contains("TryGetCadenceDelegate") &&
                    !integration.Contains("Func<IntPtr, bool>") &&
                    !integration.Contains("Action<IntPtr>") &&
                    cadencePatch.Contains("transaction.AddInline(") &&
                    cadencePatch.Contains("GeneratePreTerrainSpeedFastPath") &&
                    cadencePatch.Contains("GenerateCadenceFastPath"),
                "movement hotpaths use native inline tables without managed callbacks");
            Check(cadencePatch.Contains("rallyEnabledFlag") &&
                    cadencePatch.Contains("synchronizationEnabledFlag") &&
                    cadencePatch.Contains("assembler.je(trySynchronization);") &&
                    cadencePatch.Contains("assembler.je(replayVanilla);") &&
                    integration.Contains("SetRallyEnabled(bool enabled)") &&
                    !integration.Contains("TryGetNativeRunningState") &&
                    !integration.Contains("TryGetNativeRunningSpeedBonus") &&
                    !bridge.Contains("TryGetNativeRunningState") &&
                    !bridge.Contains("TryGetNativeRunningSpeedBonus"),
                "independent native flags bypass disabled kernels and obsolete managed animation queries are removed");
            Check(cadencePatch.Contains("MaximumTrackedUnitId = 10000") &&
                    cadencePatch.Contains("MaximumTrackedTribeId = 4500") &&
                    cadencePatch.Contains("RallyObservedOffset") &&
                    cadencePatch.Contains("RallyMovingOffset") &&
                    cadencePatch.Contains("UnitInitializationAiState = 109") &&
                    cadencePatch.Contains("clearRallyAndTrySynchronization") &&
                    cadencePatch.Contains("applyRallyProfile") &&
                    cadencePatch.IndexOf("applyRallyProfile", StringComparison.Ordinal) <
                        cadencePatch.IndexOf("applyRunningProfile", StringComparison.Ordinal),
                "native tables preserve rally identity, interrupted-path state and rally precedence");
            Check(cadenceDiscovery.Contains(
                      "SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_ARAB_BOW, 1, 0x81);") &&
                    parityHarness.Contains(
                      "arab-bow-vanilla-running-pair") &&
                    parityHarness.Contains(
                      "tableUnit.Animation != 0x81 || tableUnit.SpeedBonus != 1"),
                "Arab bow rally cadence preserves Vanilla Animation 0x81 and SpeedBonus 1");
            Check(cadenceDiscovery.Contains(
                      "SetAuditedProfile(animationTransitionsByType, eChimps.CHIMP_TYPE_KNIGHT, 2, 0x81);") &&
                    parityHarness.Contains(
                      "tableUnit.SpeedBonus != 2"),
                "Knight rally cadence preserves Vanilla Animation 0x81 and SpeedBonus 2");
            Check(cadencePatch.Contains("UnitCurrentSpeed2ManagerOffset = 0x9A2") &&
                    cadencePatch.Contains("UnitCurrentSpeedManagerOffset = 0x9A4") &&
                    cadencePatch.Contains("assembler.Label(ref trySynchronization);") &&
                    cadencePatch.Contains("__word_ptr[r8 + UnitAliveStateManagerOffset]") &&
                    cadencePatch.Contains("ValidateMovementCadenceHook") &&
                    cadencePatch.Contains("assembler.pushfq()") &&
                    cadencePatch.Contains("assembler.popfq()") &&
                    cadencePatch.Contains("foreach (Instruction instruction in overwrittenInstructions)"),
                "native movement hooks validate exact fields and replay Vanilla with preserved flags");
            int cadenceEmitterStart = cadencePatch.IndexOf(
                "private void GenerateCadenceFastPath(",
                StringComparison.Ordinal);
            int cadenceEmitterEnd = cadencePatch.IndexOf(
                "private void EmitProfileAddress(",
                cadenceEmitterStart,
                StringComparison.Ordinal);
            string cadenceEmitter = cadencePatch.Substring(
                cadenceEmitterStart,
                cadenceEmitterEnd - cadenceEmitterStart);
            int aliveGate = cadenceEmitter.IndexOf(
                "__word_ptr[r8 + UnitAliveStateManagerOffset]",
                StringComparison.Ordinal);
            int rallyFlagRead = cadenceEmitter.IndexOf(
                "unchecked((ulong)rallyEnabledFlag)",
                StringComparison.Ordinal);
            Check(aliveGate >= 0 && aliveGate < rallyFlagRead &&
                    cadenceEmitter.Split(new[] {
                        "__word_ptr[r8 + UnitAliveStateManagerOffset]"
                    }, StringSplitOptions.None).Length == 2 &&
                    cadenceEmitter.IndexOf(
                        "assembler.jne(replayVanilla);",
                        aliveGate,
                        StringComparison.Ordinal) < rallyFlagRead,
                "cadence fastpath preserves the managed outer alive gate before all rally and synchronization table access");
            Check(Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_SpriteAnimationGroup)).ToInt32() == 0x004 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_AliveState)).ToInt32() == 0x088 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() == 0x092 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_GlobalId)).ToInt32() == 0x094 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_PathPlanStateBitFlags)).ToInt32() == 0x0F2 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_SpeedBonus)).ToInt32() == 0x2BA &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_AIState)).ToInt32() == 0x2BC &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_TransformIntoUnitOfType)).ToInt32() == 0x2C6 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_TribeId)).ToInt32() == 0x2D4 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentSpeed2)).ToInt32() == 0x346 &&
                    Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentSpeed)).ToInt32() == 0x348,
                "native fastpath offsets match the installed Script Extender GameUnit layout");
            // Wording guardrail only: check the relevant tooltip, not unrelated locale entries.
            string englishRallyHelp = english.Split('\n').Single(line =>
                line.StartsWith("Pathfinding.EnableFastRecruitRallyMovementHelp=", StringComparison.Ordinal));
            string germanRallyHelp = german.Split('\n').Single(line =>
                line.StartsWith("Pathfinding.EnableFastRecruitRallyMovementHelp=", StringComparison.Ordinal));
            Check(englishRallyHelp.Contains("belonging to human players") &&
                    englishRallyHelp.Contains("AI recruits are unchanged") &&
                    germanRallyHelp.Contains("Einheiten menschlicher Spieler") &&
                    germanRallyHelp.Contains("KI-Rekruten bleiben unverändert"),
                "fast recruit rally help text documents the human-only behavior");
            Check(englishRallyHelp.Contains("Terrain and other speed modifiers still apply") &&
                    germanRallyHelp.Contains("Gelände und andere Geschwindigkeitsmodifikatoren wirken weiterhin"),
                "fast recruit rally help text documents preserved terrain and speed modifiers");
        }
        private static void TestReachableEnemyGatehouseUnitIdContract()
        {
            const int unitSpanLength = 10000;
            Check(Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId(
                    1, unitSpanLength, out int firstUnitId) &&
                firstUnitId == 1 && firstUnitId - 1 == 0,
                "gatehouse event ID 1 remains game ID 1 and converts to span index 0 only at a direct-span boundary");
            Check(Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId(
                    unitSpanLength, unitSpanLength, out int lastUnitId) &&
                lastUnitId == unitSpanLength,
                "gatehouse event accepts the highest valid one-based Unit game ID unchanged");
            Check(!Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId(
                    0, unitSpanLength, out _) &&
                !Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId(
                    -1, unitSpanLength, out _) &&
                !Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId(
                    unitSpanLength + 1, unitSpanLength, out _),
                "gatehouse event rejects zero, negative and above-span Unit IDs");

            string projectDirectory = FindProjectDirectory();
            string source = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "GateRouting/ReachableEnemyGatehouseRuntime.cs"));
            Check(!source.Contains("zero-based span index") &&
                    !source.Contains("rawUnitSpanIndex") &&
                    source.Contains("one-based Unit game ID") &&
                    source.Contains("validates it without conversion") &&
                    source.Contains("eventUnitId={args.UnitId}"),
                "gatehouse source documents and diagnoses the current one-based event contract");

            int handlerStart = source.IndexOf(
                "private void OnGatehouseQuery(", StringComparison.Ordinal);
            int helperStart = handlerStart < 0
                ? -1
                : source.IndexOf(
                    "private bool TryIsUnitReachableToGate(",
                    handlerStart,
                    StringComparison.Ordinal);
            Check(handlerStart >= 0 && helperStart > handlerStart,
                "gatehouse source exposes the expected event-handler boundary");
            if (handlerStart < 0 || helperStart <= handlerStart)
                return;

            string handler = source.Substring(handlerStart, helperStart - handlerStart);
            int eventRead = handler.IndexOf(
                "int candidateUnitId = args.UnitId;", StringComparison.Ordinal);
            int validation = handler.IndexOf(
                "GatehouseQueryUnitIdPolicy.TryValidateGameId(", StringComparison.Ordinal);
            int lookup = handler.IndexOf(
                "UnitAccess.TryGetById(unitId,", StringComparison.Ordinal);
            int pathing = handler.IndexOf(
                "TryIsUnitReachableToGate(", StringComparison.Ordinal);
            Check(eventRead >= 0 && validation > eventRead && lookup > validation && pathing > lookup,
                "gatehouse handler passes the validated one-based Unit ID unchanged to lookup and pathing");
            Check(!handler.Contains("args.UnitId + 1") &&
                    !handler.Contains("args.UnitId+1") &&
                    !handler.Contains("candidateUnitId + 1") &&
                    !handler.Contains("candidateUnitId+1") &&
                    !handler.Contains("unitId + 1") &&
                    !handler.Contains("unitId+1"),
                "gatehouse handler applies no obsolete +1 conversion to the event Unit ID");

            // Footprint candidates come from the pure-coordinate cache over APIShared's ordered footprint;
            // the first-distinct drawbridge selection and the exterior-approach route test are inline.
            string footprintCache = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "GateRouting/GatehouseFootprintGeometryCache.cs"));
            Check(source.Contains("STRUCT_DRAWBRIDGE") &&
                    source.Contains("MaximumSynchronizedDrawbridges") &&
                    source.Contains("GatehouseFootprintGeometryCache.Get(") &&
                    footprintCache.Contains("BuildOrderedFootprintCandidates(") &&
                    source.Contains("id == first || id == second") &&
                    source.Contains("TryTraceExteriorApproach(") &&
                    source.Contains("CanReachDrawbridge(") &&
                    source.Contains("FindNextComponentTowardDestination(") &&
                    source.Contains("GetTileBuildingId(") &&
                    !footprintCache.Contains("GameBuilding*") &&
                    !source.Contains("GATE_REACH_DIAG") &&
                    !source.Contains("GatehouseReachabilityDiagnosticThrottle") &&
                    !source.Contains("missing-path-record") &&
                    !source.Contains("CanUnitTypeUsePathConnectionClass(") &&
                    !source.Contains("r_IsEnabledOrOpen =") &&
                    !source.Contains("SetPath") &&
                    !source.Contains("RecalculateTile"),
                "gatehouse runtime uses Vanilla's building grid and exterior approaches without mutating live pathing state");
        }
        private static void TestSynchronizedGatehouseReachabilityPolicy()
        {
            List<VanillaFootprintCandidate> candidates =
                SynchronizedGatehouseReachabilityPolicy.BuildOrderedFootprintCandidates(
                    0, 0, 5);
            string actualOrder = string.Join(
                ";",
                candidates.Select(candidate =>
                    $"({candidate.X},{candidate.Y},{candidate.OutwardX},{candidate.OutwardY})"));
            const string ExpectedOrder =
                "(2,-1,0,-1);(3,-1,0,-1);(4,-1,0,-1);" +
                "(5,0,1,0);(5,1,1,0);(5,2,1,0);(5,3,1,0);(5,4,1,0);" +
                "(4,5,0,1);(3,5,0,1);(2,5,0,1);(1,5,0,1);(0,5,0,1);" +
                "(-1,4,-1,0);(-1,3,-1,0);(-1,2,-1,0);(-1,1,-1,0);(-1,0,-1,0);" +
                "(0,-1,0,-1);(1,-1,0,-1)";
            Check(candidates.Count == 20 && actualOrder == ExpectedOrder,
                "gatehouse footprint size 5 yields Vanilla's exact 20 edge candidates in order");

            var candidateBuildings = new Dictionary<string, int>
            {
                ["2,-1"] = 90,
                ["3,-1"] = 10,
                ["4,-1"] = 10,
                ["5,0"] = 11,
                ["5,1"] = 12,
                ["5,2"] = 13
            };
            var eligible = new HashSet<int> { 10, 11, 12, 13 };
            List<int> selected =
                SynchronizedGatehouseReachabilityPolicy.CollectFirstDistinctBuildingIds(
                    candidates,
                    (x, y) => candidateBuildings.TryGetValue($"{x},{y}", out int id) ? id : 0,
                    id => eligible.Contains(id));
            Check(selected.SequenceEqual(new[] { 10, 11 }),
                "dead or non-drawbridge candidates are skipped and only Vanilla's first two distinct bridges are coupled");
            Check(selected.Contains(11),
                "a live foreign-owner drawbridge remains eligible because Vanilla's lookup has no owner filter");

            int HorizontalBuildingAt(int x, int y) => y == 2 && x >= 5 && x <= 9 ? 7 : 0;
            bool horizontalOpen = SynchronizedGatehouseReachabilityPolicy.TryTraceExteriorApproach(
                new VanillaFootprintCandidate(5, 2, 1, 0),
                7,
                (x, y) => x >= 0 && x < 20 && y >= 0 && y < 20,
                HorizontalBuildingAt,
                (x, y) => y * 20 + x,
                tileId => tileId == 50 ? 88 : 0,
                out DrawbridgeApproachSnapshot openApproach);
            bool horizontalRaised = SynchronizedGatehouseReachabilityPolicy.TryTraceExteriorApproach(
                new VanillaFootprintCandidate(5, 2, 1, 0),
                7,
                (x, y) => x >= 0 && x < 20 && y >= 0 && y < 20,
                HorizontalBuildingAt,
                (x, y) => y * 20 + x,
                tileId => tileId == 50 ? 88 : 0,
                out DrawbridgeApproachSnapshot raisedApproach);
            Check(horizontalOpen && horizontalRaised &&
                    openApproach.ExteriorX == 10 && openApproach.ExteriorY == 2 &&
                    openApproach.ExteriorPcl == 88 &&
                    raisedApproach.ExteriorTileId == openApproach.ExteriorTileId,
                "open and raised drawbridge states resolve to the same stable exterior approach without a path record");

            int VerticalBuildingAt(int x, int y) => x == 3 && y >= 4 && y <= 8 ? 8 : 0;
            Check(SynchronizedGatehouseReachabilityPolicy.TryTraceExteriorApproach(
                    new VanillaFootprintCandidate(3, 4, 0, -1),
                    8,
                    (x, y) => x >= 0 && x < 20 && y >= 0 && y < 20,
                    VerticalBuildingAt,
                    (x, y) => y * 20 + x,
                    tileId => tileId == 63 ? 77 : 0,
                    out DrawbridgeApproachSnapshot rotatedApproach) &&
                  rotatedApproach.ExteriorX == 3 && rotatedApproach.ExteriorY == 3 &&
                  rotatedApproach.ExteriorPcl == 77,
                "rotated drawbridges follow the candidate's outward direction");
            Check(!SynchronizedGatehouseReachabilityPolicy.TryTraceExteriorApproach(
                    new VanillaFootprintCandidate(0, 2, -1, 0),
                    9,
                    (x, y) => x >= 0 && x < 20 && y >= 0 && y < 20,
                    (x, y) => x == 0 && y == 2 ? 9 : 0,
                    (x, y) => y * 20 + x,
                    tileId => 22,
                    out _) &&
                  !SynchronizedGatehouseReachabilityPolicy.TryTraceExteriorApproach(
                    new VanillaFootprintCandidate(5, 2, 1, 0),
                    7,
                    (x, y) => x >= 0 && x < 20 && y >= 0 && y < 20,
                    HorizontalBuildingAt,
                    (x, y) => y * 20 + x,
                    tileId => 0,
                    out _),
                "out-of-map geometry and exterior PCL zero fail closed");

            var firstBridge = new SynchronizedDrawbridgeSnapshot(7, 700);
            firstBridge.Approaches.Add(openApproach);
            var secondBridge = new SynchronizedDrawbridgeSnapshot(8, 800);
            secondBridge.Approaches.Add(rotatedApproach);
            Check(SynchronizedGatehouseReachabilityPolicy.CanReachAnyExteriorApproach(
                    new[] { firstBridge, secondBridge },
                    component => component == 77) &&
                  !SynchronizedGatehouseReachabilityPolicy.CanReachAnyExteriorApproach(
                    new[] { firstBridge, secondBridge },
                    component => component == 99),
                "either of two coupled drawbridge exteriors can establish reachability without unrelated components");
            Check(!SynchronizedGatehouseReachabilityPolicy.CanReachAnyExteriorApproach(
                    new[] { firstBridge, secondBridge, firstBridge },
                    component => true),
                "more than two supplied drawbridges fail closed even though discovery selects only two");

            int signature =
                SynchronizedGatehouseReachabilityPolicy.ComputeDrawbridgeSignature(
                    new[] { firstBridge });
            var replacedBridge = new SynchronizedDrawbridgeSnapshot(7, 701);
            replacedBridge.Approaches.Add(openApproach);
            var changedExterior = new SynchronizedDrawbridgeSnapshot(7, 700);
            changedExterior.Approaches.Add(new DrawbridgeApproachSnapshot(
                5, 2, 10, 2, 50, 89));
            Check(signature != SynchronizedGatehouseReachabilityPolicy.ComputeDrawbridgeSignature(
                      new[] { replacedBridge }) &&
                  signature != SynchronizedGatehouseReachabilityPolicy.ComputeDrawbridgeSignature(
                      new[] { changedExterior }),
                "drawbridge cache signature changes with Global ID and exterior PCL");

            string projectDirectory = FindProjectDirectory();
            string runtime = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "GateRouting/ReachableEnemyGatehouseRuntime.cs"));
            Check(!runtime.Contains("OpenState") &&
                    !runtime.Contains("previouslyOpen"),
                "already-closed gates need no remembered open state");
            // Cache lifetime is exercised against the production class in GatehouseDiagnostics/GatehouseFixture.cs.
        }
        private static void TestMoveHereConsumersIgnorePlacementCancel()
        {
            Check(!File.ReadAllText(Path.Combine(FindProjectDirectory(), "src", "Assassins/AssassinPathfindingRuntime.cs"))
                    .Contains("if (args.SkipOriginalFunction)" +
                        Environment.NewLine + "                return;") &&
                  !ReadCommandFeature("", "UnitCommandPathRuntime.*.cs")
                    .Contains("if (disposed || args.SkipOriginalFunction)") &&
                  !File.ReadAllText(Path.Combine(FindProjectDirectory(), "src", "TroopMovement/TroopMovementFixRuntime.cs"))
                    .Contains("if (!IsFeatureEnabled || args.SkipOriginalFunction"),
                "placement-cancel handling no longer patches individual MoveHere consumers");
        }

        private static void TestCadenceSpriteAnimationOffset()
        {
            string projectDirectory = FindProjectDirectory();
            string cadence = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "TroopMovement/SynchronizedMovementCadencePatch.cs"));
            string cadenceDiscovery = File.ReadAllText(Path.Combine(
                projectDirectory, "src", "TroopMovement/MovementCadenceNativeDiscovery.cs"));
            Check(cadence.Contains("UnitAnimationStateManagerOffset = 0x660") &&
                    cadence.Contains("UnitAnimationStateManagerOffset") &&
                    !cadence.Contains("N000000F4") &&
                    !cadenceDiscovery.Contains("N000000F4"),
                "native movement fastpath uses the audited SE 2.6 sprite-animation group offset");
        }

        private static void Traversal()
        {
            bool[] enabled={false,true,false,true,false,true,true,true};
            int index=0;
            foreach(bool h in new[]{false,true})foreach(bool own in new[]{false,true})foreach(bool provider in new[]{false,true})
            {
                foreach(bool required in new[]{false,true})
                {
                    Pathfinding.UnitCommands.UnitCommandPathAPI.Traversal=new Pathfinding.UnitCommands.UnitCommandTraversalProvider{Enabled=provider,RequiredOnly=required};
                    var snapshot=Pathfinding.UnitCommands.MovementOptionsSnapshot.Capture(new Pathfinding.UnitCommands.TraversalSettings{EnableMod=h,EnableImprovedManualUnitCommands=own});
                    Check(snapshot.Enabled==enabled[index],"production movement demand: own H/option and independent provider");
                    Check(snapshot.TraversalEnabled==provider,"production provider demand is independent of own Host");
                    Check(snapshot.RequiredOnly==required,"provider route mode preserved");
                }
                index++;
            }
            Pathfinding.UnitCommands.UnitCommandPathAPI.Traversal=null;
            var absent=Pathfinding.UnitCommands.MovementOptionsSnapshot.Capture(new Pathfinding.UnitCommands.TraversalSettings());
            Check(!absent.Enabled && !absent.TraversalEnabled,"no hidden command feature without own or foreign demand");
        }

        private static void TestRunMarkerIndependentOfOptions()
        {
            string hooks = File.ReadAllText(Path.Combine(FindProjectDirectory(), "src", "Formations", "FormationRuntime.EngineAndCameraHooks.cs"));
            int disabled = hooks.IndexOf("FailOpen(\"disabled-reset\", ex);", StringComparison.Ordinal);
            int marked = hooks.IndexOf("SimulationRunScope.Enter();", Math.Max(0, disabled), StringComparison.Ordinal);
            int direct = hooks.IndexOf("return engineRunOriginal(mpFrameSkip);", Math.Max(0, disabled), StringComparison.Ordinal);
            int run = hooks.IndexOf("private int RunOriginalOnce(", StringComparison.Ordinal);
            int runMarked = hooks.IndexOf("SimulationRunScope.Enter();", Math.Max(0, run), StringComparison.Ordinal);
            Check(disabled >= 0 && marked > disabled && direct > marked && run > direct && runMarked > run &&
                    hooks.IndexOf("return engineRunOriginal(mpFrameSkip);") == direct,
                "every EngineInterface.run path enters the run marker, independent of the formation options");
        }

        private static void SaveRecordTests()
        {
            var climb = new AssassinClimbSaveState { ForbiddenPlayerIds = new[] { 2, 8 } };
            AssassinClimbSaveState climbCopy = MessagePackSerializer.Deserialize<AssassinClimbSaveState>(MessagePackSerializer.Serialize(climb));
            Check(climbCopy.ForbiddenPlayerIds.SequenceEqual(new[] { 2, 8 }), "Assassin climb record round-trips forbidden players");
            Check(Rejects<AssassinClimbSaveState>(Raw(1, new object[] { 2, 2 })), "duplicate Assassin climb player is rejected");
            Check(Rejects<AssassinClimbSaveState>(Raw(1, new object[] { 0 })) && Rejects<AssassinClimbSaveState>(Raw(1, new object[] { 9 })),
                "out-of-range Assassin climb players are rejected");
            Check(Rejects<AssassinClimbSaveState>(Raw(1, new object[] { 1, 2, 3, 4, 5, 6, 7, 8, 1 })), "oversized Assassin climb record is rejected");
            Check(Rejects<AssassinClimbSaveState>(Raw(2, new object[0])), "newer Assassin climb version is rejected");

            var movement = new TroopMovementSyncSaveState
            {
                Entries = new[]
                {
                    new TroopMovementSyncSaveEntry { TribeId = 5, TribeGlobalId = 900, Running = true, RunningSpeedBonus = 4, PreviousMovementSpeed = 30, AppliedMovementSpeed = 18 },
                    new TroopMovementSyncSaveEntry { TribeId = 4500, TribeGlobalId = uint.MaxValue, Running = false, RunningSpeedBonus = 0, PreviousMovementSpeed = ushort.MaxValue, AppliedMovementSpeed = 1 }
                }
            };
            TroopMovementSyncSaveState movementCopy = MessagePackSerializer.Deserialize<TroopMovementSyncSaveState>(MessagePackSerializer.Serialize(movement));
            Check(movementCopy.Entries.Length == 2 && movementCopy.Entries[0].TribeId == 5 && movementCopy.Entries[0].TribeGlobalId == 900 &&
                movementCopy.Entries[0].Running && movementCopy.Entries[0].RunningSpeedBonus == 4 &&
                movementCopy.Entries[0].PreviousMovementSpeed == 30 && movementCopy.Entries[0].AppliedMovementSpeed == 18 &&
                movementCopy.Entries[1].TribeGlobalId == uint.MaxValue && !movementCopy.Entries[1].Running &&
                movementCopy.Entries[1].PreviousMovementSpeed == ushort.MaxValue, "troop movement record round-trips every field");
            Check(Rejects<TroopMovementSyncSaveState>(Raw(1, new object[] { new object[] { 4501, 1u, false, 0, 1, 1 } })), "tribe beyond the cadence table is rejected");
            Check(Rejects<TroopMovementSyncSaveState>(Raw(1, new object[] { new object[] { 5, 1u, false, 3, 1, 1 } })), "walking cadence with a running bonus is rejected");
            Check(Rejects<TroopMovementSyncSaveState>(Raw(1, new object[] { new object[] { 5, 1u, false, 0, 1, 1 }, new object[] { 5, 2u, false, 0, 1, 1 } })),
                "duplicate troop movement tribe is rejected");
            Check(Rejects<TroopMovementSyncSaveState>(Raw(1, new object[] { new object[] { 5, 1u, false, 0, 1 } })), "short troop movement entry is rejected");
        }

        private static bool Rejects<T>(byte[] bytes)
        {
            try { MessagePackSerializer.Deserialize<T>(bytes); return false; }
            catch (MessagePackSerializationException) { return true; }
        }

        private static byte[] Raw(params object[] fields) => MessagePackSerializer.Serialize<object>(fields);

        private static string FindProjectDirectory()
        {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Pathfinding.csproj")))
                    return directory.FullName;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("Pathfinding project directory was not found.");
        }

        private static void Check(bool condition, string name)
        {
            if (condition)
                return;
            failures++;
            Console.WriteLine("FAIL: " + name);
        }
    }
}
