using System;
using System.Collections.Generic;
using System.IO;
using APIShared;
using APIShared.GameModes;
using APIShared.Multiplayer;
using APIShared.Savegames;
using BepInEx.Logging;
using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Shared;

namespace BugfixesAndQoL.Crenels
{
    internal sealed class NativeCrenelWorld : ICrenelWorld
    {
        internal readonly GameTileManagerAPI Tiles = GameTileManagerAPI.Instance;

        public bool TryRead(int x, int y, out CrenelCell cell)
        {
            cell = null;
            if (!Tiles.IsTileInsideMapBounds(x, y)) return false;
            int id = Tiles.GetTileId(x, y);
            Span<int> logic = Tiles.GetLogicLayer();
            Span<byte> owners = Tiles.GetWallOwnerLayer(), damage = Tiles.GetDamageLayer();
            Span<ushort> structures = Tiles.GetStructureLayer();
            if (!Tiles.IsValidTileId(id) || id >= logic.Length || id >= owners.Length || id >= damage.Length || id >= structures.Length) return false;
            var flags = (TilePropertyFlag)logic[id];
            cell = new CrenelCell
            {
                Id = id, X = x, Y = y,
                // Native 6F230 stores player-1 while preserving the upper owner-grid bits.
                Owner = (owners[id] & 7) + 1,
                IsWall = (flags & TilePropertyFlag.IsWall) != 0,
                IsCrenel = (flags & TilePropertyFlag.CrenelatedLow) != 0,
                High = (flags & TilePropertyFlag.CrenelatedHigh) != 0,
                Damaged = damage[id] != 0,
                Structure = structures[id] != 0
            };
            return true;
        }
    }

    // Keeps crenels crossable: every crenel the feature manages follows the map checkerboard (diagonal
    // pieces alternate along x), and low special pieces are drawn straight so Vanilla's renderer cannot
    // raise them again (CrenelGraphicsHook). Manages newly built and optionally pre-existing crenels
    // for the owner groups selected at session start. Heights are
    // never stored; they follow from the synchronized map on every peer.
    // Rooted for the process lifetime by BugfixesAndQoLPlugin (static field) and its publishers.
    internal sealed class CrenelRuntime
    {
        private const string Owner = BugfixesAndQoLPlugin.PluginGuid;
        private const string Feature = CrenelGraphicsHook.Feature;
        private const string SaveIdentifier = "BugfixesAndQoL.Crenels";
        private const string SaveEntry = "managed";

        private readonly object sync = new object();
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly CrenelGraphicsHook graphics;
        private readonly NativeCrenelWorld world = new NativeCrenelWorld();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        // Managed crenels (identity only), in id order for deterministic normalization on every peer.
        private SortedDictionary<int, CrenelCell> managed = new SortedDictionary<int, CrenelCell>();
        // Shape signature at the last normalization; a redraw re-checks only changed tiles.
        private readonly Dictionary<int, int> signatures = new Dictionary<int, int>();
        private readonly Stack<BuildScope> builds = new Stack<BuildScope>();
        // Pre state of the tile an AI placement (0x6CB20, one tile per call) is about to change.
        private readonly Stack<CrenelCell> aiBuilds = new Stack<CrenelCell>();
        // Per melee-damage call (Pre/Post pairs): was the tile a crenel before the hit?
        private readonly Stack<bool> damageWasCrenel = new Stack<bool>();
        // Pre-built AI castles are placed through 0x6CB20 while the mission is still starting. Collected
        // until Started decides; cleared only at Ended, the calls may precede the first notification.
        private readonly List<CrenelCell> prebuilt = new List<CrenelCell>();
        private readonly List<int> redrawn = new List<int>();
        private MissionContext context;
        private long sessionId;
        private int[][] pendingSave;
        private SortedDictionary<int, CrenelCell> repairedOrigins;
        // Host settings are frozen at the first notification of a session.
        private bool settingsCaptured, requested, manageHuman, manageAI, alignInEditor, alignOnSave, alignOnMapStart;
        private bool installed, resyncReady, active, ready, restorePending, repairing;
        private volatile bool agreed;

        private sealed class BuildScope
        {
            internal int Owner;
            internal List<CrenelCell> Before;
        }

