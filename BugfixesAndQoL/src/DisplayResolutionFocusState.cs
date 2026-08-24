// Feature: Pure state machine for event-driven startup and focus display-resolution protection.
namespace BugfixesAndQoL
{
    internal enum DisplayRecoveryAction
    {
        None,
        ApplyTarget,
        Completed,
        TimedOut
    }

    internal readonly struct DisplaySettingsSnapshot
    {
        public DisplaySettingsSnapshot(
            int windowWidth,
            int windowHeight,
            int fullscreenWidth,
            int fullscreenHeight,
            int fullscreenRefresh,
            int fullscreenType)
        {
            WindowWidth = windowWidth;
            WindowHeight = windowHeight;
            FullscreenWidth = fullscreenWidth;
            FullscreenHeight = fullscreenHeight;
            FullscreenRefresh = fullscreenRefresh;
            FullscreenType = fullscreenType;
        }

        public int WindowWidth { get; }
        public int WindowHeight { get; }
        public int FullscreenWidth { get; }
        public int FullscreenHeight { get; }
        public int FullscreenRefresh { get; }
        public int FullscreenType { get; }
        public bool IsValidBorderless =>
            FullscreenWidth > 0 && FullscreenHeight > 0 && FullscreenType == 1;

        public override string ToString()
        {
            return
                $"window={WindowWidth}x{WindowHeight}, " +
                $"fullscreen={FullscreenWidth}x{FullscreenHeight}@{FullscreenRefresh}Hz/type{FullscreenType}";
        }
    }

    internal sealed class DisplayResolutionFocusState
    {
        private int consecutiveMatchingFrames;

        public bool IsArmed { get; private set; }
        public bool IsRecoveryActive { get; private set; }
        public DisplaySettingsSnapshot Snapshot { get; private set; }

        public bool TryArm(DisplaySettingsSnapshot snapshot, bool enabled)
        {
            if (!enabled || IsArmed || !snapshot.IsValidBorderless)
                return false;

            Snapshot = snapshot;
            IsArmed = true;
            IsRecoveryActive = false;
            consecutiveMatchingFrames = 0;
            return true;
        }

        public bool TryProtectSave(
            DisplaySettingsSnapshot current,
            bool enabled,
            bool manualApply,
            out DisplaySettingsSnapshot protectedSettings)
        {
            if (IsArmed && enabled && !manualApply)
            {
                protectedSettings = Snapshot;
                return true;
            }

            protectedSettings = current;
            return false;
        }

        public DisplayRecoveryAction OnFocusGained(bool enabled, bool targetAlreadyMatches)
        {
            if (!enabled)
            {
                Cancel();
                return DisplayRecoveryAction.None;
            }

            if (!IsArmed)
                return DisplayRecoveryAction.None;

            if (targetAlreadyMatches)
            {
                Cancel();
                return DisplayRecoveryAction.Completed;
            }

            IsRecoveryActive = true;
            consecutiveMatchingFrames = 0;
            return DisplayRecoveryAction.ApplyTarget;
        }

        public void OnFocusLost()
        {
            IsRecoveryActive = false;
            consecutiveMatchingFrames = 0;
        }

        public DisplayRecoveryAction ObserveRecovery(
            bool enabled,
            bool focused,
            bool targetMatches,
            bool timedOut)
        {
            if (!enabled)
            {
                Cancel();
                return DisplayRecoveryAction.None;
            }

            if (!IsArmed || !IsRecoveryActive)
                return DisplayRecoveryAction.None;

            if (!focused)
            {
                OnFocusLost();
                return DisplayRecoveryAction.None;
            }

            if (timedOut)
            {
                IsRecoveryActive = false;
                consecutiveMatchingFrames = 0;
                return DisplayRecoveryAction.TimedOut;
            }

            consecutiveMatchingFrames = targetMatches
                ? consecutiveMatchingFrames + 1
                : 0;
            if (consecutiveMatchingFrames < 2)
                return DisplayRecoveryAction.None;

            Cancel();
            return DisplayRecoveryAction.Completed;
        }

        public void Cancel()
        {
            IsArmed = false;
            IsRecoveryActive = false;
            consecutiveMatchingFrames = 0;
            Snapshot = default;
        }
    }
}
