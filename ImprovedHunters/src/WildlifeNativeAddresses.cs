using BepInEx.Logging;
using System;

namespace ImprovedHunters
{
    /// <summary>
    /// Vanilla's wildlife data, read from its own respawn function 0x123670 and the tick that calls it.
    /// That function only runs in single player; the mod uses the same data elsewhere. Bound to the audited DLL.
    /// </summary>
    internal sealed unsafe class WildlifeNativeAddresses
    {
        private const int RespawnRva = 0x123670;
        private const string RespawnPattern =
            "41 57 48 83 EC 40 8B 05 ?? ?? ?? ?? 4C 8B F9 83 F8 01 0F 84 ?? ?? ?? ?? 83 F8 06 0F 84 ?? ?? ?? ?? " +
            "83 F8 02 75 0D 83 3D ?? ?? ?? ?? 00 0F 84 ?? ?? ?? ?? 83 3D ?? ?? ?? ?? 00 0F 85 ?? ?? ?? ??";
        private const int ModeCompareOffset = 0x33;
        private const int ModeCompareLength = 7;
        private const int DeerSpawnCallOperand = 0x14D;

        private const int TickCallRva = 0xCE27E;
        private const string TickCallPattern = "48 8D 0D ?? ?? ?? ?? E8 C6 AD 04 00 48 8D 0D ?? ?? ?? ?? E8 DA 53 05 00";
        private const int HerdManagerLeaOperand = 15;

        public const int HerdCount = WildlifeOriginTable.HerdCapacity;
        public const int HerdStride = 0x688;
        public const int HerdOwnerOffset = 0x2C;
        public const int HerdSerialOffset = 0x34;
        public const int HerdStateOffset = 0x40;
        public const int HerdKindOffset = 0x42;
        public const int HerdLeaderOffset = 0x5A;
        public const int HerdMemberBitsOffset = 0x60;
        public const int HerdMemberWords = 0x271;
        public const int HerdRouteOffset = 0x5B4;
        public const int HerdRouteCountOffset = 0x5DE;
        public const short HerdStateInUse = 2;
        public const short DeerHerd = 0xD;
        public const short GoatHerd = 0x28;

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
            SpawnHerd = imageBase + (ulong)Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, respawn + DeerSpawnCallOperand, respawn + DeerSpawnCallOperand + 4);
            HerdManager = imageBase + (ulong)Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, tick + HerdManagerLeaOperand, tick + HerdManagerLeaOperand + 4);
        }

        public ulong MultiplayerMode { get; }
        public ulong SpawnHerd { get; }
        public ulong HerdManager { get; }

        public byte* GetHerdRecord(int herd) => (byte*)HerdManager + herd * HerdStride;

        /// <summary>A record in use for a wild deer or goat herd, the only herds the mod follows.</summary>
        public bool IsWildDeerOrGoatHerd(byte* record, out bool isDeer)
        {
            short kind = *(short*)(record + HerdKindOffset);
            isDeer = kind == DeerHerd;
            return (isDeer || kind == GoatHerd) &&
                *(short*)(record + HerdStateOffset) == HerdStateInUse &&
                *(int*)(record + HerdOwnerOffset) == 0;
        }
    }
}
