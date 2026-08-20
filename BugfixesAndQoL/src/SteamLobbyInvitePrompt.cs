// Feature: Offer incoming Steam lobby invitations directly inside the game.
using BepInEx.Logging;
using CrusaderDE;
using Steamworks;
using System;
using System.Globalization;

namespace BugfixesAndQoL
{
    internal sealed class SteamLobbyInvitePrompt
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private Callback<LobbyInvite_t> lobbyInviteCallback;
        private long inviteSequence;

        internal SteamLobbyInvitePrompt(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        internal void TryInitialize()
        {
            if (lobbyInviteCallback != null || !SteamManager.Initialized)
                return;

            // The static plugin reference retains this callback after BepInEx destroys its startup component.
            lobbyInviteCallback = Callback<LobbyInvite_t>.Create(OnLobbyInvite);
            Shared.DebugLogHelper.LogDebug(log, "Steam lobby-invite callback registered.");
        }

        private void OnLobbyInvite(LobbyInvite_t invite)
        {
            try
            {
                if (!settings.EnableClientFeatures || !settings.EnableIngameSteamInvitePrompt)
                    return;

                ulong lobbyId = invite.m_ulSteamIDLobby;
                ulong inviterId = invite.m_ulSteamIDUser;
                if (lobbyId == 0)
                {
                    Shared.DebugLogHelper.LogWarning(log, "Ignored an incoming Steam lobby invitation without a lobby ID.");
                    return;
                }

                string inviterName = ResolveInviterName(inviterId);
                string format = SerpLocalization.Get("BugfixesAndQoL.SteamInvitePrompt");
                string message = string.Format(CultureInfo.CurrentCulture, format, inviterName);
                long sequence = ++inviteSequence;

                HUD_ConfirmationPopup.ShowConfirmation(
                    message,
                    () => AcceptInvite(sequence, lobbyId),
                    () => DeclineInvite(sequence));

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Displayed in-game Steam lobby invitation from '{inviterName}' ({inviterId}) for lobby {lobbyId}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Could not display an incoming Steam lobby invitation; Steam's normal notification remains available: {ex}");
            }
        }

        private string ResolveInviterName(ulong inviterId)
        {
            if (inviterId != 0)
            {
                string name = SteamFriends.GetFriendPersonaName(new CSteamID(inviterId));
                if (!string.IsNullOrWhiteSpace(name) && name != "[unknown]")
                    return name;
            }

            return SerpLocalization.Get("BugfixesAndQoL.UnknownSteamUser");
        }

        private void AcceptInvite(long sequence, ulong lobbyId)
        {
            if (sequence != inviteSequence)
                return;

            try
            {
                Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
                if (multiplayer == null)
                    throw new InvalidOperationException("Vanilla multiplayer manager is unavailable.");

                // This is the complete body of Vanilla's private Steam join callback:
                // store the invited lobby and hand control to its normal resume logic.
                multiplayer.SetInviteLobbyID(lobbyId);
                multiplayer.ResumeInvite();

                if (FatControler.currentScene == Enums.SceneIDS.ActualMainGame &&
                    Director.instance != null &&
                    Director.instance.SimRunning)
                {
                    // Vanilla otherwise waits for a manual menu exit. This invokes that same guarded exit path.
                    MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
                }

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Accepted Steam lobby invitation for lobby {lobbyId} through Vanilla's join flow.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Accepting Steam lobby invitation for lobby {lobbyId} failed; no custom fallback join was attempted: {ex}");
            }
        }

        private void DeclineInvite(long sequence)
        {
            if (sequence != inviteSequence)
                return;

            Shared.DebugLogHelper.LogDebug(log, "Declined in-game Steam lobby invitation.");
        }
    }
}
