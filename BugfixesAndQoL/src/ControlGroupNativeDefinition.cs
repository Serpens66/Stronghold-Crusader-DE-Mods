// Shared audited native contracts for Vanilla control-group storage and disband processing.
namespace BugfixesAndQoL
{
    internal static class ControlGroupNativeDefinition
    {
        public const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

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

        public const int DisbandDispatcherRva = 0x1219BA;
        public const string DisbandDispatcherInstructions =
            "0F BF 84 18 E6 06 00 00 83 C0 FB 83 F8 50 0F 87 F6 00 00 00 " +
            "4C 8D 05 2B E6 ED FF 48 98 41 0F B6 84 00 E8 21 12 00 " +
            "41 8B 8C 80 DC 21 12 00 49 03 C8 FF E1";
        public const int DisbandTargetTableRva = 0x1221DC;
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
