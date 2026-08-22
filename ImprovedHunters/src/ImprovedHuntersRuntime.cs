using BepInEx.Logging;
using R3;
using SHCDESE.API;
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
using Zhuqiaomon.Assembly.Stateful;
using Zhuqiaomon.Memory;

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
        private const int MaxChickenOwnershipDiagnosticLogs = 160;
        private const int MaximumPlayerId = 8;
        private const int MaxReservationDiagnosticLogs = 80;
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
        private const int CamelDespawnTickTimeRva = 0x158468;
        private const int ChickenDespawnTickTimeRva = 0x163415;

        // The BepInEx plugin component is short-lived in SHCDE, so all repeating
        // work is driven from persistent Script Extender events and Stopwatch.
        private static readonly long NativeScanInterval = Stopwatch.Frequency / 10;
        private static readonly long IdleHunterRequeryInterval = Stopwatch.Frequency;
        private static readonly long PreyCacheRefreshInterval = Stopwatch.Frequency * 5;
        private static readonly long StaleReservationCleanupInterval = Stopwatch.Frequency * 10;
        private static readonly long BestTargetCacheInterval = Stopwatch.Frequency / 2;
        private static readonly long AbortedTargetCooldownInterval = Stopwatch.Frequency * 30;
        private static readonly long HunterTargetSummaryInterval = Stopwatch.Frequency * 5;
        private static readonly long HunterSearchDetectionGap = Stopwatch.Frequency / 4;
        private static readonly long PendingGranaryChickenSpawnTimeout = Stopwatch.Frequency * 2;
        private static readonly long GranaryChickenCleanupInterval = Stopwatch.Frequency / 10;
        private const int ShortLivedCorpseVisiblePreserveMapTicksAtSpeed40 = 1800;
        private static readonly long ShortLivedCorpsePreserveCleanupInterval = Stopwatch.Frequency * 10;

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, eChimps> hunterPreyTypes = new Dictionary<int, eChimps>();
        private readonly Dictionary<int, long> nextIdleHunterRequeryTimestamps = new Dictionary<int, long>();
        private readonly HashSet<uint> loggedCollectedCorpseGlobalIds = new HashSet<uint>();
        private readonly List<PreySnapshot> preyCache = new List<PreySnapshot>();
        private readonly Dictionary<int, CachedBestTarget> bestTargetCache = new Dictionary<int, CachedBestTarget>();
        private readonly Dictionary<int, HunterTargetSnapshot> activeHunterTargets = new Dictionary<int, HunterTargetSnapshot>();
        private readonly Dictionary<HunterPreyCooldownKey, long> abortedTargetCooldowns = new Dictionary<HunterPreyCooldownKey, long>();
        private readonly Dictionary<int, long> lastHunterQueryTimestamps = new Dictionary<int, long>();
        private readonly Dictionary<int, long> hunterMeatPickupTimestamps = new Dictionary<int, long>();
        private readonly Dictionary<int, TrackedGranaryChicken> trackedGranaryChickens = new Dictionary<int, TrackedGranaryChicken>();
        private readonly int[] trackedGranaryChickenCounts = new int[MaximumPlayerId + 1];
        private readonly Stack<PendingGranaryChickenSpawn> pendingGranaryChickenSpawns = new Stack<PendingGranaryChickenSpawn>();
        private readonly List<int> staleGranaryChickenUnitIds = new List<int>();

        // Keeps short-lived corpses visible long enough for hunters to reach them.
        // The value is either the preserve-until map tick or ExpiredShortLivedCorpsePreserve.
        private readonly Dictionary<uint, long> shortLivedCorpsePreserveUntil = new Dictionary<uint, long>();

        private ManagedAssemblyImmediate<short> rabbitDespawnTickTime;
        private ManagedAssemblyImmediate<short> camelDespawnTickTime;
        private ManagedAssemblyImmediate<short> chickenDespawnTickTime;
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
        private long nextNativeScanTimestamp;
        private long nextPreyCacheRefreshTimestamp;
        private long nextStaleReservationCleanupTimestamp;
        private long nextHunterTargetSummaryTimestamp;
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
        private int chickenOwnershipDiagnosticLogs;
        private int reservationDiagnosticLogs;
        private int invalidHunterEventLogs;
        private int hunterQueryActorWorkaroundLogs;
        private AutomaticChickenTargetPatch automaticChickenTargetPatch;
        private ManualChickenAttackPatch manualChickenAttackPatch;
        private GranaryChickenLimitPatch granaryChickenLimitPatch;
        private HunterQueryActorWorkaround hunterQueryActorWorkaround;
        private HunterLineOfSightRecovery hunterLineOfSightRecovery;
        private HunterHutVisibilityPatch hunterHutVisibilityPatch;
        private HunterNativeVisibilityProbe hunterNativeVisibilityProbe;
        private HunterActiveTargetVisibilitySnapshot hunterActiveTargetVisibilitySnapshot;
        private HunterPclReachability hunterPclReachability;
        private HunterPclReachabilityDiagnostic hunterPclReachabilityDiagnostic;
        private HunterRemainingPathSpeedRecovery hunterRemainingPathSpeedRecovery;
        private HunterPostShotContinuationDiagnostic hunterPostShotContinuationDiagnostic;
        private HunterTargetSearchFallbackDiagnostic hunterTargetSearchFallbackDiagnostic;
        private HunterVanillaPathContinuationDiagnostic hunterVanillaPathContinuationDiagnostic;
        private HunterVisibilityDiagnostic hunterVisibilityDiagnostic;
        private bool referenceHashMatches;
        private bool targetSearchFallbackSingleplayerAllowed;
        private bool loadedChickenReconstructionPending;
        private long nextGranaryChickenCleanupTimestamp;
        private bool applied;
        private bool runtimeEventsSubscribed;

        public ImprovedHuntersRuntime(ManualLogSource log, ImprovedHuntersViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Apply(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            if (applied)
                return;

            this.referenceHashMatches = referenceHashMatches;
            InitializeAutomaticChickenTargetPatch(memory, imageBase, referenceHashMatches);
            InitializeManualChickenAttackPatch(memory, imageBase, referenceHashMatches);
            InitializeGranaryChickenLimitPatch(memory, imageBase, referenceHashMatches);
            InitializeHunterQueryActorWorkaround(memory, imageBase, referenceHashMatches);
            InitializeHunterNativeVisibilityProbe(memory, imageBase, referenceHashMatches);
            InitializeHunterHutVisibilityPatch(memory, imageBase, referenceHashMatches);
            TryInitializeFeature("PCL reachability", () => InitializeHunterPclReachability(referenceHashMatches));
            TryInitializeFeature("PCL reachability diagnostics", () => InitializeHunterPclReachabilityDiagnostic(referenceHashMatches));
            TryInitializeFeature("active-target visibility snapshot", InitializeHunterActiveTargetVisibilitySnapshot);
            InitializeHunterPostShotContinuationDiagnostic(memory, imageBase, referenceHashMatches);
            InitializeHunterTargetSearchFallbackDiagnostic(memory, imageBase, referenceHashMatches);
            InitializeHunterRemainingPathSpeedRecovery(memory, imageBase, referenceHashMatches);
            InitializeHunterVanillaPathContinuationDiagnostic(memory, imageBase, referenceHashMatches);
            TryInitializeFeature("line-of-sight recovery", InitializeHunterLineOfSightRecovery);
            TryInitializeFeature("visibility diagnostics", InitializeHunterVisibilityDiagnostic);

            if (settings.EnableMod)
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

            applied = true;
            Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters runtime enabled: automaticChickenTargetAvailable=" +
                    $"{automaticChickenTargetPatch?.IsAvailable == true}, " +
                    $"automaticChickenTargetApplied={automaticChickenTargetPatch?.IsApplied == true}, " +
                    $"manualChickenAttackAvailable={manualChickenAttackPatch?.IsAvailable == true}, " +
                    $"granaryChickenLimitAvailable={granaryChickenLimitPatch?.IsAvailable == true}, " +
                    $"hunterQueryActorWorkaroundAvailable={hunterQueryActorWorkaround?.IsAvailable == true}, " +
                    $"hunterLineOfSightRecoveryAvailable={hunterLineOfSightRecovery?.IsAvailable == true}, " +
                    $"hunterHutVisibilityAvailable={hunterHutVisibilityPatch?.IsAvailable == true}, " +
                    $"hunterHutVisibilityApplied={hunterHutVisibilityPatch?.IsApplied == true}, " +
                    $"hunterNativeVisibilityProbeAvailable={hunterNativeVisibilityProbe?.IsAvailable == true}, " +
                    $"hunterActiveTargetVisibilityAvailable={hunterActiveTargetVisibilitySnapshot?.IsAvailable == true}, " +
                    $"hunterPclReachabilityAvailable={hunterPclReachability?.IsAvailable == true}, " +
                    $"hunterPclReachabilityDiagnosticAvailable={hunterPclReachabilityDiagnostic?.IsAvailable == true}, " +
                    $"hunterRemainingPathSpeedRecoveryAvailable={hunterRemainingPathSpeedRecovery?.IsAvailable == true}, " +
                    $"hunterPostShotContinuationAvailable={hunterPostShotContinuationDiagnostic?.IsAvailable == true}, " +
                    $"hunterTargetSearchFallbackAvailable={hunterTargetSearchFallbackDiagnostic?.IsAvailable == true}, " +
                    $"hunterVanillaPathContinuationAvailable={hunterVanillaPathContinuationDiagnostic?.IsAvailable == true}, " +
                    $"hunterVisibilityDiagnosticAvailable={hunterVisibilityDiagnostic?.IsAvailable == true}, " +
                    $"referenceHashMatches={referenceHashMatches}.");
        }

        public unsafe void RunNativeScan(bool force = false)
        {
            long timestamp = Stopwatch.GetTimestamp();
            if (!applied || !settings.EnableMod || (!force && timestamp < nextNativeScanTimestamp))
                return;

            nextNativeScanTimestamp = timestamp + NativeScanInterval;

            try
            {
                // Re-apply cheap native/default patches here because settings and
                // some game globals can be recreated around map transitions.
                ApplyDespawnPatches();
                ApplyCamelHealthPatch();

                SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
                if (units._array == null || units.Length == 0)
                    return;

                RemoveExpiredPendingGranaryChickenSpawns(timestamp);
                CleanupTrackedGranaryChickens();
                if (loadedChickenReconstructionPending)
                    TryReconstructLoadedGranaryChickens(units);

                // Keep stale reservation cleanup independent from future target
                // queries. A blocked shot can leave an otherwise idle hunter with
                // no reason to refresh the prey cache again.
                ReleaseStalePreyReservationsIfNeeded(units, timestamp);

                List<IntPtr> hunters = new List<IntPtr>();
                List<IntPtr> eligiblePrey = new List<IntPtr>();
                int adjustedLiveCamels = 0;
                long currentMapTick = GameTimeManagerAPI.Instance.GetElapsedMapTicks();

                for (int index = 0; index < units.Length; index++)
                {
                    GameUnit* unit = units.GetValuePointer(index);
                    int unitId = index + 1;

                    if (TryClampLiveCamelHealth(unitId, unit))
                        adjustedLiveCamels++;

                    if (settings.EnableMod &&
                        IsRuntimeHuntingEnabled(unit->r_UnitChimp) &&
                        IsOwnerAllowedForAnyHunter(unitId, unit))
                    {
                        PreserveShortLivedCorpse(unit, currentMapTick);
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
                hunterVisibilityDiagnostic?.ProcessNativeScan(units, timestamp);
                hunterNativeVisibilityProbe?.ProcessNativeScan(units, timestamp);
                hunterActiveTargetVisibilitySnapshot?.ProcessNativeScan(units, timestamp);
                RequeryHuntersWithUnreachableActivePrey(units, hunters, timestamp);
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
                    hunter->r_AliveState != AliveState.IsAlive ||
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
                    prey->r_AliveState != AliveState.IsAlive ||
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
                if (hunter->r_AliveState != AliveState.IsAlive ||
                    hunter->r_CurrentHealth == 0 ||
                    *(ushort*)(hunterBytes + 0x2BC) != 1 ||
                    *(ushort*)(hunterBytes + 0x39A) != targetUnitId ||
                    *(uint*)(hunterBytes + 0x39C) != targetGlobalId ||
                    prey->r_AliveState != AliveState.IsAlive ||
                    prey->r_CurrentHealth == 0 ||
                    prey->r_GlobalId != targetGlobalId)
                {
                    continue;
                }

                ushort pathState = *(ushort*)(hunterBytes + 0xF2);
                ushort pathFieldF4 = *(ushort*)(hunterBytes + 0xF4);
                ushort pathProgress = *(ushort*)(hunterBytes + 0xF6);
                uint pathLength = *(uint*)(hunterBytes + 0xF8);
                ushort reservationBefore = *(ushort*)((byte*)prey + 0x448);
                uint orderTargetGlobalId = *(uint*)(hunterBytes + 0x3FE);

                *(uint*)(hunterBytes + 0x39C) = 0;
                uint targetGlobalIdAfter = *(uint*)(hunterBytes + 0x39C);
                if (targetGlobalIdAfter != 0)
                {
                    hunterPclReachabilityDiagnostic?.RecordActiveTargetInvalidation(
                        $"hunter={hunterUnitId}/{hunter->r_GlobalId}, " +
                        $"target={targetUnitId}/{targetGlobalId}/{prey->r_UnitChimp}, " +
                        $"outcome=target-global-readback-failed, readback={targetGlobalIdAfter}, " +
                        $"path={pathState}/{pathFieldF4}/{pathProgress}/{pathLength}",
                        warning: true);
                    continue;
                }

                HunterTargetSnapshot invalidatedTarget =
                    new HunterTargetSnapshot(targetUnitId, targetGlobalId);
                activeHunterTargets.Remove(hunterUnitId);
                bestTargetCache.Remove(hunterUnitId);
                abortedTargetCooldowns.Remove(
                    new HunterPreyCooldownKey(hunterUnitId, targetGlobalId));
                TryReleaseAbortedPreyReservation(
                    units,
                    hunterUnitId,
                    invalidatedTarget,
                    "active-target-pcl-disconnected",
                    cooldownApplied: false);

                ushort reservationAfter = *(ushort*)((byte*)prey + 0x448);
                hunterPclReachabilityDiagnostic?.RecordActiveTargetInvalidation(
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, " +
                    $"target={targetUnitId}/{targetGlobalId}/{prey->r_UnitChimp}, " +
                    $"outcome=vanilla-requery-armed, targetGlobal={targetGlobalId}->0, " +
                    $"orderTargetGlobal={orderTargetGlobalId}, reservation={reservationBefore}->{reservationAfter}, " +
                    $"path={pathState}/{pathFieldF4}/{pathProgress}/{pathLength}, " +
                    "ownMovement=False, ownAiState=False, " +
                    "followup=Vanilla-state1-identity-failure-search",
                    warning: false);
            }
        }

        private unsafe void RequeryIdleHuntersNearPrey(
            SimpleNativeArray<GameUnit> units,
            List<IntPtr> hunters,
            List<IntPtr> eligiblePrey,
            long timestamp)
        {
            if (!settings.EnableMod || eligiblePrey.Count == 0)
                return;

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
                int hunterOwner = GameUnitManagerAPI.Instance.GetOwner(hunterId);
                bool preyInSearchRadius = false;

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

                hunterPreyTypes[hunterId] = target->r_UnitChimp;
                if (target->r_UnitChimp == eChimps.CHIMP_TYPE_CHICKEN)
                    hunterVisibilityDiagnostic?.RecordAssignedChickenTarget(hunterId, targetUnitId, targetGlobalId);

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
            if (reservation != 0 && reservation != 2)
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
            eligibility.RuntimeHuntingEnabled = settings.EnableMod && IsRuntimeHuntingEnabled(eligibility.Type);
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
            bool isAllowedDeadTarget = settings.AllowDeadTargets &&
                eligibility.CorpseFlag != 0 &&
                eligibility.AiState == HunterCorpsePickupAiState;
            eligibility.Eligible =
                eligibility.RuntimeHuntingEnabled &&
                eligibility.OwnerAllowed &&
                eligibility.AliveState == (short)AliveState.IsAlive &&
                eligibility.FlagsAllowed &&
                eligibility.Reservation == 0 &&
                (isLivePrey || isAllowedDeadTarget);

            return eligibility.KnownAnimal;
        }

        private void OnMapStarted()
        {
            hunterPreyTypes.Clear();
            nextIdleHunterRequeryTimestamps.Clear();
            loggedCollectedCorpseGlobalIds.Clear();
            shortLivedCorpsePreserveUntil.Clear();
            ClearTrackedGranaryChickens();
            pendingGranaryChickenSpawns.Clear();
            loadedChickenReconstructionPending = true;
            ClearTargetSelectionCaches();
            hunterLineOfSightRecovery?.ResetForMap();
            hunterNativeVisibilityProbe?.ResetForMap();
            hunterActiveTargetVisibilitySnapshot?.ResetForMap();
            hunterPclReachability?.ResetForMap();
            hunterPclReachabilityDiagnostic?.ResetForMap();
            hunterPostShotContinuationDiagnostic?.ResetForMap();
            hunterTargetSearchFallbackDiagnostic?.ResetForMap();
            hunterRemainingPathSpeedRecovery?.ResetForMap();
            hunterVanillaPathContinuationDiagnostic?.ResetForMap();
            hunterVisibilityDiagnostic?.ResetForMap();
            Shared.GameModeSnapshot gameMode = Shared.GameModeHelper.Capture();
            targetSearchFallbackSingleplayerAllowed = !gameMode.IsRealMultiplayer && !gameMode.IsMapEditor;
            ApplyHunterHutVisibilityPatch();
            Shared.DebugLogHelper.LogInfo(
                log,
                "Improved Hunters target-search fallback mode gate: " +
                $"allowed={targetSearchFallbackSingleplayerAllowed}, {gameMode.ToDiagnosticString()}.");
            nativeScanFailureLogged = false;
            RunNativeScan(force: true);
        }

        private void OnHunterPickUpMeat(UnitHunterPickUpMeatEventArgs args)
        {
            if (!IsValidUnitId(args.UnitId))
            {
                LogInvalidHunterEvent("pickup-meat", args.UnitId);
                return;
            }

            hunterMeatPickupTimestamps[args.UnitId] = Stopwatch.GetTimestamp();
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
            long elapsedTicks = Stopwatch.GetTimestamp() - pickupTimestamp;
            double elapsedSeconds = elapsedTicks / (double)Stopwatch.Frequency;
            if (hunterTargetDiagnosticLogs < MaxHunterTargetDiagnosticLogs)
            {
                log.LogInfo(
                    $"Improved Hunters meat trip timing: hunter={args.UnitId}, pickupToDropoffSeconds={elapsedSeconds:F2}, " +
                    $"assumedHutWorkTicks={HunterHutWorkCost}.");
            }
        }

        private unsafe void TryDeleteCollectedShortLivedCorpse(int hunterUnitId)
        {
            if (!TryGetCollectedCorpseTarget(hunterUnitId, out HunterTargetSnapshot target) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(target.UnitId, out GameUnit* unit) ||
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
                log.LogInfo(
                    $"Improved Hunters collected corpse removed: hunter={hunterUnitId}, target={target.UnitId}, " +
                    $"globalId={target.GlobalId}, aiState=0x{*(ushort*)(unitBytes + 0x2BC):X}.");
            }
        }

        private void LogShortLivedCorpsePreserve(string message)
        {
            if (shortLivedCorpsePreserveLogs >= 80)
                return;

            shortLivedCorpsePreserveLogs++;
            log.LogInfo($"{message} ({shortLivedCorpsePreserveLogs}/80).");

            if (shortLivedCorpsePreserveLogs == 80)
                log.LogInfo("Improved Hunters corpse visible preserve diagnostic limit reached.");
        }

        private unsafe bool TryGetCollectedCorpseTarget(int hunterUnitId, out HunterTargetSnapshot target)
        {
            if (GameUnitManagerAPI.Instance.TryGetUnitById(hunterUnitId, out GameUnit* hunter) &&
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
            if (!settings.EnableMod || !settings.ImprovedPathfinding)
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
            bool recoveryMoveIssued = hunterLineOfSightRecovery?.TryRecoverAfterTargetAbort(
                    units,
                    hunterUnitId,
                    previousTarget.UnitId,
                    previousTarget.GlobalId,
                    timestamp) == true;
            HunterPreyCooldownKey cooldownKey = new HunterPreyCooldownKey(hunterUnitId, previousTarget.GlobalId);
            if (recoveryMoveIssued)
                abortedTargetCooldowns.Remove(cooldownKey);
            else
                SetTargetCooldownUntil(cooldownKey, timestamp + AbortedTargetCooldownInterval);
            TryReleaseAbortedPreyReservation(
                units,
                hunterUnitId,
                previousTarget,
                hasCurrentTarget ? "target-changed" : "target-cleared");

            // MoveToTile replaces the just-observed target order. Do not cache
            // that stale target as active after a recovery move was issued.
            if (hasCurrentTarget && !recoveryMoveIssued)
                activeHunterTargets[hunterUnitId] = new HunterTargetSnapshot(targetUnitId, targetGlobalId);
        }

        private unsafe void TryReleaseAbortedPreyReservation(
            SimpleNativeArray<GameUnit> units,
            int hunterUnitId,
            HunterTargetSnapshot previousTarget,
            string transition,
            bool cooldownApplied = true)
        {
            long cooldownSeconds = cooldownApplied
                ? AbortedTargetCooldownInterval / Stopwatch.Frequency
                : 0;

            if (previousTarget.UnitId <= 0 || previousTarget.UnitId > units.Length)
            {
                LogReservationDiagnostic(
                    $"Improved Hunters prey reservation: source=target-abort, outcome=target-out-of-range, " +
                    $"hunter={hunterUnitId}, transition={transition}, target={previousTarget.UnitId}, " +
                    $"globalId={previousTarget.GlobalId}.",
                    warning: true);
                return;
            }

            GameUnit* prey = units.GetValuePointer(previousTarget.UnitId - 1);
            if (prey->r_GlobalId != previousTarget.GlobalId)
            {
                LogReservationDiagnostic(
                    $"Improved Hunters prey reservation: source=target-abort, outcome=slot-reused, " +
                    $"hunter={hunterUnitId}, transition={transition}, target={previousTarget.UnitId}, " +
                    $"expectedGlobalId={previousTarget.GlobalId}, currentGlobalId={prey->r_GlobalId}.");
                return;
            }

            TryGetPreyEligibility(previousTarget.UnitId, prey, out PreyEligibility eligibility);
            if (!eligibility.KnownAnimal ||
                eligibility.AliveState != (short)AliveState.IsAlive ||
                eligibility.CorpseFlag != 0)
            {
                LogReservationDiagnostic(
                    $"Improved Hunters prey reservation: source=target-abort, outcome=not-live-prey, " +
                    $"hunter={hunterUnitId}, transition={transition}, target={previousTarget.UnitId}/{eligibility.Type}, " +
                    $"globalId={previousTarget.GlobalId}, aliveState={eligibility.AliveState}, " +
                    $"corpseFlag={eligibility.CorpseFlag}, reservation={eligibility.Reservation}.");
                return;
            }

            if (eligibility.Reservation != 2)
            {
                LogReservationDiagnostic(
                    $"Improved Hunters prey reservation: source=target-abort, outcome=no-stale-reservation, " +
                    $"hunter={hunterUnitId}, transition={transition}, target={previousTarget.UnitId}/{eligibility.Type}, " +
                    $"globalId={previousTarget.GlobalId}, reservation={eligibility.Reservation}, " +
                    $"cooldownSeconds={cooldownSeconds}.");
                return;
            }

            if (IsTargetedByAnyLiveHunter(units, previousTarget))
            {
                LogReservationDiagnostic(
                    $"Improved Hunters prey reservation: source=target-abort, outcome=retained-other-hunter, " +
                    $"hunter={hunterUnitId}, transition={transition}, target={previousTarget.UnitId}/{eligibility.Type}, " +
                    $"globalId={previousTarget.GlobalId}, reservation={eligibility.Reservation}.");
                return;
            }

            byte* preyBytes = (byte*)prey;
            *(ushort*)(preyBytes + 0x448) = 0;
            ushort readback = *(ushort*)(preyBytes + 0x448);
            LogReservationDiagnostic(
                $"Improved Hunters prey reservation: source=target-abort, " +
                $"outcome={(readback == 0 ? "released" : "readback-failed")}, hunter={hunterUnitId}, " +
                $"transition={transition}, target={previousTarget.UnitId}/{eligibility.Type}, " +
                $"globalId={previousTarget.GlobalId}, previous=2, readback={readback}, " +
                $"cooldownSeconds={cooldownSeconds}.",
                warning: readback != 0);
        }

        private static unsafe bool IsTargetedByAnyLiveHunter(
            SimpleNativeArray<GameUnit> units,
            HunterTargetSnapshot target)
        {
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* hunter = units.GetValuePointer(index);
                if (hunter->r_AliveState != AliveState.IsAlive ||
                    hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                {
                    continue;
                }

                byte* hunterBytes = (byte*)hunter;
                if (*(ushort*)(hunterBytes + 0x39A) == target.UnitId &&
                    *(uint*)(hunterBytes + 0x39C) == target.GlobalId)
                {
                    return true;
                }
            }

            return false;
        }

        private void LogReservationDiagnostic(string message, bool warning = false)
        {
            if (reservationDiagnosticLogs >= MaxReservationDiagnosticLogs)
                return;

            reservationDiagnosticLogs++;
            string countedMessage = $"{message} ({reservationDiagnosticLogs}/{MaxReservationDiagnosticLogs}).";
            if (warning)
                Shared.DebugLogHelper.LogWarning(log, countedMessage);
            else
                Shared.DebugLogHelper.LogInfo(log, countedMessage);

            if (reservationDiagnosticLogs == MaxReservationDiagnosticLogs)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Improved Hunters prey reservation diagnostic limit reached; further repeated outcomes are suppressed.");
            }
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

        private void LogHunterTargetQuerySummary()
        {
            long timestamp = Stopwatch.GetTimestamp();
            if (timestamp < nextHunterTargetSummaryTimestamp)
                return;

            nextHunterTargetSummaryTimestamp = timestamp + HunterTargetSummaryInterval;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Improved Hunters target query summary: total={hunterTargetQueryEvents}, accepted={hunterTargetAcceptedEvents}, " +
                $"rejected={hunterTargetRejectedEvents}, searches={hunterTargetSearchStarts}, fallback={hunterTargetFallbackEvents}, noBest={hunterTargetNoBestEvents}, " +
                $"preyCache={preyCache.Count}, ranking=bounded-chebyshev-after-PCL, " +
                $"nativeReachability=({hunterPclReachability?.GetDiagnosticSummary() ?? "unavailable"}).");
        }

        private void OnCalculateBonusYield(UnitCalculateBonusYieldEventArgs args)
        {
            if (!settings.EnableMod ||
                GameUnitManagerAPI.Instance.GetType(args.UnitId) != eChimps.CHIMP_TYPE_HUNTER ||
                !hunterPreyTypes.TryGetValue(args.UnitId, out eChimps preyType) ||
                !IsRuntimeHuntingEnabled(preyType))
            {
                return;
            }

            int configuredMeatAmount = settings.GetMeatAmount(preyType);
            if (configuredMeatAmount < 0)
                return;

            args.GoodAmount = configuredMeatAmount;
            args.ReturnValue = args.GoodAmount;
            args.SkipOriginalFunction = true;
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod))
            {
                if (settings.EnableMod)
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
                propertyName == nameof(ImprovedHuntersViewModel.HuntChicken))
            {
                ApplyAutomaticChickenTargetPatch();
                if (settings.EnableMod && settings.HuntChicken)
                {
                    // Recover chickens Vanilla may have created while management was disabled.
                    loadedChickenReconstructionPending = true;
                    RunNativeScan(force: true);
                }
            }

            ClearTargetSelectionCaches();
            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) ||
                huntingTargetSettingChanged ||
                propertyName == nameof(ImprovedHuntersViewModel.ImprovedTargetSelection) ||
                propertyName == nameof(ImprovedHuntersViewModel.ImprovedPathfinding) ||
                propertyName == nameof(ImprovedHuntersViewModel.AllowDeadTargets))
            {
                hunterLineOfSightRecovery?.ResetForMap();
                hunterActiveTargetVisibilitySnapshot?.ResetForMap();
                hunterPclReachability?.ResetForMap();
                hunterPclReachabilityDiagnostic?.ResetForMap();
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

        private void InitializeAutomaticChickenTargetPatch(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                automaticChickenTargetPatch = new AutomaticChickenTargetPatch(
                    log,
                    memory,
                    imageBase,
                    referenceHashMatches);
                ApplyAutomaticChickenTargetPatch();
            }
            catch (Exception exception)
            {
                automaticChickenTargetPatch?.Dispose();
                automaticChickenTargetPatch = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters automatic chicken target protection is unavailable; " +
                    $"other prey features remain active, but chicken ownership will not be neutralized: {exception}");
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
                    memory,
                    imageBase,
                    referenceHashMatches,
                    getLiveChickenCount: GetLiveTrackedGranaryChickenCount,
                    canManageChickens: () =>
                        settings.EnableMod &&
                        settings.HuntChicken &&
                        automaticChickenTargetPatch?.IsApplied == true);
            }
            catch (Exception exception)
            {
                granaryChickenLimitPatch?.Dispose();
                granaryChickenLimitPatch = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters granary chicken limit is unavailable; Vanilla spawning remains active " +
                    $"and no chickens will be neutralized: {exception}");
            }
        }

        private void InitializeManualChickenAttackPatch(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            try
            {
                manualChickenAttackPatch = new ManualChickenAttackPatch(
                    log,
                    memory,
                    imageBase,
                    referenceHashMatches,
                    canAllowManualChickenAttack: () =>
                        settings.EnableMod &&
                        settings.HuntChicken &&
                        automaticChickenTargetPatch?.IsApplied == true);
            }
            catch (Exception exception)
            {
                manualChickenAttackPatch?.Dispose();
                manualChickenAttackPatch = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters manual chicken AttackUnit correction is unavailable; " +
                    $"automatic target protection and other prey features remain active: {exception}");
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

        private void InitializeHunterVisibilityDiagnostic()
        {
            try
            {
                hunterVisibilityDiagnostic = new HunterVisibilityDiagnostic(
                    log,
                    settings);
            }
            catch (Exception exception)
            {
                hunterVisibilityDiagnostic?.Dispose();
                hunterVisibilityDiagnostic = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters temporary visibility diagnostic is unavailable; " +
                    $"Hunter behavior remains unchanged: {exception}");
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

        private void InitializeHunterPclReachabilityDiagnostic(bool referenceHashMatches)
        {
            try
            {
                hunterPclReachabilityDiagnostic = new HunterPclReachabilityDiagnostic(
                    log,
                    referenceHashMatches,
                    CanRunHunterReachability);
            }
            catch (Exception exception)
            {
                hunterPclReachabilityDiagnostic?.Dispose();
                hunterPclReachabilityDiagnostic = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Improved Hunters PCL reachability diagnostic is unavailable; " +
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

            hunterPclReachabilityDiagnostic?.RecordMoveHereResult(
                hunterUnitId,
                preyUnitId,
                preyGlobalId,
                preyType,
                moveHereResult,
                timestamp);
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
            return settings.EnableMod &&
                settings.ImprovedTargetSelection &&
                targetSearchFallbackSingleplayerAllowed;
        }

        private bool CanRunHunterPathfinding()
        {
            return settings.EnableMod &&
                settings.ImprovedPathfinding &&
                targetSearchFallbackSingleplayerAllowed;
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

        private void ApplyAutomaticChickenTargetPatch()
        {
            bool requestedEnabled = settings.EnableMod && settings.HuntChicken;
            if (automaticChickenTargetPatch == null)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Improved Hunters automatic chicken target state request skipped: " +
                    $"requestedEnabled={requestedEnabled}, outcome=patch-not-initialized; " +
                    "chicken ownership neutralization remains inactive.");
                return;
            }

            automaticChickenTargetPatch.TrySetEnabled(requestedEnabled);
        }

        private void LogChickenOwnershipDiagnostic(string message, bool warning = false)
        {
            if (chickenOwnershipDiagnosticLogs >= MaxChickenOwnershipDiagnosticLogs)
                return;

            chickenOwnershipDiagnosticLogs++;
            string countedMessage =
                $"{message} ({chickenOwnershipDiagnosticLogs}/{MaxChickenOwnershipDiagnosticLogs}).";
            if (warning)
                Shared.DebugLogHelper.LogWarning(log, countedMessage);
            else
                Shared.DebugLogHelper.LogInfo(log, countedMessage);

            if (chickenOwnershipDiagnosticLogs == MaxChickenOwnershipDiagnosticLogs)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Improved Hunters chicken ownership diagnostic limit reached; further repeated outcomes are suppressed.");
            }
        }

        private static bool IsValidUnitId(int unitId)
        {
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            return unitId >= 1 && unitId <= units.Length;
        }

        private bool IsRuntimeHuntingEnabled(eChimps type)
        {
            return settings.IsHuntingEnabled(type);
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
            return true;
        }

        private bool IsOwnerAllowed(int hunterOwner, int preyUnitId, eChimps preyType)
        {
            return true;
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
            nextStaleReservationCleanupTimestamp = 0;
            nextHunterTargetSummaryTimestamp = 0;
            nextShortLivedCorpsePreserveCleanupTimestamp = 0;
            lastLoggedDesiredCamelHealth = 0;
            despawnPatchStateLogged = false;
            hunterTargetDiagnosticLogs = 0;
            preyCacheDiagnosticLogs = 0;
            reservationDiagnosticLogs = 0;
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

        private void InitializeHunterLineOfSightRecovery()
        {
            try
            {
                hunterLineOfSightRecovery = new HunterLineOfSightRecovery(
                    log,
                    settings);
            }
            catch (Exception exception)
            {
                hunterLineOfSightRecovery?.Dispose();
                hunterLineOfSightRecovery = null;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters line-of-sight recovery is unavailable; " +
                    $"Hunter movement remains Vanilla: {exception}");
            }
        }

        private sealed class PendingGranaryChickenSpawn
        {
            public readonly int SourcePlayerId;
            public readonly eChimps UnitType;
            public readonly int GranaryTileX;
            public readonly int GranaryTileY;
            public readonly int HeightElevation;
            public readonly long CreatedAt;
            public bool UnitCreateMatched;
            public int WorldTileX;
            public int WorldTileY;

            public PendingGranaryChickenSpawn(
                int sourcePlayerId,
                eChimps unitType,
                int granaryTileX,
                int granaryTileY,
                int heightElevation,
                long createdAt)
            {
                SourcePlayerId = sourcePlayerId;
                UnitType = unitType;
                GranaryTileX = granaryTileX;
                GranaryTileY = granaryTileY;
                HeightElevation = heightElevation;
                CreatedAt = createdAt;
            }
        }

        private struct TrackedGranaryChicken
        {
            public readonly int UnitId;
            public readonly uint GlobalId;
            public readonly int SourcePlayerId;

            public TrackedGranaryChicken(int unitId, uint globalId, int sourcePlayerId)
            {
                UnitId = unitId;
                GlobalId = globalId;
                SourcePlayerId = sourcePlayerId;
            }
        }

        private struct ChickenGranaryCandidate
        {
            public readonly int BuildingId;
            public readonly int PlayerId;
            public readonly int TileX;
            public readonly int TileY;

            public ChickenGranaryCandidate(int buildingId, int playerId, int tileX, int tileY)
            {
                BuildingId = buildingId;
                PlayerId = playerId;
                TileX = tileX;
                TileY = tileY;
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
            if (runtimeEventsSubscribed || !settings.EnableMod)
                return;

            TrySubscribeFeature("hunter target queries", () => UnitR3EventHooks.OnUnitHunterQueryTarget.Observable
                .Where(args => args.Phase == EventHookPhase.Pre).Subscribe(OnHunterQueryTarget));
            TrySubscribeFeature("bonus yield", () => UnitR3EventHooks.OnCalculateBonusYield.Observable
                .Where(args => args.Phase == EventHookPhase.Pre).Subscribe(OnCalculateBonusYield));
            TrySubscribeFeature("granary chicken spawning", () => BuildingR3EventHooks.OnGranarySpawnChicken.Observable
                .Where(args => args.Phase == EventHookPhase.Pre).Subscribe(OnGranarySpawnChicken));
            TrySubscribeFeature("unit creation", () => UnitR3EventHooks.OnUnitCreate.Observable.Subscribe(OnUnitCreate));
            TrySubscribeFeature("hunter meat pickup", () => UnitR3EventHooks.OnHunterPickUpMeat.Observable
                .Where(args => args.Phase == EventHookPhase.Pre).Subscribe(OnHunterPickUpMeat));
            TrySubscribeFeature("hunter meat dropoff", () => UnitR3EventHooks.OnHunterDropOffMeat.Observable
                .Where(args => args.Phase == EventHookPhase.Pre).Subscribe(OnHunterDropOffMeat));
            TrySubscribeFeature("projectile spawning", () => ProjectileR3EventHooks.OnProjectileSpawn.Observable
                .Where(args => args.Phase == EventHookPhase.Post).Subscribe(OnProjectileSpawn));
            TrySubscribeFeature("projectile deletion", () => ProjectileR3EventHooks.OnProjectileDelete.Observable
                .Where(args => args.Phase == EventHookPhase.Pre).Subscribe(OnProjectileDelete));
            TrySubscribeFeature("map start", () => MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post).Subscribe(_ => OnMapStarted()));
            TrySubscribeFeature("movement scan trigger", () => UnitR3EventHooks.OnUnitMovement.Observable.Subscribe(_ => RunNativeScan()));
            TrySubscribeFeature("visual scan trigger", () => UnitR3EventHooks.OnUnitUnityVisualInterpolate.Observable.Subscribe(_ => RunNativeScan()));
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
            hunterPreyTypes.Clear();
            nextIdleHunterRequeryTimestamps.Clear();
            pendingGranaryChickenSpawns.Clear();
            ClearTrackedGranaryChickens();
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
            settings.SettingChanged -= OnSettingChanged;

            UnsubscribeRuntimeEvents();
            hunterPreyTypes.Clear();
            nextIdleHunterRequeryTimestamps.Clear();
            loggedCollectedCorpseGlobalIds.Clear();
            ClearTrackedGranaryChickens();
            pendingGranaryChickenSpawns.Clear();
            staleGranaryChickenUnitIds.Clear();
            loadedChickenReconstructionPending = false;
            ClearTargetSelectionCaches();
            nativeScanFailureLogged = false;
            nextNativeScanTimestamp = 0;

            hunterVisibilityDiagnostic?.Dispose();
            hunterVisibilityDiagnostic = null;
            hunterTargetSearchFallbackDiagnostic?.Dispose();
            hunterTargetSearchFallbackDiagnostic = null;
            hunterPostShotContinuationDiagnostic?.Dispose();
            hunterPostShotContinuationDiagnostic = null;
            hunterVanillaPathContinuationDiagnostic?.Dispose();
            hunterVanillaPathContinuationDiagnostic = null;
            hunterActiveTargetVisibilitySnapshot?.Dispose();
            hunterActiveTargetVisibilitySnapshot = null;
            hunterPclReachabilityDiagnostic?.Dispose();
            hunterPclReachabilityDiagnostic = null;
            hunterPclReachability?.Dispose();
            hunterPclReachability = null;
            hunterRemainingPathSpeedRecovery?.Dispose();
            hunterRemainingPathSpeedRecovery = null;
            hunterNativeVisibilityProbe?.Dispose();
            hunterNativeVisibilityProbe = null;
            hunterHutVisibilityPatch?.Dispose();
            hunterHutVisibilityPatch = null;
            hunterLineOfSightRecovery?.Dispose();
            hunterLineOfSightRecovery = null;
            hunterQueryActorWorkaround?.Dispose();
            hunterQueryActorWorkaround = null;
            granaryChickenLimitPatch?.Dispose();
            granaryChickenLimitPatch = null;
            manualChickenAttackPatch?.Dispose();
            manualChickenAttackPatch = null;
            automaticChickenTargetPatch?.Dispose();
            automaticChickenTargetPatch = null;

            if (rabbitDespawnTicksPatched && rabbitDespawnTickTime != null)
                rabbitDespawnTickTime.SetValue(originalRabbitDespawnTicks);

            if (camelDespawnTicksPatched && camelDespawnTickTime != null)
                camelDespawnTickTime.SetValue(originalCamelDespawnTicks);

            if (chickenDespawnTicksPatched && chickenDespawnTickTime != null)
                chickenDespawnTickTime.SetValue(originalChickenDespawnTicks);

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
            rabbitDespawnTicksInitialized = false;
            extraDespawnTicksInitialized = false;
            applied = false;
        }
    }
}
