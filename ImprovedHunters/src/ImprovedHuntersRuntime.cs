using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
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
using Zhuqiaomon.Memory.Scanners;

namespace ImprovedHunters
{
    internal sealed class ImprovedHuntersRuntime : IDisposable
    {
        private const short RabbitCorpseDespawnTicks = 2400;
        private const short ExtraCorpseDespawnTicks = 2400;
        private const ushort CollectedCorpseDespawnTicks = 2401;
        private const int HunterSearchRadius = 20;
        private const int HunterTargetCandidateRadius = 54;
        private const int MaxPathCandidatesPerHunter = 24;
        private const int HunterHutWorkCost = 600;
        private const int BestTargetToleranceCost = 80;
        private const int VisualCorpseTimerResetThreshold = 120;
        private const int DefaultPreyHandlingCost = 100;
        private const int MaxPreyCacheDiagnosticLogs = 120;
        private const int MaxHunterTargetDiagnosticLogs = 160;
        private const int MaxHunterProjectileDiagnosticLogs = 160;
        private const int MaxCorpseDiagnosticLogs = 80;
        private const int CorpsePickupFallbackRadius = 8;
        private const ushort HunterCorpsePickupAiState = 0x6E;
        private const ushort HunterFreshCorpseAiState = 0x6F;
        private const string CamelDespawnTickTimePattern = "66 83 FE 6E 75 4D FE 84 2B 86 09 00 00 B9 ? ? ? ? 38 8C 2B 86 09 00 00";
        private const string ChickenDespawnTickTimePattern = "66 83 FF 6E 75 55 FE 84 2B 86 09 00 00 B9 ? ? ? ? 66 FF 84 2B 20 09 00 00";
        private const int ExtraDespawnPatternImmediateOffset = 13;
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
        private static readonly long RecentHunterTargetRetention = Stopwatch.Frequency * 10;
        private static readonly long RuntimeCorpsePreserveDuration = Stopwatch.Frequency * 60;

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
        private readonly Dictionary<int, RecentHunterTargetSnapshot> recentHunterTargets = new Dictionary<int, RecentHunterTargetSnapshot>();
        private readonly Dictionary<uint, long> preservedShortLivedCorpseExpirations = new Dictionary<uint, long>();
        private readonly Dictionary<HunterPreyCooldownKey, long> abortedTargetCooldowns = new Dictionary<HunterPreyCooldownKey, long>();
        private readonly Dictionary<int, long> lastHunterQueryTimestamps = new Dictionary<int, long>();
        private readonly Dictionary<int, long> hunterMeatPickupTimestamps = new Dictionary<int, long>();
        private readonly Dictionary<HunterShotIntentKey, PendingHunterShotIntent> pendingHunterShotIntents = new Dictionary<HunterShotIntentKey, PendingHunterShotIntent>();

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
        private int corpseDiagnosticLogs;
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

