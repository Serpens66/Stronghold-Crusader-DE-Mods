using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using APIShared;
using APIShared.NativeSites;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    // Experimental, hash-bound single-record relocation. No native code is patched here.
    internal sealed unsafe class NativeRelocation
    {
        internal const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int MaximumUnitId = 9999, TileCapacity = 320800;
        private readonly byte* image;
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void RelocateDelegate(IntPtr manager, int id, uint x, uint y, ushort height);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate long MoveDelegate(IntPtr manager, int id, int x, int y, int mode);
        private readonly RelocateDelegate relocate;
        private readonly MoveDelegate move;
        private NativeRelocation(IntPtr module)
        {
            image = (byte*)module;
            relocate = Marshal.GetDelegateForFunctionPointer<RelocateDelegate>(module + 0x19BA40);
            move = Marshal.GetDelegateForFunctionPointer<MoveDelegate>(module + 0x196280);
        }
        internal static NativeRelocation Create()
        {
            ProcessModule selected = null;
            using (var process = Process.GetCurrentProcess())
                foreach (ProcessModule module in process.Modules)
                    if (string.Equals(module.ModuleName, "CrusaderDE.dll", StringComparison.OrdinalIgnoreCase)) { selected = module; break; }
            if (selected == null) throw new InvalidOperationException("CrusaderDE.dll is not loaded.");
            using (var stream = File.OpenRead(selected.FileName))
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != NativeHash)
                    throw new InvalidOperationException("Unsupported native image; relocation disabled.");
            if (Marshal.SizeOf(typeof(GameUnit)) != 0x490 || GameUnitManager.UnitRecordByteSize != 0x490)
                throw new InvalidOperationException("Unsupported unit record size.");
            Offset(nameof(GameUnit.r_GlobalId), 0x94); Offset(nameof(GameUnit.r_CurrentTilePositionX), 0xC0);
            Offset(nameof(GameUnit.r_CurrentPositionTileId), 0xD0); Offset(nameof(GameUnit.r_PathPlanStateBitFlags), 0xF2);
            Offset(nameof(GameUnit.r_MovementSubstep), 0xF4); Offset(nameof(GameUnit.r_AssignedEngineer1), 0x310);
            Offset(nameof(GameUnit.r_AIState), 0x2BC); Offset(nameof(GameUnit.r_CurrentHealth), 0x3C4);
            // Validate pristine file bytes; live entry may legitimately be LordTeleportGuard's permanent hook.
            byte[] expected = { 0x48,0x89,0x6C,0x24,0x18,0x56,0x57,0x41,0x56,0x48,0x83,0xEC,0x20,0x49,0x63,0xF0 };
            byte[] actual = ReadRva(selected.FileName, 0x19BA40, expected.Length);
            for (int i = 0; i < expected.Length; i++) if (actual[i] != expected[i]) throw new InvalidOperationException("Relocation entry contract differs.");
            byte[] live = new byte[16]; Marshal.Copy(selected.BaseAddress + 0x19BA40,live,0,live.Length);
            bool pristine=true;for(int i=0;i<live.Length;i++)pristine &= live[i]==expected[i];
            if(!pristine)
            {
                if(!NativeSiteRegistry.TryGetWriter(0x19BA40,out NativeSiteRegistration owner) || owner.OwnerGuid!="BugfixesAndQoL_Serp" ||
                    owner.Name!="Lord.HomeTeleportGuard" || owner.Rva!=0x19BA40 || owner.Length!=16 || !owner.OriginalBytesVerified ||
                    live[0]!=0xFF || live[1]!=0x25 || BitConverter.ToInt32(live,2)!=0 || live[14]!=0x90 || live[15]!=0x90)
                    throw new InvalidOperationException("Unknown live relocation entry owner/patch.");
                IntPtr stub=new IntPtr(BitConverter.ToInt64(live,6));
                if(VirtualQuery(stub,out MemoryInformation info,(UIntPtr)Marshal.SizeOf(typeof(MemoryInformation)))==UIntPtr.Zero ||
                    info.State!=0x1000 || (info.Protect & 0xF0)==0 || (info.Protect & 0x100)!=0)
                    throw new InvalidOperationException("Lord guard stub is not executable committed memory.");
                byte[] prefix=new byte[8];Marshal.Copy(stub,prefix,0,prefix.Length);
                byte[] guardPrefix={0x9C,0x50,0x41,0x52,0x41,0x53,0x49,0xBA};
                for(int i=0;i<prefix.Length;i++)if(prefix[i]!=guardPrefix[i])throw new InvalidOperationException("Lord guard stub prefix differs.");
            }
            var callSite=NativeSiteRegistry.Register("TrappedUnitRescueTest_Serp","Experimental.RelocationCall",0x19BA40,16,NativeSiteMode.CallOnly);
            if(!callSite.Succeeded)throw new InvalidOperationException("Relocation call registration refused: "+callSite.Reason);
            return new NativeRelocation(selected.BaseAddress);
        }
        [StructLayout(LayoutKind.Sequential)] private struct MemoryInformation
        { internal IntPtr BaseAddress,AllocationBase; internal uint AllocationProtect; internal UIntPtr RegionSize; internal uint State,Protect,Type; }
        [DllImport("kernel32.dll")] private static extern UIntPtr VirtualQuery(IntPtr address,out MemoryInformation information,UIntPtr size);
        private static void Offset(string name, int expected)
        { if (Marshal.OffsetOf(typeof(GameUnit), name).ToInt32() != expected) throw new InvalidOperationException("Unit layout differs: " + name); }
        private static byte[] ReadRva(string path, int rva, int count)
        {
            using (var stream = File.OpenRead(path)) using (var reader = new BinaryReader(stream))
            {
                stream.Position = 0x3C; int pe = reader.ReadInt32(); stream.Position = pe + 6;
                int sections = reader.ReadUInt16(); stream.Position = pe + 20; int optional = reader.ReadUInt16();
                for (int i = 0; i < sections; i++)
                {
                    stream.Position = pe + 24 + optional + i * 40 + 8;
                    uint virtualSize = reader.ReadUInt32(), start = reader.ReadUInt32(), size = reader.ReadUInt32(), raw = reader.ReadUInt32();
                    if (rva >= start && rva + count <= start + Math.Min(virtualSize, size))
                    { stream.Position = raw + rva - start; return reader.ReadBytes(count); }
                }
                throw new InvalidOperationException("Native RVA has no file-backed section.");
            }
        }
        internal bool TryUnit(int id, out GameUnit* unit) => id >= 1 && id <= MaximumUnitId
            ? UnitAccess.TryGetById(id, out unit, out _) && unit != null && UnitAccess.IsReallyAlive(unit)
            : Missing(out unit);
        private static bool Missing(out GameUnit* unit) { unit = null; return false; }
        internal int Tile(int x, int y)
        {
            if ((uint)x >= 800 || (uint)y >= 800 || !GameTileManagerAPI.Instance.IsTileInsideMapBounds(x, y)) return -1;
            int tile = GameTileManagerAPI.Instance.GetTileId(x, y); return (uint)tile < TileCapacity ? tile : -1;
        }
        internal uint Flags(int tile) => ((uint*)(image + 0x48F71B0))[tile];
        internal byte Connections(int tile) => image[0x51890D0 + tile];
        internal int Building(int tile) => ((short*)(image + 0x4B6AA50))[tile];
        internal int Head(int tile) => ((short*)(image + 0x4C559B0))[tile];
        internal int Next(GameUnit* unit) => *(short*)((byte*)unit + (0x8FA - 0x65C));
        internal ushort Word(GameUnit* unit, int nativeOffset) => *(ushort*)((byte*)unit + nativeOffset - 0x65C);
        internal bool OrdinaryGround(int tile) => tile >= 0 && (Flags(tile) & 0x5A7015B1u) == 0 && Building(tile) == 0;
        internal bool HasOccupancyExactlyOnce(int tile, int id) => HasOccupancyCount(tile,id,1);
        internal bool HasOccupancyCount(int tile, int id,int expected)
        {
            int found = 0, cursor = Head(tile), steps = 0;
            var seen = new System.Collections.Generic.HashSet<int>();
            while (cursor != 0)
            {
                if (++steps > MaximumUnitId || !seen.Add(cursor) || !TryUnit(cursor, out GameUnit* unit)) return false;
                if (unit->r_CurrentPositionTileId != (uint)tile) return false;
                if (cursor == id) found++;
                cursor = Next(unit);
            }
            return found == expected;
        }
        internal bool Relocate(int id, int x, int y)
        {
            int tile = Tile(x, y); if (tile < 0) return false;
            relocate((IntPtr)GameGlobalsManager.Instance.GameUnitManagerVA, id, (uint)x, (uint)y, image[0x4DDD350 + tile]);
            return TryUnit(id, out GameUnit* unit) && unit->r_CurrentTilePositionX == x && unit->r_CurrentTilePositionY == y &&
                unit->r_CurrentPositionTileId == (uint)tile && HasOccupancyExactlyOnce(tile, id);
        }
        internal bool Reissue(int id, int x, int y)
        {
            long result = move((IntPtr)GameGlobalsManager.Instance.GameUnitManagerVA, id, x, y, 0);
            return result > 0 && TryUnit(id, out GameUnit* unit) &&
                (unit->r_PathPlanLength > 0 && unit->r_PathPlanStateBitFlags != 0 || unit->r_CurrentTilePositionX == x && unit->r_CurrentTilePositionY == y);
        }
    }
}
