// Feature: Let multiple AI attackers share an otherwise valid wall-tile target.
//
// Native baseline FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2:
// FUN_18010DF60 rejects a reserved candidate at RVA 0x10ECC3. The permanent hook
// displaces RVA 0x10ECC3..0x10ECD1 (15 bytes). When enabled it skips only the
// two-byte JNE and replays the following MOV/LEA; disabled replays all Vanilla.
using BepInEx.Logging;
using RedBird.Core.Memory;
using System;

namespace BugfixesAndQoL
{
    internal sealed class AiWallTargetingFix : IDisposable
    {
        private const string ContextPattern =
            "8B D3 49 8B CC E8 ?? ?? ?? ?? 85 C0 75 63 8B 05 ?? ?? ?? ?? " +
            "4C 8D 3D ?? ?? ?? ?? 41 8D 04 C6 48 98 41 8B 14 87 03 D3 " +
            "48 63 C2 41 F7 84 87 00 84 89 00 00 01 00 10 75 1A";
        private const int ReferencePatternRva = 0x10ECB7;
        private const int ReservationRejectJumpOffset = 12;
        private const int ReferenceReservationRejectJumpRva = 0x10ECC3;
        private const int MinimumHookSize = 2;
        private const int ExpectedDisplacedByteCount = 15;
        private static readonly byte[] OriginalReservationRejectJump = { 0x75, 0x63 };

        private readonly BugfixesAndQoLViewModel settings;
        private readonly PermanentInstructionSkipPatch patch;
        private bool disposed;

        public AiWallTargetingFix(
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
                throw new InvalidOperationException("The AI wall-targeting fix requires the audited CrusaderDE.dll hash.");

            int patternRva = Shared.NativePatternResolver.FindUniquePattern(
                memory, ContextPattern, "AI wall-target reservation rejection branch");
            int patchRva = checked(patternRva + ReservationRejectJumpOffset);
            if (patternRva != ReferencePatternRva || patchRva != ReferenceReservationRejectJumpRva ||
                patchRva < 0 || patchRva + OriginalReservationRejectJump.Length > memory.Length ||
                !memory.Slice(patchRva, OriginalReservationRejectJump.Length).SequenceEqual(OriginalReservationRejectJump))
            {
                throw new InvalidOperationException("The AI wall-target reservation branch does not match the audited Vanilla bytes.");
            }

            patch = new PermanentInstructionSkipPatch(
                region,
                new PermanentInstructionSkipPatch.Site(
                    libraryBase + unchecked((ulong)patchRva),
                    MinimumHookSize,
                    ExpectedDisplacedByteCount,
                    1,
                    "AI wall-target reservation rejection"));
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AI wall-targeting permanent hook installed: rva=0x{patchRva:X}, displaced={ExpectedDisplacedByteCount}.");
        }

        public void ApplySetting()
        {
            if (!disposed)
                patch.SetEnabled(settings.EnableMod && settings.EnableAiWallTargetingFix);
        }

        public void Dispose()
        {
            if (disposed) return;
            patch.SetEnabled(false);
            disposed = true;
        }
    }
}
