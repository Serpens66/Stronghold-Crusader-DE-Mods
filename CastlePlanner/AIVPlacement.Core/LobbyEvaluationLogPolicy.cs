using System;
using AIVPlacement.Core;

namespace CastlePlanner.AIVPlacement.Core
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
            if (result.FailureKind == LobbyEvaluationFailureKind.NativeAutoSelectionAmbiguous ||
                result.FailureKind == LobbyEvaluationFailureKind.PriorAiSelectionUnknown ||
                result.FailureKind == LobbyEvaluationFailureKind.StartOverlapUnproven)
                return LobbyEvaluationLogSeverity.None;
            return result.FailureKind == LobbyEvaluationFailureKind.PlacementEvaluationFailed
                ? LobbyEvaluationLogSeverity.Error
                : LobbyEvaluationLogSeverity.Warning;
        }

        public static string FormatSourceDiagnostics(AivPlacementCheckResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var text = new System.Text.StringBuilder();
            foreach (var candidate in result.SourceDiagnostics)
            {
                text.Append("; candidateId=").Append(candidate.CandidateId)
                    .Append(", name=[").Append(Escape(candidate.Name))
                    .Append("], sourceKind=").Append(candidate.SourceKind)
                    .Append(", source=[").Append(Escape(candidate.Source))
                    .Append("], failure=").Append(candidate.FailureKind);
            }
            return text.ToString();
        }

        private static string Escape(string value) => value.Replace("\\", "\\\\")
            .Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n")
            .Replace("\u0085", "\\u0085").Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");

        private static bool IsExpectedUnavailableRequest(string reason) =>
            string.Equals(reason, LobbyRequestFailureKind.PreBuildSequenceUnsupported.ToString(), StringComparison.Ordinal) ||
            string.Equals(reason, LobbyRequestFailureKind.ClientEvaluationNotRequired.ToString(), StringComparison.Ordinal) ||
            string.Equals(reason, LobbyRequestFailureKind.MapUnavailable.ToString(), StringComparison.Ordinal) ||
            string.Equals(reason, LobbyRequestFailureKind.KeepAssignmentMissing.ToString(), StringComparison.Ordinal) ||
            string.Equals(reason, LobbyRequestFailureKind.AivCandidatesUnavailable.ToString(), StringComparison.Ordinal);
    }
}
