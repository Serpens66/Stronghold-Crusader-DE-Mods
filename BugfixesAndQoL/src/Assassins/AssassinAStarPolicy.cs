// Feature: Exact, allocation-free priority and heuristic rules for Assassin A*.
using System;

namespace BugfixesAndQoL
{
    internal static class AssassinAStarPolicy
    {
        public static int EstimateOctileTicks(
            int x,
            int y,
            int targetX,
            int targetY,
            int cardinalTicks,
            int diagonalTicks)
        {
            int dx = Math.Abs(targetX - x);
            int dy = Math.Abs(targetY - y);
            int diagonalSteps = Math.Min(dx, dy);
            int straightSteps = Math.Max(dx, dy) - diagonalSteps;

            // A diagonal can always be replaced by two cardinal steps in the
            // obstacle-free relaxation used by the heuristic.
            int effectiveDiagonalTicks = Math.Min(
                diagonalTicks,
                SaturatingAdd(cardinalTicks, cardinalTicks));
            long estimate = (long)diagonalSteps * effectiveDiagonalTicks +
                (long)straightSteps * cardinalTicks;
            return estimate >= int.MaxValue ? int.MaxValue : (int)estimate;
        }

        public static int SaturatingAdd(int left, int right)
        {
            if (left == int.MaxValue || right == int.MaxValue ||
                left > int.MaxValue - right)
            {
                return int.MaxValue;
            }
            return left + right;
        }

        public static bool ComesBefore(
            int leftEstimatedTotal,
            int leftCost,
            int leftInsertionOrder,
            int rightEstimatedTotal,
            int rightCost,
            int rightInsertionOrder)
        {
            return leftEstimatedTotal < rightEstimatedTotal ||
                (leftEstimatedTotal == rightEstimatedTotal &&
                 (leftCost < rightCost ||
                  (leftCost == rightCost && leftInsertionOrder < rightInsertionOrder)));
        }
    }
}
