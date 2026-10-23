using APIShared;
using CrusaderDE;
using System;
using System.Collections.Generic;

namespace ExtendedData
{
    // Standalone Coop AI start for packaged missions. Vanilla's Coop start uploads only default castles and
    // drops custom AICs; APIShared owns that upload and imports the plans supplied here. BugfixesAndQoL, when
    // installed, supplies plans from the same shared factory, so equal selections merge in APIShared; its
    // synchronized complete candidate list wins over a packaged list confirmed only through candidate zero.
    internal sealed partial class ExtendedDataRuntime
    {
        private ICoopAiStartPreviewCapability coopAiPreview;
        private bool coopAiImportAvailable;
        private string coopAiImportUnavailableReason = string.Empty;

        private void RegisterCoopAiStartProvider()
        {
            try
            {
                if (ApiShared.ForMod(ExtendedDataPlugin.PluginGuid).TryGetCoopAiStartImport(
                        out ICoopAiStartImportCapability capability, out NativeCapabilityDiagnostic diagnostic) &&
                    capability.TryRegisterProvider("ExtendedData.CoopPackage", ProvideCoopAiStart, out diagnostic))
                {
                    coopAiImportAvailable = true;
                    coopAiPreview = capability as ICoopAiStartPreviewCapability;
                    return;
                }
                coopAiImportUnavailableReason = diagnostic?.Reason ?? "unknown";
            }
            catch (Exception exception)
            {
                coopAiImportUnavailableReason = exception.Message;
            }
            // Packaged Custom-AIC/AIV missions are blocked at their start button instead of silently
            // starting with Vanilla castles.
            LogError("APIShared Coop AI start import is unavailable; Coop packages with custom AI data cannot start: " +
                coopAiImportUnavailableReason);
        }

        // Runs on the Unity thread inside the native multiplayer initialization of a Coop start. Every peer
        // decides from the same data: the verified package and the selection Vanilla transmits in the lobby.
        private CoopAiStartContribution ProvideCoopAiStart(CoopAiStartRequest request)
        {
            ResolvedMission mission = selected;
            if (!activation.CoopPackages || mission == null || !HasActiveCoopPackage() ||
                request.CoopTrail != mission.Loaded.TrailNumber ||
                request.CoopMission + 1 != mission.Loaded.MissionNumber)
                return null;
            FRONT_Multiplayer self = GetExistingMainViewModel()?.FRONTMultiplayer;
            Platform_Multiplayer.MPLobby lobby = self?.currentLobby ?? Platform_Multiplayer.Instance?.activeLobby;
            if (lobby == null)
                throw new InvalidOperationException("The Coop lobby of the packaged mission is unavailable.");
            bool singlePlayer = self?.singlePlayerCoop == true;
            var plans = new List<CoopAiSlotPlan>();
            foreach (KeyValuePair<int, FRONT_Multiplayer.MPAIVInfo> entry in mission.AiInfoByPlayerIndex)
            {
                int playerId = entry.Key + 1;
                Platform_Multiplayer.MPLobbyMember member = lobby.GetLobbyMemberFromThis_PlayerID(playerId);
                if (member == null || !member.SkirmishMember || member.SkirmishHumanMember)
                    throw new InvalidOperationException("Packaged Coop AI player " + playerId + " is no longer an AI.");
                int lordType = member.GetLordType();
                FRONT_Multiplayer.MPAIVInfo current = CurrentCoopSelection(self, lobby, playerId, singlePlayer);
                CoopAiSlotPlan plan;
                if (current == null)
                {
                    // Without a current selection only a fully Vanilla packaged slot may keep Vanilla's upload.
                    if (Shared.CoopAiStartPlanFactory.Create(entry.Value, playerId, lordType, -1, null, true) != null)
                        throw new InvalidOperationException("Packaged Coop AI player " + playerId + " has no lobby selection.");
                    continue;
                }
                if (Shared.CoopAiStartPlanFactory.IsSameSelection(entry.Value, current, singlePlayer))
                    // In multiplayer only candidate zero could be compared with the lobby; the packaged rest is
                    // an assumption that a synchronized complete list of the host's selection overrides.
                    plan = Shared.CoopAiStartPlanFactory.Create(entry.Value, playerId, lordType, -1, null, true,
                        singlePlayer ? -1 : 1);
                else if (singlePlayer)
                    plan = Shared.CoopAiStartPlanFactory.Create(current, playerId, lordType, -1, null, true);
                else
                    // Customized in multiplayer: only candidate zero is known to every peer.
                    plan = Shared.CoopAiStartPlanFactory.Create(current, playerId, lordType, 1, null, false);
                if (plan != null)
                    plans.Add(plan);
            }
            return plans.Count == 0 ? null : new CoopAiStartContribution(plans);
        }

