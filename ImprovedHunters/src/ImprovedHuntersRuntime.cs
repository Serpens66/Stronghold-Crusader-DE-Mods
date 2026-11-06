using APIShared.GameModes;
using APIShared.Simulation;
using Shared;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.EventAPI.Units;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using RedBird.Core.Memory;
using RedBird.X64.Assembly.Stateful;

namespace ImprovedHunters
{
    internal sealed partial class ImprovedHuntersRuntime : IDisposable
    {
        private const short RabbitCorpseDespawnTicks = 1800;
        private const short ExtraCorpseDespawnTicks = 1800;
        private const ushort CollectedCorpseDespawnTicks = 1801;
        private const int HunterSearchRadius = 20;
        private const int HunterTargetCandidateRadius = 54;
        private const int MaxHeuristicCandidatesPerHunter = 24;
        private const int HunterHutWorkCost = 600;
        private const int BestTargetToleranceCost = 80;
        private const int DefaultPreyHandlingCost = 100;
        private const int MaxPreyCacheDiagnosticLogs = 120;
        private const int MaxHunterTargetDiagnosticLogs = 160;
        private const int MaximumPlayerId = 8;
        private const int MaxInvalidHunterEventLogs = 20;
        private const int MaxHunterQueryActorWorkaroundLogs = 20;

        // Native pickupable animal corpse state observed after regular Hunter
        // ranged damage.
        private const ushort HunterCorpsePickupAiState = 0x6E;
        // Retained only so corpses created by older KillUnit-based versions can
        // expire cleanly after loading; current code never creates 0x6F.
        private const ushort HunterFreshCorpseAiState = 0x6F;
        private const long ExpiredShortLivedCorpsePreserve = long.MinValue;

        // Rabbit despawn is exposed by the Script Extender. Camel and chicken use
        // the same native logic, but their constants are not exposed, so we patch
        // the immediate operands found by these byte patterns.
        private const string CamelDespawnTickTimePattern = "66 83 FE 6E 75 4D FE 84 2B 86 09 00 00 B9 ? ? ? ? 38 8C 2B 86 09 00 00";
        private const string ChickenDespawnTickTimePattern = "66 83 FF 6E 75 55 FE 84 2B 86 09 00 00 B9 ? ? ? ? 66 FF 84 2B 20 09 00 00";
        private const int ExtraDespawnPatternImmediateOffset = 13;
        private const int CamelDespawnTickTimeRva = 0x1584B8;
        private const int ChickenDespawnTickTimeRva = 0x163465;

        // The BepInEx plugin component is short-lived in SHCDE. Repeating work runs on APIShared's
        // process-lived map-tick callback, at the same map ticks on every multiplayer peer.
        private const int NativeScanIntervalMapTicks = HunterSimulationTime.MapTicksPerSecond / 10;
        // Vanilla's Hunter stops and shoots within this distance (0x12FC70, state 1).
        private const int HunterShootingDistance = 20;
        private const int BetterPreyCheckEveryScans = 10;
        private const int BetterPreyMinimumGainTiles = 10;
        private const int BetterPreyPauseMapTicks = 10 * HunterSimulationTime.MapTicksPerSecond;
        private static readonly long IdleHunterRequeryInterval = HunterSimulationTime.MapTicksPerSecond;
        private static readonly long PreyCacheRefreshInterval = HunterSimulationTime.MapTicksPerSecond * 5;
        private static readonly long BestTargetCacheInterval = HunterSimulationTime.MapTicksPerSecond / 2;
        private static readonly long AbortedTargetCooldownInterval = HunterSimulationTime.MapTicksPerSecond * 30;
        private static readonly long HunterSearchDetectionGap = HunterSimulationTime.MapTicksPerSecond / 4;
        private const int ShortLivedCorpseVisiblePreserveMapTicksAtSpeed40 = 1800;
        private static readonly long ShortLivedCorpsePreserveCleanupInterval = HunterSimulationTime.MapTicksPerSecond * 10;

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, eChimps> hunterPreyTypes = new Dictionary<int, eChimps>();
        private readonly Dictionary<int, long> nextBetterPreyChecks = new Dictionary<int, long>();
        private readonly Dictionary<int, long> nextIdleHunterRequeryTimestamps = new Dictionary<int, long>();
        private readonly HashSet<uint> loggedCollectedCorpseGlobalIds = new HashSet<uint>();
        private readonly List<PreySnapshot> preyCache = new List<PreySnapshot>();
        // Reused by the scan, which only runs in the simulation tick callback.
        private readonly List<IntPtr> scanHunters = new List<IntPtr>();
        private readonly List<IntPtr> scanEligiblePrey = new List<IntPtr>();
        private readonly Dictionary<int, CachedBestTarget> bestTargetCache = new Dictionary<int, CachedBestTarget>();
        private readonly Dictionary<int, HunterTargetSnapshot> activeHunterTargets = new Dictionary<int, HunterTargetSnapshot>();
        private readonly Dictionary<HunterPreyCooldownKey, long> abortedTargetCooldowns = new Dictionary<HunterPreyCooldownKey, long>();
        private readonly Dictionary<int, long> lastHunterQueryTimestamps = new Dictionary<int, long>();
        private readonly Dictionary<int, long> hunterMeatPickupTimestamps = new Dictionary<int, long>();

        // Keeps short-lived corpses visible long enough for hunters to reach them.
        // The value is either the preserve-until map tick or ExpiredShortLivedCorpsePreserve.
        private readonly Dictionary<uint, long> shortLivedCorpsePreserveUntil = new Dictionary<uint, long>();

        private ManagedAssemblyImmediate<short> rabbitDespawnTickTime;
        private ManagedAssemblyImmediate<short> camelDespawnTickTime;
        private ManagedAssemblyImmediate<short> chickenDespawnTickTime;
        private AssemblyValueOverrideScope rabbitDespawnOverride;
        private ScanRegion hookRegion;
        private short originalRabbitDespawnTicks;
        private short originalCamelDespawnTicks;
        private short originalChickenDespawnTicks;
        private bool rabbitDespawnTicksInitialized;
        private bool extraDespawnTicksInitialized;
        private bool rabbitDespawnTicksPatched;
        private bool camelDespawnTicksPatched;
        private bool chickenDespawnTicksPatched;
        private bool despawnPatchStateLogged;
        private bool camelHealthInitialized;
        private bool camelHealthPatched;
        private int originalCamelArrowDamage;
        private uint originalCamelHealth;
        private uint desiredCamelHealth;
        private uint lastLoggedDesiredCamelHealth;
        private bool nativeScanFailureLogged;
        private bool simulationTickRegistered;
        private long nextPreyCacheRefreshTimestamp;
        private long nextShortLivedCorpsePreserveCleanupTimestamp;
        private int hunterTargetDiagnosticLogs;
        private int preyCacheDiagnosticLogs;
        private int hunterTargetQueryEvents;
        private int hunterTargetAcceptedEvents;
        private int hunterTargetRejectedEvents;
        private int hunterTargetFallbackEvents;
        private int hunterTargetNoBestEvents;
        private int hunterTargetSearchStarts;
        private int shortLivedCorpsePreserveLogs;
        private int invalidHunterEventLogs;
        private int hunterQueryActorWorkaroundLogs;
        private OwnedChickenHuntPatch ownedChickenHuntPatch;
        private GranaryChickenLimitPatch granaryChickenLimitPatch;
        private PreyReservationHeartbeatPatch preyReservationHeartbeatPatch;
        private RabbitMeleeOrderPatch rabbitMeleeOrderPatch;
        private TannerMeatPatch tannerMeatPatch;
        private TannerRatioSync tannerRatioSync;
        private HunterHutAvailabilityPatch hunterHutAvailabilityPatch;
        private WildlifeRespawnFeature wildlifeRespawn;
        private HerdFleeSightFeature herdFleeSight;
        private HunterQueryActorWorkaround hunterQueryActorWorkaround;
        private HunterHutVisibilityPatch hunterHutVisibilityPatch;
        private HunterNativeVisibilityProbe hunterNativeVisibilityProbe;
        private HunterActiveTargetVisibilitySnapshot hunterActiveTargetVisibilitySnapshot;
        private HunterPclReachability hunterPclReachability;
        private HunterRemainingPathSpeedRecovery hunterRemainingPathSpeedRecovery;
        private HunterPostShotContinuationDiagnostic hunterPostShotContinuationDiagnostic;
        private HunterTargetSearchFallbackDiagnostic hunterTargetSearchFallbackDiagnostic;
        private HunterVanillaPathContinuationDiagnostic hunterVanillaPathContinuationDiagnostic;
        private bool referenceHashMatches;
        private bool targetSelectionModeAllowed;
        private bool pathfindingModeAllowed;
        private bool applied;
        private bool runtimeEventsSubscribed;

        public ImprovedHuntersRuntime(ManualLogSource log, ImprovedHuntersViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Shared.GameplayModActivationGate.Initialize(log, ImprovedHuntersPlugin.PluginGuid, ImprovedHuntersPlugin.PluginName, () => settings.EnableMod);
            Shared.GameplayModActivationGate.StateChanged += OnModeStateChanged;
        }

        public void Apply(CrusaderLibraryLoadContext context, bool referenceHashMatches)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            ReadOnlySpan<byte> memory = context.Memory;
            ulong imageBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            hookRegion = context.Region;
            if (applied)
                return;

