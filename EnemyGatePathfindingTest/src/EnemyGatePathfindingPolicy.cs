using System;

namespace EnemyGatePathfindingTest
{
    internal enum CapturedGateFilterDecision
    {
        PreserveVanilla,
        ExcludeForeignCapture,
        FailOpen
    }

    internal enum NativeQueryOrigin
    {
        Unavailable,
        HumanCursorOrCommandValidation,
        CommonUnitPathBuilder,
        OtherNativeCaller
    }

    internal static class EnemyGatePathfindingPolicy
    {
        internal static CapturedGateFilterDecision EvaluateGateAccess(
            int queryPlayerId,
            int ownerPlayerId,
            int capturedByPlayerId,
            Func<int, bool> isValidPlayer,
            Func<int, int, bool> isAllied)
        {
            if (isValidPlayer == null || isAllied == null ||
                !isValidPlayer(queryPlayerId) || !isValidPlayer(ownerPlayerId))
                return CapturedGateFilterDecision.FailOpen;

            if (isAllied(queryPlayerId, ownerPlayerId))
                return CapturedGateFilterDecision.PreserveVanilla;

            // Vanilla already excludes an uncaptured hostile entry. Keep its flags intact.
            if (capturedByPlayerId == 0)
                return CapturedGateFilterDecision.PreserveVanilla;
            if (!isValidPlayer(capturedByPlayerId))
                return CapturedGateFilterDecision.FailOpen;

            return isAllied(queryPlayerId, capturedByPlayerId)
                ? CapturedGateFilterDecision.PreserveVanilla
                : CapturedGateFilterDecision.ExcludeForeignCapture;
        }

        internal static NativeQueryOrigin ClassifyCallerRva(ulong callerRva)
        {
            if (callerRva == 0)
                return NativeQueryOrigin.Unavailable;
            if (callerRva >= EnemyGatePathfindingNativeDefinition.HumanCursorCommandStartRva &&
                callerRva < EnemyGatePathfindingNativeDefinition.HumanCursorCommandEndRva)
            {
                return NativeQueryOrigin.HumanCursorOrCommandValidation;
            }
            if (callerRva >= EnemyGatePathfindingNativeDefinition.CommonPathBuilderStartRva &&
                callerRva < EnemyGatePathfindingNativeDefinition.CommonPathBuilderEndRva)
            {
                return NativeQueryOrigin.CommonUnitPathBuilder;
            }
            return NativeQueryOrigin.OtherNativeCaller;
        }
    }
}
