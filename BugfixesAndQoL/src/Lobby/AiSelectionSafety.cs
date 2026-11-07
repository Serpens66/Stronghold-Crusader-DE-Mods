using BepInEx.Logging;
using CrusaderDE;
using System;
using System.Reflection;

namespace BugfixesAndQoL
{
    // Used only by existing Unity-thread dialog and new-game hooks. No save-load hook,
    // polling or client-side replacement of the host's transmitted selection.
    internal static class AiSelectionSafety
    {
        private static readonly Shared.LogRateGate repairLogGate = new Shared.LogRateGate();
        private static readonly MethodInfo updateHostInfo = typeof(FRONT_Multiplayer).GetMethod(
            "UpdateHostInfo", BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(bool) }, null);

        internal static bool CanEdit(FRONT_Multiplayer frontend) => frontend != null &&
            (FRONT_Multiplayer.skirmishGame || frontend.singlePlayerCoop || frontend.currentLobby?.isHost == true);

        internal static bool TryPrepare(FRONT_Multiplayer.MPAIVInfo info, ManualLogSource log,
            string reason, bool allowRepair, out string failure)
        {
            failure = null;
            if (info == null) { failure = "Missing selection data."; return false; }
            try
            {
                var manager = CustomisationFileManager.Instance;
                var aivs = !string.IsNullOrEmpty(info.lordName)
                    ? manager.getLordAIVList(-1, info.lordName) : manager.getLordAIVList(info.lordType);
                var aics = !string.IsNullOrEmpty(info.lordName)
                    ? manager.getLordLordList(-1, info.lordName) : manager.getLordLordList(info.lordType);
                var candidate = AiSelectionRepair.Copy(info);
                if (!AiSelectionRepair.TryRepair(candidate, aivs, aics, out var repair))
                { failure = repair.Failure; return false; }
                if (!repair.Changed) return true;
                if (!allowRepair)
                { failure = "The authoritative selection is incomplete or changed after synchronization."; return false; }
                AiSelectionRepair.CopySelection(candidate, info);
                if (repairLogGate.TryReserve("ai-selection-repair", 10, out long suppressed))
                    Shared.DebugLogHelper.LogWarningLimited(log, "ai-selection-repair",
                        $"Repaired AI selection: lord={info.lordName}, type={info.lordType}, " +
                        $"castleFallback={repair.CastleFallback}, aicFallback={repair.AicFallback}, " +
                        $"reason={reason}, suppressed={suppressed}.");
                return true;
            }
            catch (Exception ex) { failure = ex.Message; return false; }
        }

        internal static bool TryPrepareLobby(FRONT_Multiplayer frontend, ManualLogSource log,
            BugfixesAndQoLViewModel settings, string reason, bool allowRepair = true)
        {
            if (!settings.EnableMod) return true;
            if (frontend == null)
            { ShowFailure(log, string.Empty, 0, "Lobby selection data is unavailable."); return false; }
            if (!CanEdit(frontend)) return true;
            int playerId = 0;
            string lord = string.Empty;
            try
            {
                var lobby = frontend.currentLobby;
                if (lobby?.members == null || frontend.AIVs?.Length != 8)
                    throw new InvalidOperationException("Lobby selection data is unavailable.");
                foreach (var member in lobby.members)
                {
                    if (member == null || !member.SkirmishMember || member.SkirmishHumanMember) continue;
                    playerId = lobby.getThisPlayerFromSteamID(member.GetSteamID());
                    lord = member.customLordName ?? string.Empty;
                    if (playerId < 1 || playerId > frontend.AIVs.Length)
                        throw new InvalidOperationException("Invalid player mapping.");
                    var info = frontend.AIVs[playerId - 1];
                    if (info == null || !string.Equals(lord, info.lordName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Lobby Lord and selection identity disagree.");
                    if (info.lordType != member.GetLordType())
                    {
                        if (!allowRepair) throw new InvalidOperationException("Lord type changed after synchronization.");
                        info.lordType = member.GetLordType();
                    }
                    if (!TryPrepare(info, log, reason, allowRepair, out string failure))
                        throw new InvalidOperationException(failure);
                }
                // Before GO!/manifest preparation, publish repaired flags, candidate zero and AIC
                // through Vanilla's existing lobby channel. Never publish after synchronization.
                if (allowRepair && !FRONT_Multiplayer.skirmishGame && !frontend.singlePlayerCoop)
                    (updateHostInfo ?? throw new MissingMethodException("FRONT_Multiplayer.UpdateHostInfo"))
                        .Invoke(frontend, new object[] { false });
                return true;
            }
            catch (Exception ex)
            {
                ShowFailure(log, lord, playerId, ex.GetBaseException().Message);
                return false;
            }
        }

        internal static void ShowFailure(ManualLogSource log, string lord, int playerId, string failure)
        {
            Shared.DebugLogHelper.LogErrorLimited(log, "ai-selection-invalid",
                $"AI selection blocked: lord={lord}, player={playerId}, reason={failure}.");
            string text = SerpLocalization.Get("BugfixesAndQoL.AiSelectionInvalid")
                .Replace("{Lord}", lord ?? string.Empty).Replace("{Player}", playerId.ToString())
                .Replace("{Reason}", failure ?? string.Empty);
            if (!MainViewModel.viewModelLoaded) return;
            HUD_ConfirmationPopup.ShowConfirmationOKMessage(
                SerpLocalization.Get("BugfixesAndQoL.AiSelectionTitle"), () => { }, text);
            MainViewModel.Instance.Show_HUD_Confirmation = false;
            MainViewModel.Instance.Show_HUD_ConfirmationMP = true;
        }
    }
}
