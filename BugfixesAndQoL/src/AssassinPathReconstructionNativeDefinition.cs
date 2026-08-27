// Audited native contract for Assassin climb-route reconstruction.
namespace BugfixesAndQoL
{
    internal static class AssassinPathReconstructionNativeDefinition
    {
        public const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        public const int EndpointBuildingGuardsPatternRva = 0xE19D4;
        public const int CurrentTileRejectJumpOffset = 4;
        public const int NeighborTileRejectJumpOffset = 37;
        public const string EndpointBuildingGuardsPattern =
            "66 45 85 DB 0F 85 B1 00 00 00 49 8D 04 D6 41 8B 84 87 B0 ED 05 04 41 03 C0 48 63 C8 66 45 39 9C 4F 50 AA B6 04 0F 85 88 00 00 00";

        public static readonly byte[] OriginalCurrentTileRejectJump =
            { 0x0F, 0x85, 0xB1, 0x00, 0x00, 0x00 };
        public static readonly byte[] OriginalNeighborTileRejectJump =
            { 0x0F, 0x85, 0x88, 0x00, 0x00, 0x00 };
    }
}
