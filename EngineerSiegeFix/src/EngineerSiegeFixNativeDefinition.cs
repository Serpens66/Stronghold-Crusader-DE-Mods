namespace EngineerSiegeFix
{
    internal static class EngineerSiegeFixNativeDefinition
    {
        public const int CatapultHandlerRva = 0x1520D0;
        public const int TrebuchetHandlerRva = 0x1535F0;
        public const int CatapultStateSixRva = 0x1524FA;
        public const int TrebuchetStateSixRva = 0x153A78;
        public const int SiegeTentTickRva = 0x158690;
        public const int SiegeTentCompletionTailRva = 0x158762;
        public const int UnitConversionRva = 0x195D10;
        public const int UnitDispatcherRva = 0x182B00;
        public const int UnitDispatchTypeLoadRva = 0x184103;
        public const int UnitDispatchCallRva = 0x18410C;
        public const int UnitHandlerTableRva = 0x321CB0;

        // The complete saved-register prefix plus the distinct stack allocation
        // makes each handler signature unique in the canonical executable.
        public const string CatapultHandlerPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 " +
            "41 54 41 55 41 56 41 57 48 81 EC D0 00 00 00";

        public const string TrebuchetHandlerPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 " +
            "41 54 41 55 41 56 41 57 48 81 EC F0 00 00 00";

        public const string CatapultStateSixPattern =
            "8B 84 2B DC 09 00 00 33 05 ?? ?? ?? ?? 83 F0 F8 A8 0F " +
            "0F 85 ?? ?? ?? ?? 66 41 83 3F 06";

        public const string TrebuchetStateSixPattern =
            "42 8B 84 1B DC 09 00 00 33 05 ?? ?? ?? ?? 83 F0 F8 A8 0F " +
            "0F 85 ?? ?? ?? ?? 66 41 83 3C 24 06";

        public const string SiegeTentTickPattern =
            "40 53 48 83 EC 30 48 63 05 ?? ?? ?? ?? 48 8D 1D ?? ?? ?? ?? " +
            "48 69 C8 90 04 00 00";

        public const string SiegeTentCompletionTailPattern =
            "C7 84 19 14 0A 00 00 00 00 00 00 48 83 C4 30 5B C3";

        public const string UnitConversionPattern =
            "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 20 B8 02 00 00 00 " +
            "48 63 FA 33 F6 4C 8B C9 48 69 DF 90 04 00 00";
    }
}
