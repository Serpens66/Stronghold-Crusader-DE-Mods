using APIShared;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace AIBuildDiagnoseTest
{
    internal sealed unsafe class AIBuildDiagnoseRuntime : IAivBuildStepObserver
    {
        private readonly ManualLogSource log;
        private readonly bool fixesLoaded;
        private readonly IDisposable buildingSubscription;
        private readonly IDisposable buildStructureSubscription;
        private readonly Dictionary<long, Attempt> attempts = new Dictionary<long, Attempt>();
        private readonly Dictionary<string, int> outcomes = new Dictionary<string, int>();
        private readonly Dictionary<string, int> seen = new Dictionary<string, int>();
        private readonly int[] schedulerCalls = new int[9];
        private readonly int[] woodBuildCalls = new int[9];
        private readonly int[] woodSearchCalls = new int[9];
        private readonly int[] hutSpawns = new int[9];
        private readonly int[] initialHuts = new int[9];
        private readonly string[] lastObservedStage = new string[9];
        private bool active;
        private bool firstTick;
        private long sessionId;
        private int lastTick;
        private int nextSummaryTick;

        internal AIBuildDiagnoseRuntime(ManualLogSource logger, bool hasFixes)
        {
            log = logger ?? throw new ArgumentNullException(nameof(logger));
            fixesLoaded = hasFixes;
            buildingSubscription = BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn);
            buildStructureSubscription = BuildingR3EventHooks.OnBuildStructure.Observable.Subscribe(OnBuildStructure);
            if (ApiShared.Current.TryGetAivBuildStep(AIBuildDiagnosePlugin.Guid,
                out IAivBuildStepCapability steps, out NativeCapabilityDiagnostic diagnostic))
            {
                if (!steps.TryRegisterObserver("wood-build-context", this, out diagnostic))
                    Log("AI_BUILD_AIV_OBSERVER_UNAVAILABLE: " + diagnostic?.Reason);
            }
            else Log("AI_BUILD_AIV_OBSERVER_UNAVAILABLE: " + diagnostic?.Reason);
        }

        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            sessionId = session.SessionId;
            active = !session.IsEditor && !session.IsReplay;
            firstTick = false;
            lastTick = -1;
            nextSummaryTick = 0;
            seen.Clear();
            attempts.Clear();
            outcomes.Clear();
            Array.Clear(schedulerCalls, 0, schedulerCalls.Length);
            Array.Clear(woodBuildCalls, 0, woodBuildCalls.Length);
            Array.Clear(woodSearchCalls, 0, woodSearchCalls.Length);
            Array.Clear(hutSpawns, 0, hutSpawns.Length);
            Array.Clear(initialHuts, 0, initialHuts.Length);
            Array.Clear(lastObservedStage, 0, lastObservedStage.Length);
            Log($"AI_BUILD_SESSION: session={sessionId}, kind={session.Kind}, loadedSave={session.IsLoadedSave}, " +
                $"file={session.SaveFileName}, fixesLoaded={fixesLoaded}, active={active}, mode={session.Mode.ToDiagnosticString()}.");
            if (active)
            {
                CaptureInitialHuts();
                LogPlayers("start");
            }
        }

        internal void OnSessionEnded()
        {
            if (active) LogSummary("end");
            active = false;
        }

        internal void OnTick(int tick)
        {
            if (!active) return;
            lastTick = tick;
            if (!firstTick)
            {
                firstTick = true;
                Log($"AI_BUILD_POST_STARTUP_TICK: session={sessionId}, tick={tick}.");
                LogPlayers("first-tick");
            }
            if (tick >= nextSummaryTick)
            {
                LogSummary("periodic");
                nextSummaryTick = tick + 500;
            }
        }

        internal void OnNativeRecord(AiBuildDiagnosticRecord record)
        {
            if (!active || record.PlayerId < 1 || record.PlayerId > 8) return;
            lastObservedStage[record.PlayerId] = record.Stage;
            switch (record.Stage)
            {
                case "scheduler-before": schedulerCalls[record.PlayerId]++; break;
                case "wood-build-before":
                    if (woodBuildCalls[record.PlayerId]++ == 0)
                        LogResources(record.PlayerId, "first-wood-build");
                    break;
                case "wood-search-before": woodSearchCalls[record.PlayerId]++; break;
            }
            Attempt attempt = null;
            if (record.AttemptId != 0)
            {
                if (record.Stage == "wood-build-before")
                    attempts[record.AttemptId] = new Attempt(record.PlayerId);
                attempts.TryGetValue(record.AttemptId, out attempt);
                if (attempt != null)
                {
                    if (record.Stage == "route-result") { attempt.RouteSeen = true; attempt.RouteResult = (int)record.A; }
                    if (record.Stage == "wood-search-after") attempt.SearchX = (int)record.B;
                    if (record.Stage == "wood-nearby-after") attempt.NearX = (int)record.A;
                    if (record.Stage != "wood-build-after") attempt.LastStep = record.Stage;
                }
            }
            string key = record.PlayerId + ":" + record.Stage + ":" + record.A + ":" + record.B + ":" + record.C + ":" + record.D;
            if (!seen.TryGetValue(key, out int count)) count = 0;
            seen[key] = count + 1;
            // First occurrence of each exact state is retained; periodic repeats show persistence.
            if (count < 2 || count == 9 || count == 99 || count % 500 == 499)
                Log($"AI_BUILD_TRACE: session={sessionId}, tick={lastTick}, player={record.PlayerId}, " +
                    $"attempt={record.AttemptId}, stage={record.Stage}, {Describe(record)}, repeat={count + 1}.");
            if (record.Stage == "wood-build-after" && attempt != null)
            {
                string outcome = Classify(attempt);
                string outcomeKey = record.PlayerId + ":" + outcome;
                outcomes.TryGetValue(outcomeKey, out int outcomeCount);
                outcomes[outcomeKey] = ++outcomeCount;
                if (outcomeCount <= 2 || outcomeCount == 10 || outcomeCount % 100 == 0)
                    Log($"AI_BUILD_ATTEMPT: session={sessionId}, tick={lastTick}, player={record.PlayerId}, " +
                        $"attempt={record.AttemptId}, observedLast={attempt.LastStep}, inference={outcome}, " +
                        $"route={(attempt.RouteSeen ? attempt.RouteResult.ToString() : "unobserved")}, " +
                        $"buildPre={attempt.BuildPre}, buildPost={attempt.BuildPost}, " +
                        $"spawnPre={attempt.SpawnPre}, spawnPost={attempt.SpawnPost}, spawnId={attempt.SpawnId}.");
                attempts.Remove(record.AttemptId);
            }
        }

        private void OnBuildStructure(BuildStructureEventArgs args)
        {
            if (!active || args.Mappers != eMappers.MAPPER_WOODSMAN ||
                !AiBuildDiagnostic.TryGetCurrentWoodAttempt(out long id, out int owner) ||
                owner != args.PlayerId || !attempts.TryGetValue(id, out Attempt attempt)) return;
            if (args.Phase == EventHookPhase.Pre) attempt.BuildPre = true;
            else if (args.Phase == EventHookPhase.Post) attempt.BuildPost = true;
            attempt.LastStep = "build-structure-" + args.Phase;
            string placement = AiBuildDiagnostic.TryReadPlacementStatus(out int preparation,
                out int rejected, out int mode)
                ? $"placementPreparation={preparation}, placementRejected={rejected}, placementMode={mode}"
                : "placementStatus=unavailable";
            try
            {
                int cost = GameBuildingManagerAPI.Instance.GetWoodCost(eStructs.STRUCT_WOODCUTTERS_HUT);
                Log($"AI_BUILD_STRUCTURE: session={sessionId}, tick={lastTick}, player={owner}, attempt={id}, " +
                    $"phase={args.Phase}, tile=({args.TileX},{args.TileY}), mapper={args.Mappers}, " +
                    $"scale={args.BuildingScaleUnknown}, free={args.IsFree}, woodCost={cost}, " +
                    $"{ReadResources(owner)}, {placement}.");
            }
            catch (Exception ex) { Log("AI_BUILD_STRUCTURE_OBSERVATION_FAILED: " + ex); }
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (!active ||
                args.Building != eStructs.STRUCT_WOODCUTTERS_HUT ||
                args.PlayerId < 1 || args.PlayerId > 8) return;
            long id = 0;
            if (AiBuildDiagnostic.TryGetCurrentWoodAttempt(out long current, out int owner) &&
                owner == args.PlayerId && attempts.TryGetValue(current, out Attempt attempt))
            {
                id = current;
                if (args.Phase == EventHookPhase.Pre) attempt.SpawnPre = true;
                else if (args.Phase == EventHookPhase.Post)
                {
                    attempt.SpawnPost = true;
                    attempt.SpawnId = args.ReturnValue;
                }
                attempt.LastStep = "building-spawn-" + args.Phase;
            }
            if (args.Phase == EventHookPhase.Post && args.ReturnValue > 0) hutSpawns[args.PlayerId]++;
            Log($"AI_BUILD_WOODCUTTER_SPAWN: session={sessionId}, tick={lastTick}, player={args.PlayerId}, " +
                $"attempt={id}, phase={args.Phase}, buildingId={(args.Phase == EventHookPhase.Post ? args.ReturnValue.ToString() : "pending")}, " +
                $"tile=({args.TileX},{args.TileY}).");
        }

        private static string Classify(Attempt attempt)
        {
            if (attempt.SpawnPost && attempt.SpawnId > 0) return "spawn-observed";
            if (attempt.SpawnPre) return "spawn-entered-without-successful-post";
            if (attempt.BuildPre) return "building-creation-entered-before-spawn-aborted";
            if (attempt.RouteSeen && attempt.RouteResult == 0) return "route-rejected";
            if (attempt.RouteSeen) return "route-accepted-building-event-unobserved";
            if (attempt.NearX >= 0) return "near-position-found-route-unobserved";
            if (attempt.SearchX >= 0) return "search-found-near-position-unobserved";
            return "search-or-earlier-exit";
        }

        private sealed class Attempt
        {
            internal Attempt(int playerId) { PlayerId = playerId; }
            internal int PlayerId;
            internal int SearchX = -1;
            internal int NearX = -1;
            internal bool RouteSeen;
            internal int RouteResult;
            internal bool BuildPre;
            internal bool BuildPost;
            internal bool SpawnPre;
            internal bool SpawnPost;
            internal long SpawnId;
            internal string LastStep = "wood-build-before";
        }

        private void LogPlayers(string phase)
        {
            try
            {
                var players = GamePlayerManagerAPI.Instance;
                for (int playerId = 1; playerId <= 8; playerId++)
                {
                    if (!players.IsAIPlayer(playerId)) continue;
                    Log($"AI_BUILD_PLAYER: session={sessionId}, phase={phase}, player={playerId}, " +
                        $"lord={players.GetAILord(playerId)}, {ReadResources(playerId)}.");
                }
            }
            catch (Exception ex) { Log("AI_BUILD_PLAYER_READ_FAILED: " + ex); }
        }

        private void LogSummary(string phase)
        {
            try
            {
                var players = GamePlayerManagerAPI.Instance;
                for (int playerId = 1; playerId <= 8; playerId++)
                {
                    if (!players.IsAIPlayer(playerId)) continue;
                    string outcomeText = "";
                    foreach (KeyValuePair<string, int> outcome in outcomes)
                        if (outcome.Key.StartsWith(playerId + ":", StringComparison.Ordinal))
                            outcomeText += outcome.Key.Substring(2) + "=" + outcome.Value + ",";
                    Log($"AI_BUILD_SUMMARY: session={sessionId}, phase={phase}, tick={lastTick}, " +
                        $"player={playerId}, lord={players.GetAILord(playerId)}, scheduler={schedulerCalls[playerId]}, " +
                        $"woodBuild={woodBuildCalls[playerId]}, woodSearch={woodSearchCalls[playerId]}, " +
                        $"initialHuts={initialHuts[playerId]}, newHuts={hutSpawns[playerId]}, " +
                        $"lastObserved={lastObservedStage[playerId] ?? "none"}, " +
                        $"inference={InferStage(playerId)}, attemptOutcomes={outcomeText}.");
                }
                LogPlayers(phase);
            }
            catch (Exception ex) { Log("AI_BUILD_SUMMARY_FAILED: " + ex); }
        }

        private void LogResources(int playerId, string phase)
        {
            try
            {
                Log($"AI_BUILD_RESOURCES: session={sessionId}, phase={phase}, tick={lastTick}, " +
                    $"player={playerId}, {ReadResources(playerId)}.");
            }
            catch (Exception ex) { Log("AI_BUILD_RESOURCE_READ_FAILED: " + ex); }
        }

        private static string ReadResources(int playerId)
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId,
                out GamePlayerResources* resources) || resources == null)
                return "resources=unavailable";
            return $"gold={resources->r_TotalGoodsGold}, woodLogs={resources->r_TotalGoodsWoodLogs}, " +
                $"woodPlanks={resources->r_TotalGoodsWoodPlanks}";
        }

        private void CaptureInitialHuts()
        {
            try
            {
                Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
                {
                    ref GameBuilding building = ref buildings[spanIndex];
                    int playerId = building.r_PlayerIdOwner;
                    if (playerId >= 1 && playerId <= 8 &&
                        building.r_AliveState == AliveState.IsAlive &&
                        building.r_BuildingType == eStructs.STRUCT_WOODCUTTERS_HUT)
                        initialHuts[playerId]++;
                }
            }
            catch (Exception ex) { Log("AI_BUILD_INITIAL_HUT_SCAN_FAILED: " + ex); }
        }

        private string InferStage(int playerId)
        {
            if (hutSpawns[playerId] > 0) return "woodcutter-spawn-observed";
            if (schedulerCalls[playerId] == 0) return "scheduler-not-observed";
            if (woodBuildCalls[playerId] == 0) return "wood-build-call-not-observed";
            if (woodSearchCalls[playerId] == 0) return "wood-build-returned-before-search";
            return "search-or-later-stage-see-trace";
        }

        private static string Describe(AiBuildDiagnosticRecord record)
        {
            switch (record.Stage)
            {
                case "scheduler-before":
                case "scheduler-after":
                    return $"villageSlot={record.A}, schedulerDelay={record.B}, economyPhase={record.C}";
                case "wood-build-before":
                    return $"fixSession={record.A}, fixEnabled={record.B}, loadedSave={record.C}, fixMapRelevant={record.D}";
                case "wood-search-before":
                case "wood-search-after":
                    return $"cooldown={record.A}, resultX={record.B}, resultY={record.C}";
                case "wood-nearby-before":
                    return $"coarseX={record.A}, coarseY={record.B}";
                case "wood-build-after":
                case "wood-nearby-after":
                    return $"resultX={record.A}, resultY={record.B}";
                case "wood-candidate-scan":
                    return $"visitedCells={record.A}, formalCandidates={record.B}, bestScore={record.C}, generation={record.D}";
                case "wood-candidate-scan-error":
                    return $"scanHResult={record.A}";
                case "route-components":
                    return $"sourceComponent={record.A}, targetComponent={record.B}, sourceTile={record.C}, targetTile={record.D}";
                case "route-result":
                    return $"result={record.A}, mapperIndex={record.B}, target=({record.C},{record.D})";
                default:
                    return $"a={record.A}, b={record.B}, c={record.C}, d={record.D}";
            }
        }

        public IAivBuildStepInvocation TryBegin(AivBuildStepContext context)
        {
            if (!active || context.PlayerId < 1 || context.PlayerId > 8) return null;
            return new BuildStepObservation(this, context.PlayerId, context.FrameIndex);
        }

        private sealed class BuildStepObservation : IAivBuildStepInvocation
        {
            private readonly AIBuildDiagnoseRuntime owner;
            private readonly int playerId;
            private readonly int frameIndex;
            internal BuildStepObservation(AIBuildDiagnoseRuntime owner, int playerId, int frameIndex)
            { this.owner = owner; this.playerId = playerId; this.frameIndex = frameIndex; }
            public void Complete(AivBuildStepCompletion completion)
            {
                if (!owner.active) return;
                string key = "aiv:" + playerId + ":" + frameIndex + ":" + completion.VanillaResult;
                if (!owner.seen.TryGetValue(key, out int count)) count = 0;
                owner.seen[key] = count + 1;
                if (count == 0)
                    owner.Log($"AI_BUILD_AIV_STEP: session={owner.sessionId}, tick={owner.lastTick}, " +
                        $"player={playerId}, frame={frameIndex}, completed={completion.VanillaCompleted}, result={completion.VanillaResult}.");
            }
        }

        private void Log(string value) => Shared.DebugLogHelper.LogInfo(log, value);
    }
}
