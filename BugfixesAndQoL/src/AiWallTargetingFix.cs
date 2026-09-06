// Feature: Let multiple AI attackers share an otherwise valid wall-tile target.
//
// CrusaderDE.dll SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2:
// FUN_18010DF60 at RVA 0x10DF60 excludes a wall candidate at RVA 0x10ECC3 when
// another unit has already reserved it. Removing only that two-byte branch keeps
// all reachability and approach-tile validation while lifting the one-unit limit.
// The replacement spans exactly RVA 0x10ECC3..0x10ECC5. NOPs do not change any
// register, stack slot, or flag; execution continues at the original fallthrough.
using BepInEx.Logging;
using RedBird.Core.Memory;
using System;
using System.Runtime.InteropServices;

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

        private static readonly byte[] OriginalReservationRejectJump = { 0x75, 0x63 };
        private static readonly byte[] EnabledBytes = { 0x90, 0x90 };

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly ulong patchAddress;
        private bool applied;
        private bool disposed;

        public AiWallTargetingFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The AI wall-targeting fix requires the audited CrusaderDE.dll hash.");
            }

            // Hash, unique context, fixed RVA, and original bytes independently guard the patch.
            int patternRva = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                ContextPattern,
                "AI wall-target reservation rejection branch");
            if (patternRva != ReferencePatternRva)
            {
                throw new InvalidOperationException(
                    $"The AI wall-targeting context resolved at unexpected RVA 0x{patternRva:X}.");
            }

            int patchRva = checked(patternRva + ReservationRejectJumpOffset);
            if (patchRva != ReferenceReservationRejectJumpRva ||
                patchRva < 0 ||
                patchRva + OriginalReservationRejectJump.Length > memory.Length ||
                !memory.Slice(patchRva, OriginalReservationRejectJump.Length)
                    .SequenceEqual(OriginalReservationRejectJump))
            {
                throw new InvalidOperationException(
                    "The AI wall-target reservation branch does not match the audited Vanilla bytes.");
            }

            patchAddress = checked(libraryBase + unchecked((ulong)patchRva));
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AI wall-targeting patch resolved: patternRva=0x{patternRva:X}, " +
                $"patchRva=0x{patchRva:X}, original={ToHex(OriginalReservationRejectJump)}.");
        }

        public void ApplySetting()
        {
            if (disposed)
                return;

            SetEnabled(
                settings.EnableMod &&
                settings.EnableAiFixes &&
                settings.EnableAiWallTargetingFix);
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (applied)
                SetEnabled(false);
            disposed = true;
        }

        private void SetEnabled(bool enabled)
        {
            byte[] currentState = applied ? EnabledBytes : OriginalReservationRejectJump;
            VerifyCurrentBytes(currentState, enabled ? "enable" : "disable");
            if (enabled == applied)
                return;

            byte[] targetState = enabled ? EnabledBytes : OriginalReservationRejectJump;
            try
            {
                CodePatch.Write(patchAddress, targetState);
                VerifyCurrentBytes(targetState, "verify");
            }
            catch (Exception transitionError)
            {
                Exception rollbackError = TryRollback(targetState, currentState);
                if (rollbackError != null)
                {
                    throw new AggregateException(
                        "The AI wall-targeting patch transition and rollback both failed.",
                        transitionError,
                        rollbackError);
                }

                throw;
            }

            applied = enabled;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"AI wall-targeting patch {(enabled ? "enabled" : "disabled")}: " +
                $"address=0x{patchAddress:X}, bytes={ToHex(targetState)}.");
        }

        private Exception TryRollback(byte[] transitionBytes, byte[] rollbackBytes)
        {
            try
            {
                byte[] current = ReadBytes(transitionBytes.Length);
                if (current.AsSpan().SequenceEqual(rollbackBytes))
                    return null;
                if (!current.AsSpan().SequenceEqual(transitionBytes))
                {
                    throw new InvalidOperationException(
                        "The AI wall-targeting patch bytes changed during a failed transition.");
                }

                CodePatch.Write(patchAddress, rollbackBytes);
                VerifyCurrentBytes(rollbackBytes, "roll back");
                return null;
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        private void VerifyCurrentBytes(byte[] expected, string operation)
        {
            byte[] current = ReadBytes(expected.Length);
            if (!current.AsSpan().SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    $"Cannot {operation} the AI wall-targeting patch because its native bytes changed: " +
                    $"expected={ToHex(expected)}, actual={ToHex(current)}.");
            }
        }

        private byte[] ReadBytes(int length)
        {
            byte[] bytes = new byte[length];
            Marshal.Copy(unchecked((IntPtr)(long)patchAddress), bytes, 0, length);
            return bytes;
        }

        private static string ToHex(byte[] bytes) =>
            BitConverter.ToString(bytes).Replace('-', ' ');
    }
}
