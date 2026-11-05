// Feature: Audited native contracts for allowing Lords in Vanilla control groups.
namespace BugfixesAndQoL
{
    internal static class LordControlGroupNativeDefinition
    {
        public const string ReferenceSha256 = ControlGroupNativeDefinition.ReferenceSha256;
        public const int DisbandDispatcherRva = ControlGroupNativeDefinition.DisbandDispatcherRva;
        public const string DisbandDispatcherInstructions = ControlGroupNativeDefinition.DisbandDispatcherInstructions;
        public const int DisbandTargetTableRva = ControlGroupNativeDefinition.DisbandTargetTableRva;
        public const int DisbandBranchRva = ControlGroupNativeDefinition.DisbandBranchRva;
        public const int DisbandCallRva = ControlGroupNativeDefinition.DisbandCallRva;
        public const int DisbandFunctionRva = ControlGroupNativeDefinition.DisbandFunctionRva;
        public const int DisbandDefaultTargetRva = ControlGroupNativeDefinition.DisbandDefaultTargetRva;
        public const string DisbandBranchInstructions = ControlGroupNativeDefinition.DisbandBranchInstructions;

        public const int LordUnitType = 0x37;
        public const int UnitTypeTableMinimum = 5;
        public const int EuropeanArcherUnitType = 0x16;

        public const int AddClassifierPatternRva = 0xCAEF2;
        public const int AddLordBranchOffset = 0x2A;
        public const int AddLordBranchRva = AddClassifierPatternRva + AddLordBranchOffset;
        public const string AddClassifierPattern =
            "66 42 83 BC 02 E4 06 00 00 02 0F 85 ? ? ? ? " +
            "66 42 83 BC 02 F8 08 00 00 00 0F 85 ? ? ? ? " +
            "66 42 83 BC 02 E6 06 00 00 37 0F 84 F8 00 00 00";

        public const int ReplaceClassifierPatternRva = 0xD0FF7;
        public const int ReplaceLordBranchOffset = 0x29;
        public const int ReplaceLordBranchRva = ReplaceClassifierPatternRva + ReplaceLordBranchOffset;
        public const string ReplaceClassifierPattern =
            "66 41 83 BC 18 E4 06 00 00 02 0F 85 ? ? ? ? " +
            "66 45 39 AC 18 F8 08 00 00 0F 85 ? ? ? ? " +
            "66 41 83 BC 18 E6 06 00 00 37 0F 84 DA 00 00 00";

        public const string VanillaAddLordBranch = "0F 84 F8 00 00 00";
        public const string VanillaReplaceLordBranch = "0F 84 DA 00 00 00";
        public const string BypassLordBranch = "90 90 90 90 90 90";

        // UIT_DISBAND (0x1E) dispatch contract. The Lord maps to the no-op/default
        // class while a European Archer maps to the normal disband block.
        public const int DisbandTypeTableRva = 0x1221E8;
        public const int LordDisbandClassEntryRva =
            DisbandTypeTableRva + LordUnitType - UnitTypeTableMinimum;
        public const int EuropeanArcherDisbandClassEntryRva =
            DisbandTypeTableRva + EuropeanArcherUnitType - UnitTypeTableMinimum;
        public const byte EuropeanArcherDisbandClass = 0x00;
        public const byte LordDisbandClass = 0x02;
    }
}
