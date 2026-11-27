using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using APIShared;
using APIShared.Units;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.TrappedUnitRescue
{
    // Rescue-specific layout readers remain here; the public API owns relocation.
    // This consumer publishes no relocation hook and holds no private owner/stub contract.
    internal sealed unsafe class NativeRelocation
    {
        internal const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int MaximumUnitId = 9999, TileCapacity = 320800;
        private readonly byte* image;
        private readonly string ownerGuid;
        // Instances can only be created after the canonical file/layout/entry audit.
        internal IntPtr ImageBase => (IntPtr)image;
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate long MoveDelegate(IntPtr manager, int id, int x, int y, int mode);
        private readonly MoveDelegate move;
        private NativeRelocation(IntPtr module,string ownerGuid)
        {
            image = (byte*)module;
            this.ownerGuid=ownerGuid;
            move = Marshal.GetDelegateForFunctionPointer<MoveDelegate>(module + 0x196280);
        }
        internal static NativeRelocation Create(string ownerGuid)
        {
            if(string.IsNullOrWhiteSpace(ownerGuid))throw new ArgumentException("A stable caller GUID is required.",nameof(ownerGuid));
            if(!UnitMoveEvents.IsAvailable)throw new InvalidOperationException("Verified native move observations are unavailable.");
            if(!UnitRelocationService.EnsureAvailable(ownerGuid,out string relocationReason))
                throw new InvalidOperationException("Shared relocation unavailable: "+relocationReason);
            ProcessModule selected = null;
            using (var process = Process.GetCurrentProcess())
                foreach (ProcessModule module in process.Modules)
                    if (string.Equals(module.ModuleName, "CrusaderDE.dll", StringComparison.OrdinalIgnoreCase)) { selected = module; break; }
            if (selected == null) throw new InvalidOperationException("CrusaderDE.dll is not loaded.");
            if (Shared.DebugLogHelper.GetFileSha256(selected.FileName) != NativeHash)
                throw new InvalidOperationException("Unsupported native image; relocation disabled.");
            if (Marshal.SizeOf(typeof(GameUnit)) != 0x490 || GameUnitManager.UnitRecordByteSize != 0x490)
                throw new InvalidOperationException("Unsupported unit record size.");
            Offset(nameof(GameUnit.r_GlobalId), 0x94); Offset(nameof(GameUnit.r_CurrentTilePositionX), 0xC0);
            Offset(nameof(GameUnit.r_AliveState),0x88);Offset(nameof(GameUnit.r_UnitChimp),0x8A);
            Offset(nameof(GameUnit.r_ControllableForPlayerId),0x92);Offset(nameof(GameUnit.r_IsKilledByProjectile),0x29C);
            Offset(nameof(GameUnit.r_CurrentPositionTileId), 0xD0); Offset(nameof(GameUnit.r_PathPlanStateBitFlags), 0xF2);
            Offset(nameof(GameUnit.r_NextTilePositionX2),0xDC);Offset(nameof(GameUnit.r_NextTilePositionY2),0xDE);
            Offset(nameof(GameUnit.r_NextPositionTileId2),0xE0);
            Offset(nameof(GameUnit.r_TargetTilePositionX2),0xE8);Offset(nameof(GameUnit.r_TargetTilePositionY2),0xEA);
            Offset(nameof(GameUnit.r_PathPlanLength),0xF8);
            Offset(nameof(GameUnit.r_MovementSubstep), 0xF4); Offset(nameof(GameUnit.r_AssignedEngineer1), 0x310);
            Offset(nameof(GameUnit.r_AIState), 0x2BC); Offset(nameof(GameUnit.r_CurrentHealth), 0x3C4);
            Offset(nameof(GameUnit.N00000573),0x90);Offset(nameof(GameUnit.N000000DB),0x442);
            return new NativeRelocation(selected.BaseAddress,ownerGuid);
        }
        private static void Offset(string name, int expected)
        { if (Marshal.OffsetOf(typeof(GameUnit), name).ToInt32() != expected) throw new InvalidOperationException("Unit layout differs: " + name); }
        internal bool RelocationAvailable => UnitRelocationService.IsAvailable && UnitMoveEvents.IsAvailable;
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
        internal ushort JobWord(GameUnit* unit,int nativeOffset)
        {
            if(unit==null || nativeOffset<0x65C || nativeOffset>0xAEA)throw new ArgumentOutOfRangeException(nameof(nativeOffset));
            return Word(unit,nativeOffset);
        }
        internal uint JobDword(GameUnit* unit,int nativeOffset)
        {
            if(unit==null || nativeOffset<0x65C || nativeOffset>0xAE8)throw new ArgumentOutOfRangeException(nameof(nativeOffset));
            return *(uint*)((byte*)unit+nativeOffset-0x65C);
        }
        internal bool TryTribeGlobal(int tribeId,out uint globalId)
        {
            globalId=0;
            if(tribeId==0)return true;
            if(!GameTribeManagerAPI.Instance.TryGetTribeById(tribeId,out var tribe) || tribe==null)return false;
            globalId=tribe->r_GlobalId;return true;
        }
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
        // 19BA40 normalizes Current/Next XY and clears the midpoint marker. The
        // packed Next cache at native73C is inactive until a new step publishes it.
        internal static bool IsNormalizedPose(GameUnit* unit,int currentTile,uint midpointMarker) =>
            unit!=null && currentTile>=0 && unit->r_MovementSubstep==8 &&
            unit->r_CurrentPositionTileId==(uint)currentTile &&
            unit->r_CurrentTilePositionX==unit->r_NextTilePositionX2 &&
            unit->r_CurrentTilePositionY==unit->r_NextTilePositionY2 &&
            midpointMarker==0;

        internal bool Relocate(int id, int x, int y)
        {
            if(!RelocationAvailable || !TryUnit(id,out GameUnit* before) ||
                !APIShared.WorldMutations.WorldMutationEvents.TryGetCurrentWorld(out long session,out long generation))return false;
            int tile=Tile(x,y);if(tile<0)return false;
            var request=new UnitRelocationRequest(session,generation,id,before->r_GlobalId,
                before->r_UnitChimp,before->r_ControllableForPlayerId,x,y);
            if(!UnitRelocationService.TryRelocate(ownerGuid,request,out UnitRelocationResult result,out _) ||
                result.Status!=UnitRelocationStatus.VerifiedRelocated)return false;
            return TryUnit(id,out GameUnit* unit) && unit->r_CurrentTilePositionX==x && unit->r_CurrentTilePositionY==y &&
                unit->r_CurrentPositionTileId==(uint)tile && HasOccupancyExactlyOnce(tile,id);
        }
        internal bool Reissue(int id, int x, int y,Func<bool> authorityCurrent=null)
            =>TryReissue(id,x,y,authorityCurrent,out _);
        internal bool TryReissue(int id,int x,int y,Func<bool> authorityCurrent,out UnitMoveRunResult result)
        {
            result=null;
            if(!TryUnit(id,out var before))return false;
            uint global=before->r_GlobalId;var type=before->r_UnitChimp;
            int owner=before->r_ControllableForPlayerId,tribe=before->r_TribeId;
            if(!TryTribeGlobal(tribe,out uint tribeGlobal) || tribe!=0 && JobDword(before,0x93C)!=tribeGlobal)return false;
            bool IdentityCurrent()=>TryUnit(id,out var live) && live->r_GlobalId==global && live->r_UnitChimp==type &&
                live->r_ControllableForPlayerId==owner && live->r_TribeId==tribe && TryTribeGlobal(tribe,out uint currentTribeGlobal) && currentTribeGlobal==tribeGlobal &&
                (tribe==0 || JobDword(live,0x93C)==tribeGlobal) && (authorityCurrent==null || authorityCurrent());
            // R3 Post can overwrite or throw after the original. Its managed return is
            // not evidence of native refusal or publication; use the sole shared owner.
            if(!UnitMoveEvents.TryRun(ownerGuid,id,x,y,0,IdentityCurrent,
                ()=>move((IntPtr)GameGlobalsManager.Instance.GameUnitManagerVA,id,x,y,0),out result,out _))return false;
            return IdentityCurrent() && TryUnit(id,out var unit) && unit->r_TargetTilePositionX2==x && unit->r_TargetTilePositionY2==y &&
                ((unit->r_PathPlanLength>0 && (unit->r_PathPlanStateBitFlags==2 || unit->r_PathPlanStateBitFlags==4)) ||
                (unit->r_PathPlanStateBitFlags==0 && unit->r_CurrentTilePositionX==x && unit->r_CurrentTilePositionY==y));
        }
    }
}
