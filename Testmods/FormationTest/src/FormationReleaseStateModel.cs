namespace FormationTest
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
