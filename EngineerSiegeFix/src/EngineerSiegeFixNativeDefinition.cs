namespace EngineerSiegeFix
{
    internal static class EngineerSiegeFixNativeDefinition
    {
        public const int CatapultHandlerRva = 0x1520D0;
        public const int TrebuchetHandlerRva = 0x1535F0;
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
    }
}
