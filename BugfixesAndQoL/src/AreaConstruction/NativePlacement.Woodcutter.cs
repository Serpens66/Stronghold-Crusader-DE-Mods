using System.Runtime.InteropServices;

namespace BugfixesAndQoL.AreaConstruction
{
    internal sealed unsafe partial class NativePlacement
    {
        private const int WoodcutterPresent = 0x67E83E8;
        private const int WoodPlanks = 0x379B2A8;
        private const int PlayerStride = 0x583C;
        private const int WoodcutterWoodCost = 0x67E5BC8;

        // 0xB9710 checks an owner flag refreshed by the Vanilla building census.
        // The preview budget reserves one emergency hut while that flag is clear.
        private int FreeWoodcutterQuota(int actor) =>
            actor >= 1 && actor <= 8 && Read(WorldKind) != 6 &&
            Marshal.ReadByte(image, WoodcutterPresent + actor) == 0 &&
            Read(WoodPlanks + actor * PlayerStride) < Read(WoodcutterWoodCost) ? 1 : 0;
    }
}
