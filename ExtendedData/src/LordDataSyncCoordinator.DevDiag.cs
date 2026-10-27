using System;
using CrusaderDE;

namespace ExtendedData
{
    // Development diagnostics only: compiled by build.bat /devdiag (SERP_DEVDIAG), never in normal or release builds.
    internal sealed partial class LordDataSyncCoordinator
    {
        private string diagHostGate;
        private string diagHostCapture;
        private string diagRemoteStatus;
        private string diagMapApplied;
        private string diagClientReceived;
        private string diagClientAccepted;
        private string diagHostEcho;

        partial void DiagMapApplied(LordDataSnapshot snapshot)
        {
            if (string.Equals(diagMapApplied, snapshot.Digest, StringComparison.Ordinal)) return;
            diagMapApplied = snapshot.Digest;
            Shared.DevDiag.Log(() => "Lord-data map initialization applied: " + LordDataSyncDiagnostics.DescribeSnapshot(snapshot));
        }

        partial void DiagMapEnded() => diagMapApplied = null;

        partial void DiagHostCapture(string source, LordDataSnapshot snapshot, FRONT_Multiplayer lobby)
        {
            string signature = snapshot.SessionId + ":" + snapshot.Digest;
            if (string.Equals(signature, diagHostCapture, StringComparison.Ordinal) &&
                !string.Equals(source, "start-attempt", StringComparison.Ordinal)) return;
            diagHostCapture = signature;
            Shared.DevDiag.Log(() => "Lord-data host capture: source=" + source + "," +
                LordDataSyncDiagnostics.DescribeSnapshot(snapshot) + ",selection=" + DescribeLobbySelection(lobby));
        }

        partial void DiagHostEcho(string wireJson)
        {
            string signature = LordDataSyncDiagnostics.Hash(wireJson);
            if (string.Equals(signature, diagHostEcho, StringComparison.Ordinal)) return;
            diagHostEcho = signature;
            Shared.DevDiag.Log(() => "Lord-data setting callback ignored as local host echo: lobby=" +
                lobbyId + ",wire=" + LordDataSyncDiagnostics.DescribeJson(wireJson, false));
        }

        partial void DiagClientReceived(string wireJson)
        {
            string signature = lobbyId + ":" + LordDataSyncDiagnostics.Hash(wireJson);
            if (string.Equals(signature, diagClientReceived, StringComparison.Ordinal)) return;
            diagClientReceived = signature;
            Shared.DevDiag.Log(() => "Lord-data host setting received: lobby=" + lobbyId +
                ",wire=" + LordDataSyncDiagnostics.DescribeJson(wireJson, false));
        }

        partial void DiagClientAccepting(LordDataSnapshot snapshot, ref bool newlyAccepted)
        {
            newlyAccepted = !string.Equals(snapshot.Digest, diagClientAccepted, StringComparison.Ordinal);
            if (newlyAccepted)
                Shared.DevDiag.Log(() => "Lord-data snapshot parsed: " + LordDataSyncDiagnostics.DescribeSnapshot(snapshot));
        }

        partial void DiagClientAccepted(LordDataSnapshot snapshot) => diagClientAccepted = snapshot.Digest;

        partial void DiagRemoteStatusChanged()
        {
            string summary = DescribeAcknowledgements(lobbyHumanSlots);
            if (string.Equals(summary, diagRemoteStatus, StringComparison.Ordinal)) return;
            diagRemoteStatus = summary;
            Shared.DevDiag.Log(() => "Lord-data remote status changed: " + summary);
        }

        partial void DiagHostGateSkip(FRONT_Multiplayer lobby, string source)
        {
            string gate = DescribeHostGate(lobby);
            string signature = source + ":" + gate;
            if (string.Equals(signature, diagHostGate, StringComparison.Ordinal)) return;
            diagHostGate = signature;
            Shared.DevDiag.Log(() => "Lord-data host capture skipped: source=" + source + "," + gate);
        }

        partial void DiagResetSession()
        {
            diagHostGate = diagHostCapture = diagRemoteStatus = diagMapApplied = null;
            diagClientReceived = diagClientAccepted = diagHostEcho = null;
        }
    }
}
