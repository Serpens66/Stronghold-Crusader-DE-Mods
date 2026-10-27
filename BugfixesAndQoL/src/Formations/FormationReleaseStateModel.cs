namespace BugfixesAndQoL.UnitCommands
{
    internal readonly struct FormationMouseState
    {
        internal FormationMouseState(
            int leftState,
            bool rightDown,
            bool rightUp,
            bool stateRead,
            bool upPending)
        {
            LeftState = leftState;
            RightDown = rightDown;
            RightUp = rightUp;
            StateRead = stateRead;
            UpPending = upPending;
        }

        internal int LeftState { get; }
        internal bool RightDown { get; }
        internal bool RightUp { get; }
        internal bool StateRead { get; }
        internal bool UpPending { get; }
    }

    internal static class FormationReleaseStateModel
    {
        internal static bool HasCommandRelease(
            FormationMouseState state,
            int commandButton) =>
            commandButton == 0 ? state.LeftState == 3 : state.RightUp;

        internal static FormationMouseState Consume() =>
            new FormationMouseState(0, false, false, true, false);

        // Restore just the deferred command edge. A new down supersedes the old edge;
        // input for the other mouse button must survive the handoff.
        internal static FormationMouseState RestoreDeferredForVanilla(
            FormationMouseState current, FormationMouseState deferred, int commandButton) =>
            commandButton == 0
                ? current.LeftState == 1 || current.LeftState == 2 ? current :
                    new FormationMouseState(deferred.LeftState, current.RightDown, current.RightUp,
                        deferred.StateRead, deferred.UpPending)
                : current.RightDown ? current :
                    new FormationMouseState(current.LeftState, current.RightDown, deferred.RightUp,
                        current.StateRead, current.UpPending);

        // Remove only the old command edge; a new down or the opposite button
        // may already have been published by Unity when the deferred gesture aborts.
        internal static FormationMouseState DiscardDeferred(FormationMouseState state, int commandButton) =>
            commandButton == 0
                ? new FormationMouseState(state.LeftState == 3 ? 0 : state.LeftState,
                    state.RightDown, state.RightUp, state.LeftState == 3 || state.StateRead, false)
                : new FormationMouseState(state.LeftState, state.RightDown, false, state.StateRead, state.UpPending);
    }

    internal sealed class FormationReleaseGate
    {
        internal FormationReleaseGate(int commandButton)
        {
            CommandButton = commandButton;
        }

        internal int CommandButton { get; }
        internal bool ReleaseEventSeen { get; private set; }
        internal bool VanillaReleaseClaimed { get; private set; }
        internal bool CanModify => !ReleaseEventSeen && !VanillaReleaseClaimed;

        internal bool ObserveInputRelease()
        {
            if (!CanModify)
                return false;
            ReleaseEventSeen = true;
            return true;
        }

        internal bool TryClaimVanillaRelease(FormationMouseState state)
        {
            if (VanillaReleaseClaimed ||
                !FormationReleaseStateModel.HasCommandRelease(state, CommandButton))
                return false;
            VanillaReleaseClaimed = true;
            return true;
        }
    }

}
