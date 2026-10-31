using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AIBuildDiagnoseTest;
using APIShared;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AIWoodSafetyTest
{
    internal sealed unsafe class WoodSafetyRuntime : IWoodSafetyIntervention
    {
        private sealed class BuildScope
        {
            internal WoodSafetyRuntime Owner;
            internal BuildScope Parent;
            internal long Generation;
            internal int Player, Enemies;
            internal bool Closed;
        }
        [ThreadStatic] private static BuildScope current;
        private readonly object sync = new object();
        private readonly ManualLogSource log;
        private readonly ThreatMasks masks = new ThreatMasks();
        private readonly HashSet<int> dirty = new HashSet<int>(), refresh = new HashSet<int>();
        private readonly List<int> slots = new List<int>();
        private readonly int[] teams = new int[9];
        private readonly ConcurrentQueue<string> output = new ConcurrentQueue<string>();
        private readonly WoodCandidateHook hook;
        private readonly bool supported;
        private bool buildLayoutValid = true, counterLayoutValid = true;
        private long session, generation, corrections, rejected, placementRejected, callbacks, buildErrors, counterErrors, logOmitted;
        private int tick, nextSummary, scopeCount, scopeThread, logBytes;
        private bool active, protect, suppress, buildFaulted, counterFaulted;
        private const int LogLimit = 1024 * 1024, FinalReserve = 64 * 1024;

        internal ThreatMasks Masks => masks;
        // Managed state constructor also lets behavior tests drive real lifecycle/scope code
        // without substituting game singletons or installing any hook.
        internal WoodSafetyRuntime(ManualLogSource logger) { log = logger; }
        internal WoodSafetyRuntime(ManualLogSource logger, CrusaderLibraryLoadContext context) : this(logger)
        {
            supported = IntPtr.Size == 8 && string.Equals(WoodSafetyBridge.NativeHash, NativeDiagnosticContracts.SupportedHash, StringComparison.OrdinalIgnoreCase);
            ValidateCapabilities(ValidateBuildLayout, ValidateCounterLayout);
            if (!WoodSafetyBridge.TryRegister(AIWoodSafetyPlugin.Guid, this, out string reason)) throw new InvalidOperationException(reason);
            if (supported && buildLayoutValid) { hook = new WoodCandidateHook(this, masks.Counts); hook.Install(context); }
            if (!supported) Info("UNSUPPORTED_NATIVE both rules inactive; hash=" + WoodSafetyBridge.NativeHash);
        }
        internal void ValidateCapabilities(Action buildLayout, Action counterLayout)
        {
            try { buildLayout(); } catch (Exception ex) { buildLayoutValid = false; Info("BUILD_GUARD_FAILED layout: " + ex.Message); }
            try { counterLayout(); } catch (Exception ex) { counterLayoutValid = false; Info("COUNTER_FAILED layout: " + ex.Message); }
        }
        internal static void ValidateLayout() { ValidateBuildLayout(); ValidateCounterLayout(); }
        private static void ValidateBuildingIdentity()
        {
            if (Marshal.SizeOf(typeof(GameBuilding)) != 0x32C ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_AliveState)).ToInt32() != 0xD0 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_BuildingType)).ToInt32() != 0xD2 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_GlobalId)).ToInt32() != 0xD8 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_PlayerIdOwner)).ToInt32() != 0xD6)
                throw new InvalidOperationException("Wood safety building identity layout differs");
        }
        private static void ValidateCounterLayout()
        {
            ValidateBuildingIdentity();
            EconomyDiagnostics.ValidateWorkerLayout();
            if (Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_IsKilledByProjectile)).ToInt32() != 0x29C ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_UnitChimp)).ToInt32() != 0x8A ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() != 0x92 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.N0000183D)).ToInt32() != 0x2DC)
                throw new InvalidOperationException("Wood safety counter/worker layout differs");
        }
        private static void ValidateBuildLayout()
        {
            ValidateBuildingIdentity();
            if (
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_TilePositionXBegin)).ToInt32() != 0xEE ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_TilePositionYBegin)).ToInt32() != 0xF0 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_OccupyTileGridSize)).ToInt32() != 0xF8)
                throw new InvalidOperationException("Wood safety footprint layout differs");
        }
        internal void Start(Shared.GameplaySessionStartedContext context, bool buildProtection, bool deathProtection)
        {
            bool permitted = supported && !context.IsEditor && !context.IsReplay &&
                !context.Mode.IsRealMultiplayer && !context.Mode.MultiplayerSave &&
                context.Mode.Kind != APIShared.GameModes.GameModeKind.Unknown && !context.Mode.HasConflictingCustomizedOrigin;
            BeginSession(context.SessionId, permitted, buildProtection, deathProtection);
            lock (sync)
            {
                Info($"SESSION active={active}, buildProtection={protect}, deathProtection={suppress}, counterPointReady={WoodSafetyBridge.CounterReady}, mode={context.Mode.ToDiagnosticString()}");
                if (!active || !protect || hook?.Ready != true) return;
                try
                {
                    var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                    for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
                        if (Fortification.IsTrackedType(buildings[spanIndex].r_BuildingType))
                            masks.Update(spanIndex + 1, CopyFortification(spanIndex + 1, in buildings[spanIndex]));
                    RefreshThreats();
                    Info($"INDEX_READY tracked={masks.RecordCount}, range={masks.Range}, bytes={masks.Counts.LongLength * sizeof(ushort)}, initialBuildingScan=true");
                }
                catch (Exception ex) { BuildFailure(ex); }
            }
        }
        internal void BeginSession(long id, bool permitted, bool buildProtection, bool deathProtection)
        {
            lock (sync)
            {
                EndCore(); generation++; session = id; tick = -1; nextSummary = 0;
                corrections = rejected = placementRejected = callbacks = buildErrors = counterErrors = logOmitted = 0; logBytes = 0;
                buildFaulted = counterFaulted = false; active = permitted;
                protect = buildProtection && buildLayoutValid; suppress = deathProtection && counterLayoutValid && WoodSafetyBridge.CounterReady;
            }
        }
        internal void End()
        {
            lock (sync)
            {
                if (active) Summary("end", true);
                EndCore();
            }
        }
        private void EndCore()
        {
            active = false; generation++; current = null; scopeCount = scopeThread = 0;
            hook?.SetDemand(false, 0); hook?.ResetCounters(); masks.Clear(); dirty.Clear(); refresh.Clear(); slots.Clear();
        }
        internal void BuildingChanged(int id)
        { lock (sync) if (active && protect && id > 0) dirty.Add(id); }
        private static Fortification CopyFortification(int id, in GameBuilding b) => new Fortification(id,
            b.r_GlobalId, b.r_PlayerIdOwner, b.r_BuildingType,
            new SafetyRect(b.r_TilePositionXBegin, b.r_TilePositionYBegin, checked((int)b.r_OccupyTileGridSize)), b.r_AliveState != AliveState.None);
        private void RefreshThreats()
        {
            int range = Math.Max(GameUnitManagerAPI.Instance.GetUnitAttackRange(eChimps.CHIMP_TYPE_ARCHER),
                GameUnitManagerAPI.Instance.GetUnitAttackRange(eChimps.CHIMP_TYPE_ARAB_BOW));
            if (range < 0) throw new InvalidOperationException("Effective archer range unavailable");
            masks.SetRange(range);
            slots.Clear(); masks.CopySlots(slots); refresh.Clear();
            foreach (int id in slots) refresh.Add(id);
            foreach (int id in dirty) refresh.Add(id);
            dirty.Clear();
            foreach (int id in refresh)
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(id, out GameBuilding* b) || b == null)
                    throw new InvalidOperationException("Tracked building lookup unavailable: " + id);
                masks.Update(id, CopyFortification(id, in *b));
            }
        }
        private static bool IsAI(int player) => player >= 1 && player <= 8 && GamePlayerManagerAPI.Instance.IsAIPlayer(player);
        private int EnemyBits(int player)
        {
            for (int owner = 1; owner <= 8; owner++)
                teams[owner] = GamePlayerManagerAPI.Instance.GetPlayerTeam(owner);
            return masks.EnemyOwnerBits(player, teams);
        }
        public Action EnterWoodBuild(int player)
        {
            lock (sync)
            {
                if (!active || !protect || buildFaulted || hook?.Ready != true) return null;
                try
                {
                    if (!IsAI(player)) return null;
                    RefreshThreats();
                    return EnterPreparedBuild(player, EnemyBits(player));
                }
                catch (Exception ex) { BuildFailure(ex); return null; }
            }
        }
        internal Action EnterPreparedBuild(int player, int enemies)
        {
            lock (sync)
            {
                if (!active || !protect || buildFaulted) return null;
                int thread = Thread.CurrentThread.ManagedThreadId;
                if (scopeCount != 0 && scopeThread != thread) throw new InvalidOperationException("Concurrent wood searches violate the shared native AIV workspace contract");
                if (current != null && (current.Owner != this || current.Generation != generation)) current = null;
                var frame = new BuildScope { Owner = this, Parent = current, Generation = generation, Player = player, Enemies = enemies };
                current = frame; scopeCount++; scopeThread = thread;
                hook?.SetDemand(frame.Enemies != 0, frame.Enemies);
                return () => ExitBuild(frame);
            }
        }
        private void ExitBuild(BuildScope frame)
        {
            lock (sync)
            {
                if (frame.Closed) return; frame.Closed = true;
                // An old finally may execute after a replacement session has already begun.
                // It must not unlink the replacement session's current context.
                if (frame.Generation != generation) return;
                bool top = ReferenceEquals(current, frame);
                if (top) current = frame.Parent; else current = null;
                if (!top) { BuildFailure(new InvalidOperationException("Wood build contexts ended out of order")); scopeCount = scopeThread = 0; return; }
                if (scopeCount > 0) scopeCount--;
                if (scopeCount == 0) scopeThread = 0;
                bool enabled = active && !buildFaulted && current?.Owner == this && current.Generation == generation;
                hook?.SetDemand(enabled && current.Enemies != 0, enabled ? current.Enemies : 0);
            }
        }
        internal bool RejectCandidate(int coarseX, int coarseY, bool maskAlreadyChecked = false)
        {
            // No allocation, building lookup, hashing, scan, path query or logging on a normal candidate.
            lock (sync)
            {
                var frame = current;
                if (!active || !protect || buildFaulted || frame?.Owner != this || frame.Generation != generation) return false;
                callbacks++;
                if ((uint)coarseX >= 160 || (uint)coarseY >= 160) throw new InvalidOperationException("Candidate coordinate outside audited grid");
                // Production's native prefilter already made the <=8 mask reads.
                // The managed boundary verifies context, without reading those masks twice.
                bool deny = maskAlreadyChecked || masks.Blocks(frame.Enemies, coarseX * 5, coarseY * 5);
                if (deny)
                {
                    rejected++;
                    if (rejected <= 3) Info($"CANDIDATE_REJECTED player={frame.Player}, coarse=({coarseX},{coarseY}), range={masks.Range}; sameVanillaSearchContinues=true");
                }
                return deny;
            }
        }
        internal void ValidatePlacement(BuildingPlacementValidationEventArgs args)
        {
            // Post runs after Vanilla and after the Extender's global placement override.
            // A Pre-only veto could be undone by that override.
            if (args.Phase != EventHookPhase.Post || args.Mappers != eMappers.MAPPER_WOODSMAN) return;
            lock (sync)
            {
                if (!active || !protect || buildFaulted || hook?.Ready != true) return;
                try
                {
                    if (!IsAI(args.PlayerId)) return;
                    var frame = current;
                    int enemies;
                    if (frame?.Owner == this && frame.Generation == generation && frame.Player == args.PlayerId) enemies = frame.Enemies;
                    else { RefreshThreats(); enemies = EnemyBits(args.PlayerId); }
                    ApplyPreparedPlacement(args, enemies, true, BlockPlacement);
                }
                catch (Exception ex) { BuildFailure(ex); }
            }
        }
        private static void BlockPlacement() => GameTileManagerAPI.Instance.TileManager.IsPlacementBlocked = true;
        internal void ApplyPreparedPlacement(BuildingPlacementValidationEventArgs args, int enemies, bool ai, Action block)
        {
            lock (sync)
            {
                if (!active || !protect || buildFaulted || !ai || args.PlayerId < 1 || args.PlayerId > 8 ||
                    args.Phase != EventHookPhase.Post || args.Mappers != eMappers.MAPPER_WOODSMAN ||
                    !masks.Blocks(enemies, args.TileX, args.TileY)) return;
                block();
                placementRejected++;
                if (placementRejected <= 3) Info($"PLACEMENT_REJECTED player={args.PlayerId}, tile=({args.TileX},{args.TileY}); nativeValidationFailurePath=true");
            }
        }
        internal bool DeathRuleActive => active && suppress && !counterFaulted && WoodSafetyBridge.CounterReady;
        public WoodCounterCorrection CorrectDeathCounter(WoodCounterPoint point)
        {
            lock (sync)
            {
                if (!DeathRuleActive || point.Session != session) return default;
                try
                {
                    if (!UnitAccess.TryGetById(point.UnitId, out GameUnit* u, out _) || u == null || point.BuildingId <= 0 ||
                        !GameBuildingManagerAPI.Instance.TryGetBuildingById(point.BuildingId, out GameBuilding* b) || b == null) return default;
                    if (!DeathCounterPolicy.TryUndo(u, point.UnitId, point.UnitGlobal, b, point.BuildingId, point.BuildingGlobal, point.NativeAfter, IsAI(b->r_PlayerIdOwner))) return default;
                    uint unitGlobal = u->r_GlobalId, buildingGlobal = b->r_GlobalId;
                    short corrected = DeathCounterPolicy.UndoSingleIncrement(point.NativeAfter);
                    corrections++;
                    if (corrections <= 3) Info($"DEATH_COUNTER_CORRECTED unit={point.UnitId}/{unitGlobal}, hut={point.BuildingId}/{buildingGlobal}, nativeAfter={point.NativeAfter}, remaining={corrected}; nativeAdd=10, netAdd=0");
                    return new WoodCounterCorrection(unitGlobal, buildingGlobal);
                }
                catch (Exception ex)
                {
                    counterFaulted = true; counterErrors++;
                    Info("COUNTER_FAILED " + ex.GetType().Name + ": " + ex.Message);
                    return default;
                }
            }
        }
        internal void Tick(int now)
        {
            lock (sync)
            {
                if (!active) return;
                tick = now;
                if (now >= nextSummary) { Summary("periodic", false); nextSummary = now + 500; }
            }
        }
        internal void BuildFailure(Exception ex)
        {
            lock (sync)
            {
                buildFaulted = true; buildErrors++; hook?.SetDemand(false, 0);
                if (buildErrors <= 3) Info("BUILD_GUARD_FAILED " + ex.GetType().Name + ": " + ex.Message);
            }
        }
        private void Summary(string phase, bool final) => Info($"SUMMARY phase={phase}, nativeCandidates={hook?.Candidates ?? 0}, maskHits={hook?.MaskHits ?? 0}, callbacks={callbacks}, rejected={rejected}, placementRejected={placementRejected}, deathCounterCorrections={corrections}, buildErrors={buildErrors}, counterErrors={counterErrors}, omitted={logOmitted}, buildAvailable={buildLayoutValid && hook?.Ready == true && !buildFaulted && AiBuildDiagnostic.HasObserver}, counterAvailable={counterLayoutValid && WoodSafetyBridge.CounterReady && !counterFaulted}, buildLayoutValid={buildLayoutValid}, counterLayoutValid={counterLayoutValid}", final);
        internal void Info(string message, bool final = false)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] AI_WOOD_SAFETY_{message}; session={session}, tick={tick}.";
            int bytes = Encoding.UTF8.GetByteCount(line) + 80;
            int limit = final ? LogLimit : LogLimit - FinalReserve;
            lock (sync)
            {
                if (logBytes + bytes > limit) { logOmitted++; return; }
                logBytes += bytes; output.Enqueue(line);
            }
        }
        private void Write(string line)
        {
            if (line.Contains("_FAILED") || line.Contains("UNSUPPORTED_NATIVE")) log?.LogError(line);
            else log?.LogInfo(line);
        }
        internal void Drain() { for (int i = 0; i < 256 && output.TryDequeue(out string line); i++) Write(line); }
        internal void FinishAndDrain() { End(); while (output.TryDequeue(out string line)) Write(line); }
    }
}
