using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AIVPlacementLobby.Core;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using Button = Noesis.Button;
using ToolTipService = Noesis.ToolTipService;

namespace AIVPlacementLobby
{
    internal sealed class AIVPlacementLobbyRuntime
    {
        private static readonly FieldInfo SelectedMapHeaderField =
            FindField(typeof(FRONT_Multiplayer), "selectedMPHeader");
        private static readonly FieldInfo MultiplayerSetupDataField =
            FindField(typeof(FRONT_Multiplayer), "MPsetupData");
        private static readonly FieldInfo MultiplayerLocalReadyField =
            FindField(typeof(FRONT_Multiplayer), "MPLocalReady");
        private static readonly FieldInfo MultiplayerReadyButtonField =
            FindField(typeof(FRONT_Multiplayer), "RefReadyButton");

        private delegate void UpdateDelegate(FRONT_Multiplayer self);
        private delegate void StartSkirmishGameDelegate(
            FRONT_Multiplayer self,
            HUD_IngameMenu.RestartSkirmishMapInfo restartInfo);
        private delegate void ButtonClickedDelegate(FRONT_Multiplayer self, string param);

        private readonly ManualLogSource log;
        private readonly LobbyRequestBuilder requestBuilder = new LobbyRequestBuilder();
        private readonly LobbyRequestGenerationGate generations = new LobbyRequestGenerationGate();
        private readonly AivPlacementEvaluationService evaluationService =
            new AivPlacementEvaluationService();
        private readonly AivSelectionListViewModel selectionList = new AivSelectionListViewModel();
        private readonly AivSelectionDialogRuntime selectionDialog;
        private readonly ConcurrentQueue<CompletedEvaluation> completedEvaluations =
            new ConcurrentQueue<CompletedEvaluation>();
        private readonly Dictionary<string, string> assetOverrideCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string vanillaAivDirectory;
        private readonly Dictionary<int, AivPlacementCheckResult> currentResults =
            new Dictionary<int, AivPlacementCheckResult>();
        private readonly Dictionary<int, int> selectedNetworkCandidateIds =
            new Dictionary<int, int>();
        private readonly HashSet<int> pendingPlayerIds = new HashSet<int>();
        private readonly Random random = new Random();
        private readonly object randomSync = new object();
        private Hook updateHook;
        private Hook startHook;
        private Hook buttonClickedHook;
        private UpdateDelegate updateTrampoline;
        private StartSkirmishGameDelegate startTrampoline;
        private ButtonClickedDelegate buttonClickedTrampoline;
        private string lastFingerprint = string.Empty;
        private string lastSourceFingerprint = string.Empty;
        private long nextSourcePollTimestamp;
        private bool captureFailureLogged;
        private bool lobbyContextActive;
        private Button blockedReadyButton;
        private bool blockedReadyButtonWasEnabled;
        private object blockedReadyButtonToolTip;
        private string lastContextDiagnosticFingerprint = string.Empty;

        public AIVPlacementLobbyRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            vanillaAivDirectory = Path.Combine(pluginDirectory ?? string.Empty, "VanillaAIV");
            selectionDialog = new AivSelectionDialogRuntime(log, selectionList);
        }

        public object SelectionList => selectionList;

        public void Install()
        {
            MethodInfo update = FindMethod(typeof(FRONT_Multiplayer), "Update", Type.EmptyTypes);
            MethodInfo start = FindMethod(
                typeof(FRONT_Multiplayer),
                "StartSkirmishGame",
                new[] { typeof(HUD_IngameMenu.RestartSkirmishMapInfo) });
            MethodInfo buttonClicked = FindMethod(
                typeof(FRONT_Multiplayer),
                "ButtonClicked",
                new[] { typeof(string) });
            updateHook = new Hook(update, (UpdateDelegate)UpdateHook);
            updateTrampoline = updateHook.GenerateTrampoline<UpdateDelegate>();
            startHook = new Hook(start, (StartSkirmishGameDelegate)StartSkirmishGameHook);
            startTrampoline = startHook.GenerateTrampoline<StartSkirmishGameDelegate>();
            buttonClickedHook = new Hook(buttonClicked, (ButtonClickedDelegate)ButtonClickedHook);
            buttonClickedTrampoline = buttonClickedHook.GenerateTrampoline<ButtonClickedDelegate>();
            selectionDialog.Install();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AIV lobby data-flow hooks installed; vanillaAivDirectory={vanillaAivDirectory}.");
        }

