using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace ExtraFeatures
{
    // Only the optional repair HUD uses this read-only view. See UpdateToNewDLL.md.
    internal sealed class BuildingRepairHudVisibilityPolicy
    {
        internal const int DamageTableRva = 0x2E6710;
        internal const int DamageTableByteLength = ((int)eStructs.STRUCT_GARDEN_LARGE + 1) * sizeof(int);
        private IntPtr damageTable;

        internal bool InitializeNative(IntPtr moduleBase, int imageLength, bool auditedHash)
        {
            IntPtr address = IntPtr.Zero;
            if (auditedHash && moduleBase != IntPtr.Zero &&
                imageLength >= DamageTableRva + DamageTableByteLength)
                address = IntPtr.Add(moduleBase, DamageTableRva);
            Volatile.Write(ref damageTable, address);
            return address != IntPtr.Zero;
        }

        internal bool ShouldShow(int buildingType, int panel)
        {
            if (buildingType <= (int)eStructs.STRUCT_NULL ||
                buildingType > (int)eStructs.STRUCT_GARDEN_LARGE)
                return false;
            // Trade screens can also be opened from the granary or armoury.
            switch ((Enums.InBuildingModes)panel)
            {
                case Enums.InBuildingModes.INSIDE_TRADEPOST_PRICES:
                case Enums.InBuildingModes.INSIDE_TRADEPOST_FOOD:
                case Enums.InBuildingModes.INSIDE_TRADEPOST_BULK:
                case Enums.InBuildingModes.INSIDE_TRADEPOST_ARMS:
                case Enums.InBuildingModes.INSIDE_TRADEPOST_DO_THE_TRADE:
                    return false;
            }
            if (panel == (int)Enums.InBuildingModes.INSIDE_TRADEPOST &&
                buildingType != (int)eStructs.STRUCT_TRADEPOST)
                return false;
            if (buildingType == (int)eStructs.STRUCT_TRADEPOST &&
                panel != (int)Enums.InBuildingModes.INSIDE_TRADEPOST)
                return false;
            IntPtr table = Volatile.Read(ref damageTable);
            // Read live so another mod's damage-table changes remain authoritative.
            return table != IntPtr.Zero && Marshal.ReadInt32(table, buildingType * sizeof(int)) != 0;
        }
    }
}
