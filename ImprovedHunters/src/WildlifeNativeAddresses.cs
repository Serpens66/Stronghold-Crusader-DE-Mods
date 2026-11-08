using BepInEx.Logging;
using System;

namespace ImprovedHunters
{
    /// <summary>
    /// Vanilla's game-mode word, read from its own wildlife respawn function 0x123670, which only runs in single
    /// player (mode 0). The mod's own herd return runs in the other modes. Herd records, spawning, routes and flight
    /// come from APIShared's Herds service. Bound to the audited DLL.
    /// </summary>
    internal sealed unsafe class WildlifeNativeAddresses
    {
        private const int RespawnRva = 0x123670;
        private const string RespawnPattern =
            "41 57 48 83 EC 40 8B 05 ?? ?? ?? ?? 4C 8B F9 83 F8 01 0F 84 ?? ?? ?? ?? 83 F8 06 0F 84 ?? ?? ?? ?? " +
            "83 F8 02 75 0D 83 3D ?? ?? ?? ?? 00 0F 84 ?? ?? ?? ?? 83 3D ?? ?? ?? ?? 00 0F 85 ?? ?? ?? ??";
        private const int ModeCompareOffset = 0x33;
        private const int ModeCompareLength = 7;

        public WildlifeNativeAddresses(ManualLogSource log, ReadOnlySpan<byte> memory, ulong imageBase, bool referenceHashMatches)
        {
            if (!referenceHashMatches)
                throw new InvalidOperationException("Vanilla's wildlife data is bound to the audited native hash.");

            int respawn = Shared.NativePatternResolver.ResolveUnique(
                memory, RespawnPattern, RespawnRva, referenceHashMatches, "Vanilla wildlife respawn", log).Rva;

            // "cmp dword ptr [rip+disp], 0": 2 opcode bytes, the displacement, then the immediate.
            int mode = respawn + ModeCompareOffset;
            MultiplayerMode = imageBase + (ulong)(mode + ModeCompareLength + Shared.NativePatternResolver.ReadInt32(memory, mode + 2));
        }

        public ulong MultiplayerMode { get; }

        /// <summary>A wild deer or goat herd, the only herds the mod follows.</summary>
        public static bool IsWildDeerOrGoatHerd(in APIShared.Wildlife.HerdInfo herd, out bool isDeer)
        {
            isDeer = herd.Kind == APIShared.Wildlife.HerdKind.Deer;
            return (isDeer || herd.Kind == APIShared.Wildlife.HerdKind.Goat) && herd.OwnerPlayerId == 0;
        }
    }
}
