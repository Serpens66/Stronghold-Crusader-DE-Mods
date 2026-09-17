using System;
using System.Runtime.InteropServices;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;

namespace OutpostTest
{
    internal sealed unsafe class OutpostNative
    {
        internal const int TribeManagerRva = 0x7CC6720, AiManagerRva = 0x404C950;
        private readonly ulong image;
        private readonly ManualLogSource log;
        private IntPtr flags;
        private HookTransaction hooks;
        private readonly HookHandle<X64InlineHook> gate = new HookHandle<X64InlineHook>();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int AllocateDelegate(IntPtr manager, int owner);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void FinishDelegate(IntPtr manager, int tribeId, uint global);
        private readonly AllocateDelegate allocate;
        private readonly FinishDelegate finish;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PlayerMoveDelegate(IntPtr manager,int tribe,int x,int y,int patrol,int flags);
        private readonly PlayerMoveDelegate playerMove;
        internal bool Enabled { get => flags != IntPtr.Zero && Marshal.ReadInt32(flags) != 0;
            set { if (flags != IntPtr.Zero) Marshal.WriteInt32(flags, value ? 1 : 0); } }
        internal long Bypasses => Marshal.ReadInt64(flags, 8);

        internal OutpostNative(ManualLogSource log, CrusaderLibraryLoadContext context)
        {
            this.log = log;
            image = unchecked((ulong)context.ModuleHandle.ToInt64());
            ValidateLayouts();
            // Hash-verified function entry; deliberately call through any existing MoatMove detour.
            // Do not pattern-check patched live entry bytes or create a competing hook.
            playerMove=Marshal.GetDelegateForFunctionPointer<PlayerMoveDelegate>((IntPtr)(image+0x196100));
            if ((ulong)GameTribeManagerAPI.Instance.GetTribeManager().Pointer != image + TribeManagerRva)
                throw new InvalidOperationException("Extender tribe manager does not match the audited native base.");
            Resolve(context, OutpostGate.Rva, "44 39 1D A9 A2 5B 03 0F 84 56 11 00 00 45 85 FF", "production gate");
            Resolve(context, OutpostGate.ExitRva, "48 83 C4 60 41 5F 41 5D 41 5C 5E 5B C3", "outpost epilogue");
            int allocRva = Resolve(context, 0x119D60, "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 20 BB 94 11 00 00", "outpost tribe allocator");
            int finishRva = Resolve(context, 0x2E2B0, "48 89 5C 24 10 48 89 6C 24 18 56 48 83 EC 20 48 63 DA 48 8D 2D 37 1D FD FF", "outpost tribe handoff");
            ValidateControlFlow(context.Memory.Slice(0xABB90, 0xACDE8 - 0xABB90).ToArray(), image);
            allocate = Marshal.GetDelegateForFunctionPointer<AllocateDelegate>((IntPtr)(image + (uint)allocRva));
            finish = Marshal.GetDelegateForFunctionPointer<FinishDelegate>((IntPtr)(image + (uint)finishRva));
            try
            {
                flags = Marshal.AllocHGlobal(16);
                Marshal.WriteInt64(flags, 0, 0); Marshal.WriteInt64(flags, 8, 0);
                using (var probe = new X64InlineHook(image + OutpostGate.Rva, OutpostGate.Length))
                    if (probe.DisplacedByteCount != OutpostGate.Length) throw new InvalidOperationException("RedBird probe span differs.");
                hooks = new HookTransaction(context.Region, SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = true });
                hooks.AddInline(gate, HookTarget.FromAddress(image + OutpostGate.Rva),
                    (a, original, back) => OutpostGate.Generate(a, original, back, unchecked((ulong)flags.ToInt64()), image + OutpostGate.ExitRva),
                    hookSize: OutpostGate.Length);
                var result = hooks.Commit();
                if (!result.IsCompleteSuccess || !gate.Success || gate.Hook.DisplacedByteCount != OutpostGate.Length)
                    throw new InvalidOperationException("Incomplete OutpostTest hook transaction.");
                Shared.DebugLogHelper.LogInfo(log, "OutpostTest gate installed inactive: RVA=0xABC78 length=16 return=0xABC88 exit=0xACDDB; native layouts validated.");
            }
            catch { RollbackUnpublishedInitialization(); throw; }
        }

