// Feature: Offer validated Steam-friend lobby invitations directly inside the game.
using BepInEx.Logging;
using CrusaderDE;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace BugfixesAndQoL
{
    internal sealed class SteamLobbyInvitePrompt
    {
        private const int MaximumPendingInvites = 32;
        private static readonly long ValidationTimeoutTicks = Stopwatch.Frequency * 20L;
        private static readonly long DuplicateWindowTicks = Stopwatch.Frequency * 60L;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly SteamInviteBlacklistStore blacklist;
        private readonly object initializationRoot = new object();
        private readonly object syncRoot = new object();
        private readonly Dictionary<string, PendingInvite> pendingInvites =
            new Dictionary<string, PendingInvite>(StringComparer.Ordinal);
        private readonly Dictionary<string, long> recentInvites =
            new Dictionary<string, long>(StringComparer.Ordinal);
        private Callback<LobbyInvite_t> lobbyInviteCallback;
        private Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;
        private Timer validationTimer;
        private long inviteSequence;

        internal SteamLobbyInvitePrompt(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            SteamInviteBlacklistStore blacklist)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.blacklist = blacklist ?? throw new ArgumentNullException(nameof(blacklist));
        }

        internal void TryInitialize()
        {
            if (!SteamManager.Initialized)
                return;

            lock (initializationRoot)
            {
                if (lobbyInviteCallback != null && lobbyDataUpdateCallback != null && validationTimer != null)
                    return;

                // Initialize each missing listener independently so a later retry can repair a partial failure.
                // These listeners only observe Steam state for the mod popup; they do not consume or alter invites.
                if (lobbyInviteCallback == null)
                    lobbyInviteCallback = Callback<LobbyInvite_t>.Create(OnLobbyInvite);
                if (lobbyDataUpdateCallback == null)
                    lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
                if (validationTimer == null)
                    validationTimer = new Timer(ExpirePendingInvites, null, 5000, 5000);

                Shared.DebugLogHelper.LogDebug(log, "Steam lobby-invite popup validation callbacks registered.");
            }
        }

        private void OnLobbyInvite(LobbyInvite_t invite)
        {
            SteamInviteContext context = SteamInviteContext.From(invite);
            try
            {
                SteamInviteRejectionReason reason = Validate(context, out string detail);
                if (reason != SteamInviteRejectionReason.None)
                {
                    LogBlocked(context, "InitialValidation", reason, detail);
                    return;
                }

                string key = context.Key;
                long now = Stopwatch.GetTimestamp();
                lock (syncRoot)
                {
                    RemoveExpiredRecentLocked(now);
                    if (pendingInvites.ContainsKey(key) ||
                        (recentInvites.TryGetValue(key, out long recentUntil) && recentUntil > now))
                    {
                        LogBlocked(context, "Queue", SteamInviteRejectionReason.Duplicate, string.Empty);
                        return;
                    }
                    if (pendingInvites.Count >= MaximumPendingInvites)
                    {
                        LogBlocked(context, "Queue", SteamInviteRejectionReason.PendingLimit,
                            "pendingCount=" + pendingInvites.Count.ToString(CultureInfo.InvariantCulture));
                        return;
                    }

                    pendingInvites.Add(key, new PendingInvite(context, now));
                    recentInvites[key] = now + DuplicateWindowTicks;
                }

                bool requestStarted;
                try
                {
                    requestStarted = SteamMatchmaking.RequestLobbyData(new CSteamID(context.LobbyId));
                }
                catch (Exception ex)
                {
                    RemovePending(key, removeRecent: true);
                    LogBlocked(context, "LobbyRequest", SteamInviteRejectionReason.ValidationException, ex.Message);
                    return;
                }

                if (!requestStarted)
                {
                    RemovePending(key, removeRecent: true);
                    LogBlocked(context, "LobbyRequest", SteamInviteRejectionReason.LobbyRequestNotStarted, string.Empty);
                    return;
                }

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Validating Steam lobby invitation: inviterId={context.InviterId}, lobbyId={context.LobbyId}, " +
                    $"gameId={context.GameId}, inviteAppId={context.InviteAppId}, currentAppId={context.CurrentAppId}.");
            }
            catch (Exception ex)
            {
                LogBlocked(context, "InitialValidation", SteamInviteRejectionReason.ValidationException, ex.Message);
            }
        }

        private void OnLobbyDataUpdate(LobbyDataUpdate_t update)
        {
            // Member-data notifications are unrelated to RequestLobbyData. Leave pending popup checks intact
            // until Steam sends the lobby-level result or the local validation timeout expires.
            if (!SteamLobbyInvitePolicy.IsLobbyMetadataUpdate(
                    update.m_ulSteamIDLobby,
                    update.m_ulSteamIDMember))
                return;

            List<PendingInvite> matching = TakePendingForLobby(update.m_ulSteamIDLobby);
            if (matching.Count == 0)
                return;

            foreach (PendingInvite pending in matching)
            {
                SteamInviteContext context = pending.Context;
                try
                {
                    if (update.m_bSuccess == 0)
                    {
                        RemoveRecent(context.Key);
                        LogBlocked(context, "LobbyResponse", SteamInviteRejectionReason.LobbyDataFailed, string.Empty);
                        continue;
                    }

                    SteamInviteRejectionReason reason = Validate(context, out string detail);
                    if (reason != SteamInviteRejectionReason.None)
                    {
                        RemoveRecent(context.Key);
                        LogBlocked(context, "PrePromptValidation", reason, detail);
                        continue;
                    }
                    if (!IsSafeToShowPrompt(out string promptState))
                    {
                        RemoveRecent(context.Key);
                        LogBlocked(
                            context,
                            "PrePromptValidation",
                            SteamInviteRejectionReason.UnsafeUiState,
                            promptState);
                        continue;
                    }

                    ShowPrompt(context);
                }
                catch (Exception ex)
                {
                    RemoveRecent(context.Key);
                    LogBlocked(context, "PrePromptValidation", SteamInviteRejectionReason.ValidationException, ex.Message);
                }
            }
        }

        private SteamInviteRejectionReason Validate(SteamInviteContext context, out string detail)
        {
            CSteamID inviter = new CSteamID(context.InviterId);
            CSteamID lobby = new CSteamID(context.LobbyId);
            CSteamID localUser = SteamUser.GetSteamID();
            CGameID game = new CGameID(context.GameId);
            EFriendRelationship relationship = inviter.IsValid() && inviter.BIndividualAccount()
                ? SteamFriends.GetFriendRelationship(inviter)
                : EFriendRelationship.k_EFriendRelationshipNone;
            context.CurrentAppId = SteamUtils.GetAppID().m_AppId;
            context.InviteAppId = game.AppID().m_AppId;

            var input = new SteamInviteValidationInput
            {
                ClientFeaturesEnabled = settings.EnableClientFeatures,
                PromptEnabled = settings.EnableIngameSteamInvitePrompt,
                InviterIdValid = inviter.IsValid() && inviter.BIndividualAccount(),
                LobbyIdValid = lobby.IsValid() && lobby.IsLobby(),
                GameIdValid = game.IsValid() && game.IsSteamApp(),
                InviteAppId = context.InviteAppId,
                CurrentAppId = context.CurrentAppId,
                IsSelfInvite = localUser.IsValid() && localUser == inviter,
                Relationship = MapRelationship(relationship),
                BlacklistUsable = blacklist.IsUsable,
                LocallyBlacklisted = blacklist.Contains(context.InviterId),
            };
            SteamInviteRejectionReason reason = SteamLobbyInvitePolicy.Validate(input);
            detail = reason == SteamInviteRejectionReason.BlacklistUnavailable
                ? blacklist.LoadError
                : "relationship=" + relationship;
            return reason;
        }

        private static SteamInviteRelationshipKind MapRelationship(EFriendRelationship relationship)
        {
            switch (relationship)
            {
                case EFriendRelationship.k_EFriendRelationshipFriend:
                    return SteamInviteRelationshipKind.Friend;
                case EFriendRelationship.k_EFriendRelationshipBlocked:
                case EFriendRelationship.k_EFriendRelationshipIgnored:
                case EFriendRelationship.k_EFriendRelationshipIgnoredFriend:
                    return SteamInviteRelationshipKind.BlockedOrIgnored;
                default:
                    return SteamInviteRelationshipKind.Other;
            }
        }

        private void ShowPrompt(SteamInviteContext context)
        {
            string inviterName = ResolveInviterName(context.InviterId);
            string format = SerpLocalization.Get("BugfixesAndQoL.SteamInvitePrompt");
            string message = string.Format(CultureInfo.CurrentCulture, format, inviterName);
            string neverAskAgain = SerpLocalization.Get("BugfixesAndQoL.SteamInviteNeverAskAgain");
            long sequence = ++inviteSequence;
            bool blockInviter = false;
            bool useMultiplayerConfirmation = MainViewModel.Instance.Show_MultiplayerSetup;

            HUD_ConfirmationPopup.ShowConfirmationCheck(
                message,
                () => AcceptInvite(sequence, context),
                () => DeclineInvite(sequence, context, blockInviter),
                neverAskAgain,
                initialCheckState: false,
                _checkChangeAction: value => blockInviter = value,
                MPConf: useMultiplayerConfirmation);

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Displayed validated Steam lobby invitation from '{inviterName}' ({context.InviterId}) " +
                $"for lobby {context.LobbyId}; confirmationSurface=" +
                $"{(useMultiplayerConfirmation ? "multiplayer" : "standard")}.");
        }

        private static bool IsSafeToShowPrompt(out string state)
        {
            if (!MainViewModel.viewModelLoaded)
            {
                state = "viewModelLoaded=False";
                return false;
            }

            MainViewModel viewModel = MainViewModel.Instance;
            Enums.SceneIDS scene = FatControler.currentScene;
            bool confirmationOpen =
                viewModel.Show_HUD_Confirmation ||
                viewModel.Show_HUD_ConfirmationMP ||
                viewModel.Show_HUD_ConfirmationSM ||
                viewModel.Show_HUD_ConfirmationSands;
            bool simulationRunning = Director.instance != null && Director.instance.SimRunning;
            bool gameStateReady = GameData.Instance != null && GameData.Instance.lastGameState != null;

            state =
                $"scene={scene}, screen={viewModel.CurrentScreenNo}, showFrontend={viewModel.Show_Frontend}, showInGame={viewModel.Show_InGame}, " +
                $"showMultiplayerSetup={viewModel.Show_MultiplayerSetup}, missionOver={viewModel.Show_HUD_MissionOver}, " +
                $"loadingBlack={viewModel.Show_MP_LoadingBlack}, briefing={viewModel.Show_HUD_Briefing}, " +
                $"confirmationOpen={confirmationOpen}, simRunning={simulationRunning}, gameStateReady={gameStateReady}, " +
                $"mainUiLoaded={viewModel.MainUILoaded}, radarLoaded={viewModel.RadarLoaded}";

            if (confirmationOpen || viewModel.Show_MP_LoadingBlack)
                return false;
            if (viewModel.Show_Frontend)
                return !viewModel.Show_InGame;
            if (viewModel.Show_HUD_MissionOver)
                return viewModel.Show_InGame && viewModel.MainUILoaded;
            if (scene != Enums.SceneIDS.ActualMainGame)
                return false;
            return viewModel.Show_InGame &&
                   !viewModel.Show_Frontend &&
                   !viewModel.Show_HUD_Briefing &&
                   simulationRunning &&
                   gameStateReady &&
                   viewModel.MainUILoaded &&
                   viewModel.RadarLoaded;
        }

        private string ResolveInviterName(ulong inviterId)
        {
            string name = SteamFriends.GetFriendPersonaName(new CSteamID(inviterId));
            return !string.IsNullOrWhiteSpace(name) && name != "[unknown]"
                ? name
                : SerpLocalization.Get("BugfixesAndQoL.UnknownSteamUser");
        }

        private void AcceptInvite(long sequence, SteamInviteContext context)
        {
            if (sequence != inviteSequence)
                return;

            try
            {
                Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
                if (multiplayer == null)
                    throw new InvalidOperationException("Vanilla multiplayer manager is unavailable.");

                multiplayer.SetInviteLobbyID(context.LobbyId);
                multiplayer.ResumeInvite();
                if (FatControler.currentScene == Enums.SceneIDS.ActualMainGame &&
                    Director.instance != null && Director.instance.SimRunning)
                    MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Accepted validated Steam lobby invitation from {context.InviterId} for lobby {context.LobbyId} through Vanilla's join flow.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Accepting validated Steam lobby invitation from {context.InviterId} for lobby {context.LobbyId} failed; " +
                    $"no custom fallback join was attempted: {ex}");
            }
        }

        private void DeclineInvite(long sequence, SteamInviteContext context, bool blockInviter)
        {
            if (sequence != inviteSequence)
                return;

            if (blockInviter)
            {
                if (blacklist.TryAdd(context.InviterId, out string error))
                {
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Dismissed the in-game Steam lobby-invite popup and added inviter {context.InviterId} to the local popup blacklist.");
                    return;
                }

                Shared.DebugLogHelper.LogError(
                    log,
                    $"Dismissed the in-game Steam lobby-invite popup, but inviter {context.InviterId} could not be added to the local popup blacklist: {error}");
                return;
            }

            Shared.DebugLogHelper.LogDebug(log, "Dismissed the validated in-game Steam lobby-invite popup; the Steam invitation was not changed.");
        }

        private void ExpirePendingInvites(object state)
        {
            var expired = new List<PendingInvite>();
            long now = Stopwatch.GetTimestamp();
            lock (syncRoot)
            {
                var expiredKeys = new List<string>();
                foreach (KeyValuePair<string, PendingInvite> entry in pendingInvites)
                {
                    if (now - entry.Value.StartedAt >= ValidationTimeoutTicks)
                    {
                        expiredKeys.Add(entry.Key);
                        expired.Add(entry.Value);
                    }
                }
                foreach (string key in expiredKeys)
                {
                    pendingInvites.Remove(key);
                    recentInvites.Remove(key);
                }
                RemoveExpiredRecentLocked(now);
            }

            foreach (PendingInvite pending in expired)
                LogBlocked(pending.Context, "LobbyResponse", SteamInviteRejectionReason.LobbyValidationExpired, string.Empty);
        }

        private List<PendingInvite> TakePendingForLobby(ulong lobbyId)
        {
            var matching = new List<PendingInvite>();
            lock (syncRoot)
            {
                var keys = new List<string>();
                foreach (KeyValuePair<string, PendingInvite> entry in pendingInvites)
                {
                    if (entry.Value.Context.LobbyId == lobbyId)
                    {
                        keys.Add(entry.Key);
                        matching.Add(entry.Value);
                    }
                }
                foreach (string key in keys)
                    pendingInvites.Remove(key);
            }
            return matching;
        }

        private void RemovePending(string key, bool removeRecent)
        {
            lock (syncRoot)
            {
                pendingInvites.Remove(key);
                if (removeRecent)
                    recentInvites.Remove(key);
            }
        }

        private void RemoveRecent(string key)
        {
            lock (syncRoot)
                recentInvites.Remove(key);
        }

        private void RemoveExpiredRecentLocked(long now)
        {
            var keys = new List<string>();
            foreach (KeyValuePair<string, long> entry in recentInvites)
            {
                if (entry.Value <= now)
                    keys.Add(entry.Key);
            }
            foreach (string key in keys)
                recentInvites.Remove(key);
        }

        private void LogBlocked(
            SteamInviteContext context,
            string phase,
            SteamInviteRejectionReason reason,
            string detail)
        {
            Shared.DebugLogHelper.LogWarning(
                log,
                SteamLobbyInvitePolicy.FormatWarning(
                    reason,
                    phase,
                    context.InviterId,
                    context.LobbyId,
                    context.GameId,
                    context.InviteAppId,
                    context.CurrentAppId,
                    detail));
        }

        private sealed class PendingInvite
        {
            internal PendingInvite(SteamInviteContext context, long startedAt)
            {
                Context = context;
                StartedAt = startedAt;
            }

            internal SteamInviteContext Context { get; }
            internal long StartedAt { get; }
        }

        private sealed class SteamInviteContext
        {
            internal ulong InviterId { get; private set; }
            internal ulong LobbyId { get; private set; }
            internal ulong GameId { get; private set; }
            internal uint InviteAppId { get; set; }
            internal uint CurrentAppId { get; set; }
            internal string Key => InviterId.ToString(CultureInfo.InvariantCulture) + ":" +
                                   LobbyId.ToString(CultureInfo.InvariantCulture);

            internal static SteamInviteContext From(LobbyInvite_t invite) => new SteamInviteContext
            {
                InviterId = invite.m_ulSteamIDUser,
                LobbyId = invite.m_ulSteamIDLobby,
                GameId = invite.m_ulGameID,
            };
        }
    }
}
