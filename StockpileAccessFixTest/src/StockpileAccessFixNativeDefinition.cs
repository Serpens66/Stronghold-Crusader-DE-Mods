namespace StockpileAccessFixTest
{
    internal static class StockpileAccessFixNativeDefinition
    {
        internal const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

        internal const int RevalidateBuildingAccessRva = 0xC90E0;
        internal const string RevalidateBuildingAccessPattern =
            "44 89 44 24 18 55 41 57 48 83 EC 58 48 63 EA 4C 8B F9 85 D2 7F 0A " +
            "33 C0 48 83 C4 58 41 5F 5D C3";

        internal const int MoveHereRva = 0x196280;
        internal const int UnitHandlerTableRva = 0x321CB0;
        internal const ulong PreferredImageBase = 0x180000000;

        internal const int GameUnitSize = 0x490;
        internal const int GameBuildingSize = 0x32C;
        internal const int UnitStoredBuildingGlobalIdOffset = 0x9C;
        internal const int UnitStorageBuildingIdOffset = 0x332;
        internal const int BuildingEntryXOffset = 0xFE;
        internal const int BuildingEntryYOffset = 0x100;

        // 0xC90E0 addresses buildings from the manager base; the publicized span begins 0x388 bytes in.
        // Accounting for the one-based ID leaves this 0x5C bias between native working and struct offsets.
        internal const int BuildingManagerArrayBias = 0x5C;
        internal const int NativeAccessXWorkingOffset = 0x15A;
        internal const int NativeAccessYWorkingOffset = 0x15C;
        internal const uint AccessCandidateRejectedTileMask = 0x50501481;

        internal const int FletcherHandlerRva = 0x12D230;
        internal const int MillerHandlerRva = 0x1377C0;
        internal const int BakerHandlerRva = 0x138850;
        internal const int BrewerHandlerRva = 0x139950;
        internal const int PoleturnerHandlerRva = 0x13AAD0;
        internal const int BlacksmithHandlerRva = 0x13BDD0;
        internal const int ArmourerHandlerRva = 0x13CF30;
        internal const int InnkeeperHandlerRva = 0x1505D0;

        internal const string AuditedScriptExtenderVersion = "2.2.0";
        internal const string AuditedScriptExtenderCommit =
            "10d28f717d38166e5875c666f20fc5653ae44b0c";
    }
}
