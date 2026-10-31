using AIBuildDiagnoseTest;
using static CanarariWoodTest.NearbyWoodEvidence;
using APIShared;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop.Enums;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
namespace CanarariWoodTest
{
    internal sealed unsafe class CanarariWoodRuntime
    {
        private const string ProbeSaveName = "test_canari_nowoodcutters_probe.sav";
        private const int ProbePlayer = 6, ProbeX = 345, ProbeY = 485;
        private const int NearbyCopyMaxApplications = 12;
        private readonly ManualLogSource log;
        private readonly ulong nativeModuleBase;
        private readonly bool placementProbeEnabled, nearbyWoodTestEnabled;
        private readonly FarmSiteContractProbe farmContractProbe;
        private readonly CanariFarmSwapProbe canariFarmSwapProbe;
        private readonly Dictionary<long, Attempt> attempts = new Dictionary<long, Attempt>();
        private readonly Action<string> diagnosticWriter;
        private readonly bool[] nearbyDetailedByPlayer = new bool[9];
        private bool active, probeSession, probePending, probeDone, probeRunning;
        private bool nearbyCopySession, nearbyCalibrated, nearbyTestDisabled;
        private int nearbyCopyApplications, nearbyAlternativeTraces, nearbyCalibrationAttempts, lastTick;
        private long sessionId, probeAttemptId, probeSpawnId;
        internal CanarariWoodRuntime(ManualLogSource logger, ulong moduleBase, bool placement, bool nearby, bool farms, bool swap,
            Action<string> writer = null)
        {
            log = logger;
            diagnosticWriter = writer ?? (value => Shared.DebugLogHelper.LogInfo(log, value));
            nativeModuleBase = string.Equals(Shared.DebugLogHelper.CurrentNativeSha256,
                NativeDiagnosticContracts.SupportedHash, StringComparison.OrdinalIgnoreCase) ? moduleBase : 0;
            placementProbeEnabled = placement && nativeModuleBase != 0;
            nearbyWoodTestEnabled = nearby && nativeModuleBase != 0;
            farmContractProbe = new FarmSiteContractProbe(farms, nativeModuleBase, Log, ReadResources);
            canariFarmSwapProbe = new CanariFarmSwapProbe(swap && nativeModuleBase != 0, Log);
        }
        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            sessionId = session.SessionId;
            lastTick = -1;
            active = !session.IsEditor && !session.IsReplay;
            attempts.Clear();
            probePending = probeDone = probeRunning = false;
            probeAttemptId = probeSpawnId = 0;
            nearbyCalibrated = nearbyTestDisabled = false;
            nearbyCopyApplications = nearbyAlternativeTraces = nearbyCalibrationAttempts = 0;
            Array.Clear(nearbyDetailedByPlayer, 0, nearbyDetailedByPlayer.Length);
            bool namedCopy = active && DisposableProbePolicy.IsNamedCopy(session.IsLoadedSave, session.IsEditor,
                session.IsReplay, session.SaveFileName, ProbeSaveName);
            probeSession = namedCopy && placementProbeEnabled;
            nearbyCopySession = namedCopy && nearbyWoodTestEnabled;
            farmContractProbe.OnSessionStarted(session);
            canariFarmSwapProbe.OnSessionStarted(session);
            Log($"AI_BUILD_PROBE_ARMED: session={sessionId}, armed={probeSession}, requiredSave={ProbeSaveName}, player={ProbePlayer}, target=({ProbeX},{ProbeY}).");
            Log($"AI_BUILD_NEARBY_TEST_ARMED: session={sessionId}, armed={nearbyCopySession}, calibrationRequired=true.");
        }
        internal void OnSessionEnded()
        {
            farmContractProbe.OnSessionEnded(); canariFarmSwapProbe.OnSessionEnded();
            active = probeSession = nearbyCopySession = probePending = false;
            attempts.Clear();
        }
        internal void OnTick(int tick)
        {
            if (!active) return;
            lastTick = tick;
            farmContractProbe.OnTick(tick, ReadPathGeneration());
            canariFarmSwapProbe.OnTick(tick, ReadPathGeneration());
            if (probePending)
                try { RunPlacementProbe(); }
                catch (Exception ex) { probePending = false; probeDone = true; probeRunning = false; Log("AI_BUILD_PROBE_FAILED_CLOSED: " + ex); }
        }
        internal bool ShouldDeferWoodBuild(int player) => canariFarmSwapProbe.ShouldDeferWoodBuild(player);
        internal void OnNativeRecord(AiBuildDiagnosticRecord record)
        {
            canariFarmSwapProbe.OnNativeRecord(record);
            if (!active || record.PlayerId != ProbePlayer || record.AttemptId == 0 || (!probeSession && !nearbyCopySession)) return;
            if (record.Stage == "wood-build-before") attempts[record.AttemptId] = new Attempt(record.PlayerId);
            if (!attempts.TryGetValue(record.AttemptId, out Attempt attempt)) return;
            if (record.Stage == "route-result") { attempt.RouteSeen = true; attempt.RouteResult = (int)record.A; }
            if (record.Stage == "wood-nearby-path-before")
            { attempt.NearbyBefore = record.NearbyPathEvidence; PrepareNearbyWoodShadow(record.AttemptId, attempt); }
            if (record.Stage == "wood-nearby-path-after")
            { attempt.NearbyAfter = record.NearbyPathEvidence; CompleteNearbyWoodShadow(record.AttemptId, attempt); }
            if (record.Stage != "wood-build-after") return;
            if (probeSession && !probeDone && !probePending && attempt.RouteSeen && attempt.RouteResult == 0 &&
                attempt.NearbyAfter != null && attempt.NearbyAfter.ResultX * 5 == ProbeX && attempt.NearbyAfter.ResultY * 5 == ProbeY)
            {
                probeAttemptId = record.AttemptId; probePending = true;
                Log($"AI_BUILD_PROBE_QUEUED: session={sessionId}, attempt={probeAttemptId}, nextTick=true.");
            }
            attempts.Remove(record.AttemptId);
        }
        internal void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            farmContractProbe.OnBuildingSpawn(args); canariFarmSwapProbe.OnBuildingSpawn(args);
            if (probeRunning && args.PlayerId == ProbePlayer && args.Building == eStructs.STRUCT_WOODCUTTERS_HUT)
            {
                if (args.Phase == EventHookPhase.Post) probeSpawnId = args.ReturnValue;
                Log($"AI_BUILD_PROBE_SPAWN: session={sessionId}, attempt={probeAttemptId}, phase={args.Phase}, tile=({args.TileX},{args.TileY}), buildingId={args.ReturnValue}.");
            }
        }
        internal void OnBuildStructure(BuildStructureEventArgs args)
        {
            if (probeRunning && args.PlayerId == ProbePlayer && args.Mappers == eMappers.MAPPER_WOODSMAN)
                Log($"AI_BUILD_PROBE_STRUCTURE: session={sessionId}, attempt={probeAttemptId}, phase={args.Phase}, tile=({args.TileX},{args.TileY}), scale={args.BuildingScaleUnknown}, free={args.IsFree}.");
        }
        private void Log(string value) => diagnosticWriter(value);
        private void QueueDiagnostic(string value, bool preserve = false) => Log(value);
        private sealed class Attempt
        {
            internal Attempt(int playerId) { PlayerId = playerId; }
            internal readonly int PlayerId;
            internal bool RouteSeen, NearbyOverlayApplied;
            internal int RouteResult;
            internal AiNearbyPathEvidence NearbyBefore, NearbyAfter;
            internal NearbyWoodShadow NormalNearby, AlternativeNearby;
        }

        private void PrepareNearbyWoodShadow(long attemptId, Attempt attempt)
        {
            int playerId = attempt.PlayerId;
            if ((uint)(playerId - 1) >= 8 ||
                (nearbyDetailedByPlayer[playerId] && !(nearbyCopySession && playerId == ProbePlayer)))
                return;
            bool detailed = !nearbyDetailedByPlayer[playerId];
            if (detailed) nearbyDetailedByPlayer[playerId] = true;
            attempt.NormalNearby = ReplayNearbyWood(attempt.NearbyBefore, false, detailed);
            if (nearbyCopySession && playerId == ProbePlayer &&
                nearbyCopyApplications < NearbyCopyMaxApplications)
                attempt.AlternativeNearby = ReplayNearbyWood(attempt.NearbyBefore, true, false);
            if (attempt.AlternativeNearby != null && nearbyAlternativeTraces < 2)
            {
                nearbyAlternativeTraces++;
                foreach (string line in attempt.AlternativeNearby.CandidateTrace)
                    QueueDiagnostic($"AI_BUILD_NEARBY_COPY_CANDIDATE: session={sessionId}, " +
                        $"attempt={attemptId}, player={playerId}, {line}", true);
                QueueDiagnostic($"AI_BUILD_NEARBY_COPY_PREDICTION: session={sessionId}, " +
                    $"attempt={attemptId}, player={playerId}, normal=({attempt.NormalNearby.X}," +
                    $"{attempt.NormalNearby.Y}), predicted=({attempt.AlternativeNearby.X}," +
                    $"{attempt.AlternativeNearby.Y}), masked={attempt.AlternativeNearby.MaskedCandidates.Count}, " +
                    $"visited={attempt.AlternativeNearby.Visited}, valid={attempt.AlternativeNearby.Valid}, " +
                    $"probeEligible={attempt.AlternativeNearby.ExpectedFootprint?.NoMeasuredBlockers ?? false}, " +
                    $"error={attempt.AlternativeNearby.Error}, predictedFootprint=" +
                    $"{attempt.AlternativeNearby.ExpectedFootprint?.Describe() ?? "unavailable"}.", true);
            }
            if (detailed)
            {
                foreach (string line in attempt.NormalNearby.Trace)
                    QueueDiagnostic($"AI_BUILD_NEARBY_SHADOW_VISIT: session={sessionId}, attempt={attemptId}, " + line, true);
                QueueDiagnostic($"AI_BUILD_NEARBY_SHADOW_FIRST: session={sessionId}, attempt={attemptId}, " +
                    $"player={playerId}, " +
                    $"input=({attempt.NearbyBefore?.InputX},{attempt.NearbyBefore?.InputY}), " +
                    $"normal=({attempt.NormalNearby.X},{attempt.NormalNearby.Y}), " +
                    $"alternative=({attempt.AlternativeNearby?.X},{attempt.AlternativeNearby?.Y}), " +
                    $"excludedAnchorOrBit4={attempt.AlternativeNearby?.MaskedCandidates.Count ?? 0}, " +
                    $"normalValid={attempt.NormalNearby.Valid}:{attempt.NormalNearby.Error}, " +
                    $"alternativeValid={attempt.AlternativeNearby?.Valid.ToString() ?? "unobserved"}:" +
                    $"{attempt.AlternativeNearby?.Error}, visited={attempt.NormalNearby.Visited}, " +
                    $"traceOmitted={attempt.NormalNearby.TraceOmitted}, " +
                    $"footprintsOmitted={attempt.NormalNearby.FootprintOmitted}.", true);
            }
        }

        private void CompleteNearbyWoodShadow(long attemptId, Attempt attempt)
        {
            AiNearbyPathEvidence after = attempt.NearbyAfter;
            NearbyWoodShadow expected = attempt.NearbyOverlayApplied
                ? attempt.AlternativeNearby : attempt.NormalNearby;
            if (expected == null) return;
            bool match = expected != null && expected.Valid && after != null &&
                expected.X == after.ResultX && expected.Y == after.ResultY;
            QueueDiagnostic($"AI_BUILD_NEARBY_SHADOW_COMPARE: session={sessionId}, attempt={attemptId}, " +
                $"player={attempt.PlayerId}, overlay={attempt.NearbyOverlayApplied}, " +
                $"expected=({expected?.X},{expected?.Y}), vanilla=({after?.ResultX},{after?.ResultY}), " +
                $"match={match}, route-and-spawn=reported-by-AI_BUILD_ATTEMPT.", true);
            if (attempt.PlayerId != ProbePlayer || !nearbyCopySession || nearbyTestDisabled) return;
            if (!attempt.NearbyOverlayApplied && !nearbyCalibrated)
            {
                nearbyCalibrationAttempts++;
                if (match && attempt.NormalNearby.X == 69 && attempt.NormalNearby.Y == 97)
                { nearbyCalibrated = true; QueueDiagnostic("AI_BUILD_NEARBY_TEST_CALIBRATED: normal shadow matches first Vanilla result.", true); }
                else { nearbyTestDisabled = true; QueueDiagnostic("AI_BUILD_NEARBY_TEST_DISABLED: first Vanilla result or expected Canari target mismatched.", true); }
            }
            else if (attempt.NearbyOverlayApplied && !match)
            { nearbyTestDisabled = true; QueueDiagnostic("AI_BUILD_NEARBY_TEST_DISABLED: overlay result differed from shadow.", true); }
        }

        internal Action BeginNearbyWoodOverlay(ulong state, int playerId, int x, int y)
        {
            if (!nearbyCopySession || !nearbyCalibrated || nearbyTestDisabled ||
                nearbyCopyApplications >= NearbyCopyMaxApplications ||
                playerId != ProbePlayer || !AiBuildDiagnostic.TryGetCurrentWoodAttempt(
                    out long attemptId, out int owner) || owner != playerId ||
                !attempts.TryGetValue(attemptId, out Attempt attempt) ||
                attempt.NearbyBefore == null || attempt.NearbyBefore.InputX != x ||
                attempt.NearbyBefore.InputY != y || attempt.NormalNearby == null ||
                !attempt.NormalNearby.Valid || attempt.NormalNearby.X != 69 ||
                attempt.NormalNearby.Y != 97 || attempt.AlternativeNearby == null ||
                !attempt.AlternativeNearby.Valid ||
                attempt.AlternativeNearby.X < 0 || attempt.AlternativeNearby.Y < 0 ||
                attempt.AlternativeNearby.MaskedCandidates.Count == 0 ||
                attempt.AlternativeNearby.ExpectedFootprint == null ||
                !attempt.AlternativeNearby.ExpectedFootprint.NoMeasuredBlockers) return null;
            var changed = new List<long>();
            try
            {
                NearbyFootprint expectedLive = CaptureNearbyFootprint(
                    attempt.AlternativeNearby.X, attempt.AlternativeNearby.Y);
                if (!expectedLive.NoMeasuredBlockers ||
                    !string.Equals(expectedLive.Tiles,
                        attempt.AlternativeNearby.ExpectedFootprint.Tiles, StringComparison.Ordinal))
                    throw new InvalidOperationException("Predicted clear footprint changed before overlay.");
                foreach (NearbyMaskedCandidate candidate in attempt.AlternativeNearby.MaskedCandidates)
                {
                    int index = candidate.Index;
                    int cx = index / 160, cy = index % 160;
                    int[] prior = ParseCoarseBytes(attempt.NearbyBefore.GetCapturedCoarseCell(cx, cy));
                    long cell = checked((long)state + 0x5B834 + (long)index * 0x30);
                    var live = new byte[16];
                    Marshal.Copy(new IntPtr(cell), live, 0, live.Length);
                    bool same = prior != null && prior[0] == 0;
                    if (same)
                        for (int i = 0; i < live.Length; i++)
                            if (live[i] != prior[i]) { same = false; break; }
                    if (!same)
                        throw new InvalidOperationException($"Coarse memory changed before overlay at ({cx},{cy}).");
                    NearbyFootprint liveFootprint = CaptureNearbyFootprint(cx, cy);
                    if (!liveFootprint.ExcludeForCopyProbe ||
                        !string.Equals(liveFootprint.Tiles, candidate.FootprintTiles,
                            StringComparison.Ordinal))
                        throw new InvalidOperationException($"Masked footprint changed before overlay at ({cx},{cy}).");
                    changed.Add(cell);
                    Marshal.WriteByte(new IntPtr(cell), 1);
                }
                attempt.NearbyOverlayApplied = true;
                nearbyCopyApplications++;
                QueueDiagnostic($"AI_BUILD_NEARBY_TEST_APPLIED: session={sessionId}, attempt={attemptId}, " +
                    $"player={playerId}, input=({x},{y}), expected=({attempt.AlternativeNearby.X}," +
                    $"{attempt.AlternativeNearby.Y}), masked={changed.Count}, " +
                    $"application={nearbyCopyApplications}/{NearbyCopyMaxApplications}, " +
                    $"reasons={string.Join("|", attempt.AlternativeNearby.MaskedCandidates.ConvertAll(c =>
                        $"({c.Index / 160},{c.Index % 160}):{c.Reason}"))}, " +
                    "onlyForeignCounter=0-to-1; restoreScheduledAfterSingleVanillaCall=true.", true);
                if (nearbyCopyApplications == NearbyCopyMaxApplications)
                    QueueDiagnostic($"AI_BUILD_NEARBY_TEST_LIMIT_REACHED: session={sessionId}, " +
                        $"applications={nearbyCopyApplications}; further calls are observation-only.", true);
                return () => RestoreNearbyWoodOverlay(changed, attemptId);
            }
            catch (Exception ex)
            {
                RestoreNearbyWoodOverlay(changed, attemptId);
                nearbyTestDisabled = true;
                QueueDiagnostic($"AI_BUILD_NEARBY_TEST_ABORTED: attempt={attemptId}, reason={ex}.", true);
                return null;
            }
        }

        private void RestoreNearbyWoodOverlay(List<long> changed, long attemptId)
        {
            bool drift = false, failed = false;
            foreach (long address in changed)
            {
                try { if (Marshal.ReadByte(new IntPtr(address)) != 1) drift = true; }
                catch { drift = true; }
                // Restore every touched cell even if another read or write fails.
                try { Marshal.WriteByte(new IntPtr(address), 0); }
                catch { failed = true; }
            }
            if (drift || failed) nearbyTestDisabled = true;
            QueueDiagnostic($"AI_BUILD_NEARBY_TEST_RESTORED: session={sessionId}, attempt={attemptId}, " +
                $"count={changed.Count}, unexpectedCounterChange={drift}, restoreFailed={failed}.", true);
        }

        private void RunPlacementProbe()
        {
            probePending = false;
            probeDone = true; // Never retry after a native call, including an exception.
            if (!active || !probeSession ||
                !GamePlayerManagerAPI.Instance.IsAIPlayer(ProbePlayer) ||
                !GameTileManagerAPI.Instance.IsTileInsideMapBounds(ProbeX + 2, ProbeY + 2))
            {
                Log($"AI_BUILD_PROBE_SKIPPED: session={sessionId}, attempt={probeAttemptId}, " +
                    "reason=session-player-or-footprint-changed.");
                return;
            }
            if (GameTileManagerAPI.Instance.TileManager.UsePlacementBlockedOverride ||
                !AiBuildDiagnostic.TryReadPlacementStatus(out int prePreparation,
                    out int preRejected, out int preMode) || prePreparation != 0)
            {
                Log($"AI_BUILD_PROBE_SKIPPED: session={sessionId}, attempt={probeAttemptId}, " +
                    "reason=placement-override-or-preparation-state.");
                return;
            }
            IReadOnlyList<AiPathTileSample> before =
                AiBuildDiagnostic.CaptureTiles(ProbeX - 1, ProbeY - 1, 5, 5);
            string resourcesBefore = ReadResources(ProbePlayer);
            if (before.Count != 25)
            {
                Log($"AI_BUILD_PROBE_SKIPPED: session={sessionId}, attempt={probeAttemptId}, " +
                    "reason=tile-snapshot-unavailable.");
                return;
            }
            Log($"AI_BUILD_PROBE_BEGIN: session={sessionId}, tick={lastTick}, attempt={probeAttemptId}, " +
                $"player={ProbePlayer}, anchor=({ProbeX},{ProbeY}), mapper=0x33, scale=3, " +
                $"variant=15, free=false, bypassPlacementRules=false, " +
                $"preparation={prePreparation}, rejected={preRejected}, mode={preMode}, {resourcesBefore}.");
            LogFootprintArea("probe-before-footprint-and-ring", ProbePlayer,
                probeAttemptId, ProbeX, ProbeY, before);
            long callResult = 0;
            string failure = "none";
            try
            {
                probeRunning = true;
                // Exact 0x51540 -> 0x6D580 construction parameters, in the disposable save only.
                callResult = GameBuildingManagerAPI.Instance.CreatePrefab(ProbePlayer,
                    ProbeX, ProbeY, eMappers.MAPPER_WOODSMAN, 3, 15, false, false);
            }
            catch (Exception ex) { failure = ex.ToString(); }
            finally { probeRunning = false; }
            IReadOnlyList<AiPathTileSample> after =
                AiBuildDiagnostic.CaptureTiles(ProbeX - 1, ProbeY - 1, 5, 5);
            LogFootprintArea("probe-after-footprint-and-ring", ProbePlayer,
                probeAttemptId, ProbeX, ProbeY, after);
            string access = "unobserved-no-spawn";
            if (probeSpawnId > 0 && probeSpawnId <= int.MaxValue)
            {
                var point = GameBuildingManagerAPI.Instance.GetAccessPosition((int)probeSpawnId);
                access = $"({point.X},{point.Y})";
                if (GameTileManagerAPI.Instance.IsTileInsideMapBounds(point.X, point.Y))
                {
                    IReadOnlyList<AiPathTileSample> accessTile =
                        AiBuildDiagnostic.CaptureTiles(point.X, point.Y, 1, 1);
                    LogSamples("probe-actual-access", ProbePlayer, probeAttemptId, accessTile);
                }
            }
            bool changed = false;
            if (after.Count == before.Count)
                for (int i = 0; i < before.Count; i++)
                    if (before[i].PropertyFlags != after[i].PropertyFlags ||
                        before[i].BuildingId != after[i].BuildingId ||
                        before[i].Organism != after[i].Organism ||
                        before[i].NativeComponent != after[i].NativeComponent)
                    { changed = true; break; }
            string placement = AiBuildDiagnostic.TryReadPlacementStatus(out int preparation,
                out int rejected, out int mode)
                ? $"preparation={preparation}, rejected={rejected}, mode={mode}"
                : "placementStatus=unavailable";
            Log($"AI_BUILD_PROBE_RESULT: session={sessionId}, attempt={probeAttemptId}, " +
                $"callResult={callResult}, spawnId={probeSpawnId}, access={access}, " +
                $"tileChanged={changed}, {placement}, resourcesBefore=[{resourcesBefore}], " +
                $"resourcesAfter=[{ReadResources(ProbePlayer)}], exception={failure}, " +
                $"observedPlacement={(probeSpawnId > 0 ? "spawned" : "no-spawn")}.");
        }

        private static string ReadResources(int playerId)
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId,
                out GamePlayerResources* resources) || resources == null)
                return "resources=unavailable";
            return $"gold={resources->r_TotalGoodsGold}, woodLogs={resources->r_TotalGoodsWoodLogs}, " +
                $"woodPlanks={resources->r_TotalGoodsWoodPlanks}";
        }

        private int ReadPathGeneration()
        {
            if (nativeModuleBase == 0) return -1;
            try
            {
                return Marshal.ReadInt32(new IntPtr(checked((long)nativeModuleBase + 0x60AD660 + 0x74)));
            }
            catch (Exception ex)
            {
                Log("AI_BUILD_PATH_GENERATION_READ_FAILED: " + ex.GetType().Name);
                return -1;
            }
        }

        private void LogFootprintArea(string phase, int playerId, long attemptId,
            int anchorX, int anchorY, IReadOnlyList<AiPathTileSample> samples)
        {
            if (samples == null) return;
            foreach (AiPathTileSample sample in samples)
            {
                bool footprint = sample.X >= anchorX && sample.X < anchorX + 3 &&
                    sample.Y >= anchorY && sample.Y < anchorY + 3;
                string role = sample.X == anchorX && sample.Y == anchorY ? "anchor-footprint" :
                    footprint ? "footprint" : "perimeter-possible-access";
                Log($"AI_BUILD_PLACE_TILE: session={sessionId}, player={playerId}, " +
                    $"attempt={attemptId}, phase={phase}, role={role}, " +
                    $"apparentBuildingFree={(sample.Status == "ok" && sample.BuildingId == 0)}, " +
                    $"{FormatSample(sample)}.");
            }
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
            ? "unobserved" : sample.Status == "components-only"
            ? $"tile=({sample.X},{sample.Y}) native={sample.NativeComponent} api={sample.ApiComponent} " +
              "tileLayers=unobserved status=components-only"
            : $"tile=({sample.X},{sample.Y}) id={sample.TileId} " +
              $"native={sample.NativeComponent} api={sample.ApiComponent} " +
              $"rawFlags=0x{sample.PropertyFlags:X8} swamp={((sample.PropertyFlags & 0x20000000u) != 0)} " +
              $"wall={IsActualWall(sample.PropertyFlags)} " +
              $"tree={((sample.PropertyFlags & (uint)TilePropertyFlag.IsTree) != 0)} " +
              $"appleFarm={((sample.PropertyFlags & (uint)TilePropertyFlag.IsAppleFarm) != 0)} " +
              $"type={sample.TileType} organism={sample.Organism} occupancy={sample.Occupancy} " +
              $"height={sample.Height} buildingId={sample.BuildingId} " +
              $"wallOwner={sample.WallOwner} status={sample.Status}";



    }
}
