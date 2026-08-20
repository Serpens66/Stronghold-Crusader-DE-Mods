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
