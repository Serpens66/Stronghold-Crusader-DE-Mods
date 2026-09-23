using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AIVPlacement.Core;
using CastlePlanner.AIVPlacement.Core;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using Button = Noesis.Button;
using ToolTipService = Noesis.ToolTipService;

namespace CastlePlanner.AIVPlacement
{
    internal sealed class AivPlacementRuntime
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
        private readonly Func<bool> isEnabled;
        private readonly LobbyRequestBuilder requestBuilder = new LobbyRequestBuilder();
        private readonly LobbyRequestGenerationGate generations = new LobbyRequestGenerationGate();
        private readonly LobbyCapturePollGate capturePoll =
            new LobbyCapturePollGate(Stopwatch.Frequency);
        private readonly AivPlacementEvaluationService evaluationService =
            new AivPlacementEvaluationService();
        private readonly AivSelectionListViewModel selectionList = new AivSelectionListViewModel();
        private readonly AivSelectionDialogRuntime selectionDialog;
        private readonly ConcurrentQueue<CompletedEvaluation> completedEvaluations =
            new ConcurrentQueue<CompletedEvaluation>();
        private readonly ConcurrentQueue<CandidateProgress> candidateProgress =
            new ConcurrentQueue<CandidateProgress>();
        private readonly ConcurrentDictionary<string, byte> reportedWarnings =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, byte> reportedErrors =
            new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> assetOverrideCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string vanillaAivDirectory;
        private readonly Dictionary<int, AivPlacementCheckResult> currentResults =
            new Dictionary<int, AivPlacementCheckResult>();
        private readonly HashSet<int> pendingPlayerIds = new HashSet<int>();
        private Hook updateHook;
        private Hook startHook;
        private Hook buttonClickedHook;
        private UpdateDelegate updateTrampoline;
        private StartSkirmishGameDelegate startTrampoline;
        private ButtonClickedDelegate buttonClickedTrampoline;
        private string lastFingerprint = string.Empty;
        private string lastSourceFingerprint = string.Empty;
        private long nextSourcePollTimestamp;
        private long nextProgressPublishTimestamp;
        private CancellationTokenSource evaluationCancellation;
        private bool lobbyContextActive;
        private bool lobbySetupObserved;
        private bool lastLobbyFeatureEnabled;
        private Button blockedReadyButton;
        private bool blockedReadyButtonWasEnabled;
        private object blockedReadyButtonToolTip;

