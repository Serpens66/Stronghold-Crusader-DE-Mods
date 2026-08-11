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
        private const int MaxPathCandidatesPerHunter = 24;
        private const int HunterHutWorkCost = 600;
        private const int BestTargetToleranceCost = 80;
        private const int DefaultPreyHandlingCost = 100;
        private const int MaxPreyCacheDiagnosticLogs = 120;
        private const int MaxHunterTargetDiagnosticLogs = 160;
        private const int MaxHunterProjectileDiagnosticLogs = 160;
        private const int MaxHunterLifecycleDiagnosticLogs = 240;
        private const int MaxHunterTargetAbortDiagnosticLogs = 160;
        private const int MaxWatchedChickenTargets = 96;
        private const int ComparisonChickenDiagnosticSpawnDistance = 5;
        private const int ComparisonChickenPlayerId = 2;
        private const int SameOwnerChickenProjectileDamageDistance = 64;
        private const int MaxSameOwnerChickenProjectileDamageAttempts = 3;

        // Native animal AI states observed for hunter corpses. 0x6E is the normal
        // pickupable corpse state; 0x6F is used when we have to finalize a corpse
        // after compensating a hunter shot that was blocked by geometry.
        private const ushort HunterCorpsePickupAiState = 0x6E;
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
        private static readonly long PathCostCacheInterval = Stopwatch.Frequency * 5;
        private static readonly long BestTargetCacheInterval = Stopwatch.Frequency / 2;
        private static readonly long AbortedTargetCooldownInterval = Stopwatch.Frequency * 30;
        private static readonly long HunterTargetSummaryInterval = Stopwatch.Frequency * 5;
        private static readonly long HunterSearchDetectionGap = Stopwatch.Frequency / 4;
        private static readonly long PendingHunterShotIntentDelay = Stopwatch.Frequency;
        private const int ShortLivedCorpseVisiblePreserveMapTicksAtSpeed40 = 1800;
        private static readonly long ShortLivedCorpsePreserveCleanupInterval = Stopwatch.Frequency * 10;

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, eChimps> hunterPreyTypes = new Dictionary<int, eChimps>();
        private readonly Dictionary<int, long> nextIdleHunterRequeryTimestamps = new Dictionary<int, long>();
        private readonly HashSet<uint> loggedCollectedCorpseGlobalIds = new HashSet<uint>();
        private readonly List<PreySnapshot> preyCache = new List<PreySnapshot>();
        private readonly Dictionary<PathCostKey, CachedPathCost> pathCostCache = new Dictionary<PathCostKey, CachedPathCost>();
        private readonly Dictionary<int, CachedBestTarget> bestTargetCache = new Dictionary<int, CachedBestTarget>();
        private readonly Dictionary<int, HunterTargetSnapshot> activeHunterTargets = new Dictionary<int, HunterTargetSnapshot>();
        private readonly Dictionary<HunterPreyCooldownKey, long> abortedTargetCooldowns = new Dictionary<HunterPreyCooldownKey, long>();
        private readonly Dictionary<int, long> lastHunterQueryTimestamps = new Dictionary<int, long>();
        private readonly Dictionary<int, long> hunterMeatPickupTimestamps = new Dictionary<int, long>();
        private readonly Dictionary<HunterShotIntentKey, PendingHunterShotIntent> pendingHunterShotIntents = new Dictionary<HunterShotIntentKey, PendingHunterShotIntent>();
        private readonly HashSet<int> diagnosticHunterIds = new HashSet<int>();
        private readonly Dictionary<int, string> lastHunterLifecycleStateSignatures = new Dictionary<int, string>();
        private readonly Dictionary<int, string> lastHunterLifecycleStateDescriptions = new Dictionary<int, string>();
        private readonly Dictionary<uint, WatchedChickenTarget> watchedChickenTargets = new Dictionary<uint, WatchedChickenTarget>();
        private readonly Dictionary<uint, string> lastChickenLifecycleStateSignatures = new Dictionary<uint, string>();

        // Keeps short-lived corpses visible long enough for hunters to reach them.
        // The value is either the preserve-until map tick or ExpiredShortLivedCorpsePreserve.
        private readonly Dictionary<uint, long> shortLivedCorpsePreserveUntil = new Dictionary<uint, long>();

        private HunterTargetEligibilityHook hunterTargetEligibilityHook;
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
        private int pathCacheHits;
        private int pathCacheMisses;
        private int hunterProjectileDiagnosticLogs;
        private int hunterLifecycleDiagnosticLogs;
        private int hunterTargetAbortDiagnosticLogs;
        private int shortLivedCorpsePreserveLogs;
        private bool hunterQueryVanillaHookConfirmedLogged;
        private bool hunterQueryChickenHookConfirmedLogged;
        private bool hunterQueryNonzeroFlagsChickenAdmittedLogged;
        private bool hunterQueryReservedChickenRetargetLogged;
        private bool hunterQueryHookInvalidContextLogged;
        private bool hunterQueryHookDiagnosticFailureLogged;
        private bool chickenTargetDiagnosticFailureLogged;
        private bool watchedChickenStateDiagnosticFailureLogged;
        private bool chickenProjectileDiagnosticFailureLogged;
        private bool chickenProjectileDamageFailureLogged;
        private bool watchedChickenLimitLogged;
        private bool mapReadyForDiagnosticSpawn;
        private bool comparisonChickenDiagnosticSpawnedThisMap;
        private bool applied;

        public ImprovedHuntersRuntime(ManualLogSource log, ImprovedHuntersViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Apply(ReadOnlySpan<byte> memory, ulong imageBase)
        {
            if (applied)
                return;

            try
            {
                hunterTargetEligibilityHook = new HunterTargetEligibilityHook(
                    log,
                    settings,
                    memory,
                    imageBase,
                    Shared.DebugLogHelper.IsCurrentNativeLibraryVersion());

                subscriptions.Add(UnitR3EventHooks.OnUnitHunterQueryTarget.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnHunterQueryTarget));
                subscriptions.Add(UnitR3EventHooks.OnUnitHunterQueryTarget.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnHunterQueryTargetPostDiagnostic));
                subscriptions.Add(UnitR3EventHooks.OnCalculateBonusYield.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnCalculateBonusYield));
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
                subscriptions.Add(UnitR3EventHooks.OnUnitTakeProjectileDamageEx.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnUnitTakeProjectileDamage));
                subscriptions.Add(UnitR3EventHooks.OnUnitKilledByProjectile.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(OnUnitKilledByProjectile));
                subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(_ => OnMapStarting()));
                subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => OnMapStarted()));
                subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnBuildingSpawn));
                subscriptions.Add(UnitR3EventHooks.OnUnitMovement.Observable
                    .Subscribe(OnUnitMovement));
                subscriptions.Add(UnitR3EventHooks.OnUnitUnityVisualInterpolate.Observable
                    .Subscribe(_ => RunNativeScan()));

                settings.SettingChanged += OnSettingChanged;
                InitializeRabbitDespawnPatch();
                InitializeExtraDespawnPatches(memory, imageBase);
                ApplyDespawnPatches();
                ApplyCamelHealthPatch();

                applied = true;
                log.LogInfo("Improved Hunters runtime enabled.");
            }
            catch
            {
                try
                {
                    Dispose();
                }
                catch (Exception cleanupException)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Improved Hunters cleanup after failed initialization also failed: {cleanupException}");
                }

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
                TryApplySameOwnerChickenDamageDuringFlight();
                ResolvePendingHunterShotIntents(timestamp);

                SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
                if (units._array == null || units.Length == 0)
                    return;

                LogDiagnosticHunterStateChanges(units, "native-scan-pre");
                TryLogWatchedChickenStateChanges(units);

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
                        IsRuntimeHuntingEnabled(unit->r_UnitChimp))
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

                    if (IsEligibleUnreservedPrey(unit))
                        eligiblePrey.Add((IntPtr)unit);
                }

                if (adjustedLiveCamels > 0)
                    LogCamelHealthPatch(adjustedLiveCamels);

                CleanupShortLivedCorpsePreserveCache(units, timestamp);
                TrackHunterPreyAndExpireCollectedCorpses(units, hunters, timestamp);
                RequeryIdleHuntersNearPrey(units, hunters, eligiblePrey, timestamp);
                LogDiagnosticHunterStateChanges(units, "native-scan-post");
            }
            catch (Exception exception)
            {
                if (nativeScanFailureLogged)
                    return;

                Shared.DebugLogHelper.LogError(log, $"Improved Hunters native scan failed; native scan remains inactive after this error: {exception}");
                nativeScanFailureLogged = true;
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
                bool preyInSearchRadius = false;
                int nearbyPreyUnitId = 0;
                uint nearbyPreyGlobalId = 0;
                eChimps nearbyPreyType = 0;
                ushort nearbyPreyReservation = 0;
                int nearbyPreyDistance = int.MaxValue;

                foreach (IntPtr preyAddress in eligiblePrey)
                {
                    GameUnit* prey = (GameUnit*)preyAddress.ToPointer();
                    byte* preyBytes = (byte*)preyAddress.ToPointer();
                    short preyTileX = *(short*)(preyBytes + 0xC0);
                    short preyTileY = *(short*)(preyBytes + 0xC2);

                    int distance = Math.Max(
                            Math.Abs(preyTileX - hunterTileX),
                            Math.Abs(preyTileY - hunterTileY));
                    if (distance <= HunterSearchRadius)
                    {
                        preyInSearchRadius = true;
                        nearbyPreyUnitId = checked((int)(prey - units._array) + 1);
                        nearbyPreyGlobalId = prey->r_GlobalId;
                        nearbyPreyType = prey->r_UnitChimp;
                        nearbyPreyReservation = *(ushort*)(preyBytes + 0x448);
                        nearbyPreyDistance = distance;
                        break;
                    }
                }

                if (!preyInSearchRadius)
                    continue;

                LogHunterLifecycleState(hunterId, "idle-requery-before-write", onlyIfChanged: false);
                LogHunterLifecycleMessage(
                    $"Improved Hunters idle requery mutation: hunter={hunterId}/{hunter->r_GlobalId}, " +
                    $"state=0x{aiState:X}->0x0, timer={*(ushort*)(hunterBytes + 0x2C4)}->0, " +
                    $"nearbyPrey={nearbyPreyUnitId}/{nearbyPreyGlobalId}/{nearbyPreyType}, " +
                    $"reservation={nearbyPreyReservation}, distance={nearbyPreyDistance}");
                *(ushort*)(hunterBytes + 0x2BC) = 0;
                *(ushort*)(hunterBytes + 0x2C4) = 0;
                nextIdleHunterRequeryTimestamps[hunterId] = timestamp + IdleHunterRequeryInterval;
                LogHunterLifecycleState(hunterId, "idle-requery-after-write", onlyIfChanged: false);
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
                TrackHunterTargetState(hunterId, targetUnitId, targetGlobalId, timestamp);

                if (targetUnitId == 0 || targetUnitId > units.Length)
                    continue;

                GameUnit* target = units.GetValuePointer(targetUnitId - 1);
                if (!settings.IsKnownAnimal(target->r_UnitChimp))
                    continue;

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

        private unsafe bool IsEligibleUnreservedPrey(GameUnit* prey)
        {
            return TryGetPreyEligibility(prey, out PreyEligibility eligibility) &&
                eligibility.Eligible;
        }

        private unsafe bool TryGetPreyEligibility(GameUnit* prey, out PreyEligibility eligibility)
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
                eligibility.AliveState == (short)AliveState.IsAlive &&
                eligibility.FlagsAllowed &&
                eligibility.Reservation == 0 &&
                (eligibility.CorpseFlag == 0 || eligibility.AiState == HunterCorpsePickupAiState);

            return eligibility.KnownAnimal;
        }

        private void OnMapStarting()
        {
            mapReadyForDiagnosticSpawn = false;
            comparisonChickenDiagnosticSpawnedThisMap = false;
        }

        private void OnMapStarted()
        {
            hunterPreyTypes.Clear();
            nextIdleHunterRequeryTimestamps.Clear();
            loggedCollectedCorpseGlobalIds.Clear();
            shortLivedCorpsePreserveUntil.Clear();
            diagnosticHunterIds.Clear();
            lastHunterLifecycleStateSignatures.Clear();
            lastHunterLifecycleStateDescriptions.Clear();
            watchedChickenTargets.Clear();
            lastChickenLifecycleStateSignatures.Clear();
            hunterLifecycleDiagnosticLogs = 0;
            hunterTargetAbortDiagnosticLogs = 0;
            chickenTargetDiagnosticFailureLogged = false;
            watchedChickenStateDiagnosticFailureLogged = false;
            chickenProjectileDiagnosticFailureLogged = false;
            watchedChickenLimitLogged = false;
            ClearTargetSelectionCaches();
            nativeScanFailureLogged = false;
            mapReadyForDiagnosticSpawn = true;
            RunNativeScan(force: true);
        }

        private unsafe void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (!mapReadyForDiagnosticSpawn ||
                comparisonChickenDiagnosticSpawnedThisMap ||
                !settings.EnableMod ||
                !settings.HuntChicken ||
                args.Building != eStructs.STRUCT_HUNTERS_HUT ||
                args.ReturnValue <= 0 ||
                args.PlayerId != GamePlayerManagerAPI.Instance.GetLocalPlayerId())
            {
                return;
            }

            try
            {
                GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
                int[] offsetsX = { ComparisonChickenDiagnosticSpawnDistance, -ComparisonChickenDiagnosticSpawnDistance, 0, 0 };
                int[] offsetsY = { 0, 0, ComparisonChickenDiagnosticSpawnDistance, -ComparisonChickenDiagnosticSpawnDistance };

                for (int index = 0; index < offsetsX.Length; index++)
                {
                    int tileX = args.TileX + offsetsX[index];
                    int tileY = args.TileY + offsetsY[index];
                    if (!tileApi.IsTileInsideMapBounds(tileX, tileY))
                        continue;

                    int tileId = tileApi.GetTileId(tileX, tileY);
                    if (!tileApi.IsValidTileId(tileId) || !tileApi.IsTileWalkableAndUnoccupied(tileId))
                        continue;

                    long createdUnitId = GameUnitManagerAPI.Instance.CreateUnitLocal(
                        playerColorId: ComparisonChickenPlayerId,
                        playerOwnerId: ComparisonChickenPlayerId,
                        localTileX: tileX,
                        localTileY: tileY,
                        heightElevation: tileApi.GetTileHeight(tileId),
                        chimp: eChimps.CHIMP_TYPE_CHICKEN);
                    if (createdUnitId <= 0 || createdUnitId > int.MaxValue)
                    {
                        Shared.DebugLogHelper.LogError(
                            log,
                            $"Improved Hunters player-2 comparison chicken spawn returned an invalid ID: hut={args.ReturnValue}, " +
                            $"tile={tileX},{tileY}, result={createdUnitId}.");
                        return;
                    }

                    int unitId = (int)createdUnitId;
                    if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* chicken) ||
                        chicken == null ||
                        chicken->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN ||
                        chicken->r_ControllableForPlayerId != ComparisonChickenPlayerId ||
                        chicken->r_SpritePlayerColorId != ComparisonChickenPlayerId)
                    {
                        Shared.DebugLogHelper.LogError(
                            log,
                            $"Improved Hunters player-2 comparison chicken spawn verification failed: hut={args.ReturnValue}, " +
                            $"unit={unitId}, expectedOwnerAndColor={ComparisonChickenPlayerId}, tile={tileX},{tileY}.");
                        return;
                    }

                    comparisonChickenDiagnosticSpawnedThisMap = true;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Improved Hunters player-2 comparison chicken spawned: hut={args.ReturnValue}, hutOwner={args.PlayerId}, " +
                        $"unit={unitId}/{chicken->r_GlobalId}, owner={chicken->r_ControllableForPlayerId}, " +
                        $"color={chicken->r_SpritePlayerColorId}, flags92={*(ushort*)((byte*)chicken + 0x92)}, " +
                        $"aliveState={(short)chicken->r_AliveState}, tile={tileX},{tileY}, " +
                        $"hutTile={args.TileX},{args.TileY}, requestedDistance={ComparisonChickenDiagnosticSpawnDistance}.");
                    return;
                }

                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters could not find a walkable player-2 comparison chicken spawn tile: " +
                    $"hut={args.ReturnValue}, hutTile={args.TileX},{args.TileY}, requestedDistance={ComparisonChickenDiagnosticSpawnDistance}.");
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters player-2 comparison chicken spawn failed: hut={args.ReturnValue}, error={exception}");
            }
        }

        private unsafe void OnHunterQueryTarget(UnitHunterQueryTargetEventArgs args)
        {
            if (!settings.EnableMod)
                return;

            if (!TryGetValidHunter(args.HunterUnitId, out GameUnit* hunter))
            {
                if (!hunterQueryHookInvalidContextLogged)
                {
                    hunterQueryHookInvalidContextLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Improved Hunters ignored an invalid Hunter-query context: hunter={args.HunterUnitId}, " +
                        $"candidate={args.QueryUnitId}. Feature eligibility remains fail-closed for this callback.");
                }

                return;
            }

            long timestamp = Stopwatch.GetTimestamp();
            TrackHunterSearchQuery(args.HunterUnitId, timestamp);
            diagnosticHunterIds.Add(args.HunterUnitId);

            if (!IsValidUnitId(args.QueryUnitId))
                return;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            eChimps queryType = unitApi.GetType(args.QueryUnitId);
            if (!settings.IsKnownAnimal(queryType) || !IsRuntimeHuntingEnabled(queryType))
                return;

            LogHunterEligibilityHookConfirmation(args, queryType);

            bool isValidTarget = true;
            bool usedFallback = true;
            TargetSelection targetSelection = default;
            BestTarget bestTarget = default;
            GameUnit* reservedChicken = null;
            bool isReservedCurrentChicken = queryType == eChimps.CHIMP_TYPE_CHICKEN &&
                TryGetReservedCurrentChickenTarget(hunter, args.QueryUnitId, out reservedChicken);
            if (isReservedCurrentChicken)
            {
                usedFallback = false;
                if (!hunterQueryReservedChickenRetargetLogged)
                {
                    hunterQueryReservedChickenRetargetLogged = true;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Improved Hunters retained the Hunter's reserved chicken during close-range re-query: " +
                        $"hunter={args.HunterUnitId}, candidate={args.QueryUnitId}/{reservedChicken->r_GlobalId}, " +
                        $"owner={reservedChicken->r_ControllableForPlayerId}, reservation=2.");
                }
            }
            else if (!settings.ImprovedPathfinding)
            {
                usedFallback = false;
            }
            else if (TryGetTargetSelectionForHunter(args.HunterUnitId, timestamp, out targetSelection))
            {
                bestTarget = targetSelection.BestTarget;
                isValidTarget = targetSelection.IsAllowed(args.QueryUnitId);
                usedFallback = false;
            }
            else
            {
                hunterTargetNoBestEvents++;
            }

            args.IsValidTarget = isValidTarget;
            hunterTargetQueryEvents++;
            if (isValidTarget)
            {
                hunterTargetAcceptedEvents++;
                hunterPreyTypes[args.HunterUnitId] = queryType;

                if (queryType == eChimps.CHIMP_TYPE_CHICKEN)
                {
                    if (unitApi.TryGetUnitById(args.QueryUnitId, out GameUnit* acceptedChicken) && acceptedChicken != null)
                    {
                        hunterTargetEligibilityHook?.RecordAcceptedChickenTarget(
                            args.HunterUnitId,
                            args.QueryUnitId,
                            acceptedChicken->r_GlobalId);
                    }
                    WatchAcceptedChickenTarget(args.HunterUnitId, args.QueryUnitId);
                    LogHunterLifecycleState(args.HunterUnitId, "chicken-query-pre-accepted", onlyIfChanged: false);
                }
            }
            else
            {
                hunterTargetRejectedEvents++;
            }

            if (usedFallback)
                hunterTargetFallbackEvents++;

            LogHunterTargetQueryDiagnostic(args.HunterUnitId, args.QueryUnitId, queryType, isValidTarget, usedFallback, targetSelection);
            LogHunterTargetQuerySummary();
        }

        private unsafe void OnHunterQueryTargetPostDiagnostic(UnitHunterQueryTargetEventArgs args)
        {
            if (!settings.EnableMod ||
                args.HunterUnitId <= 0 ||
                !diagnosticHunterIds.Contains(args.HunterUnitId) ||
                args.IsValidTarget != true ||
                !IsValidUnitId(args.QueryUnitId) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(args.QueryUnitId, out GameUnit* target) ||
                target == null ||
                !settings.IsKnownAnimal(target->r_UnitChimp))
            {
                return;
            }

            LogHunterLifecycleState(
                args.HunterUnitId,
                $"query-post(candidate={args.QueryUnitId},result={args.IsValidTarget?.ToString() ?? "null"})",
                onlyIfChanged: false);
        }

        private static unsafe bool TryGetValidHunter(int hunterUnitId, out GameUnit* hunter)
        {
            hunter = null;
            return IsValidUnitId(hunterUnitId) &&
                GameUnitManagerAPI.Instance.TryGetUnitById(hunterUnitId, out hunter) &&
                hunter != null &&
                hunter->r_AliveState == AliveState.IsAlive &&
                hunter->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER;
        }

        private unsafe bool TryGetReservedCurrentChickenTarget(
            GameUnit* hunter,
            int candidateUnitId,
            out GameUnit* candidate)
        {
            candidate = null;
            if (hunter == null || !IsValidUnitId(candidateUnitId))
                return false;

            byte* hunterBytes = (byte*)hunter;
            if (*(ushort*)(hunterBytes + 0x2BC) != 1)
                return false;

            ushort targetUnitId = *(ushort*)(hunterBytes + 0x39A);
            uint targetGlobalId = *(uint*)(hunterBytes + 0x39C);
            if (targetUnitId != candidateUnitId || targetGlobalId == 0)
                return false;

            return GameUnitManagerAPI.Instance.TryGetUnitById(candidateUnitId, out candidate) &&
                candidate != null &&
                candidate->r_GlobalId == targetGlobalId &&
                candidate->r_ControllableForPlayerId != 0 &&
                candidate->r_ControllableForPlayerId == hunter->r_ControllableForPlayerId &&
                TryGetPreyEligibility(candidate, out PreyEligibility eligibility) &&
                eligibility.Type == eChimps.CHIMP_TYPE_CHICKEN &&
                eligibility.RuntimeHuntingEnabled &&
                eligibility.AliveState == (short)AliveState.IsAlive &&
                eligibility.FlagsAllowed &&
                eligibility.Reservation == 2 &&
                eligibility.CorpseFlag == 0;
        }

        private unsafe void LogHunterEligibilityHookConfirmation(
            UnitHunterQueryTargetEventArgs args,
            eChimps queryType)
        {
            bool isVanillaAnimal = queryType == eChimps.CHIMP_TYPE_DEER ||
                queryType == eChimps.CHIMP_TYPE_GOAT;
            bool isChicken = queryType == eChimps.CHIMP_TYPE_CHICKEN;
            if ((!isVanillaAnimal || hunterQueryVanillaHookConfirmedLogged) &&
                (!isChicken || hunterQueryChickenHookConfirmedLogged && hunterQueryNonzeroFlagsChickenAdmittedLogged))
            {
                return;
            }

            try
            {
                if (!IsValidUnitId(args.HunterUnitId) ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(args.HunterUnitId, out GameUnit* hunter) ||
                    hunter == null ||
                    hunter->r_AliveState != AliveState.IsAlive ||
                    hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(args.QueryUnitId, out GameUnit* candidate) ||
                    candidate == null ||
                    candidate->r_UnitChimp != queryType)
                {
                    if (!hunterQueryHookInvalidContextLogged)
                    {
                        hunterQueryHookInvalidContextLogged = true;
                        Shared.DebugLogHelper.LogError(
                            log,
                            $"Improved Hunters Hunter-query hook reached an invalid diagnostic context; behavior is unchanged: " +
                            $"hunter={args.HunterUnitId}, candidate={args.QueryUnitId}/{queryType}.");
                    }
                    return;
                }

                byte* candidateBytes = (byte*)candidate;
                ushort flagsAt92 = *(ushort*)(candidateBytes + 0x92);
                ushort corpseFlag = *(ushort*)(candidateBytes + 0x29C);
                ushort reservation = *(ushort*)(candidateBytes + 0x448);
                bool nonzeroFlagsBypassed = isChicken && flagsAt92 != 0;

                if (isVanillaAnimal && !hunterQueryVanillaHookConfirmedLogged)
                {
                    hunterQueryVanillaHookConfirmedLogged = true;
                    LogHunterEligibilityHookCandidate(
                        "vanilla-animal",
                        args.HunterUnitId,
                        args.QueryUnitId,
                        candidate,
                        flagsAt92,
                        corpseFlag,
                        reservation,
                        nonzeroFlagsBypassed: false);
                }

                if (isChicken && !hunterQueryChickenHookConfirmedLogged)
                {
                    hunterQueryChickenHookConfirmedLogged = true;
                    LogHunterEligibilityHookCandidate(
                        "chicken",
                        args.HunterUnitId,
                        args.QueryUnitId,
                        candidate,
                        flagsAt92,
                        corpseFlag,
                        reservation,
                        nonzeroFlagsBypassed);
                }

                if (nonzeroFlagsBypassed && !hunterQueryNonzeroFlagsChickenAdmittedLogged)
                {
                    hunterQueryNonzeroFlagsChickenAdmittedLogged = true;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Improved Hunters Hunter-query nonzero-flags chicken admitted: hunter={args.HunterUnitId}, " +
                        $"candidate={args.QueryUnitId}/{candidate->r_GlobalId}, owner={candidate->r_ControllableForPlayerId}, " +
                        $"color={candidate->r_SpritePlayerColorId}, flags92={flagsAt92}, reservation={reservation}.");
                }
            }
            catch (Exception exception)
            {
                if (hunterQueryHookDiagnosticFailureLogged)
                    return;

                hunterQueryHookDiagnosticFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters Hunter-query hook diagnostic failed; eligibility behavior remains active: {exception}");
            }
        }

        private unsafe void LogHunterEligibilityHookCandidate(
            string category,
            int hunterId,
            int candidateId,
            GameUnit* candidate,
            ushort flagsAt92,
            ushort corpseFlag,
            ushort reservation,
            bool nonzeroFlagsBypassed)
        {
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Improved Hunters Hunter-query hook confirmed: category={category}, hunter={hunterId}, " +
                $"candidate={candidateId}/{candidate->r_GlobalId}/{candidate->r_UnitChimp}, " +
                $"owner={candidate->r_ControllableForPlayerId}, color={candidate->r_SpritePlayerColorId}, " +
                $"flags92={flagsAt92}, aliveState={(short)candidate->r_AliveState}, corpseFlag={corpseFlag}, " +
                $"reservation={reservation}, tile={candidate->r_CurrentTilePositionX},{candidate->r_CurrentTilePositionY}, " +
                $"nonzeroFlagsBypassed={nonzeroFlagsBypassed}.");
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
            if (!TryGetHunterOrigin(hunter, hunterOwner, timestamp, out int originTileX, out int originTileY, out int originTileId, out int granaryRoundTripCost))
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

                if (IsTargetOnCooldown(hunterUnitId, prey.GlobalId, timestamp))
                    continue;

                int heuristicDistance = GetChebyshevDistance(originTileX, originTileY, prey.TileX, prey.TileY);
                if (heuristicDistance > HunterTargetCandidateRadius)
                    continue;

                int heuristicCycleCost = HunterHutWorkCost + GetPreyHandlingCost(prey.Type) + granaryRoundTripCost + (heuristicDistance * 10 * 2);
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
            int limit = Math.Min(candidates.Count, MaxPathCandidatesPerHunter);
            for (int i = 0; i < limit; i++)
            {
                PreySnapshot prey = candidates[i].Prey;
                if (!TryGetLiveAvailablePreySnapshot(prey, out prey))
                    continue;

                if (!TryGetPathCost(originTileX, originTileY, originTileId, prey, timestamp, out int pathCost))
                    continue;

                int cycleCost = HunterHutWorkCost + GetPreyHandlingCost(prey.Type) + granaryRoundTripCost + (pathCost * 2);
                if (cycleCost <= 0)
                    cycleCost = 1;

                BestTarget candidate = new BestTarget(prey.UnitId, prey.GlobalId, prey.Type, prey.MeatAmount, pathCost, granaryRoundTripCost, cycleCost);
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

            if (!TryGetPreyEligibility(unit, out PreyEligibility eligibility) ||
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
            long timestamp,
            out int originTileX,
            out int originTileY,
            out int originTileId,
            out int granaryRoundTripCost)
        {
            originTileX = hunter->r_CurrentTilePositionX;
            originTileY = hunter->r_CurrentTilePositionY;
            originTileId = 0;
            granaryRoundTripCost = 0;

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

            originTileId = tileApi.GetTileId(originTileX, originTileY);
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

            if (TryGetNearestGranaryPathCost(hunterOwner, originTileX, originTileY, originTileId, timestamp, out int granaryPathCost))
                granaryRoundTripCost = granaryPathCost * 2;

            return true;
        }

        private unsafe bool TryGetNearestGranaryPathCost(
            int hunterOwner,
            int originTileX,
            int originTileY,
            int originTileId,
            long timestamp,
            out int bestPathCost)
        {
            bestPathCost = 0;
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            SimpleNativeArray<GameBuilding> buildings = buildingApi.GetBuildingsArray();
            if (buildings._array == null || buildings.Length == 0)
                return false;

            List<GranaryCandidate> candidates = new List<GranaryCandidate>();
            for (int index = 0; index < buildings.Length; index++)
            {
                GameBuilding* building = buildings.GetValuePointer(index);
                if (building->r_AliveState != AliveState.IsAlive ||
                    building->r_BuildingType != eStructs.STRUCT_GRANARY ||
                    building->r_PlayerIdOwner != hunterOwner)
                {
                    continue;
                }

                int heuristicDistance = GetChebyshevDistance(
                    originTileX,
                    originTileY,
                    building->r_TilePositionXBegin,
                    building->r_TilePositionYBegin);
                candidates.Add(new GranaryCandidate(index + 1, building->r_GlobalId, building->r_TilePositionXBegin, building->r_TilePositionYBegin, heuristicDistance));
            }

            if (candidates.Count == 0)
                return false;

            candidates.Sort(CompareGranaryCandidatesByHeuristic);
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            for (int i = 0; i < candidates.Count; i++)
            {
                GranaryCandidate granary = candidates[i];
                if (!TryGetWalkableTileNear(granary.TileX, granary.TileY, 10, out int targetTileX, out int targetTileY, out int targetTileId))
                    continue;

                PathCostKey key = new PathCostKey(originTileId, granary.GlobalId, targetTileId);
                if (pathCostCache.TryGetValue(key, out CachedPathCost cachedPathCost) &&
                    timestamp < cachedPathCost.ExpiresAt)
                {
                    pathCacheHits++;
                    if (cachedPathCost.Cost < 0)
                        continue;

                    bestPathCost = cachedPathCost.Cost;
                    return true;
                }

                pathCacheMisses++;
                List<UnmanagedVector2<ushort>> path = tileApi.FindPath(originTileX, originTileY, targetTileX, targetTileY);
                if (path == null || path.Count == 0)
                {
                    pathCostCache[key] = new CachedPathCost(-1, timestamp + PathCostCacheInterval);
                    continue;
                }

                bestPathCost = CalculatePathCost(originTileX, originTileY, path);
                pathCostCache[key] = new CachedPathCost(bestPathCost, timestamp + PathCostCacheInterval);
                return true;
            }

            return false;
        }

        private static int CompareGranaryCandidatesByHeuristic(GranaryCandidate left, GranaryCandidate right)
        {
            int distanceCompare = left.HeuristicDistance.CompareTo(right.HeuristicDistance);
            if (distanceCompare != 0)
                return distanceCompare;

            return left.BuildingId.CompareTo(right.BuildingId);
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

            ReleaseStalePreyReservationsIfNeeded(units, timestamp);

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
                TryGetPreyEligibility(unit, out PreyEligibility eligibility);
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
            int releasedReservations = 0;
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                int unitId = index + 1;
                TryGetPreyEligibility(unit, out PreyEligibility eligibility);
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
                    continue;

                byte* preyBytes = (byte*)unit;
                *(ushort*)(preyBytes + 0x448) = 0;
                releasedReservations++;

                eligibility.Reservation = 0;
                eligibility.Eligible =
                    eligibility.FlagsAllowed &&
                    (eligibility.CorpseFlag == 0 || eligibility.AiState == HunterCorpsePickupAiState);

                LogPreyCacheDiagnostic(unitId, eligibility, "released-stale-reservation=2");
            }

            if (releasedReservations > 0 && preyCacheDiagnosticLogs < MaxPreyCacheDiagnosticLogs)
            {
                preyCacheDiagnosticLogs++;
                log.LogInfo(
                    $"Improved Hunters stale prey reservation cleanup: reservedKnownPrey={reservedKnownPrey}, " +
                    $"activeHunterTargets={activeHunterTargetUnitIds.Count}, released={releasedReservations} " +
                    $"({preyCacheDiagnosticLogs}/{MaxPreyCacheDiagnosticLogs}).");
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
                $"reservation={eligibility.Reservation}, runtimeEnabled={eligibility.RuntimeHuntingEnabled} " +
                $"({preyCacheDiagnosticLogs}/{MaxPreyCacheDiagnosticLogs}).");

            if (preyCacheDiagnosticLogs == MaxPreyCacheDiagnosticLogs)
                log.LogInfo("Improved Hunters prey cache diagnostic limit reached.");
        }

        private static string GetPreyIneligibilityReason(PreyEligibility eligibility)
        {
            if (!eligibility.RuntimeHuntingEnabled)
                return "disabled";

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

        private bool TryGetPathCost(
            int originTileX,
            int originTileY,
            int originTileId,
            PreySnapshot prey,
            long timestamp,
            out int pathCost)
        {
            pathCost = 0;
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsTileInsideMapBounds(prey.TileX, prey.TileY))
                return false;

            int targetTileId = tileApi.GetTileId(prey.TileX, prey.TileY);
            if (!tileApi.IsValidTileId(targetTileId))
                return false;

            PathCostKey key = new PathCostKey(originTileId, prey.GlobalId, targetTileId);
            if (pathCostCache.TryGetValue(key, out CachedPathCost cachedPathCost) &&
                timestamp < cachedPathCost.ExpiresAt)
            {
                pathCacheHits++;
                pathCost = cachedPathCost.Cost;
                return pathCost >= 0;
            }

            pathCacheMisses++;
            if (originTileX == prey.TileX && originTileY == prey.TileY)
            {
                pathCost = 0;
            }
            else
            {
                List<UnmanagedVector2<ushort>> path = tileApi.FindPath(originTileX, originTileY, prey.TileX, prey.TileY);
                if (path == null || path.Count == 0)
                {
                    pathCostCache[key] = new CachedPathCost(-1, timestamp + PathCostCacheInterval);
                    return false;
                }

                pathCost = CalculatePathCost(originTileX, originTileY, path);
            }

            pathCostCache[key] = new CachedPathCost(pathCost, timestamp + PathCostCacheInterval);
            return true;
        }

        private static int CalculatePathCost(int startX, int startY, List<UnmanagedVector2<ushort>> path)
        {
            int cost = 0;
            int previousX = startX;
            int previousY = startY;
            for (int i = 0; i < path.Count; i++)
            {
                int currentX = path[i].X;
                int currentY = path[i].Y;
                int dx = Math.Abs(currentX - previousX);
                int dy = Math.Abs(currentY - previousY);
                cost += dx != 0 && dy != 0 ? 14 : 10;
                previousX = currentX;
                previousY = currentY;
            }

            return cost;
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

            if (candidate.Type == currentBest.Type && candidate.PathCost != currentBest.PathCost)
                return candidate.PathCost < currentBest.PathCost;

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
                log.LogInfo($"Improved Hunters target search start: hunter={hunterUnitId}, searchCount={hunterTargetSearchStarts}.");
        }

        private void OnHunterPickUpMeat(UnitHunterPickUpMeatEventArgs args)
        {
            LogHunterLifecycleState(args.UnitId, "meat-pickup", onlyIfChanged: false);
            hunterMeatPickupTimestamps[args.UnitId] = Stopwatch.GetTimestamp();
            TryDeleteCollectedShortLivedCorpse(args.UnitId);
            activeHunterTargets.Remove(args.UnitId);
            bestTargetCache.Remove(args.UnitId);
        }

        private void OnHunterDropOffMeat(UnitHunterDropOffMeatEventArgs args)
        {
            LogHunterLifecycleState(args.UnitId, "meat-dropoff", onlyIfChanged: false);
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

        private void TrackHunterTargetState(int hunterUnitId, ushort targetUnitId, uint targetGlobalId, long timestamp)
        {
            if (!settings.EnableMod || !settings.ImprovedPathfinding)
                return;

            if (targetUnitId != 0 && targetGlobalId != 0)
            {
                bool changed = !activeHunterTargets.TryGetValue(hunterUnitId, out HunterTargetSnapshot currentTarget) ||
                    currentTarget.UnitId != targetUnitId ||
                    currentTarget.GlobalId != targetGlobalId;
                activeHunterTargets[hunterUnitId] = new HunterTargetSnapshot(targetUnitId, targetGlobalId);
                if (changed)
                    LogHunterLifecycleState(hunterUnitId, "native-target-acquired", onlyIfChanged: false);
                return;
            }

            if (!activeHunterTargets.TryGetValue(hunterUnitId, out HunterTargetSnapshot previousTarget))
                return;

            LogHunterLifecycleState(hunterUnitId, "native-target-cleared", onlyIfChanged: false);
            activeHunterTargets.Remove(hunterUnitId);
            abortedTargetCooldowns[new HunterPreyCooldownKey(hunterUnitId, previousTarget.GlobalId)] = timestamp + AbortedTargetCooldownInterval;
            bestTargetCache.Remove(hunterUnitId);

            if (hunterTargetAbortDiagnosticLogs < MaxHunterTargetAbortDiagnosticLogs)
            {
                hunterTargetAbortDiagnosticLogs++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters target abort: hunter={hunterUnitId}, target={previousTarget.UnitId}, " +
                    $"globalId={previousTarget.GlobalId}, cooldownSeconds={AbortedTargetCooldownInterval / Stopwatch.Frequency} " +
                    $"({hunterTargetAbortDiagnosticLogs}/{MaxHunterTargetAbortDiagnosticLogs}).");

                if (hunterTargetAbortDiagnosticLogs == MaxHunterTargetAbortDiagnosticLogs)
                    Shared.DebugLogHelper.LogInfo(log, "Improved Hunters target abort diagnostic limit reached.");
            }
        }

        private void OnUnitMovement(UnitMovementEventArgs args)
        {
            if (args.UnitId <= int.MaxValue &&
                diagnosticHunterIds.Contains((int)args.UnitId))
            {
                LogHunterLifecycleState(
                    (int)args.UnitId,
                    args.Phase == EventHookPhase.Pre ? "movement-pre" : "movement-post",
                    onlyIfChanged: true);
            }

            RunNativeScan();
        }

        private unsafe void LogHunterLifecycleState(
            int hunterUnitId,
            string source,
            bool onlyIfChanged)
        {
            if (!settings.EnableMod || hunterLifecycleDiagnosticLogs >= MaxHunterLifecycleDiagnosticLogs)
                return;

            try
            {
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                if (!IsValidUnitId(hunterUnitId) ||
                    !unitApi.TryGetUnitById(hunterUnitId, out GameUnit* hunter) ||
                    hunter == null ||
                    hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                {
                    return;
                }

                byte* hunterBytes = (byte*)hunter;
                ushort currentAiState = *(ushort*)(hunterBytes + 0x2BC);
                ushort stateTimer = *(ushort*)(hunterBytes + 0x2C4);
                ushort wanderMode = *(ushort*)(hunterBytes + 0x370);
                ushort pathState = *(ushort*)(hunterBytes + 0xF2);
                ushort pathState2 = *(ushort*)(hunterBytes + 0xF4);
                int raw2Ac = *(int*)(hunterBytes + 0x2AC);
                ushort raw340 = *(ushort*)(hunterBytes + 0x340);
                ushort lastCommand = *(ushort*)(hunterBytes + 0x398);
                byte orderBlocked = *(byte*)(hunterBytes + 0x3FE);
                ushort targetUnitId = *(ushort*)(hunterBytes + 0x39A);
                uint targetGlobalId = *(uint*)(hunterBytes + 0x39C);
                string targetText = "none";
                string targetSignature = "0";

                if (targetUnitId != 0 &&
                    IsValidUnitId(targetUnitId) &&
                    unitApi.TryGetUnitById(targetUnitId, out GameUnit* target) &&
                    target != null)
                {
                    if (target->r_UnitChimp == eChimps.CHIMP_TYPE_CHICKEN)
                    {
                        // Prefer the target Vanilla actually stored over the
                        // last accepted candidate from a multi-candidate query.
                        hunterTargetEligibilityHook?.RecordAcceptedChickenTarget(
                            hunterUnitId,
                            targetUnitId,
                            target->r_GlobalId);
                    }

                    byte* targetBytes = (byte*)target;
                    ushort targetFlags = *(ushort*)(targetBytes + 0x92);
                    ushort targetAiState = *(ushort*)(targetBytes + 0x2BC);
                    ushort targetCorpseFlag = *(ushort*)(targetBytes + 0x29C);
                    ushort targetReservation = *(ushort*)(targetBytes + 0x448);
                    bool identityMatches = target->r_GlobalId == targetGlobalId;
                    int distance = Math.Max(
                        Math.Abs(target->r_CurrentTilePositionX - hunter->r_CurrentTilePositionX),
                        Math.Abs(target->r_CurrentTilePositionY - hunter->r_CurrentTilePositionY));
                    targetText =
                        $"{targetUnitId}/{targetGlobalId}/{target->r_GlobalId}/{target->r_UnitChimp}, " +
                        $"identityMatches={identityMatches}, owner={target->r_ControllableForPlayerId}, " +
                        $"aliveState={(short)target->r_AliveState}, health={target->r_CurrentHealth}/{target->r_MaxHealth}, " +
                        $"flags92={targetFlags}, aiState=0x{targetAiState:X}, corpseFlag={targetCorpseFlag}, " +
                        $"reservation={targetReservation}, tile={target->r_CurrentTilePositionX},{target->r_CurrentTilePositionY}, " +
                        $"distance={distance}";
                    targetSignature =
                        $"{targetUnitId}|{targetGlobalId}|{target->r_GlobalId}|{(int)target->r_UnitChimp}|{(short)target->r_AliveState}|" +
                        $"{target->r_CurrentHealth}|{targetFlags}|{targetAiState}|{targetCorpseFlag}|{targetReservation}|{identityMatches}";
                }
                else if (targetUnitId != 0)
                {
                    targetText = $"{targetUnitId}/{targetGlobalId}/invalid";
                    targetSignature = $"{targetUnitId}|{targetGlobalId}|invalid";
                }

                string cachedTargetText = activeHunterTargets.TryGetValue(hunterUnitId, out HunterTargetSnapshot cachedTarget)
                    ? $"{cachedTarget.UnitId}/{cachedTarget.GlobalId}"
                    : "none";
                string signature =
                    $"{currentAiState}|{stateTimer}|{wanderMode}|{pathState}|{pathState2}|{raw2Ac}|{raw340}|" +
                    $"{lastCommand}|{orderBlocked}|" +
                    $"{targetSignature}|{cachedTargetText}";
                string stateDescription =
                    $"aiState=0x{currentAiState:X},timer={stateTimer},wander={wanderMode},path={pathState}/{pathState2}," +
                    $"raw2AC={raw2Ac},raw340={raw340},lastCommand={lastCommand},orderBlocked={orderBlocked}," +
                    $"target={targetUnitId}/{targetGlobalId},cached={cachedTargetText}";
                if (onlyIfChanged &&
                    lastHunterLifecycleStateSignatures.TryGetValue(hunterUnitId, out string previousSignature) &&
                    string.Equals(previousSignature, signature, StringComparison.Ordinal))
                {
                    return;
                }

                string previousState = lastHunterLifecycleStateDescriptions.TryGetValue(hunterUnitId, out string priorDescription)
                    ? priorDescription
                    : "none";
                lastHunterLifecycleStateSignatures[hunterUnitId] = signature;
                lastHunterLifecycleStateDescriptions[hunterUnitId] = stateDescription;
                hunterLifecycleDiagnosticLogs++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters hunter lifecycle: source={source}, hunter={hunterUnitId}/{hunter->r_GlobalId}, " +
                    $"owner={hunter->r_ControllableForPlayerId}, aiState=0x{currentAiState:X}, " +
                    $"stateTimer={stateTimer}, wanderMode={wanderMode}, pathState={pathState}, pathState2={pathState2}, " +
                    $"raw2AC={raw2Ac}, raw340={raw340}, lastCommand={lastCommand}, orderBlocked={orderBlocked}, " +
                    $"hunterTile={hunter->r_CurrentTilePositionX},{hunter->r_CurrentTilePositionY}, " +
                    $"nativeTarget={targetText}, cachedTarget={cachedTargetText}, previous={previousState} " +
                    $"({hunterLifecycleDiagnosticLogs}/{MaxHunterLifecycleDiagnosticLogs}).");

                if (hunterLifecycleDiagnosticLogs == MaxHunterLifecycleDiagnosticLogs)
                {
                    Shared.DebugLogHelper.LogInfo(log, "Improved Hunters hunter lifecycle diagnostic limit reached.");
                }
            }
            catch (Exception exception)
            {
                hunterLifecycleDiagnosticLogs = MaxHunterLifecycleDiagnosticLogs;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters hunter lifecycle diagnostic failed and was disabled: {exception}");
            }
        }

        private unsafe void LogDiagnosticHunterStateChanges(SimpleNativeArray<GameUnit> units, string source)
        {
            if (!settings.EnableMod || diagnosticHunterIds.Count == 0)
                return;

            foreach (int hunterUnitId in diagnosticHunterIds)
            {
                if (hunterUnitId < 1 || hunterUnitId > units.Length)
                    continue;

                LogHunterLifecycleState(hunterUnitId, source, onlyIfChanged: true);
            }
        }

        private unsafe void WatchAcceptedChickenTarget(int hunterUnitId, int chickenUnitId)
        {
            if (watchedChickenStateDiagnosticFailureLogged)
                return;

            try
            {
                if (!IsValidUnitId(chickenUnitId) ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(chickenUnitId, out GameUnit* chicken) ||
                    chicken == null ||
                    chicken->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN)
                {
                    return;
                }

                uint globalId = chicken->r_GlobalId;
                bool added = !watchedChickenTargets.ContainsKey(globalId);
                if (added && watchedChickenTargets.Count >= MaxWatchedChickenTargets)
                {
                    if (!watchedChickenLimitLogged)
                    {
                        watchedChickenLimitLogged = true;
                        LogHunterLifecycleMessage(
                            $"Improved Hunters watched chicken target limit reached; retaining the first {MaxWatchedChickenTargets} targets");
                    }
                    return;
                }

                watchedChickenTargets[globalId] = new WatchedChickenTarget(hunterUnitId, chickenUnitId);
                if (added)
                    LogWatchedChickenState(chicken, hunterUnitId, chickenUnitId, globalId, "query-accepted", onlyIfChanged: false);
            }
            catch (Exception exception)
            {
                if (chickenTargetDiagnosticFailureLogged)
                    return;

                chickenTargetDiagnosticFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters accepted-chicken diagnostic failed; target behavior is unchanged: {exception}");
            }
        }

        private void TryLogWatchedChickenStateChanges(SimpleNativeArray<GameUnit> units)
        {
            if (watchedChickenStateDiagnosticFailureLogged)
                return;

            try
            {
                LogWatchedChickenStateChanges(units);
            }
            catch (Exception exception)
            {
                watchedChickenStateDiagnosticFailureLogged = true;
                watchedChickenTargets.Clear();
                lastChickenLifecycleStateSignatures.Clear();
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters watched-chicken state diagnostic failed and was disabled; native scan behavior is unchanged: {exception}");
            }
        }

        private unsafe void LogWatchedChickenStateChanges(SimpleNativeArray<GameUnit> units)
        {
            if (hunterLifecycleDiagnosticLogs >= MaxHunterLifecycleDiagnosticLogs || watchedChickenTargets.Count == 0)
                return;

            List<uint> staleGlobalIds = null;
            foreach (KeyValuePair<uint, WatchedChickenTarget> pair in watchedChickenTargets)
            {
                WatchedChickenTarget watched = pair.Value;
                if (watched.UnitId < 1 || watched.UnitId > units.Length)
                {
                    if (staleGlobalIds == null)
                        staleGlobalIds = new List<uint>();
                    staleGlobalIds.Add(pair.Key);
                    continue;
                }

                GameUnit* chicken = units.GetValuePointer(watched.UnitId - 1);
                if (chicken == null ||
                    chicken->r_GlobalId != pair.Key ||
                    chicken->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN)
                {
                    LogHunterLifecycleMessage(
                        $"Improved Hunters watched chicken disappeared or slot was reused: hunter={watched.HunterUnitId}, " +
                        $"candidate={watched.UnitId}/{pair.Key}.");
                    if (staleGlobalIds == null)
                        staleGlobalIds = new List<uint>();
                    staleGlobalIds.Add(pair.Key);
                    continue;
                }

                LogWatchedChickenState(
                    chicken,
                    watched.HunterUnitId,
                    watched.UnitId,
                    pair.Key,
                    "native-scan",
                    onlyIfChanged: true);
            }

            if (staleGlobalIds == null)
                return;

            for (int index = 0; index < staleGlobalIds.Count; index++)
            {
                watchedChickenTargets.Remove(staleGlobalIds[index]);
                lastChickenLifecycleStateSignatures.Remove(staleGlobalIds[index]);
            }
        }

        private unsafe void LogWatchedChickenState(
            GameUnit* chicken,
            int hunterUnitId,
            int chickenUnitId,
            uint chickenGlobalId,
            string source,
            bool onlyIfChanged)
        {
            if (chicken == null || hunterLifecycleDiagnosticLogs >= MaxHunterLifecycleDiagnosticLogs)
                return;

            byte* chickenBytes = (byte*)chicken;
            ushort flagsAt92 = *(ushort*)(chickenBytes + 0x92);
            ushort aiState = *(ushort*)(chickenBytes + 0x2BC);
            ushort corpseFlag = *(ushort*)(chickenBytes + 0x29C);
            ushort reservation = *(ushort*)(chickenBytes + 0x448);
            ushort deathTimer = *(ushort*)(chickenBytes + 0x2C4);
            string signature =
                $"{(short)chicken->r_AliveState}|{chicken->r_CurrentHealth}|{flagsAt92}|{aiState}|{corpseFlag}|{reservation}|" +
                $"{chicken->r_CurrentTilePositionX}|{chicken->r_CurrentTilePositionY}";
            if (onlyIfChanged &&
                lastChickenLifecycleStateSignatures.TryGetValue(chickenGlobalId, out string previousSignature) &&
                string.Equals(previousSignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            lastChickenLifecycleStateSignatures[chickenGlobalId] = signature;
            LogHunterLifecycleMessage(
                $"Improved Hunters watched chicken state: source={source}, hunter={hunterUnitId}, " +
                $"candidate={chickenUnitId}/{chickenGlobalId}, owner={chicken->r_ControllableForPlayerId}, " +
                $"color={chicken->r_SpritePlayerColorId}, aliveState={(short)chicken->r_AliveState}, " +
                $"health={chicken->r_CurrentHealth}/{chicken->r_MaxHealth}, flags92={flagsAt92}, " +
                $"aiState=0x{aiState:X}, corpseFlag={corpseFlag}, reservation={reservation}, " +
                $"deathTimer={deathTimer}, tile={chicken->r_CurrentTilePositionX},{chicken->r_CurrentTilePositionY}.");
        }

        private void LogHunterLifecycleMessage(string message)
        {
            if (hunterLifecycleDiagnosticLogs >= MaxHunterLifecycleDiagnosticLogs)
                return;

            hunterLifecycleDiagnosticLogs++;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"{message} ({hunterLifecycleDiagnosticLogs}/{MaxHunterLifecycleDiagnosticLogs}).");

            if (hunterLifecycleDiagnosticLogs == MaxHunterLifecycleDiagnosticLogs)
                Shared.DebugLogHelper.LogInfo(log, "Improved Hunters hunter lifecycle diagnostic limit reached.");
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

        private void LogHunterTargetQueryDiagnostic(
            int hunterUnitId,
            int queryUnitId,
            eChimps queryType,
            bool isValidTarget,
            bool usedFallback,
            TargetSelection targetSelection)
        {
            if (hunterTargetDiagnosticLogs >= MaxHunterTargetDiagnosticLogs)
                return;

            hunterTargetDiagnosticLogs++;
            BestTarget bestTarget = targetSelection.BestTarget;
            string bestText = bestTarget.UnitId == 0
                ? "none"
                : $"{bestTarget.UnitId}/{bestTarget.Type}/meat={bestTarget.MeatAmount}/huntPath={bestTarget.PathCost}/granaryRoundTrip={bestTarget.GranaryRoundTripCost}/hutWork={HunterHutWorkCost}/cycle={bestTarget.CycleCost}/allowedNearBest={targetSelection.AllowedCount}";

            log.LogInfo(
                $"Improved Hunters target query: hunter={hunterUnitId}, candidate={queryUnitId}/{queryType}, " +
                $"allowed={isValidTarget}, fallback={usedFallback}, best={bestText} " +
                $"({hunterTargetDiagnosticLogs}/{MaxHunterTargetDiagnosticLogs}).");

            if (hunterTargetDiagnosticLogs == MaxHunterTargetDiagnosticLogs)
                log.LogInfo("Improved Hunters target query diagnostic limit reached; continuing with periodic summaries only.");
        }

        private void LogHunterTargetQuerySummary()
        {
            long timestamp = Stopwatch.GetTimestamp();
            if (timestamp < nextHunterTargetSummaryTimestamp)
                return;

            nextHunterTargetSummaryTimestamp = timestamp + HunterTargetSummaryInterval;
            log.LogInfo(
                $"Improved Hunters target query summary: total={hunterTargetQueryEvents}, accepted={hunterTargetAcceptedEvents}, " +
                $"rejected={hunterTargetRejectedEvents}, searches={hunterTargetSearchStarts}, fallback={hunterTargetFallbackEvents}, noBest={hunterTargetNoBestEvents}, " +
                $"preyCache={preyCache.Count}, pathCache={pathCostCache.Count}, pathHits={pathCacheHits}, pathMisses={pathCacheMisses}.");
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

        private unsafe void OnProjectileSpawn(ProjectileSpawnEventArgs args)
        {
            LogChickenProjectileEvent(
                "spawn-post",
                args.AttackedUnitId,
                args.SourceUnitId,
                projectileId: args.ReturnValue,
                damage: null,
                details: $"projectileType={args.ProjectileType}, playerSource={args.PlayerSourceId}/{args.UnitPlayerSourceId}, " +
                $"sourceTile={args.SourceWorldTileX},{args.SourceWorldTileY}, targetTile={args.TargetWorldTileX},{args.TargetWorldTileY}");

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

            uint projectileGlobalId = 0;
            if (args.ReturnValue > 0 && args.ReturnValue <= int.MaxValue &&
                GameProjectileManagerAPI.Instance.TryGetProjectileById(
                    (int)args.ReturnValue,
                    out GameProjectile* projectile) &&
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
                LogSameOwnerChickenProjectileDelete(args);
            }
            catch (Exception exception)
            {
                if (chickenProjectileDamageFailureLogged)
                    return;

                chickenProjectileDamageFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters same-owner chicken projectile delete diagnostic failed; Vanilla deletion continues: {exception}");
            }
        }

        private unsafe void LogSameOwnerChickenProjectileDelete(ProjectileDeleteEventArgs args)
        {
            if (!settings.EnableMod || !settings.HuntChicken ||
                args.ProjectileId <= 0 || pendingHunterShotIntents.Count == 0)
                return;

            PendingHunterShotIntent matchedIntent = default;
            bool found = false;
            foreach (KeyValuePair<HunterShotIntentKey, PendingHunterShotIntent> pair in pendingHunterShotIntents)
            {
                if (pair.Value.SpawnReturnValue != args.ProjectileId)
                    continue;

                matchedIntent = pair.Value;
                found = true;
                break;
            }

            if (!found || matchedIntent.TargetType != eChimps.CHIMP_TYPE_CHICKEN)
                return;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (!TryGetMatchingProjectile(matchedIntent, out GameProjectile* projectile) ||
                matchedIntent.HunterUnitId == 0 ||
                !unitApi.TryGetUnitById(matchedIntent.HunterUnitId, out GameUnit* hunter) ||
                hunter == null ||
                hunter->r_GlobalId != matchedIntent.HunterGlobalId ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                hunter->r_AliveState != AliveState.IsAlive ||
                !unitApi.TryGetUnitById(matchedIntent.TargetUnitId, out GameUnit* target) ||
                target == null ||
                target->r_GlobalId != matchedIntent.TargetGlobalId ||
                target->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN ||
                target->r_AliveState != AliveState.IsAlive ||
                target->r_CurrentHealth == 0 ||
                hunter->r_ControllableForPlayerId == 0 ||
                hunter->r_ControllableForPlayerId != target->r_ControllableForPlayerId ||
                projectile->r_PlayerSourceId != hunter->r_ControllableForPlayerId)
            {
                return;
            }

            LogHunterProjectileDiagnostic(
                $"Improved Hunters same-owner chicken projectile reached delete without native hit: " +
                $"hunter={matchedIntent.HunterUnitId}/{matchedIntent.HunterGlobalId}, " +
                $"target={matchedIntent.TargetUnitId}/{matchedIntent.TargetGlobalId}, " +
                $"owner={target->r_ControllableForPlayerId}, projectile={args.ProjectileId}/{matchedIntent.ProjectileGlobalId}, " +
                $"projectileAliveState={(short)projectile->r_AliveState}, " +
                $"projectileCurrent={projectile->r_CurrentTileX},{projectile->r_CurrentTileY}, " +
                $"projectileTarget={projectile->r_TargetWorldTileX},{projectile->r_TargetWorldTileY}, " +
                $"unitWorld={target->r_CurrentWorldPositionX},{target->r_CurrentWorldPositionY}, " +
                $"activeDamageAttempts={matchedIntent.ActiveDamageAttempts}, " +
                $"targetAliveState={(short)target->r_AliveState}, currentHealth={target->r_CurrentHealth}.");
        }

        private unsafe void TryApplySameOwnerChickenDamageDuringFlight()
        {
            if (!settings.EnableMod || !settings.HuntChicken || pendingHunterShotIntents.Count == 0)
                return;

            List<HunterShotIntentKey> candidateKeys = null;
            foreach (KeyValuePair<HunterShotIntentKey, PendingHunterShotIntent> pair in pendingHunterShotIntents)
            {
                if (pair.Value.TargetType != eChimps.CHIMP_TYPE_CHICKEN ||
                    pair.Value.ActiveDamageAttempts >= MaxSameOwnerChickenProjectileDamageAttempts)
                {
                    continue;
                }

                if (candidateKeys == null)
                    candidateKeys = new List<HunterShotIntentKey>();

                candidateKeys.Add(pair.Key);
            }

            if (candidateKeys == null)
                return;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            for (int index = 0; index < candidateKeys.Count; index++)
            {
                HunterShotIntentKey key = candidateKeys[index];
                if (!pendingHunterShotIntents.TryGetValue(key, out PendingHunterShotIntent intent) ||
                    !TryGetMatchingProjectile(intent, out GameProjectile* projectile) ||
                    projectile->r_AliveState != AliveState.IsAlive ||
                    intent.HunterUnitId == 0 ||
                    !unitApi.TryGetUnitById(intent.HunterUnitId, out GameUnit* hunter) ||
                    hunter == null ||
                    hunter->r_GlobalId != intent.HunterGlobalId ||
                    hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                    hunter->r_AliveState != AliveState.IsAlive ||
                    !unitApi.TryGetUnitById(intent.TargetUnitId, out GameUnit* target) ||
                    target == null ||
                    target->r_GlobalId != intent.TargetGlobalId ||
                    target->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN ||
                    target->r_AliveState != AliveState.IsAlive ||
                    target->r_CurrentHealth == 0 ||
                    hunter->r_ControllableForPlayerId == 0 ||
                    hunter->r_ControllableForPlayerId != target->r_ControllableForPlayerId ||
                    projectile->r_PlayerSourceId != hunter->r_ControllableForPlayerId)
                {
                    continue;
                }

                int distanceToProjectileTarget = Math.Max(
                    Math.Abs((int)projectile->r_CurrentTileX - projectile->r_TargetWorldTileX),
                    Math.Abs((int)projectile->r_CurrentTileY - projectile->r_TargetWorldTileY));
                int distanceToUnit = Math.Max(
                    Math.Abs((int)projectile->r_CurrentTileX - target->r_CurrentWorldPositionX),
                    Math.Abs((int)projectile->r_CurrentTileY - target->r_CurrentWorldPositionY));
                if (distanceToUnit > SameOwnerChickenProjectileDamageDistance)
                {
                    continue;
                }

                // Mark the attempt before entering native code because ranged
                // damage can synchronously trigger projectile callbacks.
                intent = intent.WithActiveDamageAttempt();
                pendingHunterShotIntents[key] = intent;
                short projectileAliveState = (short)projectile->r_AliveState;
                ushort projectileCurrentX = projectile->r_CurrentTileX;
                ushort projectileCurrentY = projectile->r_CurrentTileY;
                ushort projectileTargetX = projectile->r_TargetWorldTileX;
                ushort projectileTargetY = projectile->r_TargetWorldTileY;
                ushort targetWorldX = target->r_CurrentWorldPositionX;
                ushort targetWorldY = target->r_CurrentWorldPositionY;
                uint owner = target->r_ControllableForPlayerId;
                bool damageApplied = unitApi.DamageUnitRanged(
                    intent.TargetUnitId,
                    (int)intent.SpawnReturnValue);
                bool targetIdentityValidAfter =
                    unitApi.TryGetUnitById(intent.TargetUnitId, out GameUnit* targetAfter) &&
                    targetAfter != null &&
                    targetAfter->r_GlobalId == intent.TargetGlobalId;
                uint currentHealth = targetIdentityValidAfter ? targetAfter->r_CurrentHealth : 0;
                bool targetKilled = targetIdentityValidAfter && currentHealth == 0;

                LogHunterProjectileDiagnostic(
                    $"Improved Hunters same-owner chicken active-flight ranged damage: " +
                    $"hunter={intent.HunterUnitId}/{intent.HunterGlobalId}, " +
                    $"target={intent.TargetUnitId}/{intent.TargetGlobalId}, " +
                    $"owner={owner}, " +
                    $"projectile={intent.SpawnReturnValue}/{intent.ProjectileGlobalId}, " +
                    $"attempt={intent.ActiveDamageAttempts}/{MaxSameOwnerChickenProjectileDamageAttempts}, " +
                    $"projectileAliveState={projectileAliveState}, " +
                    $"projectileCurrent={projectileCurrentX},{projectileCurrentY}, " +
                    $"projectileTarget={projectileTargetX},{projectileTargetY}, " +
                    $"unitWorld={targetWorldX},{targetWorldY}, " +
                    $"distanceToProjectileTarget={distanceToProjectileTarget}, distanceToUnit={distanceToUnit}, " +
                    $"damageApplied={damageApplied}, targetIdentityValidAfter={targetIdentityValidAfter}, " +
                    $"targetKilled={targetKilled}, currentHealth={currentHealth}.");

                if (targetKilled)
                    pendingHunterShotIntents.Remove(key);
            }
        }

        private void OnUnitTakeProjectileDamage(UnitTakeDamageByProjectileExEventArgs args)
        {
            LogChickenProjectileEvent(
                "damage-pre",
                args.AttackedUnitId,
                args.AttackingUnitId,
                args.ProjectileId,
                args.Damage,
                details: null);
        }

        private void OnUnitKilledByProjectile(UnitKilledByProjectileEventArgs args)
        {
            if (args.AttackedUnitId < int.MinValue || args.AttackedUnitId > int.MaxValue)
            {
                return;
            }

            LogChickenProjectileEvent(
                "kill-pre",
                (int)args.AttackedUnitId,
                attackerUnitId: 0,
                projectileId: args.ProjectileId,
                damage: null,
                details: null);
        }

        private unsafe void LogChickenProjectileEvent(
            string source,
            int attackedUnitId,
            int attackerUnitId,
            long projectileId,
            int? damage,
            string details)
        {
            try
            {
                if (!settings.EnableMod ||
                    !IsValidUnitId(attackedUnitId) ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(attackedUnitId, out GameUnit* target) ||
                    target == null ||
                    target->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN)
                {
                    return;
                }

                byte* targetBytes = (byte*)target;
                string suffix = string.IsNullOrEmpty(details) ? string.Empty : $", {details}";
                LogHunterProjectileDiagnostic(
                    $"Improved Hunters chicken projectile event: source={source}, attacked={attackedUnitId}/{target->r_GlobalId}, " +
                    $"attacker={attackerUnitId}, projectile={projectileId}, damage={(damage.HasValue ? damage.Value.ToString() : "none")}, " +
                    $"owner={target->r_ControllableForPlayerId}, aliveState={(short)target->r_AliveState}, " +
                    $"health={target->r_CurrentHealth}/{target->r_MaxHealth}, flags92={*(ushort*)(targetBytes + 0x92)}, " +
                    $"aiState=0x{*(ushort*)(targetBytes + 0x2BC):X}, corpseFlag={*(ushort*)(targetBytes + 0x29C)}, " +
                    $"reservation={*(ushort*)(targetBytes + 0x448)}{suffix}.");
            }
            catch (Exception exception)
            {
                if (chickenProjectileDiagnosticFailureLogged)
                    return;

                chickenProjectileDiagnosticFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters chicken projectile diagnostic failed; projectile behavior is unchanged: {exception}");
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

            return IsCompensableHunterPrey(target, out eligibility);
        }

        private static unsafe bool TryGetMatchingProjectile(
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

        private void QueuePendingHunterShotIntent(
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
            HunterShotIntentKey key = new HunterShotIntentKey(targetUnitId, targetGlobalId);
            PendingHunterShotIntent intent = new PendingHunterShotIntent(
                hunterUnitId,
                hunterGlobalId,
                targetUnitId,
                targetGlobalId,
                targetType,
                timestamp + PendingHunterShotIntentDelay,
                hunterSource,
                spawnReturnValue,
                projectileGlobalId);

            bool updatedExisting = pendingHunterShotIntents.ContainsKey(key);
            pendingHunterShotIntents[key] = intent;

            LogHunterProjectileDiagnostic(
                $"Improved Hunters hunter shot intent queued: hunter={hunterUnitId}, target={targetUnitId}/{targetType}, " +
                $"targetGlobalId={targetGlobalId}, delaySeconds={PendingHunterShotIntentDelay / Stopwatch.Frequency}, " +
                $"hunterSource={hunterSource}, returnValue={spawnReturnValue}, " +
                $"projectileGlobalId={projectileGlobalId}, updated={updatedExisting}.");
        }

        private unsafe void ResolvePendingHunterShotIntents(long timestamp)
        {
            if (pendingHunterShotIntents.Count == 0)
                return;

            List<HunterShotIntentKey> dueKeys = null;
            foreach (KeyValuePair<HunterShotIntentKey, PendingHunterShotIntent> pair in pendingHunterShotIntents)
            {
                if (timestamp < pair.Value.DueAt)
                    continue;

                if (dueKeys == null)
                    dueKeys = new List<HunterShotIntentKey>();

                dueKeys.Add(pair.Key);
            }

            if (dueKeys == null)
                return;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            for (int index = 0; index < dueKeys.Count; index++)
            {
                HunterShotIntentKey key = dueKeys[index];
                if (!pendingHunterShotIntents.TryGetValue(key, out PendingHunterShotIntent intent))
                    continue;

                pendingHunterShotIntents.Remove(key);
                ResolvePendingHunterShotIntent(unitApi, intent);
            }
        }

        private unsafe void ResolvePendingHunterShotIntent(GameUnitManagerAPI unitApi, PendingHunterShotIntent intent)
        {
            if (intent.HunterUnitId != 0 &&
                (!unitApi.TryGetUnitById(intent.HunterUnitId, out GameUnit* hunter) ||
                hunter == null ||
                hunter->r_GlobalId != intent.HunterGlobalId ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER))
            {
                LogHunterProjectileDiagnostic(
                    $"Improved Hunters hunter shot intent skipped: hunter={intent.HunterUnitId}, " +
                    $"target={intent.TargetUnitId}/{intent.TargetType}, reason=hunter-invalid.");
                return;
            }

            if (!unitApi.TryGetUnitById(intent.TargetUnitId, out GameUnit* target) ||
                target == null ||
                target->r_GlobalId != intent.TargetGlobalId)
            {
                LogHunterProjectileDiagnostic(
                    $"Improved Hunters hunter shot intent skipped: hunter={intent.HunterUnitId}, " +
                    $"target={intent.TargetUnitId}/{intent.TargetType}, reason=target-missing-or-reused.");
                return;
            }

            if (!IsCompensableHunterPrey(target, out PreyEligibility eligibility))
            {
                LogHunterProjectileDiagnostic(
                    $"Improved Hunters hunter shot intent skipped: hunter={intent.HunterUnitId}, " +
                    $"target={intent.TargetUnitId}/{intent.TargetType}, reason=target-invalid-or-already-dead, " +
                    $"aliveState={(short)target->r_AliveState}, currentHealth={target->r_CurrentHealth}.");
                return;
            }

            GameUnit* sameOwnerHunter = null;
            bool sameOwnerChicken =
                intent.TargetType == eChimps.CHIMP_TYPE_CHICKEN &&
                intent.HunterUnitId != 0 &&
                unitApi.TryGetUnitById(intent.HunterUnitId, out sameOwnerHunter) &&
                sameOwnerHunter != null &&
                sameOwnerHunter->r_GlobalId == intent.HunterGlobalId &&
                sameOwnerHunter->r_ControllableForPlayerId != 0 &&
                sameOwnerHunter->r_ControllableForPlayerId == target->r_ControllableForPlayerId;
            bool rangedDamageAttempted = sameOwnerChicken &&
                TryGetMatchingProjectile(intent, out GameProjectile* rangedProjectile) &&
                rangedProjectile->r_AliveState == AliveState.IsAlive &&
                rangedProjectile->r_PlayerSourceId == sameOwnerHunter->r_ControllableForPlayerId;
            bool rangedDamageApplied = rangedDamageAttempted &&
                unitApi.DamageUnitRanged(intent.TargetUnitId, (int)intent.SpawnReturnValue);
            bool rangedDamageKilledTarget = rangedDamageAttempted &&
                unitApi.TryGetUnitById(intent.TargetUnitId, out GameUnit* targetAfterRangedDamage) &&
                targetAfterRangedDamage != null &&
                targetAfterRangedDamage->r_GlobalId == intent.TargetGlobalId &&
                targetAfterRangedDamage->r_CurrentHealth == 0;

            // The regular blocked-arrow fallback remains unchanged for neutral,
            // enemy, and all non-chicken prey. Do not synthesize a same-owner
            // chicken corpse after native ranged damage was rejected.
            bool killFallback = !sameOwnerChicken && !rangedDamageKilledTarget;
            if (killFallback)
                unitApi.KillUnit(intent.TargetUnitId);

            bool corpseFinalized = false;
            bool stillAlive =
                unitApi.TryGetUnitById(intent.TargetUnitId, out target) &&
                target != null &&
                target->r_GlobalId == intent.TargetGlobalId;

            ushort aiState = 0;
            ushort corpseFlag = 0;
            ushort reservation = 0;
            uint currentHealth = 0;
            if (stillAlive)
            {
                byte* targetBytes = (byte*)target;
                if (killFallback && target->r_CurrentHealth == 0)
                    corpseFinalized = TryFinalizeShotIntentCorpse(target, intent.TargetType);

                aiState = *(ushort*)(targetBytes + 0x2BC);
                corpseFlag = *(ushort*)(targetBytes + 0x29C);
                reservation = *(ushort*)(targetBytes + 0x448);
                currentHealth = target->r_CurrentHealth;
                stillAlive =
                    target->r_AliveState == AliveState.IsAlive &&
                    target->r_CurrentHealth > 0 &&
                    corpseFlag == 0;
            }

            LogHunterProjectileDiagnostic(
                $"Improved Hunters hunter shot intent resolved: hunter={intent.HunterUnitId}, " +
                $"target={intent.TargetUnitId}/{eligibility.Type}, targetGlobalId={intent.TargetGlobalId}, " +
                $"hunterSource={intent.HunterSource}, returnValue={intent.SpawnReturnValue}, " +
                $"projectileGlobalId={intent.ProjectileGlobalId}, " +
                $"sameOwnerChicken={sameOwnerChicken}, rangedDamageAttempted={rangedDamageAttempted}, " +
                $"rangedDamageApplied={rangedDamageApplied}, rangedDamageKilledTarget={rangedDamageKilledTarget}, " +
                $"activeDamageAttempts={intent.ActiveDamageAttempts}, " +
                $"killFallback={killFallback}, " +
                $"corpseFinalized={corpseFinalized}, stillAlive={stillAlive}, currentHealth={currentHealth}, " +
                $"aiState=0x{aiState:X}, corpseFlag={corpseFlag}, reservation={reservation}.");
        }

        private static unsafe bool TryFinalizeShotIntentCorpse(GameUnit* target, eChimps targetType)
        {
            if (target == null ||
                target->r_UnitChimp != targetType ||
                target->r_AliveState != AliveState.IsAlive)
            {
                return false;
            }

            byte* targetBytes = (byte*)target;
            target->r_CurrentHealth = 0;
            *(ushort*)(targetBytes + 0x29C) = 1;
            *(ushort*)(targetBytes + 0x2BC) = HunterFreshCorpseAiState;

            // Start at zero so the visible-corpse preserve can take over on the
            // next native scan.
            if (IsShortLivedPrey(targetType))
                *(ushort*)(targetBytes + 0x2C4) = 0;

            UpdateUnitHealthDisplay(target);
            return true;
        }

        private void LogHunterProjectileDiagnostic(string message)
        {
            if (hunterProjectileDiagnosticLogs >= MaxHunterProjectileDiagnosticLogs)
                return;

            hunterProjectileDiagnosticLogs++;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"{message} ({hunterProjectileDiagnosticLogs}/{MaxHunterProjectileDiagnosticLogs}).");

            if (hunterProjectileDiagnosticLogs == MaxHunterProjectileDiagnosticLogs)
                Shared.DebugLogHelper.LogInfo(log, "Improved Hunters hunter projectile diagnostic limit reached.");
        }

        private void OnSettingChanged(string propertyName)
        {
            ClearTargetSelectionCaches();

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
                    referenceHashMatches: true,
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

        private unsafe bool IsCompensableHunterPrey(GameUnit* prey, out PreyEligibility eligibility)
        {
            return TryGetPreyEligibility(prey, out eligibility) &&
                eligibility.KnownAnimal &&
                eligibility.RuntimeHuntingEnabled &&
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
            pathCostCache.Clear();
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
            hunterTargetQueryEvents = 0;
            hunterTargetAcceptedEvents = 0;
            hunterTargetRejectedEvents = 0;
            hunterTargetFallbackEvents = 0;
            hunterTargetNoBestEvents = 0;
            hunterTargetSearchStarts = 0;
            pathCacheHits = 0;
            pathCacheMisses = 0;
        }

        private struct PreyEligibility
        {
            public bool KnownAnimal;
            public bool RuntimeHuntingEnabled;
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

        private struct GranaryCandidate
        {
            public readonly int BuildingId;
            public readonly uint GlobalId;
            public readonly int TileX;
            public readonly int TileY;
            public readonly int HeuristicDistance;

            public GranaryCandidate(int buildingId, uint globalId, int tileX, int tileY, int heuristicDistance)
            {
                BuildingId = buildingId;
                GlobalId = globalId;
                TileX = tileX;
                TileY = tileY;
                HeuristicDistance = heuristicDistance;
            }
        }

        private struct BestTarget
        {
            public readonly int UnitId;
            public readonly uint GlobalId;
            public readonly eChimps Type;
            public readonly int MeatAmount;
            public readonly int PathCost;
            public readonly int GranaryRoundTripCost;
            public readonly int CycleCost;

            public BestTarget(int unitId, uint globalId, eChimps type, int meatAmount, int pathCost, int granaryRoundTripCost, int cycleCost)
            {
                UnitId = unitId;
                GlobalId = globalId;
                Type = type;
                MeatAmount = meatAmount;
                PathCost = pathCost;
                GranaryRoundTripCost = granaryRoundTripCost;
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

        private struct CachedPathCost
        {
            public readonly int Cost;
            public readonly long ExpiresAt;

            public CachedPathCost(int cost, long expiresAt)
            {
                Cost = cost;
                ExpiresAt = expiresAt;
            }
        }

        private struct PathCostKey : IEquatable<PathCostKey>
        {
            private readonly int originTileId;
            private readonly uint targetGlobalId;
            private readonly int targetTileId;

            public PathCostKey(int originTileId, uint targetGlobalId, int targetTileId)
            {
                this.originTileId = originTileId;
                this.targetGlobalId = targetGlobalId;
                this.targetTileId = targetTileId;
            }

            public bool Equals(PathCostKey other)
            {
                return originTileId == other.originTileId &&
                    targetGlobalId == other.targetGlobalId &&
                    targetTileId == other.targetTileId;
            }

            public override bool Equals(object obj)
            {
                return obj is PathCostKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + originTileId;
                    hash = hash * 31 + targetGlobalId.GetHashCode();
                    hash = hash * 31 + targetTileId;
                    return hash;
                }
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

        private readonly struct WatchedChickenTarget
        {
            public readonly int HunterUnitId;
            public readonly int UnitId;

            public WatchedChickenTarget(int hunterUnitId, int unitId)
            {
                HunterUnitId = hunterUnitId;
                UnitId = unitId;
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

            public HunterShotIntentKey(int targetUnitId, uint targetGlobalId)
            {
                this.targetUnitId = targetUnitId;
                this.targetGlobalId = targetGlobalId;
            }

            public bool Equals(HunterShotIntentKey other)
            {
                return targetUnitId == other.targetUnitId &&
                    targetGlobalId == other.targetGlobalId;
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
            public readonly long DueAt;
            public readonly string HunterSource;
            public readonly long SpawnReturnValue;
            public readonly uint ProjectileGlobalId;
            public readonly int ActiveDamageAttempts;

            public PendingHunterShotIntent(
                int hunterUnitId,
                uint hunterGlobalId,
                int targetUnitId,
                uint targetGlobalId,
                eChimps targetType,
                long dueAt,
                string hunterSource,
                long spawnReturnValue,
                uint projectileGlobalId,
                int activeDamageAttempts = 0)
            {
                HunterUnitId = hunterUnitId;
                HunterGlobalId = hunterGlobalId;
                TargetUnitId = targetUnitId;
                TargetGlobalId = targetGlobalId;
                TargetType = targetType;
                DueAt = dueAt;
                HunterSource = hunterSource;
                SpawnReturnValue = spawnReturnValue;
                ProjectileGlobalId = projectileGlobalId;
                ActiveDamageAttempts = activeDamageAttempts;
            }

            public PendingHunterShotIntent WithActiveDamageAttempt()
            {
                return new PendingHunterShotIntent(
                    HunterUnitId,
                    HunterGlobalId,
                    TargetUnitId,
                    TargetGlobalId,
                    TargetType,
                    DueAt,
                    HunterSource,
                    SpawnReturnValue,
                    ProjectileGlobalId,
                    ActiveDamageAttempts + 1);
            }
        }

        public void Dispose()
        {
            hunterTargetEligibilityHook?.Dispose();
            hunterTargetEligibilityHook = null;
            settings.SettingChanged -= OnSettingChanged;

            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            hunterPreyTypes.Clear();
            nextIdleHunterRequeryTimestamps.Clear();
            loggedCollectedCorpseGlobalIds.Clear();
            pendingHunterShotIntents.Clear();
            diagnosticHunterIds.Clear();
            lastHunterLifecycleStateSignatures.Clear();
            lastHunterLifecycleStateDescriptions.Clear();
            watchedChickenTargets.Clear();
            lastChickenLifecycleStateSignatures.Clear();
            ClearTargetSelectionCaches();
            nativeScanFailureLogged = false;
            hunterLifecycleDiagnosticLogs = 0;
            hunterTargetAbortDiagnosticLogs = 0;
            chickenTargetDiagnosticFailureLogged = false;
            watchedChickenStateDiagnosticFailureLogged = false;
            chickenProjectileDiagnosticFailureLogged = false;
            chickenProjectileDamageFailureLogged = false;
            watchedChickenLimitLogged = false;
            nextNativeScanTimestamp = 0;

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
