using BepInEx.Logging;
using RedBird.Core.Memory;
using SHCDESE.Interop;
using System;

namespace ImprovedHunters
{
    /// <summary>
    /// Gives melee units the rule ranged units already follow: rabbits are only attacked
    /// after an attack order on a rabbit, and then also nearby rabbits until the next order.
    /// The tribe flag this depends on has no second independent anchor, so the feature is
    /// bound to the audited DLL (see UpdateToNewDLL.md).
    /// </summary>
    internal sealed class RabbitMeleeOrderPatch : IDisposable
    {
        private const int HostilityRabbitChainRva = 0x18694C;
        private const string HostilityRabbitChainPattern =
            "66 83 F9 58 74 5B 66 83 F9 2E 74 55 66 83 F9 09 74 06 66 83 F9 33 75 23 " +
            "80 3D ?? ?? ?? ?? 00 74 40 8B 05 ?? ?? ?? ?? 83 F8 63 74 35 85 C0 74 31 " +
            "B8 01 00 00 00 48 8B 5C 24 08 C3 66 83 F9 34 0F 85 ?? ?? ?? ??";
        private const int HostilitySiteOffset = 6;
        private const int HostilityDisplaced = 16;
        private const int HostilityAttackableOffset = 0x61;
        private const int HostilityChainNextOffset = 0x18;
        private const int HostilityOtherDisplacementOffset = 65;
        private const int HostilityOtherNextOffset = 69;

        private const int MeleeOrderBlockRva = 0x11EE64;
        private const string MeleeOrderBlockPattern =
            "66 47 89 B4 27 3C 06 00 00 44 8B CE 8B 84 2B 2C 07 00 00 43 89 84 27 44 06 00 00";
        private const int MeleeOrderDisplaced = 19;

        private const int StanceScanFilterRva = 0x18C731;
        private const string StanceScanFilterPattern =
            "41 80 BA 84 09 00 00 00 74 22 41 8D 41 D3 66 83 F8 2A 0F 87 ?? ?? ?? ?? " +
            "48 BA 01 00 40 00 00 04 00 00 48 0F A3 C2 0F 83 ?? ?? ?? ?? 41 8D 41 FF";
        private const int StanceDisplaced = 14;
        private const int StanceAcceptOffset = 0x2C;

        private const int RangedFlagReaderRva = 0x18F315;
        private const string RangedFlagReaderPattern =
            "48 69 C8 88 06 00 00 66 83 BC 11 ?? ?? ?? ?? 00 0F 84 ?? ?? ?? ?? E9 ?? ?? ?? ??";
        private const int RangedFlagDisplacementOffset = 11;
        private const int TribeRabbitHuntFlagOffset = 0x58E;
        private const int AuditedFlagRva = 0x7CC6CAE;

        private readonly PermanentNativeGate gate;

        public RabbitMeleeOrderPatch(
            ManualLogSource log,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));
            if (memory.Length == 0 || imageBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");
            if (!referenceHashMatches)
                throw new InvalidOperationException("The melee rabbit rule is bound to the audited native hash.");

            int chain = Shared.NativePatternResolver.ResolveUnique(
                memory, HostilityRabbitChainPattern, HostilityRabbitChainRva, true, "melee hostility rabbit chain", log).Rva;
            int order = Shared.NativePatternResolver.ResolveUnique(
                memory, MeleeOrderBlockPattern, MeleeOrderBlockRva, true, "melee attack order block", log).Rva;
            int stance = Shared.NativePatternResolver.ResolveUnique(
                memory, StanceScanFilterPattern, StanceScanFilterRva, true, "melee stance scan filter", log).Rva;
            int reader = Shared.NativePatternResolver.ResolveUnique(
                memory, RangedFlagReaderPattern, RangedFlagReaderRva, true, "ranged rabbit flag reader", log).Rva;
            int flagRva = Shared.NativePatternResolver.ReadInt32(memory, reader + RangedFlagDisplacementOffset);
            if (flagRva != AuditedFlagRva)
                throw new InvalidOperationException($"The tribe rabbit flag moved: 0x{flagRva:X}.");

            int notAttackable = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, chain + HostilityOtherDisplacementOffset, chain + HostilityOtherNextOffset);
            int attackable = chain + HostilityAttackableOffset;
            int nextChain = chain + HostilityChainNextOffset;

            int hunter = (int)eChimps.CHIMP_TYPE_HUNTER;
            int rabbit = (int)eChimps.CHIMP_TYPE_RABBIT;
            PermanentNativeGate candidate = new PermanentNativeGate(region, imageBase);
            try
            {
                ulong flag = candidate.FlagAddress;
                ulong flagBase = imageBase + (ulong)flagRva;
                ulong notAttackableAddress = imageBase + (ulong)notAttackable;
                ulong attackableAddress = imageBase + (ulong)attackable;
                ulong nextChainAddress = imageBase + (ulong)nextChain;
                ulong acceptAddress = imageBase + (ulong)(stance + StanceAcceptOffset);
                candidate.AddSite(memory, chain + HostilitySiteOffset, HostilityDisplaced,
                    (a, i) => HunterNativeStubs.EmitRabbitMeleeHostility(
                        a, i, flag, flagBase, notAttackableAddress, attackableAddress, nextChainAddress, hunter));
                candidate.AddSite(memory, order, MeleeOrderDisplaced,
                    (a, i) => HunterNativeStubs.EmitRabbitMeleeOrder(a, i, flag, rabbit, TribeRabbitHuntFlagOffset));
                candidate.AddSite(memory, stance, StanceDisplaced,
                    (a, i) => HunterNativeStubs.EmitRabbitStanceScan(a, i, flag, flagBase, acceptAddress, rabbit));
                candidate.Commit();
                gate = candidate;
            }
            catch
            {
                candidate.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Improved Hunters melee rabbit rule installed: hostility=0x{chain + HostilitySiteOffset:X}, " +
                $"order=0x{order:X}, stance=0x{stance:X}, tribeFlag=0x{flagRva:X}.");
        }

        public bool IsAvailable => gate?.IsInstalled == true;

        public void SetEnabled(bool enabled) => gate?.SetEnabled(enabled);

        public void Dispose() => gate?.Dispose();
    }
}