        // Construct on the Unity thread during plugin startup (agreement registration).
        internal CrenelRuntime(BugfixesAndQoLViewModel settings, ManualLogSource log)
        {
            this.settings = settings;
            this.log = log;
            graphics = new CrenelGraphicsHook(log);
            // Every peer must run the same normalization; a peer without the hook keeps it off for all.
            SimulationAgreement.Register(Owner, Feature, required: false,
                isReady: () => installed && resyncReady && graphics.Intact,
                apply: enabled => agreed = enabled);
        }

        internal void Install(CrusaderLibraryLoadContext library, bool currentNative)
        {
            if (installed) return;
            // The resync provider list stays the same when the option is off; inactive peers send a header.
            resyncReady = MultiplayerResyncEvents.TryRegister(Owner, Feature, BeginRepair, CompleteRepair, out string reason) &&
                MultiplayerResyncData.TryRegister<SortedDictionary<int, CrenelCell>>(Owner, Feature, 1,
                    CrenelRepairRecord.MaximumBytes, CaptureRepair, PrepareRepair, replacement => repairedOrigins = replacement, out reason);
            if (!resyncReady) DebugLogHelper.LogError(log, "Crenel checkerboard multiplayer resync registration failed: " + reason);
            // 0x5C2D0, its callers and the 0x65EF0 write are audited for this native hash only.
            if (!currentNative) throw new InvalidOperationException("The installed CrusaderDE.dll differs from the audited version.");
            if (!ApiShared.ForMod(Owner).TryGetMissionLifecycle(out var missions, out var diagnostic) ||
                !missions.TryRegisterObserver(Feature, Started, Ended, Initializing, out diagnostic))
                throw new InvalidOperationException("Mission service unavailable: " + diagnostic?.Reason);
            var save = new VersionedSaveDataRegistration(Owner, SaveIdentifier, 1, 1, new[] { SaveEntry }, CaptureSave, LoadedSave)
            { Scope = VersionedSaveScope.SaveGamesAndMaps, SimulationRelevant = true };
            if (!VersionedSaveData.TryRegister(save, out _, out reason)) throw new InvalidOperationException(reason);
            graphics.Install(library);
            subscriptions.Add(BuildingR3EventHooks.OnBuildWall.Observable.Subscribe(BuildWall));
            // Automatic AI construction uses 0x6CB20, not 0x6F230; the Extender's OnAIBuildWall wraps it.
            subscriptions.Add(AIR3EventHooks.OnAIBuildWall.Observable.Subscribe(AIBuildWall));
            subscriptions.Add(BuildingR3EventHooks.OnWallBulldoze.Observable.Subscribe(Bulldozed));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(Damaged));
            GameTimeManagerAPI.Instance.OnTick += Tick;
            installed = true;
        }

