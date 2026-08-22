using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AIVPlacement.Core;
using CastlePlanner.AIVPlacement.Core;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;

namespace CastlePlanner.AIVPlacement
{
    internal sealed class AivSelectionDialogRuntime
    {
        private const string SelectionChangedCommand = "CastlePlanner_AIVPlacement_SelectionChanged";
        private const string VanillaSelectionListName = "CastlePlannerVanillaAivSelectionList";
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
        private delegate void AiSettingsAddSelectedDelegate(FRONT_Multiplayer_AISettings self);

        public const int MaxCustomAivsPerLord = 999;

        private static readonly FieldInfo AiSettingsAivInfoField =
            FindField(typeof(FRONT_Multiplayer_AISettings), "AIVInfo");
        private static readonly FieldInfo AiSettingsAivListField =
            FindField(typeof(FRONT_Multiplayer_AISettings), "aivList");
        private static readonly FieldInfo AiSettingsFileListField =
            FindField(typeof(FRONT_Multiplayer_AISettings), "RefFileLists");
        private static readonly FieldInfo AiSettingsMpModeField =
            FindField(typeof(FRONT_Multiplayer_AISettings), "MPMode");
        private static readonly MethodInfo AiSettingsPopulateListMethod = FindMethod(
            typeof(FRONT_Multiplayer_AISettings),
            "populateList",
            typeof(FRONT_Multiplayer.MPAIVInfo),
            typeof(bool));

        private readonly ManualLogSource log;
        private readonly AivSelectionListViewModel selectionList;
        private readonly Func<bool> isEnabled;
        private readonly Dictionary<FRONT_Multiplayer.MPAIVInfo, int> playerIdsByInfo =
            new Dictionary<FRONT_Multiplayer.MPAIVInfo, int>();
        private readonly Dictionary<int, IReadOnlyDictionary<int, AivCandidateVisualState>> statesByPlayer =
            new Dictionary<int, IReadOnlyDictionary<int, AivCandidateVisualState>>();
        private readonly HashSet<string> reportedWarnings = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> reportedErrors = new HashSet<string>(StringComparer.Ordinal);

        private Hook initHook;
        private Hook populateHook;
        private Hook buttonHook;
        private Hook addHook;
        private AiSettingsInitDelegate initTrampoline;
        private AiSettingsPopulateListDelegate populateTrampoline;
        private AiSettingsButtonClickedDelegate buttonTrampoline;
        private AiSettingsAddSelectedDelegate addTrampoline;
        private FRONT_Multiplayer.MPAIVInfo activeInfo;

        public AivSelectionDialogRuntime(
            ManualLogSource log,
            AivSelectionListViewModel selectionList,
            Func<bool> isEnabled)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.selectionList = selectionList ?? throw new ArgumentNullException(nameof(selectionList));
            this.isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
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
            MethodInfo add = FindMethod(typeof(FRONT_Multiplayer_AISettings), "AddSelected");

            try
            {
                initHook = new Hook(init, (AiSettingsInitDelegate)InitHook);
                initTrampoline = initHook.GenerateTrampoline<AiSettingsInitDelegate>();
                populateHook = new Hook(AiSettingsPopulateListMethod, (AiSettingsPopulateListDelegate)PopulateHook);
                populateTrampoline = populateHook.GenerateTrampoline<AiSettingsPopulateListDelegate>();
                buttonHook = new Hook(button, (AiSettingsButtonClickedDelegate)ButtonClickedHook);
                buttonTrampoline = buttonHook.GenerateTrampoline<AiSettingsButtonClickedDelegate>();
                addHook = new Hook(add, (AiSettingsAddSelectedDelegate)AddSelectedHook);
                addTrampoline = addHook.GenerateTrampoline<AiSettingsAddSelectedDelegate>();
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
            ReleaseHook(ref addHook);
            addTrampoline = null;
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
            playerIdsByInfo.Clear();
            if (mappings != null)
            {
                foreach (KeyValuePair<FRONT_Multiplayer.MPAIVInfo, int> entry in mappings)
                    playerIdsByInfo[entry.Key] = entry.Value;
            }
            RefreshSelectionList(FRONT_Multiplayer_AISettings.Instance);
        }

