#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using APIShared;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using BetterPathfindingTest.Core;

namespace BetterPathfindingTest
{
    internal sealed class CaptureEnvelope
    {
        internal CapturedMap Map;
        internal ManagedEvidence Evidence;
        internal IEnemyGateRoutePolicySnapshot[] Gates;
        internal void MaterializeGates(WorkBudget budget)
        {
            SnapshotCapture.DeriveStructures(Map);
            for (int player = 1; player <= 8; player++)
            {
                IEnemyGateRoutePolicySnapshot snapshot = Gates[player]; if (snapshot == null) continue;
                var mask = new byte[Map.Flags.Length];
                // Pure immutable provider contract: never read native state on the worker.
                for (int tile = 0; tile < mask.Length; tile++) { if ((tile & 1023) == 0) budget.Check(); for (int d = 0; d < 8; d++) if (snapshot.IsDirectionAllowed(tile, d)) mask[tile] |= (byte)(1 << d); }
                Map.GateMasks[player] = mask;
            }
            Gates = null;
        }
    }
    internal sealed class SnapshotCapture
    {
        private const int AvailabilityRva = 0x3A11EA4;
        private readonly IntPtr module;
        private readonly ManagedEvidence evidence;
        internal SnapshotCapture(CrusaderLibraryLoadContext context)
        {
            module = context.ModuleHandle;
            if (module == IntPtr.Zero) throw new NotSupportedException("Native module handle is null.");
            string native = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            if (Shared.DebugLogHelper.GetFileSha256(native) != CapturedMap.AuditedHash) throw new NotSupportedException("Native hash is not audited; capture disabled.");
            var masks = new byte[8]; Marshal.Copy(IntPtr.Add(module, 0x312620), masks, 0, masks.Length);
            for (int d = 0; d < masks.Length; d++) if (masks[d] != (1 << d)) throw new NotSupportedException("Audited native direction-mask order changed.");
            ManagedContracts.Validate();
            evidence = new ManagedEvidence(BepInEx.Paths.GameRootPath, typeof(EngineInterface).Assembly, Shared.DebugLogHelper.GetFileSha256);
        }
        internal unsafe CaptureEnvelope Copy(long session, string context, double waitMs)
        {
            var watch = Stopwatch.StartNew(); var tile = GameTileManagerAPI.Instance;
            if (tile.GetTileManager() == IntPtr.Zero || tile.MapRowLookupTable == null) throw new InvalidOperationException("TileManager/row lookup unavailable for ready session.");
            var logic = tile.GetLogicLayer(); var heights = tile.GetHeightLayer(); var edges = tile.GetPathLinkageLayer(); var structures = tile.GetStructureLayer(); var organisms = tile.GetOrganismLayer(); var indices = tile.GetMoatWorkTaskIndexLayer(); var tasks = tile.GetMoatWorkTasks(); int slots = tile.GetMoatWorkTaskSlotLimit();
            if (logic.Length != 320800 || heights.Length != logic.Length || edges.Length != logic.Length || structures.Length != logic.Length || organisms.Length != logic.Length || indices.Length != logic.Length || slots < 0 || slots > tasks.Length) throw new InvalidOperationException("Live tile/moat capacities do not match audited snapshot contract.");
            IEnemyGatePathPolicy policy = EnemyGatePathPolicyBridge.Current;
            bool published = policy != null && policy.HasPublishedMask;
            var gates = GateCapture.Read(policy);
            var map = new CapturedMap { Session = session, Context = context, Identity = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"), NativeHash = CapturedMap.AuditedHash, ManagedHash = evidence.InstalledHash, MapSize = tile.GetCurrentMapSize(), LockWaitMilliseconds = waitMs };
            map.Rows = new int[2400]; Marshal.Copy((IntPtr)tile.MapRowLookupTable, map.Rows, 0, map.Rows.Length);
            map.Available = new byte[640000]; Marshal.Copy(IntPtr.Add(module, AvailabilityRva), map.Available, 0, map.Available.Length);
            var flags = logic.ToArray(); map.Flags = new uint[flags.Length]; Buffer.BlockCopy(flags, 0, map.Flags, 0, flags.Length * 4);
            map.Heights = heights.ToArray(); map.NativeEdges = edges.ToArray(); map.Structures = structures.ToArray(); map.Organisms = organisms.ToArray();
            map.MoatOwner = new byte[map.Flags.Length];
            for (int t = 0; t < indices.Length; t++) { int id = indices[t]; if (id > 0 && id < slots && id < tasks.Length && tasks[id].r_TileId == t) map.MoatOwner[t] = tasks[id].r_OwnerPlayerId; }
            var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            map.BuildingTypes = new ushort[buildings.Length + 1]; map.StructureHeights = new short[buildings.Length + 1]; map.LowWalls = new byte[buildings.Length + 1];
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            { int buildingId = spanIndex + 1; map.BuildingTypes[buildingId] = (ushort)buildings[spanIndex].r_BuildingType; }
            // Unlike unit/building spans, this SE view starts at vegetation slot zero.
            var vegetation = GameVegetationManagerAPI.Instance.GetVegetationAsSpan(); map.VegetationStates = new short[vegetation.Length];
            for (int spanIndex = 0; spanIndex < vegetation.Length; spanIndex++) map.VegetationStates[spanIndex] = vegetation[spanIndex].r_ResourceState;
            var players = GamePlayerManagerAPI.Instance;
            for (int a = 1; a <= 8; a++) for (int b = 1; b <= 8; b++) map.Allied[a * 9 + b] = players.IsPlayerAlliedTo(a, b);
            var units = GameUnitManagerAPI.Instance.GetUnitsAsSpan(); var seeds = new List<UnitSeed>(); var owners = new SortedSet<int>();
            for (int spanIndex = 0; spanIndex < units.Length && seeds.Count < 512; spanIndex++)
            {
                ref GameUnit unit = ref units[spanIndex]; int player = unit.r_ControllableForPlayerId;
                if (unit.r_AliveState != AliveState.IsAlive || unit.r_CurrentHealth == 0 || player < 1 || player > 8) continue;
                int x = unit.r_CurrentTilePositionX, y = unit.r_CurrentTilePositionY;
                if ((uint)x >= 800 || (uint)y >= 800 || map.Available[y * 800 + x] == 0) continue;
                int packed = map.Rows[y * 3] + x; if ((uint)packed >= map.Flags.Length) continue;
                int target = (uint)unit.r_TargetTilePositionX < 800 && (uint)unit.r_TargetTilePositionY < 800 ? unit.r_TargetTilePositionY * 800 + unit.r_TargetTilePositionX : -1;
                var seed = new UnitSeed { Id = spanIndex + 1, Player = player, Node = y * 800 + x, Target = target, Speed = unchecked((short)unit.r_CurrentSpeed), Speed2 = unchecked((short)unit.r_CurrentSpeed2), Bonus = unchecked((short)unit.r_SpeedBonus), Extra = unchecked((short)unit.r_PathPlanRelated1), Additional = unchecked((short)(unit.N000001E6 >> 16)), Phase = (byte)unit.N0000010E, OnMoat = (map.Flags[packed] & 0x40000000U) != 0 };
                if (!seed.OnMoat && seed.Phase != 0 && (map.Flags[packed] & 0x00200000U) != 0) seed.Speed2 = -1; // ambiguous terrain/cadence snapshot: do not claim a measured profile.
                seeds.Add(seed); owners.Add(player);
            }
            if (owners.Count == 0) { int local = players.GetLocalPlayerId(); owners.Add(local >= 1 && local <= 8 ? local : 1); }
            map.Units = seeds.ToArray(); map.Players = new int[owners.Count]; owners.CopyTo(map.Players);
            if (!ReferenceEquals(policy, EnemyGatePathPolicyBridge.Current) || (policy != null && policy.HasPublishedMask != published)) throw new CaptureNotReadyException("gate-provider-changed-during-capture");
            GateCapture.Validate(gates);
            if (policy != null) map.GateCoverage = published ? "captured-immutable-published-policy" : "provider-present-without-published-mask";
            map.CopyMilliseconds = watch.Elapsed.TotalMilliseconds; map.Validate();
            return new CaptureEnvelope { Map = map, Gates = gates, Evidence = evidence };
        }
        private static short StructureHeight(eStructs type)
        {
            switch (type)
            {
                case eStructs.STRUCT_KEEP_ONE: return 0x40; case eStructs.STRUCT_KEEP_TWO: return 0x5C; case eStructs.STRUCT_KEEP_THREE: return 0xBE;
                case eStructs.STRUCT_GATE_MAIN: case eStructs.STRUCT_GATE_INNER: return 0x80;
                case eStructs.STRUCT_SIEGE_TOWER: return 0x76; case eStructs.STRUCT_TOWER1: return 0x128; case eStructs.STRUCT_TOWER2: return 0x94;
                case eStructs.STRUCT_TOWER3: return 0xB4; case eStructs.STRUCT_TOWER4: case eStructs.STRUCT_TOWER5: return 0xC0; default: return 0;
            }
        }
        internal static void DeriveStructures(CapturedMap map)
        {
            for (int id = 1; id < map.BuildingTypes.Length; id++)
            { eStructs kind = (eStructs)map.BuildingTypes[id]; map.StructureHeights[id] = StructureHeight(kind); map.LowWalls[id] = kind == eStructs.STRUCT_GATE_MAIN || kind == eStructs.STRUCT_GATE_INNER ? (byte)1 : (byte)0; }
        }
    }
}
#endif
