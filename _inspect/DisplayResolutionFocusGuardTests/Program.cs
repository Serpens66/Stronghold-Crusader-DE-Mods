using System;
using BugfixesAndQoL;

namespace BugfixesAndQoL.FocusGuardTests
{
    internal static class Program
    {
        private static readonly DisplaySettingsSnapshot FullHd =
            new DisplaySettingsSnapshot(-1, -1, 1920, 1080, 100, 1);
        private static readonly DisplaySettingsSnapshot FourK =
            new DisplaySettingsSnapshot(-1, -1, 3840, 2160, 100, 1);

        private static int Main()
        {
            try
            {
                AutomaticFocusLossIsRecovered();
                RepeatedFocusLossPreservesOriginalTarget();
                ManualApplyCancelsProtection();
                DisabledAndInvalidStatesRemainInactive();
                UnfocusedStartupCanArmProtection();
                RecoveryTimeoutStopsFrameWorkButKeepsSaveProtection();
                Console.WriteLine("PASS: focus loss, protected save, recovery, manual Apply, disabled, invalid, startup-unfocused, and timeout states.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex);
                return 1;
            }
        }

        private static void AutomaticFocusLossIsRecovered()
        {
            var state = new DisplayResolutionFocusState();
            Assert(state.TryArm(FullHd, enabled: true), "1080p focus loss must arm");
            Assert(state.TryProtectSave(FourK, true, false, out var protectedSettings), "automatic 4K save must be protected");
            Assert(protectedSettings.FullscreenWidth == 1920, "protected save must retain 1080p");
            Assert(state.OnFocusGained(true, targetAlreadyMatches: false) == DisplayRecoveryAction.ApplyTarget, "focus gain must request recovery");
            Assert(state.ObserveRecovery(true, true, true, false) == DisplayRecoveryAction.None, "one matching frame is insufficient");
            Assert(state.ObserveRecovery(true, true, true, false) == DisplayRecoveryAction.Completed, "two matching frames must complete");
            Assert(!state.IsArmed, "completed recovery must clear snapshot");
        }

        private static void RepeatedFocusLossPreservesOriginalTarget()
        {
            var state = new DisplayResolutionFocusState();
            Assert(state.TryArm(FullHd, true), "initial focus loss must arm");
            Assert(!state.TryArm(FourK, true), "repeated focus loss must not replace snapshot");
            Assert(state.Snapshot.FullscreenWidth == 1920, "original target must survive repeated focus loss");
        }

        private static void ManualApplyCancelsProtection()
        {
            var state = new DisplayResolutionFocusState();
            state.TryArm(FullHd, true);
            state.Cancel();
            Assert(!state.TryProtectSave(FourK, true, true, out var settings), "manual Apply must bypass save protection");
            Assert(settings.FullscreenWidth == 3840, "manual 4K target must remain authoritative");
        }

        private static void DisabledAndInvalidStatesRemainInactive()
        {
            var disabled = new DisplayResolutionFocusState();
            Assert(!disabled.TryArm(FullHd, false), "disabled feature must not arm");
            var invalid = new DisplayResolutionFocusState();
            Assert(!invalid.TryArm(new DisplaySettingsSnapshot(-1, -1, -1, -1, -1, -1), true), "invalid settings must not arm");
            var exclusive = new DisplayResolutionFocusState();
            Assert(!exclusive.TryArm(new DisplaySettingsSnapshot(-1, -1, 1920, 1080, 100, 0), true), "exclusive fullscreen must remain untouched");
        }

        private static void UnfocusedStartupCanArmProtection()
        {
            var state = new DisplayResolutionFocusState();
            Assert(state.TryArm(FullHd, true), "loaded settings while unfocused must arm through the same state transition");
        }

        private static void RecoveryTimeoutStopsFrameWorkButKeepsSaveProtection()
        {
            var state = new DisplayResolutionFocusState();
            state.TryArm(FullHd, true);
            state.OnFocusGained(true, false);
            Assert(state.ObserveRecovery(true, true, false, true) == DisplayRecoveryAction.TimedOut, "timeout must be reported");
            Assert(state.IsArmed && !state.IsRecoveryActive, "timeout must retain save protection without recovery polling");
            Assert(state.TryProtectSave(FourK, true, false, out var protectedSettings), "save protection must survive timeout");
            Assert(protectedSettings.FullscreenWidth == 1920, "timeout protection must retain the original target");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
