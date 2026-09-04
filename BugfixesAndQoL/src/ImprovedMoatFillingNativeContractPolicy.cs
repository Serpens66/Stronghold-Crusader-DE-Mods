namespace BugfixesAndQoL
{
    internal static class ImprovedMoatFillingNativeContractPolicy
    {
        internal const int FindMoatWorkTargetRva = 0x69D60;
        internal const int ResolveMoatWorkTileRva = 0x6AF60;
        internal const int MovementPlannerRva = 0x196280;
        internal const int MovementPlannerLowFlagGateRva = 0x196464;
        internal const int MovementPlannerStructureFlagGateRva = 0x19648D;

        internal static bool RequiresPristineLiveBytes(int rva) =>
            rva == FindMoatWorkTargetRva || rva == ResolveMoatWorkTileRva;
    }
}
