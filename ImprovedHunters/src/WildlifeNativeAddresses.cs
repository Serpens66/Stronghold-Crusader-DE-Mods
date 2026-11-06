using BepInEx.Logging;
using System;

namespace ImprovedHunters
{
    /// <summary>
    /// Vanilla's wildlife data, read from its own respawn function 0x123670 and the tick that calls it.
    /// That function only runs in single player; the mod uses the same data elsewhere. Bound to the audited DLL.
    /// </summary>
    internal sealed class WildlifeNativeAddresses
    {
        private const int RespawnRva = 0x123670;
        private const string RespawnPattern =
            "41 57 48 83 EC 40 8B 05 ?? ?? ?? ?? 4C 8B F9 83 F8 01 0F 84 ?? ?? ?? ?? 83 F8 06 0F 84 ?? ?? ?? ?? " +
            "83 F8 02 75 0D 83 3D ?? ?? ?? ?? 00 0F 84 ?? ?? ?? ?? 83 3D ?? ?? ?? ?? 00 0F 85 ?? ?? ?? ??";
        private const int ModeCompareOffset = 0x33;
        private const int ModeCompareLength = 7;
        // Displacements of "[r12 + index*4 + disp]"; R12 is the image base here.
        private const int DeerPointsXOffset = 0x9B;
        private const int DeerPointsYOffset = 0xA4;
        private const int GoatPointsXOffset = 0x18B;
        private const int GoatPointsYOffset = 0x194;
        private const int DeerSpawnCallOperand = 0x14D;

        private const int TickCallRva = 0xCE27E;
        private const string TickCallPattern = "48 8D 0D ?? ?? ?? ?? E8 C6 AD 04 00 48 8D 0D ?? ?? ?? ?? E8 DA 53 05 00";
        private const int HerdManagerLeaOperand = 15;

        public const int HerdStride = 0x688;
        public const int HerdLeaderOffset = 0x5A;
        public const int HerdFleeingOffset = 0x5F4;
        public const int SpawnPointsPerKind = 4;

        public WildlifeNativeAddresses(ManualLogSource log, ReadOnlySpan<byte> memory, ulong imageBase, bool referenceHashMatches)
        {
            if (!referenceHashMatches)
                throw new InvalidOperationException("Vanilla's wildlife data is bound to the audited native hash.");

            int respawn = Shared.NativePatternResolver.ResolveUnique(
                memory, RespawnPattern, RespawnRva, referenceHashMatches, "Vanilla wildlife respawn", log).Rva;
            int tick = Shared.NativePatternResolver.ResolveUnique(
                memory, TickCallPattern, TickCallRva, referenceHashMatches, "Vanilla wildlife tick", log).Rva;

            // "cmp dword ptr [rip+disp], 0": 2 opcode bytes, the displacement, then the immediate.
            int mode = respawn + ModeCompareOffset;
            MultiplayerMode = imageBase + (ulong)(mode + ModeCompareLength + Shared.NativePatternResolver.ReadInt32(memory, mode + 2));
            DeerPointsX = imageBase + (ulong)Shared.NativePatternResolver.ReadInt32(memory, respawn + DeerPointsXOffset);
            DeerPointsY = imageBase + (ulong)Shared.NativePatternResolver.ReadInt32(memory, respawn + DeerPointsYOffset);
            GoatPointsX = imageBase + (ulong)Shared.NativePatternResolver.ReadInt32(memory, respawn + GoatPointsXOffset);
            GoatPointsY = imageBase + (ulong)Shared.NativePatternResolver.ReadInt32(memory, respawn + GoatPointsYOffset);
            SpawnHerd = imageBase + (ulong)Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, respawn + DeerSpawnCallOperand, respawn + DeerSpawnCallOperand + 4);
            HerdManager = imageBase + (ulong)Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, tick + HerdManagerLeaOperand, tick + HerdManagerLeaOperand + 4);
        }

        public ulong MultiplayerMode { get; }
        public ulong DeerPointsX { get; }
        public ulong DeerPointsY { get; }
        public ulong GoatPointsX { get; }
        public ulong GoatPointsY { get; }
        public ulong SpawnHerd { get; }
        public ulong HerdManager { get; }
    }
}
