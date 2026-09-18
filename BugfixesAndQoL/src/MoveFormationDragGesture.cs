namespace BugfixesAndQoL
{
    internal static class MoveFormationDragEligibility
    {
        internal const int MaximumSelectionCount = 10000;

        internal static bool RequiresNormalTribeOwnership(bool isMapEditor) =>
            !isMapEditor;

        internal static bool IsUsableSelectionCount(int count) =>
            count >= 2 && count <= MaximumSelectionCount;

        internal static bool IsVanillaRelease(
            int commandButton, int leftMouseState, bool rightMouseUp) =>
            commandButton == 0 ? leftMouseState == 3 : rightMouseUp;
    }

    internal enum MoveFormationGestureResult
    {
        Ignored,
        SpacingChanged,
        Released,
        Aborted
    }

    internal sealed class MoveFormationDragGesture
    {
        internal MoveFormationDragGesture(int commandButton, float pressedScreenX)
        {
            CommandButton = commandButton;
            PressedScreenX = pressedScreenX;
            Spacing = MoveFormationSpacingPolicy.Default;
        }

        internal int CommandButton { get; }
        internal float PressedScreenX { get; }
        internal int Spacing { get; private set; }
        internal bool Released { get; private set; }
        internal bool Aborted { get; private set; }

        internal MoveFormationGestureResult OnMouseDown(int mouseButton)
        {
            if (Released || Aborted || mouseButton == CommandButton)
                return MoveFormationGestureResult.Ignored;
            Aborted = true;
            return MoveFormationGestureResult.Aborted;
        }

        internal MoveFormationGestureResult OnHeld(
            int mouseButton, float currentScreenX, int screenWidth)
        {
            if (Released || Aborted || mouseButton != CommandButton)
                return MoveFormationGestureResult.Ignored;
            int spacing = MoveFormationSpacingPolicy.FromHorizontalDrag(
                PressedScreenX, currentScreenX, screenWidth);
            if (spacing == Spacing)
                return MoveFormationGestureResult.Ignored;
            Spacing = spacing;
            return MoveFormationGestureResult.SpacingChanged;
        }

        internal MoveFormationGestureResult OnMouseUp(
            int mouseButton, float currentScreenX, int screenWidth)
        {
            if (Released || Aborted || mouseButton != CommandButton)
                return MoveFormationGestureResult.Ignored;
            Spacing = MoveFormationSpacingPolicy.FromHorizontalDrag(
                PressedScreenX, currentScreenX, screenWidth);
            Released = true;
            return MoveFormationGestureResult.Released;
        }

        internal MoveFormationGestureResult Abort()
        {
            if (Released || Aborted)
                return MoveFormationGestureResult.Ignored;
            Aborted = true;
            return MoveFormationGestureResult.Aborted;
        }
    }

    internal sealed class MoveFormationReleaseGate
    {
        private readonly MoveFormationDragGesture gesture;
        private float lastScreenX;

        internal MoveFormationReleaseGate(int commandButton, float pressedScreenX)
        {
            gesture = new MoveFormationDragGesture(commandButton, pressedScreenX);
            lastScreenX = pressedScreenX;
        }

        internal int CommandButton => gesture.CommandButton;
        internal int Spacing => gesture.Spacing;
        internal bool Released => gesture.Released;
        internal bool Aborted => gesture.Aborted;
        internal bool ReleaseEventSeen { get; private set; }
        internal bool VanillaReleaseClaimed { get; private set; }

        internal MoveFormationGestureResult OnMouseDown(int mouseButton) =>
            gesture.OnMouseDown(mouseButton);

        internal MoveFormationGestureResult OnHeld(float currentScreenX, int screenWidth)
        {
            lastScreenX = currentScreenX;
            return gesture.OnHeld(CommandButton, currentScreenX, screenWidth);
        }

        internal MoveFormationGestureResult OnInputRelease(
            float currentScreenX, int screenWidth)
        {
            lastScreenX = currentScreenX;
            MoveFormationGestureResult result = gesture.OnMouseUp(
                CommandButton, currentScreenX, screenWidth);
            if (result == MoveFormationGestureResult.Released)
                ReleaseEventSeen = true;
            return result;
        }

        internal bool TryClaimVanillaRelease(
            int leftMouseState, bool rightMouseUp, int screenWidth)
        {
            if (VanillaReleaseClaimed || Aborted ||
                !MoveFormationDragEligibility.IsVanillaRelease(
                    CommandButton, leftMouseState, rightMouseUp))
                return false;
            if (!Released)
                gesture.OnMouseUp(CommandButton, lastScreenX, screenWidth);
            VanillaReleaseClaimed = true;
            return true;
        }

        internal MoveFormationGestureResult Abort() => gesture.Abort();
    }
}
