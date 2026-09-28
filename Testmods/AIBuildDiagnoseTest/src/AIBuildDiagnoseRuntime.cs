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
        private readonly Dictionary<string, int> routeCauses = new Dictionary<string, int>();
        private readonly Dictionary<string, int> nearbyCauses = new Dictionary<string, int>();
        private readonly HashSet<string> detailedRoutes = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> detailedNearby = new HashSet<string>(StringComparer.Ordinal);
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
            routeCauses.Clear();
            nearbyCauses.Clear();
            detailedRoutes.Clear();
            detailedNearby.Clear();
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
                    if (record.Stage == "route-evidence") attempt.RouteEvidence = record.RouteEvidence;
                    if (record.Stage == "wood-nearby-path-before")
                        attempt.NearbyBefore = record.NearbyPathEvidence;
                    if (record.Stage == "wood-nearby-path-after")
                        attempt.NearbyAfter = record.NearbyPathEvidence;
                    if (record.Stage == "near-region-result" && record.NearbyRegionEvidence != null)
                    {
                        AiNearbyRegionEvidence region = record.NearbyRegionEvidence;
                        attempt.NearbyRegionCallCount++;
                        attempt.AnyRegionOverride |= region.VanillaResult != region.EffectiveResult;
                        attempt.AnyRejectedZeroTarget |= region.Target == 0 &&
                            region.VanillaResult == 0 && region.EffectiveResult == 0;
                        if (attempt.NearbyRegions.Count == 128)
                            attempt.NearbyRegions.RemoveAt(64); // Keep first 64 and most recent 64.
                        attempt.NearbyRegions.Add(region);
                    }
                    if (record.Stage == "wood-search-after") attempt.SearchX = (int)record.B;
                    if (record.Stage == "wood-nearby-after") attempt.NearX = (int)record.A;
                    if (record.Stage != "wood-build-after") attempt.LastStep = record.Stage;
                }
            }
            if (record.Stage != "near-region-result")
            {
                string key = record.PlayerId + ":" + record.Stage + ":" + record.A + ":" + record.B + ":" + record.C + ":" + record.D;
                if (!seen.TryGetValue(key, out int count)) count = 0;
                seen[key] = count + 1;
                // First occurrence of each exact state is retained; periodic repeats show persistence.
                if (count < 2 || count == 9 || count == 99 || count % 500 == 499)
                    Log($"AI_BUILD_TRACE: session={sessionId}, tick={lastTick}, player={record.PlayerId}, " +
                        $"attempt={record.AttemptId}, stage={record.Stage}, {Describe(record)}, repeat={count + 1}.");
            }
            if (record.Stage == "wood-build-after" && attempt != null)
            {
                string outcome = Classify(attempt);
                string routeCause = attempt.RouteSeen && attempt.RouteResult == 0
                    ? AnalyzeRoute(attempt.RouteEvidence) : "not-rejected";
                string nearbyCause = AnalyzeNearby(attempt);
                if (attempt.NearbyAfter != null)
                    LogNearbyEvidence(record.PlayerId, record.AttemptId, attempt, nearbyCause);
                if (attempt.RouteSeen && attempt.RouteResult == 0)
                {
                    string causeKey = record.PlayerId + ":" + routeCause;
                    routeCauses.TryGetValue(causeKey, out int causeCount);
                    routeCauses[causeKey] = causeCount + 1;
                    LogRouteEvidence(record.PlayerId, record.AttemptId, attempt.RouteEvidence, routeCause);
                }
                string outcomeKey = record.PlayerId + ":" + outcome;
                outcomes.TryGetValue(outcomeKey, out int outcomeCount);
                outcomes[outcomeKey] = ++outcomeCount;
                if (outcomeCount <= 2 || outcomeCount == 10 || outcomeCount % 100 == 0)
                    Log($"AI_BUILD_ATTEMPT: session={sessionId}, tick={lastTick}, player={record.PlayerId}, " +
                        $"attempt={record.AttemptId}, observedLast={attempt.LastStep}, inference={outcome}, " +
                        $"routeCause={routeCause}, " +
                        $"nearbyCause={nearbyCause}, " +
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

        private static string AnalyzeRoute(AiRouteEvidence evidence)
        {
            if (evidence == null) return "snapshot-unobserved";
            if (evidence.Status != "ok") return "snapshot-unavailable:" + evidence.Status;
            if (evidence.Bypass != 0) return "route-bypass-result-divergence";
            if (evidence.SourceComponent <= 0) return "source-component-result-divergence";
            if (evidence.TargetComponent == 0) return "target-component-zero";
            if (evidence.TargetComponent < 0) return "target-component-invalid";
            if (evidence.SourceComponent == evidence.TargetComponent)
                return "same-component-result-divergence";
            if (!CanReach(evidence, (connection, snapshot) => true)) return "no-macro-connection";
            if (!CanReach(evidence, (connection, snapshot) => connection.Active == 1))
                return "inactive-connection-filter";
            if (!CanReach(evidence, (connection, snapshot) =>
                connection.Active == 1 && connection.Open != 0))
                return "closed-connection-filter";
            if (!CanReach(evidence, (connection, snapshot) =>
                connection.Active == 1 && connection.Open != 0 && connection.ConnectionClass != 1))
                return "ladder-class-filter";
            if (CanReach(evidence, IsVanillaEligible)) return "eligible-graph-result-divergence";
            foreach (AiRouteConnection connection in evidence.Connections)
                if (connection.Active == 1 && connection.Open != 0 &&
                    connection.ConnectionClass != 1 &&
                    connection.OwnerToken == int.MinValue &&
                    (connection.GateFlag == 0 || connection.GateFlag == int.MinValue))
                    return "connection-filter-or-unavailable-access-data";
            return "player-access-filter";
        }

        private static bool IsVanillaEligible(AiRouteConnection connection,
            AiRouteEvidence evidence) => connection.Active == 1 && connection.Open != 0 &&
            connection.ConnectionClass != 1 &&
            (connection.OwnerToken == evidence.PlayerToken ||
             connection.GateFlag != 0 && connection.GateFlag != int.MinValue);

        private static AiPathTileSample FindAnchor(AiNearbyPathEvidence evidence, int x, int y)
        {
            if (evidence == null) return null;
            foreach (AiPathTileSample sample in evidence.Anchors)
                if (sample.X == x && sample.Y == y) return sample;
            return null;
        }

        private static bool HasViewDifference(AiNearbyPathEvidence evidence)
        {
            if (evidence == null) return false;
            foreach (AiPathTileSample sample in evidence.Anchors)
                if (sample.Status == "ok" && sample.NativeComponent != sample.ApiComponent)
                    return true;
            foreach (AiPathTileSample sample in evidence.Footprint)
                if (sample.Status == "ok" && sample.NativeComponent != sample.ApiComponent)
                    return true;
            return false;
        }

        private static string AnalyzeNearby(Attempt attempt)
        {
            AiNearbyPathEvidence before = attempt.NearbyBefore;
            AiNearbyPathEvidence after = attempt.NearbyAfter;
            if (before == null || after == null) return "nearby-snapshot-unobserved";
            if (before.Status != "ok" || after.Status != "ok")
                return "nearby-snapshot-unavailable:" + before.Status + "/" + after.Status;
            if (HasViewDifference(before) || HasViewDifference(after) ||
                attempt.RouteEvidence != null && attempt.RouteEvidence.Status == "ok" &&
                (attempt.RouteEvidence.SourceComponent != attempt.RouteEvidence.SourceNativeComponent ||
                 attempt.RouteEvidence.TargetComponent != attempt.RouteEvidence.TargetNativeComponent))
                return "native-api-component-view-divergence";
            if (after.ResultX < 0 || after.ResultY < 0) return "no-nearby-result";
            int x = after.ResultX * 5;
            int y = after.ResultY * 5;
            AiPathTileSample preTarget = FindAnchor(before, x, y);
            AiPathTileSample postTarget = FindAnchor(after, x, y);
            if (preTarget == null || postTarget == null ||
                preTarget.Status != "ok" || postTarget.Status != "ok")
                return "candidate-outside-measured-anchor-window";
            if (preTarget.NativeComponent != postTarget.NativeComponent)
                return "component-changed-during-nearby-search";
            if (attempt.RouteEvidence != null && attempt.RouteEvidence.Status == "ok" &&
                postTarget.NativeComponent != attempt.RouteEvidence.TargetNativeComponent)
                return "component-changed-after-nearby-search";
            if (attempt.AnyRegionOverride)
                return "existing-region-hook-changed-result";
            if (attempt.NearbyRegionCallCount == 0)
                return "nearby-region-call-unobserved";
            if (postTarget.NativeComponent == 0 && attempt.AnyRejectedZeroTarget)
                return "nearby-returned-after-rejected-zero-region-call";
            return "nearby-selection-unexplained-by-observed-region-calls";
        }

        private void LogNearbyEvidence(int playerId, long attemptId, Attempt attempt, string inference)
        {
            AiNearbyPathEvidence before = attempt.NearbyBefore;
            AiNearbyPathEvidence after = attempt.NearbyAfter;
            int x = after.ResultX * 5;
            int y = after.ResultY * 5;
            AiPathTileSample preOrigin = FindAnchor(before, after.InputX * 5, after.InputY * 5);
            AiPathTileSample postOrigin = FindAnchor(after, after.InputX * 5, after.InputY * 5);
            AiPathTileSample preTarget = FindAnchor(before, x, y);
            AiPathTileSample postTarget = FindAnchor(after, x, y);
            string signature = playerId + ":" + x + ":" + y + ":" + inference;
            bool full = detailedNearby.Add(signature);
            nearbyCauses.TryGetValue(signature, out int count);
            nearbyCauses[signature] = ++count;
            if (full || count == 2 || count == 10 || count % 100 == 0)
                Log($"AI_BUILD_NEARBY_EVIDENCE: session={sessionId}, tick={lastTick}, " +
                    $"player={playerId}, attempt={attemptId}, input=({after.InputX},{after.InputY}), " +
                    $"result=({after.ResultX},{after.ResultY}), " +
                    $"originBefore={FormatSample(preOrigin)}, originAfter={FormatSample(postOrigin)}, " +
                    $"targetBefore={FormatSample(preTarget)}, targetAfter={FormatSample(postTarget)}, " +
                    $"routeTargetNative={attempt.RouteEvidence?.TargetNativeComponent.ToString() ?? "unobserved"}, " +
                    $"regionCalls={attempt.NearbyRegionCallCount}, recordedRegionCalls={attempt.NearbyRegions.Count}, " +
                    $"observedLast={attempt.LastStep}, " +
                    $"inference={inference}, repeat={count}, fullSamples={full}.");
            if (!full) return;
            foreach (AiNearbyRegionEvidence region in attempt.NearbyRegions)
                Log($"AI_BUILD_NEARBY_REGION: session={sessionId}, player={playerId}, " +
                    $"attempt={attemptId}, components=({region.Source},{region.Target}), " +
                    $"mode={region.Mode}, vanilla={region.VanillaResult}, effective={region.EffectiveResult}.");
            LogSamples("before-anchor", playerId, attemptId, before?.Anchors);
            LogSamples("after-anchor", playerId, attemptId, after.Anchors);
            LogSamples("after-footprint", playerId, attemptId, after.Footprint);
        }

        private void LogSamples(string phase, int playerId, long attemptId,
            IReadOnlyList<AiPathTileSample> samples)
        {
            if (samples == null) return;
            foreach (AiPathTileSample sample in samples)
                Log($"AI_BUILD_PATH_TILE: session={sessionId}, player={playerId}, " +
                    $"attempt={attemptId}, phase={phase}, {FormatSample(sample)}.");
        }

        private static string FormatSample(AiPathTileSample sample) => sample == null
            ? "unobserved" : $"tile=({sample.X},{sample.Y}) id={sample.TileId} " +
              $"native={sample.NativeComponent} api={sample.ApiComponent} status={sample.Status}";

        private static bool CanReach(AiRouteEvidence evidence,
            Func<AiRouteConnection, AiRouteEvidence, bool> eligible)
        {
            var reached = new HashSet<int> { evidence.SourceComponent };
            bool changed;
            do
            {
                changed = false;
                foreach (AiRouteConnection connection in evidence.Connections)
                {
                    if (!eligible(connection, evidence)) continue;
                    if (!reached.Contains(connection.A) && !reached.Contains(connection.B) &&
                        !reached.Contains(connection.C)) continue;
                    if (connection.A > 0) changed |= reached.Add(connection.A);
                    if (connection.B > 0) changed |= reached.Add(connection.B);
                    if (connection.C > 0) changed |= reached.Add(connection.C);
                    if (reached.Contains(evidence.TargetComponent)) return true;
                }
            } while (changed);
            return reached.Contains(evidence.TargetComponent);
        }

        private void LogRouteEvidence(int playerId, long attemptId, AiRouteEvidence evidence, string cause)
        {
            if (evidence == null)
            {
                Log($"AI_BUILD_ROUTE_EVIDENCE: session={sessionId}, player={playerId}, attempt={attemptId}, status=missing.");
                return;
            }
            string signature = playerId + ":" + evidence.SourceComponent + ":" +
                evidence.TargetComponent + ":" + cause;
            bool first = detailedRoutes.Add(signature);
            Log($"AI_BUILD_ROUTE_EVIDENCE: session={sessionId}, tick={lastTick}, player={playerId}, " +
                $"attempt={attemptId}, sourceTile={evidence.SourceTile}, targetTile={evidence.TargetTile}, " +
                $"target=({evidence.TileX},{evidence.TileY}), sourceComponent={evidence.SourceComponent}, " +
                $"targetComponent={evidence.TargetComponent}, " +
                $"sourceNative={evidence.SourceNativeComponent}, targetNative={evidence.TargetNativeComponent}, " +
                $"mode={evidence.QueryMode}, " +
                $"bypass={evidence.Bypass}, playerAccessToken={evidence.PlayerToken}, " +
                $"records={evidence.Connections.Count}, status={evidence.Status}, inference={cause}, " +
                $"fullRecords={first}.");
            if (!first) return;
            foreach (AiRouteConnection connection in evidence.Connections)
            {
                if (connection.A <= 0 && connection.B <= 0 && connection.C <= 0 &&
                    connection.Active == 0) continue;
                Log($"AI_BUILD_ROUTE_RECORD: session={sessionId}, player={playerId}, attempt={attemptId}, " +
                    $"id={connection.Id}, active={connection.Active}, open={connection.Open}, " +
                    $"class={connection.ConnectionClass}, owner={connection.Owner}, building={connection.BuildingId}, " +
                    $"components=({connection.A},{connection.B},{connection.C}), " +
                    $"ownerAccessToken={connection.OwnerToken}, gateFlag={connection.GateFlag}, " +
                    $"eligibleInference={IsVanillaEligible(connection, evidence)}.");
            }
        }

        private sealed class Attempt
        {
            internal Attempt(int playerId) { PlayerId = playerId; }
            internal int PlayerId;
            internal int SearchX = -1;
            internal int NearX = -1;
            internal bool RouteSeen;
            internal int RouteResult;
            internal AiRouteEvidence RouteEvidence;
            internal AiNearbyPathEvidence NearbyBefore;
            internal AiNearbyPathEvidence NearbyAfter;
            internal readonly List<AiNearbyRegionEvidence> NearbyRegions =
                new List<AiNearbyRegionEvidence>();
            internal int NearbyRegionCallCount;
            internal bool AnyRegionOverride;
            internal bool AnyRejectedZeroTarget;
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
                    string routeCauseText = "";
                    foreach (KeyValuePair<string, int> cause in routeCauses)
                        if (cause.Key.StartsWith(playerId + ":", StringComparison.Ordinal))
                            routeCauseText += cause.Key.Substring(2) + "=" + cause.Value + ",";
                    Log($"AI_BUILD_SUMMARY: session={sessionId}, phase={phase}, tick={lastTick}, " +
                        $"player={playerId}, lord={players.GetAILord(playerId)}, scheduler={schedulerCalls[playerId]}, " +
                        $"woodBuild={woodBuildCalls[playerId]}, woodSearch={woodSearchCalls[playerId]}, " +
                        $"initialHuts={initialHuts[playerId]}, newHuts={hutSpawns[playerId]}, " +
                        $"lastObserved={lastObservedStage[playerId] ?? "none"}, " +
                        $"inference={InferStage(playerId)}, attemptOutcomes={outcomeText}, " +
                        $"routeCauses={routeCauseText}.");
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
                case "route-evidence":
                    return $"status={record.RouteEvidence?.Status ?? "missing"}, sourceComponent={record.A}, " +
                        $"targetComponent={record.B}, sourceNative={record.RouteEvidence?.SourceNativeComponent}, " +
                        $"targetNative={record.RouteEvidence?.TargetNativeComponent}, " +
                        $"sourceTile={record.C}, targetTile={record.D}";
                case "wood-nearby-path-before":
                case "wood-nearby-path-after":
                    return $"status={record.NearbyPathEvidence?.Status ?? "missing"}, " +
                        $"input=({record.A},{record.B}), result=({record.C},{record.D})";
                case "near-region-result":
                    return $"components=({record.A},{record.B}), " +
                        $"mode={record.NearbyRegionEvidence?.Mode}, vanilla={record.C}, effective={record.D}";
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
