namespace BugfixesAndQoL
{
    internal static class MoveFormationDragEligibility
    {
        internal static bool RequiresNormalTribeOwnership(bool isMapEditor) =>
            !isMapEditor;

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
}
