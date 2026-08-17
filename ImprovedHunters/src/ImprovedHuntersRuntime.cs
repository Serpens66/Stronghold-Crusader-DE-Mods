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
    internal sealed class ImprovedHuntersRuntime : IDisposable
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
        private const int MaxHunterProjectileDiagnosticLogs = 160;
        private const int MaxChickenOwnershipDiagnosticLogs = 160;
        private const int MaximumPlayerId = 8;
        private const int MaxReservationDiagnosticLogs = 80;
        private const int MaxInvalidHunterEventLogs = 20;
        private const int MaxHunterQueryActorWorkaroundLogs = 20;

        // Native pickupable animal corpse state observed after regular Hunter
        // ranged damage.
        private const ushort HunterCorpsePickupAiState = 0x6E;
        // Retained only so corpses created by older KillUnit-based versions can
        // expire cleanly after loading; new compensation never creates 0x6F.
        private const ushort HunterFreshCorpseAiState = 0x6F;
        private const long ExpiredShortLivedCorpsePreserve = long.MinValue;
        private const int MaxHunterProjectileDamageAttempts = 3;
        private const int HunterProjectileNearTargetDistance = 32;

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
        private static readonly long HunterProjectileMinimumFlightTime = Stopwatch.Frequency / 4;
        private static readonly long HunterProjectileStallInterval = Stopwatch.Frequency * 3 / 10;
        private static readonly long HunterProjectileRetryInterval = Stopwatch.Frequency / 10;
        private static readonly long HunterProjectileIntentLifetime = Stopwatch.Frequency * 5;
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
        private readonly Dictionary<HunterShotIntentKey, PendingHunterShotIntent> pendingHunterShotIntents = new Dictionary<HunterShotIntentKey, PendingHunterShotIntent>();
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
        private bool hunterProjectileCompensationFailureLogged;
        private bool hunterProjectileCleanupFailureLogged;
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
        private int hunterProjectileDiagnosticLogs;
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
        private HunterPclReachability hunterPclReachability;
        private HunterPclReachabilityDiagnostic hunterPclReachabilityDiagnostic;
        private HunterRemainingPathSpeedRecovery hunterRemainingPathSpeedRecovery;
        private HunterTargetSearchFallbackDiagnostic hunterTargetSearchFallbackDiagnostic;
        private HunterVanillaPathContinuationDiagnostic hunterVanillaPathContinuationDiagnostic;
        private HunterVisibilityDiagnostic hunterVisibilityDiagnostic;
        private bool referenceHashMatches;
        private bool targetSearchFallbackSingleplayerAllowed;
        private bool loadedChickenReconstructionPending;
        private long nextGranaryChickenCleanupTimestamp;
        private bool applied;

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

            try
            {
                this.referenceHashMatches = referenceHashMatches;
                InitializeAutomaticChickenTargetPatch(memory, imageBase, referenceHashMatches);
                InitializeManualChickenAttackPatch(memory, imageBase, referenceHashMatches);
                InitializeGranaryChickenLimitPatch(memory, imageBase, referenceHashMatches);
                InitializeHunterQueryActorWorkaround(memory, imageBase, referenceHashMatches);
                InitializeHunterNativeVisibilityProbe(memory, imageBase, referenceHashMatches);
                InitializeHunterHutVisibilityPatch(memory, imageBase, referenceHashMatches);
                InitializeHunterPclReachability(referenceHashMatches);
                InitializeHunterPclReachabilityDiagnostic(referenceHashMatches);
                InitializeHunterTargetSearchFallbackDiagnostic(memory, imageBase, referenceHashMatches);
                InitializeHunterRemainingPathSpeedRecovery(memory, imageBase, referenceHashMatches);
                InitializeHunterVanillaPathContinuationDiagnostic(memory, imageBase, referenceHashMatches);
                InitializeHunterLineOfSightRecovery();
                InitializeHunterVisibilityDiagnostic();

                subscriptions.Add(UnitR3EventHooks.OnUnitHunterQueryTarget.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnHunterQueryTarget));
                subscriptions.Add(UnitR3EventHooks.OnCalculateBonusYield.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnCalculateBonusYield));
                subscriptions.Add(BuildingR3EventHooks.OnGranarySpawnChicken.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnGranarySpawnChicken));
                subscriptions.Add(UnitR3EventHooks.OnUnitCreate.Observable.Subscribe(OnUnitCreate));
                subscriptions.Add(UnitR3EventHooks.OnHunterPickUpMeat.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnHunterPickUpMeat));
                subscriptions.Add(UnitR3EventHooks.OnHunterDropOffMeat.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnHunterDropOffMeat));
                subscriptions.Add(ProjectileR3EventHooks.OnProjectileSpawn.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnProjectileSpawn));
                subscriptions.Add(ProjectileR3EventHooks.OnProjectileDelete.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnProjectileDelete));
                subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => OnMapStarted()));
                subscriptions.Add(UnitR3EventHooks.OnUnitMovement.Observable
                    .Subscribe(_ => RunNativeScan()));
                subscriptions.Add(UnitR3EventHooks.OnUnitUnityVisualInterpolate.Observable
                    .Subscribe(_ => RunNativeScan()));

                settings.SettingChanged += OnSettingChanged;
                InitializeRabbitDespawnPatch();
                InitializeExtraDespawnPatches(memory, imageBase);
                ApplyDespawnPatches();
                ApplyCamelHealthPatch();

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
                    $"hunterPclReachabilityAvailable={hunterPclReachability?.IsAvailable == true}, " +
                    $"hunterPclReachabilityDiagnosticAvailable={hunterPclReachabilityDiagnostic?.IsAvailable == true}, " +
                    $"hunterRemainingPathSpeedRecoveryAvailable={hunterRemainingPathSpeedRecovery?.IsAvailable == true}, " +
                    $"hunterTargetSearchFallbackAvailable={hunterTargetSearchFallbackDiagnostic?.IsAvailable == true}, " +
                    $"hunterVanillaPathContinuationAvailable={hunterVanillaPathContinuationDiagnostic?.IsAvailable == true}, " +
                    $"hunterVisibilityDiagnosticAvailable={hunterVisibilityDiagnostic?.IsAvailable == true}, " +
                    $"referenceHashMatches={referenceHashMatches}.");
            }
            catch
            {
                // Restore the native dispatch entry and remove partial subscriptions.
                Dispose();
                throw;
            }
        }

        public unsafe void RunNativeScan(bool force = false)
        {
            long timestamp = Stopwatch.GetTimestamp();
            if (!applied || (!force && timestamp < nextNativeScanTimestamp))
                return;

            nextNativeScanTimestamp = timestamp + NativeScanInterval;

            try
            {
                // Re-apply cheap native/default patches here because settings and
                // some game globals can be recreated around map transitions.
                ApplyDespawnPatches();
                ApplyCamelHealthPatch();
                RunHunterProjectileCompensation(timestamp);

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
            if (!CanRunHunterTargetSearchFallback() ||
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
                if (!hunterPclReachability.TryIsReachable(
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
            eligibility.Eligible =
                eligibility.RuntimeHuntingEnabled &&
                eligibility.OwnerAllowed &&
                eligibility.AliveState == (short)AliveState.IsAlive &&
                eligibility.FlagsAllowed &&
                eligibility.Reservation == 0 &&
                (eligibility.CorpseFlag == 0 || eligibility.AiState == HunterCorpsePickupAiState);

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
            hunterPclReachability?.ResetForMap();
            hunterPclReachabilityDiagnostic?.ResetForMap();
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
            hunterProjectileCompensationFailureLogged = false;
            hunterProjectileCleanupFailureLogged = false;
            RunNativeScan(force: true);
        }

        private void OnHunterQueryTarget(UnitHunterQueryTargetEventArgs args)
        {
            if (!settings.EnableMod)
                return;

            long timestamp = Stopwatch.GetTimestamp();
            if (!IsValidUnitId(args.QueryUnitId))
                return;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!TryResolveHunterQueryActor(unitApi, args, out int hunterUnitId))
                return;

            eChimps queryType = unitApi.GetType(args.QueryUnitId);
            int queryGlobalId = unitApi.GetGlobalId(args.QueryUnitId);
            if (settings.IsKnownAnimal(queryType) && queryGlobalId > 0)
            {
                hunterNativeVisibilityProbe?.RecordQueryCandidate(
                    hunterUnitId,
                    args.QueryUnitId,
                    queryType,
                    unchecked((uint)queryGlobalId),
                    timestamp);
            }

            TrackHunterSearchQuery(hunterUnitId, timestamp);

            if (!settings.IsKnownAnimal(queryType))
                return;

            if (!IsRuntimeHuntingEnabled(queryType))
                return;

            if (!IsOwnerAllowed(unitApi.GetOwner(hunterUnitId), args.QueryUnitId, queryType))
            {
                return;
            }

            if (settings.ImprovedPathfinding && queryGlobalId > 0)
            {
                hunterPclReachabilityDiagnostic?.RecordCandidate(
                    hunterUnitId,
                    args.QueryUnitId,
                    unchecked((uint)queryGlobalId),
                    queryType,
                    timestamp);
            }

            bool targetPclUnreachable = false;
            if (settings.ImprovedPathfinding &&
                queryGlobalId > 0 &&
                hunterPclReachability != null &&
                hunterPclReachability.TryIsReachable(
                    hunterUnitId,
                    args.QueryUnitId,
                    unchecked((uint)queryGlobalId),
                    queryType,
                    timestamp,
                    out bool targetPclReachable))
            {
                targetPclUnreachable = !targetPclReachable;
            }
            bool targetOnCooldown =
                settings.ImprovedPathfinding &&
                queryGlobalId > 0 &&
                IsTargetOnCooldown(hunterUnitId, unchecked((uint)queryGlobalId), timestamp);
            bool isValidTarget = !targetPclUnreachable && !targetOnCooldown;
            bool usedFallback = false;
            TargetSelection targetSelection = default;
            BestTarget bestTarget = default;
            if (!settings.ImprovedPathfinding)
            {
                isValidTarget = true;
            }
            else if (targetPclUnreachable)
            {
                // A zero from the same player-aware PCL query used by MoveHere
                // is sufficient to reject this candidate before any order.
            }
            else if (targetOnCooldown)
            {
                // A failed native MoveHere is authoritative for this Hunter/prey
                // pair until its bounded retry window expires.
            }
            else if (TryGetTargetSelectionForHunter(hunterUnitId, timestamp, out targetSelection))
            {
                bestTarget = targetSelection.BestTarget;
                isValidTarget = targetSelection.IsAllowed(args.QueryUnitId);
                usedFallback = false;
            }
            else
            {
                hunterTargetNoBestEvents++;
                isValidTarget = true;
                usedFallback = true;
            }

            args.IsValidTarget = isValidTarget;
            if (isValidTarget &&
                settings.ImprovedPathfinding &&
                queryGlobalId > 0)
            {
                // This is a no-op outside the exact state-1 near-target query.
                // It lets only this Hunter's still-live reservation-2 target
                // pass through Vanilla's normal state-0 reacquisition path.
                hunterTargetSearchFallbackDiagnostic?.RecordStateOneRefreshCandidate(
                    hunterUnitId,
                    args.QueryUnitId,
                    unchecked((uint)queryGlobalId),
                    queryType);
            }
            if (isValidTarget &&
                settings.ImprovedPathfinding &&
                targetSelection.HasTarget &&
                queryGlobalId > 0)
            {
                hunterTargetSearchFallbackDiagnostic?.RecordCandidate(
                    hunterUnitId,
                    args.QueryUnitId,
                    unchecked((uint)queryGlobalId),
                    queryType,
                    preferred: args.QueryUnitId == bestTarget.UnitId,
                    timestamp: timestamp);
            }
            hunterTargetQueryEvents++;
            if (isValidTarget)
            {
                hunterTargetAcceptedEvents++;
                hunterPreyTypes[hunterUnitId] = queryType;
                if (queryType == eChimps.CHIMP_TYPE_CHICKEN)
                {
                    if (queryGlobalId > 0)
                    {
                        hunterVisibilityDiagnostic?.RecordAcceptedChickenTarget(
                            hunterUnitId,
                            args.QueryUnitId,
                            unchecked((uint)queryGlobalId));
                    }
                }
            }
            else
            {
                hunterTargetRejectedEvents++;
            }

            if (usedFallback)
                hunterTargetFallbackEvents++;

            LogHunterTargetQueryDiagnostic(
                hunterUnitId,
                args.QueryUnitId,
                queryType,
                isValidTarget,
                usedFallback,
                targetPclUnreachable,
                targetOnCooldown,
                targetSelection);
            LogHunterTargetQuerySummary();
        }

        private unsafe bool TryResolveHunterQueryActor(
            GameUnitManagerAPI unitApi,
            UnitHunterQueryTargetEventArgs args,
            out int hunterUnitId)
        {
            hunterUnitId = 0;
            int capturedHunterUnitId = 0;
            bool captured = hunterQueryActorWorkaround?.TryConsumeHunterUnitId(
                args.QueryUnitId,
                out capturedHunterUnitId) == true;
            hunterVisibilityDiagnostic?.RecordActorResolution(
                args.HunterUnitId,
                capturedHunterUnitId,
                args.QueryUnitId,
                captured);

            if (captured && IsLiveHunter(unitApi, capturedHunterUnitId))
            {
                hunterUnitId = capturedHunterUnitId;
                if (capturedHunterUnitId != args.HunterUnitId)
                {
                    LogHunterQueryActorWorkaround(
                        $"Improved Hunters corrected Script Extender issue-123 Hunter ID: " +
                        $"reported={args.HunterUnitId}, reconstructed={capturedHunterUnitId}, " +
                        $"query={args.QueryUnitId}.");
                }

                return true;
            }

            if (IsLiveHunter(unitApi, args.HunterUnitId))
            {
                hunterUnitId = args.HunterUnitId;
                return true;
            }

            LogHunterQueryActorWorkaround(
                $"Improved Hunters ignored Hunter target query with unresolved actor: " +
                $"reported={args.HunterUnitId}, captured={capturedHunterUnitId}, " +
                $"captureMatched={captured}, query={args.QueryUnitId}, outcome=leave-Vanilla-unchanged.",
                warning: true);
            return false;
        }

        private static unsafe bool IsLiveHunter(GameUnitManagerAPI unitApi, int unitId)
        {
            return IsValidUnitId(unitId) &&
                unitApi.TryGetUnitById(unitId, out GameUnit* unit) &&
                unit != null &&
                unit->r_AliveState == AliveState.IsAlive &&
                unit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER;
        }

        private void LogHunterQueryActorWorkaround(string message, bool warning = false)
        {
            if (hunterQueryActorWorkaroundLogs >= MaxHunterQueryActorWorkaroundLogs)
                return;

            hunterQueryActorWorkaroundLogs++;
            string boundedMessage =
                $"{message} ({hunterQueryActorWorkaroundLogs}/{MaxHunterQueryActorWorkaroundLogs}).";
            if (warning)
                Shared.DebugLogHelper.LogWarning(log, boundedMessage);
            else
                Shared.DebugLogHelper.LogInfo(log, boundedMessage);
        }

        private unsafe bool TryGetTargetSelectionForHunter(int hunterUnitId, long timestamp, out TargetSelection targetSelection)
        {
            targetSelection = default;

            if (bestTargetCache.TryGetValue(hunterUnitId, out CachedBestTarget cachedBestTarget) &&
                timestamp < cachedBestTarget.ExpiresAt)
            {
                targetSelection = cachedBestTarget.Selection;
                return targetSelection.HasTarget;
            }

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!unitApi.TryGetUnitById(hunterUnitId, out GameUnit* hunter) ||
                hunter == null ||
                hunter->r_AliveState != AliveState.IsAlive ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
            {
                CacheTargetSelection(hunterUnitId, default, timestamp);
                return false;
            }

            int hunterOwner = unitApi.GetOwner(hunterUnitId);
            if (!TryGetHunterOrigin(hunter, hunterOwner, out int originTileX, out int originTileY, out int granaryRoundTripHeuristicCost))
            {
                CacheTargetSelection(hunterUnitId, default, timestamp);
                return false;
            }

            RefreshPreyCacheIfNeeded(force: false, timestamp);
            if (preyCache.Count == 0)
            {
                CacheTargetSelection(hunterUnitId, default, timestamp);
                return false;
            }

            List<PreyCandidate> candidates = new List<PreyCandidate>();
            for (int i = 0; i < preyCache.Count; i++)
            {
                PreySnapshot prey = preyCache[i];
                if (!TryGetLiveAvailablePreySnapshot(prey, out prey))
                    continue;

                if (!IsOwnerAllowed(hunterOwner, prey.UnitId, prey.Type))
                    continue;

                int heuristicDistance = GetChebyshevDistance(originTileX, originTileY, prey.TileX, prey.TileY);
                if (heuristicDistance > HunterTargetCandidateRadius)
                    continue;

                if (hunterPclReachability != null &&
                    hunterPclReachability.TryIsReachable(
                        hunterUnitId,
                        prey.UnitId,
                        prey.GlobalId,
                        prey.Type,
                        timestamp,
                        out bool preyReachable) &&
                    !preyReachable)
                {
                    continue;
                }

                if (IsTargetOnCooldown(hunterUnitId, prey.GlobalId, timestamp))
                    continue;

                int heuristicCycleCost = HunterHutWorkCost + GetPreyHandlingCost(prey.Type) + granaryRoundTripHeuristicCost + (heuristicDistance * 10 * 2);
                candidates.Add(new PreyCandidate(prey, heuristicCycleCost));
            }

            if (candidates.Count == 0)
            {
                CacheTargetSelection(hunterUnitId, default, timestamp);
                return false;
            }

            candidates.Sort(ComparePreyCandidatesByHeuristic);

            bool hasBest = false;
            BestTarget currentBest = default;
            List<BestTarget> evaluatedTargets = new List<BestTarget>();
            int limit = Math.Min(candidates.Count, MaxHeuristicCandidatesPerHunter);
            for (int i = 0; i < limit; i++)
            {
                PreySnapshot prey = candidates[i].Prey;
                if (!TryGetLiveAvailablePreySnapshot(prey, out prey))
                    continue;

                // The Script Extender's managed A* has no expansion budget and
                // can monopolize the game thread for unreachable destinations.
                // PCL connectivity has already rejected disconnected regions.
                // This estimate ranks the remaining candidates while Vanilla's
                // detailed MoveHere path creation stays authoritative.
                int approachHeuristicCost = GetChebyshevDistance(
                    originTileX,
                    originTileY,
                    prey.TileX,
                    prey.TileY) * 10;
                int cycleCost = HunterHutWorkCost + GetPreyHandlingCost(prey.Type) + granaryRoundTripHeuristicCost + (approachHeuristicCost * 2);
                if (cycleCost <= 0)
                    cycleCost = 1;

                BestTarget candidate = new BestTarget(
                    prey.UnitId,
                    prey.GlobalId,
                    prey.Type,
                    prey.MeatAmount,
                    approachHeuristicCost,
                    granaryRoundTripHeuristicCost,
                    cycleCost);
                evaluatedTargets.Add(candidate);
                if (!hasBest || IsBetterTarget(candidate, currentBest))
                {
                    currentBest = candidate;
                    hasBest = true;
                }
            }

            if (!hasBest)
            {
                CacheTargetSelection(hunterUnitId, default, timestamp);
                return false;
            }

            HashSet<int> allowedUnitIds = new HashSet<int>();
            for (int i = 0; i < evaluatedTargets.Count; i++)
            {
                BestTarget candidate = evaluatedTargets[i];
                if (IsWithinTargetTolerance(candidate, currentBest))
                    allowedUnitIds.Add(candidate.UnitId);
            }

            if (allowedUnitIds.Count == 0)
                allowedUnitIds.Add(currentBest.UnitId);

            targetSelection = new TargetSelection(currentBest, allowedUnitIds);
            CacheTargetSelection(hunterUnitId, targetSelection, timestamp);
            return true;
        }

        private unsafe bool TryGetLiveAvailablePreySnapshot(PreySnapshot cachedPrey, out PreySnapshot livePrey)
        {
            livePrey = default;
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!unitApi.TryGetUnitById(cachedPrey.UnitId, out GameUnit* unit) ||
                unit == null)
            {
                return false;
            }

            if (!TryGetPreyEligibility(cachedPrey.UnitId, unit, out PreyEligibility eligibility) ||
                eligibility.GlobalId != cachedPrey.GlobalId ||
                eligibility.Type != cachedPrey.Type ||
                !eligibility.Eligible)
            {
                return false;
            }

            livePrey = new PreySnapshot(
                cachedPrey.UnitId,
                cachedPrey.GlobalId,
                cachedPrey.Type,
                eligibility.TileX,
                eligibility.TileY,
                cachedPrey.MeatAmount);
            return true;
        }

        private unsafe bool TryGetHunterOrigin(
            GameUnit* hunter,
            int hunterOwner,
            out int originTileX,
            out int originTileY,
            out int granaryRoundTripHeuristicCost)
        {
            originTileX = hunter->r_CurrentTilePositionX;
            originTileY = hunter->r_CurrentTilePositionY;
            granaryRoundTripHeuristicCost = 0;

            ushort linkedBuildingId = hunter->r_LinkedProductionBuildingId;
            if (linkedBuildingId != 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(linkedBuildingId, out GameBuilding* building) &&
                building != null &&
                building->r_AliveState == AliveState.IsAlive &&
                building->r_BuildingType == eStructs.STRUCT_HUNTERS_HUT)
            {
                originTileX = building->r_TilePositionXBegin;
                originTileY = building->r_TilePositionYBegin;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsTileInsideMapBounds(originTileX, originTileY))
                return false;

            int originTileId = tileApi.GetTileId(originTileX, originTileY);
            if (!tileApi.IsValidTileId(originTileId))
                return false;

            if (!tileApi.IsTileWalkableAndUnoccupied(originTileId))
            {
                UnmanagedVector2<ushort> nearestWalkable = tileApi.GetNearestUnoccupiedTile(originTileX, originTileY, maxRange: 8);
                originTileX = nearestWalkable.X;
                originTileY = nearestWalkable.Y;
                if (!tileApi.IsTileInsideMapBounds(originTileX, originTileY))
                    return false;

                originTileId = tileApi.GetTileId(originTileX, originTileY);
            }

            if (!tileApi.IsValidTileId(originTileId))
                return false;

            if (TryGetNearestGranaryHeuristicCost(hunterOwner, originTileX, originTileY, out int granaryHeuristicCost))
                granaryRoundTripHeuristicCost = granaryHeuristicCost * 2;

            return true;
        }

        private unsafe bool TryGetNearestGranaryHeuristicCost(
            int hunterOwner,
            int originTileX,
            int originTileY,
            out int bestHeuristicCost)
        {
            bestHeuristicCost = 0;
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            SimpleNativeArray<GameBuilding> buildings = buildingApi.GetBuildingsArray();
            if (buildings._array == null || buildings.Length == 0)
                return false;

            bool found = false;
            int bestBuildingId = int.MaxValue;
            for (int index = 0; index < buildings.Length; index++)
            {
                GameBuilding* building = buildings.GetValuePointer(index);
                if (building->r_AliveState != AliveState.IsAlive ||
                    building->r_BuildingType != eStructs.STRUCT_GRANARY ||
                    building->r_PlayerIdOwner != hunterOwner)
                {
                    continue;
                }

                if (!TryGetWalkableTileNear(
                        building->r_TilePositionXBegin,
                        building->r_TilePositionYBegin,
                        10,
                        out int targetTileX,
                        out int targetTileY,
                        out _))
                {
                    continue;
                }

                int heuristicCost = GetChebyshevDistance(
                    originTileX,
                    originTileY,
                    targetTileX,
                    targetTileY) * 10;
                int buildingId = index + 1;
                if (!found ||
                    heuristicCost < bestHeuristicCost ||
                    (heuristicCost == bestHeuristicCost && buildingId < bestBuildingId))
                {
                    bestHeuristicCost = heuristicCost;
                    bestBuildingId = buildingId;
                    found = true;
                }
            }

            return found;
        }

        private bool TryGetWalkableTileNear(int tileX, int tileY, int maxRange, out int walkableTileX, out int walkableTileY, out int walkableTileId)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            UnmanagedVector2<ushort> nearestWalkable = tileApi.GetNearestUnoccupiedTile(tileX, tileY, maxRange);
            walkableTileX = nearestWalkable.X;
            walkableTileY = nearestWalkable.Y;
            walkableTileId = 0;

            if (!tileApi.IsTileInsideMapBounds(walkableTileX, walkableTileY))
                return false;

            walkableTileId = tileApi.GetTileId(walkableTileX, walkableTileY);
            return tileApi.IsValidTileId(walkableTileId) &&
                tileApi.IsTileWalkableAndUnoccupied(walkableTileId);
        }

        private unsafe void RefreshPreyCacheIfNeeded(bool force, long timestamp)
        {
            if (!force && timestamp < nextPreyCacheRefreshTimestamp)
                return;

            nextPreyCacheRefreshTimestamp = timestamp + PreyCacheRefreshInterval;
            preyCache.Clear();
            bestTargetCache.Clear();

            if (!settings.EnableMod)
                return;

            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            if (units._array == null || units.Length == 0)
                return;

            int knownCount = 0;
            int skippedKnownCount = 0;
            int eligibleDeer = 0;
            int eligibleGoat = 0;
            int eligibleRabbit = 0;
            int eligibleCamel = 0;
            int eligibleChicken = 0;
            int eligibleCow = 0;
            int skippedCamels = 0;

            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                int unitId = index + 1;
                TryGetPreyEligibility(unitId, unit, out PreyEligibility eligibility);
                if (!eligibility.KnownAnimal)
                    continue;

                knownCount++;
                if (!eligibility.Eligible)
                {
                    skippedKnownCount++;
                    if (eligibility.Type == eChimps.CHIMP_TYPE_CAMEL)
                    {
                        skippedCamels++;
                        LogPreyCacheDiagnostic(unitId, eligibility, GetPreyIneligibilityReason(eligibility));
                    }

                    continue;
                }

                preyCache.Add(new PreySnapshot(
                    unitId,
                    eligibility.GlobalId,
                    eligibility.Type,
                    eligibility.TileX,
                    eligibility.TileY,
                    settings.GetMeatAmount(eligibility.Type)));

                IncrementAnimalCount(
                    eligibility.Type,
                    ref eligibleDeer,
                    ref eligibleGoat,
                    ref eligibleRabbit,
                    ref eligibleCamel,
                    ref eligibleChicken,
                    ref eligibleCow);

                if (eligibility.Type == eChimps.CHIMP_TYPE_CAMEL)
                    LogPreyCacheDiagnostic(unitId, eligibility, "eligible");
            }

            LogPreyCacheSummary(
                knownCount,
                skippedKnownCount,
                skippedCamels,
                eligibleDeer,
                eligibleGoat,
                eligibleRabbit,
                eligibleCamel,
                eligibleChicken,
                eligibleCow);
        }

        private unsafe void ReleaseStalePreyReservationsIfNeeded(SimpleNativeArray<GameUnit> units, long timestamp)
        {
            if (timestamp < nextStaleReservationCleanupTimestamp)
                return;

            nextStaleReservationCleanupTimestamp = timestamp + StaleReservationCleanupInterval;

            HashSet<int> activeHunterTargetUnitIds = new HashSet<int>();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                {
                    continue;
                }

                byte* hunterBytes = (byte*)unit;
                ushort targetUnitId = *(ushort*)(hunterBytes + 0x39A);
                if (targetUnitId > 0 && targetUnitId <= units.Length)
                    activeHunterTargetUnitIds.Add(targetUnitId);
            }

            int reservedKnownPrey = 0;
            int retainedReservations = 0;
            int releasedReservations = 0;
            int failedReadbacks = 0;
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                int unitId = index + 1;
                TryGetPreyEligibility(unitId, unit, out PreyEligibility eligibility);
                if (!eligibility.KnownAnimal ||
                    !eligibility.RuntimeHuntingEnabled ||
                    eligibility.AliveState != (short)AliveState.IsAlive ||
                    eligibility.CorpseFlag != 0 ||
                    eligibility.Reservation != 2)
                {
                    continue;
                }

                reservedKnownPrey++;
                if (activeHunterTargetUnitIds.Contains(unitId))
                {
                    retainedReservations++;
                    continue;
                }

                byte* preyBytes = (byte*)unit;
                *(ushort*)(preyBytes + 0x448) = 0;
                ushort readback = *(ushort*)(preyBytes + 0x448);
                eligibility.Reservation = readback;
                if (readback == 0)
                {
                    releasedReservations++;
                    LogReservationDiagnostic(
                        $"Improved Hunters prey reservation: source=periodic-cleanup, outcome=released, " +
                        $"target={unitId}/{eligibility.Type}, globalId={eligibility.GlobalId}, previous=2, readback={readback}.");
                }
                else
                {
                    failedReadbacks++;
                    LogReservationDiagnostic(
                        $"Improved Hunters prey reservation: source=periodic-cleanup, outcome=readback-failed, " +
                        $"target={unitId}/{eligibility.Type}, globalId={eligibility.GlobalId}, previous=2, readback={readback}.",
                        warning: true);
                }
            }

            if (releasedReservations > 0 || failedReadbacks > 0)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters stale prey reservation cleanup: reservedKnownPrey={reservedKnownPrey}, " +
                    $"retained={retainedReservations}, released={releasedReservations}, failedReadbacks={failedReadbacks}, " +
                    $"activeHunterTargets={activeHunterTargetUnitIds.Count}, " +
                    $"invariant={reservedKnownPrey == retainedReservations + releasedReservations + failedReadbacks}.");
            }
        }

        private void LogPreyCacheSummary(
            int knownCount,
            int skippedKnownCount,
            int skippedCamels,
            int eligibleDeer,
            int eligibleGoat,
            int eligibleRabbit,
            int eligibleCamel,
            int eligibleChicken,
            int eligibleCow)
        {
            if (preyCacheDiagnosticLogs >= MaxPreyCacheDiagnosticLogs)
                return;

            preyCacheDiagnosticLogs++;
            log.LogInfo(
                $"Improved Hunters prey cache refreshed: eligible={preyCache.Count}, known={knownCount}, skippedKnown={skippedKnownCount}, " +
                $"eligibleByType=deer:{eligibleDeer}/goat:{eligibleGoat}/rabbit:{eligibleRabbit}/camel:{eligibleCamel}/chicken:{eligibleChicken}/cow:{eligibleCow}, " +
                $"skippedCamels={skippedCamels} ({preyCacheDiagnosticLogs}/{MaxPreyCacheDiagnosticLogs}).");

            if (preyCacheDiagnosticLogs == MaxPreyCacheDiagnosticLogs)
                log.LogInfo("Improved Hunters prey cache diagnostic limit reached.");
        }

        private void LogPreyCacheDiagnostic(int unitId, PreyEligibility eligibility, string status)
        {
            if (preyCacheDiagnosticLogs >= MaxPreyCacheDiagnosticLogs)
                return;

            preyCacheDiagnosticLogs++;
            log.LogInfo(
                $"Improved Hunters prey cache animal: unit={unitId}/{eligibility.Type}, globalId={eligibility.GlobalId}, " +
                $"tile={eligibility.TileX},{eligibility.TileY}, status={status}, aliveState={eligibility.AliveState}, " +
                $"flags92={eligibility.FlagsAt92}, aiState=0x{eligibility.AiState:X}, corpseFlag={eligibility.CorpseFlag}, " +
                $"reservation={eligibility.Reservation}, runtimeEnabled={eligibility.RuntimeHuntingEnabled}, ownerAllowed={eligibility.OwnerAllowed} " +
                $"({preyCacheDiagnosticLogs}/{MaxPreyCacheDiagnosticLogs}).");

            if (preyCacheDiagnosticLogs == MaxPreyCacheDiagnosticLogs)
                log.LogInfo("Improved Hunters prey cache diagnostic limit reached.");
        }

        private static string GetPreyIneligibilityReason(PreyEligibility eligibility)
        {
            if (!eligibility.RuntimeHuntingEnabled)
                return "disabled";

            if (!eligibility.OwnerAllowed)
                return "owner-not-allowed";

            if (eligibility.AliveState != (short)AliveState.IsAlive)
                return $"aliveState={eligibility.AliveState}";

            if (!eligibility.FlagsAllowed)
                return $"flags92={eligibility.FlagsAt92}";

            if (eligibility.Reservation != 0)
                return $"reservation={eligibility.Reservation}";

            if (eligibility.CorpseFlag != 0 && eligibility.AiState != HunterCorpsePickupAiState)
                return $"corpseFlag={eligibility.CorpseFlag}/aiState=0x{eligibility.AiState:X}";

            return "unknown";
        }

        private static void IncrementAnimalCount(
            eChimps type,
            ref int deer,
            ref int goat,
            ref int rabbit,
            ref int camel,
            ref int chicken,
            ref int cow)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_DEER:
                    deer++;
                    break;
                case eChimps.CHIMP_TYPE_GOAT:
                    goat++;
                    break;
                case eChimps.CHIMP_TYPE_RABBIT:
                    rabbit++;
                    break;
                case eChimps.CHIMP_TYPE_CAMEL:
                    camel++;
                    break;
                case eChimps.CHIMP_TYPE_CHICKEN:
                    chicken++;
                    break;
                case eChimps.CHIMP_TYPE_COW:
                    cow++;
                    break;
            }
        }

        private static int ComparePreyCandidatesByHeuristic(PreyCandidate left, PreyCandidate right)
        {
            long leftScore = (long)left.Prey.MeatAmount * right.HeuristicCycleCost;
            long rightScore = (long)right.Prey.MeatAmount * left.HeuristicCycleCost;
            int scoreCompare = rightScore.CompareTo(leftScore);
            if (scoreCompare != 0)
                return scoreCompare;

            return left.HeuristicCycleCost.CompareTo(right.HeuristicCycleCost);
        }

        private static bool IsBetterTarget(BestTarget candidate, BestTarget currentBest)
        {
            long candidateScore = (long)candidate.MeatAmount * currentBest.CycleCost;
            long currentScore = (long)currentBest.MeatAmount * candidate.CycleCost;
            if (candidateScore != currentScore)
                return candidateScore > currentScore;

            if (candidate.Type == currentBest.Type &&
                candidate.ApproachHeuristicCost != currentBest.ApproachHeuristicCost)
            {
                return candidate.ApproachHeuristicCost < currentBest.ApproachHeuristicCost;
            }

            if (candidate.MeatAmount != currentBest.MeatAmount)
                return candidate.MeatAmount > currentBest.MeatAmount;

            return candidate.UnitId < currentBest.UnitId;
        }

        private static bool IsWithinTargetTolerance(BestTarget candidate, BestTarget currentBest)
        {
            if (candidate.UnitId == currentBest.UnitId)
                return true;

            int candidateMeat = Math.Max(1, candidate.MeatAmount);
            int bestMeat = Math.Max(1, currentBest.MeatAmount);
            long candidateNormalizedCost = (long)candidate.CycleCost * bestMeat;
            long toleratedBestNormalizedCost = (long)(currentBest.CycleCost + BestTargetToleranceCost) * candidateMeat;
            return candidateNormalizedCost <= toleratedBestNormalizedCost;
        }

        private static int GetChebyshevDistance(int ax, int ay, int bx, int by)
        {
            return Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
        }

        private static int GetPreyHandlingCost(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_RABBIT:
                case eChimps.CHIMP_TYPE_CHICKEN:
                    return 80;
                case eChimps.CHIMP_TYPE_CAMEL:
                    return 120;
                default:
                    return DefaultPreyHandlingCost;
            }
        }

        private void CacheTargetSelection(int hunterUnitId, TargetSelection selection, long timestamp)
        {
            bestTargetCache[hunterUnitId] = new CachedBestTarget(selection, timestamp + BestTargetCacheInterval);
        }

        private void TrackHunterSearchQuery(int hunterUnitId, long timestamp)
        {
            if (!IsValidUnitId(hunterUnitId))
                return;

            bool isNewSearch =
                !lastHunterQueryTimestamps.TryGetValue(hunterUnitId, out long lastTimestamp) ||
                timestamp - lastTimestamp > HunterSearchDetectionGap;

            lastHunterQueryTimestamps[hunterUnitId] = timestamp;
            if (!isNewSearch)
                return;

            hunterTargetSearchStarts++;
            if (hunterTargetDiagnosticLogs < MaxHunterTargetDiagnosticLogs)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters target search start: hunter={hunterUnitId}, " +
                    $"searchCount={hunterTargetSearchStarts}.");
            }
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

            args.GoodAmount = settings.GetMeatAmount(preyType);
            args.ReturnValue = args.GoodAmount;
            args.SkipOriginalFunction = true;
        }

        private void OnGranarySpawnChicken(GranarySpawnChickenEventArgs args)
        {
            RemoveExpiredPendingGranaryChickenSpawns(Stopwatch.GetTimestamp());
            if (!IsChickenManagementActive ||
                args.Chimp != eChimps.CHIMP_TYPE_CHICKEN ||
                args.PlayerId < 1 ||
                args.PlayerId > MaximumPlayerId)
            {
                return;
            }

            pendingGranaryChickenSpawns.Push(new PendingGranaryChickenSpawn(
                args.PlayerId,
                args.Chimp,
                args.TileX,
                args.TileY,
                args.HeightElevation,
                Stopwatch.GetTimestamp()));
            LogChickenOwnershipDiagnostic(
                $"Improved Hunters granary chicken spawn captured: player={args.PlayerId}, " +
                $"tile={args.TileX},{args.TileY}, height={args.HeightElevation}, " +
                $"pendingDepth={pendingGranaryChickenSpawns.Count}.");
        }

        private void OnUnitCreate(UnitCreateEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
                OnUnitCreatePre(args);
            else if (args.Phase == EventHookPhase.Post)
                OnUnitCreatePost(args);
        }

        private void OnUnitCreatePre(UnitCreateEventArgs args)
        {
            RemoveExpiredPendingGranaryChickenSpawns(Stopwatch.GetTimestamp());
            if (!IsChickenManagementActive || pendingGranaryChickenSpawns.Count == 0)
                return;

            PendingGranaryChickenSpawn pending = pendingGranaryChickenSpawns.Peek();
            // The Script Extender's granary event exposes native local Y as TileX
            // and local X as TileY; UnitCreate receives those coordinates scaled by 8.
            if (!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(
                    IsChickenManagementActive,
                    pending.UnitCreateMatched,
                    pending.SourcePlayerId,
                    (int)pending.UnitType,
                    pending.GranaryTileX,
                    pending.GranaryTileY,
                    pending.HeightElevation,
                    args.PlayerOwnerId,
                    (int)args.UnitType,
                    args.WorldTileX,
                    args.WorldTileY,
                    args.HeightElevation))
            {
                return;
            }

            pending.UnitCreateMatched = true;
            pending.WorldTileX = args.WorldTileX;
            pending.WorldTileY = args.WorldTileY;
            int previousOwner = args.PlayerOwnerId;
            int previousColor = args.PlayerColorId;
            args.PlayerOwnerId = 0;
            args.PlayerColorId = 0;
            LogChickenOwnershipDiagnostic(
                $"Improved Hunters granary chicken spawn neutralized before creation: " +
                $"sourcePlayer={pending.SourcePlayerId}, owner={previousOwner}->0, color={previousColor}->0, " +
                $"worldTile={args.WorldTileX},{args.WorldTileY}, pendingDepth={pendingGranaryChickenSpawns.Count}.");
        }

        private unsafe void OnUnitCreatePost(UnitCreateEventArgs args)
        {
            if (pendingGranaryChickenSpawns.Count == 0)
                return;

            PendingGranaryChickenSpawn pending = pendingGranaryChickenSpawns.Peek();
            if (!pending.UnitCreateMatched ||
                args.UnitType != pending.UnitType ||
                args.PlayerOwnerId != pending.SourcePlayerId)
            {
                return;
            }

            pendingGranaryChickenSpawns.Pop();
            int unitId = args.ReturnValue > 0 && args.ReturnValue <= int.MaxValue
                ? (int)args.ReturnValue
                : 0;
            GameUnit* chicken = null;
            bool unitResolved = unitId != 0 &&
                GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out chicken) &&
                chicken != null;
            if (!GranaryChickenSpawnPolicy.CanAssignCompletedSpawn(
                    IsChickenManagementActive,
                    args.ReturnValue,
                    unitResolved,
                    unitResolved && chicken->r_UnitChimp == eChimps.CHIMP_TYPE_CHICKEN,
                    unitResolved ? chicken->r_GlobalId : 0,
                    unitResolved && chicken->r_ControllableForPlayerId == 0,
                    unitResolved && chicken->r_SpritePlayerColorId == 0,
                    unitResolved && IsChickenLive(chicken)))
            {
                LogChickenOwnershipDiagnostic(
                    $"Improved Hunters granary chicken spawn was not assigned: sourcePlayer={pending.SourcePlayerId}, " +
                    $"unit={unitId}, returnValue={args.ReturnValue}, " +
                    $"outcome={(IsChickenManagementActive ? "post-spawn-validation-failed" : "safety-guard-inactive")}.",
                    warning: true);
                return;
            }

            TrackGranaryChicken(
                unitId,
                chicken->r_GlobalId,
                pending.SourcePlayerId);
            LogChickenOwnershipDiagnostic(
                $"Improved Hunters granary chicken assigned: player={pending.SourcePlayerId}, unit={unitId}, " +
                $"globalId={chicken->r_GlobalId}, owner=0, color=0, " +
                $"worldTile={pending.WorldTileX},{pending.WorldTileY}.");
        }

        private unsafe void OnProjectileSpawn(ProjectileSpawnEventArgs args)
        {
            if (!TryGetCompensableProjectileTarget(args, out _, out PreyEligibility eligibility))
                return;

            // Hunter arrows do not always report the hunter as SourceUnitId. Use
            // several weak signals and fall back to the target intent if needed.
            bool hasHunterContext = TryResolveHunterForProjectile(
                args.SourceUnitId,
                args.AttackedUnitId,
                eligibility.GlobalId,
                out int hunterUnitId,
                out uint hunterGlobalId,
                out string hunterSource);
            if (!hasHunterContext)
                hunterSource = "animal-arrow-fallback";
            else if (eligibility.Type == eChimps.CHIMP_TYPE_CHICKEN)
            {
                long timestamp = Stopwatch.GetTimestamp();
                hunterLineOfSightRecovery?.RecordProjectileSpawn(hunterUnitId, timestamp);
                hunterVisibilityDiagnostic?.RecordProjectileSpawn(
                    hunterUnitId,
                    args.AttackedUnitId,
                    eligibility.GlobalId,
                    args.ReturnValue,
                    hunterSource);
            }

            uint projectileGlobalId = 0;
            if (args.ReturnValue > 0 &&
                args.ReturnValue <= int.MaxValue &&
                GameProjectileManagerAPI.Instance.TryGetProjectileById((int)args.ReturnValue, out GameProjectile* projectile) &&
                projectile != null &&
                projectile->r_AliveState != AliveState.None &&
                projectile->r_ProjectileType == ProjectileType.ArcherArrow &&
                projectile->r_TargetUnidId == args.AttackedUnitId)
            {
                projectileGlobalId = projectile->r_GlobalId;
            }

            QueuePendingHunterShotIntent(
                hunterUnitId,
                hunterGlobalId,
                args.AttackedUnitId,
                eligibility.GlobalId,
                eligibility.Type,
                hunterSource,
                args.ReturnValue,
                projectileGlobalId);
        }

        private void OnProjectileDelete(ProjectileDeleteEventArgs args)
        {
            try
            {
                TryApplyHunterProjectileDamageOnDelete(args.ProjectileId);
            }
            catch (Exception exception)
            {
                LogHunterProjectileDiagnostic(
                    $"Improved Hunters projectile-delete compensation failed; Vanilla deletion continues: {exception}");
            }
        }

        private unsafe bool TryResolveHunterForProjectile(
            int sourceUnitId,
            int targetUnitId,
            uint targetGlobalId,
            out int hunterUnitId,
            out uint hunterGlobalId,
            out string source)
        {
            hunterUnitId = 0;
            hunterGlobalId = 0;
            source = null;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (IsValidUnitId(sourceUnitId) &&
                unitApi.TryGetUnitById(sourceUnitId, out GameUnit* sourceUnit) &&
                sourceUnit != null &&
                sourceUnit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER)
            {
                hunterUnitId = sourceUnitId;
                hunterGlobalId = sourceUnit->r_GlobalId;
                source = "projectile-source";
                return true;
            }

            if (TryFindHunterTargetingPrey(targetUnitId, targetGlobalId, out hunterUnitId, out hunterGlobalId))
            {
                source = "live-hunter-target";
                return true;
            }

            foreach (KeyValuePair<int, HunterTargetSnapshot> pair in activeHunterTargets)
            {
                if (pair.Value.UnitId != targetUnitId ||
                    pair.Value.GlobalId != targetGlobalId ||
                    !unitApi.TryGetUnitById(pair.Key, out GameUnit* hunter) ||
                    hunter == null ||
                    hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                {
                    continue;
                }

                hunterUnitId = pair.Key;
                hunterGlobalId = hunter->r_GlobalId;
                source = "cached-hunter-target";
                return true;
            }

            return false;
        }

        private unsafe bool TryFindHunterTargetingPrey(
            int targetUnitId,
            uint targetGlobalId,
            out int hunterUnitId,
            out uint hunterGlobalId)
        {
            hunterUnitId = 0;
            hunterGlobalId = 0;

            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            if (units._array == null || units.Length == 0)
                return false;

            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                {
                    continue;
                }

                byte* hunterBytes = (byte*)unit;
                ushort hunterTargetUnitId = *(ushort*)(hunterBytes + 0x39A);
                uint hunterTargetGlobalId = *(uint*)(hunterBytes + 0x39C);
                if (hunterTargetUnitId != targetUnitId ||
                    hunterTargetGlobalId != targetGlobalId)
                {
                    continue;
                }

                hunterUnitId = index + 1;
                hunterGlobalId = unit->r_GlobalId;
                return true;
            }

            return false;
        }

        private unsafe bool TryGetCompensableProjectileTarget(
            ProjectileSpawnEventArgs args,
            out GameUnit* target,
            out PreyEligibility eligibility)
        {
            target = null;
            eligibility = default;

            if (!settings.EnableMod ||
                args.ProjectileType != ProjectileType.ArcherArrow ||
                !IsValidUnitId(args.AttackedUnitId) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(args.AttackedUnitId, out target) ||
                target == null)
            {
                return false;
            }

            return IsCompensableHunterPrey(args.AttackedUnitId, target, out eligibility);
        }

        private unsafe void QueuePendingHunterShotIntent(
            int hunterUnitId,
            uint hunterGlobalId,
            int targetUnitId,
            uint targetGlobalId,
            eChimps targetType,
            string hunterSource,
            long spawnReturnValue,
            uint projectileGlobalId)
        {
            long timestamp = Stopwatch.GetTimestamp();
            ushort projectileX = 0;
            ushort projectileY = 0;
            if (spawnReturnValue > 0 &&
                spawnReturnValue <= int.MaxValue &&
                GameProjectileManagerAPI.Instance.TryGetProjectileById((int)spawnReturnValue, out GameProjectile* projectile) &&
                projectile != null &&
                projectile->r_GlobalId == projectileGlobalId)
            {
                projectileX = projectile->r_CurrentTileX;
                projectileY = projectile->r_CurrentTileY;
            }

            HunterShotIntentKey key = new HunterShotIntentKey(
                targetUnitId,
                targetGlobalId,
                projectileGlobalId,
                spawnReturnValue);
            PendingHunterShotIntent intent = new PendingHunterShotIntent(
                hunterUnitId,
                hunterGlobalId,
                targetUnitId,
                targetGlobalId,
                targetType,
                timestamp,
                timestamp + HunterProjectileIntentLifetime,
                hunterSource,
                spawnReturnValue,
                projectileGlobalId,
                projectileX,
                projectileY,
                timestamp);

            bool updatedExisting = pendingHunterShotIntents.ContainsKey(key);
            pendingHunterShotIntents[key] = intent;

            LogHunterProjectileDiagnostic(
                $"Improved Hunters hunter shot intent queued: hunter={hunterUnitId}, target={targetUnitId}/{targetType}, " +
                $"targetGlobalId={targetGlobalId}, lifetimeSeconds={HunterProjectileIntentLifetime / Stopwatch.Frequency}, " +
                $"hunterSource={hunterSource}, projectile={spawnReturnValue}/{projectileGlobalId}, updated={updatedExisting}.");
        }

        private unsafe void TryApplyHunterProjectileDamageDuringFlight(long timestamp)
        {
            if (!settings.EnableMod || pendingHunterShotIntents.Count == 0)
                return;

            List<HunterShotIntentKey> keys = new List<HunterShotIntentKey>();
            foreach (KeyValuePair<HunterShotIntentKey, PendingHunterShotIntent> pair in pendingHunterShotIntents)
                keys.Add(pair.Key);

            for (int index = 0; index < keys.Count; index++)
            {
                HunterShotIntentKey key = keys[index];
                if (!pendingHunterShotIntents.TryGetValue(key, out PendingHunterShotIntent intent))
                    continue;

                if (!TryGetMatchingProjectile(intent, out GameProjectile* projectile) ||
                    projectile->r_AliveState != AliveState.IsAlive)
                {
                    continue;
                }

                if (projectile->r_CurrentTileX != intent.LastProjectileX ||
                    projectile->r_CurrentTileY != intent.LastProjectileY)
                {
                    intent = intent.WithProjectileObservation(
                        projectile->r_CurrentTileX,
                        projectile->r_CurrentTileY,
                        timestamp);
                    pendingHunterShotIntents[key] = intent;
                }

                if (intent.ActiveDamageAttempts >= MaxHunterProjectileDamageAttempts ||
                    timestamp < intent.NextDamageAttemptAt ||
                    timestamp - intent.CreatedAt < HunterProjectileMinimumFlightTime)
                {
                    continue;
                }

                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                if (!unitApi.TryGetUnitById(intent.TargetUnitId, out GameUnit* target) ||
                    target == null ||
                    target->r_GlobalId != intent.TargetGlobalId ||
                    target->r_CurrentHealth == 0)
                {
                    pendingHunterShotIntents.Remove(key);
                    continue;
                }

                int distanceToTarget = Math.Max(
                    Math.Abs((int)projectile->r_CurrentTileX - target->r_CurrentWorldPositionX),
                    Math.Abs((int)projectile->r_CurrentTileY - target->r_CurrentWorldPositionY));
                bool nearTarget = distanceToTarget <= HunterProjectileNearTargetDistance;
                bool stalled = timestamp - intent.LastProjectileMovementAt >= HunterProjectileStallInterval;
                if (!nearTarget && !stalled)
                    continue;

                TryApplyHunterProjectileDamage(
                    key,
                    intent,
                    nearTarget ? "active-near-target" : "active-stalled",
                    timestamp,
                    allowRetry: true);
            }
        }

        private void RunHunterProjectileCompensation(long timestamp)
        {
            try
            {
                TryApplyHunterProjectileDamageDuringFlight(timestamp);
            }
            catch (Exception exception)
            {
                if (!hunterProjectileCompensationFailureLogged)
                {
                    hunterProjectileCompensationFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Improved Hunters active-flight ranged compensation failed safely; " +
                        $"the native scan and Vanilla continue: {exception}");
                }
            }

            try
            {
                ResolvePendingHunterShotIntents(timestamp);
            }
            catch (Exception exception)
            {
                if (!hunterProjectileCleanupFailureLogged)
                {
                    hunterProjectileCleanupFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Improved Hunters projectile-intent cleanup failed safely; " +
                        $"the native scan and Vanilla continue: {exception}");
                }
            }
        }

        private unsafe void TryApplyHunterProjectileDamageOnDelete(long projectileId)
        {
            if (!settings.EnableMod || projectileId <= 0 || pendingHunterShotIntents.Count == 0)
                return;

            HunterShotIntentKey matchedKey = default;
            PendingHunterShotIntent matchedIntent = default;
            bool found = false;
            foreach (KeyValuePair<HunterShotIntentKey, PendingHunterShotIntent> pair in pendingHunterShotIntents)
            {
                if (pair.Value.SpawnReturnValue != projectileId)
                    continue;

                matchedKey = pair.Key;
                matchedIntent = pair.Value;
                found = true;
                break;
            }

            if (!found)
                return;

            TryApplyHunterProjectileDamage(
                matchedKey,
                matchedIntent,
                "projectile-delete",
                Stopwatch.GetTimestamp(),
                allowRetry: false);
        }

        private unsafe void TryApplyHunterProjectileDamage(
            HunterShotIntentKey key,
            PendingHunterShotIntent intent,
            string trigger,
            long timestamp,
            bool allowRetry)
        {
            pendingHunterShotIntents.Remove(key);
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!unitApi.TryGetUnitById(intent.TargetUnitId, out GameUnit* target) ||
                target == null ||
                target->r_GlobalId != intent.TargetGlobalId ||
                target->r_AliveState != AliveState.IsAlive ||
                target->r_CurrentHealth == 0)
            {
                // The native hit already completed, or the slot was reused.
                return;
            }

            if (intent.HunterUnitId <= 0 ||
                !unitApi.TryGetUnitById(intent.HunterUnitId, out GameUnit* hunter) ||
                hunter == null ||
                hunter->r_GlobalId != intent.HunterGlobalId ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                hunter->r_AliveState != AliveState.IsAlive ||
                !IsCompensableHunterPrey(intent.TargetUnitId, target, out PreyEligibility eligibility) ||
                !TryGetMatchingProjectile(intent, out GameProjectile* projectile) ||
                projectile->r_PlayerSourceId != hunter->r_ControllableForPlayerId)
            {
                LogHunterProjectileDiagnostic(
                    $"Improved Hunters ranged compensation skipped: trigger={trigger}, hunter={intent.HunterUnitId}/{intent.HunterGlobalId}, " +
                    $"target={intent.TargetUnitId}/{intent.TargetGlobalId}/{intent.TargetType}, " +
                    $"projectile={intent.SpawnReturnValue}/{intent.ProjectileGlobalId}, reason=identity-or-state-validation-failed.");
                return;
            }

            // Remove before entering native code: ranged damage may synchronously
            // dispatch projectile deletion and must never re-enter this intent.
            int attempt = intent.ActiveDamageAttempts + 1;
            short projectileAliveState = (short)projectile->r_AliveState;
            ushort projectileX = projectile->r_CurrentTileX;
            ushort projectileY = projectile->r_CurrentTileY;
            bool damageApplied = unitApi.DamageUnitRanged(
                intent.TargetUnitId,
                (int)intent.SpawnReturnValue);
            bool targetIdentityValidAfter =
                unitApi.TryGetUnitById(intent.TargetUnitId, out GameUnit* targetAfter) &&
                targetAfter != null &&
                targetAfter->r_GlobalId == intent.TargetGlobalId;
            uint currentHealth = targetIdentityValidAfter ? targetAfter->r_CurrentHealth : 0;
            ushort aiState = targetIdentityValidAfter ? *(ushort*)((byte*)targetAfter + 0x2BC) : (ushort)0;
            ushort corpseFlag = targetIdentityValidAfter ? *(ushort*)((byte*)targetAfter + 0x29C) : (ushort)0;
            ushort reservation = targetIdentityValidAfter ? *(ushort*)((byte*)targetAfter + 0x448) : (ushort)0;
            bool targetKilled = targetIdentityValidAfter && currentHealth == 0;

            LogHunterProjectileDiagnostic(
                $"Improved Hunters Vanilla ranged compensation: trigger={trigger}, " +
                $"hunter={intent.HunterUnitId}/{intent.HunterGlobalId}, " +
                $"target={intent.TargetUnitId}/{intent.TargetGlobalId}/{eligibility.Type}, " +
                $"projectile={intent.SpawnReturnValue}/{intent.ProjectileGlobalId}, " +
                $"projectileAliveState={projectileAliveState}, projectilePosition={projectileX},{projectileY}, " +
                $"attempt={attempt}/{MaxHunterProjectileDamageAttempts}, damageApplied={damageApplied}, " +
                $"targetIdentityValidAfter={targetIdentityValidAfter}, targetKilled={targetKilled}, " +
                $"currentHealth={currentHealth}, aiState=0x{aiState:X}, corpseFlag={corpseFlag}, reservation={reservation}.");

            if (!targetKilled &&
                allowRetry &&
                attempt < MaxHunterProjectileDamageAttempts &&
                timestamp < intent.ExpiresAt &&
                TryGetMatchingProjectile(intent, out projectile) &&
                projectile->r_AliveState == AliveState.IsAlive)
            {
                pendingHunterShotIntents[key] = intent.WithDamageAttempt(
                    attempt,
                    timestamp + HunterProjectileRetryInterval);
            }
        }

        private unsafe bool TryGetMatchingProjectile(
            PendingHunterShotIntent intent,
            out GameProjectile* projectile)
        {
            projectile = null;
            if (intent.SpawnReturnValue <= 0 ||
                intent.SpawnReturnValue > int.MaxValue ||
                intent.ProjectileGlobalId == 0 ||
                intent.HunterUnitId <= 0)
            {
                return false;
            }

            return GameProjectileManagerAPI.Instance.TryGetProjectileById(
                    (int)intent.SpawnReturnValue,
                    out projectile) &&
                projectile != null &&
                projectile->r_AliveState != AliveState.None &&
                projectile->r_GlobalId == intent.ProjectileGlobalId &&
                projectile->r_ProjectileType == ProjectileType.ArcherArrow &&
                projectile->r_SourceUnitId == intent.HunterUnitId &&
                projectile->r_TargetUnidId == intent.TargetUnitId;
        }

        private unsafe void ResolvePendingHunterShotIntents(long timestamp)
        {
            if (pendingHunterShotIntents.Count == 0)
                return;

            List<HunterShotIntentKey> expiredKeys = null;
            foreach (KeyValuePair<HunterShotIntentKey, PendingHunterShotIntent> pair in pendingHunterShotIntents)
            {
                if (timestamp < pair.Value.ExpiresAt)
                    continue;

                if (expiredKeys == null)
                    expiredKeys = new List<HunterShotIntentKey>();

                expiredKeys.Add(pair.Key);
            }

            if (expiredKeys == null)
                return;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            for (int index = 0; index < expiredKeys.Count; index++)
            {
                HunterShotIntentKey key = expiredKeys[index];
                if (!pendingHunterShotIntents.TryGetValue(key, out PendingHunterShotIntent intent))
                    continue;

                pendingHunterShotIntents.Remove(key);
                bool targetStillAlive =
                    unitApi.TryGetUnitById(intent.TargetUnitId, out GameUnit* target) &&
                    target != null &&
                    target->r_GlobalId == intent.TargetGlobalId &&
                    target->r_AliveState == AliveState.IsAlive &&
                    target->r_CurrentHealth > 0;
                LogHunterProjectileDiagnostic(
                    $"Improved Hunters projectile intent expired without synthetic KillUnit: " +
                    $"hunter={intent.HunterUnitId}/{intent.HunterGlobalId}, " +
                    $"target={intent.TargetUnitId}/{intent.TargetGlobalId}/{intent.TargetType}, " +
                    $"projectile={intent.SpawnReturnValue}/{intent.ProjectileGlobalId}, " +
                    $"attempts={intent.ActiveDamageAttempts}, targetStillAlive={targetStillAlive}.");
            }
        }

        private void LogHunterProjectileDiagnostic(string message)
        {
            if (hunterProjectileDiagnosticLogs >= MaxHunterProjectileDiagnosticLogs)
                return;

            hunterProjectileDiagnosticLogs++;
            log.LogInfo($"{message} ({hunterProjectileDiagnosticLogs}/{MaxHunterProjectileDiagnosticLogs}).");

            if (hunterProjectileDiagnosticLogs == MaxHunterProjectileDiagnosticLogs)
                log.LogInfo("Improved Hunters hunter projectile diagnostic limit reached.");
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(ImprovedHuntersViewModel.EnableMod) && !settings.EnableMod)
                pendingHunterShotIntents.Clear();

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
                propertyName == nameof(ImprovedHuntersViewModel.HuntChicken) ||
                propertyName == nameof(ImprovedHuntersViewModel.ImprovedPathfinding))
            {
                hunterLineOfSightRecovery?.ResetForMap();
                hunterPclReachability?.ResetForMap();
                hunterPclReachabilityDiagnostic?.ResetForMap();
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

        private void InitializeRabbitDespawnPatch()
        {
            if (rabbitDespawnTicksInitialized)
                return;

            rabbitDespawnTickTime = GameGlobalsManager.Instance.RabbitDespawnTickTime;
            originalRabbitDespawnTicks = rabbitDespawnTickTime.GetValue();
            rabbitDespawnTicksInitialized = true;
        }

        private void InitializeExtraDespawnPatches(ReadOnlySpan<byte> memory, ulong imageBase)
        {
            if (extraDespawnTicksInitialized)
                return;

            camelDespawnTickTime = FindExtraDespawnImmediate(
                memory,
                imageBase,
                "camel despawn immediate",
                CamelDespawnTickTimePattern,
                CamelDespawnTickTimeRva);
            chickenDespawnTickTime = FindExtraDespawnImmediate(
                memory,
                imageBase,
                "chicken despawn immediate",
                ChickenDespawnTickTimePattern,
                ChickenDespawnTickTimeRva);

            if (camelDespawnTickTime != null)
                originalCamelDespawnTicks = camelDespawnTickTime.GetValue();

            if (chickenDespawnTickTime != null)
                originalChickenDespawnTicks = chickenDespawnTickTime.GetValue();

            extraDespawnTicksInitialized = true;
        }

        private ManagedAssemblyImmediate<short> FindExtraDespawnImmediate(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            string name,
            string pattern,
            int referenceRva)
        {
            try
            {
                int offset = Shared.NativePatternResolver.ResolveUnique(
                    memory,
                    pattern,
                    referenceRva,
                    referenceHashMatches,
                    name,
                    log).Rva;

                return new ManagedAssemblyImmediate<short>(
                    new IntPtr(unchecked((long)(imageBase + (ulong)offset + ExtraDespawnPatternImmediateOffset))),
                    // The matched instruction has more than one operand; operand 1
                    // is the immediate despawn threshold.
                    operand: 1);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters failed to initialize {name}; this native patch remains inactive: {exception}");
                return null;
            }
        }

        private void ApplyDespawnPatches()
        {
            try
            {
                if (rabbitDespawnTickTime != null)
                {
                    short desired = settings.EnableMod && settings.HuntRabbit
                        ? RabbitCorpseDespawnTicks
                        : originalRabbitDespawnTicks;

                    if (rabbitDespawnTickTime.GetValue() != desired)
                        rabbitDespawnTickTime.SetValue(desired);

                    rabbitDespawnTicksPatched = desired != originalRabbitDespawnTicks;
                }

                ApplyExtraDespawnPatch(camelDespawnTickTime, originalCamelDespawnTicks, settings.EnableMod && settings.HuntCamel, ref camelDespawnTicksPatched);
                ApplyExtraDespawnPatch(chickenDespawnTickTime, originalChickenDespawnTicks, settings.EnableMod && settings.HuntChicken, ref chickenDespawnTicksPatched);
                LogDespawnPatchState();
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters failed to apply an animal despawn patch; the affected patch remains inactive: {exception}");
            }
        }

        private void LogDespawnPatchState()
        {
            if (despawnPatchStateLogged)
                return;

            despawnPatchStateLogged = true;
            log.LogInfo(
                $"Improved Hunters despawn patch state: " +
                $"rabbit={FormatDespawnPatchState(rabbitDespawnTickTime, originalRabbitDespawnTicks, settings.EnableMod && settings.HuntRabbit ? RabbitCorpseDespawnTicks : originalRabbitDespawnTicks, rabbitDespawnTicksPatched)}, " +
                $"camel={FormatDespawnPatchState(camelDespawnTickTime, originalCamelDespawnTicks, settings.EnableMod && settings.HuntCamel ? ExtraCorpseDespawnTicks : originalCamelDespawnTicks, camelDespawnTicksPatched)}, " +
                $"chicken={FormatDespawnPatchState(chickenDespawnTickTime, originalChickenDespawnTicks, settings.EnableMod && settings.HuntChicken ? ExtraCorpseDespawnTicks : originalChickenDespawnTicks, chickenDespawnTicksPatched)}.");
        }

        private static string FormatDespawnPatchState(
            ManagedAssemblyImmediate<short> immediate,
            short originalTicks,
            short desiredTicks,
            bool patched)
        {
            if (immediate == null)
                return "missing";

            return $"original={originalTicks}/desired={desiredTicks}/current={immediate.GetValue()}/patched={patched}";
        }

        private static void ApplyExtraDespawnPatch(
            ManagedAssemblyImmediate<short> immediate,
            short originalTicks,
            bool enabled,
            ref bool patched)
        {
            if (immediate == null)
                return;

            short desired = enabled ? ExtraCorpseDespawnTicks : originalTicks;
            if (immediate.GetValue() != desired)
                immediate.SetValue(desired);

            patched = desired != originalTicks;
        }

        private void ApplyCamelHealthPatch()
        {
            try
            {
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                if (!camelHealthInitialized)
                {
                    originalCamelArrowDamage = unitApi.GetRangedArrowDamageTo(eChimps.CHIMP_TYPE_CAMEL);
                    originalCamelHealth = unitApi.GetDefaultHealth(eChimps.CHIMP_TYPE_CAMEL);
                    camelHealthInitialized = true;
                }

                uint desired = originalCamelHealth;
                if (settings.EnableMod && settings.HuntCamel)
                {
                    uint oneShotHealth = (uint)Math.Max(1, originalCamelArrowDamage - 1);
                    desired = Math.Min(originalCamelHealth, oneShotHealth);
                }

                if (unitApi.GetDefaultHealth(eChimps.CHIMP_TYPE_CAMEL) != desired)
                    unitApi.SetDefaultHealth(eChimps.CHIMP_TYPE_CAMEL, desired);

                desiredCamelHealth = desired;
                camelHealthPatched = desired != originalCamelHealth;
                LogCamelHealthPatch(0);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters failed to apply the camel health patch; that patch remains inactive: {exception}");
            }
        }

        private unsafe bool TryClampLiveCamelHealth(int unitId, GameUnit* unit)
        {
            if (!settings.EnableMod ||
                !settings.HuntCamel ||
                !camelHealthInitialized ||
                desiredCamelHealth == 0 ||
                unit == null ||
                unit->r_UnitChimp != eChimps.CHIMP_TYPE_CAMEL ||
                unit->r_AliveState != AliveState.IsAlive)
            {
                return false;
            }

            bool changed = false;
            if (unit->r_MaxHealth > desiredCamelHealth)
            {
                unit->r_MaxHealth = desiredCamelHealth;
                changed = true;
            }

            if (unit->r_CurrentHealth > desiredCamelHealth)
            {
                unit->r_CurrentHealth = desiredCamelHealth;
                changed = true;
            }

            if (changed)
                UpdateUnitHealthDisplay(unit);

            return changed;
        }

        private unsafe void LogCamelHealthPatch(int adjustedLiveCamels)
        {
            if (!camelHealthInitialized)
                return;

            if (adjustedLiveCamels <= 0 && lastLoggedDesiredCamelHealth == desiredCamelHealth)
                return;

            lastLoggedDesiredCamelHealth = desiredCamelHealth;
            log.LogInfo(
                $"Improved Hunters camel health patch: originalHealth={originalCamelHealth}, desiredHealth={desiredCamelHealth}, " +
                $"originalArrowDamage={originalCamelArrowDamage}, enabled={settings.EnableMod && settings.HuntCamel}, " +
                $"adjustedLiveCamels={adjustedLiveCamels}.");
        }

        private bool IsChickenManagementActive =>
            settings.EnableMod &&
            settings.HuntChicken &&
            automaticChickenTargetPatch?.IsApplied == true &&
            granaryChickenLimitPatch?.IsAvailable == true;

        private unsafe int GetLiveTrackedGranaryChickenCount(int playerId)
        {
            CleanupTrackedGranaryChickens();
            return playerId >= 1 && playerId <= MaximumPlayerId
                ? trackedGranaryChickenCounts[playerId]
                : 0;
        }

        private unsafe void CleanupTrackedGranaryChickens()
        {
            long timestamp = Stopwatch.GetTimestamp();
            if (timestamp < nextGranaryChickenCleanupTimestamp)
                return;

            nextGranaryChickenCleanupTimestamp = timestamp + GranaryChickenCleanupInterval;
            staleGranaryChickenUnitIds.Clear();
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            foreach (KeyValuePair<int, TrackedGranaryChicken> pair in trackedGranaryChickens)
            {
                TrackedGranaryChicken tracked = pair.Value;
                if (!unitApi.TryGetUnitById(tracked.UnitId, out GameUnit* chicken) ||
                    chicken == null ||
                    !GranaryChickenSpawnPolicy.IsTrackedIdentityValid(
                        tracked.GlobalId,
                        chicken->r_GlobalId,
                        chicken->r_UnitChimp == eChimps.CHIMP_TYPE_CHICKEN,
                        IsChickenLive(chicken)))
                {
                    staleGranaryChickenUnitIds.Add(pair.Key);
                }
            }

            foreach (int unitId in staleGranaryChickenUnitIds)
                RemoveTrackedGranaryChicken(unitId);
        }

        private void TrackGranaryChicken(int unitId, uint globalId, int sourcePlayerId)
        {
            if (sourcePlayerId < 1 || sourcePlayerId > MaximumPlayerId || globalId == 0)
                return;

            RemoveTrackedGranaryChicken(unitId);
            trackedGranaryChickens[unitId] = new TrackedGranaryChicken(unitId, globalId, sourcePlayerId);
            trackedGranaryChickenCounts[sourcePlayerId]++;
        }

        private void RemoveTrackedGranaryChicken(int unitId)
        {
            if (!trackedGranaryChickens.TryGetValue(unitId, out TrackedGranaryChicken tracked))
                return;

            trackedGranaryChickens.Remove(unitId);
            if (tracked.SourcePlayerId >= 1 &&
                tracked.SourcePlayerId <= MaximumPlayerId &&
                trackedGranaryChickenCounts[tracked.SourcePlayerId] > 0)
            {
                trackedGranaryChickenCounts[tracked.SourcePlayerId]--;
            }
        }

        private void ClearTrackedGranaryChickens()
        {
            trackedGranaryChickens.Clear();
            Array.Clear(trackedGranaryChickenCounts, 0, trackedGranaryChickenCounts.Length);
            nextGranaryChickenCleanupTimestamp = 0;
        }

        private unsafe void TryReconstructLoadedGranaryChickens(SimpleNativeArray<GameUnit> units)
        {
            if (!loadedChickenReconstructionPending || !IsChickenManagementActive)
                return;

            List<ChickenGranaryCandidate> granaries = GetActiveChickenGranaries();
            int eligible = 0;
            int alreadyTracked = 0;
            int assigned = 0;
            int neutralized = 0;
            int unresolved = 0;

            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* chicken = units.GetValuePointer(index);
                if (chicken == null ||
                    chicken->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN ||
                    chicken->r_GlobalId == 0 ||
                    !IsChickenLive(chicken))
                {
                    continue;
                }

                eligible++;
                int unitId = index + 1;
                if (trackedGranaryChickens.ContainsKey(unitId))
                {
                    alreadyTracked++;
                    continue;
                }

                int sourcePlayerId = chicken->r_ControllableForPlayerId;
                if (sourcePlayerId < 1 || sourcePlayerId > MaximumPlayerId)
                {
                    if (!TryFindNearestGranaryOwner(
                            granaries,
                            chicken->r_CurrentTilePositionX,
                            chicken->r_CurrentTilePositionY,
                            out sourcePlayerId))
                    {
                        unresolved++;
                        continue;
                    }
                }

                byte previousOwner = chicken->r_ControllableForPlayerId;
                uint previousColor = chicken->r_SpritePlayerColorId;
                if (previousOwner != 0 || previousColor != 0)
                {
                    chicken->r_ControllableForPlayerId = 0;
                    chicken->r_SpritePlayerColorId = 0;
                    neutralized++;
                }

                TrackGranaryChicken(
                    unitId,
                    chicken->r_GlobalId,
                    sourcePlayerId);
                assigned++;
            }

            loadedChickenReconstructionPending = unresolved > 0;
            if (eligible > 0 || granaries.Count > 0)
            {
                LogChickenOwnershipDiagnostic(
                    $"Improved Hunters loaded chicken reconstruction: eligible={eligible}, " +
                    $"alreadyTracked={alreadyTracked}, assigned={assigned}, unresolved={unresolved}, " +
                    $"neutralized={neutralized}, activeGranaries={granaries.Count}, " +
                    $"invariant={eligible == alreadyTracked + assigned + unresolved}, " +
                    $"retryPending={loadedChickenReconstructionPending}.",
                    warning: eligible != alreadyTracked + assigned + unresolved);
            }
        }

        private unsafe List<ChickenGranaryCandidate> GetActiveChickenGranaries()
        {
            List<ChickenGranaryCandidate> granaries = new List<ChickenGranaryCandidate>();
            SimpleNativeArray<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsArray();
            if (buildings._array == null || buildings.Length == 0)
                return granaries;

            for (int index = 0; index < buildings.Length; index++)
            {
                GameBuilding* building = buildings.GetValuePointer(index);
                int playerId = building->r_PlayerIdOwner;
                if (building->r_AliveState != AliveState.IsAlive ||
                    building->r_BuildingType != eStructs.STRUCT_GRANARY ||
                    playerId < 1 ||
                    playerId > MaximumPlayerId)
                {
                    continue;
                }

                granaries.Add(new ChickenGranaryCandidate(
                    index + 1,
                    playerId,
                    building->r_TilePositionXBegin,
                    building->r_TilePositionYBegin));
            }

            return granaries;
        }

        private static bool TryFindNearestGranaryOwner(
            List<ChickenGranaryCandidate> granaries,
            int chickenTileX,
            int chickenTileY,
            out int playerId)
        {
            playerId = 0;
            int bestDistance = int.MaxValue;
            int bestBuildingId = int.MaxValue;
            int bestPlayerId = int.MaxValue;
            foreach (ChickenGranaryCandidate granary in granaries)
            {
                int distance = GranaryChickenSpawnPolicy.ChebyshevDistance(
                    chickenTileX,
                    chickenTileY,
                    granary.TileX,
                    granary.TileY);
                if (!GranaryChickenSpawnPolicy.IsBetterGranaryCandidate(
                        distance,
                        granary.BuildingId,
                        granary.PlayerId,
                        bestDistance,
                        bestBuildingId,
                        bestPlayerId))
                {
                    continue;
                }

                bestDistance = distance;
                bestBuildingId = granary.BuildingId;
                bestPlayerId = granary.PlayerId;
                playerId = granary.PlayerId;
            }

            return playerId != 0;
        }

        private static unsafe bool IsChickenLive(GameUnit* chicken)
        {
            if (chicken == null)
                return false;
            if (chicken->r_AliveState == AliveState.NeedsInit)
                return true;
            if (chicken->r_AliveState != AliveState.IsAlive || chicken->r_CurrentHealth <= 0)
                return false;

            return *(ushort*)((byte*)chicken + 0x29C) == 0;
        }

        private void RemoveExpiredPendingGranaryChickenSpawns(long timestamp)
        {
            while (pendingGranaryChickenSpawns.Count > 0)
            {
                PendingGranaryChickenSpawn pending = pendingGranaryChickenSpawns.Peek();
                if (timestamp - pending.CreatedAt <= PendingGranaryChickenSpawnTimeout)
                    return;

                pendingGranaryChickenSpawns.Pop();
                LogChickenOwnershipDiagnostic(
                    $"Improved Hunters granary chicken spawn tracking expired: " +
                    $"sourcePlayer={pending.SourcePlayerId}, matched={pending.UnitCreateMatched}, " +
                    $"granaryTile={pending.GranaryTileX},{pending.GranaryTileY}, height={pending.HeightElevation}, " +
                    $"pendingDepth={pendingGranaryChickenSpawns.Count}.",
                    warning: true);
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
                    CanRunHunterTargetSearchFallback,
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

        private void InitializeHunterPclReachabilityDiagnostic(bool referenceHashMatches)
        {
            try
            {
                hunterPclReachabilityDiagnostic = new HunterPclReachabilityDiagnostic(
                    log,
                    referenceHashMatches,
                    CanRunHunterTargetSearchFallback);
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
                    CanRunHunterTargetSearchFallback);
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
            hunterPclReachabilityDiagnostic?.RecordMoveHereResult(
                hunterUnitId,
                preyUnitId,
                preyGlobalId,
                preyType,
                moveHereResult,
                timestamp);
        }

        private bool CanRunHunterTargetSearchFallback()
        {
            return settings.EnableMod &&
                settings.ImprovedPathfinding &&
                targetSearchFallbackSingleplayerAllowed;
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
            bool requestedEnabled = CanRunHunterTargetSearchFallback();
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
                        hunterNativeVisibilityProbe,
                        memory,
                        imageBase,
                        referenceHashMatches,
                        CanRunHunterTargetSearchFallback);
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
                        CanRunHunterTargetSearchFallback);
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
            return type != eChimps.CHIMP_TYPE_COW &&
                settings.IsHuntingEnabled(type);
        }

        private unsafe bool IsCompensableHunterPrey(int unitId, GameUnit* prey, out PreyEligibility eligibility)
        {
            return TryGetPreyEligibility(unitId, prey, out eligibility) &&
                eligibility.KnownAnimal &&
                eligibility.RuntimeHuntingEnabled &&
                eligibility.OwnerAllowed &&
                eligibility.AliveState == (short)AliveState.IsAlive &&
                eligibility.FlagsAllowed &&
                (eligibility.Reservation == 0 || eligibility.Reservation == 2) &&
                eligibility.CorpseFlag == 0;
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
            pendingHunterShotIntents.Clear();
            nextPreyCacheRefreshTimestamp = 0;
            nextStaleReservationCleanupTimestamp = 0;
            nextHunterTargetSummaryTimestamp = 0;
            nextShortLivedCorpsePreserveCleanupTimestamp = 0;
            lastLoggedDesiredCamelHealth = 0;
            despawnPatchStateLogged = false;
            hunterTargetDiagnosticLogs = 0;
            preyCacheDiagnosticLogs = 0;
            hunterProjectileDiagnosticLogs = 0;
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

        private struct HunterShotIntentKey : IEquatable<HunterShotIntentKey>
        {
            private readonly int targetUnitId;
            private readonly uint targetGlobalId;
            private readonly uint projectileGlobalId;
            private readonly long projectileId;

            public HunterShotIntentKey(
                int targetUnitId,
                uint targetGlobalId,
                uint projectileGlobalId,
                long projectileId)
            {
                this.targetUnitId = targetUnitId;
                this.targetGlobalId = targetGlobalId;
                this.projectileGlobalId = projectileGlobalId;
                this.projectileId = projectileId;
            }

            public bool Equals(HunterShotIntentKey other)
            {
                return targetUnitId == other.targetUnitId &&
                    targetGlobalId == other.targetGlobalId &&
                    projectileGlobalId == other.projectileGlobalId &&
                    projectileId == other.projectileId;
            }

            public override bool Equals(object obj)
            {
                return obj is HunterShotIntentKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + targetUnitId;
                    hash = hash * 31 + targetGlobalId.GetHashCode();
                    hash = hash * 31 + projectileGlobalId.GetHashCode();
                    hash = hash * 31 + projectileId.GetHashCode();
                    return hash;
                }
            }
        }

        private struct PendingHunterShotIntent
        {
            public readonly int HunterUnitId;
            public readonly uint HunterGlobalId;
            public readonly int TargetUnitId;
            public readonly uint TargetGlobalId;
            public readonly eChimps TargetType;
            public readonly long CreatedAt;
            public readonly long ExpiresAt;
            public readonly string HunterSource;
            public readonly long SpawnReturnValue;
            public readonly uint ProjectileGlobalId;
            public readonly ushort LastProjectileX;
            public readonly ushort LastProjectileY;
            public readonly long LastProjectileMovementAt;
            public readonly long NextDamageAttemptAt;
            public readonly int ActiveDamageAttempts;

            public PendingHunterShotIntent(
                int hunterUnitId,
                uint hunterGlobalId,
                int targetUnitId,
                uint targetGlobalId,
                eChimps targetType,
                long createdAt,
                long expiresAt,
                string hunterSource,
                long spawnReturnValue,
                uint projectileGlobalId,
                ushort lastProjectileX,
                ushort lastProjectileY,
                long lastProjectileMovementAt,
                long nextDamageAttemptAt = 0,
                int activeDamageAttempts = 0)
            {
                HunterUnitId = hunterUnitId;
                HunterGlobalId = hunterGlobalId;
                TargetUnitId = targetUnitId;
                TargetGlobalId = targetGlobalId;
                TargetType = targetType;
                CreatedAt = createdAt;
                ExpiresAt = expiresAt;
                HunterSource = hunterSource;
                SpawnReturnValue = spawnReturnValue;
                ProjectileGlobalId = projectileGlobalId;
                LastProjectileX = lastProjectileX;
                LastProjectileY = lastProjectileY;
                LastProjectileMovementAt = lastProjectileMovementAt;
                NextDamageAttemptAt = nextDamageAttemptAt;
                ActiveDamageAttempts = activeDamageAttempts;
            }

            public PendingHunterShotIntent WithProjectileObservation(ushort x, ushort y, long timestamp)
            {
                return new PendingHunterShotIntent(
                    HunterUnitId,
                    HunterGlobalId,
                    TargetUnitId,
                    TargetGlobalId,
                    TargetType,
                    CreatedAt,
                    ExpiresAt,
                    HunterSource,
                    SpawnReturnValue,
                    ProjectileGlobalId,
                    x,
                    y,
                    timestamp,
                    NextDamageAttemptAt,
                    ActiveDamageAttempts);
            }

            public PendingHunterShotIntent WithDamageAttempt(int attempts, long nextAttemptAt)
            {
                return new PendingHunterShotIntent(
                    HunterUnitId,
                    HunterGlobalId,
                    TargetUnitId,
                    TargetGlobalId,
                    TargetType,
                    CreatedAt,
                    ExpiresAt,
                    HunterSource,
                    SpawnReturnValue,
                    ProjectileGlobalId,
                    LastProjectileX,
                    LastProjectileY,
                    LastProjectileMovementAt,
                    nextAttemptAt,
                    attempts);
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

        public void Dispose()
        {
            settings.SettingChanged -= OnSettingChanged;

            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            hunterPreyTypes.Clear();
            nextIdleHunterRequeryTimestamps.Clear();
            loggedCollectedCorpseGlobalIds.Clear();
            pendingHunterShotIntents.Clear();
            ClearTrackedGranaryChickens();
            pendingGranaryChickenSpawns.Clear();
            staleGranaryChickenUnitIds.Clear();
            loadedChickenReconstructionPending = false;
            ClearTargetSelectionCaches();
            nativeScanFailureLogged = false;
            hunterProjectileCompensationFailureLogged = false;
            hunterProjectileCleanupFailureLogged = false;
            nextNativeScanTimestamp = 0;

            hunterVisibilityDiagnostic?.Dispose();
            hunterVisibilityDiagnostic = null;
            hunterTargetSearchFallbackDiagnostic?.Dispose();
            hunterTargetSearchFallbackDiagnostic = null;
            hunterPclReachabilityDiagnostic?.Dispose();
            hunterPclReachabilityDiagnostic = null;
            hunterPclReachability?.Dispose();
            hunterPclReachability = null;
            hunterRemainingPathSpeedRecovery?.Dispose();
            hunterRemainingPathSpeedRecovery = null;
            hunterVanillaPathContinuationDiagnostic?.Dispose();
            hunterVanillaPathContinuationDiagnostic = null;
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
