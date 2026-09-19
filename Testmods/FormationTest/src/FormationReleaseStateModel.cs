namespace FormationTest
{
    internal readonly struct FormationMouseState
    {
        internal FormationMouseState(
            int leftState,
            bool rightUp,
            bool stateRead,
            bool upPending)
        {
            LeftState = leftState;
            RightUp = rightUp;
            StateRead = stateRead;
            UpPending = upPending;
        }

        internal int LeftState { get; }
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

        internal static FormationMouseState SuppressAuxiliaryReleaseForOneRun(
            FormationMouseState state,
            int commandButton)
        {
            int leftState = state.LeftState;
            bool rightUp = state.RightUp;
            if (commandButton == 0)
            {
                rightUp = false;
            }
            else
            {
                if (leftState == 3)
                    leftState = 0;
            }
            return new FormationMouseState(
                leftState,
                rightUp,
                state.StateRead,
                state.UpPending);
        }

        internal static FormationMouseState Consume() =>
            new FormationMouseState(0, false, true, false);
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
