using System;
using System.Threading;
using APIShared;
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
        private string diagSaveSession;
        private string diagLastFixesVerification;

        partial void DiagMapApplied(LordDataSnapshot snapshot)
        {
            if (string.Equals(diagMapApplied, snapshot.Digest, StringComparison.Ordinal)) return;
            diagMapApplied = snapshot.Digest;
            Shared.DevDiag.Log(() => "Lord-data map initialization applied: " + LordDataSyncDiagnostics.DescribeSnapshot(snapshot));
            string session = Volatile.Read(ref diagSaveSession);
            if (session != null)
                DiagSaveStage("mission-applied", session, null, null);
        }

        partial void DiagMapEnded()
        {
            diagMapApplied = null;
            Volatile.Write(ref diagSaveSession, null);
            Volatile.Write(ref diagLastFixesVerification, null);
        }

        partial void DiagSaveInitialization(MissionLifecycleNotification notification)
        {
            if (!notification.Context.IsSave) return;
            string session = "mission=" + notification.Context.SessionId + ",save=" +
                DiagnosticSaveName(notification.Context.FilePath) + ",role=" +
                (notification.Context.IsHost is bool host ? (host ? "host" : "client") : "unknown");
            Volatile.Write(ref diagSaveSession, session);
            DiagSaveStage("mission-" + notification.Phase, session, null, null);
        }

        partial void DiagSavePreparation(string path, LordDataSnapshot saved, LordPackageManifest manifest, bool ready) =>
            DiagSaveStage(ready ? "preparation-confirmed" : "preparation-blocked",
                "role=host,save=" + DiagnosticSaveName(path), saved, manifest);

        partial void DiagSaveArchive(string path, LordDataSnapshot saved, LordPackageManifest manifest, string result) =>
            DiagSaveStage("archive-" + result, "save=" + DiagnosticSaveName(path) +
                ",lastInitialization=" + (Volatile.Read(ref diagSaveSession) ?? "not-observed"), saved, manifest);

        partial void DiagFixesVerification(LordDataSnapshot snapshot, string source, string result) =>
            Volatile.Write(ref diagLastFixesVerification, source + ":" + snapshot.Digest + ":" + result);

        private void DiagSaveStage(string stage, string context, LordDataSnapshot saved, LordPackageManifest manifest)
        {
            Shared.DevDiag.Log(() => "Lord-data save transition: stage=" + stage + "," + context +
                ",storedDigest=" + (saved?.Digest ?? "none") +
                ",inputManifestDigest=" + (manifest?.Digest ?? "none") +
                "," + LordDataSyncDiagnostics.DescribePackageMode(packageManifest) +
                ",publishedDigest=" + (ExtendedDataModDataApi.DiagnosticPublishedLordDigest ?? "none") +
                ",lastFixesVerification=" + (Volatile.Read(ref diagLastFixesVerification) ?? "not-observed") +
                "," + DescribeAcknowledgements(lobbyHumanSlots));
        }

        private static string DiagnosticSaveName(string path)
        {
            if (string.IsNullOrEmpty(path)) return "unknown";
            int lastSeparator = Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
            return LordDataSyncDiagnostics.SafeLabel(path.Substring(lastSeparator + 1));
        }

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
            Volatile.Write(ref diagSaveSession, null);
            Volatile.Write(ref diagLastFixesVerification, null);
        }
    }
}
