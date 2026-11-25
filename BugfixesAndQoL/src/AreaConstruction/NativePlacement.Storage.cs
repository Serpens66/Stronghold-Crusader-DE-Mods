using System;
using System.Runtime.InteropServices;
using SHCDESE.API;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.AreaConstruction
{
    internal sealed unsafe partial class NativePlacement
    {
        private const int GranaryPresent = 0x67E83F1;
        private const int GranaryWoodCost = 0x67E5D08;

        private int FreeGranaryQuota(int actor) =>
            Marshal.ReadByte(image, GranaryPresent + actor) == 0 &&
            Marshal.ReadByte(image, WoodcutterPresent + actor) == 0 &&
            Read(WoodPlanks + actor * PlayerStride) < Read(GranaryWoodCost) ? 1 : 0;

        private int StorageAllowance(eMappers tool, int actor)
        {
            var manager = GameBuildingManagerAPI.Instance;
            var managerPointer = manager.GetBuildingManager();
            var slots = manager.GetBuildingsAsSpan();
            if (managerPointer.IsNull || slots.Length == 0)
                return 0;

            int count = 0;
            var structure = tool.ConvertToEStructs();
            // The span begins at game slot one. B8140 iterates up to the
            // allocation watermark and counts live records with empty stacks.
            int allocated = checked((int)managerPointer.Pointer->BuildingsAllocated);
            for (int index = 0; index < Math.Min(slots.Length, allocated - 1); index++)
            {
                ref readonly var slot = ref slots[index];
                if (slot.r_AliveState == AliveState.IsAlive && slot.r_PlayerIdOwner == actor &&
                    slot.r_BuildingType == structure && unchecked((int)slot.r_CurrentGoodStackAmount) < 1)
                    count++;
            }
            bool stockpile = tool == eMappers.MAPPER_STORES;
            int recordLimit = storageLimitPatch?.RecordLimit(stockpile) ??
                StorageEmptyLimitPolicy.RecordLimit(StorageEmptyLimitPolicy.Vanilla, stockpile);
            return recordLimit - count;
        }
    }
}
