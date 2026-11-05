namespace BugfixesAndQoL
{
    internal sealed class DetourContractSnapshot
    {
        internal bool IsInstalled { get; set; }
        internal ulong TargetAddress { get; set; }
        internal string Scheme { get; set; }
        internal int DisplacedByteCount { get; set; }
        internal System.IntPtr TrampolineAddress { get; set; }
        internal int TrampolineSize { get; set; }
        internal System.IntPtr HookEntryPointAddress { get; set; }
        internal System.IntPtr OriginalEntryPointAddress { get; set; }
        internal System.IntPtr PointerSlot { get; set; }
        internal int ChainDepth { get; set; }
        internal byte[] EntryPatch { get; set; }
        internal System.IntPtr ResolvedPointerSlot { get; set; }
        internal System.IntPtr PointerSlotTarget { get; set; }
    }

    internal static class WaterboyNativeDefinition
    {
        internal const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
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

        internal static bool TryValidateDetourContract(
            DetourContractSnapshot snapshot,
            ulong expectedTargetAddress,
            out string failure)
        {
            if (snapshot == null) failure = "NativeDetour instance is missing.";
            else if (!snapshot.IsInstalled) failure = "NativeDetour is not installed.";
            else if (snapshot.TargetAddress != expectedTargetAddress) failure = "Target address changed.";
            else if (snapshot.Scheme != "Indirect") failure = "Detour scheme is not Indirect.";
            else if (snapshot.DisplacedByteCount != FindNearestBurningBuildingDisplacedLength) failure = "Displaced byte count is not 10.";
            else if (snapshot.TrampolineAddress == System.IntPtr.Zero || snapshot.TrampolineSize <= 0) failure = "Trampoline is invalid.";
            else if (snapshot.HookEntryPointAddress == System.IntPtr.Zero) failure = "Hook entry is invalid.";
            else if (snapshot.OriginalEntryPointAddress == System.IntPtr.Zero) failure = "Original entry is invalid.";
            else if (snapshot.PointerSlot == System.IntPtr.Zero) failure = "Pointer slot is invalid.";
            else if (snapshot.ChainDepth != 1) failure = "Detour chain depth is not one.";
            else if (snapshot.EntryPatch == null || snapshot.EntryPatch.Length != FindNearestBurningBuildingDisplacedLength) failure = "Entry patch length is invalid.";
            else if (snapshot.EntryPatch[0] != 0xFF || snapshot.EntryPatch[1] != 0x25 ||
                snapshot.EntryPatch[6] != 0x90 || snapshot.EntryPatch[7] != 0x90 ||
                snapshot.EntryPatch[8] != 0x90 || snapshot.EntryPatch[9] != 0x90) failure = "Entry patch is not FF 25 plus four NOPs.";
            else if (snapshot.ResolvedPointerSlot != snapshot.PointerSlot) failure = "Entry displacement does not resolve to the pointer slot.";
            else if (snapshot.PointerSlotTarget != snapshot.HookEntryPointAddress) failure = "Pointer slot does not contain the hook entry.";
            else
            {
                failure = null;
                return true;
            }
            return false;
        }
    }
}
