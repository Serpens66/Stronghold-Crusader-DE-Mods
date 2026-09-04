// Feature: Audited native contracts for allowing Lords in Vanilla control groups.
namespace BugfixesAndQoL
{
    internal static class LordControlGroupNativeDefinition
    {
        public const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
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

        public const int ControlGroupStoragePatternRva = 0x186338;
        public const string ControlGroupStoragePattern =
            "48 8D 1D ? ? ? ? 48 8B F8 48 8B E9 48 8D 05 ? ? ? ? " +
            "BE 0A 00 00 00 45 33 F6";
        public const int ControlGroupStorageDisplacementOffset = 0x10;
        public const int ControlGroupStorageNextInstructionOffset = 0x14;
        public const int ControlGroupStorageRva = 0x36D78D0;
        public const int ControlGroupCount = 10;
        public const int ControlGroupCapacity = 10000;
        public const int ControlGroupRecordIntCount = 2;

        // UIT_DISBAND (0x1E) dispatch contract. The Lord maps to the no-op/default
        // class while a European Archer maps to the normal disband block.
        public const int DisbandDispatcherRva = 0x1219BA;
        public const string DisbandDispatcherInstructions =
            "0F BF 84 18 E6 06 00 00 83 C0 FB 83 F8 50 0F 87 F6 00 00 00 " +
            "4C 8D 05 2B E6 ED FF 48 98 41 0F B6 84 00 E8 21 12 00 " +
            "41 8B 8C 80 DC 21 12 00 49 03 C8 FF E1";
        public const int DisbandTypeTableRva = 0x1221E8;
        public const int DisbandTargetTableRva = 0x1221DC;
        public const int LordDisbandClassEntryRva =
            DisbandTypeTableRva + LordUnitType - UnitTypeTableMinimum;
        public const int EuropeanArcherDisbandClassEntryRva =
            DisbandTypeTableRva + EuropeanArcherUnitType - UnitTypeTableMinimum;
        public const byte EuropeanArcherDisbandClass = 0x00;
        public const byte LordDisbandClass = 0x02;
        public const int DisbandBranchRva = 0x1219ED;
        public const int DisbandCallRva = 0x121A0F;
        public const int DisbandFunctionRva = 0x186D10;
        public const int DisbandDefaultTargetRva = 0x121AC4;
        public const string DisbandBranchInstructions =
            "48 8B 44 24 40 48 8B CB 45 0F B6 F6 42 0F BF 44 20 5A " +
            "41 3B C1 B8 01 00 00 00 44 0F B6 C0 44 0F 44 F0 " +
            "E8 FC 52 06 00 84 C0 0F 84 A8 00 00 00 8B FE E9 A1 00 00 00";
    }
}
