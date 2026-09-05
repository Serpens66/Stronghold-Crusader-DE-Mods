// Feature: Restore AIV defender positions that Vanilla excludes for three troop rows.
//
// CrusaderDE.dll SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2:
// c_game_aiv_prepare_layout at RVA 0x53D00 skips rows 9, 11, and 18 through the
// six-byte JB at RVA 0x5472A. Removing only that branch lets the existing native
// loader process Pikeman, European Swordsman, and Arabian Swordsman positions.
using BepInEx.Logging;
using RedBird.Core.Memory;
using System;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    internal sealed class AivDefenderPositionFix : IDisposable
    {
        private const string ContextPattern =
            "42 83 BC 93 3C 40 8D 00 00 C7 01 00 00 00 00 75 " +
            "0F 83 F8 12 77 0A 41 0F A3 C3 0F 82 9D 03 00 00";
        private const int ReferencePatternRva = 0x54710;
        private const int RejectJumpOffset = 26;
        private const int ReferenceRejectJumpRva = 0x5472A;

        private static readonly byte[] OriginalRejectJump =
            { 0x0F, 0x82, 0x9D, 0x03, 0x00, 0x00 };
        private static readonly byte[] EnabledBytes =
            { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 };

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly ulong patchAddress;
        private bool applied;
        private bool disposed;

        public AivDefenderPositionFix(
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
                    "The AIV defender-position fix requires the audited CrusaderDE.dll hash.");
            }

            // The exact hash, unique full context, and fixed RVA are independent guards.
            int patternRva = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                ContextPattern,
                "AIV defender-position exclusion branch");
            if (patternRva != ReferencePatternRva)
            {
                throw new InvalidOperationException(
                    $"The AIV defender-position context resolved at unexpected RVA 0x{patternRva:X}.");
            }

            int patchRva = checked(patternRva + RejectJumpOffset);
            if (patchRva != ReferenceRejectJumpRva ||
                patchRva < 0 ||
                patchRva + OriginalRejectJump.Length > memory.Length ||
                !memory.Slice(patchRva, OriginalRejectJump.Length).SequenceEqual(OriginalRejectJump))
            {
                throw new InvalidOperationException(
                    "The AIV defender-position exclusion branch does not match the audited Vanilla bytes.");
            }

            patchAddress = checked(libraryBase + unchecked((ulong)patchRva));
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AIV defender-position patch resolved: patternRva=0x{patternRva:X}, " +
                $"patchRva=0x{patchRva:X}, original={ToHex(OriginalRejectJump)}.");
        }

        public void ApplySetting()
        {
            if (disposed)
                return;

            SetEnabled(
                settings.EnableMod &&
                settings.EnableAiFixes &&
                settings.EnableAivDefenderPositionFix);
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
            byte[] currentState = applied ? EnabledBytes : OriginalRejectJump;
            VerifyCurrentBytes(currentState, enabled ? "enable" : "disable");
            if (enabled == applied)
                return;

            byte[] targetState = enabled ? EnabledBytes : OriginalRejectJump;
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
                        "The AIV defender-position patch transition and rollback both failed.",
                        transitionError,
                        rollbackError);
                }

                throw;
            }

            applied = enabled;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"AIV defender-position patch {(enabled ? "enabled" : "disabled")}: " +
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
                        "The AIV defender-position patch bytes changed during a failed transition.");
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
                    $"Cannot {operation} the AIV defender-position patch because its native bytes changed: " +
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
