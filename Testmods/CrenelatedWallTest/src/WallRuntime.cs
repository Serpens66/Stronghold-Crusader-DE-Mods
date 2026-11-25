using APIShared;
using APIShared.GameModes;
using APIShared.Multiplayer;
using BepInEx.Logging;
using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
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

    internal sealed partial class WallRuntime
    {
        private readonly object sync = new object();
        // Axis neighbours first, then diagonals (diagonal pieces depend on diagonal neighbours).
        private static readonly int[] NeighborX = { 0, 1, 0, -1, 1, 1, -1, -1 }, NeighborY = { -1, 0, 1, 0, -1, 1, 1, -1 };
        private readonly ManualLogSource log;
        private readonly WallSettings settings;
        private readonly StraightGraphicsHook graphics;
        private readonly NativeWallWorld world = new NativeWallWorld();
        // Crenels placed by humans or the AI while height adjustment was active (Id, X, Y, Owner
        // only). Heights are never remembered: they follow from the current map. Map and
        // pre-mod walls are absent.
        private SortedDictionary<int, WallCell> managed = new SortedDictionary<int, WallCell>();
        private readonly Dictionary<int, int> signatures = new Dictionary<int, int>();
        private readonly Stack<BuildScope> builds = new Stack<BuildScope>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private IMissionLifecycleCapability missions;
        private MissionContext context;
        private int[][] pendingSave;
        private readonly List<int> redrawn = new List<int>();
        // active = mod allowed and enabled this session; heights = its frozen height adjustment.
        // existing*: frozen per session, align crenels that already exist at Started (editor / save / map).
        private bool existingInEditor, existingOnSave, existingOnMapStart;
        private bool initialized, active, heights, activationRequested, heightsRequested, activationCaptured, ready, restorePending, tickLogged, callbackLogged, aiLogged;
        // Pre state of the tile an AI placement (0x6CB20, one tile per call) is about to change.
        private readonly Stack<WallCell> aiBuilds = new Stack<WallCell>();
        // Per melee-damage call (SE c_game_buildingtile_take_melee_damage, Pre/Post pairs): crenel before the hit?
        private readonly Stack<bool> damageWasCrenel = new Stack<bool>();
        // Pre-built AI castles are placed through 0x6CB20 while the mission is still starting.
        // Collected until Started decides (heights on, no editor, no save); cleared only at
        // Ended, because the calls may precede this mission's first lifecycle notification.
        private readonly List<WallCell> prebuilt = new List<WallCell>();
        private bool repairing, resyncReady;
        private SortedDictionary<int, WallCell> repairedOrigins;
        private long sessionId;
        private sealed class BuildScope
        {
            internal int Owner, BeginX, BeginY, EndX, EndY;
            internal List<WallCell> Before;
        }
        internal WallRuntime(ManualLogSource log, WallSettings settings)
        {
            this.log = log; this.settings = settings;
            graphics = new StraightGraphicsHook(log);
        }
        internal void Register(ModApiClient api, CrusaderLibraryLoadContext library)
        {
            if (!api.TryGetMissionLifecycle(out missions, out var diagnostic) ||
                !missions.TryRegisterObserver("crenel-session", Started, Ended, Initializing, out diagnostic))
                throw new InvalidOperationException("Mission service unavailable: " + diagnostic?.Reason);
            if (!ModSaveDataAPI.Instance.RegisterModDataHandler(Plugin.Guid, Save, Load))
                throw new InvalidOperationException("Save registration rejected.");
            subscriptions.Add(BuildingR3EventHooks.OnBuildWall.Observable.Subscribe(BuildWall));
            // Automatic AI construction uses 0x6CB20, not 0x6F230; SE's OnAIBuildWall wraps it.
            subscriptions.Add(AIR3EventHooks.OnAIBuildWall.Observable.Subscribe(AIBuildWall));
            subscriptions.Add(BuildingR3EventHooks.OnWallBulldoze.Observable.Subscribe(Bulldozed));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(Damaged));
            GameTimeManagerAPI.Instance.OnTick += Tick;
            graphics.Install(library);
            resyncReady = MultiplayerResyncEvents.TryRegister(Plugin.Guid, "crenel-origins", BeginRepair, CompleteRepair,
                out string reason) && MultiplayerResyncData.TryRegister<SortedDictionary<int, WallCell>>(Plugin.Guid,
                    "crenel-origins", 1, WallRepairRecord.MaximumBytes, CaptureRepair, PrepareRepair,
                    replacement => repairedOrigins = replacement, out reason);
            if (!resyncReady) Shared.DebugLogHelper.LogError(log, "Crenel origin multiplayer data registration failed: " + reason);
            initialized = true;
            Shared.DebugLogHelper.LogInfo(log, "CRENEL_READY durable publishers=OnBuildWall,OnAIBuildWall,APIShared missions,ModSaveDataAPI,OnTick; permanent local graphics predicate.");
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
                restorePending = heights && pendingSave != null;
                if (restorePending) Restore();
                AdoptPrebuilt();
                AdoptExisting();
                DiagSession("started");
                try { Shared.NativePatchGuard.Recheck(); } // later foreign patches on the graphics hook
                catch (Exception ex) { Error("patch-recheck", ex); }
                Shared.DebugLogHelper.LogInfo(log, "CRENEL_SESSION id=" + sessionId + "; active=" + active + "; heights=" + heights + "; save=" + context.IsSave + "; editor=" + context.IsEditor + "; host settings frozen until next session.");
            }
        }
        private static bool Allowed(MissionContext ctx) => ctx != null && (ctx.IsEditor || ctx.Mode.Kind != GameModeKind.Unknown);
        private void CaptureActivation()
        {
            if (!activationCaptured)
            {
                activationRequested = settings.EnableMod; heightsRequested = settings.AdjustHeights;
                existingInEditor = settings.AdjustExistingInEditor; existingOnSave = settings.AdjustExistingOnSaveLoad;
                existingOnMapStart = settings.AdjustExistingOnMapStart;
                activationCaptured = true;
            }
            // Mode evidence may become known only at ManagedReady; settings stay frozen.
            active = Allowed(context) && activationRequested && (!context.Mode.IsRealMultiplayer || resyncReady);
            heights = active && heightsRequested;
            if (!heights) { graphics.ClearEndpoints(); graphics.ClearWatch(); }
        }
        private void Ended(MissionLifecycleNotification note) { lock (sync) { Clear(); prebuilt.Clear(); context = null; sessionId = 0; } }
        // Saves restore their own managed set; the editor has no automatic AI construction.
        private void AdoptPrebuilt()
        {
            if (prebuilt.Count == 0) return;
            var cells = new List<WallCell>(); var seen = new HashSet<int>();
            try
            {
                if (!heights || context.IsEditor || context.IsSave) return;
                foreach (WallCell placed in prebuilt)
                    if (world.TryRead(placed.X, placed.Y, out WallCell now) && now.Id == placed.Id && WallPolicy.Crenel(now) &&
                        now.Owner == placed.Owner && seen.Add(now.Id)) cells.Add(now);
                int changed = cells.Count == 0 ? 0 : Adopt(cells, out _);
                Shared.DebugLogHelper.LogInfo(log, "CRENEL_AI_PREBUILT calls=" + prebuilt.Count + "; adopted=" + cells.Count + "; changed=" + changed + "; managed=" + managed.Count + ".");
            }
            catch (Exception ex) { Error("prebuilt", ex); }
            finally { prebuilt.Clear(); }
        }
        // Optional: adopts every crenel that already exists when the session starts (map designer walls,
        // savegame walls the mod did not build, pre-placed map walls), per start kind. Runs at Started on
        // every peer with the frozen host settings, after saved managed tiles were restored.
        private void AdoptExisting()
        {
            if (!heights || context == null) return;
            string kind = context.IsEditor ? "editor" : context.IsSave ? "save" : "map";
            if (!(context.IsEditor ? existingInEditor : context.IsSave ? existingOnSave : existingOnMapStart)) return;
            try
            {
                GameTileManagerAPI tiles = world.Tiles;
                Span<int> logic = tiles.GetLogicLayer();
                const TilePropertyFlag crenelBits = TilePropertyFlag.IsWall | TilePropertyFlag.CrenelatedLow;
                var cells = new List<WallCell>();
                for (int y = 0; y < GameTileManagerAPI.MAX_HEIGHT; y++)
                    for (int x = 0; x < GameTileManagerAPI.MAX_WIDTH; x++)
                    {
                        if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                        int id = tiles.GetTileId(x, y);
                        if (id < 0 || id >= logic.Length || ((TilePropertyFlag)logic[id] & crenelBits) != crenelBits || managed.ContainsKey(id)) continue;
                        if (world.TryRead(x, y, out WallCell cell) && WallPolicy.Crenel(cell)) cells.Add(cell);
                    }
                int changed = cells.Count == 0 ? 0 : Adopt(cells, out _);
                Shared.DebugLogHelper.LogInfo(log, "CRENEL_ADOPT_EXISTING kind=" + kind + "; adopted=" + cells.Count + "; changed=" + changed + "; managed=" + managed.Count + ".");
            }
            catch (Exception ex) { Error("adopt-existing", ex); }
        }
        private void Clear()
        {
            active = heights = activationRequested = heightsRequested = activationCaptured = false;
            existingInEditor = existingOnSave = existingOnMapStart = false;
            ready = restorePending = tickLogged = callbackLogged = aiLogged = false;
            aiBuilds.Clear(); damageWasCrenel.Clear();
            repairing = false;
            repairedOrigins = null;
            managed.Clear(); signatures.Clear(); builds.Clear(); pendingSave = null;
            graphics.ClearEndpoints(); graphics.ClearWatch();
        }

        private void BeginRepair()
        {
            if (!resyncReady) return;
            lock (sync) { repairing = true; repairedOrigins = null; DiagSession("resync-begin"); }
        }

        private byte[] CaptureRepair()
        {
            lock (sync) return heights ? WallRepairRecord.Capture(world.Tiles.GetCurrentMapSize(), managed.Values, world) : Array.Empty<byte>();
        }

        private SortedDictionary<int, WallCell> PrepareRepair(ReadOnlyMemory<byte> bytes)
        {
            lock (sync)
            {
                if (!repairing || bytes.IsEmpty == heights)
                    throw new InvalidDataException("Crenel placement activation differs during repair.");
                SortedDictionary<int, WallCell> prepared = bytes.IsEmpty ? new SortedDictionary<int, WallCell>() :
                    WallRepairRecord.Prepare(bytes.Span, world.Tiles.GetCurrentMapSize(), ReadCellById);
                DiagEvent("resync-prepare", prepared.Count, 0);
                return prepared;
            }
        }

        private unsafe WallCell ReadCellById(int id)
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
                // The managed set was replaced as a whole: rebuild the render-side sets from it.
                graphics.ClearEndpoints(); graphics.ClearWatch();
                // Once Chore 67 has re-enqueued held actions, restore derived path/graphics state
                // through the same native-facing policy used for a successful placement.
                int repairWrites = Normalize(managed.Keys);
                repairing = false;
                DiagEvent("resync-complete", -1, repairWrites);
            }
        }
        private void BuildWall(BuildWallEventArgs e)
        {
            lock (sync)
            {
                if (e.Phase == EventHookPhase.Pre)
                {
                    BuildScope scope = null; builds.Push(null);
                    try
                    {
                        if (!initialized || !ready || !heights || e.WallType != eMappers.MAPPER_CRENAL ||
                            !GamePlayerManagerAPI.Instance.IsPlayerIdValid(e.PlayerId)) return;
                        scope = new BuildScope { Owner = e.PlayerId, BeginX = e.TileXBegin, BeginY = e.TileYBegin,
                            EndX = e.TileXEnd, EndY = e.TileYEnd,
                            Before = WallPolicy.Raster(world, e.TileXBegin, e.TileYBegin, e.TileXEnd, e.TileYEnd, e.BuildWallsMaximum) };
                        builds.Pop(); builds.Push(scope);
                    }
                    catch (Exception ex) { Error("pre", ex); }
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
                    if (added.Count == 0 || !heights) return;
                    int changed = Adopt(added, out int checkedCount);
                    if (!callbackLogged)
                    {
                        callbackLogged = true;
                        Shared.DebugLogHelper.LogInfo(log, "CRENEL_HOOK_CONFIRMED owner=" + completed.Owner + "; tile=" + added[0].Id + "; position=" + added[0].X + "," + added[0].Y + "; durable native publisher reached after startup cleanup.");
                    }
#if SERP_DEVDIAG
                    // Per-command line for the in-game acceptance tests only (build.bat /devdiag).
                    Shared.DebugLogHelper.LogInfo(log, "CRENEL_BUILD owner=" + completed.Owner + "; line=" + Line(completed) + "; built=" + added.Count + "; checked=" + checkedCount + "; changed=" + changed + "; unchanged=" + (checkedCount - changed) + "; managed=" + managed.Count + ".");
#endif
                }
                catch (Exception ex) { Error("post", ex); }
            }
        }
        // Registers newly built crenels and normalizes them with their managed neighbours.
        private int Adopt(List<WallCell> added, out int checkedCount)
        {
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
            checkedCount = normalize.Count;
            return Normalize(normalize);
        }
        // Automatic AI placement (0x6CB20) changes at most one tile per call.
        private void AIBuildWall(AIBuildWallEventArgs e)
        {
            lock (sync)
            {
                if (e.Phase == EventHookPhase.Pre)
                {
                    WallCell before = null;
                    try
                    {
                        if (initialized && ready && heights && !repairing && world.TryRead(e.TileX, e.TileY, out WallCell cell)) before = cell;
                    }
                    catch (Exception ex) { Error("ai-pre", ex); }
                    aiBuilds.Push(before);
                    return;
                }
                if (e.Phase != EventHookPhase.Post || aiBuilds.Count == 0) return;
                WallCell old = aiBuilds.Pop();
                if (old == null && !ready && initialized && !repairing)
                {
                    // Mission start (pre-built castle): remember the resulting crenel, decide at Started.
                    try
                    {
                        if (world.TryRead(e.TileX, e.TileY, out WallCell placed) && WallPolicy.Crenel(placed) && prebuilt.Count < WallPolicy.MaximumCells)
                            prebuilt.Add(new WallCell { Id = placed.Id, X = placed.X, Y = placed.Y, Owner = placed.Owner });
                    }
                    catch (Exception ex) { Error("ai-prebuilt", ex); }
                    return;
                }
                if (old == null || !heights || repairing) return;
                try
                {
                    if (!world.TryRead(old.X, old.Y, out WallCell now) || now.Id != old.Id || !WallPolicy.Crenel(now) ||
                        (old.IsWall && old.IsCrenel && old.Damaged == now.Damaged && old.Owner == now.Owner)) return;
                    int changed = Adopt(new List<WallCell> { now }, out int checkedCount);
                    if (!aiLogged)
                    {
                        aiLogged = true;
                        Shared.DebugLogHelper.LogInfo(log, "CRENEL_AI_HOOK_CONFIRMED player=" + e.PlayerId + "; owner=" + now.Owner + "; tile=" + now.Id + "; position=" + now.X + "," + now.Y + "; mapper=" + e.Mappers + ".");
                    }
#if SERP_DEVDIAG
                    // Per-tile line for the in-game acceptance tests only (build.bat /devdiag).
                    Shared.DebugLogHelper.LogInfo(log, "CRENEL_AI_BUILD owner=" + now.Owner + "; position=" + now.X + "," + now.Y + "; checked=" + checkedCount + "; changed=" + changed + "; managed=" + managed.Count + ".");
#endif
                }
                catch (Exception ex) { Error("ai-post", ex); }
            }
        }
        // Rewrites managed, intact, structure-free crenels to their checkerboard height
        // (WallPolicy.TargetHigh) and publishes the straight drawing of the low ones first.
        // Managed cells that are no longer the owner's crenel are forgotten.
        private int Normalize(IEnumerable<int> ids)
        {
            Func<int, int, WallCell> read = WallPolicy.Reader(world);
            var writes = new List<KeyValuePair<WallCell, bool>>();
            var considered = new List<int>();
            // Callers pass every managed tile whose shape may have changed (the tile and its eight
            // neighbours), so the cost follows the change, never the number of managed crenels.
            foreach (int id in new SortedSet<int>(ids))
            {
                if (!managed.TryGetValue(id, out WallCell entry)) continue;
                WallCell cell = read(entry.X, entry.Y);
                if (cell == null || cell.Id != id || !WallPolicy.Crenel(cell) || cell.Owner != entry.Owner)
                { managed.Remove(id); signatures.Remove(id); graphics.Watch(id, false); graphics.SetEndpoint(id, null); continue; }
                considered.Add(id); graphics.Watch(id, true);
                bool? target = WallPolicy.TargetHigh(read, cell);
                // Publish the straight drawing before the bit changes: a redraw in between would
                // otherwise take the special branch and OR the new low tile high again (65EF0).
                graphics.SetEndpoint(id, Assignment(cell, target ?? cell.High));
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
            return writes.Count;
        }
        // Straight drawing for every low, intact managed crenel (straight middles included; native
        // 5C2D0 answers 1 for them before the hook looks), or null. Deliberately independent of the
        // neighbourhood: the hook picks the axis from the live neighbour table, so a camera rotation
        // or flatten toggle (full re-render on one peer only) can never raise a managed low crenel.
        private static EndpointAssignment Assignment(WallCell cell, bool high) =>
            high || cell.Damaged || cell.Structure ? null : new EndpointAssignment(cell.Owner, cell.Y);
        // Shape and state of a managed tile, read straight from the layers: it runs for every
        // reported redraw, so it must not allocate (WallCell reads would create nine objects).
        private int Signature(WallCell at)
        {
            GameTileManagerAPI tiles = world.Tiles;
            if (!tiles.IsTileInsideMapBounds(at.X, at.Y)) return -1;
            int id = tiles.GetTileId(at.X, at.Y);
            Span<int> logic = tiles.GetLogicLayer(); Span<byte> owners = tiles.GetWallOwnerLayer();
            Span<byte> damage = tiles.GetDamageLayer(); Span<ushort> structures = tiles.GetStructureLayer();
            if (id != at.Id || !tiles.IsValidTileId(id) || id >= logic.Length || id >= owners.Length || id >= damage.Length || id >= structures.Length) return -1;
            var flags = (TilePropertyFlag)logic[id];
            int value = ((owners[id] & 7) + 1) | ((flags & TilePropertyFlag.IsWall) != 0 ? 16 : 0) | ((flags & TilePropertyFlag.CrenelatedLow) != 0 ? 32 : 0) |
                ((flags & TilePropertyFlag.CrenelatedHigh) != 0 ? 64 : 0) | (damage[id] != 0 ? 128 : 0) | (structures[id] != 0 ? 256 : 0);
            for (int i = 0; i < 8; i++)
            {
                int x = at.X + NeighborX[i], y = at.Y + NeighborY[i];
                if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                int n = tiles.GetTileId(x, y);
                if (!tiles.IsValidTileId(n) || n >= logic.Length) continue;
                var nf = (TilePropertyFlag)logic[n];
                if ((nf & TilePropertyFlag.IsWall) != 0 && (nf & TilePropertyFlag.CrenelatedLow) != 0) value |= 1 << (i + 9);
            }
            return value;
        }
        // Multiplayer diagnostics, implemented only in WallRuntime.DevDiag.cs (build.bat /devdiag);
        // without that file the compiler removes these calls including their arguments.
        partial void DiagTick(int tick);
        partial void DiagRedraw(int reported, int changed, int writes);
        partial void DiagEvent(string phase, int tile, int writes);
        partial void DiagSession(string phase);
        private void Tick(int tick)
        {
            lock (sync)
            {
                DiagTick(tick);
                if (repairing || !initialized || !ready) return;
                try
                {
                    if (!tickLogged) { tickLogged = true; Shared.DebugLogHelper.LogInfo(log, "CRENEL_RUNTIME_AFTER_CLEANUP session=" + sessionId + "; tick=" + tick + "; active=" + active + "; heights=" + heights); }
                    if (!heights) return;
                    if (restorePending) Restore();
                    // Vanilla changes a managed crenel's height only while redrawing it (sticky high
                    // at 65EF0), and it redraws a crenel when the crenel or a neighbour changed. The
                    // graphics predicate reports those redraws, including changes without an SE event
                    // (e.g. 7DF60 projectile path, 71E90 player removal), so only they are re-checked.
                    graphics.TakeReports(redrawn);
                    if (redrawn.Count == 0) return;
                    var changed = new List<int>();
                    foreach (int id in redrawn)
                        if (managed.TryGetValue(id, out WallCell entry) && (!signatures.TryGetValue(id, out int prior) || Signature(entry) != prior))
                            changed.Add(id);
                    // Normalize stays outside the Diag call: an unimplemented partial call drops its arguments.
                    if (changed.Count != 0) { int writes = Normalize(changed); DiagRedraw(redrawn.Count, changed.Count, writes); }
                }
                catch (Exception ex) { Error("tick", ex); }
            }
        }
        private void Bulldozed(WallBulldozeEventArgs e)
        {
            if (e.Phase == EventHookPhase.Post) ShapeChanged(e.TileId, "bulldoze");
        }
        private void Damaged(BuildingTileTakeDamageEventArgs e)
        {
            // Hits arrive many times per siege tick. Shapes and targets depend only on crenel
            // neighbours, so only a crenel that is gone after the hit needs its neighbours normalized;
            // hits on plain walls, gatehouses or buildings and merely damaged crenels change nothing.
            lock (sync)
            {
                if (e.Phase == EventHookPhase.Pre)
                {
                    bool crenel = false;
                    try { crenel = heights && WallPolicy.Crenel(ReadCellById(e.TileId)); }
                    catch (Exception ex) { Error("damage-pre", ex); }
                    damageWasCrenel.Push(crenel);
                    return;
                }
                if (e.Phase != EventHookPhase.Post || damageWasCrenel.Count == 0 || !damageWasCrenel.Pop() || !heights) return;
                try { if (WallPolicy.Crenel(ReadCellById(e.TileId))) return; }
                catch (Exception ex) { Error("damage", ex); return; }
            }
            ShapeChanged(e.TileId, "damage");
        }
        // Demolition or destruction changes the shape of the managed neighbours (e.g. a middle
        // becomes an endpoint). Normalizing and publishing assignments right in the event keeps the
        // renderer from raising them first. Redraw reports cover changes without an event.
        private void ShapeChanged(int tileId, string phase)
        {
            lock (sync)
            {
                if (!initialized || !ready || !heights || repairing) return;
                try
                {
                    WallCell at = ReadCellById(tileId);
                    if (at == null) return;
                    var ids = new SortedSet<int>();
                    if (managed.ContainsKey(tileId)) ids.Add(tileId);
                    for (int d = 0; d < 8; d++)
                        if (world.TryRead(at.X + NeighborX[d], at.Y + NeighborY[d], out WallCell next) && managed.ContainsKey(next.Id))
                            ids.Add(next.Id);
                    if (ids.Count != 0) { int writes = Normalize(ids); DiagEvent(phase, tileId, writes); }
                }
                catch (Exception ex) { Error(phase, ex); }
            }
        }
        private byte[] Save(SaveContext save)
        {
            lock (sync)
            {
                if (!initialized || !ready || context == null) return null;
                var valid = new List<WallCell>();
                if (heights) foreach (WallCell entry in managed.Values)
                    if (world.TryRead(entry.X, entry.Y, out WallCell cell) && cell.Id == entry.Id && cell.IsWall && cell.IsCrenel && cell.Owner == entry.Owner)
                        valid.Add(entry);
                // An empty header overwrites earlier metadata when disabled, rather than
                // leaving stale entries in the archive. MP IsSaveFile can be false.
                byte[] bytes = MessagePackSerializer.Serialize(SaveState.Capture(world.Tiles.GetCurrentMapSize(), valid));
                Shared.DebugLogHelper.LogInfo(log, "CRENEL_SAVE tiles=" + valid.Count + "; bytes=" + bytes.Length + "; heights=" + heights);
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
                DiagEvent("save-data-load", bytes.Length, 0);
                restorePending = true;
            }
        }
        private void Restore()
        {
            restorePending = false;
            if (!heights || context == null || pendingSave == null) return;
            int[][] data = pendingSave; pendingSave = null;
            try
            {
                // Only tiles the mod itself adopted are restored and normalized. Map-designer
                // and pre-mod walls stay exactly as loaded. Editor metadata may name AI slots;
                // replaying it is not intercepting automatic AI construction.
                List<WallCell> cells = SaveState.Validate(data, world.Tiles.GetCurrentMapSize(), world);
                foreach (WallCell cell in cells) managed[cell.Id] = cell;
                int changed = Normalize(cells.Select(c => c.Id));
                Shared.DebugLogHelper.LogInfo(log, "CRENEL_LOAD tiles=" + cells.Count + "; changed=" + changed + "; managed=" + managed.Count + "; unmanaged walls untouched.");
            }
            catch (Exception ex) { Error("load", ex); }
        }
        private static string Line(BuildScope s) => s.BeginX + "," + s.BeginY + "->" + s.EndX + "," + s.EndY;
        private void Error(string phase, Exception ex) => Shared.DebugLogHelper.LogErrorLimited(log, "crenel-" + phase, "CRENEL_CALLBACK_ERROR phase=" + phase + ": " + ex);
    }
}
