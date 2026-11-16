using System;

namespace MultiplayerRestartTest
{
    // Receipts belong to one explicitly authorized replacement, never the source mission.
    // The caller observes Unity/native truth; this class only combines those observations.
    internal sealed class RestartTargetState
    {
        private bool authorized, inactive, managed, playState, native;
        internal RestartTargetState(long sourceSessionId)
        {
            if (sourceSessionId <= 0) throw new ArgumentOutOfRangeException(nameof(sourceSessionId));
            SourceSessionId = sourceSessionId;
        }

        internal long SourceSessionId { get; }
        internal long TargetSessionId { get; private set; }
        internal bool IsReady => TargetSessionId > 0 && inactive && managed && playState && native;

        internal void AuthorizeLaunch()
        {
            if (authorized) throw new InvalidOperationException("This restart already owns a launch authorization.");
            authorized = true;
        }

        internal void BindTarget(long sessionId)
        {
            if (sessionId <= 0) throw new ArgumentOutOfRangeException(nameof(sessionId));
            if (!authorized) throw new InvalidOperationException("A replacement requires explicit launch authorization.");
            if (sessionId == SourceSessionId) throw new InvalidOperationException("The source mission cannot be the restart target.");
            if (TargetSessionId != 0 && TargetSessionId != sessionId)
                throw new InvalidOperationException("Another mission replaced the authorized restart target.");
            TargetSessionId = sessionId;
        }

        // Record the explicit inactive observation before accepting a new native-start receipt.
        // A stale MPGameActive=true value, even on the target, cannot establish this proof.
        internal void NoteInactive() => inactive = true;
        internal bool MatchesTarget(long sessionId) => TargetSessionId > 0 && sessionId == TargetSessionId;
        internal bool MarkManagedReady(long sessionId)
        {
            if (!MatchesTarget(sessionId)) return false;
            managed = true;
            return true;
        }
        internal bool MarkPlayStateReady(long sessionId)
        {
            if (!MatchesTarget(sessionId)) return false;
            playState = true;
            return true;
        }
        internal bool ObserveNativeStarted(long sessionId, bool mpActive, bool simRunning)
        {
            if (!inactive || !MatchesTarget(sessionId) || !mpActive || !simRunning) return false;
            native = true;
            return true;
        }
        internal bool CanIgnoreSourceEnd(long sessionId, bool committed, bool expectedDeparture) =>
            sessionId == SourceSessionId && committed && expectedDeparture;
    }
}