        private static FRONT_Multiplayer.MPAIVInfo CurrentCoopSelection(FRONT_Multiplayer self,
            Platform_Multiplayer.MPLobby lobby, int playerId, bool singlePlayer)
        {
            if (singlePlayer)
                return self?.AIVs != null && playerId <= self.AIVs.Length ? self.AIVs[playerId - 1] : null;
            string data = TransmittedAivData(lobby, playerId);
            if (string.IsNullOrWhiteSpace(data))
                return null;
            var info = new FRONT_Multiplayer.MPAIVInfo();
            info.decode(data);
            return info;
        }

        private static string TransmittedAivData(Platform_Multiplayer.MPLobby lobby, int playerId)
        {
            switch (playerId)
            {
                case 2: return lobby.AIVDataPlayer2;
                case 3: return lobby.AIVDataPlayer3;
                case 4: return lobby.AIVDataPlayer4;
                case 5: return lobby.AIVDataPlayer5;
                case 6: return lobby.AIVDataPlayer6;
                case 7: return lobby.AIVDataPlayer7;
                case 8: return lobby.AIVDataPlayer8;
                default: return null;
            }
        }

        // Host check before the start: returns the block reason or null. A customized multiplayer slot with
        // several castle candidates needs a provider that transfers its complete list to every peer (Vanilla's
        // lobby data carries one candidate). APIShared's preview runs every provider for this start, so the check
        // relies on the plan that will actually be imported, and contradicting plans block here instead of
        // aborting inside the native start.
        private string CoopAiStartBlockReason(FRONT_Multiplayer self)
        {
            ResolvedMission mission = selected;
            if (mission == null || self?.currentLobby == null)
                return null;
            bool needsImport = false;
            var needsSynchronizedList = new Dictionary<int, int>();
            foreach (KeyValuePair<int, FRONT_Multiplayer.MPAIVInfo> entry in mission.AiInfoByPlayerIndex)
            {
                int playerId = entry.Key + 1;
                FRONT_Multiplayer.MPAIVInfo hostSelection = self.AIVs != null && playerId <= self.AIVs.Length
                    ? self.AIVs[playerId - 1] : null;
                if (hostSelection == null)
                    continue;
                Shared.CoopAiSelectionRules.Classify(hostSelection.lordType, hostSelection.builtInLord,
                    hostSelection.lordConfig != null, hostSelection.lordName, hostSelection.builtIn,
                    hostSelection.community, hostSelection.historical, hostSelection.aivs?.Count ?? 0,
                    out Shared.CoopAiCastleKind castle, out bool needsPlan);
                needsImport |= needsPlan;
                if (self.singlePlayerCoop || castle != Shared.CoopAiCastleKind.Explicit ||
                    hostSelection.aivs.Count <= 1 ||
                    Shared.CoopAiStartPlanFactory.IsSameSelection(entry.Value, hostSelection, true))
                    continue;
                needsSynchronizedList[playerId] = hostSelection.aivs.Count;
            }
            if (!needsImport)
                return null;
            if (!coopAiImportAvailable || coopAiPreview == null)
                return SerpLocalization.Get("ExtendedData.ErrorCoopAiImportUnavailable") + " " +
                    (coopAiImportAvailable ? "Coop AI start preview unavailable." : coopAiImportUnavailableReason);
            if (!coopAiPreview.TryPreview(mission.Loaded.TrailNumber, mission.Loaded.MissionNumber - 1,
                    out CoopAiStartPreview preview, out NativeCapabilityDiagnostic diagnostic))
                return SerpLocalization.Get("ExtendedData.ErrorCoopAiImportUnavailable") + " " + diagnostic?.Reason;
            if (!preview.Succeeded)
                return SerpLocalization.Get("ExtendedData.ErrorCoopAiPlansConflict").Replace("{Reason}", preview.Failure);
            foreach (KeyValuePair<int, int> slot in needsSynchronizedList)
            {
                CoopAiSlotPlan plan = preview.PlanFor(slot.Key);
                if (plan == null || !plan.CandidateListComplete || plan.ConfirmedCandidateCount != plan.CandidateCount ||
                    plan.CandidateCount != slot.Value)
                    return SerpLocalization.Get("ExtendedData.ErrorCustomizedCandidatesUnsynchronized")
                        .Replace("{Player}", slot.Key.ToString());
            }
            return null;
        }
    }
}
