using BepInEx.Logging;
using RedBird.Core.Memory;
using SHCDESE.Interop;
using System;

namespace ImprovedHunters
{
    /// <summary>
    /// Makes chickens huntable while they keep their owner, so Vanilla's wandering,
    /// counting and removal stay intact. Vanilla blocks a Hunter's own animals three
    /// times: in the target query, in the explicit attack and at the arrow hit.
    /// </summary>
    internal sealed class OwnedChickenHuntPatch : IDisposable
    {
        private const int HunterQueryOwnerSequenceRva = 0x18AFC0;
        private const string HunterQueryOwnerSequencePattern =
            "66 83 BB EC FD FF FF 02 0F 85 ?? ?? ?? ?? 66 83 3B 00 0F 85 ?? ?? ?? ?? " +
            "66 83 BB F6 FD FF FF 00 0F 85 ?? ?? ?? ?? 0F B7 83 EE FD FF FF";
        private const int QuerySiteOffset = 24;
        private const int QueryDisplaced = 14;

        private const int ExplicitAttackTeamSequenceRva = 0x18EBC2;
        private const string ExplicitAttackTeamSequencePattern =
            "49 0F BF 8C 28 EE 06 00 00 4C 8D 0D ?? ?? ?? ?? 43 8B 84 A9 ?? ?? ?? ?? " +
            "41 39 84 89 ?? ?? ?? ?? 0F 84 ?? ?? ?? ?? 41 83 FC 06";
        private const int AttackSiteOffset = 0x18;
        private const int AttackDisplaced = 14;

        private const int ArrowTeamSequenceRva = 0x192A87;
        private const string ArrowTeamSequencePattern =
            "4F 0F BF 84 28 ?? ?? ?? ?? 43 8B 84 93 ?? ?? ?? ?? 4C 8D 1D ?? ?? ?? ?? " +
            "4E 8D 0C 85 00 00 00 00 43 39 84 19 ?? ?? ?? ?? 44 0F B7 5C 24 78 75 49 " +
            "45 85 F6 0F 84 ?? ?? ?? ??";
        private const int ArrowSiteOffset = 0x20;
        private const int ArrowDisplaced = 14;
        private const int ArrowOwnerDisplacementOffset = 5;
        private const int ArrowMissDisplacementOffset = 0x35;
        private const int ArrowMissNextOffset = 0x39;
        private const int ArrowGateOffset = 0x79;

        // The whole block between the team check and the warning sound: the stub repeats
        // its gates and continues behind the warning, so all of it must stay this shape.
        private const int ArrowGateBlockRva = 0x192B00;
        private const string ArrowGateBlockPattern =
            "66 42 83 BC 3E F8 08 00 00 00 0F 85 ?? ?? ?? ?? 42 0F BF 84 3E 12 07 00 00 " +
            "46 0F BF 8C 3E 14 07 00 00 44 03 C8 48 8D 05 ?? ?? ?? ?? 42 0F BF 8C 28 ?? ?? ?? ?? " +
            "44 3B C9 0F 8F ?? ?? ?? ?? 41 8D 04 11 3B C1 0F 8C ?? ?? ?? ?? 66 45 85 C0 74 28 " +
            "66 45 3B C2 74 22 48 8D 05 ?? ?? ?? ?? 66 46 89 84 3E 58 0A 00 00 " +
            "42 0F B7 84 28 ?? ?? ?? ?? 66 42 89 84 3E 8E 0A 00 00 44 3B 15 ?? ?? ?? ?? 75 12 " +
            "48 8D 0D ?? ?? ?? ?? E8 ?? ?? ?? ?? 44 0F B7 5C 24 78 4C 8B 4C 24 70";
        private const int GateMissDisplacementOffset = 12;
        private const int GateMissNextOffset = 16;
        private const int GateHeightDisplacementOffset = 49;
        private const int GateSourceDisplacementOffset = 107;
        private const int GateContinueOffset = 0x93;
        // Field distances inside one projectile record: independent proof of the three displacements.
        private const int ProjectileOwnerToHeight = 0x10;
        private const int ProjectileOwnerToSource = 0x76;
        private const int ArrowProjectileType = 1;

        private readonly PermanentNativeGate gate;

        public OwnedChickenHuntPatch(
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

            int querySequence = Shared.NativePatternResolver.ResolveUnique(
                memory, HunterQueryOwnerSequencePattern, HunterQueryOwnerSequenceRva,
                referenceHashMatches, "Hunter query owner check", log).Rva;
            int queryReject = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, querySequence + QuerySiteOffset + 10, querySequence + QuerySiteOffset + 14);
            int aliveReject = Shared.NativePatternResolver.ResolveRelativeTarget(memory, querySequence + 10, querySequence + 14);
            int corpseReject = Shared.NativePatternResolver.ResolveRelativeTarget(memory, querySequence + 20, querySequence + 24);
            if (queryReject != aliveReject || queryReject != corpseReject)
                throw new InvalidOperationException("The Hunter query candidate branches no longer share one rejection.");

