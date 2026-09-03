namespace HealerAttackCommandFixTest
{
    internal static class HealerAttackCommandFixNativeDefinition
    {
        public const int AttackUnitCommand = 4;
        public const int EngineerType = 30;
        public const int BedouinHealerType = 79;
        public const int FirstClassifierRva = 0x11EBF5;
        public const int SecondClassifierRva = 0x11EF39;
        public const int FirstTableRva = 0x121DF0;
        public const int SecondTableRva = 0x121E4C;
        public const int FirstDispatchTableRva = 0x121DDC;
        public const int SecondDispatchTableRva = 0x121E44;
        public const int FirstHealerEntryRva = 0x121E3A;
        public const int SecondHealerEntryRva = 0x121E96;
        public const int UnitTypeTableMinimum = 5;
        public const int FirstTableInstructionOffset = 0x24;
        public const int SecondTableInstructionOffset = 0x1F;
        public const int TableDisplacementOffset = 5;
        public const int FirstDispatchInstructionOffset = 0x2D;
        public const int SecondDispatchInstructionOffset = 0x28;
        public const int DispatchDisplacementOffset = 4;
        public const byte FirstVanillaHealerClass = 0;
        public const byte FirstNoOpClass = 4;
        public const byte SecondVanillaHealerClass = 0;
        public const byte SecondNoOpClass = 1;

        public const int FirstMeleeTargetRva = 0x11ECF8;
        public const int FirstNoOpTargetRva = 0x11ED82;
        public const int SecondMeleeTargetRva = 0x11EF6E;
        public const int SecondNoOpTargetRva = 0x11F11D;

        public const string FirstClassifierPattern =
            "0F BF 84 1F E6 06 00 00 83 C0 FB 89 B4 1F 48 07 00 00 83 F8 50 " +
            "0F 87 ?? ?? ?? ?? 4C 8D 05 ?? ?? ?? ?? 48 98 41 0F B6 84 00 ?? ?? ?? ?? " +
            "41 8B 8C 80 ?? ?? ?? ?? 49 03 C8 FF E1";

        public const string SecondClassifierPattern =
            "44 0F BF 84 1A E6 06 00 00 41 8D 40 FB 83 F8 50 0F 87 ?? ?? ?? ?? " +
            "4C 8D 1D ?? ?? ?? ?? 48 98 41 0F B6 84 03 ?? ?? ?? ?? " +
            "41 8B 8C 83 ?? ?? ?? ?? 49 03 CB FF E1";
    }
}
