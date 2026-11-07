using APIShared.Savegames;
using BepInEx.Logging;
using R3;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;

namespace ImprovedHunters
{
    /// <summary>
    /// A deer or goat herd that has died out returns to its start place with two animals, in the game modes
    /// where Vanilla's own respawn does not run; the herd then grows by Vanilla's own rule. A map without
    /// these herds stays without them. Everything follows from the map tick and simulation data, so all
    /// peers act alike. The start places go into the save through WildlifeOriginTable.
    /// </summary>
    internal sealed unsafe class WildlifeRespawnFeature
    {
        private const int DeerHerdKind = 0xDC;
        private const int GoatHerdKind = 0xE2;
        private const int CheckIntervalTicks = 30 * HunterSimulationTime.MapTicksPerSecond;
        private const int ReturningAnimals = 2;
        private const int CorpseFlagOffset = 0x29C;
        private const string SaveEntry = "origins";
        private const int FarthestPlace = 10;
        private const int PlaceStep = 2;

        // The places around a built-over start place, one ring after the other, the same on all peers.
        private static readonly (int X, int Y)[] Directions =
            { (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1) };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SpawnHerdDelegate(IntPtr herdManager, int kind, uint tileX, uint tileY, int height);

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly WildlifeNativeAddresses addresses;
        private readonly WildlifeOriginTable origins;
        private readonly SpawnHerdDelegate spawnHerd;
        private bool archiveLoadedForMission;
        private bool failureLogged;

        public WildlifeRespawnFeature(ManualLogSource log, ImprovedHuntersViewModel settings, WildlifeNativeAddresses addresses, WildlifeOriginTable origins)
        {
            this.log = log;
            this.settings = settings;
            this.addresses = addresses;
            this.origins = origins;
            spawnHerd = Marshal.GetDelegateForFunctionPointer<SpawnHerdDelegate>((IntPtr)(long)addresses.SpawnHerd);

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
            Shared.MissionEvents.Started.Subscribe(_ => archiveLoadedForMission = false);
        }

        public void OnTick(int mapTick)
        {
            bool checkDue = settings.RespawnWildlife && unchecked((uint)mapTick) % CheckIntervalTicks == 0;
            if (origins.IsCaptured && !checkDue)
                return;

            try
            {
                lock (origins.SyncRoot)
                {
                    if (!origins.IsCaptured)
                        CaptureHerds();

                    if (checkDue && *(int*)addresses.MultiplayerMode != 0)
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
            for (int herd = 1; herd < WildlifeNativeAddresses.HerdCount; herd++)
            {
                byte* record = addresses.GetHerdRecord(herd);
                int serial = *(int*)(record + WildlifeNativeAddresses.HerdSerialOffset);
                if (!addresses.IsWildDeerOrGoatHerd(record, out bool isDeer) ||
                    origins.TryFind(herd, serial, out _) ||
                    !APIShared.UnitAccess.TryGetById(*(short*)(record + WildlifeNativeAddresses.HerdLeaderOffset), out GameUnit* leader, out _) ||
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

        private void CheckHerds(int mapTick)
        {
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            int waitTicks = settings.RespawnIntervalSeconds * HunterSimulationTime.MapTicksPerSecond;
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
                    TryReturn(units, ref origin))
                {
                    origins[index] = origin;
                }
            }
        }

        // The usual answer needs one look at the leader. The whole herd is only read when it is dead.
        private bool HasDied(SimpleNativeArray<GameUnit> units, WildlifeOrigin origin)
        {
            byte* record = addresses.GetHerdRecord(origin.Herd);
            if (*(short*)(record + WildlifeNativeAddresses.HerdStateOffset) != WildlifeNativeAddresses.HerdStateInUse ||
                *(int*)(record + WildlifeNativeAddresses.HerdSerialOffset) != origin.HerdSerial)
            {
                return true;
            }

            if (IsLivingMember(units, *(short*)(record + WildlifeNativeAddresses.HerdLeaderOffset), origin.Herd))
                return false;

            ushort* words = (ushort*)(record + WildlifeNativeAddresses.HerdMemberBitsOffset);
            for (int word = 0; word < WildlifeNativeAddresses.HerdMemberWords; word++)
            {
                for (int bits = words[word], bit = 0; bits != 0; bits >>= 1, bit++)
                {
                    if ((bits & 1) != 0 && IsLivingMember(units, word * 16 + bit, origin.Herd))
                        return false;
                }
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

        private bool TryReturn(SimpleNativeArray<GameUnit> units, ref WildlifeOrigin origin)
        {
            int kind = origin.IsDeer ? DeerHerdKind : GoatHerdKind;
            int herd = SpawnAt(kind, origin.X, origin.Y);
            for (int reach = PlaceStep; herd <= 0 && reach <= FarthestPlace; reach += PlaceStep)
            {
                foreach ((int x, int y) in Directions)
                {
                    herd = SpawnAt(kind, origin.X + x * reach, origin.Y + y * reach);
                    if (herd > 0)
                        break;
                }
            }

            if (herd <= 0)
                return false;

            KeepFirstAnimals(units, herd);
            origin.Herd = herd;
            origin.HerdSerial = *(int*)(addresses.GetHerdRecord(herd) + WildlifeNativeAddresses.HerdSerialOffset);
            origin.ExtinctSinceTick = WildlifeOrigin.NotExtinct;
            Shared.DebugLogHelper.LogInfo(log, $"Improved Hunters wildlife returned: kind=0x{kind:X}, herd={herd}, start=({origin.X},{origin.Y}).");
            return true;
        }

        private int SpawnAt(int kind, int x, int y)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsTileInsideMapBounds(x, y))
                return 0;

            int tile = tiles.GetTileId(x, y);
            return tiles.IsValidTileId(tile)
                ? spawnHerd((IntPtr)(long)addresses.HerdManager, kind, (uint)x, (uint)y, tiles.GetTileHeight(tile))
                : 0;
        }

        // Vanilla always makes a full herd. The rest leaves in the same tick, the way Vanilla removes
        // animals over its limit (0x11B460); 0x1261F0 then takes them out of the herd, also a leader.
        private void KeepFirstAnimals(SimpleNativeArray<GameUnit> units, int herd)
        {
            ushort* words = (ushort*)(addresses.GetHerdRecord(herd) + WildlifeNativeAddresses.HerdMemberBitsOffset);
            int kept = 0;
            for (int word = 0; word < WildlifeNativeAddresses.HerdMemberWords; word++)
            {
                for (int bits = words[word], bit = 0; bits != 0; bits >>= 1, bit++)
                {
                    int unitId = word * 16 + bit;
                    if ((bits & 1) == 0 || unitId <= 0 || unitId > units.Length)
                        continue;

                    if (kept++ >= ReturningAnimals)
                        units.GetValuePointer(unitId - 1)->r_AliveState = AliveState.MarkedForDeletion;
                }
            }
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
