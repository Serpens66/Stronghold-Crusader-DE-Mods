namespace WaterboyTargetReservationTest
{
    internal static class WaterboyNativeDefinition
    {
        internal const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const string AuditedScriptExtenderCommit =
            "70a4483fe606733219f0cd9fb1adbc0d08b926ea";
        internal const string AuditedScriptExtenderAssemblyVersion = "2.9.0.0";
        internal const string AuditedRedBirdAssemblyVersion = "1.5.0.0";
        internal const int FindNearestBurningBuildingRva = 0xB8F40;
        internal const int FindNearestBurningBuildingDisplacedLength = 10;
        internal const int NativeIndexedBuildingCompoundKeyOffset = 0x304;
        internal const int ManagedBuildingCompoundKeyOffset = 0x2A8;
        internal const int NativeIndexedBuildingFireTicksOffset = 0x31A;
        internal const int ManagedBuildingFireTicksOffset = 0x2BE;
        internal const int FiremanTargetSlotOffset = 0x39A;
        internal const int FiremanTargetGlobalIdOffset = 0x39C;

        internal const string FindNearestBurningBuildingPattern =
            "48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 30 48 63 C2 4C 8D 2D ?? ?? ?? ?? 48 69 E8 90 04 00 00 " +
            "33 F6 BF 01 00 00 00 4C 8B F9 41 BE E8 03 00 00";
    }
}
