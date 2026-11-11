using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AreaDemolitionTest
{
    // No detours: delegates enter the existing live SE/Fixes/BFQoL call chains.
    internal sealed unsafe class NativeBindings
    {
        internal const string SupportedHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int TileManagerRva = 0x405EDB0, BuildingManagerRva = 0x64CCBB0, ProjectileManagerRva = 0x2F7A680;
        internal const int CursorRva = 0x3A11DC0, NeighboursRva = 0x8DDCA0;
        internal const int ActorRva = 0x88E3D70, DemolitionContextRva = 0x60AD528;
        internal const int WallRefundBranchRva = 0x13880;
        internal static readonly int[] QueryOffsets = { 0x204E6FC, 0x204E704, 0x204E720, 0x204E76C, 0x204E770, 0x204E774, 0x204E7F8, 0x204E694 };
        private static readonly object HashGate = new object();
        private static string fileHash;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void QueryDelegate(IntPtr manager, int actor, uint x, uint y);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void RefundDelegate(IntPtr manager, int building, int actor, int percentage);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void DeleteDelegate(IntPtr manager, int slot);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int FindDelegate(IntPtr manager, int owner, int x, int y, int size, int type, int excluded);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void WallDelegate(IntPtr manager, int actor, int tile);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate int MoatDelegate(IntPtr manager, int task);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] internal delegate void SoundDelegate(IntPtr manager, int x, int y, int sound);

        internal readonly long Module;
        internal readonly object EngineGate;
        internal readonly QueryDelegate Query;
        internal readonly RefundDelegate Refund;
        internal readonly DeleteDelegate Delete, RemovePitch, RemoveDecorations;
        internal readonly FindDelegate Find;
        internal readonly WallDelegate Wall;
        internal readonly MoatDelegate Moat;
        internal readonly SoundDelegate Sound;
        internal IntPtr Tiles => new IntPtr(Module + TileManagerRva);
        internal IntPtr Buildings => new IntPtr(Module + BuildingManagerRva);
        internal IntPtr Projectiles => new IntPtr(Module + ProjectileManagerRva);
        internal byte* TilePointer => (byte*)Tiles;
        internal byte* ModulePointer => (byte*)Module;

#if AREA_DEMOLITION_TESTS
        // Private-memory fixtures exercise production scratch handling without loading/calling the game.
        internal NativeBindings(IntPtr module, object engineGate, QueryDelegate query, MoatDelegate moat = null, DeleteDelegate decorations = null)
        { Module = module.ToInt64(); EngineGate = engineGate; Query = query; Moat = moat; RemoveDecorations = decorations; }
#endif

        internal bool Allows(DemolitionTarget target, int actor)
        {
            byte* cursor=ModulePointer+CursorRva, neighbours=ModulePointer+NeighboursRva, tiles=TilePointer;
            byte* savedCursor=stackalloc byte[0x80];byte* savedNeighbours=stackalloc byte[32];int* savedValues=stackalloc int[QueryOffsets.Length];
            for (int i=0;i<0x80;i++) savedCursor[i]=cursor[i];
            for (int i=0;i<32;i++) savedNeighbours[i]=neighbours[i];
            for (int i=0;i<QueryOffsets.Length;i++) savedValues[i]=*(int*)(tiles+QueryOffsets[i]);
            int oldActor=*(int*)(ModulePointer+ActorRva);
            try
            {
                for (int i=0;i<0x80;i++) cursor[i]=0;
                *(int*)(cursor+0xC)=target.X; *(int*)(cursor+0x10)=target.Y;
                *(int*)(cursor+0x14)=target.TileId; *(int*)(cursor+0x68)=target.TileId;
                *(int*)(cursor+0x6C)=target.X; *(int*)(cursor+0x70)=target.Y;
                if (target.Kind==TargetKind.Building) *(int*)(cursor+0x24)=target.Slot;
                if (target.Kind==TargetKind.Pitch) *(int*)(cursor+0x4C)=target.Slot;
                if (target.Kind==TargetKind.Wall) *(int*)(cursor+0x5C)=target.TileId;
                if (target.Kind==TargetKind.DugMoat) *(uint*)(cursor+0x44)=(uint)TilePropertyFlag.IsMoat;
                *(int*)(tiles+0x204E694)=0; // Canonical ground target, independent of the local flattened view.
                *(int*)(tiles+0x204E76C)=0; *(int*)(tiles+0x204E770)=0;
                *(int*)(tiles+0x204E720)=0; *(int*)(tiles+0x204E704)=0; *(int*)(tiles+0x204E7F8)=0;
                *(int*)(ModulePointer+ActorRva)=actor;
                Query(Tiles,actor,(uint)target.X,(uint)target.Y);
                if (*(int*)(tiles+0x204E6FC)!=0) return false;
                switch (target.Kind)
                {
                    case TargetKind.Building:return *(int*)(tiles+0x204E720)==target.Slot && *(int*)(tiles+0x204E76C)==0 && *(int*)(tiles+0x204E770)==0;
                    case TargetKind.Pitch:return *(int*)(tiles+0x204E720)==-target.Slot;
                    case TargetKind.Wall:return *(int*)(tiles+0x204E76C)!=0;
                    case TargetKind.DugMoat:return *(int*)(tiles+0x204E770)!=0;
                    default:return false;
                }
            }
            finally
            {
                for (int i=0;i<0x80;i++) cursor[i]=savedCursor[i];
                for (int i=0;i<32;i++) neighbours[i]=savedNeighbours[i];
                for (int i=0;i<QueryOffsets.Length;i++) *(int*)(tiles+QueryOffsets[i])=savedValues[i];
                *(int*)(ModulePointer+ActorRva)=oldActor;
            }
        }

        internal void RemoveDecorationTile(int tile) => RemoveDecorations(Projectiles,tile);

        internal void RemoveDugMoat(int taskIndex, int tile)
        {
            // Same ordered writes/call as 0x13818..0x13856. Task index was identity/owner checked by Apply.
            var task = (MoatWorkTask*)(TilePointer + 0x1F3EE30) + taskIndex;
            task->r_Progress = 0;
            Moat(Tiles,taskIndex);
            ((ushort*)(TilePointer+0xF05F40))[tile] &= 0xFBFF;
        }

        internal NativeBindings(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            if (context == null || context.ModuleHandle == IntPtr.Zero) throw new InvalidOperationException("No native library context.");
            Module = context.ModuleHandle.ToInt64();
            if (!string.Equals(GetFileHash(context.ModuleHandle), SupportedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unsupported CrusaderDE.dll: demolition layouts are hash bound.");
            if (context.Memory.Length <= ActorRva + sizeof(int)) throw new InvalidOperationException("Native image is truncated.");
            if (GameGlobalsManager.Instance.GameTileManagerVA != unchecked((ulong)(Module + TileManagerRva)) ||
                GameGlobalsManager.Instance.GameBuildingManagerVA != unchecked((ulong)(Module + BuildingManagerRva)) ||
                GameGlobalsManager.Instance.GameProjectilesManagerVA != unchecked((ulong)(Module + ProjectileManagerRva)))
                throw new InvalidOperationException("Extender manager bindings differ from the audited native bases.");
            var field = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(object) || (EngineGate = field.GetValue(null)) == null)
                throw new MissingFieldException("EngineInterface.threadLock");
            if (sizeof(GameBuilding) != 0x32C || sizeof(GameProjectile) != 0xE8 || sizeof(MoatWorkTask) != 0x10 || sizeof(GamePitchDescriptor) != 0x14 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_AliveState)).ToInt32() != 0xD0 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_GlobalId)).ToInt32() != 0xD8 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_OccupyTileGridSize)).ToInt32() != 0xF8 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_OccupiedTileIdsArrayBegin)).ToInt32() != 0x1C8 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_StoneQuarry_StockPileBuildingId)).ToInt32() != 0x192 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.N0000178A)).ToInt32() != 0x2A8 ||
                Marshal.OffsetOf(typeof(GameProjectile), nameof(GameProjectile.r_GlobalId)).ToInt32() != 0x44 ||
                Marshal.OffsetOf(typeof(GameProjectile), nameof(GameProjectile.r_PlayerSourceId)).ToInt32() != 0x40 ||
                Marshal.OffsetOf(typeof(GameProjectile), nameof(GameProjectile.r_CurrentTileId)).ToInt32() != 0x60 ||
                Marshal.OffsetOf(typeof(GameProjectile), nameof(GameProjectile.r_CurrentTileX)).ToInt32() != 0x58 ||
                Marshal.OffsetOf(typeof(GameProjectile), nameof(GameProjectile.r_CurrentTileY)).ToInt32() != 0x5A ||
                Marshal.OffsetOf(typeof(MoatWorkTask), nameof(MoatWorkTask.r_Progress)).ToInt32() != 0xD ||
                (int)eStructs.STRUCT_DRAWBRIDGE != 0x31 || (uint)TilePropertyFlag.IsMoat != 0x40000000 ||
                // Native9F410's closed type interval; these are ABI assertions, not shadow domain constants.
                (int)ProjectileType.Flag1 != 10 || (int)ProjectileType.Flag3 != 11 || (int)ProjectileType.Flag2 != 12 ||
                (int)ProjectileType.CrusaderFlag != 13 || (int)ProjectileType.Brazier != 14 || (int)ProjectileType.Heads != 15)
                throw new InvalidOperationException("Installed Extender native layout differs from the demolition audit.");

            Query = Bind<QueryDelegate>(context, log, 0x79B90, 1414, "D34F50E76C88D8165D41ACE03F0D08B2DB4EEFB40B92E5843B7FDD3F04F9AD8D", "44894C242044894424188954241048894C24085356415541574883EC684533FF");
            Refund = Bind<RefundDelegate>(context, log, 0xC4480, 1320, "C1E4DC936740B9D35117AC1539BC0A07D48EF212F61CE57778DC2A82AA8445D7", "4585C90F841E050000534883EC50458BD14963D8448B0D8D1A5A034C8BD94183");
            Delete = Bind<DeleteDelegate>(context, log, 0xC4290, 257, "713BF8FC70A1320F9EA7BB8B1CEBC82F0E52211C78A41D1DA3286EA5E24B0E56", "48895C240848896C2410488974241848897C242041564883EC2041BE03000000");
            Find = Bind<FindDelegate>(context, log, 0xB9330, 269, "D0E1D2F747CC546A18F3E845A016E06ADF7CD42C4AF3ECE5159CF95A4FA2B345", "48895C240848896C2410488974242044894424185741544155415641574883EC");
            RemovePitch = Bind<DeleteDelegate>(context, log, 0x62EB0, 176, "3BF3CDB006B64FC547950E938BD6340806006A039630E391B54DD10CC2E9C9D7", "40534883EC204C63C2BAFFDFFFFF498D801DC71900488D0480488D1C81486304");
            RemoveDecorations = Bind<DeleteDelegate>(context, log, 0x9F410, 121, "E3F245F3E19DA54AD3C4AF52E3729A217DFAAC8198B1B21B25734222DE9719E9", "4883EC084863C24C8BC9488D0DCF58EC044C8D1441B800100000664185027454");
            Wall = Bind<WallDelegate>(context, log, 0x63700, 348, "450AF687D27EDC4A8402F742278FF9BC8D50EF6F8E1AF61CD85203723E3AE1D4", "48895C240848896C2410488974241857415641574883EC604963F84C8D3DDEC8");
            Moat = Bind<MoatDelegate>(context, log, 0x69470, 237, "F9E2EAEB9B1F1C53D33D402BF419D4459699BB2A3B3EB572F24A7404F1342AE5", "48896C24104889742418574883EC204863EA488BF1488BFD48C1E7044803F9FE");
            Sound = Bind<SoundDelegate>(context, log, 0x2960, 40, "DDC5CE169520CD99FC322CF1F43680C30414CE8045F7D5D9D16156B06A963705", "4883EC38418BC1C744242000000000458BC8488D0DA75EA900448BC28BD0E85D");
        }

        internal int WallPolicy
        {
            get
            {
                byte* branch = ModulePointer + WallRefundBranchRva;
                if (branch[1] != 0x11) return -1;
                return branch[0] == 0x7F ? 0 : branch[0] == 0xEB ? 1 : -1;
            }
        }

        // Known image: whole-body validation is cheap and pins the audited data flow.
        // Unknown images remain disabled because prologues cannot prove scratch layouts.
        private T Bind<T>(CrusaderLibraryLoadContext context, ManualLogSource log, int rva, int length, string hash, string prefix) where T : class
        {
            byte[] bytes = Hex(prefix);
            int resolved = rva;
            string method = "reference-rva";
            if (!Equal(context.Memory, rva, bytes))
            {
                method = "pattern-fallback";
                resolved = FindUniqueText(context.Memory, bytes);
                if (resolved < 0) throw new InvalidOperationException("Demolition entry missing/ambiguous: " + typeof(T).Name);
            }
            if (resolved + length > context.Memory.Length || Hash(context.Memory.Slice(resolved, length).ToArray()) != hash)
                throw new InvalidOperationException("Demolition function body differs: RVA 0x" + rva.ToString("X"));
            log?.LogInfo(DateTime.Now.ToString("HH:mm:ss.fff") + " DEMOLITION_NATIVE " + typeof(T).Name + " method=" + method + " RVA=0x" + resolved.ToString("X"));
            return (T)(object)Marshal.GetDelegateForFunctionPointer(new IntPtr(Module + resolved), typeof(T));
        }

        internal static int FindUniqueText(ReadOnlySpan<byte> image, byte[] pattern)
        {
            if (image.Length < 0x40 || pattern == null || pattern.Length == 0) return -1;
            int pe = BitConverter.ToInt32(image.Slice(0x3C, 4).ToArray(), 0);
            if (pe < 0 || pe > image.Length - 24) return -1;
            int sections = BitConverter.ToUInt16(image.Slice(pe + 6, 2).ToArray(), 0);
            int first = pe + 24 + BitConverter.ToUInt16(image.Slice(pe + 20, 2).ToArray(), 0);
            if (first < 0 || first > image.Length || sections > (image.Length - first) / 40) return -1;
            int found = -1;
            for (int section = 0; section < sections; section++)
            {
                int header = first + section * 40;
                if (header + 40 > image.Length) return -1;
                uint characteristics = BitConverter.ToUInt32(image.Slice(header + 36, 4).ToArray(), 0);
                if ((characteristics & 0x20000000) == 0) continue;
                int start = BitConverter.ToInt32(image.Slice(header + 12, 4).ToArray(), 0);
                int size = BitConverter.ToInt32(image.Slice(header + 8, 4).ToArray(), 0);
                if (start < 0 || size < 0 || start > image.Length - size) return -1;
                for (int i = start; i <= start + size - pattern.Length; i++)
                    if (Equal(image, i, pattern)) { if (found >= 0) return -1; found = i; }
            }
            return found;
        }

        private static bool Equal(ReadOnlySpan<byte> image, int offset, byte[] expected)
        {
            if (offset < 0 || offset > image.Length - expected.Length) return false;
            for (int i = 0; i < expected.Length; i++) if (image[offset + i] != expected[i]) return false;
            return true;
        }
        private static byte[] Hex(string text)
        { var result = new byte[text.Length / 2]; for (int i = 0; i < result.Length; i++) result[i] = Convert.ToByte(text.Substring(i * 2, 2), 16); return result; }
        private static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static string GetFileHash(IntPtr module)
        {
            lock (HashGate)
            {
                if (fileHash != null) return fileHash;
                foreach (ProcessModule candidate in Process.GetCurrentProcess().Modules)
                    if (candidate.BaseAddress == module)
                        using (var stream = File.OpenRead(candidate.FileName))
                        using (var sha = SHA256.Create())
                            return fileHash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                throw new InvalidOperationException("Loaded native module file is unavailable.");
            }
        }
    }
}
