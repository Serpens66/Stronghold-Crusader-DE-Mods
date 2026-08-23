// Audited native contract for the AI stone-building reserve fix.
namespace BugfixesAndQoL
{
    internal static class AiStoneReserveNativeDefinition
    {
        public const int SellerReservePatternRva = 0x3F1A0;
        public const int SellerReserveHookOffset = 0x21;
        public const int SellerReserveHookRva = SellerReservePatternRva + SellerReserveHookOffset;
        public const int StoneTradeCategory = 3;

        // c_game_ai_sell_item_handler, normal-popularity threshold branch. The hook is placed
        // on the 41 03 D1 instruction (add edx,r9d), after the callback has refreshed R9D.
        public const string SellerReservePattern =
            "83 F9 02 74 17 83 F9 04 75 07 33 D2 45 33 DB EB 13 " +
            "8B C2 99 83 E2 03 03 D0 C1 FA 02 45 33 DB EB 03 " +
            "41 03 D1 33 C9";
    }
}
