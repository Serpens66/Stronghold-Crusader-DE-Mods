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

namespace AIVPlacementLobby
{
    internal sealed class AIVPlacementLobbyRuntime
    {
        private static readonly FieldInfo SelectedMapHeaderField =
            FindField(typeof(FRONT_Multiplayer), "selectedMPHeader");
        private static readonly FieldInfo MultiplayerSetupDataField =
            FindField(typeof(FRONT_Multiplayer), "MPsetupData");

        private delegate void UpdateDelegate(FRONT_Multiplayer self);
        private delegate void StartSkirmishGameDelegate(
            FRONT_Multiplayer self,
            HUD_IngameMenu.RestartSkirmishMapInfo restartInfo);

        private readonly ManualLogSource log;
        private readonly LobbyRequestBuilder requestBuilder = new LobbyRequestBuilder();
        private readonly LobbyRequestGenerationGate generations = new LobbyRequestGenerationGate();
        private readonly AivPlacementEvaluationService evaluationService =
            new AivPlacementEvaluationService();
        private readonly ConcurrentQueue<CompletedEvaluation> completedEvaluations =
            new ConcurrentQueue<CompletedEvaluation>();
        private readonly Dictionary<string, string> assetOverrideCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string vanillaAivDirectory;
        private Hook updateHook;
        private Hook startHook;
        private UpdateDelegate updateTrampoline;
        private StartSkirmishGameDelegate startTrampoline;
        private string lastFingerprint = string.Empty;
        private string lastSourceFingerprint = string.Empty;
        private long nextSourcePollTimestamp;
        private bool captureFailureLogged;

        public AIVPlacementLobbyRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            string pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            vanillaAivDirectory = Path.Combine(pluginDirectory ?? string.Empty, "VanillaAIV");
        }

        public void Install()
        {
            MethodInfo update = FindMethod(typeof(FRONT_Multiplayer), "Update", Type.EmptyTypes);
            MethodInfo start = FindMethod(
                typeof(FRONT_Multiplayer),
                "StartSkirmishGame",
                new[] { typeof(HUD_IngameMenu.RestartSkirmishMapInfo) });
            updateHook = new Hook(update, (UpdateDelegate)UpdateHook);
            updateTrampoline = updateHook.GenerateTrampoline<UpdateDelegate>();
            startHook = new Hook(start, (StartSkirmishGameDelegate)StartSkirmishGameHook);
            startTrampoline = startHook.GenerateTrampoline<StartSkirmishGameDelegate>();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"AIV lobby data-flow hooks installed; vanillaAivDirectory={vanillaAivDirectory}.");
        }

        private void UpdateHook(FRONT_Multiplayer self)
        {
            updateTrampoline(self);
            CaptureIfChanged(self, false, "lobby update");
            PublishCompletedEvaluations();
        }

        private void StartSkirmishGameHook(
            FRONT_Multiplayer self,
            HUD_IngameMenu.RestartSkirmishMapInfo restartInfo)
        {
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
                LogBatch(batch, reason);
                QueueEvaluations(batch, assets);
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
            var assets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (lobby?.members != null && frontend.AIVs != null)
            {
                foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
                {
                    // Unused, spectator and human rows never produce AIV placement requests.
                    if (member == null || !member.SkirmishMember || member.SkirmishHumanMember)
                        continue;
                    int playerId = lobby.getThisPlayerFromSteamID(member.GetSteamID());
                    if (playerId < 1 || playerId > frontend.AIVs.Length)
                        continue;

                    FRONT_Multiplayer.MPAIVInfo info = frontend.AIVs[playerId - 1];
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
                assets);
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
            IReadOnlyDictionary<string, string> assets)
        {
            foreach (AivPlacementCheckRequest request in batch.Requests)
            {
                Task<AivPlacementCheckResult> task = evaluationService.EvaluateAsync(request, assets);
                task.ContinueWith(
                    completed =>
                    {
                        completedEvaluations.Enqueue(completed.Status == TaskStatus.RanToCompletion
                            ? new CompletedEvaluation(completed.Result, null)
                            : new CompletedEvaluation(null, completed.Exception));
                    },
                    TaskScheduler.Default);
            }
        }

        private void PublishCompletedEvaluations()
        {
            while (completedEvaluations.TryDequeue(out CompletedEvaluation completed))
            {
                if (completed.Error != null)
                {
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
                }
            }
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
                    $"aivMode={request.AivMode}, initialRotation={(int)request.InitialRotation}, " +
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

        private sealed class CompletedEvaluation
        {
            public CompletedEvaluation(AivPlacementCheckResult result, Exception error)
            {
                Result = result;
                Error = error;
            }

            public AivPlacementCheckResult Result { get; }
            public Exception Error { get; }
        }
    }
}
