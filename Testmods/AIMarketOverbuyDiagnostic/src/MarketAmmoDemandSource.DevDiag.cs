#if SERP_DEVDIAG
using System;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AIMarketOverbuyDiagnostic
{
    // Source D5791 is an atomic ten-stone reload, not an AIC purchase batch.
    // This adapter is called by the permanent source/consumer owner only. It has
    // no lifecycle, polling, allocations, native writes or independent hooks.
    internal static unsafe class MarketAmmoDemandSource
    {
        private const int ReloadRva = 0xD5710, UnitFromEdxRva = 0xD572B;
        private const int SourceStackDelta = 0x38, NativeAmmoWordOffset = 0x35E;
        // Native machine contract: D5710 requires ten stone and adds twenty ammo.
        internal const int RequiredStone = 10;
        private static readonly int[] automaticReturns = { 0x153057, 0x153156, 0x1542C6, 0x1543C7, 0x18FD8A };
        private static ulong module;
        private static int ready;

        internal enum Result { Unknown, Ended, Live }

        internal struct Origin
        {
            internal int UnitId, Owner;
            internal uint GlobalId, CreationTick;
            internal bool SameIdentity(Origin other) => UnitId == other.UnitId && Owner == other.Owner &&
                GlobalId == other.GlobalId && CreationTick == other.CreationTick;

            internal Result TryDesired(int player, int liveStone, out int wanted)
                => TryDesired(player, liveStone, out wanted, out _);

            internal Result TryDesired(int player, int liveStone, out int wanted, out int ammo)
            {
                wanted = ammo = 0;
                if (Volatile.Read(ref ready) == 0 || player != Owner || UnitId <= 0 || liveStone < 0)
                    return Result.Unknown;
                if (!TryReadUnit(UnitId, out UnitState state)) return Result.Unknown;
                ammo = state.Ammo;
                return Desired(this, player, liveStone, state, out wanted);
            }
        }

        internal struct UnitState
        {
            internal AliveState Alive;
            internal int Owner;
            internal uint GlobalId, CreationTick;
            internal short Ammo;
            internal ushort DeathMarker;
        }

        internal static void Initialize(CrusaderLibraryLoadContext context)
        {
            if (!Shared.DebugLogHelper.IsCurrentNativeLibraryVersion())
                throw new InvalidOperationException("Automatic ammunition demand requires the audited native hash.");
            Initialize(context.Memory, unchecked((ulong)context.ModuleHandle.ToInt64()));
        }

        // Offline fixtures validate the same ABI bytes and installed SDK layout.
        internal static void Initialize(ReadOnlySpan<byte> image, ulong moduleBase)
        {
            Volatile.Write(ref ready, 0);
            if (moduleBase == 0 || sizeof(GameUnit) != 0x490 ||
                Offset(nameof(GameUnit.r_AliveState)) != 0x88 || Offset(nameof(GameUnit.r_ControllableForPlayerId)) != 0x92 ||
                Offset(nameof(GameUnit.r_GlobalId)) != 0x94 || Offset(nameof(GameUnit.r_CreationTickStamp)) != 0x98 ||
                Offset(nameof(GameUnit.r_IsKilledByProjectile)) != 0x29C ||
                Offset(nameof(GameUnit.r_StoneAmmoLeft)) != NativeAmmoWordOffset ||
                Offset(nameof(GameUnit.r_StoneAmmoStacksLeft)) != NativeAmmoWordOffset + 1)
                throw new InvalidOperationException("Automatic ammunition unit layout differs from the native contract.");
            Require(image, ReloadRva, new byte[] { 0x48,0x89,0x5C,0x24,0x08,0x57,0x48,0x83,0xEC,0x30 });
            Require(image, UnitFromEdxRva, new byte[] { 0x48,0x63,0xDA });
            foreach (int continuation in automaticReturns)
            {
                int call = continuation - 5;
                if (call < 0 || continuation > image.Length || image[call] != 0xE8)
                    throw new InvalidOperationException("Automatic ammunition caller is not the audited direct CALL.");
                int displacement = image[call+1] | image[call+2]<<8 | image[call+3]<<16 | image[call+4]<<24;
                if ((long)continuation + displacement != ReloadRva)
                    throw new InvalidOperationException("Automatic ammunition caller targets a different native function.");
            }
            module = moduleBase;
            Volatile.Write(ref ready, 1);
        }

        // The owner calls this only for the verified D5791 source frame before its
        // MOV. D5710's stack is push RDI + sub RSP,30h, so the real caller return
        // is at original RSP+38h. Manual18AE0 tail-jumps here and is not whitelisted.
        internal static bool TryCapture(IntPtr savedFrame, int player, int sourceStock, ulong moduleBase, out Origin origin)
        {
            origin = default;
            if (!TryReadFrame(savedFrame, player, sourceStock, moduleBase, out int unitId) ||
                !TryReadUnit(unitId, out UnitState state)) return false;
            return TryCreate(player, unitId, sourceStock, state, out origin);
        }

        internal static bool TryReadFrame(IntPtr frame, int player, int sourceStock, ulong moduleBase, out int unitId)
        {
            unitId = 0;
            if (Volatile.Read(ref ready) == 0 || frame == IntPtr.Zero || moduleBase != module ||
                player <= 0 || player > GamePlayerManagerAPI.MAX_PLAYERS || sourceStock < 0 || sourceStock >= RequiredStone)
                return false;
            ulong rawId = *(ulong*)((byte*)frame + MarketDemandWriterHooks.Rbx);
            if (rawId == 0 || rawId > int.MaxValue) return false;
            ulong caller = *(ulong*)((byte*)frame + MarketDemandWriterHooks.SnapshotBytes + SourceStackDelta);
            if (caller < moduleBase || !IsAutomaticReturn(caller - moduleBase)) return false;
            unitId = (int)rawId;
            return true;
        }

        internal static bool IsAutomaticReturn(ulong rva) => rva == 0x153057 || rva == 0x153156 ||
            rva == 0x1542C6 || rva == 0x1543C7 || rva == 0x18FD8A;

        internal static bool TryCreate(int player, int unitId, int sourceStock, UnitState state, out Origin origin)
        {
            origin = default;
            if (player <= 0 || player > GamePlayerManagerAPI.MAX_PLAYERS || unitId <= 0 || sourceStock < 0 ||
                sourceStock >= RequiredStone || state.Owner != player || state.Owner == 0 ||
                state.Alive != AliveState.IsAlive || state.DeathMarker != 0 || state.Ammo > 0) return false;
            origin = new Origin { UnitId = unitId, Owner = player, GlobalId = state.GlobalId, CreationTick = state.CreationTick };
            return true;
        }

        internal static Result Desired(Origin origin, int player, int liveStone, UnitState current, out int wanted)
        {
            wanted = 0;
            if (origin.UnitId <= 0 || player <= 0 || player > GamePlayerManagerAPI.MAX_PLAYERS ||
                origin.Owner != player || origin.Owner == 0 || liveStone < 0) return Result.Unknown;
            if (current.GlobalId != origin.GlobalId || current.CreationTick != origin.CreationTick ||
                current.Owner != origin.Owner || current.Alive != AliveState.IsAlive || current.DeathMarker != 0 || current.Ammo > 0)
                return Result.Ended;
            wanted = liveStone >= RequiredStone ? 0 : RequiredStone - liveStone;
            return Result.Live;
        }

        private static bool TryReadUnit(int unitId, out UnitState state)
        {
            state = default;
            bool found = UnitAccess.TryGetById(unitId, out GameUnit* unit, out UnitLookupFailure failure);
            if (!found || failure != UnitLookupFailure.None || unit == null)
                return false;
            state = ReadUnit(unit);
            return true;
        }

        internal static UnitState ReadUnit(GameUnit* unit) => new UnitState
        {
            Alive = unit->r_AliveState, Owner = unit->r_ControllableForPlayerId,
            GlobalId = unit->r_GlobalId, CreationTick = unit->r_CreationTickStamp,
            DeathMarker = unit->r_IsKilledByProjectile,
            // Native code reads signed16 across both SDK byte fields. Reading only
            // r_StoneAmmoLeft would misclassify e.g. word256 as empty.
            Ammo = *(short*)&unit->r_StoneAmmoLeft
        };

        private static int Offset(string field) => Marshal.OffsetOf(typeof(GameUnit), field).ToInt32();
        private static void Require(ReadOnlySpan<byte> image, int rva, byte[] bytes)
        {
            if (rva < 0 || rva > image.Length - bytes.Length) throw new InvalidOperationException("Ammunition ABI bytes exceed the image.");
            for (int i=0;i<bytes.Length;i++) if (image[rva+i] != bytes[i])
                throw new InvalidOperationException("Ammunition source stack/unit-register ABI differs.");
        }
    }
}
#endif
