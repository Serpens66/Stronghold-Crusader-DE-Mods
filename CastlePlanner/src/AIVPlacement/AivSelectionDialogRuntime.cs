using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AIVPlacement.Core;
using CastlePlanner.AIVPlacement.Core;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using APIShared;

namespace CastlePlanner.AIVPlacement
{
    internal sealed class AivSelectionDialogRuntime
    {
        private const string ExtendedSelectionListName = "CastlePlannerAivSelectionListHost";

        private delegate void AiSettingsInitDelegate(
            FRONT_Multiplayer_AISettings self,
            FRONT_Multiplayer.MPAIVInfo aivInfo,
            bool mpMode);
        private delegate void AiSettingsPopulateListDelegate(
            FRONT_Multiplayer_AISettings self,
            FRONT_Multiplayer.MPAIVInfo aivInfo,
            bool doPopulate);
        private delegate void AiSettingsButtonClickedDelegate(FRONT_Multiplayer_AISettings self, string param);

        private static readonly FieldInfo AiSettingsAivInfoField =
            FindField(typeof(FRONT_Multiplayer_AISettings), "AIVInfo");
        private static readonly MethodInfo AiSettingsPopulateListMethod = FindMethod(
            typeof(FRONT_Multiplayer_AISettings),
            "populateList",
            typeof(FRONT_Multiplayer.MPAIVInfo),
            typeof(bool));

        private readonly ManualLogSource log;
        private readonly AivSelectionListViewModel selectionList;
        private readonly Func<bool> isEnabled;
        private readonly Action requestRefresh;
        private readonly Dictionary<FRONT_Multiplayer.MPAIVInfo, int> playerIdsByInfo =
            new Dictionary<FRONT_Multiplayer.MPAIVInfo, int>();
        private readonly Dictionary<int, IReadOnlyDictionary<int, AivCandidateVisualState>> statesByPlayer =
            new Dictionary<int, IReadOnlyDictionary<int, AivCandidateVisualState>>();
        private readonly Dictionary<int, Dictionary<int, AivPlacementCandidateEvaluation>> evaluatedByPlayer =
            new Dictionary<int, Dictionary<int, AivPlacementCandidateEvaluation>>();
        private readonly Dictionary<int, NativeAivAutoDecision> autoByPlayer =
            new Dictionary<int, NativeAivAutoDecision>();
        private readonly HashSet<string> reportedWarnings = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> reportedErrors = new HashSet<string>(StringComparer.Ordinal);

        private Hook initHook;
        private Hook populateHook;
        private Hook buttonHook;
        private AiSettingsInitDelegate initTrampoline;
        private AiSettingsPopulateListDelegate populateTrampoline;
        private AiSettingsButtonClickedDelegate buttonTrampoline;
        private FRONT_Multiplayer.MPAIVInfo activeInfo;
        private bool activeMpMode;

        public AivSelectionDialogRuntime(
            ManualLogSource log,
            AivSelectionListViewModel selectionList,
            Func<bool> isEnabled,
            Action requestRefresh)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.selectionList = selectionList ?? throw new ArgumentNullException(nameof(selectionList));
            this.isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
            this.requestRefresh = requestRefresh ??
                throw new ArgumentNullException(nameof(requestRefresh));
        }

