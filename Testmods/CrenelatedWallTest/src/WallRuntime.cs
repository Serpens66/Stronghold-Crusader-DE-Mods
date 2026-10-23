using APIShared;
using APIShared.GameModes;
using BepInEx.Logging;
using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CrenelatedWallTest
{
    internal sealed class NativeWallWorld : IWallWorld
    {
        internal readonly GameTileManagerAPI Tiles = GameTileManagerAPI.Instance;
        public bool TryRead(int x, int y, out WallCell cell)
        {
            cell = null;
            if (!Tiles.IsTileInsideMapBounds(x, y)) return false;
            int id = Tiles.GetTileId(x, y);
            if (!Tiles.IsValidTileId(id) || id >= Tiles.GetLogicLayer().Length || id >= Tiles.GetWallOwnerLayer().Length ||
                id >= Tiles.GetDamageLayer().Length || id >= Tiles.GetStructureLayer().Length) return false;
            TilePropertyFlag flags = Tiles.GetTilePropertyFlag(id);
            cell = new WallCell { Id = id, X = x, Y = y,
                // Native 6F230 stores player-1 while preserving the upper owner-grid bits.
                Owner = (Tiles.GetWallOwnerLayer()[id] & 7) + 1,
                IsWall = (flags & TilePropertyFlag.IsWall) != 0,
                IsCrenel = (flags & TilePropertyFlag.CrenelatedLow) != 0,
                High = (flags & TilePropertyFlag.CrenelatedHigh) != 0,
                Damaged = Tiles.GetDamageLayer()[id] != 0, Structure = Tiles.GetStructureLayer()[id] != 0 };
            return true;
        }
    }

    internal sealed class WallRuntime
    {
        private readonly object sync = new object();
        private static readonly int[] NeighborX = { 0, 1, 0, -1 }, NeighborY = { -1, 0, 1, 0 };
        private readonly ManualLogSource log;
        private readonly WallSettings settings;
        private readonly NativeAvailability availability;
        private readonly StraightGraphicsHook graphics;
        private readonly PreviewValidationHook preview;
        private readonly PreviewAdmissionCache previewCache = new PreviewAdmissionCache();
        [ThreadStatic] private static int buildDepth;
        private readonly NativeWallWorld world = new NativeWallWorld();
        private readonly SortedDictionary<int, WallCell> tracked = new SortedDictionary<int, WallCell>();
        private readonly Dictionary<int, int> signatures = new Dictionary<int, int>();
        private readonly Stack<BuildScope> builds = new Stack<BuildScope>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Shared.LogRateGate rejectionLog = new Shared.LogRateGate();
        private IMissionLifecycleCapability missions;
        private MissionContext context;
        private int[][] pendingSave;
        private int[] scanIds = new int[0];
        private int scanCursor;
        private bool initialized, active, activationRequested, activationCaptured, ready, restorePending, tickLogged, callbackLogged;
        private long sessionId;
        private sealed class BuildScope
        {
            internal int Owner, BeginX, BeginY, EndX, EndY, Stone;
            internal bool Rejected;
            internal List<WallCell> Before;
        }
        internal WallRuntime(ManualLogSource log, WallSettings settings, NativeAvailability availability)
        {
            this.log = log; this.settings = settings; this.availability = availability;
            graphics = new StraightGraphicsHook(log);
            preview = new PreviewValidationHook(log, Plugin.Guid, RejectPreview);
        }
        internal void Register(ModApiClient api, CrusaderLibraryLoadContext library)
        {
            if (!api.TryGetMissionLifecycle(out missions, out var diagnostic) ||
                !missions.TryRegisterObserver("crenel-session", Started, Ended, Initializing, out diagnostic))
                throw new InvalidOperationException("Mission service unavailable: " + diagnostic?.Reason);
            if (!ModSaveDataAPI.Instance.RegisterModDataHandler(Plugin.Guid, Save, Load))
                throw new InvalidOperationException("Save registration rejected.");
            subscriptions.Add(BuildingR3EventHooks.OnBuildWall.Observable.Subscribe(BuildWall));
            subscriptions.Add(BuildingR3EventHooks.OnWallBulldoze.Observable.Subscribe(Bulldozed));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(Damaged));
            GameTimeManagerAPI.Instance.OnTick += Tick;
            graphics.Install(library);
            preview.Install(library); // Optional: execution protection survives a preview conflict.
            initialized = true;
            Shared.DebugLogHelper.LogInfo(log, "CRENEL_READY durable publishers=OnBuildWall,APIShared missions,ModSaveDataAPI,OnTick; permanent local graphics predicate.");
        }
        private void Initializing(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (!initialized) return;
                if (sessionId != note.Context.SessionId) { Clear(); sessionId = note.Context.SessionId; }
                context = note.Context;
                if (note.Phase == MissionInitializationPhase.NativeLoaded)
                { CaptureActivation(); Restore(); }
            }
        }
        private void Started(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (!initialized) return;
                if (sessionId != note.Context.SessionId) { Clear(); sessionId = note.Context.SessionId; }
                context = note.Context; CaptureActivation(); ready = true;
                restorePending = active && pendingSave != null;
                if (restorePending) Restore();
                Shared.DebugLogHelper.LogInfo(log, "CRENEL_SESSION id=" + sessionId + "; active=" + active + "; save=" + context.IsSave + "; editor=" + context.IsEditor + "; host settings frozen until next session.");
            }
        }
        private static bool Allowed(MissionContext ctx) => ctx != null && (ctx.IsEditor || ctx.Mode.Kind != GameModeKind.Unknown);
        private void CaptureActivation()
        {
            if (!activationCaptured) { activationRequested = settings.EnableMod; activationCaptured = true; }
            // Mode evidence may become known only at ManagedReady; settings stay frozen.
            active = Allowed(context) && activationRequested;
            if (!active) graphics.Publish(null);
        }
        private void Ended(MissionLifecycleNotification note) { lock (sync) { Clear(); context = null; sessionId = 0; } }
        private void Clear()
        {
            active = activationRequested = activationCaptured = ready = restorePending = tickLogged = callbackLogged = false;
            tracked.Clear(); signatures.Clear(); builds.Clear(); pendingSave = null; scanIds = new int[0]; scanCursor = 0;
            graphics.Publish(null);
            previewCache.Clear();
        }
        private void BuildWall(BuildWallEventArgs e)
        {
            // OnBuildWall wraps the original synchronously. Only this thread's
            // native validator belongs to execution; another peer's preview cannot.
            if (e.Phase == EventHookPhase.Pre) buildDepth++;
            else if (e.Phase == EventHookPhase.Post && buildDepth > 0) buildDepth--;
            lock (sync)
            {
                if (e.Phase == EventHookPhase.Pre)
                {
                    BuildScope scope = null; builds.Push(null);
                    try
                    {
                        if (!initialized || !ready || !active || e.WallType != eMappers.MAPPER_CRENAL ||
                            !GamePlayerManagerAPI.Instance.IsPlayerIdValid(e.PlayerId) || (!context.IsEditor && GamePlayerManagerAPI.Instance.IsAIPlayer(e.PlayerId))) return;
                        scope = new BuildScope { Owner = e.PlayerId, BeginX = e.TileXBegin, BeginY = e.TileYBegin,
                            EndX = e.TileXEnd, EndY = e.TileYEnd,
                            Before = WallPolicy.Raster(world, e.TileXBegin, e.TileYBegin, e.TileXEnd, e.TileYEnd, e.BuildWallsMaximum),
                            Stone = GamePlayerManagerAPI.Instance.GetGoodAmount(e.PlayerId, eGoods.STORED_STONE_BLOCKS) };
                        builds.Pop(); builds.Push(scope);
                        WallPlan plan = WallAdmission.Evaluate(world, scope.Owner, scope.Before, availability.Get(scope.Owner), tracked);
                        if (!plan.Accepted)
                        {
                            scope.Rejected = true; e.BuildWallsMaximum = 0;
                            Rejection("decision", "CRENEL_REJECT owner=" + scope.Owner + "; line=" + Line(scope) + "; reason=" + plan.Rejection + "; entire command; maximum=0.");
                        }
                    }
                    catch (Exception ex)
                    {
                        e.BuildWallsMaximum = 0; if (scope != null) scope.Rejected = true;
                        Error("pre", ex); // Unsafe admission never reaches a mutating placement.
                    }
                    return;
                }
                if (e.Phase != EventHookPhase.Post || builds.Count == 0) return;
                BuildScope completed = builds.Pop(); if (completed == null) return;
                try
                {
                    var added = new List<WallCell>();
                    foreach (WallCell old in completed.Before)
                        if (world.TryRead(old.X, old.Y, out WallCell now) && now.IsWall && now.IsCrenel && now.Owner == completed.Owner &&
                            (!old.IsWall || !old.IsCrenel || old.Damaged != now.Damaged || old.Owner != now.Owner)) added.Add(now);
                    if (completed.Rejected)
                    {
                        int stone = GamePlayerManagerAPI.Instance.GetGoodAmount(completed.Owner, eGoods.STORED_STONE_BLOCKS);
                        if (added.Count != 0 || stone != completed.Stone)
                            throw new InvalidOperationException("Rejected Vanilla command changed tiles/resources: added=" + added.Count + ",stone=" + completed.Stone + "->" + stone);
                        Rejection("verified", "CRENEL_REJECT_VERIFIED line=" + Line(completed) + "; added=0; stone unchanged=" + stone);
                        return;
                    }
                    if (added.Count == 0) return;
                    WallPlan plan = WallPolicy.Plan(world, completed.Owner, added, false, tracked);
                    if (!plan.Accepted) { Shared.DebugLogHelper.LogErrorLimited(log, "crenel-post-topology", "CRENEL_POST_UNRESOLVED " + plan.Rejection + "; Vanilla retained; no rollback or resource mutation."); return; }
                    int changed = Apply(plan);
                    if (!callbackLogged)
                    {
                        callbackLogged = true;
                        Shared.DebugLogHelper.LogInfo(log, "CRENEL_HOOK_CONFIRMED owner=" + completed.Owner + "; tile=" + added[0].Id + "; position=" + added[0].X + "," + added[0].Y + "; durable native publisher reached after startup cleanup.");
                    }
                    Shared.DebugLogHelper.LogInfo(log, "CRENEL_BUILD owner=" + completed.Owner + "; line=" + Line(completed) + "; built=" + added.Count + "; eligible=" + plan.Heights.Count + "; changed=" + changed + "; unchanged=" + (plan.Heights.Count - changed) + "; anchors=" + plan.Anchors + "; equalPairs=" + plan.EqualPairs + "; noTriples=true.");
                }
                catch (Exception ex) { Error("post", ex); }
            }
        }
        private bool RejectPreview(IntPtr manager, int owner, uint x, uint y, uint endX, uint endY, short mapper)
        {
            if (buildDepth != 0 || mapper != (short)eMappers.MAPPER_CRENAL) return false;
            lock (sync)
            {
                if (!initialized || !ready || !active || manager != world.Tiles.GetTileManager() ||
                    !GamePlayerManagerAPI.Instance.IsPlayerIdValid(owner) ||
                    (!context.IsEditor && GamePlayerManagerAPI.Instance.IsAIPlayer(owner))) return false;
                // 94120 queues CF640's result as BuildWallsMaximum (raster visits),
                // independently of 6F230's actual-placement quota. Preserve both.
                int quota = availability.Get(owner);
                return !previewCache.Evaluate(world, owner, (int)x, (int)y, (int)endX, (int)endY, quota, tracked).Accepted;
            }
        }
        private int Apply(WallPlan plan)
        {
            int changed = 0;
            foreach (var pair in plan.Heights)
            {
                WallCell expected = plan.Cells[pair.Key];
                if (!world.TryRead(expected.X, expected.Y, out WallCell cell) || cell.Id != expected.Id ||
                    cell.Owner != expected.Owner || !cell.IsWall || !cell.IsCrenel || cell.Damaged || cell.Structure)
                    throw new InvalidOperationException("Validated topology changed before publication at " + expected.Id);
            }
            if (plan.Heights.Count != 0)
            {
                WallCell first = plan.Cells[plan.Heights.First().Key];
                // Validate the public path service before publishing any changed flag.
                if (!GamePathingManagerAPI.Instance.RecalculateTileAndNeighbours(first.X, first.Y))
                    throw new InvalidOperationException("Path refresh unavailable before publication.");
            }
            foreach (var pair in plan.Heights)
            {
                WallCell cell = plan.Cells[pair.Key].Copy();
                TilePropertyFlag flags = world.Tiles.GetTilePropertyFlag(cell.Id);
                bool oldHigh = (flags & TilePropertyFlag.CrenelatedHigh) != 0;
                if (oldHigh != pair.Value)
                {
                    world.Tiles.SetTilePropertyFlag(cell.Id, pair.Value ? flags | TilePropertyFlag.CrenelatedHigh : flags & ~TilePropertyFlag.CrenelatedHigh);
                    if (!GamePathingManagerAPI.Instance.RecalculateTileAndNeighbours(cell.X, cell.Y))
                        throw new InvalidOperationException("Path refresh unavailable at " + cell.Id);
                    world.Tiles.RefreshTileAreaVisuals(cell.X, cell.Y); changed++;
                }
                cell.High = pair.Value; tracked[cell.Id] = cell;
            }
            foreach (int id in plan.Heights.Keys) signatures[id] = Signature(tracked[id]);
            scanIds = tracked.Keys.ToArray(); scanCursor = scanIds.Length == 0 ? 0 : scanCursor % scanIds.Length;
            PublishEndpoints();
            return changed;
        }
        private void PublishEndpoints()
        {
            var endpoints = new Dictionary<int, EndpointAssignment>();
            foreach (WallCell saved in tracked.Values)
            {
                if (saved.High || !world.TryRead(saved.X, saved.Y, out WallCell cell) || cell.Id != saved.Id || cell.Owner != saved.Owner ||
                    !cell.IsWall || !cell.IsCrenel || cell.High || cell.Damaged || cell.Structure) continue;
                int count = 0, neighbor = -1;
                for (int d = 0; d < 4; d++)
                    if (world.TryRead(cell.X + NeighborX[d], cell.Y + NeighborY[d], out WallCell next) && next.IsWall && next.IsCrenel)
                    { count++; neighbor = next.Id; }
                if (count == 1) endpoints.Add(cell.Id, new EndpointAssignment(cell.Owner, cell.Y, neighbor));
            }
            graphics.Publish(endpoints);
        }
        private int Signature(WallCell at)
        {
            if (!world.TryRead(at.X, at.Y, out WallCell cell) || cell.Id != at.Id) return -1;
            int value = cell.Owner | (cell.IsWall ? 16 : 0) | (cell.IsCrenel ? 32 : 0) | (cell.High ? 64 : 0) | (cell.Damaged ? 128 : 0) | (cell.Structure ? 256 : 0);
            for (int i = 0; i < 4; i++) if (world.TryRead(at.X + NeighborX[i], at.Y + NeighborY[i], out WallCell neighbor) && neighbor.IsWall && neighbor.IsCrenel) value |= 1 << (i + 9);
            return value;
        }
        private void Tick(int tick)
        {
            lock (sync)
            {
                if (!initialized || !ready) return;
                try
                {
                    if (!tickLogged) { tickLogged = true; Shared.DebugLogHelper.LogInfo(log, "CRENEL_RUNTIME_AFTER_CLEANUP session=" + sessionId + "; tick=" + tick + "; active=" + active); }
                    if (!active) return;
                    if (restorePending) Restore();
                    // A bounded native-tick scan also catches projectile destruction, which
                    // is not covered by the Extender's melee damage event.
                    int count = Math.Min(256, scanIds.Length);
                    var seeds = new List<WallCell>();
                    for (int i = 0; i < count && scanIds.Length != 0; i++)
                    {
                        if (scanCursor >= scanIds.Length) scanCursor = 0;
                        int id = scanIds[scanCursor++]; if (!tracked.TryGetValue(id, out WallCell old)) continue;
                        int current = Signature(old);
                        if (signatures.TryGetValue(id, out int prior) && current == prior) continue;
                        signatures[id] = current;
                        if (!world.TryRead(old.X, old.Y, out WallCell cell) || !cell.IsWall || !cell.IsCrenel || cell.Owner != old.Owner)
                        { tracked.Remove(id); signatures.Remove(id); continue; }
                        seeds.Add(cell);
                    }
                    if (scanIds.Length != tracked.Count)
                    { scanIds = tracked.Keys.ToArray(); scanCursor = scanIds.Length == 0 ? 0 : scanCursor % scanIds.Length; PublishEndpoints(); }
                    Maintain(seeds);
                }
                catch (Exception ex) { Error("tick", ex); }
            }
        }
        private void Maintain(List<WallCell> seeds)
        {
            foreach (var group in seeds.GroupBy(c => c.Owner).OrderBy(g => g.Key))
            {
                if (!context.IsEditor && GamePlayerManagerAPI.Instance.IsAIPlayer(group.Key)) continue;
                WallPlan plan = WallPolicy.Plan(world, group.Key, group.ToArray(), false, tracked);
                if (plan.Accepted) Apply(plan);
                else Shared.DebugLogHelper.LogErrorLimited(log, "crenel-maintenance-" + group.Key,
                    "CRENEL_SPECIAL_UNRESOLVED damage/removal: " + plan.Rejection + "; special pieces retained; no automatic demolition.");
            }
        }
        private void Bulldozed(WallBulldozeEventArgs e)
        {
            if (e.Phase != EventHookPhase.Post) return;
            lock (sync) if (active && tracked.TryGetValue(e.TileId, out WallCell old)) signatures[e.TileId] = int.MinValue;
        }
        private void Damaged(BuildingTileTakeDamageEventArgs e)
        {
            if (e.Phase != EventHookPhase.Post) return;
            lock (sync) if (active && tracked.ContainsKey(e.TileId)) signatures[e.TileId] = int.MinValue;
        }
        private byte[] Save(SaveContext save)
        {
            lock (sync)
            {
                if (!initialized || !ready || context == null) return null;
                var valid = new List<WallCell>();
                if (active) foreach (WallCell old in tracked.Values)
                    if (world.TryRead(old.X, old.Y, out WallCell cell) && cell.IsWall && cell.IsCrenel && cell.Owner == old.Owner)
                    { cell.High = old.High; valid.Add(cell); }
                // An empty header overwrites earlier metadata when disabled, rather than
                // leaving stale entries in the archive. MP IsSaveFile can be false.
                byte[] bytes = MessagePackSerializer.Serialize(SaveState.Capture(world.Tiles.GetCurrentMapSize(), valid));
                Shared.DebugLogHelper.LogInfo(log, "CRENEL_SAVE tiles=" + valid.Count + "; bytes=" + bytes.Length + "; active=" + active);
                return bytes;
            }
        }
        private void Load(byte[] bytes, LoadContext load)
        {
            lock (sync)
            {
                if (bytes == null || bytes.Length > 16 * 1024 * 1024) throw new InvalidOperationException("Save payload too large.");
                // SE invokes archive reads more than once. Stage only; lifecycle NativeLoaded
                // supplies the authoritative save/editor classification and memory readiness.
                pendingSave = MessagePackSerializer.Deserialize<int[][]>(bytes);
                restorePending = true;
            }
        }
        private void Restore()
        {
            restorePending = false;
            if (!active || context == null || pendingSave == null) return;
            int[][] data = pendingSave; pendingSave = null;
            try
            {
                List<WallCell> cells = SaveState.Validate(data, world.Tiles.GetCurrentMapSize(), world);
                var plan = new WallPlan();
                foreach (var group in cells.GroupBy(c => c.Owner).OrderBy(g => g.Key))
                {
                    // Stored editor assignments may intentionally belong to an AI slot.
                    // Replaying explicit metadata is not intercepting automatic AI builds.
                    // Validate the restored assignment once per owner/component, not once per
                    // tile. Saved bits are the tie-break input, so a valid saved arrangement wins.
                    WallPlan shape = WallPolicy.Plan(world, group.Key, group.ToArray(), false);
                    var storedIds = new HashSet<int>(group.Select(c => c.Id));
                    bool outOfScope = shape.Heights.Any(p => !storedIds.Contains(p.Key) && shape.Cells[p.Key].High != p.Value);
                    if (!shape.Accepted || outOfScope)
                    {
                        Shared.DebugLogHelper.LogWarning(log, "CRENEL_LOAD_SKIPPED owner=" + group.Key + "; " + (shape.Rejection ?? "restoration would change an unrecorded tile"));
                        continue;
                    }
                    foreach (WallCell cell in group)
                        if (shape.Heights.TryGetValue(cell.Id, out bool height))
                        { plan.Cells.Add(cell.Id, shape.Cells[cell.Id]); plan.Heights.Add(cell.Id, height); }
                }
                int changed = Apply(plan);
                Shared.DebugLogHelper.LogInfo(log, "CRENEL_LOAD tiles=" + plan.Heights.Count + "; changed=" + changed + "; metadata-only scope restored.");
            }
            catch (Exception ex) { Error("load", ex); }
        }
        private static string Line(BuildScope s) => s.BeginX + "," + s.BeginY + "->" + s.EndX + "," + s.EndY;
        private void Rejection(string site, string message)
        {
            // Wall-clock rate limiting controls logging only, never a build decision.
            if (rejectionLog.TryReserve(site, 5, out long suppressed))
                Shared.DebugLogHelper.LogInfo(log, message + (suppressed == 0 ? "" : "; suppressed=" + suppressed));
        }
        private void Error(string phase, Exception ex) => Shared.DebugLogHelper.LogErrorLimited(log, "crenel-" + phase, "CRENEL_CALLBACK_ERROR phase=" + phase + ": " + ex);
    }
}
