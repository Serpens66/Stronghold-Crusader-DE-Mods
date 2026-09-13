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

    internal readonly struct StatisticsTeamBadgeStyle
    {
        internal StatisticsTeamBadgeStyle(byte red, byte green, byte blue, bool useDarkText)
        {
            Red = red;
            Green = green;
            Blue = blue;
            UseDarkText = useDarkText;
        }

        internal byte Red { get; }
        internal byte Green { get; }
        internal byte Blue { get; }
        internal bool UseDarkText { get; }
    }

    internal static class SurrenderPolicy
    {
        internal const int StatisticsTeamBadgesOff = 0;
        internal const int StatisticsTeamBadgesVanillaIcons = 1;
        internal const int StatisticsTeamBadgesEasyReadIcons = 2;
        internal const int DefaultStatisticsTeamBadgeMode = StatisticsTeamBadgesVanillaIcons;

        private static readonly int[] StatisticsSortRankingIndices =
        {
            -1, 0, 1, 10, 3, 2, 5, 6, 7, 8, 4, 11, 12, 13, 14, 15
        };

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

        internal static int ResolvePresentedGameOverState(
            int state,
            bool spectatorPromotionRequested) =>
            state == 1 && spectatorPromotionRequested ? 2 : state;

        internal static bool TryBuildStatisticsRowPlayerIds(
            int[] validPlayers,
            int[] ranking,
            int[][] individualRanking,
            int sortType,
            bool sortReversed,
            int[] rowPlayerIds)
        {
            if (rowPlayerIds == null || rowPlayerIds.Length < 8)
                return false;

            for (int row = 0; row < 8; row++)
                rowPlayerIds[row] = 0;

            if (validPlayers == null || validPlayers.Length < 9 ||
                ranking == null || ranking.Length < 9)
            {
                return false;
            }

            int[] selectedRanking = ranking;
            if (sortType >= 1 && sortType < StatisticsSortRankingIndices.Length)
            {
                int rankingIndex = StatisticsSortRankingIndices[sortType];
                if (individualRanking == null ||
                    rankingIndex < 0 ||
                    rankingIndex >= individualRanking.Length ||
                    individualRanking[rankingIndex] == null ||
                    individualRanking[rankingIndex].Length < 9)
                {
                    return false;
                }

                selectedRanking = individualRanking[rankingIndex];
            }

            int visibleRow = 0;
            for (int ordinal = 1; ordinal <= 8; ordinal++)
            {
                int selectedIndex = sortReversed ? 9 - ordinal : ordinal;
                int playerId = selectedRanking[selectedIndex];
                if (playerId < 1 || playerId > 8)
                {
                    for (int row = 0; row < 8; row++)
                        rowPlayerIds[row] = 0;
                    return false;
                }

                if (validPlayers[playerId] <= 0)
                    continue;

                rowPlayerIds[visibleRow++] = playerId;
            }

            return true;
        }

        internal static int ResolveStatisticsTeamShield(int playerId, int[] teamShields)
        {
            if (playerId < 1 || playerId > 8 ||
                teamShields == null || teamShields.Length < 9)
            {
                return 0;
            }

            int teamId = teamShields[playerId];
            return teamId >= 1 && teamId <= 4 ? teamId : 0;
        }

        internal static int NormalizeStatisticsTeamBadgeMode(int mode) =>
            mode >= StatisticsTeamBadgesOff && mode <= StatisticsTeamBadgesEasyReadIcons
                ? mode
                : DefaultStatisticsTeamBadgeMode;

        internal static bool TryResolveStatisticsTeamBadgeStyle(
            int teamId,
            out StatisticsTeamBadgeStyle style)
        {
            switch (teamId)
            {
                case 1:
                    style = new StatisticsTeamBadgeStyle(204, 80, 80, useDarkText: false);
                    return true;
                case 2:
                    style = new StatisticsTeamBadgeStyle(204, 204, 80, useDarkText: true);
                    return true;
                case 3:
                    style = new StatisticsTeamBadgeStyle(80, 140, 204, useDarkText: false);
                    return true;
                case 4:
                    style = new StatisticsTeamBadgeStyle(80, 204, 80, useDarkText: false);
                    return true;
                default:
                    style = default;
                    return false;
            }
        }

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

        internal static bool IsChoreDelivery(bool senderSteamIdPresent) =>
            !senderSteamIdPresent;

        internal static bool CanExecute(
            int packetPlayerId,
            SurrenderLordSnapshot currentLord,
            int resolvedUnitId) =>
            packetPlayerId >= 1 && packetPlayerId <= 8 &&
            IsValidLord(currentLord) &&
            currentLord.PlayerId == packetPlayerId &&
            resolvedUnitId == currentLord.UnitId;
    }
}
