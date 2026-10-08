using SHCDESE.Interop;
using System.Runtime.InteropServices;

namespace Shared
{
    // Vanilla deletion uses this native record slot as the compound-building key.
    // Its Script Extender field name changed in 2.10.4; the native offset did not.
    internal static unsafe class NativeBuildingCompoundGroup
    {
        internal const int BuildingSize = 0x32C;
        internal const int QuarryPileLinkOffset = 0x192;
        internal const int GroupOffset = 0x2A8;

        internal static bool HasExpectedLayout(out int size, out int pileLinkOffset)
        {
            size = Marshal.SizeOf(typeof(GameBuilding));
            pileLinkOffset = Marshal.OffsetOf(
                typeof(GameBuilding),
                nameof(GameBuilding.r_StoneQuarry_StockPileBuildingId)).ToInt32();
            return size == BuildingSize && pileLinkOffset == QuarryPileLinkOffset;
        }

        internal static uint Read(GameBuilding* building) =>
            *(uint*)((byte*)building + GroupOffset);

        internal static uint Read(ref GameBuilding building)
        {
            fixed (GameBuilding* pointer = &building)
                return Read(pointer);
        }

        internal static void Write(GameBuilding* building, uint value) =>
            *(uint*)((byte*)building + GroupOffset) = value;
    }
}
