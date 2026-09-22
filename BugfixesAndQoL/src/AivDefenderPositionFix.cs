// Feature: Restore defender positions excluded from game-provided AIV sets.
//
// Native baseline FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2:
// the permanent hook displaces RVA 0x5472A..0x5473D (20 bytes). Enabled skips
// only the six-byte JB and replays XOR/MOV; disabled replays complete Vanilla.
using BepInEx.Logging;
using RedBird.Core.Memory;
using System;

namespace BugfixesAndQoL
{
    internal sealed class AivDefenderPositionFix : IDisposable
    {
        private const string AivDefenderPositionContextPattern =
            "42 83 BC 93 3C 40 8D 00 00 C7 01 00 00 00 00 75 " +
            "0F 83 F8 12 77 0A 41 0F A3 C3 0F 82 9D 03 00 00";
        private const int ReferencePatternRva = 0x54710;
        private const int RejectJumpOffset = 26;
        private const int ReferenceRejectJumpRva = 0x5472A;
        private const int MinimumHookSize = 6;
        private const int ExpectedDisplacedByteCount = 20;
        private static readonly byte[] OriginalRejectJump = { 0x0F, 0x82, 0x9D, 0x03, 0x00, 0x00 };

        private readonly BugfixesAndQoLViewModel settings;
        private readonly PermanentInstructionSkipPatch patch;
        private bool disposed;

        public AivDefenderPositionFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (!referenceHashMatches)
                throw new InvalidOperationException("The AIV defender-position fix requires the audited CrusaderDE.dll hash.");

            int patternRva = Shared.NativePatternResolver.FindUniquePattern(
                memory, AivDefenderPositionContextPattern, "AIV defender-position exclusion branch");
            int patchRva = checked(patternRva + RejectJumpOffset);
            if (patternRva != ReferencePatternRva || patchRva != ReferenceRejectJumpRva ||
                patchRva < 0 || patchRva + OriginalRejectJump.Length > memory.Length ||
                !memory.Slice(patchRva, OriginalRejectJump.Length).SequenceEqual(OriginalRejectJump))
            {
                throw new InvalidOperationException("The AIV defender-position exclusion branch does not match the audited Vanilla bytes.");
            }

            patch = new PermanentInstructionSkipPatch(
                region,
                new PermanentInstructionSkipPatch.Site(
                    libraryBase + unchecked((ulong)patchRva),
                    MinimumHookSize,
                    ExpectedDisplacedByteCount,
                    1,
                    "AIV defender-position exclusion"));
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AIV defender-position permanent hook installed: rva=0x{patchRva:X}, displaced={ExpectedDisplacedByteCount}.");
        }

        public void ApplySetting()
        {
            if (!disposed)
                patch.SetEnabled(settings.EnableMod && settings.EnableAivDefenderPositionFix);
        }

        public void Dispose()
        {
            if (disposed) return;
            patch.SetEnabled(false);
            disposed = true;
        }
    }
}
