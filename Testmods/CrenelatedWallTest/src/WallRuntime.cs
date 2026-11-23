using APIShared;
using APIShared.GameModes;
using APIShared.Multiplayer;
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
using System.IO;
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
        // Axis neighbours first, then diagonals (diagonal pieces depend on diagonal neighbours).
        private static readonly int[] NeighborX = { 0, 1, 0, -1, 1, 1, -1, -1 }, NeighborY = { -1, 0, 1, 0, -1, 1, 1, -1 };
        // Preview raster bound: longer than any native map axis, so the whole dragged line is checked.
        private const int PreviewMaximum = 801;
        private readonly ManualLogSource log;
        private readonly WallSettings settings;
        private readonly StraightGraphicsHook graphics;
        private readonly PreviewValidationHook preview;
        private readonly PreviewAdmissionCache previewCache = new PreviewAdmissionCache();
        [ThreadStatic] private static int buildDepth;
        private readonly NativeWallWorld world = new NativeWallWorld();
        // Crenels placed by humans while the mod was active (Id, X, Y, Owner only). Heights are
        // never remembered: they follow from the current map. Map/AI/pre-mod walls are absent.
        private SortedDictionary<int, WallCell> managed = new SortedDictionary<int, WallCell>();
        private readonly Func<int, bool> isManaged;
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
        private bool repairing, resyncReady;
        private SortedDictionary<int, WallCell> repairedOrigins;
        private long sessionId;
        private sealed class BuildScope
        {
            internal int Owner, BeginX, BeginY, EndX, EndY, Stone;
            internal bool Rejected;
            internal List<WallCell> Before;
        }
        internal WallRuntime(ManualLogSource log, WallSettings settings)
        {
            this.log = log; this.settings = settings; isManaged = id => managed.ContainsKey(id);
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
            resyncReady = MultiplayerResyncEvents.TryRegister(Plugin.Guid, "crenel-origins", BeginRepair, CompleteRepair,
                out string reason) && MultiplayerResyncData.TryRegister<SortedDictionary<int, WallCell>>(Plugin.Guid,
                    "crenel-origins", 1, WallRepairRecord.MaximumBytes, CaptureRepair, PrepareRepair,
                    replacement => repairedOrigins = replacement, out reason);
            if (!resyncReady) Shared.DebugLogHelper.LogError(log, "Crenel origin multiplayer data registration failed: " + reason);
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
            active = Allowed(context) && activationRequested && (!context.Mode.IsRealMultiplayer || resyncReady);
            if (!active) graphics.Publish(null);
        }
        private void Ended(MissionLifecycleNotification note) { lock (sync) { Clear(); context = null; sessionId = 0; } }
        private void Clear()
        {
            active = activationRequested = activationCaptured = ready = restorePending = tickLogged = callbackLogged = false;
            repairing = false;
            repairedOrigins = null;
            managed.Clear(); signatures.Clear(); builds.Clear(); pendingSave = null; scanIds = new int[0]; scanCursor = 0;
            graphics.Publish(null);
            previewCache.Clear();
        }

        private void BeginRepair()
        {
            if (!resyncReady) return;
            lock (sync) { repairing = true; repairedOrigins = null; }
        }

        private byte[] CaptureRepair()
        {
            lock (sync) return active ? WallRepairRecord.Capture(world.Tiles.GetCurrentMapSize(), managed.Values, world) : Array.Empty<byte>();
        }

        private SortedDictionary<int, WallCell> PrepareRepair(ReadOnlyMemory<byte> bytes)
        {
            lock (sync)
            {
                if (!repairing || bytes.IsEmpty == active)
                    throw new InvalidDataException("Crenel placement activation differs during repair.");
                return bytes.IsEmpty ? new SortedDictionary<int, WallCell>() :
                    WallRepairRecord.Prepare(bytes.Span, world.Tiles.GetCurrentMapSize(), ReadRepairedCell);
            }
        }

        private unsafe WallCell ReadRepairedCell(int id)
        {
            var tiles = world.Tiles;
            if (!tiles.IsValidTileId(id) || tiles.MapColumnLookupTable == null || tiles.MapRowLookupTable == null) return null;
            int y = tiles.MapColumnLookupTable[id];
            if (y >= GameTileManagerAPI.MAX_HEIGHT) return null;
            int x = tiles.GetTileXFromTileIdAndY(id, y);
            return world.TryRead(x, y, out WallCell cell) ? cell : null;
        }

        private void CompleteRepair()
        {
            lock (sync)
            {
                if (!repairing) return;
                if (repairedOrigins == null) throw new InvalidOperationException("Crenel repair completed without host origins.");
                managed = repairedOrigins;
                repairedOrigins = null;
                signatures.Clear();
                scanCursor = 0;
                previewCache.Clear();
                // Once Chore 67 has re-enqueued held actions, restore derived path/graphics state
                // through the same native-facing policy used for a successful placement.
                Normalize(managed.Keys);
                repairing = false;
            }
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
                        WallVerdict verdict = WallAdmission.Evaluate(world, scope.Owner, scope.Before, isManaged);
                        if (!verdict.Accepted)
                        {
                            scope.Rejected = true; e.BuildWallsMaximum = 0;
                            Rejection("decision", "CRENEL_REJECT owner=" + scope.Owner + "; line=" + Line(scope) + "; reason=" + verdict.Rejection + "; entire command; maximum=0.");
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
                    var normalize = new SortedSet<int>();
                    foreach (WallCell cell in added)
                    {
                        managed[cell.Id] = new WallCell { Id = cell.Id, X = cell.X, Y = cell.Y, Owner = cell.Owner };
                        normalize.Add(cell.Id);
                        // A neighbour's shape changes with the new piece (e.g. former endpoint
                        // now straight); only managed neighbours are ever rewritten.
                        for (int d = 0; d < 8; d++)
                            if (world.TryRead(cell.X + NeighborX[d], cell.Y + NeighborY[d], out WallCell next) && managed.ContainsKey(next.Id))
                                normalize.Add(next.Id);
                    }
                    int changed = Normalize(normalize);
                    if (!callbackLogged)
                    {
                        callbackLogged = true;
                        Shared.DebugLogHelper.LogInfo(log, "CRENEL_HOOK_CONFIRMED owner=" + completed.Owner + "; tile=" + added[0].Id + "; position=" + added[0].X + "," + added[0].Y + "; durable native publisher reached after startup cleanup.");
                    }
                    Shared.DebugLogHelper.LogInfo(log, "CRENEL_BUILD owner=" + completed.Owner + "; line=" + Line(completed) + "; built=" + added.Count + "; checked=" + normalize.Count + "; changed=" + changed + "; unchanged=" + (normalize.Count - changed) + "; managed=" + managed.Count + ".");
                }
                catch (Exception ex) { Error("post", ex); }
            }
        }
        private bool RejectPreview(IntPtr manager, int owner, uint x, uint y, uint endX, uint endY, short mapper)
        {
            if (buildDepth != 0 || mapper != (short)eMappers.MAPPER_CRENAL) return false;
            lock (sync)
            {
                if (repairing || !initialized || !ready || !active || manager != world.Tiles.GetTileManager() ||
                    !GamePlayerManagerAPI.Instance.IsPlayerIdValid(owner) ||
                    (!context.IsEditor && GamePlayerManagerAPI.Instance.IsAIPlayer(owner))) return false;
                // The whole dragged line is checked. Execution re-checks its queued raster
                // (possibly a shorter prefix) and remains the authoritative, synchronized decision.
                return !previewCache.Evaluate(world, owner, (int)x, (int)y, (int)endX, (int)endY, PreviewMaximum, isManaged).Accepted;
            }
        }
        // Rewrites only managed, intact, structure-free line pieces (straight middles and
        // endpoints) to Vanilla's placement parity. Specials keep Vanilla's own height.
        // Managed cells that are no longer the owner's crenel are forgotten.
        private int Normalize(IEnumerable<int> ids)
        {
            Func<int, int, WallCell> read = WallPolicy.Reader(world);
            var writes = new List<KeyValuePair<WallCell, bool>>();
            var considered = new List<int>();
            foreach (int id in ids.Distinct().OrderBy(i => i).ToArray())
            {
                if (!managed.TryGetValue(id, out WallCell entry)) continue;
                WallCell cell = read(entry.X, entry.Y);
                if (cell == null || cell.Id != id || !WallPolicy.Crenel(cell) || cell.Owner != entry.Owner)
                { managed.Remove(id); signatures.Remove(id); continue; }
                considered.Add(id);
                bool? target = WallPolicy.TargetHigh(read, cell);
                if (target.HasValue && target.Value != cell.High) writes.Add(new KeyValuePair<WallCell, bool>(cell, target.Value));
            }
            // Validate the public path service before publishing any changed flag.
            if (writes.Count != 0 && !GamePathingManagerAPI.Instance.RecalculateTileAndNeighbours(writes[0].Key.X, writes[0].Key.Y))
                throw new InvalidOperationException("Path refresh unavailable before publication.");
            foreach (var write in writes)
            {
                WallCell cell = write.Key;
                TilePropertyFlag flags = world.Tiles.GetTilePropertyFlag(cell.Id);
                world.Tiles.SetTilePropertyFlag(cell.Id, write.Value ? flags | TilePropertyFlag.CrenelatedHigh : flags & ~TilePropertyFlag.CrenelatedHigh);
                if (!GamePathingManagerAPI.Instance.RecalculateTileAndNeighbours(cell.X, cell.Y))
                    throw new InvalidOperationException("Path refresh unavailable at " + cell.Id);
                world.Tiles.RefreshTileAreaVisuals(cell.X, cell.Y);
            }
            foreach (int id in considered) signatures[id] = Signature(managed[id]);
            scanIds = managed.Keys.ToArray(); scanCursor = scanIds.Length == 0 ? 0 : scanCursor % scanIds.Length;
            PublishEndpoints();
            return writes.Count;
        }
        private void PublishEndpoints()
        {
            var endpoints = new Dictionary<int, EndpointAssignment>();
            foreach (WallCell saved in managed.Values)
            {
                if (!world.TryRead(saved.X, saved.Y, out WallCell cell) || cell.Id != saved.Id || cell.Owner != saved.Owner ||
                    !cell.IsWall || !cell.IsCrenel || cell.High || cell.Damaged || cell.Structure) continue;
                int count = 0, neighbor = -1;
                for (int d = 0; d < 4; d++)
                    if (world.TryRead(cell.X + NeighborX[d], cell.Y + NeighborY[d], out WallCell next) && next.IsWall && next.IsCrenel)
                    { count++; neighbor = next.Id; }
                if (count == 1) endpoints.Add(cell.Id, new EndpointAssignment(cell.Owner, cell.Y, neighbor));
                else if (count == 0 && WallPolicy.IsDiagonalPiece(WallPolicy.Reader(world), cell))
                    endpoints.Add(cell.Id, new EndpointAssignment(cell.Owner, cell.Y, -1));
            }
            graphics.Publish(endpoints);
        }
        private int Signature(WallCell at)
        {
            if (!world.TryRead(at.X, at.Y, out WallCell cell) || cell.Id != at.Id) return -1;
            int value = cell.Owner | (cell.IsWall ? 16 : 0) | (cell.IsCrenel ? 32 : 0) | (cell.High ? 64 : 0) | (cell.Damaged ? 128 : 0) | (cell.Structure ? 256 : 0);
            for (int i = 0; i < 8; i++) if (world.TryRead(at.X + NeighborX[i], at.Y + NeighborY[i], out WallCell neighbor) && neighbor.IsWall && neighbor.IsCrenel) value |= 1 << (i + 9);
            return value;
        }
        private void Tick(int tick)
        {
            lock (sync)
            {
                if (repairing || !initialized || !ready) return;
                try
                {
                    if (!tickLogged) { tickLogged = true; Shared.DebugLogHelper.LogInfo(log, "CRENEL_RUNTIME_AFTER_CLEANUP session=" + sessionId + "; tick=" + tick + "; active=" + active); }
                    if (!active) return;
                    if (restorePending) Restore();
                    // A bounded native-tick scan catches demolition, projectile destruction and
                    // Vanilla's deferred renderer promotion of a managed piece's height.
                    int count = Math.Min(256, scanIds.Length);
                    var changed = new List<int>();
                    for (int i = 0; i < count && scanIds.Length != 0; i++)
                    {
                        if (scanCursor >= scanIds.Length) scanCursor = 0;
                        int id = scanIds[scanCursor++]; if (!managed.TryGetValue(id, out WallCell entry)) continue;
                        if (signatures.TryGetValue(id, out int prior) && Signature(entry) == prior) continue;
                        changed.Add(id);
                    }
                    if (changed.Count != 0 || scanIds.Length != managed.Count) Normalize(changed);
                }
                catch (Exception ex) { Error("tick", ex); }
            }
        }
        private void Bulldozed(WallBulldozeEventArgs e)
        {
            if (e.Phase != EventHookPhase.Post) return;
            lock (sync) if (active && managed.ContainsKey(e.TileId)) signatures[e.TileId] = int.MinValue;
        }
        private void Damaged(BuildingTileTakeDamageEventArgs e)
        {
            if (e.Phase != EventHookPhase.Post) return;
            lock (sync) if (active && managed.ContainsKey(e.TileId)) signatures[e.TileId] = int.MinValue;
        }
        private byte[] Save(SaveContext save)
        {
            lock (sync)
            {
                if (!initialized || !ready || context == null) return null;
                var valid = new List<WallCell>();
                if (active) foreach (WallCell entry in managed.Values)
                    if (world.TryRead(entry.X, entry.Y, out WallCell cell) && cell.Id == entry.Id && cell.IsWall && cell.IsCrenel && cell.Owner == entry.Owner)
                        valid.Add(entry);
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
                // Only tiles the mod itself placed are restored and normalized. Map-designer,
                // AI and pre-mod walls stay exactly as loaded. Editor metadata may name AI slots;
                // replaying it is not intercepting automatic AI construction.
                List<WallCell> cells = SaveState.Validate(data, world.Tiles.GetCurrentMapSize(), world);
                foreach (WallCell cell in cells) managed[cell.Id] = cell;
                int changed = Normalize(cells.Select(c => c.Id));
                Shared.DebugLogHelper.LogInfo(log, "CRENEL_LOAD tiles=" + cells.Count + "; changed=" + changed + "; managed=" + managed.Count + "; unmanaged walls untouched.");
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