        private void Initializing(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (!installed) return;
                Enter(note.Context);
                if (note.Phase == MissionInitializationPhase.NativeLoaded) { CaptureActivation(); Restore(); }
            }
        }

        private void Started(MissionLifecycleNotification note)
        {
            lock (sync)
            {
                if (!installed) return;
                Enter(note.Context);
                CaptureActivation();
                ready = true;
                // Also covers a save staged while the feature was not yet active at NativeLoaded.
                if (restorePending || pendingSave != null) Restore();
                AdoptPrebuilt();
                AdoptExisting();
            }
        }

        private void Ended(MissionLifecycleNotification note)
        {
            lock (sync) { Clear(); prebuilt.Clear(); context = null; sessionId = 0; }
        }

        private void Enter(MissionContext next)
        {
            if (sessionId != next.SessionId) { Clear(); sessionId = next.SessionId; }
            context = next;
        }

        private void CaptureActivation()
        {
            if (!settingsCaptured)
            {
                requested = settings.EnableMod;
                manageHuman = settings.EnableCrenelCheckerboard;
                manageAI = settings.EnableCrenelCheckerboardAI;
                alignInEditor = settings.AlignExistingCrenelsInEditor;
                alignOnSave = settings.AlignExistingCrenelsOnSaveLoad;
                alignOnMapStart = settings.AlignExistingCrenelsOnMapStart;
                settingsCaptured = true;
            }
            // Mode evidence may become known only later in the start; the settings stay frozen.
            bool allowed = context != null && (context.IsEditor
                ? manageAI
                : (manageHuman || manageAI) && context.Mode.Kind != GameModeKind.Unknown);
            bool intact = graphics.Intact;
            active = allowed && requested && intact && (!context.Mode.IsRealMultiplayer || agreed);
            if (allowed && requested && !intact && context.Mode.IsRealMultiplayer && agreed)
                SimulationAgreement.ReportFault("Crenel checkerboard hook is no longer intact in the agreed world.");
            if (!active) graphics.Clear();
        }

        private void Clear()
        {
            settingsCaptured = requested = manageHuman = manageAI = alignInEditor = alignOnSave = alignOnMapStart = false;
            active = ready = restorePending = repairing = false;
            aiBuilds.Clear(); damageWasCrenel.Clear(); builds.Clear();
            managed.Clear(); signatures.Clear();
            pendingSave = null; repairedOrigins = null;
            graphics.Clear();
        }

        // Saves restore their own managed set; the editor has no automatic AI construction.
        private void AdoptPrebuilt()
        {
            if (prebuilt.Count == 0) return;
            try
            {
                if (!active || context.IsEditor || context.IsSave) return;
                var cells = new List<CrenelCell>(); var seen = new HashSet<int>();
                foreach (CrenelCell placed in prebuilt)
                    if (world.TryRead(placed.X, placed.Y, out CrenelCell now) && now.Id == placed.Id && CrenelPolicy.Crenel(now) &&
                        now.Owner == placed.Owner && seen.Add(now.Id)) cells.Add(now);
                if (cells.Count != 0) Adopt(cells);
            }
            catch (Exception ex) { Error("prebuilt", ex); }
            finally { prebuilt.Clear(); }
        }

        // Optional per start kind: adopts existing crenels for the selected owner groups after
        // saved managed tiles were restored.
        private void AdoptExisting()
        {
            if (!active || !(context.IsEditor ? alignInEditor : context.IsSave ? alignOnSave : alignOnMapStart)) return;
            try
            {
                GameTileManagerAPI tiles = world.Tiles;
                Span<int> logic = tiles.GetLogicLayer();
                const TilePropertyFlag crenelBits = TilePropertyFlag.IsWall | TilePropertyFlag.CrenelatedLow;
                var cells = new List<CrenelCell>();
                for (int y = 0; y < GameTileManagerAPI.MAX_HEIGHT; y++)
                    for (int x = 0; x < GameTileManagerAPI.MAX_WIDTH; x++)
                    {
                        if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                        int id = tiles.GetTileId(x, y);
                        if (id < 0 || id >= logic.Length || ((TilePropertyFlag)logic[id] & crenelBits) != crenelBits || managed.ContainsKey(id)) continue;
                        if (world.TryRead(x, y, out CrenelCell cell) && CrenelPolicy.Crenel(cell)) cells.Add(cell);
                    }
                if (cells.Count != 0) Adopt(cells);
            }
            catch (Exception ex) { Error("adopt-existing", ex); }
        }

        private void BeginRepair()
        {
            if (!resyncReady) return;
            lock (sync) { repairing = true; repairedOrigins = null; }
        }

        private byte[] CaptureRepair()
        {
            lock (sync) return active ? CrenelRepairRecord.Capture(world.Tiles.GetCurrentMapSize(), managed.Values, world) : Array.Empty<byte>();
        }

        private SortedDictionary<int, CrenelCell> PrepareRepair(ReadOnlyMemory<byte> bytes)
        {
            lock (sync)
            {
                if (!repairing || bytes.IsEmpty == active)
                    throw new InvalidDataException("Crenel checkerboard activation differs during repair.");
                return bytes.IsEmpty ? new SortedDictionary<int, CrenelCell>() :
                    CrenelRepairRecord.Prepare(bytes.Span, world.Tiles.GetCurrentMapSize(), ReadCellById);
            }
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
                // The managed set was replaced as a whole: rebuild the render-side state from it, then
                // restore path and graphics state through the same normalization as a placement.
                graphics.Clear();
                Normalize(managed.Keys);
                repairing = false;
            }
        }

        private unsafe CrenelCell ReadCellById(int id)
        {
            var tiles = world.Tiles;
            if (!tiles.IsValidTileId(id) || tiles.MapColumnLookupTable == null || tiles.MapRowLookupTable == null) return null;
            int y = tiles.MapColumnLookupTable[id];
            if (y >= GameTileManagerAPI.MAX_HEIGHT) return null;
            int x = tiles.GetTileXFromTileIdAndY(id, y);
            return world.TryRead(x, y, out CrenelCell cell) ? cell : null;
        }

        private void BuildWall(BuildWallEventArgs e)
        {
            lock (sync)
            {
                if (e.Phase == EventHookPhase.Pre)
                {
                    BuildScope scope = null;
                    try
                    {
                        if (ready && active && e.WallType == eMappers.MAPPER_CRENAL && CanManageOwner(e.PlayerId))
                            scope = new BuildScope { Owner = e.PlayerId,
                                Before = CrenelPolicy.Raster(world, e.TileXBegin, e.TileYBegin, e.TileXEnd, e.TileYEnd, e.BuildWallsMaximum) };
                    }
                    catch (Exception ex) { Error("build-pre", ex); }
                    builds.Push(scope);
                    return;
                }
                if (e.Phase != EventHookPhase.Post || builds.Count == 0) return;
                BuildScope completed = builds.Pop();
                if (completed == null || !active) return;
                try
                {
                    var added = new List<CrenelCell>();
                    foreach (CrenelCell old in completed.Before)
                        if (world.TryRead(old.X, old.Y, out CrenelCell now) && CrenelPolicy.Crenel(now) &&
                            (context.IsEditor || now.Owner == completed.Owner) &&
                            (!CrenelPolicy.Crenel(old) || old.Damaged != now.Damaged || old.Owner != now.Owner)) added.Add(now);
                    if (added.Count != 0) Adopt(added);
                }
                catch (Exception ex) { Error("build-post", ex); }
            }
        }

        // Automatic AI placement (0x6CB20) changes at most one tile per call.
        private void AIBuildWall(AIBuildWallEventArgs e)
        {
            lock (sync)
            {
                if (e.Phase == EventHookPhase.Pre)
                {
                    CrenelCell before = null;
                    try
                    {
                        if (installed && ready && active && !repairing && world.TryRead(e.TileX, e.TileY, out CrenelCell cell)) before = cell;
                    }
                    catch (Exception ex) { Error("ai-pre", ex); }
                    aiBuilds.Push(before);
                    return;
                }
                if (e.Phase != EventHookPhase.Post || aiBuilds.Count == 0) return;
                CrenelCell old = aiBuilds.Pop();
                try
                {
                    if (old == null)
                    {
                        // Mission start (pre-built castle): remember the resulting crenel, decide at Started.
                        if (installed && !ready && !repairing && prebuilt.Count < CrenelPolicy.MaximumCells &&
                            world.TryRead(e.TileX, e.TileY, out CrenelCell placed) && CrenelPolicy.Crenel(placed))
                            prebuilt.Add(placed.Identity());
                        return;
                    }
                    if (!active || repairing || !world.TryRead(old.X, old.Y, out CrenelCell now) || now.Id != old.Id || !CrenelPolicy.Crenel(now) ||
                        (CrenelPolicy.Crenel(old) && old.Damaged == now.Damaged && old.Owner == now.Owner)) return;
                    Adopt(new List<CrenelCell> { now });
                }
                catch (Exception ex) { Error("ai-post", ex); }
            }
        }

        // Registers new crenels and normalizes them with their managed neighbours, whose shape changes too.
        private void Adopt(List<CrenelCell> added)
        {
            var normalize = new SortedSet<int>();
            foreach (CrenelCell cell in added)
            {
                if (!CanManageOwner(cell.Owner)) continue;
                managed[cell.Id] = cell.Identity();
                normalize.Add(cell.Id);
                AddManagedNeighbours(cell.X, cell.Y, normalize);
            }
            if (normalize.Count != 0) Normalize(normalize);
        }

        private bool CanManageOwner(int owner)
        {
            if (context.IsEditor) return manageAI;
            var players = GamePlayerManagerAPI.Instance;
            return players.IsPlayerIdValid(owner) && (players.IsAIPlayer(owner) ? manageAI : manageHuman);
        }

        private void AddManagedNeighbours(int x, int y, SortedSet<int> into)
        {
            for (int d = 0; d < 8; d++)
                if (world.TryRead(x + CrenelPolicy.DX[d], y + CrenelPolicy.DY[d], out CrenelCell next) && managed.ContainsKey(next.Id))
                    into.Add(next.Id);
        }

        // Writes managed, intact, structure-free crenels to their target height and publishes the
        // straight drawing of the low ones first. Managed cells that are no longer the owner's crenel
        // are forgotten. Callers pass only tiles whose shape may have changed, so the cost follows the
        // change, never the number of managed crenels.
        private void Normalize(IEnumerable<int> ids)
        {
            Func<int, int, CrenelCell> read = CrenelPolicy.Reader(world);
            var writes = new List<KeyValuePair<CrenelCell, bool>>();
            var considered = new List<int>();
            foreach (int id in ids is SortedSet<int> sorted ? sorted : new SortedSet<int>(ids))
            {
                if (!managed.TryGetValue(id, out CrenelCell entry)) continue;
                CrenelCell cell = read(entry.X, entry.Y);
                if (!CrenelPolicy.Crenel(cell) || cell.Id != id || cell.Owner != entry.Owner || !CanManageOwner(cell.Owner))
                {
                    managed.Remove(id); signatures.Remove(id);
                    graphics.Watch(id, false); graphics.SetAssignment(id, null);
                    continue;
                }
                considered.Add(id); graphics.Watch(id, true);
                bool? target = CrenelPolicy.TargetHigh(read, cell);
                // Publish the straight drawing before the bit changes: a redraw in between would otherwise
                // take the special branch and OR the new low tile high again (0x65EF0). It covers every low
                // intact tile regardless of shape; only this normalization ever raises a crenel.
                bool high = target ?? cell.High;
                graphics.SetAssignment(id, high || cell.Damaged || cell.Structure ? null : new StraightAssignment(cell.Owner, cell.Y));
                if (target.HasValue && target.Value != cell.High) writes.Add(new KeyValuePair<CrenelCell, bool>(cell, target.Value));
            }
            if (writes.Count != 0)
            {
                // Validate the public path service before publishing any changed flag.
                if (!GamePathingManagerAPI.Instance.RecalculateTileAndNeighbours(writes[0].Key.X, writes[0].Key.Y))
                    throw new InvalidOperationException("Path refresh unavailable before publication.");
                foreach (var write in writes)
                {
                    CrenelCell cell = write.Key;
                    TilePropertyFlag flags = world.Tiles.GetTilePropertyFlag(cell.Id);
                    world.Tiles.SetTilePropertyFlag(cell.Id, write.Value ? flags | TilePropertyFlag.CrenelatedHigh : flags & ~TilePropertyFlag.CrenelatedHigh);
                    if (!GamePathingManagerAPI.Instance.RecalculateTileAndNeighbours(cell.X, cell.Y))
                        throw new InvalidOperationException("Path refresh unavailable at " + cell.Id);
                    world.Tiles.RefreshTileAreaVisuals(cell.X, cell.Y);
                }
            }
            foreach (int id in considered) signatures[id] = Signature(managed[id]);
        }

        // Shape and state of a managed tile, read straight from the layers: it runs for every reported
        // redraw, so it must not allocate.
        private int Signature(CrenelCell at)
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
                int x = at.X + CrenelPolicy.DX[i], y = at.Y + CrenelPolicy.DY[i];
                if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                int n = tiles.GetTileId(x, y);
                if (!tiles.IsValidTileId(n) || n >= logic.Length) continue;
                var nf = (TilePropertyFlag)logic[n];
                if ((nf & TilePropertyFlag.IsWall) != 0 && (nf & TilePropertyFlag.CrenelatedLow) != 0) value |= 1 << (i + 9);
            }
            return value;
        }

        private void Tick(int tick)
        {
            lock (sync)
            {
                if (!ready || !active || repairing) return;
                try
                {
                    if (restorePending) Restore();
                    // Vanilla changes a managed crenel's height only while redrawing it (sticky high at
                    // 0x65EF0), and it redraws a crenel when the crenel or a neighbour changed. The hook
                    // reports those redraws, including changes without an Extender event (projectile path
                    // 0x7DF60, player removal 0x71E90), so only they are re-checked.
                    graphics.TakeReports(redrawn);
                    if (redrawn.Count == 0) return;
                    SortedSet<int> changed = null;
                    foreach (int id in redrawn)
                        if (managed.TryGetValue(id, out CrenelCell entry) && (!signatures.TryGetValue(id, out int prior) || Signature(entry) != prior))
                            (changed ?? (changed = new SortedSet<int>())).Add(id);
                    if (changed != null) Normalize(changed);
                }
                catch (Exception ex) { Error("tick", ex); }
            }
        }

        private void Bulldozed(WallBulldozeEventArgs e)
        {
            if (e.Phase == EventHookPhase.Post) ShapeChanged(e.TileId, "bulldoze");
        }

        // Hits arrive many times per siege tick. Shapes and targets depend only on crenel neighbours, so
        // only a crenel that is gone after the hit needs its neighbours normalized.
        private void Damaged(BuildingTileTakeDamageEventArgs e)
        {
            lock (sync)
            {
                if (e.Phase == EventHookPhase.Pre)
                {
                    bool crenel = false;
                    try { crenel = active && IsCrenelTile(e.TileId); }
                    catch (Exception ex) { Error("damage-pre", ex); }
                    damageWasCrenel.Push(crenel);
                    return;
                }
                if (e.Phase != EventHookPhase.Post || damageWasCrenel.Count == 0 || !damageWasCrenel.Pop() || !active) return;
                try { if (IsCrenelTile(e.TileId)) return; }
                catch (Exception ex) { Error("damage", ex); return; }
                ShapeChanged(e.TileId, "damage");
            }
        }

        // Allocation-free crenel test for the many melee hits of a siege.
        private bool IsCrenelTile(int id)
        {
            const int bits = (int)(TilePropertyFlag.IsWall | TilePropertyFlag.CrenelatedLow);
            Span<int> logic = world.Tiles.GetLogicLayer();
            return world.Tiles.IsValidTileId(id) && id < logic.Length && (logic[id] & bits) == bits;
        }

        // Demolition or destruction changes the shape of the managed neighbours (e.g. a middle becomes an
        // endpoint). Normalizing right in the event keeps the renderer from raising them first; redraw
        // reports cover changes without an event.
        private void ShapeChanged(int tileId, string phase)
        {
            lock (sync)
            {
                if (!ready || !active || repairing) return;
                try
                {
                    CrenelCell at = ReadCellById(tileId);
                    if (at == null) return;
                    var ids = new SortedSet<int>();
                    if (managed.ContainsKey(tileId)) ids.Add(tileId);
                    AddManagedNeighbours(at.X, at.Y, ids);
                    if (ids.Count != 0) Normalize(ids);
                }
                catch (Exception ex) { Error(phase, ex); }
            }
        }

        // An empty record replaces earlier data when the feature is inactive.
        private bool CaptureSave(VersionedSaveWriter writer)
        {
            lock (sync)
            {
                if (repairing || !ready) return false;
                if (!active) return true;
                var valid = new List<CrenelCell>();
                foreach (CrenelCell entry in managed.Values)
                    if (world.TryRead(entry.X, entry.Y, out CrenelCell cell) && cell.Id == entry.Id && CrenelPolicy.Crenel(cell) && cell.Owner == entry.Owner)
                        valid.Add(entry);
                writer.Write(SaveEntry, MessagePackSerializer.Serialize(CrenelSaveRecord.Capture(world.Tiles.GetCurrentMapSize(), valid)));
                return true;
            }
        }

        // Stages only; the mission lifecycle (NativeLoaded/Started) supplies the authoritative
        // classification and memory readiness for the restore.
        private void LoadedSave(VersionedSaveLoad load)
        {
            lock (sync)
            {
                if (load.Status != VersionedSaveLoadStatus.Restored || !load.Entries.TryGetValue(SaveEntry, out byte[] bytes)) return;
                try { pendingSave = MessagePackSerializer.Deserialize<int[][]>(bytes); restorePending = true; }
                catch (Exception ex) { Error("load", ex); }
            }
        }

        // Only tiles the feature itself managed are restored; designer and pre-feature walls stay as
        // loaded unless an align-existing option adopts them afterwards.
        private void Restore()
        {
            restorePending = false;
            if (!active || context == null || pendingSave == null) return;
            int[][] data = pendingSave; pendingSave = null;
            try
            {
                List<CrenelCell> cells = CrenelSaveRecord.Validate(data, world.Tiles.GetCurrentMapSize(), world);
                var ids = new SortedSet<int>();
                foreach (CrenelCell cell in cells) { managed[cell.Id] = cell; ids.Add(cell.Id); }
                Normalize(ids);
            }
            catch (Exception ex) { Error("load", ex); }
        }

        private void Error(string phase, Exception ex) =>
            DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/Crenels/" + phase, "Crenel checkerboard " + phase + " failed: " + ex);
    }
}
