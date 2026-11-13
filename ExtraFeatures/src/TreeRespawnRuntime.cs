using APIShared;
using APIShared.Savegames;
using APIShared.Simulation;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Vegetation;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal sealed class TreeRespawnRuntime
    {
        private const string SaveEntry = "sites";
        // Vanilla's seed placement exclusions, plus pitch traps and crop/structure flags.
        private const TilePropertyFlag Blocked =
            TilePropertyFlag.Sea | TilePropertyFlag.IsFarm | TilePropertyFlag.PitchTrap |
            TilePropertyFlag.RealityEdge | TilePropertyFlag.MapBorder | TilePropertyFlag.ImpassableEdge |
            TilePropertyFlag.IsWall | TilePropertyFlag.IsBuilding | TilePropertyFlag.IsStairs |
            TilePropertyFlag.IsTree | TilePropertyFlag.TreeProximity | TilePropertyFlag.PlannedMoat |
            TilePropertyFlag.HasStone | TilePropertyFlag.Unknown18 | TilePropertyFlag.HasIron |
            TilePropertyFlag.River | TilePropertyFlag.Ford | TilePropertyFlag.IsElevated |
            TilePropertyFlag.IsSwamp | TilePropertyFlag.IsMoat | TilePropertyFlag.IsPitch |
            TilePropertyFlag.GoodsyardRelated | TilePropertyFlag.CrenelatedLow |
            TilePropertyFlag.CrenelatedHigh | TilePropertyFlag.IsLowWall |
            TilePropertyFlag.IsWheat | TilePropertyFlag.IsHops | TilePropertyFlag.IsAppleFarm |
            TilePropertyFlag.IsFarmFence;

        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly object engine;
        private readonly Dictionary<int, int> deleting = new Dictionary<int, int>();
        private readonly Func<TreeRespawnState.Site, bool> hasTree;
        private readonly Func<TreeRespawnState.Site, bool> plant;
        private TreeRespawnState captured;
        private volatile TreeRespawnState active;
        private MissionContext mission;
        private byte[] archive;
        private long attempt;
        private bool awaitingArchive;
        private bool loggedPlant;
        private int minutes;

        internal TreeRespawnRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log;
            this.settings = settings;
            engine = EngineLock.Gate;
            hasTree = HasWoodland;
            plant = TryPlant;
        }

        internal void Register()
        {
            if (!SimulationTickEvents.TryRegister(ExtraFeaturesPlugin.PluginGuid, "tree-respawn", OnTick, out string reason))
                throw new InvalidOperationException(reason);
            var save = new VersionedSaveDataRegistration(ExtraFeaturesPlugin.PluginGuid, "ExtraFeatures.TreeRespawn",
                1, 1, new[] { SaveEntry }, Save, ArchiveLoaded) { SimulationRelevant = true };
            if (!VersionedSaveData.TryRegister(save, out _, out reason))
                throw new InvalidOperationException(reason);

            // These publishers own the runtime after the BepInEx component has been destroyed.
            Shared.MissionEvents.Loading.Subscribe(Loading);
            Shared.MissionEvents.Started.Subscribe(Started);
            Shared.MissionEvents.Ended.Subscribe(Ended);
            VegetationR3EventHooks.OnVegetationDelete.Observable.Subscribe(OnDelete);
        }

        private void Loading(MissionLifecycleNotification note)
        {
            lock (engine)
            {
                if (note.Phase == MissionInitializationPhase.BeforeLoad)
                {
                    Reset();
                    attempt = note.Context.SessionId;
                    awaitingArchive = true;
                    return;
                }
                if (note.Context.SessionId != attempt)
                    return;

                awaitingArchive = false;
                byte[] data = archive;
                archive = null;
                if (!SimulationClock.TryGetCurrentTick(note.Context, out int tick))
                    return;

                if (note.Context.IsSave)
                {
                    captured = TreeRespawnState.Decode(data, tick, ValidSite);
                    if (data != null && captured == null)
                        Shared.DebugLogHelper.LogWarningLimited(log, "TreeRespawn.Save", "Tree Respawn save record is invalid.");
                }
                else
                    captured = CaptureSites(tick);
            }
        }

        private void Started(MissionLifecycleNotification note)
        {
            if (note.IsReplay)
                return;
            lock (engine)
            {
                settings.SetTreeRespawnMissionLocked(true);
                minutes = settings.TreeRespawnMinutes;
                mission = note.Context;
                if (attempt != mission.SessionId || captured == null ||
                    !SimulationClock.TryGetCurrentTick(mission, out int tick))
                    return;
                bool enabled = minutes != TreeRespawnState.Off && Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod);
                captured.ReconcileSettings(tick, enabled ? minutes : TreeRespawnState.Off);
                if (enabled)
                {
                    active = captured;
                    Shared.DebugLogHelper.LogInfo(log, $"Tree Respawn: {active.Count} original positions, delay {minutes} min.");
                }
            }
        }

        private void Ended(MissionLifecycleNotification note)
        {
            lock (engine)
            {
                if (note.Context.SessionId == attempt || note.Context.SessionId == mission?.SessionId)
                {
                    Reset();
                    settings.SetTreeRespawnMissionLocked(false);
                }
            }
        }

        private void Reset()
        {
            active = captured = null;
            mission = null;
            archive = null;
            attempt = 0;
            awaitingArchive = false;
            loggedPlant = false;
            deleting.Clear();
        }

        private bool Save(VersionedSaveWriter writer)
        {
            lock (engine)
            {
                // Keep the original map positions even when a later load uses current host settings.
                if (captured != null && mission?.SessionId == attempt && SimulationClock.TryGetCurrentTick(mission, out _))
                    writer.Write(SaveEntry, captured.Encode());
                return true;
            }
        }

        private void ArchiveLoaded(VersionedSaveLoad load)
        {
            lock (engine)
            {
                if (!awaitingArchive)
                    return;
                archive = null;
                if ((load.Source == VersionedSaveLoadSource.SaveGame || load.Source == VersionedSaveLoadSource.MultiplayerSave) &&
                    load.Status == VersionedSaveLoadStatus.Restored && load.Entries.TryGetValue(SaveEntry, out byte[] entry))
                    archive = entry;
            }
        }

        private void OnTick(int tick)
        {
            TreeRespawnState state = active;
            if (state == null || state.Count == 0 || !state.HasWork(tick) ||
                !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                return;
            // Normal ticks already hold the monitor. The editor can also reach the
            // time update through map resizing, outside EngineInterface.run.
            lock (engine)
            {
                if (!ReferenceEquals(state, active) || !state.HasWork(tick) ||
                    !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                    return;
                if (state.Advance(tick, minutes, hasTree, plant))
                {
                    GamePathingManagerAPI.Instance.RequestComponentRebuild();
                    if (!loggedPlant)
                    {
                        loggedPlant = true;
                        Shared.DebugLogHelper.LogInfo(log, $"Tree Respawn: first seedling planted at map tick {tick}.");
                    }
                }
            }
        }

        private void OnDelete(VegetationDeleteEventArgs args)
        {
            if (active == null)
                return;
            try
            {
                lock (engine)
                {
                    TreeRespawnState state = active;
                    if (state == null || !Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod))
                        return;

                    int id = args.VegetationId;
                    if (args.Phase == EventHookPhase.Pre)
                    {
                        if (TryReadWoodland(id, out GameVegetation tree) &&
                            TryGetTile(tree.r_TilePositionX, tree.r_TilePositionY, out int tile) &&
                            GameTileManagerAPI.Instance.GetOrganismLayer()[tile] == id &&
                            state.Contains(TreeRespawnState.KeyOf(tree.r_TilePositionX, tree.r_TilePositionY)))
                            deleting[id] = TreeRespawnState.KeyOf(tree.r_TilePositionX, tree.r_TilePositionY);
                    }
                    else if (args.Phase == EventHookPhase.Post && deleting.TryGetValue(id, out int key))
                    {
                        deleting.Remove(id);
                        if (TryGetTile((ushort)key, (ushort)(key >> 16), out int tile) &&
                            GameTileManagerAPI.Instance.GetOrganismLayer()[tile] == 0 &&
                            SimulationClock.TryGetCurrentTick(mission, out int tick))
                            state.Removed(key, tick);
                    }
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "TreeRespawn.Delete", "Tree Respawn could not observe tree deletion: " + ex);
            }
        }

        private static TreeRespawnState CaptureSites(int tick)
        {
            Span<GameVegetation> pool = GameVegetationManagerAPI.Instance.GetVegetationAsSpan();
            Span<ushort> organisms = GameTileManagerAPI.Instance.GetOrganismLayer();
            var sites = new List<TreeRespawnState.Site>();
            // The span starts at native slot zero. Native events and the organism grid use the
            // slot number directly. Extender TryGetVegetationById subtracts one from that number.
            for (int slot = 1; slot < pool.Length; slot++)
            {
                ref GameVegetation tree = ref pool[slot];
                if (IsWoodlandRecord(in tree) &&
                    TryGetTile(tree.r_TilePositionX, tree.r_TilePositionY, out int tile) && organisms[tile] == slot)
                    sites.Add(new TreeRespawnState.Site(tree.r_TilePositionX, tree.r_TilePositionY, tree.r_VegetationType));
            }
            return new TreeRespawnState(sites, tick);
        }

        private static bool IsWoodlandRecord(in GameVegetation record) =>
            record.r_AliveState != AliveState.None && TreeRespawnState.IsWoodland(record.r_VegetationType);

        private static bool TryReadWoodland(int id, out GameVegetation tree)
        {
            tree = default;
            if (id <= 0 || id >= TreeRespawnState.VegetationSlots)
                return false;
            Span<GameVegetation> pool = GameVegetationManagerAPI.Instance.GetVegetationAsSpan();
            if (id >= pool.Length || !IsWoodlandRecord(in pool[id]))
                return false;
            tree = pool[id];
            return true;
        }

        private static bool ValidSite(TreeRespawnState.Site site) => TryGetTile(site.X, site.Y, out _);

        private static bool TryGetTile(ushort x, ushort y, out int tile)
        {
            tile = 0;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsTileInsideMapBounds(x, y))
                return false;
            tile = tiles.GetTileId(x, y);
            return tiles.IsValidTileId(tile);
        }

        private static bool HasWoodland(TreeRespawnState.Site site)
        {
            if (!TryGetTile(site.X, site.Y, out int tile))
                return false;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            int id = tiles.GetOrganismLayer()[tile];
            return TryReadWoodland(id, out GameVegetation tree) &&
                tree.r_TilePositionX == site.X && tree.r_TilePositionY == site.Y;
        }

        private static bool TryPlant(TreeRespawnState.Site site)
        {
            if (!TryGetTile(site.X, site.Y, out int tile))
                return false;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (tiles.GetOrganismLayer()[tile] != 0 || tiles.GetTileBuildingId(tile) != 0 ||
                tiles.GetTileUnitId(tile) != 0 || (tiles.GetTilePropertyFlag(tile) & Blocked) != 0)
                return false;

            long id = GameVegetationManagerAPI.Instance.CreateEx(site.X, site.Y, site.Species, growthStage: (int)TreeGrowthStage.Stage0);
            return id > 0 && id < TreeRespawnState.VegetationSlots && tiles.GetOrganismLayer()[tile] == id &&
                TryReadWoodland((int)id, out GameVegetation tree) && tree.r_TilePositionX == site.X &&
                tree.r_TilePositionY == site.Y && tree.r_VegetationType == site.Species &&
                tree.r_GrowthStage == (uint)TreeGrowthStage.Stage0;
        }
    }
}
