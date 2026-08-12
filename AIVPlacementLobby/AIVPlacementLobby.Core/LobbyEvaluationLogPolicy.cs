using System;
using AIVPlacement.Core;

namespace AIVPlacementLobby.Core
{
    public enum LobbyEvaluationLogSeverity
    {
        None,
        Warning,
        Error
    }

    public static class LobbyEvaluationLogPolicy
    {
        public static LobbyEvaluationLogSeverity Classify(AivPlacementCheckResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (result.Status != AivPlacementStatus.NotEvaluable)
                return LobbyEvaluationLogSeverity.None;
            if (result.FailureKind == LobbyEvaluationFailureKind.None)
                return LobbyEvaluationLogSeverity.Error;
            if (result.FailureKind == LobbyEvaluationFailureKind.RequestNotReady &&
                IsExpectedUnavailableRequest(result.FailureMessage))
            {
                return LobbyEvaluationLogSeverity.None;
            }
            return result.FailureKind == LobbyEvaluationFailureKind.PlacementEvaluationFailed
                ? LobbyEvaluationLogSeverity.Error
                : LobbyEvaluationLogSeverity.Warning;
        }

        private static bool IsExpectedUnavailableRequest(string reason) =>
            string.Equals(reason, LobbyRequestFailureKind.PreBuildSequenceUnsupported.ToString(), StringComparison.Ordinal) ||
            string.Equals(reason, LobbyRequestFailureKind.ClientEvaluationNotRequired.ToString(), StringComparison.Ordinal) ||
            string.Equals(reason, LobbyRequestFailureKind.MapUnavailable.ToString(), StringComparison.Ordinal) ||
            string.Equals(reason, LobbyRequestFailureKind.KeepAssignmentMissing.ToString(), StringComparison.Ordinal) ||
            string.Equals(reason, LobbyRequestFailureKind.AivCandidatesUnavailable.ToString(), StringComparison.Ordinal);
    }
}
