namespace BugfixesAndQoL.GateRouting
{
    // Narrow read-only subset of DCE60: only its unambiguous roof-to-ground branch.
    internal static class AssassinGateDescentRule
    {
        internal static bool Allows(int direction, int consumedDirection, int index, int length,
            int pathMode, int specialStep, int alternateStep, int state, bool enabled,
            byte sourceConnections, byte targetConnections, byte forwardMask, byte reverseMask,
            uint sourceFlags, uint targetFlags)
        {
            if (!enabled || direction < 0 || direction > 7 || (direction & 1) != 0 ||
                direction != consumedDirection || index < 1 || index > length || length > 2000 ||
                pathMode != 0 || specialStep != 0 || alternateStep != 0 || state != 101 ||
                forwardMask == 0 || reverseMask == 0) return false;
            // Ordinary forward OR reverse connection wins in Vanilla: never exempt ground travel.
            return (sourceConnections & forwardMask) == 0 && (targetConnections & reverseMask) == 0 &&
                (sourceFlags & 0x100) != 0 && (targetFlags & 0x100) == 0 &&
                (targetFlags & 0x4A5014B1) == 0;
        }
    }
}
