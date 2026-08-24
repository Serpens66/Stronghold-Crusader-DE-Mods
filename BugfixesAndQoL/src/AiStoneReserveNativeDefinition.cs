// Audited native contract for the AI stone-building reserve fix.
namespace BugfixesAndQoL
{
    internal static class AiStoneReserveNativeDefinition
    {
        public const int SellerReservePatternRva = 0x3F14F;
        public const int SellerReserveHookOffset = 0x07;
        public const int SellerReserveHookRva = SellerReservePatternRva + SellerReserveHookOffset;
        public const int SellerReserveOverwriteLength = 20;
        public const int StoneTradeCategory = 3;

        // All resource-category branches converge at the hook. Its complete 20-byte decoded
        // span has no incoming branch target in its interior; the callback runs before it.
        public const string SellerReservePattern =
            "41 8B 82 B0 00 00 00 42 8D 14 18 45 85 E4 7E 34 " +
            "41 81 BE CC F0 12 00 F4 01 00 00 7D 27 83 F9 02";

        public const int AivSlotLayoutPatternRva = 0x5068A;
        public const string AivSlotLayoutPattern =
            "BF 01 00 00 00 48 8D 81 9C 6D 00 00 44 8B C7 48 8B F1 " +
            "0F 1F 40 00 83 38 00 74 1E FF C7 49 FF C0 48 05 98 6D 00 00 " +
            "49 83 F8 09 7C EA 33 C0 48 8B 74 24 38 48 83 C4 20 5F C3 " +
            "48 63 C7 48 89 5C 24 30 48 69 D8 98 6D 00 00 48 63 C2 " +
            "48 69 C8 3C 58 00 00 48 03 DE 48 8D 05 ?? ?? ?? ?? 89 53 04";

        public const int AivStepLayoutPatternRva = 0x517C2;
        public const string AivStepLayoutPattern =
            "4A 63 BC 11 BC 0E 13 00 48 69 C7 22 09 00 00 " +
            "48 89 7C 24 48 48 89 44 24 58 4C 8D 3C 02 4B 8D 1C 7F " +
            "4D 0F BF 44 9D 3A 66 44 89 84 24 C8 00 00 00 49 8B F0 " +
            "45 85 C9 74 05 83 FE 36 75 1F 41 0F B6 44 9D 38 A8 FB " +
            "74 15 3C 05";

        public const int AivHighestFramePatternRva = 0x55F64;
        public const string AivHighestFramePattern =
            "48 63 F2 48 8D 05 ?? ?? ?? ?? 4C 69 CE 3C 58 00 00 " +
            "48 8B E9 BB 00 00 00 00 49 63 04 01 4C 69 C8 98 6D 00 00 " +
            "B8 1F 85 EB 51 45 0F AF 44 09 24";
    }
}