        // Only for an unpublished initialization candidate, never mission cleanup.
        internal void RollbackUnpublishedInitialization()
        {
            Enabled = false;
            hooks?.Dispose(); hooks = null;
            if (flags != IntPtr.Zero) { Marshal.FreeHGlobal(flags); flags = IntPtr.Zero; }
        }
        private int Resolve(CrusaderLibraryLoadContext context, int rva, string pattern, string name)
        {
            var result = Shared.NativePatternResolver.ResolveUnique(context.Memory, pattern, rva, true, name, log);
            if (result.Rva != rva) throw new InvalidOperationException(name + ": fixed-layout contract is hash/RVA bound.");
            Shared.DebugLogHelper.LogInfo(log, $"OutpostTest native {name}: {result.Method}, RVA=0x{result.Rva:X}.");
            return result.Rva;
        }
        internal static void ValidateControlFlow(byte[] code, ulong image)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(code), image + 0xABB90);
            ulong start = image + OutpostGate.Rva, end = start + OutpostGate.Length;
            while (decoder.IP < image + 0xACDE8)
            {
                var i = decoder.Decode();
                if (i.IsInvalid) throw new InvalidOperationException("Invalid outpost instruction.");
                if ((i.FlowControl == FlowControl.ConditionalBranch || i.FlowControl == FlowControl.UnconditionalBranch || i.FlowControl == FlowControl.Call) &&
                    (i.NearBranchTarget > start && i.NearBranchTarget < end))
                    throw new InvalidOperationException("Incoming branch into gate interior.");
            }
        }
        internal int ReadInt(int rva) => *(int*)(image + (uint)rva);
        internal bool ProductionAllowed
        {
            get
            {
                if (ReadInt(0x3665F28) == 1) return false;
                int mode = ReadInt(0x8574B90);
                return mode == 0 || mode == 99 || (ReadInt(0x37EF974) == 0 && *(byte*)(image + 0x38722DC) == 0);
            }
        }
        internal bool HasCapacity(int owner, int addedThisTick)
        {
            int mode = ReadInt(0x8574B90);
            int count = checked(ReadInt(0x379E6D4 + owner * 0x583C) + ReadInt(0x379B30C + owner * 0x583C));
            int limit = ReadInt(ReadInt(0x8574BCC + owner * 4) == -1 ? 0x37EF950 : 0x37EF954);
            return OutpostSchedule.HasCapacity(mode, count, addedThisTick, limit);
        }
        internal void RunTo(int tribe,int x,int y) => playerMove((IntPtr)(image+0x67E8400),tribe,x,y,0,0x81);
        internal int Allocate(int owner) => allocate((IntPtr)(image + TribeManagerRva), owner);
        internal void Finish(int tribeId, uint global) => finish((IntPtr)(image + AiManagerRva), tribeId, global);
        internal void ValidateBuildingPointer(int id, GameBuilding* p)
        {
            if ((ulong)p != image + 0x64CCBB0 + 0x5C + (ulong)id * 0x32C)
                throw new InvalidOperationException("Building ID/pointer contract differs.");
        }
        internal void ValidateTribePointer(int id, GameTribe* p)
        {
            if ((ulong)p != image + TribeManagerRva + 0x2A + (ulong)id * 0x688)
                throw new InvalidOperationException("Tribe ID/pointer contract differs.");
        }
        internal void ValidateUnitPointer(int id, GameUnit* p)
        {
            if ((ulong)p != image + 0x67E8400 + 0x65C + (ulong)id * 0x490)
                throw new InvalidOperationException("Unit ID/pointer contract differs.");
        }
        internal static void SetRole(GameTribe* tribe) => *(short*)((byte*)tribe + 0x652) = 184;
        internal static void InitializeUnit(GameUnit* unit)
        {
            *(short*)((byte*)unit + 0x426) = 50;
            unit->N0000011E = 0;
        }
        internal static void ValidateLayouts()
        {
            if (Marshal.SizeOf<GameBuilding>() != 0x32C || Marshal.SizeOf<GameUnit>() != 0x490 || Marshal.SizeOf<GameTribe>() != 0x688)
                throw new InvalidOperationException("Native structure size changed.");
            Offset<GameBuilding>(nameof(GameBuilding.r_BuildingType), 0xD2);
            Offset<GameBuilding>(nameof(GameBuilding.r_PlayerIdOwner), 0xD6);
            Offset<GameBuilding>(nameof(GameBuilding.r_GlobalId), 0xD8);
            Offset<GameBuilding>(nameof(GameBuilding.r_TilePositionXEnd), 0xFE);
            Offset<GameBuilding>(nameof(GameBuilding.r_TilePositionYEnd), 0x100);
            Offset<GameUnit>(nameof(GameUnit.r_AliveState), 0x88);
            Offset<GameUnit>(nameof(GameUnit.r_GlobalId), 0x94);
            Offset<GameUnit>(nameof(GameUnit.N0000011E), 0xAC);
            Offset<GameUnit>(nameof(GameUnit.r_AITribeRole), 0x426);
            Offset<GameTribe>(nameof(GameTribe.r_GlobalId), 0xA);
            Offset<GameTribe>(nameof(GameTribe.r_UnitsInGroup), 0x32);
            Offset<GameTribe>(nameof(GameTribe.r_TribeStance), 0x60A);
        }
        private static void Offset<T>(string field, int expected)
        {
            if (Marshal.OffsetOf(typeof(T), field).ToInt32() != expected)
                throw new InvalidOperationException(typeof(T).Name + "." + field + " layout differs.");
        }
    }
}