        public void BeginGeneration(AivPlacementRequestBatch batch)
        {
            statesByPlayer.Clear();
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
            foreach (AivPlacementCandidateEvaluation candidate in result.Candidates)
                states[candidate.CandidateId] = BuildVisualState(candidate);

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

        public void PublishFailure(int playerId, string reason)
        {
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
            playerIdsByInfo.Clear();
            statesByPlayer.Clear();
            activeInfo = null;
            selectionList.Refresh(null, false, null);
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
                EnforceRuntimeLimit(aivInfo);
            }
            catch (Exception ex)
            {
                LogErrorOnce("init-preparation", $"AIV selection dialog preparation failed; Vanilla continues unchanged: {ex}");
                initTrampoline(self, aivInfo, mpMode);
                return;
            }

            // The vanilla MP mode restricts the list to one AIV; the host reduces it only at start.
            initTrampoline(self, aivInfo, false);
            try
            {
                ApplySelectionListMode(self, true);
                SetEffectiveDialogMode(self);
                UpdateAddButtonVisibility(self);
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

            try
            {
                SetEffectiveDialogMode(self);
            }
            catch (Exception ex)
            {
                LogErrorOnce("populate-preparation", $"Preparing the extended AIV selection list failed; Vanilla continues unchanged: {ex}");
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

            try
            {
                SetEffectiveDialogMode(self);
            }
            catch (Exception ex)
            {
                LogErrorOnce("dialog-button-preparation", $"Preparing the extended AIV selection button action failed; Vanilla continues unchanged: {ex}");
                buttonTrampoline(self, param);
                return;
            }
            buttonTrampoline(self, param);
            try
            {
                RefreshSelectionList(self);
            }
            catch (Exception ex)
            {
                LogErrorOnce("dialog-button-refresh", $"Refreshing the extended AIV selection after a button action failed: {ex}");
            }
        }

        private void AddSelectedHook(FRONT_Multiplayer_AISettings self)
        {
            bool lobbySetupActive;
            try
            {
                lobbySetupActive = IsLobbySetupActive();
            }
            catch (Exception ex)
            {
                LogErrorOnce("add-context", $"Extended AIV add context check failed; Vanilla continues unchanged: {ex}");
                addTrampoline(self);
                return;
            }

            if (!lobbySetupActive)
            {
                addTrampoline(self);
                return;
            }

            try
            {
                SetEffectiveDialogMode(self);
            }
            catch (Exception ex)
            {
                LogErrorOnce("add-preparation", $"Preparing the extended AIV add action failed; Vanilla continues unchanged: {ex}");
                addTrampoline(self);
                return;
            }
            try
            {
                AddSelectedAivs(self);
                RefreshSelectionList(self);
                // This no-op command lets the separate SomeSettings memory hook persist the mutation.
                self.ButtonClicked(SelectionChangedCommand);
            }
            catch (Exception ex)
            {
                LogErrorOnce("add-action", $"Extended AIV add failed: {ex}");
            }
        }

        private void AddSelectedAivs(FRONT_Multiplayer_AISettings self)
        {
            FRONT_Multiplayer.MPAIVInfo info = GetAivInfo(self);
            List<CustomisationFileManager.CustomAIV> availableAivs = GetAvailableAivs(self);
            ListView fileList = GetFileList(self);
            if (info?.aivs == null || availableAivs == null || fileList?.SelectedItem == null)
                return;

            var selectedIndexes = new List<int>();
            if (fileList.SelectedItems != null && fileList.SelectedItems.Count > 1)
            {
                foreach (object selectedItem in (IEnumerable)fileList.SelectedItems)
                {
                    int selectedIndex = fileList.Items.IndexOf(selectedItem);
                    if (selectedIndex >= 0)
                        selectedIndexes.Add(selectedIndex);
                }
            }
            else if (fileList.SelectedIndex >= 0)
            {
                selectedIndexes.Add(fileList.SelectedIndex);
            }

            var checksums = new HashSet<ulong>();
            foreach (CustomisationFileManager.CustomAIV existing in info.aivs)
            {
                if (existing != null)
                    checksums.Add(existing.checksum);
            }

            foreach (int selectedIndex in selectedIndexes)
            {
                if (selectedIndex < 0 || selectedIndex >= availableAivs.Count)
                {
                    LogWarningOnce(
                        $"invalid-selected-index-{selectedIndex}",
                        $"Ignored invalid AIV selection index={selectedIndex}; availableCount={availableAivs.Count}.");
                    break;
                }
                if (info.aivs.Count >= MaxCustomAivsPerLord)
                {
                    break;
                }

                CustomisationFileManager.CustomAIV candidate = availableAivs[selectedIndex];
                if (candidate == null)
                {
                    LogWarningOnce(
                        $"null-selected-candidate-{selectedIndex}",
                        $"Ignored a null AIV candidate at selection index={selectedIndex}.");
                    continue;
                }
                if (!checksums.Add(candidate.checksum))
                    continue;
                info.aivs.Add(candidate);
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
                RefreshSelectionList(instance);
                instance.ButtonClicked(SelectionChangedCommand);
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
            selectionList.Refresh(info, allowRemoval, states);
        }

        private static AivCandidateVisualState BuildVisualState(
            AivPlacementCandidateEvaluation candidate)
        {
            switch (candidate.Status)
            {
                case AivPlacementStatus.Complete:
                    return new AivCandidateVisualState(
                        candidate.Status,
                        SerpLocalization.Get(SerpLocalization.AivPlacementComplete));
                case AivPlacementStatus.Partial:
                    AivPlacementResult best = candidate.Selection?.BestVariant;
                    return new AivCandidateVisualState(
                        candidate.Status,
                        SerpLocalization.Get(
                            SerpLocalization.AivPlacementPartial,
                            "FitPercentage", best?.Score.FitPercentage ?? 0,
                            "SequentialBuildScore", best?.Score.SequentialBuildScore ?? 0));
                case AivPlacementStatus.Impossible:
                    return new AivCandidateVisualState(
                        candidate.Status,
                        SerpLocalization.Get(SerpLocalization.AivPlacementImpossible));
                default:
                    return new AivCandidateVisualState(
                        AivPlacementStatus.NotEvaluable,
                        BuildNotEvaluableToolTip(candidate.FailureMessage));
            }
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

        private void EnforceRuntimeLimit(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info?.aivs == null || info.aivs.Count <= MaxCustomAivsPerLord)
                return;
            int removed = info.aivs.Count - MaxCustomAivsPerLord;
            info.aivs.RemoveRange(MaxCustomAivsPerLord, info.aivs.Count - MaxCustomAivsPerLord);
            LogWarningOnce(
                "runtime-limit-truncated",
                $"Trimmed {removed} AIV selections that exceeded the runtime limit of {MaxCustomAivsPerLord}.");
        }

        private static void SetEffectiveDialogMode(FRONT_Multiplayer_AISettings instance)
        {
            if (instance != null)
                AiSettingsMpModeField.SetValue(instance, false);
        }

        private void UpdateAddButtonVisibility(FRONT_Multiplayer_AISettings instance)
        {
            Button addButton = instance?.FindName("MP_Add") as Button;
            if (addButton == null)
            {
                LogWarningOnce(
                    "missing-add-button",
                    "AIV selection dialog did not expose the expected MP_Add button; extended add remains unavailable.");
                return;
            }
            addButton.Visibility = Visibility.Visible;
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

            FrameworkElement vanilla = instance.FindName(VanillaSelectionListName) as FrameworkElement;
            FrameworkElement extended = instance.FindName(ExtendedSelectionListName) as FrameworkElement;
            if (vanilla == null || extended == null)
            {
                LogWarningOnce(
                    "selection-list-hosts-missing",
                    "The AIV selection dialog did not expose both CastlePlanner list hosts; Vanilla remains usable.");
                return;
            }

            vanilla.Visibility = useExtendedList ? Visibility.Collapsed : Visibility.Visible;
            extended.Visibility = useExtendedList ? Visibility.Visible : Visibility.Collapsed;
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

        private static List<CustomisationFileManager.CustomAIV> GetAvailableAivs(
            FRONT_Multiplayer_AISettings instance) =>
            instance == null
                ? null
                : AiSettingsAivListField.GetValue(instance) as List<CustomisationFileManager.CustomAIV>;

        private static ListView GetFileList(FRONT_Multiplayer_AISettings instance) =>
            instance == null ? null : AiSettingsFileListField.GetValue(instance) as ListView;

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