            subscriptions.Add(UnitR3EventHooks.OnUnitHunterQueryTarget.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnHunterQueryTarget));
            subscriptions.Add(UnitR3EventHooks.OnCalculateBonusYield.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnCalculateBonusYield));
            subscriptions.Add(UnitR3EventHooks.OnUnitCreate.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnUnitCreate));
            subscriptions.Add(UnitR3EventHooks.OnHunterPickUpMeat.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnHunterPickUpMeat));
            subscriptions.Add(UnitR3EventHooks.OnHunterDropOffMeat.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnHunterDropOffMeat));
            subscriptions.Add(ProjectileR3EventHooks.OnProjectileSpawn.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnProjectileSpawn));
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
            log.LogInfo("Improved Hunters runtime enabled.");
        }

        public unsafe void RunNativeScan(bool force = false)
        {
            long timestamp = Stopwatch.GetTimestamp();
            if (!applied || (!force && timestamp < nextNativeScanTimestamp))
                return;

            nextNativeScanTimestamp = timestamp + NativeScanInterval;

            try
            {
                ApplyDespawnPatches();
                ApplyCamelHealthPatch();
                ResolvePendingHunterShotIntents(timestamp);

                SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
                if (units._array == null || units.Length == 0)
                    return;

                List<IntPtr> hunters = new List<IntPtr>();
                List<IntPtr> eligiblePrey = new List<IntPtr>();
                int adjustedLiveCamels = 0;

                for (int index = 0; index < units.Length; index++)
                {
                    GameUnit* unit = units.GetValuePointer(index);
                    int unitId = index + 1;

                    if (TryClampLiveCamelHealth(unitId, unit))
                        adjustedLiveCamels++;

                    if (settings.EnableMod &&
                        unit->r_UnitChimp == eChimps.CHIMP_TYPE_CHICKEN)
                    {
                        NeutralizePlayerOwnedChicken(unit);
                    }

                    if (settings.EnableMod &&
                        IsRuntimeHuntingEnabled(unit->r_UnitChimp) &&
                        IsOwnerAllowedForAnyHunter(unitId, unit))
                    {
                        PreserveShortLivedCorpse(unitId, unit, timestamp);
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

                TrackHunterPreyState(units, hunters, timestamp);
                RequeryIdleHuntersNearPrey(units, hunters, eligiblePrey, timestamp);
            }
            catch (Exception exception)
            {
                if (nativeScanFailureLogged)
                    return;

                log.LogWarning($"Improved Hunters native scan failed: {exception}");
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

        private unsafe void TrackHunterPreyState(
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
            }
        }

        private unsafe void PreserveShortLivedCorpse(int unitId, GameUnit* unit, long timestamp)
        {
            if (!IsShortLivedPrey(unit->r_UnitChimp))
                return;

            byte* unitBytes = (byte*)unit;
            if (!IsPreservableCorpseState(*(ushort*)(unitBytes + 0x2BC)) ||
                *(ushort*)(unitBytes + 0x29C) == 0)
            {
                preservedShortLivedCorpseExpirations.Remove(unit->r_GlobalId);
                return;
            }

            ushort reservation = *(ushort*)(unitBytes + 0x448);
            if (reservation != 0 && reservation != 2)
                return;

            uint globalId = unit->r_GlobalId;
            if (!preservedShortLivedCorpseExpirations.TryGetValue(globalId, out long expiresAt))
            {
                expiresAt = timestamp + RuntimeCorpsePreserveDuration;
                preservedShortLivedCorpseExpirations[globalId] = expiresAt;
                LogCorpseDiagnostic(
                    $"Improved Hunters runtime corpse preserve start: unit={unitId}/{unit->r_UnitChimp}, " +
                    $"globalId={globalId}, aiState=0x{*(ushort*)(unitBytes + 0x2BC):X}, reservation={reservation}, " +
                    $"durationSeconds={RuntimeCorpsePreserveDuration / Stopwatch.Frequency}.");
            }

            if (timestamp >= expiresAt)
            {
                preservedShortLivedCorpseExpirations.Remove(globalId);
                return;
            }

            ushort deathTimer = *(ushort*)(unitBytes + 0x2C4);
            if (deathTimer > 0 && deathTimer < CollectedCorpseDespawnTicks)
            {
                *(ushort*)(unitBytes + 0x2C4) = 0;
                if (deathTimer < VisualCorpseTimerResetThreshold)
                {
                    LogCorpseDiagnostic(
                        $"Improved Hunters runtime corpse timer reset: unit={unitId}/{unit->r_UnitChimp}, " +
                        $"globalId={globalId}, timer={deathTimer}, aiState=0x{*(ushort*)(unitBytes + 0x2BC):X}, reservation={reservation}.");
                }
            }
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
            ClearTargetSelectionCaches();
            nativeScanFailureLogged = false;
            RunNativeScan(force: true);
        }

        private void OnHunterQueryTarget(UnitHunterQueryTargetEventArgs args)
        {
            if (!settings.EnableMod)
                return;

            long timestamp = Stopwatch.GetTimestamp();
            TrackHunterSearchQuery(args.HunterUnitId, timestamp);

            if (!IsValidUnitId(args.QueryUnitId))
                return;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            eChimps queryType = unitApi.GetType(args.QueryUnitId);
            if (!settings.IsKnownAnimal(queryType) || !IsRuntimeHuntingEnabled(queryType))
                return;

            if (IsValidUnitId(args.HunterUnitId) &&
                !IsOwnerAllowed(unitApi.GetOwner(args.HunterUnitId), args.QueryUnitId, queryType))
            {
                return;
            }

            bool isValidTarget = true;
            bool usedFallback = true;
            TargetSelection targetSelection = default;
            BestTarget bestTarget = default;
            if (!settings.ImprovedPathfinding)
            {
                usedFallback = false;
            }
            else if (IsValidUnitId(args.HunterUnitId) &&
                TryGetTargetSelectionForHunter(args.HunterUnitId, timestamp, out targetSelection))
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
                if (IsValidUnitId(args.HunterUnitId))
                    hunterPreyTypes[args.HunterUnitId] = queryType;
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

                if (!IsOwnerAllowed(hunterOwner, prey.UnitId, prey.Type))
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
            int releasedReservations = 0;
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
                    continue;

                byte* preyBytes = (byte*)unit;
                *(ushort*)(preyBytes + 0x448) = 0;
                releasedReservations++;

                eligibility.Reservation = 0;
                eligibility.Eligible =
                    eligibility.OwnerAllowed &&
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
            hunterMeatPickupTimestamps[args.UnitId] = Stopwatch.GetTimestamp();
            TryDeleteCollectedShortLivedCorpse(args.UnitId);
            activeHunterTargets.Remove(args.UnitId);
            recentHunterTargets.Remove(args.UnitId);
            bestTargetCache.Remove(args.UnitId);
        }

        private void OnHunterDropOffMeat(UnitHunterDropOffMeatEventArgs args)
        {
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
            GameUnit* unit = null;
            HunterTargetSnapshot target = default;
            string source = "hunter-target";

            if (!TryGetCollectedCorpseTarget(hunterUnitId, out target) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(target.UnitId, out unit) ||
                unit == null ||
                unit->r_GlobalId != target.GlobalId ||
                !IsShortLivedPrey(unit->r_UnitChimp))
            {
                if (!TryFindCollectedCorpseNearHunter(hunterUnitId, out target, out unit))
                {
                    LogCorpseDiagnostic($"Improved Hunters collected corpse remove skipped: hunter={hunterUnitId}, reason=no-target-or-nearby-corpse.");
                    return;
                }

                source = "nearby-corpse";
            }

            byte* unitBytes = (byte*)unit;
            if (*(ushort*)(unitBytes + 0x29C) == 0)
            {
                LogCorpseDiagnostic(
                    $"Improved Hunters collected corpse remove skipped: hunter={hunterUnitId}, target={target.UnitId}, " +
                    $"globalId={target.GlobalId}, reason=corpse-flag-zero, aiState=0x{*(ushort*)(unitBytes + 0x2BC):X}.");
                return;
            }

            *(ushort*)(unitBytes + 0x2C4) = CollectedCorpseDespawnTicks;
            unit->r_AliveState = AliveState.MarkedForDeletion;
            preservedShortLivedCorpseExpirations.Remove(target.GlobalId);

            if (loggedCollectedCorpseGlobalIds.Add(target.GlobalId) &&
                hunterTargetDiagnosticLogs < MaxHunterTargetDiagnosticLogs)
            {
                log.LogInfo(
                    $"Improved Hunters collected corpse removed: hunter={hunterUnitId}, target={target.UnitId}, " +
                    $"globalId={target.GlobalId}, aiState=0x{*(ushort*)(unitBytes + 0x2BC):X}, source={source}.");
            }
        }

        private unsafe bool TryFindCollectedCorpseNearHunter(
            int hunterUnitId,
            out HunterTargetSnapshot target,
            out GameUnit* corpse)
        {
            target = default;
            corpse = null;

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(hunterUnitId, out GameUnit* hunter) ||
                hunter == null ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
            {
                return false;
            }

            int hunterTileX = hunter->r_CurrentTilePositionX;
            int hunterTileY = hunter->r_CurrentTilePositionY;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            if (units._array == null || units.Length == 0)
                return false;

            int bestUnitId = 0;
            uint bestGlobalId = 0;
            int bestScore = int.MaxValue;
            GameUnit* bestCorpse = null;
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* candidate = units.GetValuePointer(index);
                if (candidate == null ||
                    candidate->r_AliveState != AliveState.IsAlive ||
                    !IsShortLivedPrey(candidate->r_UnitChimp))
                {
                    continue;
                }

                byte* candidateBytes = (byte*)candidate;
                if (*(ushort*)(candidateBytes + 0x29C) == 0)
                    continue;

                int distance = Math.Max(
                    Math.Abs(candidate->r_CurrentTilePositionX - hunterTileX),
                    Math.Abs(candidate->r_CurrentTilePositionY - hunterTileY));
                if (distance > CorpsePickupFallbackRadius)
                    continue;

                ushort reservation = *(ushort*)(candidateBytes + 0x448);
                int score = (distance * 10) + (reservation == 2 ? 0 : 5);
                if (score >= bestScore)
                    continue;

                bestScore = score;
                bestUnitId = index + 1;
                bestGlobalId = candidate->r_GlobalId;
                bestCorpse = candidate;
            }

            if (bestCorpse == null)
                return false;

            target = new HunterTargetSnapshot(checked((ushort)bestUnitId), bestGlobalId);
            corpse = bestCorpse;
            return true;
        }

        private unsafe bool TryGetCollectedCorpseTarget(int hunterUnitId, out HunterTargetSnapshot target)
        {
            if (activeHunterTargets.TryGetValue(hunterUnitId, out target))
                return true;

            if (recentHunterTargets.TryGetValue(hunterUnitId, out RecentHunterTargetSnapshot recentTarget))
            {
                long timestamp = Stopwatch.GetTimestamp();
                if (timestamp <= recentTarget.ExpiresAt)
                {
                    target = recentTarget.Target;
                    return true;
                }

                recentHunterTargets.Remove(hunterUnitId);
            }

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

            target = default;
            return false;
        }

        private void TrackHunterTargetState(int hunterUnitId, ushort targetUnitId, uint targetGlobalId, long timestamp)
        {
            if (!settings.EnableMod || !settings.ImprovedPathfinding)
                return;

            if (targetUnitId != 0 && targetGlobalId != 0)
            {
                activeHunterTargets[hunterUnitId] = new HunterTargetSnapshot(targetUnitId, targetGlobalId);
                return;
            }

            if (!activeHunterTargets.TryGetValue(hunterUnitId, out HunterTargetSnapshot previousTarget))
                return;

            activeHunterTargets.Remove(hunterUnitId);
            recentHunterTargets[hunterUnitId] = new RecentHunterTargetSnapshot(previousTarget, timestamp + RecentHunterTargetRetention);
            abortedTargetCooldowns[new HunterPreyCooldownKey(hunterUnitId, previousTarget.GlobalId)] = timestamp + AbortedTargetCooldownInterval;
            bestTargetCache.Remove(hunterUnitId);

            if (hunterTargetDiagnosticLogs < MaxHunterTargetDiagnosticLogs)
            {
                log.LogInfo(
                    $"Improved Hunters target abort: hunter={hunterUnitId}, target={previousTarget.UnitId}, " +
                    $"globalId={previousTarget.GlobalId}, cooldownSeconds={AbortedTargetCooldownInterval / Stopwatch.Frequency}.");
            }
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

        private void OnUnitCreate(UnitCreateEventArgs args)
        {
            if (!settings.EnableMod ||
                !settings.HuntChicken ||
                args.UnitType != eChimps.CHIMP_TYPE_CHICKEN ||
                args.PlayerOwnerId == 0)
            {
                return;
            }

            args.PlayerOwnerId = 0;
            args.PlayerColorId = 0;
        }

        private unsafe void OnProjectileSpawn(ProjectileSpawnEventArgs args)
        {
            if (!TryGetCompensableProjectileTarget(args, out _, out PreyEligibility eligibility))
                return;

            bool hasHunterContext = TryResolveHunterForProjectile(
                args.SourceUnitId,
                args.AttackedUnitId,
                eligibility.GlobalId,
                out int hunterUnitId,
                out uint hunterGlobalId,
                out string hunterSource);
            if (!hasHunterContext)
                hunterSource = "animal-arrow-fallback";

            QueuePendingHunterShotIntent(
                hunterUnitId,
                hunterGlobalId,
                args.AttackedUnitId,
                eligibility.GlobalId,
                eligibility.Type,
                hunterSource,
                args.ReturnValue);
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

        private void QueuePendingHunterShotIntent(
            int hunterUnitId,
            uint hunterGlobalId,
            int targetUnitId,
            uint targetGlobalId,
            eChimps targetType,
            string hunterSource,
            long spawnReturnValue)
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
                spawnReturnValue);

            bool updatedExisting = pendingHunterShotIntents.ContainsKey(key);
            pendingHunterShotIntents[key] = intent;

            LogHunterProjectileDiagnostic(
                $"Improved Hunters hunter shot intent queued: hunter={hunterUnitId}, target={targetUnitId}/{targetType}, " +
                $"targetGlobalId={targetGlobalId}, delaySeconds={PendingHunterShotIntentDelay / Stopwatch.Frequency}, " +
                $"hunterSource={hunterSource}, returnValue={spawnReturnValue}, updated={updatedExisting}.");
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

            if (!IsCompensableHunterPrey(intent.TargetUnitId, target, out PreyEligibility eligibility))
            {
                LogHunterProjectileDiagnostic(
                    $"Improved Hunters hunter shot intent skipped: hunter={intent.HunterUnitId}, " +
                    $"target={intent.TargetUnitId}/{intent.TargetType}, reason=target-invalid-or-already-dead, " +
                    $"aliveState={(short)target->r_AliveState}, currentHealth={target->r_CurrentHealth}.");
                return;
            }

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
                if (target->r_CurrentHealth == 0)
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
                $"Improved Hunters hunter shot intent kill: hunter={intent.HunterUnitId}, " +
                $"target={intent.TargetUnitId}/{eligibility.Type}, targetGlobalId={intent.TargetGlobalId}, " +
                $"hunterSource={intent.HunterSource}, returnValue={intent.SpawnReturnValue}, " +
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
            log.LogInfo($"{message} ({hunterProjectileDiagnosticLogs}/{MaxHunterProjectileDiagnosticLogs}).");

            if (hunterProjectileDiagnosticLogs == MaxHunterProjectileDiagnosticLogs)
                log.LogInfo("Improved Hunters hunter projectile diagnostic limit reached.");
        }

        private void LogCorpseDiagnostic(string message)
        {
            if (corpseDiagnosticLogs >= MaxCorpseDiagnosticLogs)
                return;

            corpseDiagnosticLogs++;
            log.LogInfo($"{message} ({corpseDiagnosticLogs}/{MaxCorpseDiagnosticLogs}).");

            if (corpseDiagnosticLogs == MaxCorpseDiagnosticLogs)
                log.LogInfo("Improved Hunters corpse diagnostic limit reached.");
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

            camelDespawnTickTime = FindExtraDespawnImmediate(memory, imageBase, CamelDespawnTickTimePattern);
            chickenDespawnTickTime = FindExtraDespawnImmediate(memory, imageBase, ChickenDespawnTickTimePattern);

            if (camelDespawnTickTime != null)
                originalCamelDespawnTicks = camelDespawnTickTime.GetValue();

            if (chickenDespawnTickTime != null)
                originalChickenDespawnTicks = chickenDespawnTickTime.GetValue();

            extraDespawnTicksInitialized = true;
        }

        private ManagedAssemblyImmediate<short> FindExtraDespawnImmediate(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            string pattern)
        {
            try
            {
                long offset = PatternScanner.FindPattern(memory, pattern);
                if (offset < 0)
                    return null;

                return new ManagedAssemblyImmediate<short>(
                    new IntPtr(unchecked((long)(imageBase + (ulong)offset + ExtraDespawnPatternImmediateOffset))),
                    operand: 1);
            }
            catch (Exception exception)
            {
                log.LogWarning($"Failed to initialize animal despawn patch: {exception}");
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
                log.LogWarning($"Failed to apply animal despawn patch: {exception}");
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
                log.LogWarning($"Failed to apply camel health patch: {exception}");
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

        private unsafe void NeutralizePlayerOwnedChicken(GameUnit* chicken)
        {
            if (!settings.EnableMod ||
                !settings.HuntChicken ||
                chicken == null ||
                chicken->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN)
            {
                return;
            }

            chicken->r_ControllableForPlayerId = 0;
            chicken->r_SpritePlayerColorId = 0;
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
            recentHunterTargets.Clear();
            preservedShortLivedCorpseExpirations.Clear();
            abortedTargetCooldowns.Clear();
            lastHunterQueryTimestamps.Clear();
            hunterMeatPickupTimestamps.Clear();
            pendingHunterShotIntents.Clear();
            nextPreyCacheRefreshTimestamp = 0;
            nextStaleReservationCleanupTimestamp = 0;
            nextHunterTargetSummaryTimestamp = 0;
            lastLoggedDesiredCamelHealth = 0;
            despawnPatchStateLogged = false;
            hunterTargetDiagnosticLogs = 0;
            preyCacheDiagnosticLogs = 0;
            hunterProjectileDiagnosticLogs = 0;
            corpseDiagnosticLogs = 0;
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

        private struct RecentHunterTargetSnapshot
        {
            public readonly HunterTargetSnapshot Target;
            public readonly long ExpiresAt;

            public RecentHunterTargetSnapshot(HunterTargetSnapshot target, long expiresAt)
            {
                Target = target;
                ExpiresAt = expiresAt;
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

            public PendingHunterShotIntent(
                int hunterUnitId,
                uint hunterGlobalId,
                int targetUnitId,
                uint targetGlobalId,
                eChimps targetType,
                long dueAt,
                string hunterSource,
                long spawnReturnValue)
            {
                HunterUnitId = hunterUnitId;
                HunterGlobalId = hunterGlobalId;
                TargetUnitId = targetUnitId;
                TargetGlobalId = targetGlobalId;
                TargetType = targetType;
                DueAt = dueAt;
                HunterSource = hunterSource;
                SpawnReturnValue = spawnReturnValue;
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
            ClearTargetSelectionCaches();
            nativeScanFailureLogged = false;
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
