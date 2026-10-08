using System;
using System.Collections.Generic;

namespace APIShared
{
    /// <summary>The physical Assassin movement branch, not merely search eligibility.</summary>
    public enum AssassinTransitionKind
    {
        /// <summary>The available evidence does not establish a permitted transition.</summary>
        Unknown,
        /// <summary>An ordinary forward or reverse native connection exists.</summary>
        Ground,
        /// <summary>A verified cardinal transition enters a wall from ground.</summary>
        ClimbUp,
        /// <summary>A verified cardinal transition leaves a wall.</summary>
        ClimbDown
    }

    /// <summary>Pure rules from DCE60 and E1640; no hooks, game queries or mutable state.</summary>
    public static class AssassinGateTransitionPolicy
    {
        /// <summary>Vanilla physical stepping accepts either endpoint's native connection.</summary>
        public static bool HasOrdinaryConnection(byte sourceConnections, byte targetConnections,
            byte forwardMask, byte reverseMask) =>
            (sourceConnections & forwardMask) != 0 || (targetConnections & reverseMask) != 0;

        /// <summary>Both connection directions precede the physical climb branch.</summary>
        public static AssassinTransitionKind Classify(int direction, byte sourceConnections,
            byte targetConnections, byte forwardMask, byte reverseMask, uint sourceFlags,
            uint targetFlags, bool targetSurfaceAccepted, bool climbEnabled, bool endpointsAccepted)
        {
            if ((uint)direction > 7) return AssassinTransitionKind.Unknown;
            if (HasOrdinaryConnection(sourceConnections, targetConnections, forwardMask, reverseMask))
                return AssassinTransitionKind.Ground;
            if ((direction & 1) != 0 || !climbEnabled || !endpointsAccepted ||
                !targetSurfaceAccepted || ((sourceFlags | targetFlags) & 0x100) == 0)
                return AssassinTransitionKind.Unknown;
            return (sourceFlags & 0x100) == 0 ? AssassinTransitionKind.ClimbUp : AssassinTransitionKind.ClimbDown;
        }

        /// <summary>Only a fully verified climb can cross a masked ground cut.</summary>
        public static bool Allows(bool groundAllowed, AssassinTransitionKind movement, bool identityCurrent) =>
            groundAllowed || (identityCurrent && (movement == AssassinTransitionKind.ClimbUp || movement == AssassinTransitionKind.ClimbDown));

        /// <summary>
        /// Route is target-first. E1640 reduces its comparison distance inside the
        /// direction loop, so every earlier adjacent stamped node must be checked.
        /// </summary>
        public static bool ValidateReconstructionField(int[] targetFirstRoute, int length, int width,
            Func<int, int, bool> allowsTransition)
        {
            if (targetFirstRoute == null || length < 1 || length > targetFirstRoute.Length || width < 1 || allowsTransition == null) return false;
            var positions = new Dictionary<int, int>(length);
            for (int i = 0; i < length; i++)
            {
                if (targetFirstRoute[i] < 0 || positions.ContainsKey(targetFirstRoute[i])) return false;
                positions.Add(targetFirstRoute[i], i);
            }
            for (int i = 0; i < length - 1; i++)
            {
                int current = targetFirstRoute[i], x = current % width, y = current / width;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if ((dx == 0 && dy == 0) || x + dx < 0 || x + dx >= width || y + dy < 0) continue;
                        int candidate = current + dy * width + dx;
                        if (positions.TryGetValue(candidate, out int earlier) && earlier > i &&
                            !allowsTransition(candidate, current)) return false;
                    }
            }
            return true;
        }
    }
}
