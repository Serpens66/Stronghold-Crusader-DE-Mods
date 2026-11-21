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

        // Native pickupable animal corpse state observed after regular Hunter
        // ranged damage.
        private const ushort HunterCorpsePickupAiState = 0x6E;
        // Retained only so corpses created by older KillUnit-based versions can
        // expire cleanly after loading; current code never creates 0x6F.
        private const ushort HunterFreshCorpseAiState = 0x6F;
        private const long ExpiredShortLivedCorpsePreserve = long.MinValue;

        // The BepInEx plugin component is short-lived in SHCDE. Repeating work runs on APIShared's
        // process-lived map-tick callback, at the same map ticks on every multiplayer peer.
        private const int NativeScanIntervalMapTicks = HunterFiringPositionBudget.WindowTicks;
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
        // Process-lived late foreign-patch check; never part of the toggled subscriptions above.
        private static IDisposable nativePatchRecheckSubscription;
        private readonly Dictionary<int, long> nextBetterPreyChecks = new Dictionary<int, long>();
        private readonly Dictionary<int, long> nextIdleHunterRequeryTimestamps = new Dictionary<int, long>();
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

        private ScanRegion hookRegion;
        private APIShared.Health.HealthModifierHandle camelHealthPolicy;
        private int camelArrowDamage;
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
        private APIShared.Wildlife.ChickenPopulationModifierHandle chickenTargetModifier;
        private readonly GranaryChickenSpawnPolicy chickenSpawnPolicy = new GranaryChickenSpawnPolicy(GamePlayerManagerAPI.MAX_PLAYERS);
        private RabbitMeleeOrderPatch rabbitMeleeOrderPatch;
        private TannerMeatPatch tannerMeatPatch;
        private APIShared.Buildings.BuildMenuAvailabilityRule hunterHutRule;
        private WildlifeRespawnFeature wildlifeRespawn;
        private WildlifeRangeFeature wildlifeRange;
        private HerdFleeSightFeature herdFleeSight;
        // APIShared's Hunter's hut sight rule (APIShared.Sight.SightBlockingBuildings); permanent, logically enabled.
        private APIShared.Sight.SightBlockingBuildingRule hunterHutSightRule;
        private HunterNativeVisibilityProbe hunterNativeVisibilityProbe;
        private HunterActiveTargetVisibilitySnapshot hunterActiveTargetVisibilitySnapshot;
        private HunterFiringPositionSearch hunterFiringPositionSearch;
        private readonly HunterFiringPositionBudget firingPositionBudget = new HunterFiringPositionBudget();
        private readonly Func<bool> reserveUrgentVisibilityProbe;
        private HunterPclReachability hunterPclReachability;
        private HunterRemainingPathSpeedRecovery hunterRemainingPathSpeedRecovery;
        private HunterPostShotContinuation hunterPostShotContinuation;
        private HunterTargetSearchFallback hunterTargetSearchFallback;
        private HunterVanillaPathContinuation hunterVanillaPathContinuation;
        private bool referenceHashMatches;
        private bool targetSelectionModeAllowed;
        private bool pathfindingModeAllowed;
        private bool applied;
        private bool runtimeEventsSubscribed;

        public ImprovedHuntersRuntime(ManualLogSource log, ImprovedHuntersViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            reserveUrgentVisibilityProbe = TryReserveUrgentVisibilityProbe;
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
            HunterHookInfrastructure.Bind(context, imageBase);
            // Load snapshot for the foreign-patch checks of every later hook commit.
            Shared.NativePatchGuard.Bind(context, log);
            if (applied)
                return;

            TryInitializeFeature("native foreign-patch recheck", SubscribeNativePatchRecheck);

            this.referenceHashMatches = referenceHashMatches;
            TryInitializeFeature("APIShared hunting registrations", RegisterHuntingWithApiShared);
            TryInitializeFeature("chickens per player", RegisterChickenTarget);
            InitializeRabbitMeleeOrderPatch(memory, imageBase, referenceHashMatches);
            InitializeTannerMeatPatch(memory, imageBase, referenceHashMatches);
            InitializeHunterNativeVisibilityProbe();
            RegisterHunterHutSightRule();
            Shared.DevDiag.Attach(log);
            InitializeWildlifeFeatures(memory, imageBase, referenceHashMatches);
            InitializeHunterCorpseChoicePatch(memory, imageBase, referenceHashMatches);
            TryInitializeFeature("PCL reachability", () => InitializeHunterPclReachability(referenceHashMatches));
            TryInitializeFeature("active-target visibility snapshot", InitializeHunterActiveTargetVisibilitySnapshot);
            InitializeHunterPostShotContinuation(memory, imageBase, referenceHashMatches);
            InitializeHunterTargetSearchFallback(memory, imageBase, referenceHashMatches);
            InitializeHunterRemainingPathSpeedRecovery(memory, imageBase, referenceHashMatches);
            InitializeHunterVanillaPathContinuation(memory, imageBase, referenceHashMatches);

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
            TryInitializeFeature("camel health", RegisterCamelHealthPolicy);

            applied = true;
            Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters runtime enabled: apiSharedPrey={preyRegistrations.Count}/3, " +
                    $"apiSharedTargetPolicy={targetPolicyRegistered}, apiSharedMeat={meatHandlerRegistered}, " +
                    $"chickensPerPlayerRegistered={chickenTargetModifier != null}, " +
                    $"rabbitMeleeOrderAvailable={rabbitMeleeOrderPatch?.IsAvailable == true}, " +
                    $"tannerMeatAvailable={tannerMeatPatch?.IsAvailable == true}, " +
                    $"hunterHutVisibilityAvailable={hunterHutSightRule != null}, " +
                    $"hunterHutVisibilityApplied={hunterHutSightRule?.IsEnabled == true}, " +
                    $"hunterNativeVisibilityProbeAvailable={hunterNativeVisibilityProbe?.IsAvailable == true}, " +
                    $"hunterActiveTargetVisibilityAvailable={hunterActiveTargetVisibilitySnapshot?.IsAvailable == true}, " +
                    $"hunterPclReachabilityAvailable={hunterPclReachability?.IsAvailable == true}, " +
                    $"hunterRemainingPathSpeedRecoveryAvailable={hunterRemainingPathSpeedRecovery?.IsAvailable == true}, " +
                    $"hunterPostShotContinuationAvailable={hunterPostShotContinuation?.IsAvailable == true}, " +
                    $"hunterTargetSearchFallbackAvailable={hunterTargetSearchFallback?.IsAvailable == true}, " +
                    $"hunterVanillaPathContinuationAvailable={hunterVanillaPathContinuation?.IsAvailable == true}, " +
                    $"referenceHashMatches={referenceHashMatches}.");
        }

        // Mods loaded after us may overwrite a published hook span; check once per mission or
        // editor start (AGENTS/native-hooks.md#fremde-patches-erkennen).
        private void SubscribeNativePatchRecheck()
        {
            if (nativePatchRecheckSubscription != null)
                return;
            Shared.MissionEvents.SetOwner(ImprovedHuntersPlugin.PluginGuid);
            nativePatchRecheckSubscription = Shared.MissionEvents.Started.Subscribe(_ =>
            {
                try { Shared.NativePatchGuard.Recheck(); }
                catch (Exception ex) { Shared.DebugLogHelper.LogError(log, "Improved Hunters native foreign-patch recheck failed: " + ex); }
            });
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
                if (!targetSelectionModeAllowed && !pathfindingModeAllowed)
                    return;

                SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
                if (units._array == null || units.Length == 0)
                    return;

                List<IntPtr> hunters = scanHunters;
                List<IntPtr> eligiblePrey = scanEligiblePrey;
                hunters.Clear();
                eligiblePrey.Clear();

                for (int index = 0; index < units.Length; index++)
                {
                    GameUnit* unit = units.GetValuePointer(index);
                    int unitId = index + 1;

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

                CleanupShortLivedCorpsePreserveCache(units, timestamp);
                hunterActiveTargetVisibilitySnapshot?.ProcessNativeScan(units, hunters, timestamp, reserveUrgentVisibilityProbe);
                RequeryHuntersWithUnreachableActivePrey(units, hunters, timestamp);
                RequeryHuntersWithBetterPrey(units, hunters, timestamp);
                long measured = 0;
                if (CanRunHunterPathfinding() && hunterTargetSearchFallback?.IsAvailable == true)
                {
                    Shared.DevDiag.Start(ref measured);
                    hunterFiringPositionSearch?.Observe(units, hunters, hunterActiveTargetVisibilitySnapshot, timestamp);
                    Shared.DevDiag.Stop(ref measured, "hunters.firingPosition");
                }
                Shared.DevDiag.Start(ref measured);
                herdFleeSight?.Update(units);
                Shared.DevDiag.Stop(ref measured, "hunters.herdSight");
                Shared.DevDiag.Start(ref measured);
                UpdateCorpseChoices(units, hunters, timestamp);
                Shared.DevDiag.Stop(ref measured, "hunters.corpseChoice");
                TrackHunterTargets(units, hunters, timestamp);
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
                    hunterPostShotContinuation?.HasShotInProgress(hunterUnitId) == true)
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
                LogRuntimeSample(
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

        // The prey of each yield and the removal of collected rabbit, camel and chicken corpses are APIShared's
        // (recorded natively when the Hunter reaches the corpse); this only keeps the target state per Hunter.
        private unsafe void TrackHunterTargets(
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

        private void OnMapStarted(bool fromSave)
        {
            chickenSpawnPolicy.Reset(fromSave);
            ResetCorpseChoices();
            nextBetterPreyChecks.Clear();
            nextIdleHunterRequeryTimestamps.Clear();
            shortLivedCorpsePreserveUntil.Clear();
            ClearTargetSelectionCaches();
            hunterNativeVisibilityProbe?.ResetForMap();
            hunterActiveTargetVisibilitySnapshot?.ResetForMap();
            hunterFiringPositionSearch?.ResetForMap();
            firingPositionBudget.Reset();
            hunterPclReachability?.ResetForMap();
            hunterPostShotContinuation?.ResetForMap();
            hunterTargetSearchFallback?.ResetForMap();
            hunterRemainingPathSpeedRecovery?.ResetForMap();
            hunterVanillaPathContinuation?.ResetForMap();
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
            ApplyHunterHutSightRule();
            Shared.DebugLogHelper.LogInfo(
                log,
                "Improved Hunters feature mode gates: " +
                $"targetSelectionAllowed={targetSelectionModeAllowed}, " +
                $"pathfindingAllowed={pathfindingModeAllowed}, {gameMode.ToDiagnosticString()}.");
            nativeScanFailureLogged = false;
        }

        private void OnHunterPickUpMeat(UnitHunterPickUpMeatEventArgs args)
        {
            if (!IsValidUnitId(args.UnitId))
            {
                LogInvalidHunterEvent("pickup-meat", args.UnitId);
                return;
            }

            hunterMeatPickupTimestamps[args.UnitId] = HunterSimulationTime.Now;
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
            LogMeatTripSample(args.UnitId, pickupTimestamp);
        }

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        private void LogMeatTripSample(int hunterUnitId, long pickupTimestamp)
        {
            long elapsedTicks = HunterSimulationTime.Now - pickupTimestamp;
            double elapsedSeconds = elapsedTicks / (double)HunterSimulationTime.MapTicksPerSecond;
            if (hunterTargetDiagnosticLogs < MaxHunterTargetDiagnosticLogs)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters meat trip timing: hunter={hunterUnitId}, pickupToDropoffSeconds={elapsedSeconds:F2}, " +
                    $"assumedHutWorkTicks={HunterHutWorkCost}.");
            }
        }

        // Development samples only: /devdiag builds compile these calls including their message text.
        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        private void LogRuntimeSample(string message) => Shared.DebugLogHelper.LogInfo(log, message);

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        private void LogShortLivedCorpsePreserve(string message)
        {
            if (shortLivedCorpsePreserveLogs >= 80)
                return;

            shortLivedCorpsePreserveLogs++;
            Shared.DebugLogHelper.LogInfo(log, $"{message} ({shortLivedCorpsePreserveLogs}/80).");

            if (shortLivedCorpsePreserveLogs == 80)
                Shared.DebugLogHelper.LogInfo(log, "Improved Hunters corpse visible preserve diagnostic limit reached.");
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

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
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
                ApplyPreyRegistrations();
            }

            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                propertyName == nameof(ImprovedHuntersViewModel.HunterHutAlwaysAvailable) ||
                propertyName == nameof(ImprovedHuntersViewModel.DeerFleeOnlyInSight) ||
                propertyName == nameof(ImprovedHuntersViewModel.HerdsIgnoreFleeingHerds) ||
                propertyName == nameof(ImprovedHuntersViewModel.StartHerdMode))
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

            ClearTargetSelectionCaches();
            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                huntingTargetSettingChanged ||
                propertyName == nameof(ImprovedHuntersViewModel.ImprovedTargetSelection) ||
                propertyName == nameof(ImprovedHuntersViewModel.ImprovedPathfinding))
            {
                hunterActiveTargetVisibilitySnapshot?.ResetForMap();
                hunterFiringPositionSearch?.ResetForMap();
                firingPositionBudget.Reset();
                hunterPclReachability?.ResetForMap();
                hunterPostShotContinuation?.ResetForMap();
                hunterTargetSearchFallback?.ResetForMap();
                hunterRemainingPathSpeedRecovery?.ResetForMap();
                hunterVanillaPathContinuation?.ResetForMap();
            }

            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                propertyName == nameof(ImprovedHuntersViewModel.ImprovedPathfinding))
            {
                ApplyHunterHutSightRule();
            }

            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntChicken))
            {
                UpdateChickenTargetModifier();
            }

            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                propertyName == nameof(ImprovedHuntersViewModel.HuntCamel))
            {
                UpdateCamelHealthPolicy();
            }
        }

        private void OnModeStateChanged(bool allowed)
        {
            if (!applied)
                return;

            OnSettingChanged(nameof(ImprovedHuntersViewModel.EnableMod));
        }

        // APIShared owns Vanilla's chicken target compare; this mod only supplies its setting. Registered once at
        // startup, before the first mission (APIShared installs its bridge on the first registration).
        private void RegisterChickenTarget()
        {
            if (chickenTargetModifier != null)
                return;

            if (!APIShared.Wildlife.ChickenPopulationModifiers.TryRegister(
                ImprovedHuntersPlugin.PluginGuid,
                "chickens-per-player",
                SelectChickenTarget,
                out APIShared.Wildlife.ChickenPopulationModifierHandle handle,
                out string reason))
            {
                throw new InvalidOperationException(
                    $"The chicken population setting is unavailable; Vanilla's food-based population remains active: {reason}");
            }

            chickenTargetModifier = handle;
            UpdateChickenTargetModifier();
        }

        // Settings only change between matches; the provider re-checks the mission gate and chicken admission itself.
        private void UpdateChickenTargetModifier() =>
            chickenTargetModifier?.SetEnabled(settings.EnableMod && settings.HuntChicken);

        // Simulation thread: deterministic from host settings, the mission mode and the frozen huntable-prey catalog.
        private APIShared.Modifiers.ValueModifierContribution SelectChickenTarget(APIShared.Wildlife.ChickenPopulationContext context)
        {
            if (!IsOwnedChickenHuntingEnabled)
                return APIShared.Modifiers.ValueModifierContribution.None;

            int target = GranaryChickenSpawnPolicy.ClampTarget(settings.ChickensPerPlayer);
            int interval = settings.ChickenRespawnSeconds * HunterSimulationTime.MapTicksPerSecond;
            if (interval != 0)
            {
                target = SimulationTickEvents.TryGetCurrentTick(out int mapTick)
                    ? chickenSpawnPolicy.TargetForDecision(context.PlayerId, target, context.CurrentChickens,
                        unchecked((uint)mapTick), interval)
                    : Math.Min(target, Math.Max(0, context.CurrentChickens));
            }
            return APIShared.Modifiers.ValueModifierContribution.FixedValue(target);
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
            // APIShared owns Vanilla's build-menu availability query; its wrapper installs on this first rule,
            // which must come during startup.
            if (APIShared.Buildings.BuildMenuAvailability.TryRegister(
                ImprovedHuntersPlugin.PluginGuid, "hunters-hut", out APIShared.Buildings.BuildMenuAvailabilityRule hutRule, out string hutReason))
                hunterHutRule = hutRule;
            else
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters leaves the Hunter's hut availability to Vanilla: {hutReason}");

            // Herd records, spawning, routes and flight belong to APIShared's Herds service; its route events and
            // flight gate install on these registrations, which must come during startup.
            try
            {
                if (!APIShared.Wildlife.Herds.IsAvailable)
                    throw new InvalidOperationException(APIShared.Wildlife.Herds.UnavailableReason);

                var addresses = new WildlifeNativeAddresses(log, memory, imageBase, referenceHashMatches);
                var origins = new WildlifeOriginTable();
                herdFleeSight = new HerdFleeSightFeature(log, hunterNativeVisibilityProbe);
                wildlifeRespawn = new WildlifeRespawnFeature(log, settings, addresses, origins, herdFleeSight);
                wildlifeRange = new WildlifeRangeFeature(log, settings, origins);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters wildlife features are unavailable: {exception}");
            }

            ApplyWildlifeRules();
        }

        private void ApplyWildlifeRules()
        {
            bool modEnabled = Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod);
            // A start herd needs its Hunter's hut, and the map's own count at its start does not know that herd.
            bool hutAlwaysAvailable = modEnabled && (settings.HunterHutAlwaysAvailable || settings.StartHerdMode != HerdSettingsPolicy.StartHerdsOff);
            if (hunterHutRule != null && !hunterHutRule.TrySetDecision((int)eMappers.MAPPER_HUNTER,
                    hutAlwaysAvailable ? APIShared.Buildings.BuildMenuAvailabilityDecision.Allow : APIShared.Buildings.BuildMenuAvailabilityDecision.Default,
                    out string hutReason))
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters could not update the Hunter's hut availability: {hutReason}");
            herdFleeSight?.Configure(modEnabled && settings.DeerFleeOnlyInSight, modEnabled && settings.HerdsIgnoreFleeingHerds);
        }

        private void InitializeTannerMeatPatch(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                Func<bool> isEnabled = () => tannerMeatPatch?.IsAvailable == true && IsTannerMeatModeEnabled;
                // Unconditionally on every peer, also when the patch fails: APIShared registers its request
                // Chore with the first table, and packet IDs must match on all peers.
                TannerMeatState state = TannerMeatState.Register(isEnabled);
                tannerMeatPatch = new TannerMeatPatch(log, hookRegion, memory, imageBase, referenceHashMatches, state.Layout);
                ApplyTannerMeatRule();
                TannerModePanel.Install(log, isEnabled, state);
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

        private void InitializeHunterNativeVisibilityProbe()
        {
            try
            {
                hunterNativeVisibilityProbe = new HunterNativeVisibilityProbe(log, settings);
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

        private void InitializeHunterTargetSearchFallback(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                if (hunterNativeVisibilityProbe?.IsAvailable == true)
                    hunterFiringPositionSearch = new HunterFiringPositionSearch(
                        log, settings, hunterNativeVisibilityProbe, firingPositionBudget, SetHunterGiveUpCooldown);
                hunterTargetSearchFallback = new HunterTargetSearchFallback(
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
                    RecordHunterPclMoveHereResult,
                    TryTakeHunterFiringPosition,
                    RecordHunterFiringPositionMove,
                    (hunterUnitId, preyGlobalId) => hunterFiringPositionSearch?.StageTooCloseRetry(hunterUnitId, preyGlobalId));
            }
            catch (Exception exception)
            {
                hunterTargetSearchFallback?.Dispose();
                hunterTargetSearchFallback = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Improved Hunters target-search fallback diagnostic is unavailable; " +
                    $"Hunter behavior remains unchanged: {exception}");
            }
        }

        private void InitializeHunterPostShotContinuation(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                hunterPostShotContinuation = new HunterPostShotContinuation(
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
                hunterPostShotContinuation?.Dispose();
                hunterPostShotContinuation = null;
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
            return hunterPostShotContinuation?.TryPrepareStateZeroContinuation(
                hunterUnitId,
                timestamp,
                out candidate) == true;
        }

        private void RecordAcceptedHunterPostShotAttack(
            HunterPostShotContinuationCandidate candidate,
            long timestamp)
        {
            hunterPostShotContinuation?.RecordAcceptedAttack(candidate, timestamp);
        }

        private void RecordFailedHunterPostShotAttack(
            HunterPostShotContinuationCandidate candidate,
            long timestamp)
        {
            hunterPostShotContinuation?.RecordFailedDirectAttack(candidate, timestamp);
        }

        private void RecordHunterPostShotStateZeroHandoff(
            HunterPostShotContinuationCandidate candidate,
            int vanillaTargetUnitId)
        {
            hunterFiringPositionSearch?.StageTooCloseRetry(candidate.HunterUnitId, candidate.PreyGlobalId);
            hunterPostShotContinuation?.RecordStateZeroHandoff(
                candidate,
                vanillaTargetUnitId);
        }

        private void RecordHunterPostShotMoveHereResult(
            HunterPostShotContinuationCandidate candidate,
            int moveHereResult)
        {
            hunterPostShotContinuation?.RecordMoveHereResult(candidate, moveHereResult);
        }

        private void ResetHunterPostShotAttemptBudget(
            int hunterUnitId,
            int preyUnitId,
            uint preyGlobalId)
        {
            hunterPostShotContinuation?.ResetAttemptBudgetForIndependentMove(
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
            if (hunterVanillaPathContinuation == null)
                return HunterStateOneNearRefreshAction.None;

            return hunterVanillaPathContinuation.TryPrepareStateOneNearRefresh(
                hunterUnitId,
                preyUnitId,
                preyGlobalId,
                nativeWorldDistance,
                out shouldLog);
        }

        // The Hunter's hut blocks sight like the Woodcutter's hut while Improved pathfinding runs. APIShared owns the
        // native dispatch hook; its first registration must happen in this LibraryLoaded handler, before any mission.
        private void RegisterHunterHutSightRule()
        {
            if (!APIShared.Sight.SightBlockingBuildings.TryRegister(
                    ImprovedHuntersPlugin.PluginGuid,
                    "hunters-hut-sight",
                    eStructs.STRUCT_HUNTERS_HUT,
                    out hunterHutSightRule,
                    out string reason))
            {
                hunterHutSightRule = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters Hunter's Hut visibility rule is unavailable; " +
                    $"Vanilla's visibility exception remains unchanged: {reason}");
            }
        }

        // Called at mission start and on setting changes outside a mission; every peer derives it from synchronized settings.
        private void ApplyHunterHutSightRule()
        {
            hunterHutSightRule?.SetEnabled(CanRunHunterPathfinding());
        }

        private void InitializeHunterVanillaPathContinuation(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                hunterVanillaPathContinuation =
                    new HunterVanillaPathContinuation(
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
                hunterVanillaPathContinuation?.Dispose();
                hunterVanillaPathContinuation = null;
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

        private bool TryReserveUrgentVisibilityProbe() =>
            firingPositionBudget.TryTakeSight(HunterSimulationTime.Now, inline: false, urgent: true);

        private bool TryTakeHunterFiringPosition(
            int hunterUnitId, uint preyGlobalId, eChimps preyType, long timestamp, out int tileX, out int tileY)
        {
            tileX = 0;
            tileY = 0;
            return hunterFiringPositionSearch != null &&
                hunterFiringPositionSearch.TryTakeDestination(hunterUnitId, preyGlobalId, preyType, timestamp, out tileX, out tileY);
        }

        private bool RecordHunterFiringPositionMove(int hunterUnitId, uint preyGlobalId, int moveResult, long timestamp) =>
            hunterFiringPositionSearch?.RecordMoveResult(hunterUnitId, preyGlobalId, moveResult, timestamp) == true;

        // A Hunter gave up on this prey even from a firing position, or none exists: other prey first for a while.
        private void SetHunterGiveUpCooldown(int hunterUnitId, uint preyGlobalId, long expiresAt)
        {
            bestTargetCache.Remove(hunterUnitId);
            SetTargetCooldownUntil(new HunterPreyCooldownKey(hunterUnitId, preyGlobalId), expiresAt);
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

        // APIShared admits own-player chickens and releases their reservation.
        private bool IsOwnedChickenHuntingEnabled =>
            Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
            settings.HuntChicken &&
            IsPreyAdmitted(eChimps.CHIMP_TYPE_CHICKEN);

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

            // Only APIShared's admission releases their reservation; otherwise they would stay reserved forever.
            if (type == eChimps.CHIMP_TYPE_RABBIT || type == eChimps.CHIMP_TYPE_CAMEL)
                return IsPreyAdmitted(type);
            if (type == eChimps.CHIMP_TYPE_CHICKEN)
                return IsOwnedChickenHuntingEnabled;
            return true;
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
                Shared.GameplaySessionLifecycle.SubscribeStarted(log, context => OnMapStarted(context.IsLoadedSave), () =>
                {
                    targetSelectionModeAllowed = false;
                    pathfindingModeAllowed = false;
                    hunterFiringPositionSearch?.ResetForMap();
                    firingPositionBudget.Reset();
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
            nextBetterPreyChecks.Clear();
            nextIdleHunterRequeryTimestamps.Clear();
            ClearTargetSelectionCaches();
            nativeScanFailureLogged = false;

            hunterTargetSearchFallback?.Dispose();
            hunterTargetSearchFallback = null;
            hunterPostShotContinuation?.Dispose();
            hunterPostShotContinuation = null;
            hunterVanillaPathContinuation?.Dispose();
            hunterVanillaPathContinuation = null;
            hunterActiveTargetVisibilitySnapshot?.Dispose();
            hunterActiveTargetVisibilitySnapshot = null;
            hunterPclReachability?.Dispose();
            hunterPclReachability = null;
            hunterRemainingPathSpeedRecovery?.Dispose();
            hunterRemainingPathSpeedRecovery = null;
            hunterNativeVisibilityProbe?.Dispose();
            hunterNativeVisibilityProbe = null;
            hunterHutSightRule?.SetEnabled(false);
            chickenTargetModifier?.SetEnabled(false);
            rabbitMeleeOrderPatch?.Dispose();
            rabbitMeleeOrderPatch = null;
            tannerMeatPatch?.Dispose();
            tannerMeatPatch = null;

            camelHealthPolicy?.SetEnabled(false);
            hookRegion = null;
            applied = false;
        }
    }
}
