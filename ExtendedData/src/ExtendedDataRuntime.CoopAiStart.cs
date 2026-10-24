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
            var selections = new Dictionary<int, FRONT_Multiplayer.MPAIVInfo>(mission.AiInfoByPlayerIndex);
            if (singlePlayer)
            {
                Shared.CoopAiStartPlanFactory.ValidateLobby(lobby, self.AIVs);
                foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
                {
                    if (member == null || !member.SkirmishMember || member.SkirmishHumanMember) continue;
                    int id = lobby.getThisPlayerFromSteamID(member.GetSteamID());
                    if (!selections.ContainsKey(id - 1)) selections.Add(id - 1, self.AIVs[id - 1]);
                }
            }
            foreach (KeyValuePair<int, FRONT_Multiplayer.MPAIVInfo> entry in selections)
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
            try
            {
                Shared.CoopAiStartPlanFactory.ValidateLobby(self.currentLobby, self.AIVs);
                var expected = new List<CoopAiSlotPlan>();
                var complete = new HashSet<int>();
                foreach (Platform_Multiplayer.MPLobbyMember member in self.currentLobby.members)
                {
                    if (member == null || !member.SkirmishMember || member.SkirmishHumanMember) continue;
                    int id = self.currentLobby.getThisPlayerFromSteamID(member.GetSteamID());
                    FRONT_Multiplayer.MPAIVInfo info = self.AIVs[id - 1];
                    bool requireComplete = self.singlePlayerCoop ||
                        info.aivs.Count > 1 && (!mission.AiInfoByPlayerIndex.TryGetValue(id - 1, out var packaged) ||
                            !Shared.CoopAiStartPlanFactory.IsSameSelection(packaged, info, true));
                    CoopAiSlotPlan plan = Shared.CoopAiStartPlanFactory.Create(info, id, member.GetLordType(),
                        requireComplete ? -1 : 1, null, requireComplete);
                    if (plan != null) expected.Add(plan);
                    if (requireComplete) complete.Add(id);
                }
                if (!coopAiImportAvailable || coopAiPreview == null)
                {
                    if (expected.Count == 0) return null;
                    throw new Shared.CoopAiSelectionException("MissingImportPlan", expected[0].PlayerId,
                        "import unavailable: " + coopAiImportUnavailableReason);
                }
                if (!coopAiPreview.TryPreview(mission.Loaded.TrailNumber, mission.Loaded.MissionNumber - 1,
                    out CoopAiStartPreview preview, out NativeCapabilityDiagnostic diagnostic))
                    throw new Shared.CoopAiSelectionException("ImportPlanMismatch", 0, diagnostic?.Reason ?? "preview unavailable");
                if (!preview.Succeeded)
                    throw new Shared.CoopAiSelectionException("ImportPlanMismatch", 0, preview.Failure);
                foreach (CoopAiSlotPlan plan in expected)
                    Shared.CoopAiStartPlanFactory.RequirePlan(plan, preview.PlanFor(plan.PlayerId), complete.Contains(plan.PlayerId));
                return null;
            }
            catch (Shared.CoopAiSelectionException failure)
            {
                LogError(CoopLaunchDiagnostic(self) + ": " + failure + "; selections=[" +
                    Shared.CoopAiStartPlanFactory.DescribeLobby(self.currentLobby, self.AIVs) + "]");
                return LocalizeCoopSelectionFailure(failure);
            }
        }

        private static string LocalizeCoopSelectionFailure(Shared.CoopAiSelectionException failure) =>
            SerpLocalization.Get("CoopAi.Error." + failure.Code).Replace("{Player}", failure.PlayerId.ToString());

        private static string CoopLaunchDiagnostic(FRONT_Multiplayer self) =>
            "Coop launch trail=" + (self?.currentLobby?.coopTrailID + 1) + ", mission=" + self?.currentLobby?.coopSelectedMission +
            ", route=" + (APIShared.MissionRestart.IsInProgress ? "restart" :
                ExtendedDataLaunchOriginApi.RestoredFromSave ? "restore" : FRONT_Multiplayer.skirmishGame ? "customize" : "direct");
    }
}