        private void UpdateHook(FRONT_Multiplayer self)
        {
            updateTrampoline(self);
            bool lobbySetupActive = IsLobbySetupActive();
            LogContextIfChanged(self, lobbySetupActive);
            if (!lobbySetupActive)
            {
                LeaveLobbyContext();
                return;
            }

            lobbyContextActive = true;
            CaptureIfChanged(self, false, "lobby update");
            PublishCompletedEvaluations();
            selectionList.UpdateToolTipScale(CalculateFrontendToolTipScale());
            UpdateHostReadyButton(self);
        }

        private void ButtonClickedHook(FRONT_Multiplayer self, string param)
        {
            if (!IsLobbySetupActive())
            {
                buttonClickedTrampoline(self, param);
                return;
            }

            bool networkHost = IsNetworkHost(self);
            if (networkHost && pendingPlayerIds.Count > 0 &&
                (string.Equals(param, "Ready", StringComparison.Ordinal) ||
                 string.Equals(param, "Play", StringComparison.Ordinal)))
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Blocked multiplayer host action {param}; " +
                    $"pendingPlacementChecks={pendingPlayerIds.Count}.");
                UpdateHostReadyButton(self);
                return;
            }

            List<NetworkAivSnapshot> snapshots = null;
            if (networkHost && string.Equals(param, "Play", StringComparison.Ordinal))
                snapshots = SelectNetworkStartAivs(self);

            try
            {
                buttonClickedTrampoline(self, param);
            }
            finally
            {
                RestoreNetworkStartAivs(snapshots);
            }
        }

        private void StartSkirmishGameHook(
            FRONT_Multiplayer self,
            HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
        {
            if (!IsLobbySetupActive())
            {
                startTrampoline(self, restartInfo);
                return;
            }

            // Capture before Vanilla rewrites preferredAIVs and transfers setup into native state.
            CaptureIfChanged(self, true, "before StartSkirmishGame");
            startTrampoline(self, restartInfo);
        }

        private void CaptureIfChanged(FRONT_Multiplayer frontend, bool force, string reason)
        {
            try
            {
                LobbyStateCapture capture = Capture(frontend);
                string fingerprint = LobbyRequestBuilder.BuildFingerprint(capture);
                bool stateChanged = !string.Equals(
                    fingerprint,
                    lastFingerprint,
                    StringComparison.Ordinal);
                long now = Stopwatch.GetTimestamp();
                if (!force && !stateChanged && now < nextSourcePollTimestamp)
                    return;

                nextSourcePollTimestamp = now + Stopwatch.Frequency / 2;
                AivPlacementRequestBatch provisional = requestBuilder.Build(
                    1,
                    capture,
                    vanillaAivDirectory);
                IReadOnlyDictionary<string, string> assets = CaptureAssetSnapshot(provisional);
                string sourceFingerprint = AivPlacementEvaluationService.BuildSourceFingerprint(
                    provisional,
                    assets);
                if (!force && !stateChanged && string.Equals(
                        sourceFingerprint,
                        lastSourceFingerprint,
                        StringComparison.Ordinal))
                {
                    return;
                }

                lastFingerprint = fingerprint;
                lastSourceFingerprint = sourceFingerprint;
                long generation = generations.Advance();
                AivPlacementRequestBatch batch = requestBuilder.Build(
                    generation,
                    capture,
                    vanillaAivDirectory);
                BeginGeneration(frontend, batch);
                LogBatch(batch, reason);
                QueueEvaluations(batch, assets, IsNetworkHost(frontend));
            }
            catch (Exception ex)
            {
                if (captureFailureLogged)
                    return;
                captureFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Lobby capture failed; Vanilla remains unchanged and further identical errors are suppressed: {ex}");
            }
        }

        private LobbyStateCapture Capture(FRONT_Multiplayer frontend)
        {
            FileHeader header = frontend == null
                ? null
                : SelectedMapHeaderField.GetValue(frontend) as FileHeader;
            EngineInterface.MultiplayerSetupData setup = frontend == null
                ? null
                : MultiplayerSetupDataField.GetValue(frontend) as EngineInterface.MultiplayerSetupData;
            Platform_Multiplayer.MPLobby lobby = frontend?.currentLobby;
            var slots = new List<LobbyAiSlotInput>();
            var humanPlayerIds = new List<int>();
            var assets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var playerMappings = new Dictionary<FRONT_Multiplayer.MPAIVInfo, int>();

            if (lobby?.members != null && frontend.AIVs != null)
            {
                foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
                {
                    // Unused and spectator rows never contribute a serialized player start.
                    if (member == null || !member.SkirmishMember)
                        continue;
                    int playerId = lobby.getThisPlayerFromSteamID(member.GetSteamID());
                    if (playerId < 1 || playerId > frontend.AIVs.Length)
                        continue;
                    if (member.SkirmishHumanMember)
                    {
                        humanPlayerIds.Add(playerId);
                        continue;
                    }

                    FRONT_Multiplayer.MPAIVInfo info = frontend.AIVs[playerId - 1];
                    if (info != null)
                        playerMappings[info] = playerId;
                    int lordType = member.GetLordType();
                    string lordEnumName = ToLordEnumName(lordType);
                    var candidates = new List<LobbyAivCandidateInput>();
                    if (info?.aivs != null)
                    {
                        foreach (CustomisationFileManager.CustomAIV candidate in info.aivs)
                        {
                            if (candidate == null)
                                continue;
                            string candidateLord = ToLordEnumName(candidate.lordType);
                            candidates.Add(new LobbyAivCandidateInput(
                                candidate.AIVName,
                                candidate.path,
                                candidate.checksum,
                                candidate.builtIn,
                                candidateLord));
                        }
                    }

                    LobbyAivMode mode = GetMode(info);
                    ProbeOverrides(lordEnumName, mode == LobbyAivMode.Historical ? 1 : 8, assets);
                    foreach (LobbyAivCandidateInput candidate in candidates)
                        ProbeOverrides(candidate.LordEnumName, 8, assets);
                    slots.Add(new LobbyAiSlotInput(
                        playerId,
                        lordType,
                        lordEnumName,
                        member.customLordName,
                        mode,
                        info?.rotation ?? -1,
                        candidates));
                }
            }

            selectionDialog.SetPlayerMappings(playerMappings);

            return new LobbyStateCapture(
                header?.filePath,
                header?.display_filename ?? header?.fileName,
                DescribeMapOrigin(header),
                lobby?.isHost == true,
                setup?.advopt_pre_build ?? -1,
                setup?.start_keep_location_order == null
                    ? Array.Empty<int>()
                    : (int[])setup.start_keep_location_order.Clone(),
                slots,
                assets,
                humanPlayerIds);
        }

        private void ProbeOverrides(string lordEnumName, int count, ISet<string> assets)
        {
            if (string.IsNullOrEmpty(lordEnumName))
                return;
            for (int index = 0; index < count; index++)
            {
                string asset = $"AIV/{lordEnumName}_{index}.aivjson";
                if (!assetOverrideCache.TryGetValue(asset, out string content))
                {
                    try
                    {
                        if (GameAssetManagerAPI.Instance == null ||
                            !GameAssetManagerAPI.Instance.GetModifiedFileTextContent(asset, out content))
                        {
                            content = null;
                        }
                    }
                    catch
                    {
                        content = null;
                    }
                    assetOverrideCache[asset] = content;
                }
                if (content != null)
                    assets.Add(asset);
            }
        }

        private IReadOnlyDictionary<string, string> CaptureAssetSnapshot(
            AivPlacementRequestBatch batch)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (AivPlacementCandidateRequest candidate in batch.Requests
                .SelectMany(request => request.Candidates))
            {
                if (candidate.SourceKind == LobbyCandidateSourceKind.ScriptExtenderAsset &&
                    assetOverrideCache.TryGetValue(candidate.Source, out string content) &&
                    content != null)
                {
                    // Unity-backed asset access ends here; workers receive only immutable text.
                    result[candidate.Source] = content;
                }
            }
            return result;
        }

        private void QueueEvaluations(
            AivPlacementRequestBatch batch,
            IReadOnlyDictionary<string, string> assets,
            bool networkHost)
        {
            var requestsByPlayer = batch.Requests.ToDictionary(request => request.PlayerId);
            Task<AivPlacementBatchResult> task = evaluationService.EvaluateBatchAsync(
                batch,
                assets,
                result => SelectCandidateForSequentialNetworkState(
                    result,
                    requestsByPlayer,
                    networkHost));
            task.ContinueWith(
                completed =>
                {
                    if (completed.Status == TaskStatus.RanToCompletion)
                    {
                        foreach (AivPlacementCheckResult result in completed.Result.Results)
                        {
                            completed.Result.SelectedCandidateIdsByPlayer.TryGetValue(
                                result.PlayerId,
                                out int selectedCandidateId);
                            completedEvaluations.Enqueue(new CompletedEvaluation(
                                result.Generation,
                                result.PlayerId,
                                result,
                                null,
                                completed.Result.SelectedCandidateIdsByPlayer.ContainsKey(result.PlayerId)
                                    ? (int?)selectedCandidateId
                                    : null));
                        }
                        return;
                    }

                    foreach (AivPlacementCheckRequest request in batch.Requests)
                    {
                        completedEvaluations.Enqueue(new CompletedEvaluation(
                            request.Generation,
                            request.PlayerId,
                            null,
                            completed.Exception,
                            null));
                    }
                },
                TaskScheduler.Default);
        }

        private int? SelectCandidateForSequentialNetworkState(
            AivPlacementCheckResult result,
            IReadOnlyDictionary<int, AivPlacementCheckRequest> requestsByPlayer,
            bool networkHost)
        {
            if (!networkHost ||
                !requestsByPlayer.TryGetValue(result.PlayerId, out AivPlacementCheckRequest request) ||
                request.AivMode != LobbyAivMode.Custom ||
                result.Candidates.Count <= 1)
            {
                return null;
            }

            IReadOnlyList<int> eligibleIds = BestFitCandidateSelector.GetEligibleCandidateIds(result);
            if (eligibleIds.Count == 0)
                eligibleIds = result.Candidates.Select(candidate => candidate.CandidateId).ToArray();
            if (eligibleIds.Count == 0)
                return null;

            lock (randomSync)
                return eligibleIds[random.Next(eligibleIds.Count)];
        }

        private void PublishCompletedEvaluations()
        {
            while (completedEvaluations.TryDequeue(out CompletedEvaluation completed))
            {
                if (completed.Error != null)
                {
                    if (generations.IsCurrent(completed.Generation))
                    {
                        pendingPlayerIds.Remove(completed.PlayerId);
                        selectionDialog.PublishFailure(
                            completed.PlayerId,
                            completed.Error.GetBaseException().Message);
                    }
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Asynchronous lobby placement evaluation failed: {completed.Error}");
                    continue;
                }

                AivPlacementCheckResult result = completed.Result;
                if (!generations.IsCurrent(result.Generation))
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Discarded stale lobby placement result generation={result.Generation}, " +
                        $"currentGeneration={generations.Current}, playerId={result.PlayerId}.");
                    continue;
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Lobby placement result generation={result.Generation}, playerId={result.PlayerId}, " +
                    $"keepIndex={result.KeepSlotIndex}, advopt_pre_build={result.PreBuildSetting}, " +
                    $"status={result.Status}, reason={result.FailureKind}, " +
                    $"selectedCandidateId={result.SelectedCandidate?.CandidateId.ToString() ?? "none"}, " +
                    $"selectedRotation={result.SelectedVariant?.Rotation.ToString() ?? "none"}, " +
                    $"elapsedMs={result.Elapsed.TotalMilliseconds:F3}.");
                pendingPlayerIds.Remove(result.PlayerId);
                currentResults[result.PlayerId] = result;
                if (completed.SelectedCandidateId.HasValue)
                    selectedNetworkCandidateIds[result.PlayerId] = completed.SelectedCandidateId.Value;
                selectionDialog.Publish(result);
                foreach (AivPlacementCandidateEvaluation candidate in result.Candidates)
                {
                    LobbyPlacementPhaseTimings timings = candidate.Timings;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Lobby placement timing generation={result.Generation}, playerId={result.PlayerId}, " +
                        $"candidateId={candidate.CandidateId}, status={candidate.Status}, " +
                        $"cache={candidate.CacheDisposition}, mapCacheHit={timings.MapCacheHit}, " +
                        $"mapLoadShared={timings.MapLoadShared}, mapParseMs={timings.MapParse.TotalMilliseconds:F3}, " +
                        $"snapshotMs={timings.Snapshot.TotalMilliseconds:F3}, " +
                        $"aivParseMs={timings.AivParse.TotalMilliseconds:F3}, " +
                        $"projectionMs={timings.Projection.TotalMilliseconds:F3}, " +
                        $"ruleMs={timings.RuleEvaluation.TotalMilliseconds:F3}, " +
                        $"reason={candidate.FailureKind}.");

                    if (candidate.Selection == null)
                        continue;
                    foreach (var variant in candidate.Selection.Variants)
                    {
                        var firstIssue = variant.Issues.FirstOrDefault();
                        Shared.DebugLogHelper.LogInfo(
                            log,
                            $"Lobby placement variant generation={result.Generation}, playerId={result.PlayerId}, " +
                            $"candidateId={candidate.CandidateId}, rotation={(int)variant.Rotation}, " +
                            $"status={variant.Status}, sequentialBuildScore={variant.Score.SequentialBuildScore}, " +
                            $"fitPercentage={variant.Score.FitPercentage}, blockedElements={variant.BlockedElementCount}, " +
                            $"issueCount={variant.Issues.Count}, " +
                            $"firstBlockingBuildStep={variant.FirstBlockingBuildStep?.ToString() ?? "none"}, " +
                            $"firstIssue={firstIssue?.Kind.ToString() ?? "none"}, " +
                            $"firstIssueBuildIndex={firstIssue?.BuildIndex.ToString() ?? "none"}, " +
                            $"firstIssueCoordinate={(firstIssue == null ? "none" : $"{firstIssue.MapCoordinate.X},{firstIssue.MapCoordinate.Y}")}.");
                    }
                }
            }
        }

        private static float CalculateFrontendToolTipScale()
        {
            float width = UnityEngine.Screen.width;
            float height = UnityEngine.Screen.height;
            float scale = 1f;
            if (width < 1920f || height < 1080f)
            {
                float widthRatio = width / 1920f;
                float heightRatio = height / 1080f;
                scale = 1f / Math.Min(widthRatio, heightRatio);
                if (scale < 1f)
                    scale = 1f;
            }

            // Mirror FrontendMenus.UpdateFrontMenuPopupScale so popup text follows resolution and UI scale.
            if (UnityEngine.Screen.width > 1366 && UnityEngine.Screen.height > 768)
                scale = (1.6f - scale) * ConfigSettings.Settings_UIScale + scale;
            return scale;
        }

        private void LogBatch(AivPlacementRequestBatch batch, string reason)
        {
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Lobby placement generation={batch.Generation}, reason={reason}, " +
                $"aiRequests={batch.Requests.Count}.");
            foreach (AivPlacementCheckRequest request in batch.Requests)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Lobby placement request generation={request.Generation}, host={request.IsHost}, " +
                    $"map={request.MapName}, mapPath={request.MapPath}, mapOrigin={request.MapOrigin}, " +
                    $"playerId={request.PlayerId}, keepIndex={request.KeepSlotIndex}, lord={request.LordName}, " +
                    $"retainedStartSlots={string.Join(",", request.RetainedStartSlotIndexes)}, " +
                    $"aivMode={request.AivMode}, rotationMode=" +
                    $"{(request.UsesMapFacingRotation ? "MapFacing" : "Fixed")}, " +
                    $"initialRotation={(int)request.InitialRotation}, " +
                    $"advopt_pre_build={request.PreBuildSetting}, status={(request.IsReady ? "Ready" : "NotEvaluable")}, " +
                    $"reason={request.FailureKind}, candidates={request.Candidates.Count}.");
                foreach (AivPlacementCandidateRequest candidate in request.Candidates)
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Lobby AIV candidate generation={request.Generation}, playerId={request.PlayerId}, " +
                        $"candidateId={candidate.CandidateId}, name={candidate.Name}, sourceKind={candidate.SourceKind}, " +
                        $"source={candidate.Source}, checksum={candidate.Checksum}, " +
                        $"status={(candidate.IsAvailable ? "Ready" : "NotEvaluable")}, reason={candidate.FailureKind}.");
                }
            }
        }

        private static LobbyAivMode GetMode(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null || info.builtIn || info.aivs == null || info.aivs.Count == 0)
                return LobbyAivMode.Default;
            if (info.community)
                return LobbyAivMode.Community;
            if (info.historical)
                return LobbyAivMode.Historical;
            return LobbyAivMode.Custom;
        }

        private static string ToLordEnumName(int zeroBasedLordType)
        {
            int value = zeroBasedLordType + 1;
            return Enum.IsDefined(typeof(Enums.AILords), value)
                ? ((Enums.AILords)value).ToString()
                : $"UNKNOWN_{zeroBasedLordType}";
        }

        private static string DescribeMapOrigin(FileHeader header)
        {
            if (header == null)
                return "Unavailable";
            if (header.builtinMap)
                return "BuiltIn";
            if (header.workshopMap)
                return "Workshop";
            if (header.userMap)
                return "User";
            return "Other";
        }

        private static MethodInfo FindMethod(Type type, string name, Type[] parameterTypes)
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

        private void BeginGeneration(
            FRONT_Multiplayer frontend,
            AivPlacementRequestBatch batch)
        {
            currentResults.Clear();
            selectedNetworkCandidateIds.Clear();
            pendingPlayerIds.Clear();
            foreach (AivPlacementCheckRequest request in batch.Requests)
                pendingPlayerIds.Add(request.PlayerId);
            selectionDialog.BeginGeneration(batch);

            // A changed lobby invalidates a ready state that was based on an older generation.
            bool localReady = frontend != null &&
                MultiplayerLocalReadyField.GetValue(frontend) is bool ready && ready;
            if (IsNetworkHost(frontend) && pendingPlayerIds.Count > 0 && localReady)
            {
                MultiplayerLocalReadyField.SetValue(frontend, false);
                Platform_Multiplayer.Instance.SetMemberReadyState(false);
            }
            UpdateHostReadyButton(frontend);
        }

        private void UpdateHostReadyButton(FRONT_Multiplayer frontend)
        {
            Button readyButton = frontend == null
                ? null
                : MultiplayerReadyButtonField.GetValue(frontend) as Button;
            if (!IsNetworkHost(frontend) || readyButton == null)
            {
                RestoreBlockedReadyButton();
                return;
            }

            bool pending = pendingPlayerIds.Count > 0;
            if (!pending)
            {
                RestoreBlockedReadyButton();
                return;
            }

            if (!ReferenceEquals(blockedReadyButton, readyButton))
            {
                RestoreBlockedReadyButton();
                blockedReadyButton = readyButton;
                blockedReadyButtonWasEnabled = readyButton.IsEnabled;
                blockedReadyButtonToolTip = readyButton.ToolTip;
            }

            readyButton.IsEnabled = false;
            readyButton.ToolTip = SerpLocalization.Get(SerpLocalization.AivPlacementChecking);
            ToolTipService.SetShowOnDisabled(readyButton, true);
        }

        private void LeaveLobbyContext()
        {
            if (!lobbyContextActive)
                return;

            lobbyContextActive = false;
            // Invalidate workers and UI state so a completed lobby check cannot leak into Trail selection.
            generations.Advance();
            while (completedEvaluations.TryDequeue(out _))
            {
            }
            currentResults.Clear();
            selectedNetworkCandidateIds.Clear();
            pendingPlayerIds.Clear();
            lastFingerprint = string.Empty;
            lastSourceFingerprint = string.Empty;
            nextSourcePollTimestamp = 0;
            selectionDialog.Reset();
            RestoreBlockedReadyButton();
            Shared.DebugLogHelper.LogInfo(log, "AIV placement lobby context left; runtime state reset.");
        }

        private void RestoreBlockedReadyButton()
        {
            if (blockedReadyButton == null)
                return;

            // Restore Vanilla's exact prior state instead of assuming that the button should be enabled.
            blockedReadyButton.IsEnabled = blockedReadyButtonWasEnabled;
            blockedReadyButton.ToolTip = blockedReadyButtonToolTip;
            blockedReadyButton = null;
            blockedReadyButtonToolTip = null;
        }

        private void LogContextIfChanged(FRONT_Multiplayer frontend, bool active)
        {
            MainViewModel viewModel = MainViewModel.Instance;
            Button readyButton = frontend == null
                ? null
                : MultiplayerReadyButtonField.GetValue(frontend) as Button;
            string fingerprint = string.Join(
                "|",
                active,
                viewModel?.Show_MultiplayerSetup == true,
                viewModel?.Show_MPGameCreation == true,
                viewModel?.SkirmishSetupMode == true,
                viewModel?.MultiplayerSetupMode == true,
                FRONT_Multiplayer.skirmishGame,
                FRONT_Multiplayer.coopGame,
                frontend?.singlePlayerCoop == true,
                frontend?.currentLobby?.isHost == true,
                readyButton?.Name ?? "none",
                readyButton?.IsEnabled == true);
            if (string.Equals(fingerprint, lastContextDiagnosticFingerprint, StringComparison.Ordinal))
                return;

            lastContextDiagnosticFingerprint = fingerprint;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AIV lobby context active={active}, multiplayerSetup={viewModel?.Show_MultiplayerSetup == true}, " +
                $"gameCreation={viewModel?.Show_MPGameCreation == true}, " +
                $"skirmishSetupMode={viewModel?.SkirmishSetupMode == true}, " +
                $"multiplayerSetupMode={viewModel?.MultiplayerSetupMode == true}, " +
                $"skirmishGame={FRONT_Multiplayer.skirmishGame}, coopGame={FRONT_Multiplayer.coopGame}, " +
                $"singlePlayerCoop={frontend?.singlePlayerCoop == true}, lobbyHost={frontend?.currentLobby?.isHost == true}, " +
                $"readyButton={readyButton?.Name ?? "none"}, readyEnabled={readyButton?.IsEnabled == true}, " +
                $"pendingPlacementChecks={pendingPlayerIds.Count}.");
        }

        private List<NetworkAivSnapshot> SelectNetworkStartAivs(FRONT_Multiplayer frontend)
        {
            var snapshots = new List<NetworkAivSnapshot>();
            if (frontend?.currentLobby?.members == null || frontend.AIVs == null)
                return snapshots;

            foreach (Platform_Multiplayer.MPLobbyMember member in frontend.currentLobby.members)
            {
                if (member == null || !member.SkirmishMember || member.SkirmishHumanMember)
                    continue;
                int playerId = frontend.currentLobby.getThisPlayerFromSteamID(member.GetSteamID());
                if (playerId < 1 || playerId > frontend.AIVs.Length)
                    continue;

                FRONT_Multiplayer.MPAIVInfo info = frontend.AIVs[playerId - 1];
                if (GetMode(info) != LobbyAivMode.Custom || info?.aivs == null || info.aivs.Count <= 1)
                    continue;

                var fullList = new List<CustomisationFileManager.CustomAIV>(info.aivs);
                var eligibleIds = new List<int>();
                if (currentResults.TryGetValue(playerId, out AivPlacementCheckResult result))
                    eligibleIds.AddRange(BestFitCandidateSelector.GetEligibleCandidateIds(result));
                if (eligibleIds.Count == 0)
                {
                    // Equally non-evaluable candidates retain the old unbiased multiplayer fallback.
                    for (int candidateId = 0; candidateId < fullList.Count; candidateId++)
                        eligibleIds.Add(candidateId);
                }

                eligibleIds.RemoveAll(candidateId => candidateId < 0 || candidateId >= fullList.Count);
                if (eligibleIds.Count == 0)
                    continue;

                int selectedCandidateId;
                if (!selectedNetworkCandidateIds.TryGetValue(playerId, out selectedCandidateId) ||
                    !eligibleIds.Contains(selectedCandidateId))
                {
                    lock (randomSync)
                        selectedCandidateId = eligibleIds[random.Next(eligibleIds.Count)];
                }
                CustomisationFileManager.CustomAIV selected = fullList[selectedCandidateId];
                snapshots.Add(new NetworkAivSnapshot(info, fullList));
                info.aivs.Clear();
                info.aivs.Add(selected);

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Selected multiplayer-start AIV playerId={playerId}, " +
                    $"status={result?.Status.ToString() ?? "NotEvaluable"}, " +
                    $"eligibleTies={eligibleIds.Count}, candidateId={selectedCandidateId}, " +
                    $"name={selected.AIVName}, checksum={selected.checksum}.");
            }
            return snapshots;
        }

        private static void RestoreNetworkStartAivs(List<NetworkAivSnapshot> snapshots)
        {
            if (snapshots == null)
                return;
            foreach (NetworkAivSnapshot snapshot in snapshots)
            {
                snapshot.Info.aivs.Clear();
                snapshot.Info.aivs.AddRange(snapshot.FullList);
            }
        }

        private static bool IsNetworkHost(FRONT_Multiplayer frontend) =>
            !FRONT_Multiplayer.skirmishGame &&
            frontend?.currentLobby != null &&
            frontend.currentLobby.isHost;

        private static bool IsLobbySetupActive()
        {
            MainViewModel viewModel = MainViewModel.Instance;
            // Vanilla's setup panel is the positive lobby signal. Coop Trail pages also prepare
            // it in the background; only an explicit Skirmish-style customization may opt in.
            return viewModel?.Show_MultiplayerSetup == true &&
                viewModel.Show_MPGameCreation == true &&
                (!FRONT_Multiplayer.coopGame || FRONT_Multiplayer.skirmishGame);
        }

        private sealed class CompletedEvaluation
        {
            public CompletedEvaluation(
                long generation,
                int playerId,
                AivPlacementCheckResult result,
                Exception error,
                int? selectedCandidateId)
            {
                Generation = generation;
                PlayerId = playerId;
                Result = result;
                Error = error;
                SelectedCandidateId = selectedCandidateId;
            }

            public long Generation { get; }
            public int PlayerId { get; }
            public AivPlacementCheckResult Result { get; }
            public Exception Error { get; }
            public int? SelectedCandidateId { get; }
        }

        private sealed class NetworkAivSnapshot
        {
            public NetworkAivSnapshot(
                FRONT_Multiplayer.MPAIVInfo info,
                List<CustomisationFileManager.CustomAIV> fullList)
            {
                Info = info;
                FullList = fullList;
            }

            public FRONT_Multiplayer.MPAIVInfo Info { get; }
            public List<CustomisationFileManager.CustomAIV> FullList { get; }
        }
    }
}
