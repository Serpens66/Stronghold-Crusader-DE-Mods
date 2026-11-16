using SHCDESE.Interop;
using System.Runtime.InteropServices;

namespace CastlePlanner
{
    // Keep geometry uses +0x92, independently of the allocator's sprite variation at +0x102.
    // Contract and native writer/reader evidence: baseline knowledge/KEEP_DIRECTION.md.
    internal static class KeepDirectionReader
    {
        private static bool layoutVerified;

        internal static bool Configure(bool referenceNativeHashMatches)
        {
            layoutVerified = referenceNativeHashMatches &&
                Marshal.SizeOf(typeof(GameBuilding)) == 0x32C &&
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.N00012042_2)).ToInt32() == 0x92 &&
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_SpriteVariationIndex)).ToInt32() == 0x102;
            return layoutVerified;
        }

        internal static bool TryRead(in GameBuilding building, out int rotation)
        {
            rotation = 0;
            return layoutVerified &&
                KeepRotationPolicy.TryNormalize(building.N00012042_2, out rotation);
        }
    }
}