            this.referenceHashMatches = referenceHashMatches;
            InitializeOwnedChickenHuntPatch(memory, imageBase, referenceHashMatches);
            InitializeGranaryChickenLimitPatch(memory, imageBase, referenceHashMatches);
            InitializePreyReservationHeartbeatPatch(memory, imageBase, referenceHashMatches);
            InitializeRabbitMeleeOrderPatch(memory, imageBase, referenceHashMatches);
            InitializeTannerMeatPatch(memory, imageBase, referenceHashMatches);
            InitializeHunterQueryActorWorkaround(memory, imageBase, referenceHashMatches);
            InitializeHunterNativeVisibilityProbe(memory, imageBase, referenceHashMatches);
            InitializeHunterHutVisibilityPatch(memory, imageBase, referenceHashMatches);
            Shared.DevDiag.Attach(log);
            InitializeWildlifeFeatures(memory, imageBase, referenceHashMatches);
            InitializeHunterCorpseChoicePatch(memory, imageBase, referenceHashMatches);
            TryInitializeFeature("PCL reachability", () => InitializeHunterPclReachability(referenceHashMatches));
            TryInitializeFeature("active-target visibility snapshot", InitializeHunterActiveTargetVisibilitySnapshot);
            InitializeHunterPostShotContinuationDiagnostic(memory, imageBase, referenceHashMatches);
            InitializeHunterTargetSearchFallbackDiagnostic(memory, imageBase, referenceHashMatches);
            InitializeHunterRemainingPathSpeedRecovery(memory, imageBase, referenceHashMatches);
            InitializeHunterVanillaPathContinuationDiagnostic(memory, imageBase, referenceHashMatches);

            TryInitializeFeature("simulation tick", RegisterSimulationTick);

            if (Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                SubscribeRuntimeEvents();

            try
            {
                settings.SettingChanged += OnSettingChanged;
            }
            catch (Exception ex)
            {
                LogFeatureFailure("settings callback", ex);
            }
            TryInitializeFeature("rabbit despawn patch", InitializeRabbitDespawnPatch);
            InitializeExtraDespawnPatches(memory, imageBase);
            TryInitializeFeature("despawn settings", ApplyDespawnPatches);
            TryInitializeFeature("camel health", ApplyCamelHealthPatch);
            TryInitializeFeature("native site registry", () => HunterHookInfrastructure.PublishNativeSites(imageBase, log));

            applied = true;
            Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters runtime enabled: ownedChickenHuntAvailable=" +
                    $"{ownedChickenHuntPatch?.IsAvailable == true}, " +
                    $"granaryChickenLimitAvailable={granaryChickenLimitPatch?.IsAvailable == true}, " +
                    $"preyReservationHeartbeatAvailable={preyReservationHeartbeatPatch?.IsAvailable == true}, " +
                    $"rabbitMeleeOrderAvailable={rabbitMeleeOrderPatch?.IsAvailable == true}, " +
                    $"tannerMeatAvailable={tannerMeatPatch?.IsAvailable == true}, " +
                    $"hunterQueryActorWorkaroundAvailable={hunterQueryActorWorkaround?.IsAvailable == true}, " +
                    $"hunterHutVisibilityAvailable={hunterHutVisibilityPatch?.IsAvailable == true}, " +
                    $"hunterHutVisibilityApplied={hunterHutVisibilityPatch?.IsApplied == true}, " +
                    $"hunterNativeVisibilityProbeAvailable={hunterNativeVisibilityProbe?.IsAvailable == true}, " +
                    $"hunterActiveTargetVisibilityAvailable={hunterActiveTargetVisibilitySnapshot?.IsAvailable == true}, " +
                    $"hunterPclReachabilityAvailable={hunterPclReachability?.IsAvailable == true}, " +
                    $"hunterRemainingPathSpeedRecoveryAvailable={hunterRemainingPathSpeedRecovery?.IsAvailable == true}, " +
                    $"hunterPostShotContinuationAvailable={hunterPostShotContinuationDiagnostic?.IsAvailable == true}, " +
                    $"hunterTargetSearchFallbackAvailable={hunterTargetSearchFallbackDiagnostic?.IsAvailable == true}, " +
                    $"hunterVanillaPathContinuationAvailable={hunterVanillaPathContinuationDiagnostic?.IsAvailable == true}, " +
                    $"referenceHashMatches={referenceHashMatches}.");
        }

        private void RegisterSimulationTick()
        {
            if (!SimulationTickEvents.TryRegister(ImprovedHuntersPlugin.PluginGuid, "hunter-scan", OnSimulationTick, out string reason, 300))
                throw new InvalidOperationException(reason);

            simulationTickRegistered = true;
        }

        // Fixed map-tick boundaries keep the scan on the same ticks on all peers, also after a save load.
        private void OnSimulationTick(int mapTick)
        {
            if (!applied ||
                !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
            {
                return;
            }

            if (targetSelectionModeAllowed || pathfindingModeAllowed)
                wildlifeRespawn?.OnTick(mapTick);

            if (unchecked((uint)mapTick) % NativeScanIntervalMapTicks == 0)
            {
                long measured = 0;
                Shared.DevDiag.Start(ref measured);
                RunNativeScan(mapTick);
                Shared.DevDiag.StopSpike(ref measured, "hunters.scan", 5000);
            }
        }

        private unsafe void RunNativeScan(long timestamp)
        {
            try
            {
                // Re-apply cheap native/default patches here because settings and
                // some game globals can be recreated around map transitions.
                ApplyDespawnPatches();
                ApplyCamelHealthPatch();

                if (!targetSelectionModeAllowed && !pathfindingModeAllowed)
                    return;

                SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
                if (units._array == null || units.Length == 0)
                    return;

                List<IntPtr> hunters = scanHunters;
                List<IntPtr> eligiblePrey = scanEligiblePrey;
                hunters.Clear();
                eligiblePrey.Clear();
                int adjustedLiveCamels = 0;

                for (int index = 0; index < units.Length; index++)
                {
                    GameUnit* unit = units.GetValuePointer(index);
                    int unitId = index + 1;

                    if (TryClampLiveCamelHealth(unitId, unit))
                        adjustedLiveCamels++;

                    if (Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
                        IsRuntimeHuntingEnabled(unit->r_UnitChimp) &&
                        IsOwnerAllowedForAnyHunter(unitId, unit))
                    {
                        PreserveShortLivedCorpse(unit, timestamp);
                    }

                    if (unit->r_AliveState == AliveState.None)
                        continue;

                    if (unit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER)
                    {
                        hunters.Add((IntPtr)unit);
                        continue;
                    }

                    if (IsEligibleUnreservedPrey(unitId, unit))
                        eligiblePrey.Add((IntPtr)unit);
                }

                if (adjustedLiveCamels > 0)
                    LogCamelHealthPatch(adjustedLiveCamels);

                CleanupShortLivedCorpsePreserveCache(units, timestamp);
                hunterActiveTargetVisibilitySnapshot?.ProcessNativeScan(units, timestamp);
                RequeryHuntersWithUnreachableActivePrey(units, hunters, timestamp);
                RequeryHuntersWithBetterPrey(units, hunters, timestamp);
                long measured = 0;
                Shared.DevDiag.Start(ref measured);
                herdFleeSight?.Update(units);
                Shared.DevDiag.Stop(ref measured, "hunters.herdSight");
                Shared.DevDiag.Start(ref measured);
                UpdateCorpseChoices(units, hunters, timestamp);
                Shared.DevDiag.Stop(ref measured, "hunters.corpseChoice");
                TrackHunterPreyAndExpireCollectedCorpses(units, hunters, timestamp);
                RequeryIdleHuntersNearPrey(units, hunters, eligiblePrey, timestamp);
            }
            catch (Exception exception)
            {
                if (nativeScanFailureLogged)
                    return;

                Shared.DebugLogHelper.LogError(log, $"Improved Hunters native scan failed; native scan remains inactive after this error: {exception}");
                nativeScanFailureLogged = true;
            }
        }

        private unsafe void RequeryHuntersWithUnreachableActivePrey(
            SimpleNativeArray<GameUnit> units,
            List<IntPtr> hunters,
            long timestamp)
        {
            if (!CanRunHunterPathfinding() ||
                hunterPclReachability?.IsAvailable != true)
            {
                return;
            }

            foreach (IntPtr hunterAddress in hunters)
            {
                GameUnit* hunter = (GameUnit*)hunterAddress.ToPointer();
                if (hunter == null ||
                    !APIShared.UnitAccess.IsReallyAlive(hunter) ||
                    hunter->r_CurrentHealth == 0 ||
                    hunter->r_GlobalId == 0)
                {
                    continue;
                }

                byte* hunterBytes = (byte*)hunter;
                ushort aiState = *(ushort*)(hunterBytes + 0x2BC);
                ushort targetUnitId = *(ushort*)(hunterBytes + 0x39A);
                uint targetGlobalId = *(uint*)(hunterBytes + 0x39C);
                if (aiState != 1 ||
                    targetUnitId == 0 ||
                    targetUnitId > units.Length ||
                    targetGlobalId == 0)
                {
                    continue;
                }

                GameUnit* prey = units.GetValuePointer(targetUnitId - 1);
                if (prey == null ||
                    !APIShared.UnitAccess.IsReallyAlive(prey) ||
                    prey->r_CurrentHealth == 0 ||
                    prey->r_GlobalId != targetGlobalId ||
                    !settings.IsKnownAnimal(prey->r_UnitChimp) ||
                    !settings.IsHuntingEnabled(prey->r_UnitChimp))
                {
                    continue;
                }

                int hunterUnitId = checked((int)(hunter - units._array) + 1);
                if (!hunterPclReachability.TryRefreshActiveTargetReachability(
                        hunterUnitId,
                        targetUnitId,
                        targetGlobalId,
                        prey->r_UnitChimp,
                        timestamp,
                        out bool reachable) ||
                    reachable)
                {
                    continue;
                }

                // Revalidate after the native query. Invalidating only the stored
                // global ID enters HunterUpdate's own state-1 identity-failure
                // branch, which stops the old order and runs Vanilla's search.
                if (!APIShared.UnitAccess.IsReallyAlive(hunter) ||
                    hunter->r_CurrentHealth == 0 ||
                    *(ushort*)(hunterBytes + 0x2BC) != 1 ||
                    *(ushort*)(hunterBytes + 0x39A) != targetUnitId ||
                    *(uint*)(hunterBytes + 0x39C) != targetGlobalId ||
                    !APIShared.UnitAccess.IsReallyAlive(prey) ||
                    prey->r_CurrentHealth == 0 ||
                    prey->r_GlobalId != targetGlobalId)
                {
                    continue;
                }

                if (!TryAbandonHunterTarget(hunterBytes, hunterUnitId))
                    continue;

                abortedTargetCooldowns.Remove(
                    new HunterPreyCooldownKey(hunterUnitId, targetGlobalId));
            }
        }

        // Invalidating only the stored global ID enters HunterUpdate's own state-1 identity-failure
        // branch, which stops the old order and runs Vanilla's search. The reservation runs out by itself.
        private unsafe bool TryAbandonHunterTarget(byte* hunterBytes, int hunterUnitId)
        {
            *(uint*)(hunterBytes + 0x39C) = 0;
            if (*(uint*)(hunterBytes + 0x39C) != 0)
                return false;

            activeHunterTargets.Remove(hunterUnitId);
            bestTargetCache.Remove(hunterUnitId);
            return true;
        }

        // A Hunter on his way changes his mind only for clearly closer prey the ranking also prefers.
        // Once he has shot he stays with that prey, and the old target's cooldown prevents a change back.
        private unsafe void RequeryHuntersWithBetterPrey(
            SimpleNativeArray<GameUnit> units,
            List<IntPtr> hunters,
            long timestamp)
        {
            if (!CanRunHunterTargetSelection())
                return;

            long scan = timestamp / NativeScanIntervalMapTicks;
            foreach (IntPtr hunterAddress in hunters)
            {
                GameUnit* hunter = (GameUnit*)hunterAddress.ToPointer();
                if (hunter == null || !APIShared.UnitAccess.IsReallyAlive(hunter) || hunter->r_CurrentHealth == 0)
                    continue;

                byte* hunterBytes = (byte*)hunter;
                int hunterUnitId = checked((int)(hunter - units._array) + 1);
                ushort targetUnitId = *(ushort*)(hunterBytes + 0x39A);
                uint targetGlobalId = *(uint*)(hunterBytes + 0x39C);
                if ((scan + hunterUnitId) % BetterPreyCheckEveryScans != 0 ||
                    *(ushort*)(hunterBytes + 0x2BC) != 1 ||
                    targetUnitId == 0 ||
                    targetUnitId > units.Length ||
                    targetGlobalId == 0)
                {
                    continue;
                }

                if (nextBetterPreyChecks.TryGetValue(hunterUnitId, out long nextCheck) && timestamp < nextCheck)
                    continue;

                GameUnit* current = units.GetValuePointer(targetUnitId - 1);
                if (current == null || current->r_GlobalId != targetGlobalId ||
                    hunterPostShotContinuationDiagnostic?.HasShotInProgress(hunterUnitId) == true)
                {
                    continue;
                }

                int currentDistance = GetChebyshevDistance(
                    hunter->r_CurrentTilePositionX,
                    hunter->r_CurrentTilePositionY,
                    current->r_CurrentTilePositionX,
                    current->r_CurrentTilePositionY);
                if (currentDistance <= HunterShootingDistance ||
                    !TryGetTargetSelectionForHunter(hunterUnitId, timestamp, out TargetSelection selection) ||
                    selection.BestTarget.UnitId == targetUnitId ||
                    selection.IsAllowed(targetUnitId) ||
                    selection.BestTarget.UnitId <= 0 ||
                    selection.BestTarget.UnitId > units.Length)
                {
                    continue;
                }

                GameUnit* better = units.GetValuePointer(selection.BestTarget.UnitId - 1);
                int betterDistance = GetChebyshevDistance(
                    hunter->r_CurrentTilePositionX,
                    hunter->r_CurrentTilePositionY,
                    better->r_CurrentTilePositionX,
                    better->r_CurrentTilePositionY);
                if (currentDistance - betterDistance < BetterPreyMinimumGainTiles ||
                    !TryAbandonHunterTarget(hunterBytes, hunterUnitId))
                {
                    continue;
                }

                nextBetterPreyChecks[hunterUnitId] = timestamp + BetterPreyPauseMapTicks;
                SetTargetCooldownUntil(
                    new HunterPreyCooldownKey(hunterUnitId, targetGlobalId),
                    timestamp + AbortedTargetCooldownInterval);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters Hunter {hunterUnitId} changed to closer prey: " +
                    $"{targetUnitId}/{currentDistance} tiles -> {selection.BestTarget.UnitId}/{betterDistance} tiles.");
            }
        }

