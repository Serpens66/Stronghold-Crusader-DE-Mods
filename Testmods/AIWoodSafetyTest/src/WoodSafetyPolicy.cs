using System;
using System.Collections.Generic;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AIWoodSafetyTest
{
    internal readonly struct SafetyRect : IEquatable<SafetyRect>
    {
        internal readonly int X, Y, Side;
        internal SafetyRect(int x, int y, int side) { X = x; Y = y; Side = side; }
        internal bool Valid => Side > 0 && X >= 0 && Y >= 0 && (long)X + Side <= ThreatMasks.MapSide && (long)Y + Side <= ThreatMasks.MapSide;
        internal static long DistanceSquared(SafetyRect a, SafetyRect b)
        {
            long dx = Math.Max(0L, Math.Max((long)a.X - b.X - b.Side + 1, (long)b.X - a.X - a.Side + 1));
            long dy = Math.Max(0L, Math.Max((long)a.Y - b.Y - b.Side + 1, (long)b.Y - a.Y - a.Side + 1));
            return dx * dx + dy * dy;
        }
        public bool Equals(SafetyRect other) => X == other.X && Y == other.Y && Side == other.Side;
    }
    internal readonly struct Fortification : IEquatable<Fortification>
    {
        internal readonly int Slot, Owner;
        internal readonly uint Global;
        internal readonly eStructs Type;
        internal readonly SafetyRect Footprint;
        internal readonly bool Standing;
        internal Fortification(int slot, uint global, int owner, eStructs type, SafetyRect footprint, bool standing)
        { Slot = slot; Global = global; Owner = owner; Type = type; Footprint = footprint; Standing = standing; }
        internal bool Threat => Standing && Global != 0 && Owner >= 1 && Owner <= 8 && IsIntactType(Type);
        internal static bool IsIntactType(eStructs t) => t == eStructs.STRUCT_TOWER ||
            t == eStructs.STRUCT_TOWER1 || t == eStructs.STRUCT_TOWER2 || t == eStructs.STRUCT_TOWER3 || t == eStructs.STRUCT_TOWER4 || t == eStructs.STRUCT_TOWER5 ||
            t == eStructs.STRUCT_GATE_MAIN || t == eStructs.STRUCT_GATE_INNER || t == eStructs.STRUCT_GATE_WOOD || t == eStructs.STRUCT_GATE_POSTERN || t == eStructs.STRUCT_GATEHOUSE ||
            t == eStructs.STRUCT_GATE_WOOD1A || t == eStructs.STRUCT_GATE_WOOD1B || t == eStructs.STRUCT_GATE_WOOD1C || t == eStructs.STRUCT_GATE_WOOD1D ||
            t == eStructs.STRUCT_GATE_STONE1A || t == eStructs.STRUCT_GATE_STONE1B || t == eStructs.STRUCT_GATE_STONE2A || t == eStructs.STRUCT_GATE_STONE2B;
        internal static bool IsTrackedType(eStructs t) => IsIntactType(t) || t == eStructs.STRUCT_TOWER1_DESTROYED ||
            t == eStructs.STRUCT_TOWER2_DESTROYED || t == eStructs.STRUCT_TOWER3_DESTROYED || t == eStructs.STRUCT_TOWER4_DESTROYED || t == eStructs.STRUCT_TOWER5_DESTROYED;
        public bool Equals(Fortification b) => Slot == b.Slot && Owner == b.Owner && Global == b.Global && Type == b.Type && Standing == b.Standing && Footprint.Equals(b.Footprint);
    }
    internal sealed class ThreatMasks
    {
        internal const int MapSide = 800, OwnerStride = MapSide * MapSide;
        // Audited fixed 3x3 footprint: native 0x51540 -> mapper woodsman -> 0x619C0.
        internal const int HutSide = 3;
        internal readonly ushort[] Counts = new ushort[9 * OwnerStride];
        private readonly Dictionary<int, Fortification> records = new Dictionary<int, Fortification>();
        internal int Range { get; private set; } = -1;
        internal int RecordCount => records.Count;
        internal readonly int[] OwnerThreats = new int[9];
        internal int EnemyOwnerBits(int player, int[] teams)
        {
            int bits = 0;
            for (int owner = 1; owner <= 8; owner++)
                if (owner != player && OwnerThreats[owner] != 0 && teams[owner] != teams[player]) bits |= 1 << owner;
            return bits;
        }
        internal void Clear() { records.Clear(); Array.Clear(Counts, 0, Counts.Length); Array.Clear(OwnerThreats, 0, 9); Range = -1; }
        internal void CopySlots(List<int> destination) { foreach (int id in records.Keys) destination.Add(id); }
        internal void SetRange(int range)
        {
            if (range < 0) throw new ArgumentOutOfRangeException(nameof(range));
            if (Range == range) return;
            Array.Clear(Counts, 0, Counts.Length); Range = range;
            foreach (var record in records.Values) if (record.Threat) Paint(record, 1);
        }
        internal void Update(int slot, Fortification? current)
        {
            if (records.TryGetValue(slot, out var old))
            {
                if (current.HasValue && old.Equals(current.Value)) return;
                if (old.Threat) { if (Range >= 0) Paint(old, -1); OwnerThreats[old.Owner]--; }
                records.Remove(slot);
            }
            if (!current.HasValue || !Fortification.IsTrackedType(current.Value.Type) || !current.Value.Standing) return;
            var next = current.Value;
            if (next.Threat && !next.Footprint.Valid) throw new InvalidOperationException("Fortification footprint invalid: " + slot);
            records.Add(slot, next);
            if (next.Threat) { if (Range >= 0) Paint(next, 1); OwnerThreats[next.Owner]++; }
        }
        private void Paint(Fortification source, int delta)
        {
            var b = source.Footprint;
            // Bounded local rasterization, never done by the candidate callback.
            int loX = (int)Math.Max(0L, (long)b.X - Range - HutSide + 1), hiX = (int)Math.Min(MapSide - HutSide, (long)b.X + b.Side - 1 + Range);
            int loY = (int)Math.Max(0L, (long)b.Y - Range - HutSide + 1), hiY = (int)Math.Min(MapSide - HutSide, (long)b.Y + b.Side - 1 + Range);
            long radiusSquared = (long)Range * Range;
            for (int x = loX; x <= hiX; x++) for (int y = loY; y <= hiY; y++)
                if (SafetyRect.DistanceSquared(new SafetyRect(x, y, HutSide), b) <= radiusSquared)
                {
                    int at = source.Owner * OwnerStride + x * MapSide + y;
                    Counts[at] = checked((ushort)(Counts[at] + delta));
                }
        }
        internal bool Blocks(int enemyBits, int x, int y)
        {
            if (x < 0 || y < 0 || x > MapSide - HutSide || y > MapSide - HutSide) return false;
            int cell = x * MapSide + y;
            for (int owner = 1; owner <= 8; owner++)
                if ((enemyBits & (1 << owner)) != 0 && Counts[owner * OwnerStride + cell] != 0) return true;
            return false;
        }
    }
    internal static unsafe class DeathCounterPolicy
    {
        // State 110 is shared by death and return failure. The 16-bit death marker
        // (despite its projectile name) is set by both native fatal damage paths.
        internal static bool CanCorrect(in GameUnit unit, int unitId, in GameBuilding hut, int hutId, short expected, short actual) =>
            unitId > 0 && hutId > 0 && unit.r_GlobalId != 0 && hut.r_GlobalId != 0 && expected == actual &&
            unit.r_UnitChimp == eChimps.CHIMP_TYPE_WOODCUTTER && unit.r_AIState == 110 &&
            unit.r_AliveState == AliveState.MarkedForDeletion && unit.r_CurrentHealth == 0 && unit.r_IsKilledByProjectile != 0 &&
            unit.r_LinkedProductionBuildingId == hutId && unit.r_LinkedProductionBuildingGlobalId == hut.r_GlobalId &&
            unit.r_ControllableForPlayerId == hut.r_PlayerIdOwner && hut.r_PlayerIdOwner >= 1 && hut.r_PlayerIdOwner <= 8 &&
            hut.r_BuildingType == eStructs.STRUCT_WOODCUTTERS_HUT && (hut.r_AliveState == AliveState.IsAlive || hut.r_AliveState == AliveState.Paused);
        internal static short UndoSingleIncrement(short after) => unchecked((short)(after - 10));
        internal static bool TryUndo(GameUnit* unit, int unitId, uint expectedUnitGlobal, GameBuilding* hut, int hutId, uint expectedHutGlobal, short expected, bool ai)
        {
            if (!ai || unit == null || hut == null || unit->r_GlobalId != expectedUnitGlobal || hut->r_GlobalId != expectedHutGlobal) return false;
            short* counter = (short*)((byte*)hut + 0x2DC);
            if (!CanCorrect(in *unit, unitId, in *hut, hutId, expected, *counter)) return false;
            *counter = UndoSingleIncrement(expected);
            return true;
        }
    }
}
