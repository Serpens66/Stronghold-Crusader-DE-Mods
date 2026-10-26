using System;

namespace BugfixesAndQoL.EditorRegions
{
    internal static class NativeContract
    {
        internal const string ReferenceHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int RebuildRva = 0xE49D0;
        // Force argument -> dirty +6C, countdown -> dirty gate -> generation +74
        // -> clear dirty. Only RIP-relative addresses are wildcards.
        internal const string RebuildPattern =
            "40 53 41 57 48 83 EC 58 48 8B D9 41 BF 01 00 00 00 85 D2 74 08 44 89 79 6C 33 C0 EB 06 " +
            "8B 05 ?? ?? ?? ?? FF C8 89 05 ?? ?? ?? ?? 85 C0 0F 8F 7B 04 00 00 C7 05 ?? ?? ?? ?? C8 00 00 00 " +
            "83 79 6C 00 0F 84 67 04 00 00 FF 41 74 48 89 6C 24 70 48 89 74 24 50 48 89 7C 24 48 C7 41 6C 00 00 00 00";

    }
}