        private unsafe void RequeryIdleHuntersNearPrey(
            SimpleNativeArray<GameUnit> units,
            List<IntPtr> hunters,
            List<IntPtr> eligiblePrey,
            long timestamp)
        {
            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) ||
                (eligiblePrey.Count == 0 && freeCorpses.Count == 0))
            {
                return;
            }

            foreach (IntPtr hunterAddress in hunters)
            {
                GameUnit* hunter = (GameUnit*)hunterAddress.ToPointer();
                byte* hunterBytes = (byte*)hunter;
                ushort aiState = *(ushort*)(hunterBytes + 0x2BC);
                ushort targetUnitId = *(ushort*)(hunterBytes + 0x39A);
                ushort wanderMode = *(ushort*)(hunterBytes + 0x370);

                if (aiState != 0x06 || targetUnitId != 0 || wanderMode != 1)
                    continue;

                int hunterId = checked((int)(hunter - units._array) + 1);

                if (nextIdleHunterRequeryTimestamps.TryGetValue(hunterId, out long nextTimestamp) &&
                    timestamp < nextTimestamp)
                {
                    continue;
                }

                short hunterTileX = *(short*)(hunterBytes + 0xC0);
                short hunterTileY = *(short*)(hunterBytes + 0xC2);
                int hunterOwner = (APIShared.UnitAccess.TryGetById(hunterId, out _, out _) ? GameUnitManagerAPI.Instance.GetOwner(hunterId) : -1);
                bool preyInSearchRadius = hunterCorpseChoicePatch?.HasChoice(hunterId) == true;

                foreach (IntPtr preyAddress in eligiblePrey)
                {
                    GameUnit* prey = (GameUnit*)preyAddress.ToPointer();
                    if (!IsOwnerAllowed(hunterOwner, prey))
                        continue;

                    byte* preyBytes = (byte*)preyAddress.ToPointer();
                    short preyTileX = *(short*)(preyBytes + 0xC0);
                    short preyTileY = *(short*)(preyBytes + 0xC2);

                    if (Math.Max(
                            Math.Abs(preyTileX - hunterTileX),
                            Math.Abs(preyTileY - hunterTileY)) <= HunterSearchRadius)
                    {
                        preyInSearchRadius = true;
                        break;
                    }
                }

                if (!preyInSearchRadius)
                    continue;

                *(ushort*)(hunterBytes + 0x2BC) = 0;
                *(ushort*)(hunterBytes + 0x2C4) = 0;
                nextIdleHunterRequeryTimestamps[hunterId] = timestamp + IdleHunterRequeryInterval;
            }
        }

        private unsafe void TrackHunterPreyAndExpireCollectedCorpses(
            SimpleNativeArray<GameUnit> units,
            List<IntPtr> hunters,
            long timestamp)
        {
            foreach (IntPtr hunterAddress in hunters)
            {
                GameUnit* hunter = (GameUnit*)hunterAddress.ToPointer();
                byte* hunterBytes = (byte*)hunter;
                int hunterId = checked((int)(hunter - units._array) + 1);
                ushort targetUnitId = *(ushort*)(hunterBytes + 0x39A);
                uint targetGlobalId = *(uint*)(hunterBytes + 0x39C);
                TrackHunterTargetState(units, hunterId, targetUnitId, targetGlobalId, timestamp);

                if (targetUnitId == 0 || targetUnitId > units.Length)
                    continue;

                GameUnit* target = units.GetValuePointer(targetUnitId - 1);
                if (!settings.IsKnownAnimal(target->r_UnitChimp))
                    continue;

                // The yield is decided at the hut, when the corpse is long gone, and a Hunter with meat
                // in his hut searches no new target (0x12FC70). Simulation data on fixed ticks, as on all peers.
                if (target->r_GlobalId == targetGlobalId)
                    hunterPreyTypes[hunterId] = target->r_UnitChimp;

                byte* targetBytes = (byte*)target;
                if (*(ushort*)(hunterBytes + 0x2BC) != 0x02 ||
                    !IsShortLivedPrey(target->r_UnitChimp) ||
                    *(uint*)(targetBytes + 0x94) != targetGlobalId ||
                    !IsPreservableCorpseState(*(ushort*)(targetBytes + 0x2BC)))
                {
                    continue;
                }

                ushort deathTimer = *(ushort*)(targetBytes + 0x2C4);
                if (deathTimer <= CollectedCorpseDespawnTicks)
                    *(ushort*)(targetBytes + 0x2C4) = CollectedCorpseDespawnTicks;

                if (loggedCollectedCorpseGlobalIds.Add(targetGlobalId))
                    target->r_AliveState = AliveState.MarkedForDeletion;
            }
        }

        private unsafe void PreserveShortLivedCorpse(GameUnit* unit, long currentMapTick)
        {
            if (!IsTrackedShortLivedCorpse(unit))
                return;

            byte* unitBytes = (byte*)unit;
            ushort reservation = *(ushort*)(unitBytes + 0x448);
            if (reservation > 2)
                return;

            // The native timer can jump past small thresholds at high game speed.
            // Use map ticks instead of real time, so the 60-second preserve target
            // is reached at speed 40 and scales naturally with game speed and pause.
            uint globalId = unit->r_GlobalId;
            if (!shortLivedCorpsePreserveUntil.TryGetValue(globalId, out long preserveUntil))
            {
                preserveUntil = currentMapTick + ShortLivedCorpseVisiblePreserveMapTicksAtSpeed40;
                shortLivedCorpsePreserveUntil[globalId] = preserveUntil;
                LogShortLivedCorpsePreserve(
                    $"Improved Hunters corpse visible preserve started: unit={globalId}/{unit->r_UnitChimp}, " +
                    $"mapTicks={ShortLivedCorpseVisiblePreserveMapTicksAtSpeed40}, baselineSpeed=40.");
            }
            else if (preserveUntil == ExpiredShortLivedCorpsePreserve)
            {
                // Keep the expired marker while the unit still exists. Otherwise
                // the next scan would start a fresh 60-second preserve window.
                return;
            }

            if (currentMapTick > preserveUntil)
            {
                shortLivedCorpsePreserveUntil[globalId] = ExpiredShortLivedCorpsePreserve;
                LogShortLivedCorpsePreserve(
                    $"Improved Hunters corpse visible preserve expired: unit={globalId}/{unit->r_UnitChimp}, " +
                    $"currentMapTick={currentMapTick}, preserveUntil={preserveUntil}.");
                return;
            }

            ushort deathTimer = *(ushort*)(unitBytes + 0x2C4);
            if (deathTimer > 0)
                *(ushort*)(unitBytes + 0x2C4) = 0;
        }

        private unsafe void CleanupShortLivedCorpsePreserveCache(SimpleNativeArray<GameUnit> units, long timestamp)
        {
            if (shortLivedCorpsePreserveUntil.Count == 0 ||
                timestamp < nextShortLivedCorpsePreserveCleanupTimestamp)
            {
                return;
            }

            nextShortLivedCorpsePreserveCleanupTimestamp = timestamp + ShortLivedCorpsePreserveCleanupInterval;

            // Run this only every few seconds. It prevents stale preserve entries
            // from accumulating after the engine removes corpses from the unit list.
            HashSet<uint> activeCorpseGlobalIds = new HashSet<uint>();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit != null && IsTrackedShortLivedCorpse(unit))
                    activeCorpseGlobalIds.Add(unit->r_GlobalId);
            }

            List<uint> staleGlobalIds = null;
            foreach (uint globalId in shortLivedCorpsePreserveUntil.Keys)
            {
                if (activeCorpseGlobalIds.Contains(globalId))
                    continue;

                if (staleGlobalIds == null)
                    staleGlobalIds = new List<uint>();

                staleGlobalIds.Add(globalId);
            }

            if (staleGlobalIds == null)
                return;

            for (int index = 0; index < staleGlobalIds.Count; index++)
                shortLivedCorpsePreserveUntil.Remove(staleGlobalIds[index]);
        }

        private unsafe bool IsEligibleUnreservedPrey(int unitId, GameUnit* prey)
        {
            return TryGetPreyEligibility(unitId, prey, out PreyEligibility eligibility) &&
                eligibility.Eligible;
        }

        private unsafe bool TryGetPreyEligibility(int unitId, GameUnit* prey, out PreyEligibility eligibility)
        {
            eligibility = default;
            if (prey == null)
                return false;

            eligibility.Type = prey->r_UnitChimp;
            eligibility.KnownAnimal = settings.IsKnownAnimal(eligibility.Type);
            if (!eligibility.KnownAnimal)
                return false;

            byte* preyBytes = (byte*)prey;
            eligibility.GlobalId = prey->r_GlobalId;
            eligibility.TileX = prey->r_CurrentTilePositionX;
            eligibility.TileY = prey->r_CurrentTilePositionY;
            eligibility.RuntimeHuntingEnabled = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) && IsRuntimeHuntingEnabled(eligibility.Type);
            eligibility.OwnerAllowed = IsOwnerAllowedForAnyHunter(unitId, prey);
            eligibility.AliveState = *(short*)(preyBytes + 0x88);
            eligibility.FlagsAt92 = *(ushort*)(preyBytes + 0x92);
            eligibility.AiState = *(ushort*)(preyBytes + 0x2BC);
            eligibility.CorpseFlag = *(ushort*)(preyBytes + 0x29C);
            eligibility.Reservation = *(ushort*)(preyBytes + 0x448);
            eligibility.FlagsAllowed =
                eligibility.Type == eChimps.CHIMP_TYPE_CHICKEN ||
                eligibility.FlagsAt92 == 0;
            bool isLivePrey = eligibility.CorpseFlag == 0 && prey->r_CurrentHealth > 0;
            eligibility.Eligible =
                eligibility.RuntimeHuntingEnabled &&
                eligibility.OwnerAllowed &&
                eligibility.AliveState == (short)AliveState.IsAlive &&
                eligibility.FlagsAllowed &&
                eligibility.Reservation == 0 &&
                isLivePrey;

            return eligibility.KnownAnimal;
        }

        private void OnMapStarted()
        {
            herdFleeSight?.ResetForMap();
            ResetCorpseChoices();
            hunterPreyTypes.Clear();
            nextBetterPreyChecks.Clear();
            nextIdleHunterRequeryTimestamps.Clear();
            loggedCollectedCorpseGlobalIds.Clear();
            shortLivedCorpsePreserveUntil.Clear();
            ClearTargetSelectionCaches();
            hunterNativeVisibilityProbe?.ResetForMap();
            hunterActiveTargetVisibilitySnapshot?.ResetForMap();
            hunterPclReachability?.ResetForMap();
            hunterPostShotContinuationDiagnostic?.ResetForMap();
            hunterTargetSearchFallbackDiagnostic?.ResetForMap();
            hunterRemainingPathSpeedRecovery?.ResetForMap();
            hunterVanillaPathContinuationDiagnostic?.ResetForMap();
            APIShared.GameModes.GameModeSnapshot gameMode = Shared.GameplayModActivationGate.Snapshot;
            // Without the tick callback nothing would maintain the state these features read.
            targetSelectionModeAllowed = simulationTickRegistered && Shared.GameplayFeatureModePolicy.IsAllowed(
                ImprovedHuntersPlugin.PluginGuid,
                Shared.GameplayFeatureId.ImprovedHunterTargetSelection,
                gameMode);
            pathfindingModeAllowed = simulationTickRegistered && Shared.GameplayFeatureModePolicy.IsAllowed(
                ImprovedHuntersPlugin.PluginGuid,
                Shared.GameplayFeatureId.ImprovedHunterPathfinding,
                gameMode);
            ApplyHunterHutVisibilityPatch();
            Shared.DebugLogHelper.LogInfo(
                log,
                "Improved Hunters feature mode gates: " +
                $"targetSelectionAllowed={targetSelectionModeAllowed}, " +
                $"pathfindingAllowed={pathfindingModeAllowed}, {gameMode.ToDiagnosticString()}.");
            nativeScanFailureLogged = false;
            // Simulation state is only written from the tick callback, which follows this start.
            TryInitializeFeature("despawn settings", ApplyDespawnPatches);
            TryInitializeFeature("camel health", ApplyCamelHealthPatch);
        }

        private void OnHunterPickUpMeat(UnitHunterPickUpMeatEventArgs args)
        {
            if (!IsValidUnitId(args.UnitId))
            {
                LogInvalidHunterEvent("pickup-meat", args.UnitId);
                return;
            }

            hunterMeatPickupTimestamps[args.UnitId] = HunterSimulationTime.Now;
            TryDeleteCollectedShortLivedCorpse(args.UnitId);
            activeHunterTargets.Remove(args.UnitId);
            bestTargetCache.Remove(args.UnitId);
        }

        private void OnHunterDropOffMeat(UnitHunterDropOffMeatEventArgs args)
        {
            if (!IsValidUnitId(args.UnitId))
            {
                LogInvalidHunterEvent("dropoff-meat", args.UnitId);
                return;
            }

            if (!hunterMeatPickupTimestamps.TryGetValue(args.UnitId, out long pickupTimestamp))
                return;

            hunterMeatPickupTimestamps.Remove(args.UnitId);
            long elapsedTicks = HunterSimulationTime.Now - pickupTimestamp;
            double elapsedSeconds = elapsedTicks / (double)HunterSimulationTime.MapTicksPerSecond;
            if (hunterTargetDiagnosticLogs < MaxHunterTargetDiagnosticLogs)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters meat trip timing: hunter={args.UnitId}, pickupToDropoffSeconds={elapsedSeconds:F2}, " +
                    $"assumedHutWorkTicks={HunterHutWorkCost}.");
            }
        }

        private unsafe void TryDeleteCollectedShortLivedCorpse(int hunterUnitId)
        {
            if (!TryGetCollectedCorpseTarget(hunterUnitId, out HunterTargetSnapshot target) ||
                !APIShared.UnitAccess.TryGetById(target.UnitId, out GameUnit* unit, out _) ||
                unit == null ||
                unit->r_GlobalId != target.GlobalId ||
                !IsShortLivedPrey(unit->r_UnitChimp))
            {
                return;
            }

            byte* unitBytes = (byte*)unit;
            shortLivedCorpsePreserveUntil.Remove(target.GlobalId);
            if (*(ushort*)(unitBytes + 0x29C) == 0 ||
                !IsPreservableCorpseState(*(ushort*)(unitBytes + 0x2BC)))
            {
                return;
            }

            *(ushort*)(unitBytes + 0x2C4) = CollectedCorpseDespawnTicks;
            unit->r_AliveState = AliveState.MarkedForDeletion;

            if (loggedCollectedCorpseGlobalIds.Add(target.GlobalId) &&
                hunterTargetDiagnosticLogs < MaxHunterTargetDiagnosticLogs)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters collected corpse removed: hunter={hunterUnitId}, target={target.UnitId}, " +
                    $"globalId={target.GlobalId}, aiState=0x{*(ushort*)(unitBytes + 0x2BC):X}.");
            }
        }

        private void LogShortLivedCorpsePreserve(string message)
        {
            if (shortLivedCorpsePreserveLogs >= 80)
                return;

            shortLivedCorpsePreserveLogs++;
            Shared.DebugLogHelper.LogInfo(log, $"{message} ({shortLivedCorpsePreserveLogs}/80).");

            if (shortLivedCorpsePreserveLogs == 80)
                Shared.DebugLogHelper.LogInfo(log, "Improved Hunters corpse visible preserve diagnostic limit reached.");
        }

        private unsafe bool TryGetCollectedCorpseTarget(int hunterUnitId, out HunterTargetSnapshot target)
        {
            if (APIShared.UnitAccess.TryGetById(hunterUnitId, out GameUnit* hunter, out _) &&
                hunter != null &&
                hunter->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER)
            {
                byte* hunterBytes = (byte*)hunter;
                ushort targetUnitId = *(ushort*)(hunterBytes + 0x39A);
                uint targetGlobalId = *(uint*)(hunterBytes + 0x39C);
                if (targetUnitId != 0 && targetGlobalId != 0)
                {
                    target = new HunterTargetSnapshot(targetUnitId, targetGlobalId);
                    return true;
                }
            }

            return activeHunterTargets.TryGetValue(hunterUnitId, out target);
        }

        private unsafe void TrackHunterTargetState(
            SimpleNativeArray<GameUnit> units,
            int hunterUnitId,
            ushort targetUnitId,
            uint targetGlobalId,
            long timestamp)
        {
            if (!CanRunHunterPathfinding())
                return;

            bool hasCurrentTarget = targetUnitId != 0 && targetGlobalId != 0;
            if (!activeHunterTargets.TryGetValue(hunterUnitId, out HunterTargetSnapshot previousTarget))
            {
                if (hasCurrentTarget)
                    activeHunterTargets[hunterUnitId] = new HunterTargetSnapshot(targetUnitId, targetGlobalId);

                return;
            }

            if (hasCurrentTarget &&
                previousTarget.UnitId == targetUnitId &&
                previousTarget.GlobalId == targetGlobalId)
            {
                return;
            }

            activeHunterTargets.Remove(hunterUnitId);
            bestTargetCache.Remove(hunterUnitId);
            HunterPreyCooldownKey cooldownKey = new HunterPreyCooldownKey(hunterUnitId, previousTarget.GlobalId);
            SetTargetCooldownUntil(cooldownKey, timestamp + AbortedTargetCooldownInterval);

            if (hasCurrentTarget)
                activeHunterTargets[hunterUnitId] = new HunterTargetSnapshot(targetUnitId, targetGlobalId);
        }

        private void LogInvalidHunterEvent(string eventName, int unitId)
        {
            if (invalidHunterEventLogs >= MaxInvalidHunterEventLogs)
                return;

            invalidHunterEventLogs++;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Improved Hunters ignored invalid hunter event: event={eventName}, unitId={unitId}, " +
                $"outcome=skipped-before-unit-lookup ({invalidHunterEventLogs}/{MaxInvalidHunterEventLogs}).");
        }

        private bool IsTargetOnCooldown(int hunterUnitId, uint preyGlobalId, long timestamp)
        {
            HunterPreyCooldownKey key = new HunterPreyCooldownKey(hunterUnitId, preyGlobalId);
            if (!abortedTargetCooldowns.TryGetValue(key, out long expiresAt))
                return false;

            if (timestamp < expiresAt)
                return true;

            abortedTargetCooldowns.Remove(key);
            return false;
        }

        private void SetTargetCooldownUntil(HunterPreyCooldownKey key, long expiresAt)
        {
            // Preserve the later expiry when two independent abort observations
            // report the same Hunter/prey pair.
            if (abortedTargetCooldowns.TryGetValue(key, out long currentExpiresAt) &&
                currentExpiresAt >= expiresAt)
            {
                return;
            }

            abortedTargetCooldowns[key] = expiresAt;
        }

        private void LogHunterTargetQueryDiagnostic(
            int hunterUnitId,
            int queryUnitId,
            eChimps queryType,
            bool isValidTarget,
            bool usedFallback,
            bool targetPclUnreachable,
            bool targetOnCooldown,
            TargetSelection targetSelection)
        {
            if (hunterTargetDiagnosticLogs >= MaxHunterTargetDiagnosticLogs)
                return;

            hunterTargetDiagnosticLogs++;
            BestTarget bestTarget = targetSelection.BestTarget;
            string bestText = bestTarget.UnitId == 0
                ? "none"
                : $"{bestTarget.UnitId}/{bestTarget.Type}/meat={bestTarget.MeatAmount}/" +
                  $"approachHeuristic={bestTarget.ApproachHeuristicCost}/" +
                  $"granaryRoundTripHeuristic={bestTarget.GranaryRoundTripHeuristicCost}/" +
                  $"hutWork={HunterHutWorkCost}/cycleHeuristic={bestTarget.CycleCost}/" +
                  $"allowedNearBest={targetSelection.AllowedCount}";

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Improved Hunters target query: hunter={hunterUnitId}, candidate={queryUnitId}/{queryType}, " +
                $"allowed={isValidTarget}, fallback={usedFallback}, pclUnreachable={targetPclUnreachable}, " +
                $"cooldown={targetOnCooldown}, best={bestText} " +
                $"({hunterTargetDiagnosticLogs}/{MaxHunterTargetDiagnosticLogs}).");

            if (hunterTargetDiagnosticLogs == MaxHunterTargetDiagnosticLogs)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Improved Hunters target query diagnostic limit reached; continuing with periodic summaries only.");
            }
        }

        private unsafe void OnCalculateBonusYield(UnitCalculateBonusYieldEventArgs args)
        {
            if (!Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) ||
                !IsValidUnitId(args.UnitId) ||
                (APIShared.UnitAccess.TryGetById(args.UnitId, out _, out _) ? GameUnitManagerAPI.Instance.GetType(args.UnitId) : eChimps.CHIMP_TYPE_NULL) != eChimps.CHIMP_TYPE_HUNTER ||
                !hunterPreyTypes.TryGetValue(args.UnitId, out eChimps preyType) ||
                !IsRuntimeHuntingEnabled(preyType))
            {
                return;
            }

            // The setting replaces Vanilla's base amount; Vanilla still adds its bonus, as for all meat.
            args.GoodAmount = settings.GetMeatAmount(preyType);
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod))
            {
                if (Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                    SubscribeRuntimeEvents();
                else
                    UnsubscribeRuntimeEvents();
            }

            bool huntingTargetSettingChanged =
                propertyName == nameof(ImprovedHuntersViewModel.HuntDeer) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntGoat) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntRabbit) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntCamel) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntChicken);

            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntRabbit) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntCamel) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntChicken))
            {
                ApplyPreyReservationHeartbeat();
            }

            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                propertyName == nameof(ImprovedHuntersViewModel.HunterHutAlwaysAvailable) ||
                propertyName == nameof(ImprovedHuntersViewModel.DeerFleeOnlyInSight))
            {
                ApplyWildlifeRules();
            }

            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                propertyName == nameof(ImprovedHuntersViewModel.CollectFreeCorpses))
            {
                ApplyCorpseChoiceRule();
            }

            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                propertyName == nameof(ImprovedHuntersViewModel.RabbitsOnlyOnOrder))
            {
                ApplyRabbitMeleeOrderRule();
            }

            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                propertyName == nameof(ImprovedHuntersViewModel.TannerMeatMode) ||
                propertyName == nameof(ImprovedHuntersViewModel.TannerMeatPerTrip))
            {
                ApplyTannerMeatRule();
            }

            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntChicken))
            {
                ownedChickenHuntPatch?.SetEnabled(IsOwnedChickenHuntingEnabled);
            }

            ClearTargetSelectionCaches();
            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                huntingTargetSettingChanged ||
                propertyName == nameof(ImprovedHuntersViewModel.ImprovedTargetSelection) ||
                propertyName == nameof(ImprovedHuntersViewModel.ImprovedPathfinding))
            {
                hunterActiveTargetVisibilitySnapshot?.ResetForMap();
                hunterPclReachability?.ResetForMap();
                hunterPostShotContinuationDiagnostic?.ResetForMap();
                hunterTargetSearchFallbackDiagnostic?.ResetForMap();
                hunterRemainingPathSpeedRecovery?.ResetForMap();
                hunterVanillaPathContinuationDiagnostic?.ResetForMap();
            }

            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                propertyName == nameof(ImprovedHuntersViewModel.ImprovedPathfinding))
            {
                ApplyHunterHutVisibilityPatch();
            }

            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntRabbit) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntCamel) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntChicken))
            {
                ApplyDespawnPatches();
            }

            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntCamel))
            {
                ApplyCamelHealthPatch();
            }
        }

        private void OnModeStateChanged(bool allowed)
        {
            if (!applied)
                return;

            OnSettingChanged(nameof(ImprovedHuntersViewModel.EnableMod));
        }

        private void InitializeOwnedChickenHuntPatch(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                ownedChickenHuntPatch = new OwnedChickenHuntPatch(log, hookRegion, memory, imageBase, referenceHashMatches);
                ownedChickenHuntPatch.SetEnabled(IsOwnedChickenHuntingEnabled);
            }
            catch (Exception exception)
            {
                ownedChickenHuntPatch?.Dispose();
                ownedChickenHuntPatch = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters owned-chicken hunting is unavailable; chickens stay Vanilla and other prey features remain active: {exception}");
            }
        }

        private void InitializeGranaryChickenLimitPatch(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                granaryChickenLimitPatch = new GranaryChickenLimitPatch(
                    log,
                    settings,
                    hookRegion,
                    memory,
                    imageBase,
                    referenceHashMatches,
                    canManageChickens: () => IsOwnedChickenHuntingEnabled);
            }
            catch (Exception exception)
            {
                granaryChickenLimitPatch?.Dispose();
                granaryChickenLimitPatch = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters chicken population setting is unavailable; Vanilla's food-based population remains active: {exception}");
            }
        }

        private void InitializePreyReservationHeartbeatPatch(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                preyReservationHeartbeatPatch = new PreyReservationHeartbeatPatch(log, hookRegion, memory, imageBase, referenceHashMatches);
                ApplyPreyReservationHeartbeat();
            }
            catch (Exception exception)
            {
                preyReservationHeartbeatPatch?.Dispose();
                preyReservationHeartbeatPatch = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters reservation countdown is unavailable; rabbits, camels and chickens are not hunted because their reservations could never expire: {exception}");
            }
        }

        private void InitializeRabbitMeleeOrderPatch(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                rabbitMeleeOrderPatch = new RabbitMeleeOrderPatch(log, hookRegion, memory, imageBase, referenceHashMatches);
                ApplyRabbitMeleeOrderRule();
            }
            catch (Exception exception)
            {
                rabbitMeleeOrderPatch?.Dispose();
                rabbitMeleeOrderPatch = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters melee rabbit rule is unavailable; melee units keep Vanilla's behaviour: {exception}");
            }
        }

        private void InitializeWildlifeFeatures(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                hunterHutAvailabilityPatch = new HunterHutAvailabilityPatch(log, hookRegion, memory, imageBase, referenceHashMatches);
            }
            catch (Exception exception)
            {
                hunterHutAvailabilityPatch?.Dispose();
                hunterHutAvailabilityPatch = null;
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters leaves the Hunter's hut availability to Vanilla: {exception}");
            }

            try
            {
                var addresses = new WildlifeNativeAddresses(log, memory, imageBase, referenceHashMatches);
                wildlifeRespawn = new WildlifeRespawnFeature(log, settings, addresses);
                herdFleeSight = new HerdFleeSightFeature(log, hookRegion, memory, imageBase, referenceHashMatches, addresses, hunterNativeVisibilityProbe);
            }
            catch (Exception exception)
            {
                herdFleeSight?.Dispose();
                herdFleeSight = null;
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters wildlife features are unavailable: {exception}");
            }

            ApplyWildlifeRules();
        }

        private void ApplyWildlifeRules()
        {
            bool modEnabled = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod);
            hunterHutAvailabilityPatch?.SetEnabled(modEnabled && settings.HunterHutAlwaysAvailable);
            herdFleeSight?.SetEnabled(modEnabled && settings.DeerFleeOnlyInSight);
        }

        private void InitializeTannerMeatPatch(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            var table = new TannerRunTable();
            try
            {
                tannerRatioSync = new TannerRatioSync(log, table, () => tannerMeatPatch?.IsAvailable == true && IsTannerMeatModeEnabled);
                tannerMeatPatch = new TannerMeatPatch(log, hookRegion, memory, imageBase, referenceHashMatches, table);
                ApplyTannerMeatRule();
                TannerModePanel.Install(log, () => tannerMeatPatch?.IsAvailable == true && IsTannerMeatModeEnabled, table, tannerRatioSync);
            }
            catch (Exception exception)
            {
                tannerMeatPatch?.Dispose();
                tannerMeatPatch = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters Tanner meat mode is unavailable; Tanneries keep Vanilla's behaviour: {exception}");
            }
        }

        private void InitializeHunterQueryActorWorkaround(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                hunterQueryActorWorkaround = new HunterQueryActorWorkaround(
                    log,
                    hookRegion,
                    memory,
                    imageBase,
                    referenceHashMatches);
            }
            catch (Exception exception)
            {
                hunterQueryActorWorkaround?.Dispose();
                hunterQueryActorWorkaround = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters temporary Script Extender issue-123 workaround is unavailable; " +
                    $"validated Hunter IDs may still be used, but unresolved query actors leave Vanilla unchanged: {exception}");
            }
        }

        private void InitializeHunterNativeVisibilityProbe(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                hunterNativeVisibilityProbe = new HunterNativeVisibilityProbe(
                    log,
                    settings,
                    memory,
                    imageBase,
                    referenceHashMatches);
            }
            catch (Exception exception)
            {
                hunterNativeVisibilityProbe?.Dispose();
                hunterNativeVisibilityProbe = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters native visibility probe is unavailable; " +
                    $"Hunter behavior remains unchanged: {exception}");
            }
        }

        private void InitializeHunterTargetSearchFallbackDiagnostic(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                hunterTargetSearchFallbackDiagnostic = new HunterTargetSearchFallbackDiagnostic(
                    log,
                    settings,
                    hookRegion,
                    memory,
                    imageBase,
                    referenceHashMatches,
                    CanRunHunterTargetSelection,
                    CanRunHunterPathfinding,
                    TryPrepareHunterStateOneNearRefresh,
                    TryPrepareHunterPostShotStateZeroContinuation,
                    TryValidateHunterPostShotContinuation,
                    RecordAcceptedHunterPostShotAttack,
                    RecordFailedHunterPostShotAttack,
                    RecordHunterPostShotStateZeroHandoff,
                    RecordHunterPostShotMoveHereResult,
                    ResetHunterPostShotAttemptBudget,
                    RegisterRejectedHunterStateZeroMove,
                    RecordHunterPclMoveHereResult);
            }
            catch (Exception exception)
            {
                hunterTargetSearchFallbackDiagnostic?.Dispose();
                hunterTargetSearchFallbackDiagnostic = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Improved Hunters target-search fallback diagnostic is unavailable; " +
                    $"Hunter behavior remains unchanged: {exception}");
            }
        }

        private void InitializeHunterPostShotContinuationDiagnostic(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                hunterPostShotContinuationDiagnostic = new HunterPostShotContinuationDiagnostic(
                    log,
                    settings,
                    hookRegion,
                    memory,
                    imageBase,
                    referenceHashMatches,
                    CanRunHunterPathfinding,
                    TryValidateHunterPostShotContinuation,
                    RegisterRejectedHunterStateZeroMove);
            }
            catch (Exception exception)
            {
                hunterPostShotContinuationDiagnostic?.Dispose();
                hunterPostShotContinuationDiagnostic = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Improved Hunters post-shot continuation diagnostic is unavailable; " +
                    $"Hunter behavior remains unchanged: {exception}");
            }
        }

        private void InitializeHunterPclReachability(bool referenceHashMatches)
        {
            try
            {
                hunterPclReachability = new HunterPclReachability(
                    log,
                    referenceHashMatches,
                    CanRunHunterReachability);
            }
            catch (Exception exception)
            {
                hunterPclReachability?.Dispose();
                hunterPclReachability = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Improved Hunters native PCL reachability filter is unavailable; " +
                    $"target selection remains unchanged: {exception}");
            }
        }

        private void RecordHunterPclMoveHereResult(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            int moveHereResult,
            long timestamp)
        {
            if (moveHereResult != 0 && CanRunHunterPathfinding())
            {
                try
                {
                    hunterActiveTargetVisibilitySnapshot?.RecordAcceptedVanillaPath(
                        hunterUnitId,
                        preyUnitId,
                        preyGlobalId,
                        preyType);
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        "Improved Hunters accepted path-generation recording failed independently; " +
                        $"hunter={hunterUnitId}, target={preyUnitId}/{preyGlobalId}/{preyType}, " +
                        $"error={exception.Message}.");
                }

                hunterPclReachability?.TryPromoteSelectionResultToActiveTarget(
                    hunterUnitId,
                    preyUnitId,
                    preyGlobalId,
                    preyType,
                    timestamp);
            }

        }

        private void InitializeHunterActiveTargetVisibilitySnapshot()
        {
            try
            {
                hunterActiveTargetVisibilitySnapshot =
                    new HunterActiveTargetVisibilitySnapshot(
                        log,
                        settings,
                        hunterNativeVisibilityProbe,
                        CanRunHunterPathfinding);
            }
            catch (Exception exception)
            {
                hunterActiveTargetVisibilitySnapshot?.Dispose();
                hunterActiveTargetVisibilitySnapshot = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Improved Hunters active-target visibility snapshot is unavailable; " +
                    $"Hunter behavior remains unchanged: {exception}");
            }
        }

        private bool CanRunHunterTargetSelection()
        {
            return Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
                settings.ImprovedTargetSelection &&
                targetSelectionModeAllowed;
        }

        private bool CanRunHunterPathfinding()
        {
            return Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
                settings.ImprovedPathfinding &&
                pathfindingModeAllowed;
        }

        private bool CanRunHunterReachability()
        {
            return CanRunHunterTargetSelection() || CanRunHunterPathfinding();
        }

        private bool TryPrepareHunterPostShotStateZeroContinuation(
            int hunterUnitId,
            long timestamp,
            out HunterPostShotContinuationCandidate candidate)
        {
            candidate = default;
            return hunterPostShotContinuationDiagnostic?.TryPrepareStateZeroContinuation(
                hunterUnitId,
                timestamp,
                out candidate) == true;
        }

        private void RecordAcceptedHunterPostShotAttack(
            HunterPostShotContinuationCandidate candidate,
            long timestamp)
        {
            hunterPostShotContinuationDiagnostic?.RecordAcceptedAttack(candidate, timestamp);
        }

        private void RecordFailedHunterPostShotAttack(
            HunterPostShotContinuationCandidate candidate,
            long timestamp)
        {
            hunterPostShotContinuationDiagnostic?.RecordFailedDirectAttack(candidate, timestamp);
        }

        private void RecordHunterPostShotStateZeroHandoff(
            HunterPostShotContinuationCandidate candidate,
            int vanillaTargetUnitId)
        {
            hunterPostShotContinuationDiagnostic?.RecordStateZeroHandoff(
                candidate,
                vanillaTargetUnitId);
        }

        private void RecordHunterPostShotMoveHereResult(
            HunterPostShotContinuationCandidate candidate,
            int moveHereResult)
        {
            hunterPostShotContinuationDiagnostic?.RecordMoveHereResult(candidate, moveHereResult);
        }

        private void ResetHunterPostShotAttemptBudget(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId)
        {
            hunterPostShotContinuationDiagnostic?.ResetAttemptBudgetForIndependentMove(
                hunterUnitId,
                preyUnitId,
                preyGlobalId);
        }

        private bool TryValidateHunterPostShotContinuation(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            eChimps preyType,
            long timestamp,
            out string validation)
        {
            if (!IsRuntimeHuntingEnabled(preyType))
            {
                validation = "prey-type-disabled";
                return false;
            }

            if (IsTargetOnCooldown(hunterUnitId, preyGlobalId, timestamp))
            {
                validation = "target-on-MoveHere-cooldown";
                return false;
            }

            if (hunterPclReachability != null &&
                hunterPclReachability.TryIsReachable(
                    hunterUnitId,
                    preyUnitId,
                    preyGlobalId,
                    preyType,
                    timestamp,
                    out bool reachable))
            {
                validation = reachable ? "pcl-reachable" : "pcl-zero";
                return reachable;
            }

            // A technical PCL lookup failure must not discard live prey.
            validation = "pcl-unavailable-fail-open";
            return true;
        }

        private HunterStateOneNearRefreshAction TryPrepareHunterStateOneNearRefresh(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId,
            int nativeWorldDistance,
            out bool shouldLog)
        {
            shouldLog = false;
            if (hunterVanillaPathContinuationDiagnostic == null)
                return HunterStateOneNearRefreshAction.None;

            return hunterVanillaPathContinuationDiagnostic.TryPrepareStateOneNearRefresh(
                hunterUnitId,
                preyUnitId,
                preyGlobalId,
                nativeWorldDistance,
                out shouldLog);
        }

        private void InitializeHunterHutVisibilityPatch(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                hunterHutVisibilityPatch = new HunterHutVisibilityPatch(
                    log,
                    hookRegion,
                    memory,
                    imageBase,
                    referenceHashMatches);
            }
            catch (Exception exception)
            {
                hunterHutVisibilityPatch?.Dispose();
                hunterHutVisibilityPatch = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters Hunter's Hut visibility patch is unavailable; " +
                    $"Vanilla's visibility exception remains unchanged: {exception}");
            }
        }

        private void ApplyHunterHutVisibilityPatch()
        {
            bool requestedEnabled = CanRunHunterPathfinding();
            if (hunterHutVisibilityPatch == null)
                return;

            hunterHutVisibilityPatch.TrySetEnabled(requestedEnabled);
        }

        private void InitializeHunterVanillaPathContinuationDiagnostic(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                hunterVanillaPathContinuationDiagnostic =
                    new HunterVanillaPathContinuationDiagnostic(
                        log,
                        settings,
                        hunterActiveTargetVisibilitySnapshot,
                        hunterPclReachability,
                        hookRegion,
                        memory,
                        imageBase,
                        referenceHashMatches,
                        CanRunHunterPathfinding);
            }
            catch (Exception exception)
            {
                hunterVanillaPathContinuationDiagnostic?.Dispose();
                hunterVanillaPathContinuationDiagnostic = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Improved Hunters Vanilla-path continuation diagnostic is unavailable; " +
                    $"Hunter behavior remains unchanged: {exception}");
            }
        }

        private void InitializeHunterRemainingPathSpeedRecovery(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                hunterRemainingPathSpeedRecovery =
                    new HunterRemainingPathSpeedRecovery(
                        log,
                        settings,
                        hunterNativeVisibilityProbe,
                        hookRegion,
                        memory,
                        imageBase,
                        referenceHashMatches,
                        CanRunHunterPathfinding);
            }
            catch (Exception exception)
            {
                hunterRemainingPathSpeedRecovery?.Dispose();
                hunterRemainingPathSpeedRecovery = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Improved Hunters remaining-path speed recovery is unavailable; " +
                    $"Vanilla's direct-distance stage selection remains unchanged: {exception}");
            }
        }

        private void RegisterRejectedHunterStateZeroMove(
            int hunterUnitId,
            uint preyGlobalId,
            long timestamp)
        {
            if (hunterUnitId <= 0 || preyGlobalId == 0)
                return;

            bestTargetCache.Remove(hunterUnitId);
            SetTargetCooldownUntil(
                new HunterPreyCooldownKey(hunterUnitId, preyGlobalId),
                timestamp + AbortedTargetCooldownInterval);
        }

        private bool IsOwnedChickenHuntingEnabled =>
            Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
            settings.HuntChicken &&
            ownedChickenHuntPatch?.IsAvailable == true &&
            preyReservationHeartbeatPatch?.IsAvailable == true;

        private void ApplyPreyReservationHeartbeat()
        {
            preyReservationHeartbeatPatch?.SetEnabled(
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
                (settings.HuntRabbit || settings.HuntCamel || settings.HuntChicken));
            ownedChickenHuntPatch?.SetEnabled(IsOwnedChickenHuntingEnabled);
        }

        private bool IsTannerMeatModeEnabled =>
            Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) && settings.TannerMeatMode;

        private void ApplyTannerMeatRule()
        {
            tannerMeatPatch?.Configure(IsTannerMeatModeEnabled, settings.TannerMeatPerTrip);
        }

        private void ApplyRabbitMeleeOrderRule()
        {
            rabbitMeleeOrderPatch?.SetEnabled(
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) && settings.RabbitsOnlyOnOrder);
        }

        private static bool IsValidUnitId(int unitId)
        {
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            return unitId >= 1 && unitId <= units.Length;
        }

        private bool IsRuntimeHuntingEnabled(eChimps type)
        {
            if (!settings.IsHuntingEnabled(type))
                return false;

            // Without the countdown these prey types would stay reserved forever.
            if (type == eChimps.CHIMP_TYPE_RABBIT || type == eChimps.CHIMP_TYPE_CAMEL)
                return preyReservationHeartbeatPatch?.IsAvailable == true;
            if (type == eChimps.CHIMP_TYPE_CHICKEN)
                return IsOwnedChickenHuntingEnabled;
            return true;
        }

        private static unsafe void UpdateUnitHealthDisplay(GameUnit* unit)
        {
            if (unit == null)
                return;

            uint maxHealth = unit->r_MaxHealth == 0 ? 1u : unit->r_MaxHealth;
            uint currentHealth = Math.Min(unit->r_CurrentHealth, maxHealth);
            ushort healthPercent = (ushort)Math.Min(100u, (100u * currentHealth) / maxHealth);
            unit->r_CurrentHealth = currentHealth;
            unit->r_CurrentHealthPercentage = healthPercent;
            unit->r_HealthBarBlocks = (uint)(healthPercent / 10);
        }

        private unsafe bool IsOwnerAllowedForAnyHunter(int unitId, GameUnit* prey)
        {
            return true;
        }

        private unsafe bool IsOwnerAllowed(int hunterOwner, GameUnit* prey)
        {
            // Hunters only take chickens of their own player; other prey is neutral.
            return prey->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN ||
                prey->r_ControllableForPlayerId == 0 ||
                prey->r_ControllableForPlayerId == hunterOwner;
        }

        private unsafe bool IsOwnerAllowed(int hunterOwner, int preyUnitId, eChimps preyType)
        {
            return APIShared.UnitAccess.TryGetById(preyUnitId, out GameUnit* prey, out _) &&
                prey != null &&
                IsOwnerAllowed(hunterOwner, prey);
        }

        private static bool IsShortLivedPrey(eChimps type)
        {
            return type == eChimps.CHIMP_TYPE_RABBIT ||
                type == eChimps.CHIMP_TYPE_CAMEL ||
                type == eChimps.CHIMP_TYPE_CHICKEN;
        }

        private static unsafe bool IsTrackedShortLivedCorpse(GameUnit* unit)
        {
            if (unit == null || !IsShortLivedPrey(unit->r_UnitChimp))
                return false;

            // 0x29C is the observed corpse marker, 0x2BC is the native animal
            // state. Both must agree before we manipulate the despawn timer.
            byte* unitBytes = (byte*)unit;
            return *(ushort*)(unitBytes + 0x29C) != 0 &&
                IsPreservableCorpseState(*(ushort*)(unitBytes + 0x2BC));
        }

        private static bool IsPreservableCorpseState(ushort aiState)
        {
            return aiState == HunterCorpsePickupAiState ||
                aiState == HunterFreshCorpseAiState;
        }

        private void ClearTargetSelectionCaches()
        {
            preyCache.Clear();
            bestTargetCache.Clear();
            activeHunterTargets.Clear();
            abortedTargetCooldowns.Clear();
            lastHunterQueryTimestamps.Clear();
            hunterMeatPickupTimestamps.Clear();
            nextPreyCacheRefreshTimestamp = 0;
            nextShortLivedCorpsePreserveCleanupTimestamp = 0;
            lastLoggedDesiredCamelHealth = 0;
            despawnPatchStateLogged = false;
            hunterTargetDiagnosticLogs = 0;
            preyCacheDiagnosticLogs = 0;
            invalidHunterEventLogs = 0;
            hunterTargetQueryEvents = 0;
            hunterTargetAcceptedEvents = 0;
            hunterTargetRejectedEvents = 0;
            hunterTargetFallbackEvents = 0;
            hunterTargetNoBestEvents = 0;
            hunterTargetSearchStarts = 0;
        }

        private struct PreyEligibility
        {
            public bool KnownAnimal;
            public bool RuntimeHuntingEnabled;
            public bool OwnerAllowed;
            public bool FlagsAllowed;
            public bool Eligible;
            public eChimps Type;
            public uint GlobalId;
            public int TileX;
            public int TileY;
            public short AliveState;
            public ushort FlagsAt92;
            public ushort AiState;
            public ushort CorpseFlag;
            public ushort Reservation;
        }

        private struct PreySnapshot
        {
            public readonly int UnitId;
            public readonly uint GlobalId;
            public readonly eChimps Type;
            public readonly int TileX;
            public readonly int TileY;
            public readonly int MeatAmount;

            public PreySnapshot(int unitId, uint globalId, eChimps type, int tileX, int tileY, int meatAmount)
            {
                UnitId = unitId;
                GlobalId = globalId;
                Type = type;
                TileX = tileX;
                TileY = tileY;
                MeatAmount = meatAmount;
            }
        }

        private struct PreyCandidate
        {
            public readonly PreySnapshot Prey;
            public readonly int HeuristicCycleCost;

            public PreyCandidate(PreySnapshot prey, int heuristicCycleCost)
            {
                Prey = prey;
                HeuristicCycleCost = heuristicCycleCost <= 0 ? 1 : heuristicCycleCost;
            }
        }

        private struct BestTarget
        {
            public readonly int UnitId;
            public readonly uint GlobalId;
            public readonly eChimps Type;
            public readonly int MeatAmount;
            public readonly int ApproachHeuristicCost;
            public readonly int GranaryRoundTripHeuristicCost;
            public readonly int CycleCost;

            public BestTarget(
                int unitId,
                uint globalId,
                eChimps type,
                int meatAmount,
                int approachHeuristicCost,
                int granaryRoundTripHeuristicCost,
                int cycleCost)
            {
                UnitId = unitId;
                GlobalId = globalId;
                Type = type;
                MeatAmount = meatAmount;
                ApproachHeuristicCost = approachHeuristicCost;
                GranaryRoundTripHeuristicCost = granaryRoundTripHeuristicCost;
                CycleCost = cycleCost <= 0 ? 1 : cycleCost;
            }
        }

        private struct TargetSelection
        {
            private readonly HashSet<int> allowedUnitIds;

            public readonly BestTarget BestTarget;

            public TargetSelection(BestTarget bestTarget, HashSet<int> allowedUnitIds)
            {
                BestTarget = bestTarget;
                this.allowedUnitIds = allowedUnitIds;
            }

            public bool HasTarget => BestTarget.UnitId != 0;

            public int AllowedCount
            {
                get
                {
                    if (allowedUnitIds != null)
                        return allowedUnitIds.Count;

                    return BestTarget.UnitId == 0 ? 0 : 1;
                }
            }

            public bool IsAllowed(int unitId)
            {
                if (allowedUnitIds != null)
                    return allowedUnitIds.Contains(unitId);

                return unitId == BestTarget.UnitId;
            }
        }

        private struct CachedBestTarget
        {
            public readonly TargetSelection Selection;
            public readonly long ExpiresAt;

            public CachedBestTarget(TargetSelection selection, long expiresAt)
            {
                Selection = selection;
                ExpiresAt = expiresAt;
            }
        }

        private struct HunterTargetSnapshot
        {
            public readonly ushort UnitId;
            public readonly uint GlobalId;

            public HunterTargetSnapshot(ushort unitId, uint globalId)
            {
                UnitId = unitId;
                GlobalId = globalId;
            }
        }

        private struct HunterPreyCooldownKey : IEquatable<HunterPreyCooldownKey>
        {
            private readonly int hunterUnitId;
            private readonly uint preyGlobalId;

            public HunterPreyCooldownKey(int hunterUnitId, uint preyGlobalId)
            {
                this.hunterUnitId = hunterUnitId;
                this.preyGlobalId = preyGlobalId;
            }

            public bool Equals(HunterPreyCooldownKey other)
            {
                return hunterUnitId == other.hunterUnitId &&
                    preyGlobalId == other.preyGlobalId;
            }

            public override bool Equals(object obj)
            {
                return obj is HunterPreyCooldownKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + hunterUnitId;
                    hash = hash * 31 + preyGlobalId.GetHashCode();
                    return hash;
                }
            }
        }

        private void TryInitializeFeature(string featureName, Action initialize)
        {
            try
            {
                initialize();
            }
            catch (Exception ex)
            {
                LogFeatureFailure(featureName, ex);
            }
        }

        private void SubscribeRuntimeEvents()
        {
            if (runtimeEventsSubscribed || !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                return;

            TrySubscribeFeature("hunter target queries", () => UnitR3EventHooks.OnUnitHunterQueryTarget.Observable
                .Where(args => args.Phase == EventHookPhase.Pre).Subscribe(OnHunterQueryTarget));
            TrySubscribeFeature("bonus yield", () => UnitR3EventHooks.OnCalculateBonusYield.Observable
                .Where(args => args.Phase == EventHookPhase.Pre).Subscribe(OnCalculateBonusYield));
            TrySubscribeFeature("hunter meat pickup", () => UnitR3EventHooks.OnHunterPickUpMeat.Observable
                .Where(args => args.Phase == EventHookPhase.Pre).Subscribe(OnHunterPickUpMeat));
            TrySubscribeFeature("hunter meat dropoff", () => UnitR3EventHooks.OnHunterDropOffMeat.Observable
                .Where(args => args.Phase == EventHookPhase.Pre).Subscribe(OnHunterDropOffMeat));
            TrySubscribeFeature("projectile kills", () => UnitR3EventHooks.OnUnitKilledByProjectile.Observable
                .Where(args => args.Phase == EventHookPhase.Pre).Subscribe(OnUnitKilledByProjectile));
            TrySubscribeFeature("projectile spawning", () => ProjectileR3EventHooks.OnProjectileSpawn.Observable
                .Where(args => args.Phase == EventHookPhase.Post).Subscribe(OnProjectileSpawn));
            TrySubscribeFeature("projectile deletion", () => ProjectileR3EventHooks.OnProjectileDelete.Observable
                .Where(args => args.Phase == EventHookPhase.Pre).Subscribe(OnProjectileDelete));
            TrySubscribeFeature("gameplay session start", () =>
                Shared.GameplaySessionLifecycle.SubscribeStarted(log, _ => OnMapStarted(), () =>
                {
                    targetSelectionModeAllowed = false;
                    pathfindingModeAllowed = false;
                    ClearTargetSelectionCaches();
                }));
            runtimeEventsSubscribed = true;
        }

        private void UnsubscribeRuntimeEvents()
        {
            foreach (IDisposable subscription in subscriptions)
            {
                try { subscription.Dispose(); }
                catch (Exception ex) { LogFeatureFailure("event subscription cleanup", ex); }
            }
            subscriptions.Clear();
            runtimeEventsSubscribed = false;
            // The gate closes before the mission-end observer of these subscriptions can run.
            targetSelectionModeAllowed = false;
            pathfindingModeAllowed = false;
            hunterPreyTypes.Clear();
            nextBetterPreyChecks.Clear();
            nextIdleHunterRequeryTimestamps.Clear();
            ClearTargetSelectionCaches();
        }

        private void TrySubscribeFeature(string featureName, Func<IDisposable> subscribe)
        {
            try
            {
                IDisposable subscription = subscribe();
                if (subscription != null)
                    subscriptions.Add(subscription);
            }
            catch (Exception ex)
            {
                LogFeatureFailure(featureName, ex);
            }
        }

        private void LogFeatureFailure(string featureName, Exception ex)
        {
            Shared.DebugLogHelper.LogError(
                log,
                $"Improved Hunters feature '{featureName}' failed and remains inactive; independent features continue: {ex}");
        }

        public void Dispose()
        {
            Shared.GameplayModActivationGate.StateChanged -= OnModeStateChanged;
            settings.SettingChanged -= OnSettingChanged;

            UnsubscribeRuntimeEvents();
            hunterPreyTypes.Clear();
            nextBetterPreyChecks.Clear();
            nextIdleHunterRequeryTimestamps.Clear();
            loggedCollectedCorpseGlobalIds.Clear();
            ClearTargetSelectionCaches();
            nativeScanFailureLogged = false;

            hunterTargetSearchFallbackDiagnostic?.Dispose();
            hunterTargetSearchFallbackDiagnostic = null;
            hunterPostShotContinuationDiagnostic?.Dispose();
            hunterPostShotContinuationDiagnostic = null;
            hunterVanillaPathContinuationDiagnostic?.Dispose();
            hunterVanillaPathContinuationDiagnostic = null;
            hunterActiveTargetVisibilitySnapshot?.Dispose();
            hunterActiveTargetVisibilitySnapshot = null;
            hunterPclReachability?.Dispose();
            hunterPclReachability = null;
            hunterRemainingPathSpeedRecovery?.Dispose();
            hunterRemainingPathSpeedRecovery = null;
            hunterNativeVisibilityProbe?.Dispose();
            hunterNativeVisibilityProbe = null;
            hunterHutVisibilityPatch?.Dispose();
            hunterHutVisibilityPatch = null;
            hunterQueryActorWorkaround?.Dispose();
            hunterQueryActorWorkaround = null;
            granaryChickenLimitPatch?.Dispose();
            granaryChickenLimitPatch = null;
            ownedChickenHuntPatch?.Dispose();
            ownedChickenHuntPatch = null;
            preyReservationHeartbeatPatch?.Dispose();
            preyReservationHeartbeatPatch = null;
            rabbitMeleeOrderPatch?.Dispose();
            rabbitMeleeOrderPatch = null;
            tannerMeatPatch?.Dispose();
            tannerMeatPatch = null;

            // RabbitDespawnTickTime belongs to the Script Extender. Release only our
            // override; never dispose or reset the shared site itself.
            rabbitDespawnOverride?.Dispose();
            rabbitDespawnOverride = null;

            // These two sites were constructed by this runtime and therefore own
            // restoration of their original instructions at true final teardown.
            camelDespawnTickTime?.Dispose();
            chickenDespawnTickTime?.Dispose();

            if (camelHealthPatched && camelHealthInitialized)
                GameUnitManagerAPI.Instance.SetDefaultHealth(eChimps.CHIMP_TYPE_CAMEL, originalCamelHealth);

            rabbitDespawnTicksPatched = false;
            camelDespawnTicksPatched = false;
            chickenDespawnTicksPatched = false;
            camelHealthPatched = false;
            camelHealthInitialized = false;
            desiredCamelHealth = 0;
            lastLoggedDesiredCamelHealth = 0;
            rabbitDespawnTickTime = null;
            camelDespawnTickTime = null;
            chickenDespawnTickTime = null;
            hookRegion = null;
            rabbitDespawnTicksInitialized = false;
            extraDespawnTicksInitialized = false;
            applied = false;
        }
    }
}
