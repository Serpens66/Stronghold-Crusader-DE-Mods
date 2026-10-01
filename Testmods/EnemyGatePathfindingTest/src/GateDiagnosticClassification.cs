namespace EnemyGatePathfindingTest
{
    // Read-only labels. Never supplies an access decision to the runtime policy.
    internal static class GateDiagnosticClassification
    {
        internal static string Describe(NativeGateAccessSnapshot snapshot, int player,
            int buildingId, uint globalId, int owner, int captured)
        {
            string reason = null;
            if (player <= 0 || player > 8) reason = "invalid-query-player";
            else if (owner <= 0 || owner > 8 || captured < 0 || captured > 8)
                reason = "invalid-owner-or-capturer";
            else if (snapshot == null || !snapshot.MatchesGateIdentity(buildingId, globalId))
                reason = "identity-unverified";
            else if (buildingId >= snapshot.RecordsByBuildingId.Length ||
                !snapshot.RecordsByBuildingId[buildingId].Valid) reason = "record-untracked";
            else if (snapshot.RecordsByBuildingId[buildingId].OwnerPlayerId != owner)
                reason = "owner-snapshot-mismatch";
            else if (snapshot.RecordsByBuildingId[buildingId].CapturedByPlayerId != captured)
                reason = "capture-snapshot-mismatch";
            string identity = ",gateGlobal=" + globalId + ",ownerId=" + owner + ",capturerId=" + captured;
            if (reason != null) return Unknown(reason) + identity;
            NativeGateAccessRecord record = snapshot.RecordsByBuildingId[buildingId];
            int bit = 1 << player;
            string ownerRelation = player == owner ? "own" :
                (record.OwnerRelatedPlayers & bit) != 0 ? "allied" : "enemy";
            string captureRelation = captured == 0 ? "uncaptured" :
                player == captured ? "captured-by-self" :
                (record.CapturerRelatedPlayers & bit) != 0 ? "captured-by-ally" : "captured-by-other";
            return "ownerRelation=" + ownerRelation + ",captureRelation=" + captureRelation +
                ",policyDecision=" + snapshot.Evaluate(player, buildingId, owner, captured) +
                ",edgePolicy=" + ((record.UnrelatedPlayers & bit) != 0 ? "restricted" : "unrestricted") +
                ",classificationSource=identity-verified-policy-snapshot" + identity;
        }

        internal static string Unknown(string reason) =>
            "ownerRelation=unknown,captureRelation=unknown,policyDecision=unknown,edgePolicy=unknown," +
            "classificationSource=unverified,classificationReason=" + reason;

        internal static string BuildingContextResult(int rawArgument, int tribePlayer, int usedPlayer) =>
            "rawSearchArgument=" + rawArgument + ",argumentRole=unverified," +
            "tribePlayer=" + tribePlayer + ",resolution=" +
            (usedPlayer > 0 ? "validated" : "fail-open") + ",usedPlayer=" + usedPlayer;
    }
}
