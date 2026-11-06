using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Simulation;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    // Real-multiplayer check of ImprovedHunters. Host and client run the same fixture at the same map tick
    // inside the simulation callback, so it cannot cause a difference itself and sends nothing. Afterwards
    // each peer logs a hash of its world at fixed map ticks; identical lines in both logs mean the peers
    // simulated the same hunt. Compare with tools/Compare-HuntHashes.py.
    internal static unsafe class HuntMpSteps
    {
        private const int AiStateOffset = 0x2BC, HunterTargetIdOffset = 0x39A, HashIntervalTicks = 200;
        private const int MaxPlayers = 8, DeerPerPlayer = 3, RabbitsPerPlayer = 3, SoldiersPerPlayer = 3;
        private static bool registered;
        private static int spawnTick = int.MinValue;
        private static bool spawned;

        internal static void Register()
        {
            Runner.Register("HuntMp.Arm", s => GameSteps.Do(() => Arm(s.Int(0))));
        }

        private static void Arm(int tick)
        {
            Runner.Require(SimulationTickEvents.TryGetCurrentTick(out int current), "map tick unavailable");
            Runner.Require(unchecked(current - tick) < 0, "fixture tick " + tick + " already passed at " + current);
            spawnTick = tick;
            spawned = false;
            if (!registered)
            {
                Runner.Require(SimulationTickEvents.TryRegister("UiAcceptance_Serp", "hunt-mp", OnTick, out string reason, 900), reason);
                registered = true;
            }
            AcceptLog.Info("HUNTMP_ARMED current=" + current + " spawnTick=" + tick);
        }

        private static void OnTick(int tick)
        {
            if (spawnTick == int.MinValue) return;
            if (!spawned && tick == spawnTick)
            {
                spawned = true;
                SpawnForAllPlayers();
            }
            if (spawned && tick % HashIntervalTicks == 0)
                LogHash(tick);
        }

        private static void SpawnForAllPlayers()
        {
            for (int player = 1; player <= MaxPlayers; player++)
            {
                int lordId = GamePlayerManagerAPI.Instance.GetLordUnitId(player);
                if (lordId <= 0 || !UnitAccess.TryGetById(lordId, out GameUnit* lord, out _) || lord == null)
                    continue;
                HuntSteps.PlaceBuildings(player);
                int x = lord->r_CurrentTilePositionX, y = lord->r_CurrentTilePositionY, height = lord->r_HeightElevation;
                int tribe = checked((int)GameTribeManagerAPI.Instance.Create(player, false));
                for (int index = 0; index < SoldiersPerPlayer; index++)
                {
                    int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(player, player, x + 3 + index, y + 3, height, eChimps.CHIMP_TYPE_SWORDSMAN));
                    if (id > 0 && UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit != null)
                    {
                        if (unit->r_TribeId > 0) GameTribeManagerAPI.Instance.UnassignUnit(unit->r_TribeId, id);
                        GameTribeManagerAPI.Instance.AssignUnit(tribe, id);
                    }
                }
                for (int index = 0; index < RabbitsPerPlayer; index++)
                    GameUnitManagerAPI.Instance.CreateUnitLocal(0, 0, x + 3 + index, y + 4, height, eChimps.CHIMP_TYPE_RABBIT);
                for (int index = 0; index < DeerPerPlayer; index++)
                    GameUnitManagerAPI.Instance.CreateUnitLocal(0, 0, x + 12 + index * 2, y - 12, height, eChimps.CHIMP_TYPE_DEER);
                AcceptLog.Info("HUNTMP_SPAWNED player=" + player + " lord=" + x + "," + y);
            }
        }

        private static void LogHash(int tick)
        {
            ulong hash = 14695981039346656037UL;
            int units = 0, hunters = 0, targeting = 0;
            SimpleNativeArray<GameUnit> array = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < array.Length; index++)
            {
                GameUnit* unit = array.GetValuePointer(index);
                if (unit->r_AliveState == AliveState.None) continue;
                units++;
                byte* bytes = (byte*)unit;
                ushort target = *(ushort*)(bytes + HunterTargetIdOffset);
                if (unit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER)
                {
                    hunters++;
                    if (target != 0) targeting++;
                }
                hash = Mix(hash, (uint)index);
                hash = Mix(hash, (uint)unit->r_UnitChimp);
                hash = Mix(hash, unit->r_GlobalId);
                hash = Mix(hash, (uint)unit->r_ControllableForPlayerId);
                hash = Mix(hash, (uint)unit->r_CurrentTilePositionX);
                hash = Mix(hash, (uint)unit->r_CurrentTilePositionY);
                hash = Mix(hash, (uint)unit->r_CurrentHealth);
                hash = Mix(hash, *(ushort*)(bytes + AiStateOffset));
                hash = Mix(hash, target);
            }
            var meat = new List<int>();
            for (int player = 1; player <= MaxPlayers; player++)
            {
                int amount = GamePlayerManagerAPI.Instance.GetGoodAmount(player, eGoods.STORED_FOOD_MEAT);
                meat.Add(amount);
                hash = Mix(hash, (uint)amount);
            }
            AcceptLog.Info("HUNT_HASH tick=" + tick + " hash=" + hash.ToString("X16") + " units=" + units +
                " hunters=" + hunters + " targeting=" + targeting + " meat=" + string.Join(",", meat));
        }

        private static ulong Mix(ulong hash, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (value >> shift) & 0xFF;
                hash *= 1099511628211UL;
            }
            return hash;
        }
    }
}
