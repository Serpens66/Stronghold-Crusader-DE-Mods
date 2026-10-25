using System;
using System.Runtime.InteropServices;
using SHCDESE.API;
using SHCDESE.Interop;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        private GateAttackFreezeCapture.Context FreezeUnitContext(int id)
        {
            var x = new GateAttackFreezeCapture.Context { Unit = id, Type = -1, StartX = -1, StartY = -1, TargetX = -1, TargetY = -1 };
            if (GateAttackFreezeCapture.Current == null) return x;
            try
            {
                if (APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit != null)
                {
                    x.Global = unit->r_GlobalId; x.Type = (int)unit->r_UnitChimp; x.Tribe = unit->r_TribeId;
                    x.Player = unit->r_ControllableForPlayerId; x.Command = (int)unit->r_AI_LastIssuedTribeCommand;
                    GetNativeMovementStart(unit, out x.StartX, out x.StartY);
                }
                x.Building = activeAttackCommand != null && IsBuildingAttackCommand(activeAttackCommand.Command) ? activeAttackCommand.TargetValue1 : 0;
            }
            catch (Exception) { GateAttackFreezeCapture.Fault(); }
            return x;
        }
        private GateAttackFreezeCapture.Context FreezeGroupContext(int tribe, int building)
        {
            GateAttackFreezeCapture.Context x = default;
            if (GateAttackFreezeCapture.Current == null) return x;
            try
            {
                if (tribe > 0 && GameTribeManagerAPI.Instance.TryGetTribeById(tribe, out GameTribe* group) && group != null)
                    x = FreezeUnitContext(group->r_LeaderUnitId);
            }
            catch (Exception) { GateAttackFreezeCapture.Fault(); }
            x.Tribe = tribe; x.Building = building; return x;
        }
        private long FreezeBuilder(IntPtr manager)
        {
            if (GateAttackFreezeCapture.Current == null) return 0;
            try
            {
                var x = new GateAttackFreezeCapture.Context { Type = -1, Unit = -1 };
                if (manager != IntPtr.Zero && manager == nativePathManager && nativeUnitManager != null)
                {
                    byte* data = (byte*)manager.ToPointer();
                    byte* path = *(byte**)(data + PathManagerOutputBufferOffset);
                    long offset = path - (nativeUnitManager + NativeUnitPathBufferOffset);
                    long id = offset / NativeUnitPathBufferStride;
                    if (offset > 0 && offset % NativeUnitPathBufferStride == 0 && id <= MaximumUnitCount) x = FreezeUnitContext((int)id);
                    x.StartX = *(int*)(data + 8); x.StartY = *(int*)(data + 12);
                    x.TargetX = *(int*)(data + 16); x.TargetY = *(int*)(data + 20);
                }
                return GateAttackFreezeCapture.Enter(GateAttackFreezeCapture.Builder, x);
            }
            catch (Exception) { GateAttackFreezeCapture.Fault(); return 0; }
        }
        private void ReadFreezeCounters(GateAttackFreezeCapture.Run run, bool before)
        {
            // Existing validated path manager, never a guessed module base. FBCB9319:
            // E1640 increments +78 before repair; E49D0 increments +74 (see UpdateToNewDLL).
            if (nativePathManager == IntPtr.Zero || disposed) return;
            int repair = Marshal.ReadInt32(nativePathManager, 0x78), rebuild = Marshal.ReadInt32(nativePathManager, 0x74);
            if (before) { run.RepairBefore = repair; run.RebuildBefore = rebuild; run.CountersBefore = true; }
            else { run.RepairAfter = repair; run.RebuildAfter = rebuild; run.CountersAfter = true; }
        }
    }
}
