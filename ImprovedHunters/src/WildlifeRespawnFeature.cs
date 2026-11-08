using APIShared.Savegames;
using APIShared.Wildlife;
using BepInEx.Logging;
using R3;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ImprovedHunters
{
    /// <summary>
    /// A deer or goat herd that has died out returns to its start place with two animals, in the game modes
    /// where Vanilla's own respawn does not run; the herd then grows by Vanilla's own rule. A new map can also
    /// give every player a herd near the keep. Without that, a map without these herds stays without them.
    /// Everything follows from the map tick and simulation data, so all peers act alike. The start places go
    /// into the save through WildlifeOriginTable. Herds are read and spawned through APIShared's Herds service.
    /// </summary>
    internal sealed unsafe class WildlifeRespawnFeature
    {
        private const int CheckIntervalTicks = 30 * HunterSimulationTime.MapTicksPerSecond;
        // First boundary of the map's time, so peers read the same herds even if one starts a little later.
        // Soon, because a herd already in flight when the map starts moves on quickly.
        private const int CaptureIntervalTicks = 10;
        private const int ReturningAnimals = 2;
        // Vanilla's smallest herds, so that every player's start herd has the same size.
        private const int StartDeer = 9;
        private const int StartGoats = 4;
        private const int MaximumPlayers = 8;
        private const int MapMiddle = 400;
        private const int CorpseFlagOffset = 0x29C;
        private const string SaveEntry = "origins";
        private const int FarthestPlace = 10;
        private const int PlaceStep = 2;

        // The places around a built-over start place, one ring after the other, the same on all peers.
        private static readonly (int X, int Y)[] Directions =
            { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) };

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly WildlifeNativeAddresses addresses;
        private readonly WildlifeOriginTable origins;
        private readonly HerdFleeSightFeature sectors;
        private readonly List<int> members = new List<int>(32);
        private bool archiveLoadedForMission;
        private bool startedFromSave;
        private bool mapHasHerds;
        private bool startHerdsPending;
        private bool failureLogged;

        public WildlifeRespawnFeature(ManualLogSource log, ImprovedHuntersViewModel settings, WildlifeNativeAddresses addresses, WildlifeOriginTable origins, HerdFleeSightFeature sectors)
        {
            this.log = log;
            this.settings = settings;
            this.addresses = addresses;
            this.origins = origins;
            this.sectors = sectors;

            var registration = new VersionedSaveDataRegistration(
                ImprovedHuntersPlugin.PluginGuid,
                "ImprovedHunters.WildlifeOrigins",
                1,
                1,
                new[] { SaveEntry },
                CaptureForSave,
                OnArchiveLoaded)
            {
                SimulationRelevant = true
            };
            if (!VersionedSaveData.TryRegister(registration, out _, out string reason))
                throw new InvalidOperationException(reason);

            // A load's own result stays when the earlier mission reports its end after that load.
            Shared.MissionEvents.Ended.Subscribe(_ =>
            {
                if (archiveLoadedForMission)
                    return;

                lock (origins.SyncRoot)
                    origins.Clear();
            });
            Shared.MissionEvents.Started.Subscribe(started =>
            {
                archiveLoadedForMission = false;
                startedFromSave = started.Context.IsSave;
                startHerdsPending = false;
            });
        }

        public void OnTick(int mapTick)
        {
            // Every tick comes through here, so nothing is read beyond these two checks between the boundaries.
            uint tick = unchecked((uint)mapTick);
            bool captureDue = (!origins.IsCaptured || startHerdsPending) && tick % CaptureIntervalTicks == 0;
            bool checkDue = settings.HerdReturnSeconds != HerdSettingsPolicy.Off && tick % CheckIntervalTicks == 0;
            if (!captureDue && !checkDue)
                return;

            try
            {
                lock (origins.SyncRoot)
                {
                    if (captureDue)
                    {
                        if (!origins.IsCaptured)
                        {
                            CaptureHerds();
                            mapHasHerds = origins.Count > 0;
                        }

                        startHerdsPending = !TryPlaceStartHerds();
                    }

                    if (checkDue && origins.IsCaptured && *(int*)addresses.MultiplayerMode != 0)
                    {
                        long measured = 0;
                        Shared.DevDiag.Start(ref measured);
                        CheckHerds(mapTick);
                        Shared.DevDiag.StopSpike(ref measured, "hunters.herdCheck", 5000);
                    }
                }
            }
            catch (Exception exception)
            {
                if (failureLogged)
                    return;

                failureLogged = true;
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters wildlife respawn failed: {exception}");
            }
        }

        // On a new map this is what its creator placed. A save without this record has only today's places.
        private void CaptureHerds()
        {
            origins.MarkCaptured();
            for (int herd = 1; herd <= Herds.MaximumHerdId; herd++)
            {
                if (!Herds.TryGet(herd, out HerdInfo info) || !WildlifeNativeAddresses.IsWildDeerOrGoatHerd(info, out bool isDeer))
                    continue;

                int serial = unchecked((int)info.GlobalId);
                if (origins.TryMarkRespawning(herd, serial) ||
                    !APIShared.UnitAccess.TryGetById(info.LeaderUnitId, out GameUnit* leader, out _) ||
                    leader == null)
                {
                    continue;
                }

                origins.TryAdd(new WildlifeOrigin
                {
                    IsDeer = isDeer,
                    Respawns = true,
                    X = (short)leader->r_CurrentTilePositionX,
                    Y = (short)leader->r_CurrentTilePositionY,
                    Herd = herd,
                    HerdSerial = serial,
                    ExtinctSinceTick = WildlifeOrigin.NotExtinct
                });
            }

            Shared.DebugLogHelper.LogInfo(log, $"Improved Hunters follows {origins.Count} deer and goat herds.");
        }

        // Only a new map: a save has its herds already, also one that has no record of them.
        // False while the keeps are not ready yet, so that the next boundary tries again.
        private bool TryPlaceStartHerds()
        {
            int mode = settings.StartHerdMode;
            bool deerAllowed = settings.HuntDeer;
            if (startedFromSave || *(int*)addresses.MultiplayerMode == 0 ||
                !HerdSettingsPolicy.WantsStartHerds(mode, mapHasHerds) || (!deerAllowed && !settings.HuntGoat))
            {
                return true;
            }

            if (!Shared.ActivePlayerKeepReadiness.TryCapture(out Shared.ActivePlayerKeepSnapshot keeps, out string failure))
            {
                Shared.DebugLogHelper.LogDebug(log, $"Improved Hunters start herds wait: {failure}");
                return false;
            }

            int count = keeps.PlayerIds.Length;
            var centerX = new int[count];
            var centerY = new int[count];
            for (int index = 0; index < count; index++)
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(keeps.KeepBuildingIds[index], out GameBuilding* keep) || keep == null)
                    return false;

                centerX[index] = (int)keep->r_TilePositionXBegin + (int)keep->r_OccupyTileGridSize / 2;
                centerY[index] = (int)keep->r_TilePositionYBegin + (int)keep->r_OccupyTileGridSize / 2;
            }

            HerdKind kind = deerAllowed ? HerdKind.Deer : HerdKind.Goat;
            int animals = deerAllowed ? StartDeer : StartGoats;
            for (int index = 0; index < count; index++)
                PlaceStartHerd(keeps.PlayerIds[index], index, centerX, centerY, kind, animals);
            return true;
        }

        private void PlaceStartHerd(int player, int index, int[] centerX, int[] centerY, HerdKind kind, int animals)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            var door = GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(player);
            int doorTile = tiles.GetTileId(door.X, door.Y);
            int outside = 0, threats = 0, nearKeep = 0, blocked = 0, otherLand = 0, refused = 0;
            bool doorOnLand = tiles.IsValidTileId(doorTile) && (uint)doorTile < (uint)components.Length;
            foreach ((int x, int y) in WildlifeStartPlaces.Around(centerX[index], centerY[index], MapMiddle, MapMiddle))
            {
                if (!tiles.IsTileInsideMapBounds(x, y)) { outside++; continue; }
                if (sectors.SectorThreats(x, y) != 0) { threats++; continue; }
                if (IsNearOtherKeep(centerX, centerY, index, x, y)) { nearKeep++; continue; }

                int tile = tiles.GetTileId(x, y);
                if (!tiles.IsValidTileId(tile) || !tiles.IsTileWalkableAndUnoccupied(tile)) { blocked++; continue; }

                // An AI castle's walls can part the door from the land around it, its gates connect them.
                if (!doorOnLand || (uint)tile >= (uint)components.Length ||
                    !GamePathingManagerAPI.Instance.ArePathComponentsConnected(player, components[doorTile], components[tile], PathConnectionQueryMode.ExcludeLadderClimb))
                {
                    otherLand++;
                    continue;
                }

                if (!TrySpawn(kind, x, y, animals, out int herd, out int serial)) { refused++; continue; }

                origins.TryAdd(new WildlifeOrigin
                {
                    IsDeer = kind == HerdKind.Deer,
                    Respawns = true,
                    X = (short)x,
                    Y = (short)y,
                    Herd = herd,
                    HerdSerial = serial,
                    ExtinctSinceTick = WildlifeOrigin.NotExtinct
                });
                Shared.DebugLogHelper.LogInfo(log, $"Improved Hunters start herd: player={player}, kind={kind}, herd={herd}, tile=({x},{y}), keep=({centerX[index]},{centerY[index]}).");
                return;
            }

            Shared.DebugLogHelper.LogWarning(log, $"Improved Hunters found no start herd place for player {player}: keep=({centerX[index]},{centerY[index]}), door=({door.X},{door.Y}), " +
                $"outside={outside}, threats={threats}, nearKeep={nearKeep}, blocked={blocked}, otherLand={otherLand}, refused={refused}.");
        }

        private static bool IsNearOtherKeep(int[] centerX, int[] centerY, int index, int x, int y)
        {
            for (int other = 0; other < centerX.Length; other++)
            {
                if (other != index && Math.Max(Math.Abs(centerX[other] - x), Math.Abs(centerY[other] - y)) < WildlifeStartPlaces.NearestDistance)
                    return true;
            }

            return false;
        }

        private void CheckHerds(int mapTick)
        {
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            int waitTicks = settings.HerdReturnSeconds * HunterSimulationTime.MapTicksPerSecond;
            for (int index = 0; index < origins.Count; index++)
            {
                WildlifeOrigin origin = origins[index];
                if (!origin.Respawns)
                    continue;

                if (!HasDied(units, origin))
                {
                    if (origin.ExtinctSinceTick != WildlifeOrigin.NotExtinct)
                    {
                        origin.ExtinctSinceTick = WildlifeOrigin.NotExtinct;
                        origins[index] = origin;
                    }

                    continue;
                }

                if (origin.ExtinctSinceTick == WildlifeOrigin.NotExtinct)
                {
                    origin.ExtinctSinceTick = mapTick;
                    origins[index] = origin;
                    continue;
                }

                if (unchecked(mapTick - origin.ExtinctSinceTick) >= waitTicks &&
                    (origin.IsDeer ? settings.HuntDeer : settings.HuntGoat) &&
                    TryReturn(ref origin))
                {
                    origins[index] = origin;
                }
            }
        }

        // The usual answer needs one look at the leader. The whole herd is only read when it is dead.
        private bool HasDied(SimpleNativeArray<GameUnit> units, WildlifeOrigin origin)
        {
            if (!Herds.TryGet(origin.Herd, out HerdInfo herd) || unchecked((int)herd.GlobalId) != origin.HerdSerial)
                return true;

            if (IsLivingMember(units, herd.LeaderUnitId, origin.Herd))
                return false;

            Herds.TryCopyMemberUnitIds(origin.Herd, members);
            foreach (int unitId in members)
            {
                if (IsLivingMember(units, unitId, origin.Herd))
                    return false;
            }

            return true;
        }

        private static bool IsLivingMember(SimpleNativeArray<GameUnit> units, int unitId, int herd)
        {
            if (unitId <= 0 || unitId > units.Length)
                return false;

            GameUnit* unit = units.GetValuePointer(unitId - 1);
            return unit->r_TribeId == herd &&
                *(ushort*)((byte*)unit + CorpseFlagOffset) == 0 &&
                (unit->r_AliveState == AliveState.NeedsInit || (unit->r_AliveState == AliveState.IsAlive && unit->r_CurrentHealth > 0));
        }

        private bool TryReturn(ref WildlifeOrigin origin)
        {
            HerdKind kind = origin.IsDeer ? HerdKind.Deer : HerdKind.Goat;
            bool spawned = TrySpawn(kind, origin.X, origin.Y, ReturningAnimals, out int herd, out int serial);
            for (int reach = PlaceStep; !spawned && reach <= FarthestPlace; reach += PlaceStep)
            {
                foreach ((int x, int y) in Directions)
                {
                    spawned = TrySpawn(kind, origin.X + x * reach, origin.Y + y * reach, ReturningAnimals, out herd, out serial);
                    if (spawned)
                        break;
                }
            }

            if (!spawned)
                return false;

            origin.Herd = herd;
            origin.HerdSerial = serial;
            origin.ExtinctSinceTick = WildlifeOrigin.NotExtinct;
            Shared.DebugLogHelper.LogInfo(log, $"Improved Hunters wildlife returned: kind={kind}, herd={herd}, start=({origin.X},{origin.Y}).");
            return true;
        }

        // APIShared spawns the way Vanilla's own respawn does (0x11E150, in the simulation tick) and lets all animals
        // above the wanted number leave in the same tick, Vanilla's own path for animals over its limit.
        private static bool TrySpawn(HerdKind kind, int x, int y, int animals, out int herd, out int serial)
        {
            serial = 0;
            if (!Herds.TrySpawn(kind, x, y, animals, out herd, out _) || !Herds.TryGet(herd, out HerdInfo info))
                return false;

            serial = unchecked((int)info.GlobalId);
            return true;
        }

        private bool CaptureForSave(VersionedSaveWriter writer)
        {
            lock (origins.SyncRoot)
            {
                // Without this entry the load reads the herds again, as for an older save.
                if (origins.IsCaptured)
                    writer.Write(SaveEntry, origins.Encode());
            }

            return true;
        }

        private void OnArchiveLoaded(VersionedSaveLoad load)
        {
            archiveLoadedForMission = true;
            lock (origins.SyncRoot)
            {
                origins.Clear();
                if (load.Status != VersionedSaveLoadStatus.Restored || !load.Entries.TryGetValue(SaveEntry, out byte[] data))
                {
                    if (load.Status != VersionedSaveLoadStatus.Restored && load.Status != VersionedSaveLoadStatus.NoRecord && load.Status != VersionedSaveLoadStatus.OutOfScope)
                        Shared.DebugLogHelper.LogWarning(log, $"Improved Hunters did not use the saved herd start places: {load.Status}.");

                    return;
                }

                if (!origins.TryRestore(data))
                    Shared.DebugLogHelper.LogError(log, "Improved Hunters found damaged herd start places; herds start from where they are now.");
            }
        }
    }
}