            int attackSequence = Shared.NativePatternResolver.ResolveUnique(
                memory, ExplicitAttackTeamSequencePattern, ExplicitAttackTeamSequenceRva,
                referenceHashMatches, "explicit attack team check", log).Rva;
            int attackReject = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, attackSequence + AttackSiteOffset + 10, attackSequence + AttackSiteOffset + 14);
            if (Shared.NativePatternResolver.ReadInt32(memory, attackSequence + 0x14) !=
                Shared.NativePatternResolver.ReadInt32(memory, attackSequence + AttackSiteOffset + 4))
                throw new InvalidOperationException("The explicit attack no longer compares two team table entries.");

            int arrowSequence = Shared.NativePatternResolver.ResolveUnique(
                memory, ArrowTeamSequencePattern, ArrowTeamSequenceRva,
                referenceHashMatches, "arrow team check", log).Rva;
            int arrowGate = Shared.NativePatternResolver.ResolveUnique(
                memory, ArrowGateBlockPattern, ArrowGateBlockRva,
                referenceHashMatches, "arrow hit gates", log).Rva;
            if (arrowGate != arrowSequence + ArrowGateOffset)
                throw new InvalidOperationException("The arrow team check no longer branches to the hit gates.");

            int miss = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory, arrowSequence + ArrowMissDisplacementOffset, arrowSequence + ArrowMissNextOffset);
            if (miss != Shared.NativePatternResolver.ResolveRelativeTarget(
                    memory, arrowGate + GateMissDisplacementOffset, arrowGate + GateMissNextOffset))
                throw new InvalidOperationException("The arrow hit gates no longer share one miss exit.");

            int ownerDisplacement = Shared.NativePatternResolver.ReadInt32(memory, arrowSequence + ArrowOwnerDisplacementOffset);
            int heightDisplacement = Shared.NativePatternResolver.ReadInt32(memory, arrowGate + GateHeightDisplacementOffset);
            int sourceDisplacement = Shared.NativePatternResolver.ReadInt32(memory, arrowGate + GateSourceDisplacementOffset);
            if (heightDisplacement - ownerDisplacement != ProjectileOwnerToHeight ||
                sourceDisplacement - ownerDisplacement != ProjectileOwnerToSource)
                throw new InvalidOperationException("The projectile field layout changed.");

            int chicken = (int)eChimps.CHIMP_TYPE_CHICKEN;
            int hunter = (int)eChimps.CHIMP_TYPE_HUNTER;
            PermanentNativeGate candidate = new PermanentNativeGate(region, imageBase);
            try
            {
                ulong flag = candidate.FlagAddress;
                ulong rejectQuery = imageBase + (ulong)queryReject;
                ulong rejectAttack = imageBase + (ulong)attackReject;
                ulong continueHit = imageBase + (ulong)(arrowGate + GateContinueOffset);
                ulong missHit = imageBase + (ulong)miss;
                candidate.AddSite(memory, querySequence + QuerySiteOffset, QueryDisplaced,
                    (a, i) => HunterNativeStubs.EmitOwnedChickenQuery(a, i, flag, rejectQuery, chicken));
                candidate.AddSite(memory, attackSequence + AttackSiteOffset, AttackDisplaced,
                    (a, i) => HunterNativeStubs.EmitOwnedChickenExplicitAttack(a, i, flag, rejectAttack, chicken, hunter));
                candidate.AddSite(memory, arrowSequence + ArrowSiteOffset, ArrowDisplaced,
                    (a, i) => HunterNativeStubs.EmitOwnedChickenArrowHit(
                        a, i, flag, continueHit, missHit, chicken, hunter, ArrowProjectileType,
                        sourceDisplacement, heightDisplacement));
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
                $"Improved Hunters owned-chicken hunting installed: query=0x{querySequence + QuerySiteOffset:X}, " +
                $"attack=0x{attackSequence + AttackSiteOffset:X}, arrow=0x{arrowSequence + ArrowSiteOffset:X}, " +
                $"continue=0x{arrowGate + GateContinueOffset:X}, miss=0x{miss:X}.");
        }

        public bool IsAvailable => gate?.IsInstalled == true;

        public void SetEnabled(bool enabled) => gate?.SetEnabled(enabled);

        public void Dispose() => gate?.Dispose();
    }
}
