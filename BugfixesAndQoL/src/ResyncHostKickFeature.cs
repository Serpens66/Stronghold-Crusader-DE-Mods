using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class ResyncHostKickFeature : IDisposable
    {
        private delegate void ResyncUpdateDelegate(HUD_MPResync self);
        private delegate void ConnectionIssueShowDelegate(HUD_MPConnectionIssue self, string message, bool kickNotLeave, int playerId);
        private delegate void ConnectionIssueButtonDelegate(HUD_MPConnectionIssue self);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly MethodInfo forcedHostKickMethod;
        private readonly Hook resyncUpdateHook;
        private readonly Hook connectionIssueShowHook;
        private readonly Hook connectionIssueButtonHook;
        private readonly ResyncUpdateDelegate resyncUpdateOriginal;
        private readonly ConnectionIssueShowDelegate connectionIssueShowOriginal;
        private readonly ConnectionIssueButtonDelegate connectionIssueButtonOriginal;
        private int targetPlayerId = -1;
        private string targetPlayerName = string.Empty;
        private bool targetFromResync;
        private long promptSequence;
        private bool disposed;

        internal ResyncHostKickFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            forcedHostKickMethod = typeof(Platform_Multiplayer).GetMethod(
                "kickPlayerFromGame",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(Platform_Multiplayer.MPGameMember), typeof(bool) },
                null);
            if (forcedHostKickMethod == null || forcedHostKickMethod.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(Platform_Multiplayer).FullName, "kickPlayerFromGame(MPGameMember, bool)");

            resyncUpdateHook = new Hook(FindPublicInstanceMethod(typeof(HUD_MPResync), "Update", Type.EmptyTypes), (ResyncUpdateDelegate)ResyncUpdateHook);
            resyncUpdateOriginal = resyncUpdateHook.GenerateTrampoline<ResyncUpdateDelegate>();
            connectionIssueShowHook = new Hook(
                FindPublicInstanceMethod(typeof(HUD_MPConnectionIssue), "ShowMultiplayerConnectionError", new[] { typeof(string), typeof(bool), typeof(int) }),
                (ConnectionIssueShowDelegate)ConnectionIssueShowHook);
            connectionIssueShowOriginal = connectionIssueShowHook.GenerateTrampoline<ConnectionIssueShowDelegate>();
            connectionIssueButtonHook = new Hook(
                FindPublicInstanceMethod(typeof(HUD_MPConnectionIssue), "ButtonClicked", Type.EmptyTypes),
                (ConnectionIssueButtonDelegate)ConnectionIssueButtonHook);
            connectionIssueButtonOriginal = connectionIssueButtonHook.GenerateTrampoline<ConnectionIssueButtonDelegate>();

            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL resync host-kick hooks installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            connectionIssueButtonHook?.Undo();
            connectionIssueButtonHook?.Dispose();
            connectionIssueShowHook?.Undo();
            connectionIssueShowHook?.Dispose();
            resyncUpdateHook?.Undo();
            resyncUpdateHook?.Dispose();
            ClearTarget(clearViewModel: true);
        }

        private static MethodInfo FindPublicInstanceMethod(Type type, string name, Type[] parameters)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public, null, parameters, null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(type.FullName, name);
            return method;
        }

        private bool FeatureEnabled => settings.EnableMod && settings.EnableResyncHostKick;

        private void ResyncUpdateHook(HUD_MPResync self)
        {
            resyncUpdateOriginal(self);

            try
            {
                Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
                if (!FeatureEnabled ||
                    multiplayer == null ||
                    !Platform_Multiplayer.MPGameActive ||
                    !multiplayer.resyncing ||
                    !GameNetworkAPI.IsLocalHost())
                {
                    if (targetFromResync)
                        ClearTarget(clearViewModel: true);
                    return;
                }

                if (!TrySelectStaleMember(multiplayer, DateTime.UtcNow, out Platform_Multiplayer.MPGameMember member))
                {
                    if (targetFromResync)
                        ClearTarget(clearViewModel: true);
                    return;
                }

                SetTarget(member, fromResync: true);
                MainViewModel.Instance.MPConnectionIssueText = SerpLocalization.Get(
                    "BugfixesAndQoL.ResyncProblemPlayer",
                    "PlayerName", member.playerName ?? string.Empty);
                MainViewModel.Instance.MPConnectionIssueButtonText =
                    Translate.Instance.lookUpText(Enums.eTextSections.TEXT_NEW_TEXT, 42);
                MainViewModel.Instance.MPConectionIssueButtonVisible = true;
            }
            catch (Exception ex)
            {
                ClearTarget(clearViewModel: true);
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL resync host-kick update failed closed: {ex}");
            }
        }

        private void ConnectionIssueShowHook(HUD_MPConnectionIssue self, string message, bool kickNotLeave, int playerId)
        {
            connectionIssueShowOriginal(self, message, kickNotLeave, playerId);

            try
            {
                Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
                Platform_Multiplayer.MPGameMember member = multiplayer?.getPlayer(playerId);
                if (FeatureEnabled &&
                    kickNotLeave &&
                    member != null &&
                    IsValidHumanTarget(member) &&
                    GameNetworkAPI.IsLocalHost())
                {
                    SetTarget(member, fromResync: false);
                    MainViewModel.Instance.MPConectionIssueButtonVisible = true;
                }
                else if (!targetFromResync)
                {
                    ClearTarget(clearViewModel: false);
                    MainViewModel.Instance.MPConectionIssueButtonVisible = false;
                }
            }
            catch (Exception ex)
            {
                ClearTarget(clearViewModel: false);
                MainViewModel.Instance.MPConectionIssueButtonVisible = false;
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL connection-issue host-kick setup failed closed: {ex}");
            }
        }

        private void ConnectionIssueButtonHook(HUD_MPConnectionIssue self)
        {
            if (targetPlayerId <= 0)
            {
                connectionIssueButtonOriginal(self);
                return;
            }

            try
            {
                if (!TryResolveValidatedTarget(DateTime.UtcNow, out Platform_Multiplayer.MPGameMember member))
                {
                    ClearTarget(clearViewModel: true);
                    return;
                }

                int playerId = member.playerID;
                string playerName = member.playerName ?? targetPlayerName;
                long sequence = ++promptSequence;
                HUD_ConfirmationPopup.ShowConfirmationMessage(
                    SerpLocalization.Get("BugfixesAndQoL.ResyncKickConfirmationTitle"),
                    () => ConfirmKick(sequence, playerId, playerName),
                    () => CancelKick(sequence, playerId, playerName),
                    SerpLocalization.Get(
                        "BugfixesAndQoL.ResyncKickConfirmationMessage",
                        "PlayerName", playerName));
                Shared.DebugLogHelper.LogDebug(log, $"Displayed resync host-kick confirmation: playerId={playerId}, playerName={playerName}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL could not display the host-kick confirmation: {ex}");
            }
        }

        private void ConfirmKick(long sequence, int playerId, string playerName)
        {
            if (sequence != promptSequence || playerId != targetPlayerId)
                return;

            try
            {
                if (!TryResolveValidatedTarget(DateTime.UtcNow, out Platform_Multiplayer.MPGameMember member))
                {
                    Shared.DebugLogHelper.LogWarning(log, $"Cancelled confirmed host kick because the target recovered or became invalid: playerId={playerId}, playerName={playerName}.");
                    ClearTarget(clearViewModel: true);
                    return;
                }

                forcedHostKickMethod.Invoke(Platform_Multiplayer.Instance, new object[] { member, true });
                Shared.DebugLogHelper.LogDebug(log, $"Executed authoritative resync host kick: playerId={playerId}, playerName={playerName}.");
                ClearTarget(clearViewModel: true);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL authoritative host kick failed closed: playerId={playerId}, playerName={playerName}, error={ex}");
            }
        }

        private void CancelKick(long sequence, int playerId, string playerName)
        {
            if (sequence == promptSequence)
                Shared.DebugLogHelper.LogDebug(log, $"Cancelled resync host kick: playerId={playerId}, playerName={playerName}.");
        }

        private bool TryResolveValidatedTarget(DateTime now, out Platform_Multiplayer.MPGameMember member)
        {
            member = null;
            Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
            if (!FeatureEnabled ||
                multiplayer == null ||
                !Platform_Multiplayer.MPGameActive ||
                !GameNetworkAPI.IsLocalHost())
            {
                return false;
            }

            member = multiplayer.getPlayer(targetPlayerId);
            return IsValidHumanTarget(member) &&
                   member.lastTimePacketRecieved != DateTime.MaxValue &&
                   member.lastTimePacketRecieved < now - ResyncHostKickPolicy.HeartbeatTimeout;
        }

        private static bool TrySelectStaleMember(
            Platform_Multiplayer multiplayer,
            DateTime now,
            out Platform_Multiplayer.MPGameMember selectedMember)
        {
            selectedMember = null;
            if (multiplayer?.gameMembers == null)
                return false;

            List<ResyncHostKickCandidate> candidates = new List<ResyncHostKickCandidate>();
            foreach (Platform_Multiplayer.MPGameMember member in multiplayer.gameMembers)
            {
                candidates.Add(new ResyncHostKickCandidate(
                    member.playerID,
                    member.playerName,
                    member.lastTimePacketRecieved,
                    member.isSelf,
                    member.steamID > 1000 && !member.skirmishAI,
                    member.kicked));
            }

            if (!ResyncHostKickPolicy.TrySelect(candidates, now, out ResyncHostKickCandidate selected))
                return false;

            selectedMember = multiplayer.getPlayer(selected.PlayerId);
            return IsValidHumanTarget(selectedMember);
        }

        private static bool IsValidHumanTarget(Platform_Multiplayer.MPGameMember member) =>
            member != null &&
            member.playerID > 0 &&
            !member.isSelf &&
            !member.kicked &&
            member.steamID > 1000 &&
            !member.skirmishAI;

        private void SetTarget(Platform_Multiplayer.MPGameMember member, bool fromResync)
        {
            targetPlayerId = member.playerID;
            targetPlayerName = member.playerName ?? string.Empty;
            targetFromResync = fromResync;
        }

        private void ClearTarget(bool clearViewModel)
        {
            targetPlayerId = -1;
            targetPlayerName = string.Empty;
            targetFromResync = false;
            promptSequence++;
            if (clearViewModel && MainViewModel.viewModelLoaded)
            {
                MainViewModel.Instance.MPConnectionIssueText = string.Empty;
                MainViewModel.Instance.MPConectionIssueButtonVisible = false;
            }
        }
    }
}