        public void Install()
        {
            MethodInfo init = FindMethod(
                typeof(FRONT_Multiplayer_AISettings),
                "Init",
                typeof(FRONT_Multiplayer.MPAIVInfo),
                typeof(bool));
            MethodInfo button = FindMethod(
                typeof(FRONT_Multiplayer_AISettings),
                "ButtonClicked",
                typeof(string));
            try
            {
                initHook = new Hook(init, (AiSettingsInitDelegate)InitHook);
                initTrampoline = initHook.GenerateTrampoline<AiSettingsInitDelegate>();
                populateHook = new Hook(AiSettingsPopulateListMethod, (AiSettingsPopulateListDelegate)PopulateHook);
                populateTrampoline = populateHook.GenerateTrampoline<AiSettingsPopulateListDelegate>();
                buttonHook = new Hook(button, (AiSettingsButtonClickedDelegate)ButtonClickedHook);
                buttonTrampoline = buttonHook.GenerateTrampoline<AiSettingsButtonClickedDelegate>();
                selectionList.RemoveRequested += OnRemoveRequested;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            selectionList.RemoveRequested -= OnRemoveRequested;
            Reset();
            ReleaseHook(ref buttonHook);
            buttonTrampoline = null;
            ReleaseHook(ref populateHook);
            populateTrampoline = null;
            ReleaseHook(ref initHook);
            initTrampoline = null;
        }

        private void ReleaseHook(ref Hook hook)
        {
            Hook current = hook;
            hook = null;
            if (current == null)
                return;

            try { current.Undo(); }
            catch (Exception ex) { LogErrorOnce("selection-hook-undo", $"AIV selection hook undo failed: {ex}"); }
            try { current.Dispose(); }
            catch (Exception ex) { LogErrorOnce("selection-hook-dispose", $"AIV selection hook disposal failed: {ex}"); }
        }

        public void SetPlayerMappings(
            IReadOnlyDictionary<FRONT_Multiplayer.MPAIVInfo, int> mappings)
        {
            int nextCount = mappings?.Count ?? 0;
            if (playerIdsByInfo.Count == nextCount &&
                (mappings == null || mappings.All(entry =>
                    playerIdsByInfo.TryGetValue(entry.Key, out int playerId) &&
                    playerId == entry.Value)))
                return;

            foreach (KeyValuePair<FRONT_Multiplayer.MPAIVInfo, int> previous in playerIdsByInfo)
            {
                if (mappings == null || !mappings.TryGetValue(previous.Key, out int nextPlayerId) ||
                    nextPlayerId != previous.Value)
                    BugfixAivStatusBridge.Clear(previous.Key);
            }
            playerIdsByInfo.Clear();
            if (mappings != null)
                foreach (KeyValuePair<FRONT_Multiplayer.MPAIVInfo, int> entry in mappings)
                    playerIdsByInfo[entry.Key] = entry.Value;
            RefreshSelectionList(FRONT_Multiplayer_AISettings.Instance);
        }

        public void BeginGeneration(AivPlacementRequestBatch batch)
        {
            statesByPlayer.Clear();
            evaluatedByPlayer.Clear();
            autoByPlayer.Clear();
            foreach (AivPlacementCheckRequest request in batch.Requests)
            {
                var states = new Dictionary<int, AivCandidateVisualState>();
                foreach (AivPlacementCandidateRequest candidate in request.Candidates)
                {
                    states[candidate.CandidateId] = request.IsReady
                        ? AivCandidateVisualState.Pending
                        : new AivCandidateVisualState(
                            AivPlacementStatus.NotEvaluable,
                            BuildNotEvaluableToolTip(request.FailureKind.ToString()));
                }
                statesByPlayer[request.PlayerId] = states;
            }
            RefreshSelectionList(FRONT_Multiplayer_AISettings.Instance);
        }

        public void Publish(AivPlacementCheckResult result)
        {
            var states = new Dictionary<int, AivCandidateVisualState>();
            NativeAivAutoDecision autoDecision =
                NativeAivAutoSelector.SelectCertain(result.Candidates);
            var evaluated = new Dictionary<int, AivPlacementCandidateEvaluation>();
            foreach (AivPlacementCandidateEvaluation candidate in result.Candidates)
            {
                evaluated[candidate.CandidateId] = candidate;
                states[candidate.CandidateId] = BuildVisualState(candidate, autoDecision);
            }
            evaluatedByPlayer[result.PlayerId] = evaluated;
            autoByPlayer[result.PlayerId] = autoDecision;

            if (states.Count == 0 && statesByPlayer.TryGetValue(
                    result.PlayerId,
                    out IReadOnlyDictionary<int, AivCandidateVisualState> previous))
            {
                foreach (int candidateId in previous.Keys)
                {
                    states[candidateId] = new AivCandidateVisualState(
                        AivPlacementStatus.NotEvaluable,
                        BuildNotEvaluableToolTip(result.FailureMessage));
                }
            }

            statesByPlayer[result.PlayerId] = states;
            RefreshSelectionList(FRONT_Multiplayer_AISettings.Instance);
        }

        public void PublishCandidates(
            int playerId,
            IReadOnlyList<AivPlacementCandidateEvaluation> completed)
        {
            if (completed == null || completed.Count == 0 || !statesByPlayer.TryGetValue(playerId,
                    out IReadOnlyDictionary<int, AivCandidateVisualState> previous))
                return;
            var states = previous.ToDictionary(entry => entry.Key, entry => entry.Value);
            if (!evaluatedByPlayer.TryGetValue(playerId,
                    out Dictionary<int, AivPlacementCandidateEvaluation> evaluated))
                evaluatedByPlayer[playerId] = evaluated =
                    new Dictionary<int, AivPlacementCandidateEvaluation>();
            foreach (AivPlacementCandidateEvaluation candidate in completed)
                if (candidate != null)
                {
                    evaluated[candidate.CandidateId] = candidate;
                    states[candidate.CandidateId] = BuildVisualState(candidate, null);
                }
            statesByPlayer[playerId] = states;
            RefreshSelectionList(FRONT_Multiplayer_AISettings.Instance);
        }

        public void PublishFailure(int playerId, string reason)
        {
            evaluatedByPlayer.Remove(playerId);
            autoByPlayer.Remove(playerId);
            var states = new Dictionary<int, AivCandidateVisualState>();
            if (statesByPlayer.TryGetValue(
                    playerId,
                    out IReadOnlyDictionary<int, AivCandidateVisualState> previous))
            {
                foreach (int candidateId in previous.Keys)
                {
                    states[candidateId] = new AivCandidateVisualState(
                        AivPlacementStatus.NotEvaluable,
                        BuildNotEvaluableToolTip(reason));
                }
            }
            statesByPlayer[playerId] = states;
            RefreshSelectionList(FRONT_Multiplayer_AISettings.Instance);
        }

        public void Reset()
        {
            Shared.AiSettingsHelpHover.ClearCurrentHelp();
            foreach (FRONT_Multiplayer.MPAIVInfo previous in playerIdsByInfo.Keys)
                BugfixAivStatusBridge.Clear(previous);
            playerIdsByInfo.Clear();
            statesByPlayer.Clear();
            evaluatedByPlayer.Clear();
            autoByPlayer.Clear();
            activeInfo = null;
            selectionList.Refresh(null, false, null, activeMpMode ? 1 : 8);
            ApplySelectionListMode(FRONT_Multiplayer_AISettings.Instance, false);
        }

        private void InitHook(
            FRONT_Multiplayer_AISettings self,
            FRONT_Multiplayer.MPAIVInfo aivInfo,
            bool mpMode)
        {
            bool lobbySetupActive;
            try
            {
                lobbySetupActive = IsLobbySetupActive();
            }
            catch (Exception ex)
            {
                LogErrorOnce("init-context", $"AIV selection dialog context check failed; Vanilla continues unchanged: {ex}");
                initTrampoline(self, aivInfo, mpMode);
                return;
            }

            if (!lobbySetupActive)
            {
                initTrampoline(self, aivInfo, mpMode);
                ApplySelectionListMode(self, false);
                return;
            }

            try
            {
                activeInfo = aivInfo;
                activeMpMode = mpMode;
            }
            catch (Exception ex)
            {
                LogErrorOnce("init-preparation", $"AIV selection dialog preparation failed; Vanilla continues unchanged: {ex}");
                initTrampoline(self, aivInfo, mpMode);
                return;
            }

            initTrampoline(self, aivInfo, mpMode);
            try
            {
                FrameworkElement helpCard = self.FindName("AiSettingsHelpCard") as FrameworkElement;
                if (helpCard != null)
                    Shared.AiSettingsHelpHover.RegisterScope(helpCard);
                else
                    LogWarningOnce("help-card", "The AI-settings help card was not found.");
                ApplySelectionListMode(self, true);
                RefreshSelectionList(self);
            }
            catch (Exception ex)
            {
                LogErrorOnce("init-refresh", $"Refreshing the extended AIV selection dialog after initialization failed: {ex}");
            }
        }

        private void PopulateHook(
            FRONT_Multiplayer_AISettings self,
            FRONT_Multiplayer.MPAIVInfo aivInfo,
            bool doPopulate)
        {
            bool lobbySetupActive;
            try
            {
                lobbySetupActive = IsLobbySetupActive();
            }
            catch (Exception ex)
            {
                LogErrorOnce("populate-context", $"AIV selection list context check failed; Vanilla continues unchanged: {ex}");
                populateTrampoline(self, aivInfo, doPopulate);
                return;
            }

            if (!lobbySetupActive)
            {
                populateTrampoline(self, aivInfo, doPopulate);
                return;
            }

            populateTrampoline(self, aivInfo, doPopulate);
            try
            {
                RefreshSelectionList(self);
            }
            catch (Exception ex)
            {
                LogErrorOnce("populate-refresh", $"Refreshing the extended AIV selection list failed: {ex}");
            }
        }

        private void ButtonClickedHook(FRONT_Multiplayer_AISettings self, string param)
        {
            bool lobbySetupActive;
            try
            {
                lobbySetupActive = IsLobbySetupActive();
            }
            catch (Exception ex)
            {
                LogErrorOnce("dialog-button-context", $"AIV selection button context check failed; Vanilla continues unchanged: {ex}");
                buttonTrampoline(self, param);
                return;
            }

            if (!lobbySetupActive)
            {
                buttonTrampoline(self, param);
                return;
            }

            buttonTrampoline(self, param);
            requestRefresh();
            try
            {
                RefreshSelectionList(self);
            }
            catch (Exception ex)
            {
                LogErrorOnce("dialog-button-refresh", $"Refreshing the extended AIV selection after a button action failed: {ex}");
            }
        }

        private void OnRemoveRequested(CustomisationFileManager.CustomAIV requestedAiv)
        {
            try
            {
                if (!IsLobbySetupActive() || requestedAiv == null)
                    return;

                FRONT_Multiplayer_AISettings instance = FRONT_Multiplayer_AISettings.Instance;
                FRONT_Multiplayer.MPAIVInfo info = GetAivInfo(instance);
                if (info?.aivs == null || !IsCustomAivMode(info))
                    return;

                int index = info.aivs.IndexOf(requestedAiv);
                if (index < 0)
                    index = info.aivs.FindIndex(aiv => aiv != null && aiv.checksum == requestedAiv.checksum);
                if (index < 0)
                {
                    LogWarningOnce(
                        $"remove-missing-{requestedAiv.checksum}",
                        $"Ignored removal of an AIV that is no longer selected; checksum={requestedAiv.checksum}.");
                    return;
                }

                info.aivs.RemoveAt(index);
                requestRefresh();
                RefreshSelectionList(instance);
            }
            catch (Exception ex)
            {
                LogErrorOnce("remove-action", $"Removing an extended AIV selection failed: {ex}");
            }
        }

        private void RefreshSelectionList(FRONT_Multiplayer_AISettings instance)
        {
            FRONT_Multiplayer.MPAIVInfo info = GetAivInfo(instance) ?? activeInfo;
            bool allowRemoval = IsCustomAivMode(info);
            IReadOnlyDictionary<int, AivCandidateVisualState> states = null;
            if (info != null && playerIdsByInfo.TryGetValue(info, out int playerId))
                statesByPlayer.TryGetValue(playerId, out states);
            PublishToBugfixApi(info, states);
            bool bugfixListVisible = BugfixAivStatusBridge.IsSelectionListVisible(instance);
            selectionList.Refresh(info, allowRemoval, states, activeMpMode ? 1 : 8);
            ApplySelectionListMode(instance, !bugfixListVisible);
        }

        private static AivCandidateVisualState BuildVisualState(
            AivPlacementCandidateEvaluation candidate,
            NativeAivAutoDecision autoDecision)
        {
            string description;
            switch (candidate.Status)
            {
                case AivPlacementStatus.Complete:
                    description = SerpLocalization.Get(SerpLocalization.AivPlacementComplete);
                    break;
                case AivPlacementStatus.Partial:
                    AivPlacementResult best = candidate.Selection?.BestVariant;
                    description = SerpLocalization.Get(
                        SerpLocalization.AivPlacementPartial,
                        "FitPercentage", best?.Score.FitPercentage ?? 0,
                        "SequentialBuildScore", best?.Score.SequentialBuildScore ?? 0);
                    break;
                case AivPlacementStatus.Impossible:
                    description = SerpLocalization.Get(SerpLocalization.AivPlacementImpossible);
                    break;
                default:
                    description = BuildNotEvaluableToolTip(candidate.FailureMessage);
                    break;
            }

            if (candidate.Selection != null)
            {
                string rotations = string.Join(" | ", candidate.Selection.Variants.Select(
                    variant => $"{FormatRotation((int)variant.Rotation)}: " +
                        (variant.Status == AivPlacementStatus.Partial
                            ? $"{variant.Score.FitPercentage}%"
                            : SerpLocalization.Get(variant.Status == AivPlacementStatus.Complete
                                ? SerpLocalization.AivPlacementComplete
                                : variant.Status == AivPlacementStatus.Impossible
                                    ? SerpLocalization.AivPlacementImpossible
                                    : SerpLocalization.AivPlacementNotEvaluable,
                            "Reason", "?"))));
                description += Environment.NewLine + SerpLocalization.Get(
                    SerpLocalization.AivPlacementRotationResults,
                    "Results", rotations);
            }

            if (candidate.Selection != null)
            {
                string moat = FormatExposure(candidate.Selection,
                    candidate.ElevatedMoatTilesByRotation);
                string drawbridge = FormatExposure(candidate.Selection,
                    candidate.ElevatedDrawbridgeTilesByRotation);
                if (moat.Length > 0 || drawbridge.Length > 0)
                {
                    ElevatedMoatAiState state = ElevatedMoatAiCapability.Current;
                    string key = state == ElevatedMoatAiState.Enabled
                        ? SerpLocalization.AivPlacementHighBuildEnabled
                        : state == ElevatedMoatAiState.Disabled
                            ? SerpLocalization.AivPlacementHighBuildDisabled
                            : SerpLocalization.AivPlacementHighBuildUnknown;
                    description += Environment.NewLine + SerpLocalization.Get(key);
                    if (moat.Length > 0)
                        description += Environment.NewLine + SerpLocalization.Get(
                            SerpLocalization.AivPlacementHighMoat, "Rotations", moat);
                    if (drawbridge.Length > 0)
                        description += Environment.NewLine + SerpLocalization.Get(
                            SerpLocalization.AivPlacementHighDrawbridge, "Rotations", drawbridge);
                }
            }

            string autoText = autoDecision == null || !autoDecision.IsCertain
                ? SerpLocalization.Get(SerpLocalization.AivPlacementAutoUnknown)
                : !autoDecision.CandidateId.HasValue
                    ? SerpLocalization.Get(SerpLocalization.AivPlacementAutoImpossible)
                    : autoDecision.CandidateId.Value == candidate.CandidateId
                        ? SerpLocalization.Get(
                            SerpLocalization.AivPlacementAutoSelected,
                            "Rotation", (int)candidate.Selection.Variants[autoDecision.RotationIndex].Rotation) +
                            FormatDirectionSuffix(
                                (int)candidate.Selection.Variants[autoDecision.RotationIndex].Rotation)
                        : SerpLocalization.Get(SerpLocalization.AivPlacementAutoDifferent);
            return new AivCandidateVisualState(
                candidate.Status,
                description + Environment.NewLine + autoText);
        }

        public void RefreshMoatStatus()
        {
            foreach (KeyValuePair<int, Dictionary<int, AivPlacementCandidateEvaluation>> player in evaluatedByPlayer)
            {
                if (!statesByPlayer.TryGetValue(player.Key,
                        out IReadOnlyDictionary<int, AivCandidateVisualState> previous))
                    continue;
                var states = previous.ToDictionary(entry => entry.Key, entry => entry.Value);
                autoByPlayer.TryGetValue(player.Key, out NativeAivAutoDecision autoDecision);
                foreach (AivPlacementCandidateEvaluation candidate in player.Value.Values)
                    states[candidate.CandidateId] = BuildVisualState(candidate, autoDecision);
                statesByPlayer[player.Key] = states;
            }
            RefreshSelectionList(FRONT_Multiplayer_AISettings.Instance);
        }

        private static string FormatExposure(
            AivPlacementRotationSelection selection,
            IReadOnlyList<int> tilesByRotation)
        {
            var exposed = new List<string>();
            int count = Math.Min(selection.Variants.Count, tilesByRotation.Count);
            for (int index = 0; index < count; index++)
            {
                int tiles = tilesByRotation[index];
                if (tiles > 0)
                    exposed.Add($"{FormatRotation((int)selection.Variants[index].Rotation)}: {tiles}");
            }
            return string.Join(" | ", exposed);
        }

        private static string FormatRotation(int degrees)
        {
            return $"{degrees}°{FormatDirectionSuffix(degrees)}";
        }

        private static string FormatDirectionSuffix(int degrees)
        {
            string direction = CastleRotationOptions.GetDirection(
                degrees,
                key => Translate.Instance.GameTexts.TryGetValue(key, out string text)
                    ? text
                    : null);
            return string.IsNullOrEmpty(direction) ? string.Empty : $" ({direction})";
        }

        private static string BuildNotEvaluableToolTip(string reason)
        {
            string friendly = reason ?? string.Empty;
            if (friendly.IndexOf("PreBuildSequenceUnsupported", StringComparison.OrdinalIgnoreCase) >= 0)
                friendly = SerpLocalization.Get(SerpLocalization.AivPlacementPreBuildUnsupported);
            else if (friendly.IndexOf("ClientEvaluationNotRequired", StringComparison.OrdinalIgnoreCase) >= 0)
                friendly = SerpLocalization.Get(SerpLocalization.AivPlacementHostOnly);
            return SerpLocalization.Get(
                SerpLocalization.AivPlacementNotEvaluable,
                "Reason", friendly);
        }

        private void LogWarningOnce(string key, string message)
        {
            if (reportedWarnings.Add(key ?? string.Empty))
                Shared.DebugLogHelper.LogWarning(log, message);
        }

        private void LogErrorOnce(string key, string message)
        {
            if (reportedErrors.Add(key ?? string.Empty))
                Shared.DebugLogHelper.LogError(log, message);
        }

        private static bool IsCustomAivMode(FRONT_Multiplayer.MPAIVInfo info) =>
            info != null && !info.builtIn && !info.community && !info.historical;

        private void ApplySelectionListMode(
            FRONT_Multiplayer_AISettings instance,
            bool useExtendedList)
        {
            if (instance == null)
                return;

            FrameworkElement extended = instance.FindName(ExtendedSelectionListName) as FrameworkElement;
            FrameworkElement vanilla = null;
            if (instance.FindName("Player1_Kick") is FrameworkElement kick)
            {
                DependencyObject row = VisualTreeHelper.GetParent(kick);
                vanilla = row == null ? null : VisualTreeHelper.GetParent(row) as FrameworkElement;
            }
            if (vanilla == null || extended == null)
            {
                LogWarningOnce(
                    "selection-list-hosts-missing",
                    "The AIV selection dialog did not expose CastlePlanner's status host; Vanilla remains usable.");
                return;
            }

            FrameworkElement bugfixList =
                instance.FindName("BugfixesAndQoLAivSelectionListHost") as FrameworkElement;
            if (bugfixList?.Visibility == Visibility.Visible)
            {
                // BugfixesAndQoL owns the list while its 50-entry enhancement is active.
                vanilla.Visibility = Visibility.Collapsed;
                extended.Visibility = Visibility.Collapsed;
                return;
            }

            vanilla.Visibility = useExtendedList ? Visibility.Collapsed : Visibility.Visible;
            extended.Visibility = useExtendedList ? Visibility.Visible : Visibility.Collapsed;
        }

        private void PublishToBugfixApi(
            FRONT_Multiplayer.MPAIVInfo info,
            IReadOnlyDictionary<int, AivCandidateVisualState> states)
        {
            if (info?.aivs == null || !BugfixAivStatusBridge.IsAvailable)
                return;
            var checksums = new List<ulong>();
            var neutralStatuses = new List<int>();
            var toolTips = new List<string>();
            for (int candidateId = 0; candidateId < info.aivs.Count; candidateId++)
            {
                CustomisationFileManager.CustomAIV aiv = info.aivs[candidateId];
                if (aiv == null || states == null ||
                    !states.TryGetValue(candidateId, out AivCandidateVisualState state))
                    continue;
                int neutralStatus;
                if (!state.Status.HasValue)
                    neutralStatus = 0;
                else
                {
                    switch (state.Status.Value)
                    {
                        case AivPlacementStatus.Complete: neutralStatus = 1; break;
                        case AivPlacementStatus.Partial: neutralStatus = 2; break;
                        case AivPlacementStatus.Impossible: neutralStatus = 3; break;
                        default: neutralStatus = 4; break;
                    }
                }
                checksums.Add(aiv.checksum);
                neutralStatuses.Add(neutralStatus);
                toolTips.Add(state.ToolTip ?? string.Empty);
            }
            BugfixAivStatusBridge.TryReplace(
                info,
                checksums.ToArray(),
                neutralStatuses.ToArray(),
                toolTips.ToArray());
        }

        private bool IsLobbySetupActive()
        {
            if (!isEnabled())
                return false;

            MainViewModel viewModel = MainViewModel.Instance;
            return viewModel?.Show_MultiplayerSetup == true &&
                viewModel.Show_MPGameCreation == true &&
                (!FRONT_Multiplayer.coopGame || FRONT_Multiplayer.skirmishGame);
        }

        private static FRONT_Multiplayer.MPAIVInfo GetAivInfo(FRONT_Multiplayer_AISettings instance) =>
            instance == null ? null : AiSettingsAivInfoField.GetValue(instance) as FRONT_Multiplayer.MPAIVInfo;

        private static MethodInfo FindMethod(Type type, string name, params Type[] parameterTypes)
        {
            MethodInfo method = type.GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            return method ?? throw new MissingMethodException(type.FullName, name);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            FieldInfo field = type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field ?? throw new MissingFieldException(type.FullName, name);
        }
    }
}
