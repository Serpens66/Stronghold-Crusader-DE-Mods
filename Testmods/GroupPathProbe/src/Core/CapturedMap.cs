// Frozen copy of Testmods/BetterPathfindingTest/src/Core/CapturedMap.cs at df29be62e.
// Isolates this offline probe from parallel BetterPathfindingTest work.
using System;
using System.Collections.Generic;

namespace GroupPathProbe.Core
{
    public sealed class UnitSeed
    {
        public int Id, Player, Node, Target, Speed, Speed2, Bonus, Extra, Additional, Phase;
        public bool OnMoat;
        public bool TryProfile(out long ground, out long moat, out int progress)
        {
            ground = moat = 0; progress = 0;
            if (Speed < 0 || Speed2 < 0 || Bonus < 0 || Extra < 0 || Additional < 0 || Phase < 0 || Phase > 24 || (Phase & 3) != 0) return false;
            if (Phase != 0 && Speed2 < Speed) return false;
            if (!OnMoat && Phase == 0 && Speed2 - Speed == 3) return false;
            long advance = (long)Bonus + Additional + 1;
            if (advance > int.MaxValue) return false;
            progress = (int)advance; ground = 8L * ((Phase != 0 ? Speed : Speed2) + (long)Bonus + Extra + 1); moat = ground + 48;
            return ground > 0;
        }
    }
    public sealed class CapturedMap
    {
        public const string AuditedHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        public string NativeHash = "synthetic", ManagedHash = "", Identity = "", GateCoverage = "no-provider", Context = "";
        public long Session; public int Width = 800, Height = 800, MapSize;
        public byte[] Available, Heights, NativeEdges, MoatOwner;
        public uint[] Flags;
        public int[] Rows;
        public ushort[] Structures, Organisms, BuildingTypes;
        public short[] StructureHeights, VegetationStates;
        public byte[] LowWalls;
        public bool[] Allied = new bool[81];
        public int[] Players = new int[0];
        public byte[][] GateMasks = new byte[9][];
        public UnitSeed[] Units = new UnitSeed[0];
        public double CopyMilliseconds, LockWaitMilliseconds;
        public long BufferBytes
        { get { long n = 1024; foreach (Array a in new Array[] { Available, Heights, NativeEdges, MoatOwner, Flags, Rows, Structures, Organisms, BuildingTypes, StructureHeights, VegetationStates, LowWalls, Allied, Players }) if (a != null) n += BufferSize(a); if (GateMasks != null) foreach (byte[] mask in GateMasks) if (mask != null) n += mask.Length + 24; return n + (Units?.Length ?? 0) * 128L; } }
        private static long BufferSize(Array a) => System.Buffer.ByteLength(a) + 32L;
        public void Validate()
        {
            long count = (long)Width * Height;
            if (Width < 1 || Height < 1 || count > 640000 || Available == null || Available.Length != count || Rows == null || Rows.Length != Height * 3 || Flags == null || Flags.Length == 0 || Flags.Length > 320800 || Heights == null || NativeEdges == null || MoatOwner == null || Structures == null || Organisms == null || Heights.Length != Flags.Length || NativeEdges.Length != Flags.Length || MoatOwner.Length != Flags.Length || Structures.Length != Flags.Length || Organisms.Length != Flags.Length || Allied == null || Allied.Length != 81) throw new ArgumentException("Incomplete captured arrays.");
            if (BuildingTypes == null || StructureHeights == null || LowWalls == null || BuildingTypes.Length == 0 || BuildingTypes.Length > 10001 || StructureHeights.Length != BuildingTypes.Length || LowWalls.Length != BuildingTypes.Length || VegetationStates == null || VegetationStates.Length > 65536 || Players == null || Players.Length == 0 || Players.Length > 8 || GateMasks == null || GateMasks.Length != 9 || Units == null || Units.Length > 10000) throw new ArgumentException("Incomplete captured contexts.");
            var players = new HashSet<int>();
            foreach (int player in Players) if (player < 1 || player > 8 || !players.Add(player)) throw new ArgumentException("Player snapshot.");
            foreach (byte[] mask in GateMasks) if (mask != null && mask.Length != Flags.Length) throw new ArgumentException("Gate mask capacity.");
            foreach (UnitSeed unit in Units) if (unit == null || unit.Player < 1 || unit.Player > 8 || unit.Node < 0 || unit.Node >= count || unit.Target < -1 || unit.Target >= count) throw new ArgumentException("Unit snapshot.");
            for (int n = 0; n < count; n++) if (Available[n] != 0 && ((long)Rows[n / Width * 3] + n % Width < 0 || (long)Rows[n / Width * 3] + n % Width >= Flags.Length)) throw new ArgumentException("Available coordinate outside packed storage.");
        }
        // These masks are native machine contracts from the audited DAFD0/Precise predicate,
        // not independent game-domain IDs. Building classes are converted using SE enums.
        public GridSnapshot Build(int player, bool groundOnly = false, WorkBudget budget = null)
        {
            if (player < 1 || player > 8) throw new ArgumentOutOfRangeException(nameof(player));
            Validate(); var valid = new byte[Width * Height]; var words = new uint[valid.Length];
            for (int n = 0; n < valid.Length; n++) { if ((n & 4095) == 0) budget?.Check(); int tile = Tile(n); if (Available[n] != 0 && (uint)tile < Flags.Length) valid[n] = 1; }
            for (int node = 0; node < valid.Length; node++)
            {
                if ((node & 1023) == 0) budget?.Check();
                if (valid[node] == 0) continue;
                int x = node % Width, y = node / Width, a = Tile(node);
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + GridSnapshot.Dx[d], ny = y + GridSnapshot.Dy[d];
                    if ((uint)nx >= Width || (uint)ny >= Height) continue;
                    int next = ny * Width + nx; if (valid[next] == 0) continue; int b = Tile(next);
                    if (!Allows(player, x, y, a, nx, ny, b, d, groundOnly, out bool wet, out bool structure)) continue;
                    words[node] |= (1U << d) | (wet ? 1U << (8 + d) : 0U) | (structure ? 1U << (16 + d) : 0U);
                }
            }
            return new GridSnapshot(Width, Height, valid, words, player);
        }
        public int Tile(int node) => Rows[node / Width * 3] + node % Width;
        private bool Friendly(int player, int tile)
        { int owner = MoatOwner[tile]; return owner > 0 && owner <= 8 && (owner == player || Allied[player * 9 + owner]); }
        private bool Wet(int tile) => (Flags[tile] & 0x40000000U) != 0;
        private bool Special(int tile) => Organisms[tile] < VegetationStates.Length && VegetationStates[Organisms[tile]] > 4 && VegetationStates[Organisms[tile]] != 15;
        private bool Structural(int tile) => (Flags[tile] & 0x10900U) != 0 || Structures[tile] != 0 || ((Flags[tile] & 0x1000U) != 0 && Special(tile));
        private bool Adjacent(int tile)
        { uint flags = Flags[tile]; if ((flags & 0x40000800U) != 0) return true; if ((flags & 0x100U) != 0) return false; return (flags & 0x0A5014B1U) == 0 || ((flags & 0x1000U) != 0 && Special(tile)); }
        private bool HeightCompatible(int a, int b)
        {
            int first = Heights[a], second = Heights[b]; int sa = (short)Structures[a], sb = (short)Structures[b];
            if ((Flags[b] & 0x10000000U) != 0 && sb > 0 && sb < StructureHeights.Length) second += StructureHeights[sb];
            if (Math.Abs(second - first) <= 16) return true;
            if (sb > 0 && sb < LowWalls.Length && LowWalls[sb] != 0) second -= 90;
            else if (sa > 0 && sa < LowWalls.Length && LowWalls[sa] != 0) first -= 90;
            return Math.Abs(second - first) <= 16;
        }
        private bool Corner(int player, int current, int corner)
        { if ((uint)corner >= Flags.Length) return false; return Wet(corner) ? Friendly(player, corner) && HeightCompatible(current, corner) : Adjacent(corner) && HeightCompatible(current, corner); }
        private bool Allows(int player, int x, int y, int a, int nx, int ny, int b, int d, bool ground, out bool wet, out bool structure)
        {
            wet = Wet(a) || Wet(b); structure = Structural(a) || Structural(b);
            if (GateMasks[player] != null && (GateMasks[player][a] & (1 << d)) == 0) return false;
            if ((Wet(a) && !Friendly(player, a)) || (Wet(b) && !Friendly(player, b)) || (ground && wet)) return false;
            int first = Rows[y * 3] + nx, second = Rows[ny * 3] + x;
            if (!ground && (d & 1) != 0 && ((uint)first >= Flags.Length || (uint)second >= Flags.Length || (Wet(first) && !Friendly(player, first)) || (Wet(second) && !Friendly(player, second)))) return false;
            bool ordinary = (NativeEdges[a] & (1 << d)) != 0;
            if (!wet) return ordinary && HeightCompatible(a, b);
            if (!HeightCompatible(a, b)) return false;
            if (ordinary) return true;
            if (!Adjacent(a) || !Adjacent(b)) return false;
            if ((d & 1) == 0) return true;
            if (!Wet(a) && Wet(b)) return (Wet(first) && Friendly(player, first)) || (Wet(second) && Friendly(player, second));
            return Corner(player, a, first) || Corner(player, a, second);
        }
    }
}
