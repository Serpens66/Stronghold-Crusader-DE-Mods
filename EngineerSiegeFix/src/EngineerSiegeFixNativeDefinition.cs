namespace EngineerSiegeFix
{
    internal static class EngineerSiegeFixNativeDefinition
    {
        public const int SiegeTentTickRva = 0x158690;
        public const int AiCrewBookkeepingRva = 0x123EA0;
        public const int ClearSelectedUnitRva = 0x186C20;
        public const int RemoveUnitFromGroupsRva = 0x19A5D0;

        public const string SiegeTentTickPattern =
            "40 53 48 83 EC 30 48 63 05 ?? ?? ?? ?? 48 8D 1D ?? ?? ?? ?? " +
            "48 69 C8 90 04 00 00 C7 84 19 88 06 00 00 00 00 00 00 " +
            "8B 84 19 DC 09 00 00 FF C0 83 E0 03";

        public const string AiCrewBookkeepingPattern =
            "48 89 5C 24 08 48 89 74 24 10 48 89 7C 24 18 41 56 " +
            "48 83 EC 20 4C 63 CA 4C 8D 35 ?? ?? ?? ?? 49 63 F8 " +
            "41 8B C1 49 69 F1 90 04 00 00";

        public const string ClearSelectedUnitPattern =
            "48 63 C2 48 69 D0 90 04 00 00 66 83 BC 0A 8C 06 00 00 00 " +
            "74 0D 33 C0 66 89 84 0A 8C 06 00 00 FF 49 20 C3";

        public const string RemoveUnitFromGroupsPattern =
            "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 4D 63 D0 " +
            "48 8B D9 48 63 FA 41 83 FA FF 75 20";
    }
}
