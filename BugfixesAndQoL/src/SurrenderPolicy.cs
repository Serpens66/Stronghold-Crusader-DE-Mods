namespace BugfixesAndQoL
{
    internal readonly struct SurrenderLordSnapshot
    {
        internal SurrenderLordSnapshot(
            int playerId,
            int unitId,
            int globalId,
            int ownerPlayerId,
            bool isAlive)
        {
            PlayerId = playerId;
            UnitId = unitId;
            GlobalId = globalId;
            OwnerPlayerId = ownerPlayerId;
            IsAlive = isAlive;
        }

        internal int PlayerId { get; }
        internal int UnitId { get; }
        internal int GlobalId { get; }
        internal int OwnerPlayerId { get; }
        internal bool IsAlive { get; }
    }

    internal static class SurrenderPolicy
    {
        internal static bool IsValidLord(SurrenderLordSnapshot lord) =>
            lord.PlayerId >= 1 && lord.PlayerId <= 8 &&
            lord.UnitId > 0 &&
            lord.GlobalId > 0 &&
            lord.OwnerPlayerId == lord.PlayerId &&
            lord.IsAlive;

        internal static bool CanShowButton(
            bool featureEnabled,
            bool activeMatch,
            bool mapEditor,
            bool spectator,
            SurrenderLordSnapshot lord) =>
            featureEnabled &&
            activeMatch &&
            !mapEditor &&
            !spectator &&
            IsValidLord(lord);

        internal static bool CanEnableButton(bool visible, bool realMultiplayer, bool choreReady) =>
            visible && (!realMultiplayer || choreReady);

        internal static bool IsStatisticsViewer(
            bool startSpectator,
            int localPlayerId,
            SurrenderLordSnapshot lord) =>
            startSpectator ||
            (localPlayerId >= 1 && localPlayerId <= 8 && !IsValidLord(lord));

        internal static bool CanShowStatisticsButton(
            bool featureEnabled,
            bool activeMatch,
            bool mapEditor,
            bool spectator,
            bool supportedGameMode,
            bool statisticsReady) =>
            featureEnabled &&
            activeMatch &&
            !mapEditor &&
            spectator &&
            supportedGameMode &&
            statisticsReady;

        internal static bool CanPromoteEliminatedPlayerToSpectator(
            bool featureEnabled,
            bool activeMatch,
            bool mapEditor,
            bool alreadySpectator,
            bool supportedGameMode,
            bool validLocalParticipant,
            bool previouslyHadLivingLord,
            int localPlayerId,
            SurrenderLordSnapshot currentLord) =>
            featureEnabled &&
            activeMatch &&
            !mapEditor &&
            !alreadySpectator &&
            supportedGameMode &&
            validLocalParticipant &&
            previouslyHadLivingLord &&
            localPlayerId >= 1 && localPlayerId <= 8 &&
            !IsValidLord(currentLord);

        internal static bool CanAcceptRequest(
            bool featureEnabled,
            bool activeMatch,
            bool localHost,
            bool senderKnown,
            bool senderHuman,
            SurrenderLordSnapshot senderLord) =>
            featureEnabled &&
            activeMatch &&
            localHost &&
            senderKnown &&
            senderHuman &&
            IsValidLord(senderLord);

        internal static bool CanExecute(
            int protocolVersion,
            int expectedProtocolVersion,
            int packetPlayerId,
            int packetOperationId,
            int packetLordGlobalId,
            bool duplicateOperation,
            SurrenderLordSnapshot currentLord) =>
            protocolVersion == expectedProtocolVersion &&
            packetPlayerId >= 1 && packetPlayerId <= 8 &&
            packetOperationId != 0 &&
            packetLordGlobalId > 0 &&
            !duplicateOperation &&
            IsValidLord(currentLord) &&
            currentLord.PlayerId == packetPlayerId &&
            currentLord.GlobalId == packetLordGlobalId;
    }
}
