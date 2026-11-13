namespace PlayerControlRelayTest
{
    internal enum RelayGestureHit { None, Allied, Own, Enemy }
    internal enum RelayGestureResult { None, Select, Clear, Move, Commit, Disarm }

    // The mouse rule of Vanilla's input 0x8C5F0 for a private allied selection. rightCommands is
    // !ConfigSettings.Settings_SH1RTSControls, the value DLL_RunTick passes as its param_4.
    internal static class RelayGesture
    {
        internal static RelayGestureResult Decide(bool rightCommands, bool leftReleased, bool rightDown, bool rightUp,
            bool box, bool shift, RelayGestureHit hit, bool validTile, bool paused, bool hasSelection, bool toolActive, bool armed = false)
        {
            // An armed patrol or attack behaves like Vanilla's placement cursor in both modes:
            // the left button confirms, the right button cancels.
            if (armed)
            {
                if (rightDown) return RelayGestureResult.Disarm;
                return leftReleased && validTile && !toolActive && !box ? RelayGestureResult.Commit : RelayGestureResult.None;
            }
            if (!rightCommands && rightDown) return Clear(hasSelection);
            if (rightCommands && rightUp)
                return hit != RelayGestureHit.Enemy && validTile && !paused && hasSelection && !toolActive
                    ? RelayGestureResult.Move : RelayGestureResult.None;
            // Placement and other tools own the left button; a finished drag is back at idle here.
            if (!leftReleased || toolActive) return RelayGestureResult.None;
            if (hit == RelayGestureHit.Allied) return RelayGestureResult.Select;
            if (box || hit == RelayGestureHit.Own || rightCommands) return shift ? RelayGestureResult.None : Clear(hasSelection);
            // Clicking an enemy is reserved for a later attack order.
            return hit == RelayGestureHit.None && validTile && hasSelection ? RelayGestureResult.Move : RelayGestureResult.None;
        }

        private static RelayGestureResult Clear(bool hasSelection) => hasSelection ? RelayGestureResult.Clear : RelayGestureResult.None;
    }
}
