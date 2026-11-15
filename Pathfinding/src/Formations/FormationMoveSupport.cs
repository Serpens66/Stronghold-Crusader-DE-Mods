using System;
using SHCDESE.EventAPI.Tribes;

namespace Pathfinding.UnitCommands
{
    internal sealed class FormationMoveSupport
    {
        [ThreadStatic] private static string source;
        [ThreadStatic] internal static FormationMoveSupport Current;

        internal static string Source => source ?? "unknown";

        internal readonly struct SourceScope : IDisposable
        {
            private readonly string previous;

            internal SourceScope(string value)
            {
                previous = source;
                source = value;
            }

            public void Dispose() => source = previous;
        }

        internal FormationMoveSupport(int tribe, int x, int y)
        {
            Tribe = tribe;
            X = x;
            Y = y;
        }

        internal readonly int Tribe, X, Y;
        internal bool? PreSkip;
        internal long? PostReturn;
        internal string Veto;
        internal int? Assigned;
        internal int? ExpectedSelectionCount, ActualSelectionCount;
        internal ulong? ExpectedSelectionHash, ActualSelectionHash;
        internal bool ExecutionSummaryObserved;
        internal string AssignmentPath;
        internal int ExpectedMembers, SelectorAssigned, TerminalAttempts, TerminalObserved,
            TerminalNotAttempted, FormationSucceeded, FallbackSucceeded, FallbackFailed, FallbackTechnicalErrors;
        internal bool ShouldWarn => PostReturn != 1 || Assigned == 0 || Veto != null;
        internal bool Matches(TribeIssueOrderMoveHereEventArgs args) =>
            args.TribeId == Tribe && args.TileX == X && args.TileY == Y;

        internal string Describe() =>
            $"preSkip={PreSkip?.ToString() ?? "unobserved"}, veto={Veto ?? "unobserved"}, " +
            $"execution={(PostReturn.HasValue ? "observed" : "unobserved")}, " +
            $"postReturn={PostReturn?.ToString() ?? "unobserved"}, assignedTargets={Assigned?.ToString() ?? "unknown"}" +
            (ExpectedSelectionCount.HasValue
                ? $", expectedSelectionCount={ExpectedSelectionCount}, actualSelectionCount={ActualSelectionCount}, " +
                    $"expectedSelectionHash={ExpectedSelectionHash:X16}, actualSelectionHash={ActualSelectionHash:X16}"
                : "") +
            (ExecutionSummaryObserved
                ? $", path={AssignmentPath}, expected={ExpectedMembers}, selectorAssigned={SelectorAssigned}, " +
                    $"terminalAttempts={TerminalAttempts}, terminalObserved={TerminalObserved}, " +
                    $"terminalNotAttempted={TerminalNotAttempted}, terminalUnobserved={TerminalAttempts - TerminalObserved}, " +
                    $"formationSucceeded={FormationSucceeded}, fallbackSucceeded={FallbackSucceeded}, " +
                    $"fallbackFailed={FallbackFailed}, fallbackTechnicalErrors={FallbackTechnicalErrors}"
                : "");
    }
}
