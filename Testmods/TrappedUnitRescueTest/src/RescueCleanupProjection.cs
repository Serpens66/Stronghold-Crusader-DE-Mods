using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    // Only the predicate consumed by E3360 is projected. Cleanup can change many other flags.
    // The complete admitted cleanup writers preserve RealityEdge/MapBorder; branches may be
    // omitted/repeated in any order without changing these two bits.
    internal static class RescueCleanupProjection
    {
        private static readonly uint ExpansionMask = (uint)(TilePropertyFlag.RealityEdge | TilePropertyFlag.MapBorder);
        internal static uint ExpansionBits(uint before) => before & ExpansionMask;

        // 639C0/69470 operate on an eight-bit counter and then compare it as signed.
        // They do not inspect target mode, work type or tile flags before this early return.
        internal static bool MoatCanChangeGeometry(bool fill, byte progress)
        {
            int next = unchecked((sbyte)(byte)(progress + (fill ? -1 : 1)));
            return fill ? next <= 0 : next >= 4;
        }
    }
}
