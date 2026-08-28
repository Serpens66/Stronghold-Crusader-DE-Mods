// Feature: Keep Steam invite validation deterministic and independently testable.
using System.Globalization;

namespace BugfixesAndQoL
{
    internal enum SteamInviteRelationshipKind
    {
        Other,
        Friend,
        BlockedOrIgnored,
    }

    internal enum SteamInviteRejectionReason
    {
        None,
        ClientFeaturesDisabled,
        PromptDisabled,
        InvalidInviterId,
        InvalidLobbyId,
        InvalidGameId,
        WrongApp,
        SelfInvite,
        SteamBlocked,
        NotFriend,
        BlacklistUnavailable,
        LocallyBlacklisted,
        Duplicate,
        PendingLimit,
        LobbyRequestNotStarted,
        LobbyDataMismatch,
        LobbyDataFailed,
        LobbyValidationExpired,
        UnsafeUiState,
        ValidationException,
    }

    internal struct SteamInviteValidationInput
    {
        internal bool ClientFeaturesEnabled { get; set; }
        internal bool PromptEnabled { get; set; }
        internal bool InviterIdValid { get; set; }
        internal bool LobbyIdValid { get; set; }
        internal bool GameIdValid { get; set; }
        internal uint InviteAppId { get; set; }
        internal uint CurrentAppId { get; set; }
        internal bool IsSelfInvite { get; set; }
        internal SteamInviteRelationshipKind Relationship { get; set; }
        internal bool BlacklistUsable { get; set; }
        internal bool LocallyBlacklisted { get; set; }
    }

    internal static class SteamLobbyInvitePolicy
    {
        internal static bool IsLobbyMetadataUpdate(ulong lobbyId, ulong memberId) =>
            lobbyId != 0 && memberId == lobbyId;

        internal static SteamInviteRejectionReason Validate(in SteamInviteValidationInput input)
        {
            if (!input.ClientFeaturesEnabled)
                return SteamInviteRejectionReason.ClientFeaturesDisabled;
            if (!input.PromptEnabled)
                return SteamInviteRejectionReason.PromptDisabled;
            if (!input.InviterIdValid)
                return SteamInviteRejectionReason.InvalidInviterId;
            if (!input.LobbyIdValid)
                return SteamInviteRejectionReason.InvalidLobbyId;
            if (!input.GameIdValid)
                return SteamInviteRejectionReason.InvalidGameId;
            if (input.InviteAppId != input.CurrentAppId)
                return SteamInviteRejectionReason.WrongApp;
            if (input.IsSelfInvite)
                return SteamInviteRejectionReason.SelfInvite;
            if (input.Relationship == SteamInviteRelationshipKind.BlockedOrIgnored)
                return SteamInviteRejectionReason.SteamBlocked;
            if (input.Relationship != SteamInviteRelationshipKind.Friend)
                return SteamInviteRejectionReason.NotFriend;
            if (!input.BlacklistUsable)
                return SteamInviteRejectionReason.BlacklistUnavailable;
            if (input.LocallyBlacklisted)
                return SteamInviteRejectionReason.LocallyBlacklisted;
            return SteamInviteRejectionReason.None;
        }

        internal static string Describe(SteamInviteRejectionReason reason)
        {
            switch (reason)
            {
                case SteamInviteRejectionReason.ClientFeaturesDisabled:
                    return "local client features are disabled";
                case SteamInviteRejectionReason.PromptDisabled:
                    return "the in-game Steam invite prompt is disabled";
                case SteamInviteRejectionReason.InvalidInviterId:
                    return "the inviter Steam ID is not a valid individual account";
                case SteamInviteRejectionReason.InvalidLobbyId:
                    return "the lobby Steam ID is not a valid lobby account";
                case SteamInviteRejectionReason.InvalidGameId:
                    return "the invitation game ID is invalid or is not a Steam app";
                case SteamInviteRejectionReason.WrongApp:
                    return "the invitation belongs to a different Steam app";
                case SteamInviteRejectionReason.SelfInvite:
                    return "the invitation identifies the local Steam user as inviter";
                case SteamInviteRejectionReason.SteamBlocked:
                    return "Steam reports a blocked or ignored relationship";
                case SteamInviteRejectionReason.NotFriend:
                    return "the inviter is not currently a Steam friend";
                case SteamInviteRejectionReason.BlacklistUnavailable:
                    return "the local invite blacklist is unavailable or invalid";
                case SteamInviteRejectionReason.LocallyBlacklisted:
                    return "the inviter is on the local invite blacklist";
                case SteamInviteRejectionReason.Duplicate:
                    return "the same invitation is already pending or was handled recently";
                case SteamInviteRejectionReason.PendingLimit:
                    return "the pending invitation validation limit was reached";
                case SteamInviteRejectionReason.LobbyRequestNotStarted:
                    return "Steam did not start the lobby-data request";
                case SteamInviteRejectionReason.LobbyDataMismatch:
                    return "Steam returned lobby data for an unexpected member";
                case SteamInviteRejectionReason.LobbyDataFailed:
                    return "Steam could not resolve the invited lobby";
                case SteamInviteRejectionReason.LobbyValidationExpired:
                    return "Steam did not validate the invited lobby before the timeout";
                case SteamInviteRejectionReason.UnsafeUiState:
                    return "the game UI is not in a safe state for this prompt";
                case SteamInviteRejectionReason.ValidationException:
                    return "invite validation raised an exception";
                default:
                    return "no rejection";
            }
        }

        internal static string FormatWarning(
            SteamInviteRejectionReason reason,
            string phase,
            ulong inviterId,
            ulong lobbyId,
            ulong gameId,
            uint inviteAppId,
            uint currentAppId,
            string detail)
        {
            string safeDetail = (detail ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Replace('\'', ' ');
            return $"Suppressed in-game Steam lobby-invite popup: reason={reason}, phase={phase}, " +
                   $"inviterId={inviterId.ToString(CultureInfo.InvariantCulture)}, " +
                   $"lobbyId={lobbyId.ToString(CultureInfo.InvariantCulture)}, " +
                   $"gameId={gameId.ToString(CultureInfo.InvariantCulture)}, " +
                   $"inviteAppId={inviteAppId.ToString(CultureInfo.InvariantCulture)}, " +
                   $"currentAppId={currentAppId.ToString(CultureInfo.InvariantCulture)}, " +
                   $"description='{Describe(reason)}', detail='{safeDetail}'.";
        }
    }
}
