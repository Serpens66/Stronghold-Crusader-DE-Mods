// Feature: Audited native contracts for allowing Lords in Vanilla control groups.
namespace BugfixesAndQoL
{
    internal static class LordControlGroupNativeDefinition
    {
        // Compatibility aliases keep the Lord-specific validation concise while the
        // authoritative storage and disband contracts live in the shared definition.
        public const string ReferenceSha256 = ControlGroupNativeDefinition.ReferenceSha256;
        public const int ControlGroupStoragePatternRva = ControlGroupNativeDefinition.ControlGroupStoragePatternRva;
        public const string ControlGroupStoragePattern = ControlGroupNativeDefinition.ControlGroupStoragePattern;
        public const int ControlGroupStorageDisplacementOffset = ControlGroupNativeDefinition.ControlGroupStorageDisplacementOffset;
        public const int ControlGroupStorageNextInstructionOffset = ControlGroupNativeDefinition.ControlGroupStorageNextInstructionOffset;
        public const int ControlGroupStorageRva = ControlGroupNativeDefinition.ControlGroupStorageRva;
        public const int ControlGroupCount = ControlGroupNativeDefinition.ControlGroupCount;
        public const int ControlGroupCapacity = ControlGroupNativeDefinition.ControlGroupCapacity;
        public const int ControlGroupRecordIntCount = ControlGroupNativeDefinition.ControlGroupRecordIntCount;
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

        public const int SummaryClassifierPatternRva = 0x18645E;
        public const string SummaryClassifierPattern =
            "0F BF 84 29 E6 06 00 00 83 C0 FB 83 F8 50 0F 87 35 01 00 00 " +
            "48 98 0F B6 84 07 38 67 18 00 8B 8C 87 AC 66 18 00";
        public const int SummaryTypeTableDisplacementOffset = 0x1A;
        public const int SummaryDispatchTableDisplacementOffset = 0x21;
        public const int SummaryTypeTableRva = 0x186738;
        public const int SummaryDispatchTableRva = 0x1866AC;
        public const int LordSummaryEntryRva =
            SummaryTypeTableRva + LordUnitType - UnitTypeTableMinimum;
        public const int EuropeanArcherSummaryEntryRva =
            SummaryTypeTableRva + EuropeanArcherUnitType - UnitTypeTableMinimum;
        public const byte VanillaUnmappedSummaryClass = 0x22;
        public const byte EuropeanArcherSummaryClass = 0x01;
        public const int EuropeanArcherSummaryTargetRva = 0x186488;
        public const int UnmappedSummaryTargetRva = 0x1865A7;

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