        public AivPlacementRuntime(ManualLogSource log, Func<bool> isEnabled)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.isEnabled = isEnabled ?? throw new ArgumentNullException(nameof(isEnabled));
            string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            vanillaAivDirectory = Path.Combine(pluginDirectory ?? string.Empty, "VanillaAIV");
            selectionDialog = new AivSelectionDialogRuntime(
                log, selectionList, isEnabled, RequestRefresh);
        }

        public object SelectionList => selectionList;

        public void RequestRefresh()
        {
            nextSourcePollTimestamp = 0;
            capturePoll.Invalidate();
        }

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
            try
            {
                updateHook = new Hook(update, (UpdateDelegate)UpdateHook);
                updateTrampoline = updateHook.GenerateTrampoline<UpdateDelegate>();
                startHook = new Hook(start, (StartSkirmishGameDelegate)StartSkirmishGameHook);
                startTrampoline = startHook.GenerateTrampoline<StartSkirmishGameDelegate>();
                buttonClickedHook = new Hook(buttonClicked, (ButtonClickedDelegate)ButtonClickedHook);
                buttonClickedTrampoline = buttonClickedHook.GenerateTrampoline<ButtonClickedDelegate>();
                selectionDialog.Install();
            }
            catch
            {
                // Hook installation is transactional: never leave a partial lobby detour set.
                Deactivate();
                selectionDialog.Dispose();
                ReleaseHook(ref buttonClickedHook);
                buttonClickedTrampoline = null;
                ReleaseHook(ref startHook);
                startTrampoline = null;
                ReleaseHook(ref updateHook);
                updateTrampoline = null;
                throw;
            }
        }

        public void Deactivate()
        {
            // This is also called directly by the setting-change callback, so the ready button
            // and worker state are restored without waiting for another frontend Update.
            if (!lobbyContextActive && blockedReadyButton == null && evaluationCancellation == null)
                return;

            lobbyContextActive = true;
            LeaveLobbyContext();
        }

        private void ReleaseHook(ref Hook hook)
        {
            Hook current = hook;
            hook = null;
            if (current == null)
                return;

            try { current.Undo(); }
            catch (Exception ex) { LogErrorOnce("hook-undo", $"AIV placement hook undo failed: {ex}"); }
            try { current.Dispose(); }
            catch (Exception ex) { LogErrorOnce("hook-dispose", $"AIV placement hook disposal failed: {ex}"); }
        }

        private void UpdateHook(FRONT_Multiplayer self)
        {
            updateTrampoline(self);
            try
            {
                bool setupVisible = IsLobbySetupContext();
                if (!setupVisible)
                {
                    lobbySetupObserved = false;
                    LeaveLobbyContext();
                    return;
                }

                bool featureEnabled = isEnabled();
                if (!lobbySetupObserved || lastLobbyFeatureEnabled != featureEnabled)
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"AIV lobby placement: active={featureEnabled}, " +
                        $"host={self?.currentLobby?.isHost == true}.");
                    lobbySetupObserved = true;
                    lastLobbyFeatureEnabled = featureEnabled;
                }
                if (!featureEnabled)
                {
                    LeaveLobbyContext();
                    return;
                }

                lobbyContextActive = true;
                CaptureIfChanged(self, false);
                PublishCandidateProgress(false);
                PublishCompletedEvaluations();
                UpdateHostReadyButton(self);
            }
            catch (Exception ex)
            {
                LogErrorOnce("frontend-update", $"AIV lobby frontend update failed: {ex}");
            }
        }

        private void ButtonClickedHook(FRONT_Multiplayer self, string param)
        {
            bool lobbySetupActive;
            try
            {
                lobbySetupActive = IsLobbySetupActive();
            }
            catch (Exception ex)
            {
                LogErrorOnce("button-context", $"AIV lobby button context check failed: {ex}");
                buttonClickedTrampoline(self, param);
                return;
            }

            if (!lobbySetupActive)
            {
                buttonClickedTrampoline(self, param);
                return;
            }

            try
            {
                bool networkHost = IsNetworkHost(self);
                if (networkHost && pendingPlayerIds.Count > 0 &&
                    (string.Equals(param, "Ready", StringComparison.Ordinal) ||
                     string.Equals(param, "Play", StringComparison.Ordinal)))
                {
                    UpdateHostReadyButton(self);
                    return;
                }
            }
            catch (Exception ex)
            {
                LogErrorOnce("button-preparation", $"AIV lobby button preparation failed; Vanilla continues: {ex}");
                buttonClickedTrampoline(self, param);
                return;
            }

            try
            {
                buttonClickedTrampoline(self, param);
            }
            finally
            {
                capturePoll.Invalidate();
            }
        }

        private void StartSkirmishGameHook(
            FRONT_Multiplayer self,
            HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
        {
            bool lobbySetupActive;
            try
            {
                lobbySetupActive = IsLobbySetupActive();
            }
            catch (Exception ex)
            {
                LogErrorOnce("start-context", $"AIV lobby start context check failed; Vanilla continues unchanged: {ex}");
                startTrampoline(self, restartInfo);
                return;
            }

            if (!lobbySetupActive)
            {
                startTrampoline(self, restartInfo);
                return;
            }

            // Capture before Vanilla rewrites preferredAIVs and transfers setup into native state.
            CaptureIfChanged(self, true);
            startTrampoline(self, restartInfo);
        }

        private void CaptureIfChanged(FRONT_Multiplayer frontend, bool force)
        {
            long now = Stopwatch.GetTimestamp();
            if (!capturePoll.ShouldCapture(now, force))
                return;

            try
            {
                LobbyStateCapture capture = Capture(frontend);
                string fingerprint = LobbyRequestBuilder.BuildFingerprint(capture);
                bool stateChanged = !string.Equals(
                    fingerprint,
                    lastFingerprint,
                    StringComparison.Ordinal);
                // Frontend button actions invalidate the gate immediately; the periodic
                // check also catches map, lobby and file changes without a reliable event.
                if (!force && !stateChanged && now < nextSourcePollTimestamp)
                    return;

                nextSourcePollTimestamp = now + Stopwatch.Frequency;
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
                QueueEvaluations(
                    batch,
                    assets,
                    evaluationCancellation);
            }
            catch (Exception ex)
            {
                LogErrorOnce("lobby-capture", $"Lobby capture failed; Vanilla remains unchanged: {ex}");
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
                    {
                        LogWarningOnce(
                            $"capture-player-id-{playerId}",
                            $"Skipped an active lobby member with invalid playerId={playerId}; aivSlots={frontend.AIVs.Length}.");
                        continue;
                    }
                    if (member.SkirmishHumanMember)
                    {
                        humanPlayerIds.Add(playerId);
                        continue;
                    }

                    FRONT_Multiplayer.MPAIVInfo info = frontend.AIVs[playerId - 1];
                    if (info == null)
                    {
                        LogWarningOnce(
                            $"capture-missing-aiv-info-{playerId}",
                            $"AI lobby slot playerId={playerId} has no AIV state; placement remains not evaluable until Vanilla supplies it.");
                    }
                    else
                    {
                        playerMappings[info] = playerId;
                    }
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
                        GameAssetManagerAPI assetManager = GameAssetManagerAPI.Instance;
                        if (assetManager == null)
                        {
                            LogWarningOnce(
                                "asset-manager-unavailable",
                                "GameAssetManagerAPI.Instance is unavailable while capturing AIV overrides; embedded Vanilla AIV files are used.");
                            content = null;
                        }
                        else if (!assetManager.GetModifiedFileTextContent(asset, out content))
                        {
                            content = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogWarningOnce(
                            $"asset-read-{asset}",
                            $"Reading Script Extender AIV override {asset} failed; the embedded Vanilla AIV file is used: {ex}");
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
            CancellationTokenSource cancellation)
        {
            Task<AivPlacementBatchResult> task = evaluationService.EvaluateBatchAsync(
                batch,
                assets,
                null,
                cancellation.Token,
                (generation, playerId, candidate) =>
                    candidateProgress.Enqueue(new CandidateProgress(generation, playerId, candidate)));
            task.ContinueWith(
                completed => HandleEvaluationCompletion(batch, completed),
                TaskScheduler.Default);
        }

        private void HandleEvaluationCompletion(
            AivPlacementRequestBatch batch,
            Task<AivPlacementBatchResult> completed)
        {
            // Superseded generations are expected and must not become UI failures.
            if (completed.IsCanceled)
                return;

            if (completed.Status != TaskStatus.RanToCompletion)
            {
                Exception error = completed.Exception;
                if (error == null)
                {
                    error = new InvalidOperationException(
                        $"Evaluation task ended with unexpected status {completed.Status}.");
                }
                QueueBatchFailure(batch, error);
                return;
            }

            try
            {
                AivPlacementBatchResult batchResult = completed.Result ??
                    throw new InvalidOperationException("Evaluation task returned no batch result.");
                var expectedPlayerIds = new HashSet<int>(
                    batch.Requests.Select(request => request.PlayerId));
                var returnedPlayerIds = new HashSet<int>();

                foreach (AivPlacementCheckResult result in batchResult.Results)
                {
                    if (result == null)
                    {
                        LogErrorOnce(
                            "null-evaluation-result",
                            "Lobby placement evaluation returned a null player result.");
                        continue;
                    }
                    if (!expectedPlayerIds.Contains(result.PlayerId))
                    {
                        LogErrorOnce(
                            $"unexpected-result-player-{result.PlayerId}",
                            $"Lobby placement evaluation returned unexpected playerId={result.PlayerId} for generation={batch.Generation}.");
                        continue;
                    }
                    if (!returnedPlayerIds.Add(result.PlayerId))
                    {
                        LogErrorOnce(
                            $"duplicate-result-player-{result.PlayerId}",
                            $"Lobby placement evaluation returned duplicate playerId={result.PlayerId} for generation={batch.Generation}.");
                        continue;
                    }

                    completedEvaluations.Enqueue(new CompletedEvaluation(
                        result.Generation,
                        result.PlayerId,
                        result,
                        null));
                }

                foreach (AivPlacementCheckRequest request in batch.Requests)
                {
                    if (returnedPlayerIds.Contains(request.PlayerId))
                        continue;
                    var error = new InvalidOperationException(
                        $"Evaluation returned no result for generation={batch.Generation}, playerId={request.PlayerId}.");
                    LogErrorOnce(
                        $"missing-result-player-{request.PlayerId}",
                        error.Message);
                    completedEvaluations.Enqueue(new CompletedEvaluation(
                        request.Generation,
                        request.PlayerId,
                        null,
                        error));
                }
            }
            catch (Exception ex)
            {
                QueueBatchFailure(batch, ex);
            }
        }

        private void QueueBatchFailure(AivPlacementRequestBatch batch, Exception error)
        {
            LogErrorOnce(
                $"evaluation-{error.GetType().FullName}-{error.Message}",
                $"Asynchronous lobby placement evaluation failed: {error}");
            foreach (AivPlacementCheckRequest request in batch.Requests)
            {
                completedEvaluations.Enqueue(new CompletedEvaluation(
                    request.Generation,
                    request.PlayerId,
                    null,
                    error));
            }
        }

        private void PublishCompletedEvaluations()
        {
            if (!completedEvaluations.IsEmpty)
                PublishCandidateProgress(true);
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
                    continue;
                }

                AivPlacementCheckResult result = completed.Result;
                if (result == null)
                {
                    LogErrorOnce(
                        "queued-null-result",
                        $"Queued lobby evaluation has neither result nor error for generation={completed.Generation}, playerId={completed.PlayerId}.");
                    continue;
                }
                if (!generations.IsCurrent(result.Generation))
                    continue;

                LogUnexpectedEvaluationFailure(result);
                if (!pendingPlayerIds.Remove(result.PlayerId))
                {
                    LogWarningOnce(
                        $"result-not-pending-{result.PlayerId}",
                        $"Received a current lobby placement result for non-pending playerId={result.PlayerId}, generation={result.Generation}.");
                }
                currentResults[result.PlayerId] = result;
                selectionDialog.Publish(result);
                int evaluableCandidates = result.Candidates.Count(candidate =>
                    candidate.Status != AivPlacementStatus.NotEvaluable);
                IReadOnlyList<NativeAivAutoDecision> possible =
                    NativeAivAutoSelector.SelectPossible(result.Candidates);
                string possibleCandidates = possible.Count == 0
                    ? "unproven"
                    : string.Join(",", possible.Select(outcome =>
                        outcome.CandidateId?.ToString() ?? "none").Distinct());
                string possibleRotations = possible.Count == 0
                    ? "unproven"
                    : string.Join(",", possible.Select(outcome =>
                    {
                        AivPlacementCandidateEvaluation candidate = result.Candidates
                            .FirstOrDefault(value => value.CandidateId == outcome.CandidateId);
                        return candidate?.Selection != null && outcome.RotationIndex >= 0 &&
                               outcome.RotationIndex < candidate.Selection.Variants.Count
                            ? candidate.Selection.Variants[outcome.RotationIndex].Rotation.ToString()
                            : "none";
                    }).Distinct());
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"AIV lobby placement result: generation={result.Generation}, " +
                    $"playerId={result.PlayerId}, preBuild={result.PreBuildSetting}, " +
                    $"status={result.Status}, " +
                    $"evaluableCandidates={evaluableCandidates}/{result.Candidates.Count}, " +
                    $"selectedCandidate={result.SelectedCandidate?.CandidateId.ToString() ?? "none"}, " +
                    $"rotation={result.SelectedVariant?.Rotation.ToString() ?? "unknown"}, " +
                    $"possibleCandidates={possibleCandidates}, " +
                    $"possibleRotations={possibleRotations}, " +
                    $"reason={result.FailureKind}: {result.FailureMessage}.");
            }
        }

        private void PublishCandidateProgress(bool force)
        {
            long now = Stopwatch.GetTimestamp();
            if (!force && now < nextProgressPublishTimestamp)
                return;
            if (candidateProgress.IsEmpty)
                return;
            nextProgressPublishTimestamp = now + Stopwatch.Frequency;
            var byPlayer = new Dictionary<int, List<AivPlacementCandidateEvaluation>>();
            while (candidateProgress.TryDequeue(out CandidateProgress progress))
            {
                if (!generations.IsCurrent(progress.Generation))
                    continue;
                if (!byPlayer.TryGetValue(progress.PlayerId,
                        out List<AivPlacementCandidateEvaluation> evaluations))
                {
                    evaluations = new List<AivPlacementCandidateEvaluation>();
                    byPlayer.Add(progress.PlayerId, evaluations);
                }
                evaluations.Add(progress.Candidate);
            }
            foreach (KeyValuePair<int, List<AivPlacementCandidateEvaluation>> pair in byPlayer)
                selectionDialog.PublishCandidates(pair.Key, pair.Value);
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
            CancelEvaluation();
            evaluationCancellation = new CancellationTokenSource();
            currentResults.Clear();
            pendingPlayerIds.Clear();
            while (candidateProgress.TryDequeue(out _))
            {
            }
            nextProgressPublishTimestamp = 0;
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
            CancelEvaluation();
            while (completedEvaluations.TryDequeue(out _))
            {
            }
            while (candidateProgress.TryDequeue(out _))
            {
            }
            currentResults.Clear();
            pendingPlayerIds.Clear();
            lastFingerprint = string.Empty;
            lastSourceFingerprint = string.Empty;
            nextSourcePollTimestamp = 0;
            nextProgressPublishTimestamp = 0;
            capturePoll.Invalidate();
            selectionDialog.Reset();
            RestoreBlockedReadyButton();
        }

        private void CancelEvaluation()
        {
            CancellationTokenSource previous = evaluationCancellation;
            evaluationCancellation = null;
            if (previous == null)
                return;

            previous.Cancel();
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


        private void LogUnexpectedEvaluationFailure(AivPlacementCheckResult result)
        {
            LobbyEvaluationLogSeverity severity = LobbyEvaluationLogPolicy.Classify(result);
            if (severity == LobbyEvaluationLogSeverity.None)
                return;

            string message =
                $"Lobby placement is unexpectedly not evaluable: playerId={result.PlayerId}, " +
                $"generation={result.Generation}, reason={result.FailureKind}, detail={result.FailureMessage}.";
            string key = $"evaluation-result-{result.PlayerId}-{result.FailureKind}-{result.FailureMessage}";
            if (severity == LobbyEvaluationLogSeverity.Error)
                LogErrorOnce(key, message);
            else
                LogWarningOnce(key, message);
        }

        private void LogWarningOnce(string key, string message)
        {
            if (reportedWarnings.TryAdd(key ?? string.Empty, 0))
                Shared.DebugLogHelper.LogWarning(log, message);
        }

        private void LogErrorOnce(string key, string message)
        {
            if (reportedErrors.TryAdd(key ?? string.Empty, 0))
                Shared.DebugLogHelper.LogError(log, message);
        }

        private static bool IsNetworkHost(FRONT_Multiplayer frontend) =>
            !FRONT_Multiplayer.skirmishGame &&
            frontend?.currentLobby != null &&
            frontend.currentLobby.isHost;

        private bool IsLobbySetupActive()
        {
            return isEnabled() && IsLobbySetupContext();
        }

        private static bool IsLobbySetupContext()
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
                Exception error)
            {
                Generation = generation;
                PlayerId = playerId;
                Result = result;
                Error = error;
            }

            public long Generation { get; }
            public int PlayerId { get; }
            public AivPlacementCheckResult Result { get; }
            public Exception Error { get; }
        }

        private sealed class CandidateProgress
        {
            public CandidateProgress(
                long generation,
                int playerId,
                AivPlacementCandidateEvaluation candidate)
            {
                Generation = generation;
                PlayerId = playerId;
                Candidate = candidate;
            }

            public long Generation { get; }
            public int PlayerId { get; }
            public AivPlacementCandidateEvaluation Candidate { get; }
        }
    }
}
